## Craft/vendor comparability parity fix (2026-08-15)

Root-caused via user-approved investigation: `Services/PlanSolver.cs`'s
craft-cost path (the recipe loop inside `Evaluate`) silently valued
UNVALUED currency ingredients at ZERO coin while still letting the craft
option compete fully on coin cost in `PickCheapest` - the opposite of how
`Services/VendorBatchSolver.cs`'s `EvaluateVendorOffers` already treats an
unvalued non-coin currency line on a vendor offer (demoted to a
FALLBACK-ONLY tier that never competes on coin cost - see that method's
own doc comment, M33-era). Consequence: a recipe with a heavy, unpriced
currency cost could be declared "cheapest" purely because that cost was
invisible to the comparison, while a vendor offer charging the identical
currency was correctly barred from the same trick. This asymmetry was
about to go live the moment a parallel branch's ~188 restored
currency-ingredient recipes (e.g. Amalgamated Rift Essence: 3 currencies +
50 ectos crafted vs. the same currencies + 60 ectos vendor) started
ingesting.

**The fix: give craft the SAME comparable/fallback tier semantics vendor
already has.** `PlanSolver.Evaluate`'s recipe loop now splits candidate
recipes into two tiers, exactly mirroring `EvaluateVendorOffers`:

- **Comparable** - no `Currency`-type ingredient, or every one has a
  user-provided valuation (`CurrencyValuation`/`ModuleSettings.
  CurrencyValuationsJson`, the same mechanism vendor offers already read).
  Competes on equal footing with TP buy and a comparable vendor offer in
  `PickCheapest`, unchanged from before this fix.
- **Fallback** - at least one `Currency`-type ingredient has NO valuation
  (or its valuation arithmetic overflows - mirrors `EvaluateVendorOffers`'
  identical per-line overflow handling). Still fully offered (`CanCraft`
  stays true, the CRAFT pill still shows - the M33 guarantee is
  preserved unchanged) but never wins the automatic decision against any
  comparable option; used only when NOTHING comparable exists anywhere
  for that node (no TP price, no comparable vendor offer, no comparable
  recipe).

Two tracked "best" candidates (`bestComparableCraftCost`/
`bestFallbackCraftCost`, each with its own real-cost and RecipeId
sibling) replace the old single `bestCraftCost`, with the pre-existing
lowest-RecipeId tie-break now applied per tier. Only the comparable
value is ever passed into `PickCheapest`; a manual per-node override
(`forced == AcquisitionSource.Craft`) uses comparable-first-else-fallback,
mirroring `VendorBatchSolver`'s own override precedence for
`BuyFromVendor`. The terminal fallback branch (previously vendor-only:
"nothing beat buy, but a fallback vendor offer exists") now also
considers a fallback craft, and when BOTH a fallback craft and a
fallback vendor offer exist, applies the exact same tie-break
`PickCheapest` already uses for the comparable tier: the numerically
cheaper of the two wins, an exact tie keeps vendor - "someone must still
be picked," extended from the pre-existing vendor-only fallback
precedent to cover craft's new fallback tier too. A force-buy-only node
(`OwnedMaterialsForceBuyPrePass`) is excluded from the fallback branch
the same way it was already excluded from the primary comparison -
craft stays off every automatic path for that node, not just the
primary one; `OwnedMaterialsForceBuyPrePass`'s own raw-diagnostics
`craftCost` figure now reports the comparable-tier cost when one
exists, else the fallback-tier cost, mirroring the real decision's own
tier priority (previously the single undifferentiated number).

**Item 2 (valued currencies already competed symmetrically) - verified,
no change needed.** A recipe's valued `Currency` ingredient already fed
the craft-vs-buy DECISION value exactly like a vendor offer's valued
currency line does (`PlanSolver.cs`'s pre-existing currency-ingredient
branch in the recipe loop, unchanged by this fix) - this was already
correct and is untouched.

**Item 3 (decision-only valuation - never inflates a displayed coin
total) - verified already correct, now locked by a dedicated test.**
Audited both paths: `Decision.TotalCost`/`craftRealCost` (craft) and
`VendorOfferEvaluation.BestComparableCoinCost`/`totalCoinCost` (vendor)
already excluded valuation-derived coin from every real, committed cost
- both were already documented as intentional (`Decision`'s own doc
comment, "Never includes a valued currency's coin-equivalent"). No
display-layer code (`PlanResultBuilder`, `PlanViewModelBuilder`) touches
cost totals at all - `Plan.TotalCoinCost`/`PlanStep.TotalCost`/
`CraftingTreeNode.SubtreeCost` are the only user-visible coin surfaces
and are all sourced from `Decision.TotalCost`. Pre-existing tests
(`PlanSolverCurrencyValuationTests`,
`PlanSolverCoreDecisionTests.CurrencyIngredient_ValuedButCraftStillWins_RealCostExcludesCurrencyValue`)
already locked the step-level case; a new test
(`PlanSolverCraftVendorComparabilityTests.
ValuedCurrencyIngredient_ComparableCraftWins_PlanTotalCoinCostExcludesValuation`)
locks it explicitly at the `plan.TotalCoinCost` level too. No fix was
needed for this item.

