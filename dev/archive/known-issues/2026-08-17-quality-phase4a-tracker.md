> **Frozen record - 2026-08-17, branch `quality-phase4a-tracker`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Quality-audit phase 4a: PlanSolver best-recipe tracker (B9, quality-phase4a-tracker)

**Target:** PlanSolver.Evaluate's recipe-selection loop carried four
copies of the "improve best, tie-break on lowest RecipeId" block over
16 parallel locals (bestComparable*/bestFallback*/
bestCompetentComparable*/bestCompetentFallback*) - the same
parallel-locals shape whose cost/id desync CraftAutoPickCandidate's
doc records.

**Characterization coverage (commit 1):** measured by mutation - ten
mutations (RecipeId tie-break inversion, strict-to-non-strict
improvement swap, and fallback craftCost-for-craftRealCost ranking
desync, each per applicable block) ALL survived the pre-existing
1827-test suite; none of the four tiers' tie-break or ranking behavior
was pinned. Added PlanSolverRecipeSelectionTieBreakTests (10 tests,
real Solve() paths): comparable-tier ties built from a valued-currency
recipe (equal comparison cost, divergent real cost), fallback-tier
desync from an ingredient whose committed ComparisonValue (100)
diverges from TotalCost (50), override-path variants for the raw
(non-competent) bests that only the manual-Craft commit sites read.
One finding en route: RecomputeComparisonValues overwrites every Craft
decision's ComparisonValue post-solve (fallback tier forced to
TotalCost), so the fallback cost slot's only unrecomputed observable
is its RANKING role - the desync tests pin that, not the erased
stored value.

**Mutation kill table (per block: fallback F, competent-fallback CF,
comparable C, competent-comparable CC):**

| Mutation | Before (1827 suite) | After commit 1 | After commit 2 (re-expressed vs tracker) |
|---|---|---|---|
| Tie-break invert F/CF/C/CC | survived x4 | killed x4 (2 fails each) | killed (single Offer site, 8 fails) |
| Strictness swap F/CF/C/CC | survived x4 | killed x4 (1-2 fails each) | killed (single Offer site, 4 fails) |
| Fallback craftCost desync F | survived | killed | killed (call-site arg swap) |
| Fallback craftCost desync CF | survived | killed | killed (call-site arg swap) |

**Refactor (commit 2):** one private nested struct BestRecipeTracker
(Cost/RealCost/RecipeId/Option + Offer(cost, realCost, recipe))
replaced the four blocks; four tracker locals replaced the 16
parallel locals. Preserved exactly: comparison order (strict
improvement, then lowest-RecipeId tie), the fallback tier passing
craftRealCost for BOTH Offer slots, one AccountCanCraft evaluation
per recipe (hoisted; verified still the always-evaluated first
operand at current HEAD before hoisting). VendorBatchSolver's
merged-ceil region untouched (high-evidence zone, out of scope).
Build 0 errors, warning count 1745 -> 1744; suite 1837/1837 green.

Gate: PASS (solver-internal refactor with characterize-first proof -
the tie-break mutations that survived the baseline suite are killed by
the new characterization tests both before and after the tracker
refactor, per the mutation table above; no rendered surface changed,
so no desktop gate applies; review found zero blocking findings).
