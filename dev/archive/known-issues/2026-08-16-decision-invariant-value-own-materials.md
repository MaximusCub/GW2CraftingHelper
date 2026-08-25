## Decision-invariant "Value Own Materials" (VOM, 2026-08-16)

**Bug (audit row 31)**: `InventoryReducer.Reduce` walked the recipe
tree and consumed owned inventory stock **before** `PlanSolver.Solve`
ever decided which nodes would actually be crafted, bought from the
TP, or bought from a vendor - reduction had no idea what the solver
would choose, so it used a price-blind heuristic (only a node's
first-listed recipe option, `node.Recipes[0]`, consumed the pool, and
every visited node was assumed craftable-and-chosen). Two concrete,
confirmed failure modes resulted:

1. **Phantom `UsedMaterials` / understated `CraftingProfit`.** A node
   force-flagged to Buy by the (already-shipped)
   `OwnedMaterialsForceBuyPrePass` 15% force-buy guard still had
   reduction walk its primary recipe and phantom-consume owned stock
   for ingredients that were never actually needed, since the node was
   never crafted - inflating `UsedMaterials` and deducting a phantom
   `MaterialOpportunityCost` from `CraftingProfit` for a branch that
   does not exist in the real plan.
2. **Recipe-option bias.** Only `Recipes[0]` (the option the upstream
   recipe source happens to list first) ever got discounted by owned
   stock, regardless of which option `PlanSolver.Evaluate` would
   actually pick as cheapest at market prices - so owning the FIRST
   option's ingredients could make it look artificially cheaper than
   an objectively cheaper alternative, flipping the solver's own
   choice toward a worse-at-market-prices recipe.

Confirmed via a re-baseline audit of the full existing test suite (see
Fix below): both failure modes were live and untested before this
change - two Valued-mode fixtures' expected numbers changed once the
fix landed, and both were traced to exactly these two bugs, not a
regression (see Tests below).

**Fix (Candidate A - zero-owned decision pass, chosen over two other
designs considered and rejected: a unified solve+reduce rewrite of
`PlanSolver.Evaluate`, and a post-solve `UsedMaterials` reconciliation
filter that only patches the display symptom without fixing the
underlying recipe-option-bias decision bug)**:
`InventoryReducer.Reduce` (both the flat-`Dictionary<int,int>` overload
and the `AccountItemIndex`-sourced production overload) gained an
optional `zeroOwnedDecisions` parameter - the `Decisions` dictionary
from one more throwaway `PlanSolver.Solve` call, on the SAME
zero-owned/unreduced tree `OwnedMaterialsForceBuyPrePass` already
solves for its own force-buy flag, this time with `forceBuyOnlyNodeIds`
applied. `ReduceNode`/`ReduceNodeSourced` now consume the owned-stock
pool down a recipe option only when that node's zero-owned decision was
`Craft` AND `option.RecipeId` matches the chosen option; a node decided
`BuyFromTp`/`BuyFromVendor`/`UnknownSource` lets NO option consume the
pool for its descendants. A `null` guide (every pre-existing caller and
test, and every Free-mode generation) reproduces today's exact
`i == 0`-primary-option heuristic byte-for-byte; a guide missing a
specific NodeId falls back to the same heuristic defensively.
`CraftingPlanPipeline` (both `GenerateStructuredAsync` and
`GenerateStructuredMultiAsync`) moved the existing force-buy pre-pass
ahead of the `Reduce` call (it now must run first, to produce the
guide `Reduce` consumes) and added the new zero-owned decision solve
right after it - both only run when `useForceBuyPrePass` is true (Valued
mode + a live snapshot + a reducer), the same pre-existing gate. Since
discounting only ever lowers a cost, and only along the path the
zero-owned pass already declared the winner, owned stock can never pull
the real (post-reduction) solve toward a chain that is worse at market
prices - it can only make the zero-owned winner an even stronger
winner.