**Item 4 (fallback picks must not display a false coin cheapness) -
verified, no UI change needed.** `DecisionPillPlanner.BuildPillSpecs` and
`CraftingTreeBuilder.BuildNode` are driven purely by
`CanCraft`/`CanBuyTp`/`CanBuyVendor`/`Source`/`TotalCost` - they never
distinguish comparable from fallback tier, and no UI surface anywhere in
the module claims a decision is "cheapest" (grepped `Views/`/`Services/`/
`Models/` for the word - no hits). A fallback craft decision now flows
through the exact same `Commit`/`Decision`/`PlanStep` shape a fallback
vendor decision already used (real coin `TotalCost`, no invented
number), so it already presents identically - reuse, not a new UI path,
per the repo's "reuse existing UI, do not invent new UI" rule.

**Flagged limitation (spec item 5, documented rather than expanded in
scope): a true tie inside the ARE-shaped fallback case.** When a craft
recipe and a vendor offer are both fallback-tier (identical unvalued
currency ingredients on each side) AND their priced/real portions are
ALSO numerically equal, the terminal fallback tie-break has no finer
signal than its existing "exact tie keeps vendor" rule - it cannot
express "these two options are ACTUALLY identical in total real cost
because the currency lines net out," because currencies are ignored
entirely on both sides (decision-only valuation), never compared or
"cancelled" against each other. This is not a regression: the pre-fix
code could not express this either, and a coin/currency exchange rate
would have to be invented to do better, which the repo invariant (avoid
inventing currency exchange rates) rules out. The common case - priced
portions genuinely differ, as in the real Amalgamated Rift Essence
example (50 ectos crafted vs. 60 vendor) - is handled correctly: see
`PlanSolverCraftVendorComparabilityTests.
AmalgamatedRiftEssenceShaped_IdenticalUnvaluedCurrencies_CraftWinsOnRealItemCostDifference`.

**Tests
(`tests/GW2CraftingHelper.Tests/Services/PlanSolverCraftVendorComparabilityTests.cs`,
11 new; plus 3 pre-existing tests updated because they encoded the old
buggy behavior).** New file covers: a fallback craft stays offered
(`CanCraft` true) even when the automatic decision picks buy; a
comparable recipe is chosen over a numerically cheaper fallback recipe
on the same node; multiple-fallback-recipe tie-break by lowest RecipeId;
a valued-currency recipe still competes as comparable and can beat both
a comparable vendor offer and TP buy; `plan.TotalCoinCost` excludes a
winning comparable recipe's currency valuation; both directions of the
all-fallback craft-vs-vendor tie-break (cheaper wins) plus the exact-tie
case (vendor wins); `OwnedMaterialsForceBuyPrePass` exclusion also
blocks the fallback-craft last resort; a manual per-node override still
forces a fallback-only recipe; and the Amalgamated-Rift-Essence-shaped
case itself. The 3 pre-existing tests that encoded the bug
(`PlanSolverCoreDecisionTests.CurrencyIngredient_AppearsInCurrencyCostsNotSteps`,
`PlanSolverCoreDecisionTests.CurrencyIngredient_Unvalued_ContributesZeroToDecisionAndCost`
- renamed to `..._ComparableBuyWins_RegardlessOfFakeZeroCost` and its
assertion flipped to the corrected outcome,
`CraftingTreeBuilderTests.CurrencyNode_ResolvesKnownNames`) were each
updated to remove the item's TP buy price, so the fallback craft is
still exercised as the last resort and each test's real subject
(currency display, not the buy-vs-craft decision itself) still applies -
found via a full-suite run surfacing the regression, then a repo-wide
grep for every `Leaf(..., "Currency")`/`IngredientType = "Currency"`
construction site (fix the class, not the instance) to confirm no
further sibling was missed.

Validation: `dotnet build -p:Platform=x64` clean (0 errors, no new
warnings from any touched file). Module test suite green - 1284 passed
(was 1273; +11 new `PlanSolverCraftVendorComparabilityTests`, 3
pre-existing tests updated in place, net test count unchanged for those
three). No new Blish HUD references in tests; every new/updated test
exercises real production code (`PlanSolver.Solve` end-to-end, real
`RecipeNode`/`RecipeOption`/`VendorOffer` fixtures), no contract-mirror/
fake-logic tests. Item/currency/vendor IDs remain internal-only. Pricing
logic continues to preserve multiple sources and avoid inventing
currency exchange rates - this fix tightens that invariant for craft
rather than relaxing it.

No live desktop gate for this pass (solver-only change, seed data with a
currency-ingredient recipe not yet ingested on this branch - see the
"parallel branch" note above).
