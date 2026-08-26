> **Frozen record - 2026-08-16, branch `guildupgrade-ingredient-costing-display-fix`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## GuildUpgrade ingredient costing/display fix (2026-08-16)

Orchestrator-checksummed audit finding (confirmed via live API): the versioned
GW2 API returns ingredient `{type:"GuildUpgrade", id:<upgradeId>, count:N}` on
Guild Decoration recipes (e.g. recipe 12002 -> item 80471, guild upgrade id
829; 678 occurrences across 225 distinct ids in the current seed). The
"Recipe-ingestion bug class" entry above first surfaced this as a cosmetic
display gap; a deeper audit found it was a real mis-costing bug too,
reachable via `PlanSolver.Evaluate`'s vendor-offer path
(`VendorBatchSolver.EvaluateVendorOffers` keys `vendorOffers` by the raw
ingredient id with no `"Item"`-type gate) - latent in the current seed (no
`GuildUpgrade` id collides with a vendor-offer `outputItemId`, seed `Item`
id, or `KnownCurrencyNames` key) but not enforced by any guard before this
fix.

**Delivered behavior.** A `GuildUpgrade`-typed ingredient, and any OTHER
ingredient type this module does not specifically recognize (a future GW2
API type), is now handled consistently and safely at every site that
touches ingredient types, on an Item-positive basis
(`IngredientType != "Item"`) rather than an enumerated deny-list - so a type
the module has never seen is unpriceable and undisplayable-as-an-item by
construction, not by luck:

- `Models/CraftingDecision.cs` has two new members, `GuildUpgrade` and
  `UnrecognizedIngredient`, both appended LAST (this enum has no
  `StringEnumConverter` and round-trips through `plan.json` as a raw
  ordinal int - inserting either earlier would misread previously-persisted
  plans). Its XML doc comment is the one canonical explanation of the
  id-space rationale (a guild upgrade id, a wallet currency id, and an item
  id are three distinct id spaces with no defined relationship to each
  other) - other sites below point back to it instead of repeating the
  rationale.
- `Services/CraftingTreeBuilder.cs`'s `BuildNode` has three ordered leaf
  branches before the ordinary decision lookup: `"GuildUpgrade"` (generic
  "Guild upgrade (unresolved)" label plus an `AcquisitionHint`
  explanation), `"Currency"` (name/icon resolved through
  `CurrencyDisplayResolver`, not item metadata), and a catch-all for
  anything else (`"Unrecognized ingredient type"` label). All three
  explicitly clear `IconUrl`/`Rarity` (and, for the catch-all,
  `Name`/`AcquisitionHint`/`AcquisitionBadge` too) rather than leaving them
  at the generic item-keyed lookup every node gets by default -
  `metadata`/`hints` can carry a colliding entry for the same raw id via
  routes other than the Item-only ingestion path. Each of the three gets
  its own `CraftingDecision` value (never shares `Unknown` with a genuine
  no-source Item node), because `DecisionPillPlanner` cannot otherwise tell
  them apart from a real "no feasible source" Item leaf and would attach a
  live, interactive IGNORE pill keyed on a non-item id.
- `Services/PlanSolver.cs`'s `Evaluate`, `Collect`, and `RecomputeCraftCosts`
  all guard on `IngredientType != "Item"`. Currency ingredients keep their
  existing valuation-aware pricing; every other non-Item type (GuildUpgrade,
  or an unrecognized type) contributes zero to `craftCost`/`craftRealCost`,
  never touches `currencyValuation`/`GetBuyCost`/vendor offers, never
  accumulates into `plan.CurrencyCosts`, and demotes its containing recipe
  to the fallback tier via the existing `HasUnvaluedCurrency` machinery
  (propagated transitively through Craft ancestors, unchanged).
- `Services/RecipeService.cs`'s `BuildNodeAsync` guards the same way
  (`ingredientType != "Item"`), so a null or empty `IngredientType` - a
  defensive case, not reachable with today's seed data, but a real
  historical shape (commit e81b7e4) - is treated as an unexpanded leaf
  consistently with how `PlanSolver`/`CraftingTreeBuilder` will handle that
  same node, instead of being expanded as if it were a real item.
- `Services/VendorBatchSolver.cs`'s `EvaluateVendorOffers` cost-line
  classification loop has a final `else` alongside its `Currency`/`Item`
  branches: an unrecognized `CostLine.Type` marks the offer unpriceable
  (`priceable = false; break;`) instead of silently contributing nothing
  and letting the offer win at an understated price. The merged-ceil
  batching math below the loop is unchanged.
- `Services/DecisionPillPlanner.cs` gives `GuildUpgrade`/`Currency`/
  `UnrecognizedIngredient` each a single, non-interactive locked pill
  (`GUILD UPGRADE`/`CURRENCY`/`UNRECOGNIZED`) - never the `IGNORE` toggle.
- `Views/Rendering/TreeSectionController.cs` (Blish-bound, untestable
  directly) gives the `Unknown`/`GuildUpgrade`/`UnrecognizedIngredient`/
  `Currency` locked pills real, honest tooltips instead of the generic
  "Only available source" text a locked pill gets by default (accurate
  only when there genuinely is exactly one feasible source).

