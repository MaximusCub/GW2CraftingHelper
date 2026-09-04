> **Frozen record - 2026-08-16, branch `source-selection-simplification-adversarial-review-2`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Source selection simplification: adversarial-review fix round 2 (5 findings) (2026-08-16)

A further adversarial code review of the round-1 fix round above found 5
Critical/Must-Fix defects (two of them only-half-fixed round-1 items) plus
several nice-to-haves. All 5 fixed on branch `source-selection-
simplification`, plus one cheap same-file nice-to-have.

**1 - Weighted tooltip wording still blamed "currency values" for
pure-gold gaps (round-1 finding #2 only half-fixed).** Round-1's
`HasNonCoinCost` fired whenever either side's `CostLines` was non-empty -
but `PlanSolver.BuildCraftCostBreakdown` emits a Type == "Item" line for
EVERY craft ingredient regardless of valuation (TP-priced, never
user-valued), so any craft-vs-TP comparison had non-empty `CostLines`
purely from its ingredient list. `HasNonCoinCost` now checks for a Type
== "Currency" line specifically (`PillSubduingEvaluator.HasCurrencyLine`)
- the only `CostLine` kind a `CurrencyValuation` can ever price. Tests:
`PillSubduingEvaluatorTests.Weighted_ItemLinesOnlyNoCurrencyLine_HasNonCoinCostFalse`
/ `Weighted_CurrencyLinePresent_HasNonCoinCostTrue`,
`PlanSolverPillSubduingTests.WeightedCraftLosing_PureGoldNoValuation_HasNonCoinCostFalse`
(real Solve()-path, the exact reported TP-400c-vs-craft-500c shape).

**2 - costDiagnostics still recorded the competency-unfiltered craft cost
(round-1 finding #3 fixed only the recursion half of the same
divergence).** `PlanSolver.Evaluate` still wrote `costDiagnostics[node.NodeId]
= (buyTotalCost, bestComparableCraftCost ?? bestFallbackCraftCost)` -
ignoring competency entirely, always the numerically cheapest recipe in
each tier - while the real decision path commits
`craftBreakdownDecisionValue`/`autoPickCraftRealCost` (competency-
resolved). `OwnedMaterialsForceBuyPrePass`'s 85% rule was therefore
derived from a craft cost the real solve could never actually commit to
whenever competency demoted the pick to a costlier competent sibling
recipe (or excluded craft entirely). Fixed by moving the write to AFTER
competency resolution and changing the recorded figure to
`craftBreakdownDecisionValue ?? autoPickCraftRealCost` - the exact
tier/competency-resolved pair the Craft commit sites use. Test:
`PlanSolverForceBuyOnlyTests.
CostDiagnostics_CompetencyResolved_UsesCompetentRecipeNotCheapestOverall`.

**3 - Weighted subdued on a 1-copper margin; the margin must be
decisive.** Round 1 deferred this decision and shipped the bare
strictly-positive-margin behavior live (not merely "documented as
deferred"), so a genuinely near-equal alternative rendered in Locked's
muted gray and was told it was "more expensive". Gated with an
absolute-AND-relative floor (`PillSubduingEvaluator.IsDecisiveMargin`):
the margin must clear BOTH a 100-copper (1 silver) absolute floor AND a
1% relative floor of the selected option's own value - requiring both
(not either) is the more conservative reading, since a margin that only
clears one measure (e.g. 101c on a 10g/100000c purchase - past the
absolute floor but only 0.1%) still is not "decisive" by the other. No
specified numbers exist for either constant - these are a
deliberately modest, easily-tunable starting point, not a precisely-
derived figure. Tests: `PillSubduingEvaluatorTests.
Weighted_OneCopperMarginOnMultiGoldPurchase_NotDecisive_NotSubdued`,
`Weighted_MarginClearsAbsoluteButNotRelativeFloor_NotSubdued`,
`Weighted_MarginClearsRelativeButNotAbsoluteFloor_NotSubdued`,
`Weighted_MarginClearsBothFloors_Subdued`,
`Weighted_SelectedValueZero_AnyPositiveMarginClearingAbsoluteFloorIsDecisive`.

**4 - three parallel reference-equality ternary chains had to stay in
sync by hand (future merge hazard).** `PlanSolver.Evaluate` resolved
`autoPickCraftOption` via a 4-term `??` chain, then re-derived "which
bucket did it come from" three more times (for `craftBreakdownDecisionValue`,
`autoPickCraftRealCost`, `autoPickRecipeId`) via independent reference-
equality ternary chains against the same four `best*Option` variables -
correct today, but a future edit to the `??` precedence (or a fifth
bucket) could silently desynchronise them, producing a Commit with one
recipe's cost and another recipe's RecipeId with no test catching it.
Collapsed into a single `PlanSolver.CraftAutoPickCandidate` (a small
readonly struct holding Option/RealCost/ComparisonValue/RecipeId),
resolved once via an if/else-if chain, with the other three values read
straight off the one resolved candidate. Pure refactor - existing
behavior (and the full pre-existing test suite) unchanged; no new test
needed beyond the suite continuing to pass.

**5 - competency demotion inside the craft arm had no user-visible
explanation for two shapes (round-1 finding #7 accepted this exact gap,
closed only for the all-untrained case).** `CraftExcludedByCompetency` is
only true when NO option in EITHER tier is competent, so two real shapes
raised the plan's cost silently: (a) the cheapest COMPARABLE recipe is
untrained but a competent recipe exists only in the FALLBACK tier -
`craftBreakdownDecisionValue` becomes null and craft never enters the
comparable-tier PickCheapest race at all, TP/vendor commits, nothing
explains why; (b) a costlier competent SIBLING recipe wins Craft over a
cheaper untrained one - `CraftExcludedByCompetency`'s own "Decision ==
Craft -> nothing to report" precedent incorrectly suppressed this, even
though the user never got the cheap recipe. Added a second, independent
field set - `Decision`/`SolverDecision`/`CraftingTreeNode.
CheapestCraftUntrained`/`CheapestCraftRealCost`/`CheapestCraftDisciplines`/
`CheapestCraftMinRating` - true whenever the numerically cheapest raw
craft candidate overall (`bestComparableOption ?? bestFallbackOption`,
same tier priority as `autoPickCraftOption` but WITHOUT the competent-
first override) is untrained, independent of whether the AUTOMATIC pick
itself got excluded. Deliberately does NOT drive
`craftExcludedFromAutoPick` or any other decision-affecting behavior -
purely additive display data, same as `CraftExcludedRealCost` before it.
`CompetencyOpportunityCalculator` now reads these new fields instead of
the narrower `CraftExcludedByCompetency` pair; the existing `Decision !=
Craft` guard was DROPPED (the delta-based check - SubtreeCost strictly
greater than the cheap recipe's real cost - subsumes it: a manual
override or an automatic pick landing on that SAME cheap recipe always
makes the delta exactly 0). `CraftExcludedByCompetency` and its own
fields are UNCHANGED and still drive the real `craftExcludedFromAutoPick`
behavioral gate - only the notification/Plan-Notes path was
re-pointed. Tests: `CompetencyOpportunityCalculatorTests.
CraftUsingACostlierCompetentSiblingRecipe_StillReported`,
`PlanSolverCraftCompetencyTests.
FallbackTierCompetentRecipe_CheaperComparableUntrained_ReportsOpportunity`
(shape a, full Solve+CraftingTreeBuilder+CompetencyOpportunityCalculator
round trip), `CostlierCompetentSiblingWinsCraft_CheaperUntrainedSibling_ReportsOpportunity`
(shape b, same round trip) plus updated assertions on the existing
`MultiRecipeNode_OneCompetentOneNot_CompetentSiblingAutoWinsOverExcludedCheaperOne`.

**Nice-to-have folded in (same file):** `docs/KNOWN-ISSUES.md` finding #3's
own entry above claimed `characterDisciplines` was threaded "at all 3
real call sites in `CraftingPlanPipeline` ... and the pre-pass's own
re-run inside `ResolveWithOverrides`" - there is no such re-run; a grep
for `ComputeForceBuyOnlyNodeIds(` shows exactly 2 real call sites
(`CraftingPlanPipeline.cs:270` and `:770`), the override path reuses the
frozen `context.ForceBuyOnlyNodeIds`. Corrected in place above.

**Deliberately NOT applied** (each explicitly still needing a decision
this round did not make): the Subdued pill's missing "why" tooltip on
the non-interactive path (`TreeSectionController.cs:1157` - a Views file,
outside this round's Services/Models scope); a manual override to Craft
still commits `bestComparableRecipeId`/`bestComparableCraftRealCost`
(possibly the untrained recipe) while the CRAFT pill's own displayed
breakdown uses `autoPickCraftOption` (the competent one) - a real
display/commit mismatch, but changing WHICH recipe a manual override
commits is a behavioral decision, not a display-only fix, and needs
an explicit decision before changing; Plan Notes wording for
MinRating 0 / 3+ disciplines joined by "or" (`PlanViewModelBuilder.cs` -
untouched this round); persisted-plan JSON size (`PlanStoreHelpers.cs` -
untouched this round, still unmeasured); per-render
`PillSubduingEvaluator` allocation (`TreeSectionController.cs`'s
`RenderDecisionPills` - untouched this round); `NonLevelableDisciplineTags`
triplication (still deferred, per round-1).

**Files touched:** `Services/PlanSolver.cs`, `Services/SolverDecision.cs`,
`Services/PillSubduingEvaluator.cs`, `Services/CraftingTreeBuilder.cs`,
`Services/CompetencyOpportunityCalculator.cs`,
`Models/CraftingTreeNode.cs`, `docs/KNOWN-ISSUES.md` (this file).

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
GW2CraftingHelper.csproj -p:Platform=x64` - clean, 0 errors (StyleCop
warnings only, all pre-existing patterns, none new - verified by diffing
warning output for the touched files specifically).

Tests (measured, `"/mnt/c/Program Files/dotnet/dotnet.exe" test
tests/GW2CraftingHelper.Tests/GW2CraftingHelper.Tests.csproj`): 1631
total, 0 failures (up from round-1's 1619 - 12 new tests added this
round, no existing test deleted).

Gate: not yet run live - Blish-bound rendering (Plan Notes rows for the
two newly-reported competency shapes, the now-gated Weighted subduing
tooltip) has not been visually confirmed in a running Blish HUD client,
same constraint every UI-adjacent entry in this file notes.
Gate: not yet run live - queued for the next batched desktop session (recipe-sheet savings row and seasonal-tip negative check are explicit scenarios). Merged after the full review pipeline resolved every finding (verification's docs-staleness hold corrected in 30d66de), under the standing merge directive (2026-08-16).
