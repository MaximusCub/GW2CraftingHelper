> **Frozen record - 2026-08-16, branch `source-selection-simplification-adversarial-review`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Source selection simplification: adversarial-review fix round (8 findings) (2026-08-16)

A follow-up adversarial code review of the `source-selection-simplification`
work above (competency-aware default + subdued losing pills) found 8
Critical/Must-Fix defects plus several nice-to-haves. All 8 fixed on
branch `source-selection-simplification`, plus the cheap same-file
nice-to-haves.

**1 - competency gate inspected only the single cheapest recipe option.**
`PlanSolver.Evaluate` now tracks the best COMPETENT option per tier
(`bestCompetentComparableOption`/`bestCompetentFallbackOption`) alongside
the existing unfiltered `bestComparable`/`bestFallback` pair -
`craftExcludedFromAutoPick` (competency branch) fires only when NO option
in EITHER tier is competent. `canCraft` and the manual-override branch
still read the unfiltered pair, unchanged. `autoPickCraftOption`/
`craftBreakdownDecisionValue`/`autoPickCraftRealCost`/`autoPickRecipeId`
resolve to whichever of the four (comparable/fallback x competent/raw)
buckets actually applies, so PickCheapest, both Craft `Commit` sites, and
`BuildCraftCostBreakdown` all operate on the SAME recipe. Test:
`PlanSolverCraftCompetencyTests.
MultiRecipeNode_OneCompetentOneNot_CompetentSiblingAutoWinsOverExcludedCheaperOne`.

**2 - Weighted subduing wording blamed "currency values" for pure-gold
gaps.** Added `PillSubduingResult.HasNonCoinCost` (true when either
side's `CostLines` is non-empty), computed in `PillSubduingEvaluator`.
`PillSubduingTooltipBuilder` now says plain "More expensive (N more)"
when no currency was ever involved. Nice-to-have folded in (same file):
StrictDomination's "same currencies" claim was also wrong whenever the
union treated a missing kind as 0 on the selected side - reworded to
"needs everything the selected option needs, plus ...". Tests:
`PillSubduingEvaluatorTests.Weighted_PureCoinBothSides_HasNonCoinCostFalse`,
`PillSubduingTooltipBuilderTests.Weighted_PureCoinDifference_NoCurrencyMentioned`.

**3 - force-buy pre-pass's throwaway solve was competency-UNAWARE.**
`OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds` gained a
`characterDisciplines` parameter, threaded from `effectiveCharacterDisciplines`
at both real call sites in `CraftingPlanPipeline` (single-item and
batch). Test:
`OwnedMaterialsForceBuyPrePassTests.
ChildIngredientNotCraftable_CharacterDisciplinesThreaded_ChangesForceBuyResult`
(the same tree/prices produce a DIFFERENT force-buy result depending
solely on whether this parameter is passed).
Correction (round-2 adversarial review, 2026-08-16): the sentence above
originally claimed "all 3 real call sites ... and the pre-pass's own
re-run inside `ResolveWithOverrides`" - there is no such re-run. A grep
for `ComputeForceBuyOnlyNodeIds(` shows exactly 2 real call sites
(`CraftingPlanPipeline.cs:270` and `:770`); the override path reuses the
already-frozen `context.ForceBuyOnlyNodeIds` instead of recomputing it.
Stale guidance, now corrected in place.

**4 - StrictDomination compared post-reduction craft quantities against
un-reduced vendor quantities.** Added `PillSourceCostBreakdown.
RawQuantitiesReducedByOwnedStock`, set by `PlanSolver.
AnyIngredientReducedByOwnedStock` (reference-keyed lookup against
InventoryReducer's own `OwnedQuantityUsedByNode`, threaded through a new
`PlanSolver.Solve`/`Evaluate` parameter). `PillSubduingEvaluator` skips
StrictDomination (only - Weighted is unaffected, its DecisionValue
already reflects real discounted economics) whenever either side is
flagged. Threaded at the 3 real solve call sites; NOT threaded for
`ResolveWithOverrides`' frozen-tree branch (no fresh reduction there to
source a reference-keyed dictionary from) - documented gap, not a
regression (this check did not exist there before either).

**5 - craft breakdown silently dropped GuildUpgrade/unrecognized
ingredients.** Added `PillSourceCostBreakdown.IsIncomplete`, set by
`BuildCraftCostBreakdown` whenever an ingredient has no representable
line. `PillSubduingEvaluator` refuses BOTH rules when either side is
incomplete (same conservative posture as
`VendorComponentCostsUnreliable`). Nice-to-have folded in (same file):
corrected the "Count is always >= 1" doc claim on
`PillSourceCostBreakdown` (false for an owned-stock-reduced-to-0
ingredient line).