**Sweep (repo rule: fix the class, not the instance).** Every
`IngredientType`/`RawIngredient.Type`/`CostLine.Type` comparison in
`Services/`/`Models/` was grepped and checked against this Item-positive
shape: `AchievementBitDedupPrePass`, `CraftingPlanPipeline`'s
override/id-collection helpers, and `InventoryReducer` were already
`"Item"`-gated with no changes needed; `CraftingPlanPipeline`'s
vendor-offer-currency-id collectors compare `CostLine.Type` (a different
field with no `"GuildUpgrade"` concept) and are unrelated.

**Tests:** 1396 total (0 failed), spanning `PlanSolverGuildUpgradeTests.cs`,
`CraftingTreeBuilderTests.cs`, `DecisionPillPlannerTests.cs`,
`AmalgamatedRiftEssenceIngestionTests.cs`, `RecipeServiceTests.cs`, and
`PlanSolverVendorOfferTests.cs` - real production code paths
(`PlanSolver.Solve`, `CraftingTreeBuilder.BuildTree`,
`RecipeService.BuildTreeAsync` end-to-end against real `RecipeNode`/
`ItemPrice`/`VendorOffer`/`CurrencyValuation`/`ItemMetadata` fixtures), no
contract-mirror/fake-logic tests, no Blish HUD references.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - PASS, 0
errors. Pre-existing StyleCop analyzer warnings unchanged in nature
(measured via a forced full rebuild; an incremental no-op build prints
0 warnings and must not be quoted as the warning count). The only
warnings on lines this fix added are two instances (SA1513/SA1515,
`Views/Rendering/TreeSectionController.cs:1146-1147`) of the
comment-placement pattern that already warns three times in the same
else-if chain. Tests: `dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj` - 1396 total,
0 failed.

**Remaining / deferred.** This fix makes a `GuildUpgrade` ingredient (and
any other unrecognized ingredient type) safe to price and display - never
mis-costed, never mislabeled as a wallet currency or a real item - but does
not resolve what a `GuildUpgrade` ingredient actually IS. Two pieces remain
unimplemented: (1) the upgrade's real name and icon - the leaf still
renders the generic, ID-free "Guild upgrade (unresolved)" label; the live
GW2 API's `/v2/guild/upgrades/{id}` endpoint is the candidate future source
for both, needing a new metadata service and name/icon cache, neither of
which exist yet; (2) verifying the active character's claimed guild
actually owns/has-unlocked that upgrade, which would need the
authenticated `/v2/guild/:id/upgrades` endpoint plus a guild-membership/
permission concept this module has none of today. Separately,
`RecipeService.BuildNodeAsync` computes every ingredient's node `Quantity`
uniformly as `craftsNeeded * ingredient.Count`, correct for a consumable
Item but not obviously correct for a `GuildUpgrade` requirement, which
behaves like a one-time claimed prerequisite rather than a
per-craft-multiplied consumable; invisible today since no UI surfaces a
`GuildUpgrade` node's `Quantity`, but a future real name/requirement
display will need to decide how (or whether) to show that scaled number
rather than assuming it means "N needed per craft" the way it does for a
real item. `Models/CraftingDecision.cs`'s `GuildUpgrade` doc comment,
`CraftingTreeBuilder.BuildNode`'s `"GuildUpgrade"` branch, and the
branch's own `AcquisitionHint` text all point back to this document for
this remainder.

**Review history** (eight adversarial passes; most-recent fix per topic
only - see git log for full per-commit detail):

1. Initial fix: root-caused the CraftingTreeBuilder mislabel and the
   PlanSolver.Evaluate special-case gap; added the `GuildUpgrade` enum
   member/branches; 11 tests.
2. Adversarial follow-up: corrected a false id-overlap justification and a
   wrong mis-costing mechanism (vendor-offer path, not TP-price path);
   closed the GuildUpgrade branch's IconUrl/Rarity leak; 2 tests.
3. Closed the same IconUrl/Rarity leak for the plain Currency branch (a
   real seed collision on id 24); 2 tests.
4. Inverted every guard from an enumerated deny-list to Item-positive
   (class, not instance); fixed a triplicated Gate line; corrected the
   original finding's RESOLVED marker to PARTIALLY RESOLVED; 6 tests.
5. Completed the unrecognized-type leaf's five-field sweep
   (Name/AcquisitionHint/AcquisitionBadge, not just IconUrl/Rarity);
   corrected a false "one Gate: line" claim to "one PENDING Gate: line";
   1 test extended.
6. Fixed a false memo-contract doc comment on `Evaluate`; hoisted the
   unrecognized-type catch-all before the decisions lookup so it holds by
   construction; 1 test.
7. Gave the unrecognized-type leaf its own `UnrecognizedIngredient`
   decision (it was sharing `Unknown`, which meant it got a live,
   clickable IGNORE pill keyed on a non-item id); 3 tests plus 1 rename.
8. Orchestrator fix-loop: fixed a recurring Gate-line duplication; closed
   the fourth Item-positive guard site (`RecipeService.BuildNodeAsync`);
   closed the structurally identical unrecognized-`CostLine.Type` gap in
   `VendorBatchSolver`; 2 tests.

Gate: PARTIAL PASS 2026-08-16 (orchestrator live desktop session). Solver-side safety fully suite-covered; the GuildUpgrade pill/label visuals were unreachable live (no guild-decoration output is plannable via the search list) - visual slice rides the next natural opportunity.