**UI relocation**: `ModuleSettings.ValueOwnMaterials` (a global
Settings-tab checkbox, default true, that only ever drove the 15%
force-buy guard + `MaterialOpportunityCost` display, never reduction
itself) is extended and relocated rather than replaced - `Valued` now
covers both the pre-existing guard and the new decision-invariant
reduction under the same flag, avoiding a second, confusingly-named
toggle. The live control moved inline into
`Views/CraftingPlanView.cs`'s controls panel as a new
`_valueOwnMaterials` per-plan session checkbox (default true), next to
the existing `Use Own Materials`/price-basis controls - session state
like its two neighbors, never read from/written to `ModuleSettings`.
`.Enabled` stays synced to `Use Own Materials` (disabled, not hidden,
when Use Own Materials is off; the last-chosen value is preserved, not
reset). `ModuleSettings.ValueOwnMaterials` itself stays defined
(unused on the live path) purely so an already-persisted `settings.json`
value needs no special handling, mirroring the `ScrollDiagnosticsEnabled`
precedent; the Settings tab now shows an info line instead of a live
checkbox. **Inspection finding, not part of the original design
premise**: the design doc that authored this milestone's plan claimed
`UseOwnMaterials`/`PriceBasis` already had a precedent of restoring
their checkbox's DISPLAYED state across a module restart - inspecting
`Views/CraftingPlanView.cs::ApplyRestoredPlan` during implementation
showed this is not actually true (only `Module.cs`'s own on-disk
round-trip exists; the restored value was not fed back into the live
checkbox).