**6 - the "genuine alternative" guard counted a fallback-tier vendor
offer.** The competency-exclusion guard now requires
`buyTotalCost.HasValue || comparableVendorValue.HasValue` (a real
COMPARABLE alternative), not `canBuyTp || canBuyVendor` (which is also
true for an unvalued-currency-only offer). Without this, a node with a
fully-priced but untrained craft and only a karma-only vendor offer
would silently default onto the unvalued vendor purchase, dropping the
real priced cost from the plan. Test: `PlanSolverCraftCompetencyTests.
NonCompetentAccount_OnlyAlternativeIsFallbackTierVendor_
StillAutoCraftsRatherThanDroppingCost`.

**7 - the competency flip had no user-visible explanation (design-law
gap).** Added `SolverDecision.CraftExcludedByCompetency`/
`CraftExcludedRealCost`/`CraftExcludedDisciplines`/`CraftExcludedMinRating`
(passthrough from a new `PlanSolver.Decision` set of fields, straight
through `CraftingTreeNode`), and a new `CompetencyOpportunityCalculator`
(same shape/placement precedent as `ExcessCraftOutputCalculator`) that
walks the built display tree for a node where craft was excluded on
competency grounds, did NOT end up crafted anyway (a manual override to
Craft is excluded - nothing to report, the user already chose), and
would genuinely have been cheaper. Writes
`CraftingPlanResult.CompetencyOpportunities`, rendered by
`PlanViewModelBuilder.BuildNotesSection` as a new Plan Notes bucket
("{item}: could be crafted for less - no character has {discipline}
{rating}"), per the design law (opportunities go to
Plan Notes with concrete numbers). Tests:
`CompetencyOpportunityCalculatorTests` (8 cases: basic delta, manual-
override suppression, not-excluded, cost-neutral, reference-branch
exclusion, cross-occurrence dedup, null/empty), plus a real pipeline
round-trip in `CraftingPlanPipelineTests.
GenerateStructuredAsync_CraftExcludedByCompetency_PopulatesCompetencyOpportunities`.

**8 - a partial character-fetch failure could leave `CharacterDisciplines`
non-null.** `Gw2AccountSnapshotService`'s outer
`catch (Exception ex) when (!(ex is OperationCanceledException))` around
the per-character loop now nulls `snapshot.CharacterDisciplines`
explicitly - before this fix, anything escaping the loop (WhenAll
faulting, a `.Result` rethrow) left whatever partial list had already
been gathered, which read as an affirmative "not trained on any
character" for every character the loop never reached. No test added:
this class directly references `Blish_HUD`/`Gw2Sharp` types
(`Gw2ApiManager`), which the repo's test invariants forbid importing
into any test file - the fix is a one-line, low-risk null-out with no
production code path this repo's test suite is permitted to exercise.

**Files touched:** `Services/PlanSolver.cs`, `Services/SolverDecision.cs`,
`Services/PillSubduingEvaluator.cs`, `Services/PillSubduingTooltipBuilder.cs`,
`Services/OwnedMaterialsForceBuyPrePass.cs`, `Services/CraftingPlanPipeline.cs`,
`Services/CraftingTreeBuilder.cs`, `Services/Gw2AccountSnapshotService.cs`,
`Services/CompetencyOpportunityCalculator.cs` (new),
`Models/PillSourceCostBreakdown.cs`, `Models/CraftingTreeNode.cs`,
`Models/CraftingPlanResult.cs`, `Models/CompetencyOpportunity.cs` (new).

**Deliberately NOT applied** (each explicitly flagged by the review as
needing an explicit decision, not a unilateral call): Weighted subduing's
"any strictly-positive margin" threshold (the requirement was a decisive
margin, and no number was specified - **superseded, see the round-2 entry below**: a
round-2 finding directed gating this rather than continuing to defer it,
so it is no longer un-signed-off/live-by-default as of that entry);
extracting the now-three-times-duplicated
`NonLevelableDisciplineTags`/`NonCraftingDisciplines`/
`InherentlyAvailableDisciplines` set (flagged for a future pass, not
this one, and STILL not applied in round 2 either); the persisted-plan
JSON size of the 3 new `PillSourceCostBreakdown`s per node (no
measurement taken, no `[JsonIgnore]` added; also still not applied in
round 2).

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
GW2CraftingHelper.csproj -p:Platform=x64` - clean, 0 errors (StyleCop
warnings only, all pre-existing patterns, none new).

Tests (measured, `dotnet test tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj`): 1619 total, 0 failures, at the final
checkpoint (one pre-existing test's own expected string needed updating
for finding 2's wording change - `PillSubduingTooltipBuilderTests.
Weighted_PureCoinDifference_NoCurrencyMentioned`'s own initial FormatCoin
expectation, fixed within the same pass before commit).

Gate: not yet run live - Blish-bound rendering (the new Plan Notes rows,
the reworded subduing tooltips) has not been visually confirmed in a
running Blish HUD client, same constraint every UI-adjacent entry in
this file notes.