**Correction (post-review)**: the premise being false was found during
implementation, but `PersistedPlan.ValueOwnMaterials` was then shipped
matching that same non-restoring behavior (round-trips to disk, never
reaches the live checkbox) instead of being wired up - meaning the
field, and the schema bump that came with it, earned nothing: every
user's persisted plan would be discarded on upgrade for a value that
still would not have been restored to the control that mattered.
**Fixed**: `ApplyRestoredPlan` now takes a `valueOwnMaterials`
parameter and sets both `_valueOwnMaterials` and (when the tab has
already been built) `_valueOwnMaterialsCheckbox.Checked` from it -
`Module.cs`'s restore call site threads `_pendingPlanRestore.
ValueOwnMaterials` through. `UseOwnMaterials`/`PriceBasis` keep their
pre-existing (out of scope for this fix) non-restoring behavior - only
the NEW field this milestone added was in scope.

**Schema bump**: `PersistedPlan.ValueOwnMaterials` (new field) bumped
`PersistedPlan.CurrentSchemaVersion` from 1 to 2 - the first real
exercise of this reject-and-regenerate mechanism since it was
introduced. Effect: on first load after this ships, a `SchemaVersion`-1
persisted plan is rejected outright by
`PlanStoreHelpers.DeserializePersistedPlan` (one Warn log line), and
Module falls back to its existing "no restored plan" path (empty
Crafting Plan tab) - a known, already-exercised, safe degrade, not a
crash. **One-time cost**: every user's currently-persisted plan is
discarded on first load after this milestone ships. Now justified by
the fix above (the field is actually restored to the live control), not
by a field nobody reads.

**Re-baseline audit** (full suite run, not assumed-green - exactly two
Valued-mode fixtures' numbers changed, both traced to the audited bug
fix below, not a regression):

- `CraftingPlanPipelineTests.ResolveWithOverrides_ForceBuyPrePass_ManualOverrideStillWins`:
  **correction (post-review)** - an earlier draft of this entry
  re-baselined this test's expected `TotalCoinCost` from 30 to 150 and
  described the new number as "a known, accepted limitation of the
  override-replays-against-a-fixed-tree architecture... not a new
  regression." That framing was WRONG: 30 was the correct, real-world
  number (master already returned it), and 150 was a genuine regression
  this same milestone introduced, not a pre-existing limitation - the
  fixture's root item is force-buy-flagged (zero-owned decision =
  `BuyFromTp`), so the guided reduction correctly never discounts its
  owned ingredient stock down the never-chosen craft branch at
  GENERATION time; but `ResolveWithOverrides` used to replay a manual
  override to Craft against that same frozen, never-discounted tree,
  showing the user a plan to buy 5x item 2 for 150 coin when they
  actually own 4 of the 5 needed and would really spend 30. **Fixed**:
  `PlanSolveContext` now also snapshots the GENERATION-time unreduced
  tree (`UnreducedTree`) and the raw account items/character
  (`AccountItems`/`ActiveCharacterName`) whenever the force-buy pre-pass
  ran. `ResolveWithOverrides` uses them to re-run the SAME zero-owned-
  decision-pass-then-`Reduce` dance `GenerateStructuredAsync` uses at
  generation time, but with `overrides`/`ignoredItemIds` folded into the
  decision pass, so a node an override flips to Craft gets its
  ingredients correctly re-discounted against the user's real owned
  stock. `TotalCoinCost` for this test is back to 30, matching master.
  Falls back to the old frozen-`context.Tree` behavior verbatim
  whenever the pre-pass did not run at generation time (Free mode, or
  no snapshot) - no change to that path. **Cost note**: `PlanSolveContext`
  (persisted to disk verbatim as part of `PersistedPlan.Result.
  SolveContext` - see `PlanStoreHelpers`) now also carries
  `UnreducedTree` whenever the pre-pass ran, roughly doubling the
  tree-shaped portion of a Valued-mode-with-snapshot plan's persisted
  JSON (Metadata/Prices/VendorOffers are unaffected - same reference,
  not duplicated). It also carries `AccountItems` - the raw owned-item
  list `ResolveWithOverrides` rebuilds its `AccountItemIndex` from -
  which the original entry above omitted entirely; for a real account
  this list is plausibly thousands of entries, dwarfing the tree-shaped
  cost. **Post-review fix**: `AccountItems` is now projected down to
  the three fields `AccountItemIndex`'s constructor actually reads
  (`ItemId`/`Count`/`Source`) before being captured -
  `SnapshotItemEntry.Name`/`IconUrl` (a full render-service URL) were
  dead weight nobody downstream of `PlanSolveContext` reads. Still
  O(account item count)
  bytes, not O(tree size); not measured against a real large account,
  and gzip in `PlanStore.Save` mitigates on-disk size but not
  serialize/deserialize CPU. Accepted as the cost of correctness on
  this path, not further optimized.
- `MultiItemPlanTests.GenerateStructuredAsync_MultiItem_ValuedMode_MixedBuyCraftBatch_MaterialOpportunityCostNullForBoughtRootOwnedIngredient`
  (**post-review rename** - the original name,
  `..._MaterialOpportunityCostIsWholeTreeSum`, no longer matched what the
  test asserts once the fix below landed):
  `MaterialOpportunityCost` changed from a non-zero phantom credit to
  `null` throughout (standalone and batch) - the bought root's owned
  craft ingredient is no longer phantom-consumed, directly closing the
  audited row-31 bug. `SellSideEconomics.cs`'s own doc comments (which
  had explicitly documented the old phantom-credit behavior as
  "intentional, not a new gap") were updated to describe the fixed
  behavior.

**New tests** (all exercise real production code paths - `InventoryReducer.Reduce`
directly, or the full `CraftingPlanPipeline`/`PlanStore` pipeline - no
contract-mirror or fake-logic tests):

- `InventoryReducerTests.cs` (7 new): decision-guided non-primary-option
  consumption (converse of the pre-existing
  `MultipleRecipeOptions_OnlyPrimaryOptionConsumesPool`), a Buy-decided
  node's ingredients never consumed (no phantom `UsedMaterials`), a
  Buy-decided node's OWN owned stock still credited (its ingredient's
  Quantity still rescales to the new demand unconditionally - only pool
  CONSUMPTION is guide-gated), a missing-NodeId defensive fallback to
  the legacy heuristic, and `Sourced_` mirrors of the first three
  against the `AccountItemIndex` production overload. Full pre-existing
  suite (39+ facts, all called via the un-guided overloads) stays green
  unchanged, confirming `zeroOwnedDecisions: null` is byte-identical to
  today.
- `CraftingPlanPipelineTests.cs` (2 new):
  `Structured_ValuedMode_ForceBuyPrePass_NoPhantomUsedMaterialsOrOpportunityCost`
  (direct proof of the audited bug fix - `UsedMaterials`/
  `MaterialOpportunityCost` no longer phantom-populated) and
  `Structured_ValuedMode_CompetingRecipeOptions_DecisionInvariant_OwnedStockNeverFlipsChoice`
  (two recipe options where the non-primary option is objectively
  cheaper at zero-owned market prices; fully owns the primary option's
  ingredient - proves the winning choice does not flip toward the
  listed-first option). Two design test-plan bullets are satisfied by
  already-existing/already-updated tests rather than new duplicates:
  the manual-override-still-wins case by (following the correction
  above) `ResolveWithOverrides_ForceBuyPrePass_ManualOverrideStillWins`,
  and the Free-mode regression pin by the pre-existing
  `Structured_FreeMode_SameOwnershipScenario_CraftsFromReducedRemainder`
  (stayed green unchanged, since Free mode never builds a guide).
- `PlanStoreTests.cs` (1 new):
  `LoadLatest_VomSchemaVersion1File_ReturnsNullAndLogsWarn` pins that a
  realistic `SchemaVersion`-1 file (the actual previous
  `CurrentSchemaVersion`, not a synthetic 0) is rejected under the new
  `CurrentSchemaVersion` 2. `Save_Load_RequestAndTimestampRoundTrip`
  extended to assert `ValueOwnMaterials` round-trips independently of
  `UseOwnMaterials`.

**Perf spot-check**: measured via a temporary, non-committed CLI flag
added to `tools/GW2CraftingHelper.Harness` (reverted immediately after
measurement - `git status` confirmed clean before this milestone's
final commit), comparing `--profile 2` (Exordium - a real legendary
precursor tree, offline seed data) in Free mode against Valued mode
with an empty-but-non-null account snapshot (enough to exercise
`useForceBuyPrePass`'s new solve without needing real ownership data),
200 iterations each, warm-median timing:

- Free mode (1 `Solve()` call): Total 10ms, `Solve` line 6ms.
- Valued mode + snapshot (3 `Solve()` calls - the pre-existing
  force-buy-diagnostics solve, the new zero-owned-decision solve, and
  the real post-reduction solve): Total 21ms, `Solve` line (the final,
  named solve only) 5ms - the two untimed extra passes account for the
  remaining ~11ms gap (roughly 5-6ms each, in line with the named
  `Solve` line's own per-call cost).

Net: this design's own new solve pass (one of the three, since the
force-buy-diagnostics solve already existed pre-milestone) adds
roughly one solve-worth of time (~5-6ms) to a real, moderately deep
precursor tree - consistent with the design doc's own risk assessment
("acceptable given documented real tree depths... a dozen levels").
No cross-call memoization exists in `PlanSolver` today (a fresh
`Dictionary` every call), so this cost is linear in tree size and would
scale accordingly on a substantially larger tree than tested here.

**Perf spot-check #2 (post-review, VOM finding #3)**: the spot-check
above measures only the GENERATION path (async, off the UI thread). The
more latency-sensitive path is `ResolveWithOverrides`, reached
synchronously on the MAIN thread by every override pill click (see
`Module.cs`'s own doc comment on that wiring). Measured the same way
(temporary, non-committed `--profile-resolve` flag added to
`tools/GW2CraftingHelper.Harness`, reverted immediately after
measurement - `git status` confirmed clean before this milestone's
final commit), 200 iterations, same Exordium tree, Valued mode +
snapshot (so every click re-runs the guideSolve + re-reduction path -
see `PlanSolveContext.UnreducedTree`'s doc comment):

- `ResolveWithOverrides` median: ~15-16ms per click (empty-but-non-item
  snapshot and a 5000-synthetic-item snapshot measured the same,
  post-cache - see below). Roughly 2-3x a single generation-path
  `Solve()` call's own ~5-6ms, consistent with the design doing a
  guideSolve + `_reducer.Reduce` + the real `Solve()` per click, on top
  of the pre-existing force-buy-diagnostics solve already inside
  `GenerateStructuredAsync` (not repeated per click).
- `AccountItemIndex`'s own constructor, isolated: ~2.05ms per build for
  5000 synthetic entries (`Bank`-sourced, one call per iteration) - a
  real account's item list is plausibly this size (see finding #2's
  cost note above). **Fix applied**: `CraftingPlanPipeline` now caches
  the built `AccountItemIndex` keyed by reference equality on the
  `PlanSolveContext` (see `GetOrBuildAccountItemIndex`'s doc comment) -
  a restored/generated context's `AccountItems` list never changes
  underneath it, so every click after the first against the same
  context skips this ~2ms rebuild entirely, rather than paying it on
  every single pill click. Not measured as a percentage of total click
  latency across a range of account sizes; the isolated 2.05ms figure
  above is the concrete number the cache removes from the repeat-click
  path.

**Known residual (post-review, not guarded/tested)**: the decision-
invariance guarantee above is narrower than earlier drafts of this
entry (and `InventoryReducer`'s own doc comments) claimed. The guide
is computed on the UNREDUCED tree, but a node's OWN Quantity can still
shrink from owned stock of that node's own item id (unrelated to the
guide), and craft cost is non-linear in quantity
(`ComputeCraftsNeeded`'s ceiling division, `VendorBatchSolver`'s
per-batch math) - so shrinking a node's own Quantity can raise its
effective per-unit cost enough to flip the REAL (post-reduction) solve's
decision for THAT node away from what the guide assumed, after its
ingredients were already discounted and written into `UsedMaterials`
against the guide's Craft assumption. This is the audited row-31
phantom-`UsedMaterials` bug re-entering through a second door. Requires
a node with owned stock of ITSELF plus owned stock of its own
ingredients, and a recipe/vendor batch whose output count is greater
than 1 - not exercised by any existing fixture. See
`InventoryReducer.ReduceNodeSourced`'s doc comment for the precise
mechanism.
Left undone (not treated as blocking this milestone) rather than
attempting a fix: closing it properly needs the same
"solve-then-detect-a-flip-then-re-reduce" shape as the `ResolveWithOverrides`
fix above, applied to `GenerateStructuredAsync`/`GenerateStructuredMultiAsync`
themselves - a real design change, not a small guard.

Build: `dotnet build GW2CraftingHelper.csproj -p:Platform=x64` - clean,
0 errors (StyleCop warning count unchanged from before this milestone -
every edited file already carried pre-existing warnings of the same
codes, no new ones introduced by this change's own lines). Tests: 1425
passed, 0 failed (`dotnet test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`)
(**post-review correction**: this entry originally claimed 1420
passed / 10 new, written before commit 582a44a and never refreshed -
measured on HEAD it is 1410 baseline + 15 new: 10 `InventoryReducerTests`
[5 flat + 5 `Sourced_`, including the two StaleRecipeIdInGuide pins] + 3
`CraftingPlanPipelineTests` [including
`Structured_FreeMode_CompetingRecipeOptions_PrimaryOptionOwnedStockFlipsChoice`]
+ 1 `MultiItemPlanTests` + 1 `PlanStoreTests` = 1425). No Blish HUD/
BlishHUD.exe references in any test file; every test exercises a real
production entry point (`InventoryReducer.Reduce`,
`CraftingPlanPipeline.GenerateStructuredAsync`/
`GenerateStructuredMultiAsync`, `PlanStore.LoadLatest`), no contract
mirrors, no fake file I/O. IDs remain internal-only throughout; coin
icons unaffected (pricing/reduction logic only, no coin-rendering code
touched). No live desktop verification was performed -
`Views/CraftingPlanView.cs`'s new inline checkbox is Blish-bound and
outside this repo's test-runnable surface, same constraint every
UI-adjacent entry in this file notes; the checkbox's layout (fixed
x=350, clear of the price-basis dropdown ending at x=328 and the
right-anchored Generate button even at the window's 930x710 minimum
size) was verified by inspection of `ComputeTopRegionLayout`'s
constants only, not a live screenshot.

**Post-review fix pass (VOM findings #1-3 + nice-to-haves)**: fixed the
`PlanStructuralValidator` gap for `UnreducedTree`/`AccountItems` (finding
#1, three new `PlanStoreTests` facts pinning the `UnreducedTree.Recipes`-
null case, the `AccountItems` null-entry case, and the bonus
`UnreducedTree`-set-but-`AccountItems`-null pair check); projected
`PlanSolveContext.AccountItems` down to the three fields
`AccountItemIndex` actually reads before capture, and corrected this
entry's own "Cost note" to mention `AccountItems` at all (finding #2);
measured and documented the `ResolveWithOverrides` UI-thread click path
(see the new Perf spot-check #2 above) and added an `AccountItemIndex`
cache keyed by `PlanSolveContext` reference equality so a repeat click
against the same restored/generated plan skips rebuilding it (finding
#3). Nice-to-haves also taken: renamed the now-misleadingly-named
`MultiItemPlanTests` fact; corrected this section's own stale test count
(above); fixed the one non-ASCII byte in `InventoryReducerTests.cs`;
seeded `CraftingPlanView._valueOwnMaterials` from
`ModuleSettings.ValueOwnMaterials` at construction so a user's prior
choice survives a module reload instead of always resetting to Valued;
widened the "Value Own Materials" checkbox's tooltip to also mention the
15% force-buy guard and the `MaterialOpportunityCost` deduction it
gates; added a comment on `ResolveWithOverrides`' stale
`context.Tree`/`UsedMaterials`/`OwnedQuantityUsedByNodeId` after a
re-reducing re-solve. Build: `dotnet build GW2CraftingHelper.csproj
-p:Platform=x64` - clean, 0 errors. Tests: 1428 passed, 0 failed (1425
+ the 3 new `PlanStoreTests` facts above).

Gate: PASS 2026-08-16 (orchestrator live desktop session). Inline toggle renders next to Use Own Materials (checked default); schema-v2 one-time plan reset consumed the old v1 file cleanly (strip showed Ready, no restored plan); decision behavior suite-covered.
