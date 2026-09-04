> **Frozen record - 2026-08-17, branch `merged-ceil-remainder-largest-remainder`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Merged-ceil remainder: largest-remainder apportionment + display-layer narrowing fix (2026-08-17)

**Milestone goal:** quorum verdict C6 (TARGETED_FIX_ONLY plus the
judge's own new finding) on the `merged-ceil-remainder` stream, which
enters `VendorBatchSolver` - a former high-evidence/freeze zone
(retired 2026-08-17) - so per that retirement's own terms,
characterize the current behavior in tests BEFORE changing it, then
fix, then prove improved-X/regressed-nothing.

**What changed:**
1. **Characterization commit (`25fc887`).** Pinned
   `AllocateVendorNodeCosts`' pre-fix "UnitCost * quantity per non-last
   occurrence, last occurrence absorbs the entire remaining balance"
   shape before touching it: the unbounded equal-quantity case (a "100
   for 1000c" bulk offer split 1+1 rendered 10/990), the sum invariant,
   and three real downstream consumers -
   `CompetencyOpportunityCalculator` (real Solve()+
   `CraftingTreeBuilder`+calculator round trip),
   `RecipeSheetSavingsCalculator` (fixture bridging the same
   arithmetic), and `SellSideEconomics.ApplyBatchSellSideEconomics`/
   `CraftingProfit` (real Solve() round trip). Every assertion is
   commented with the exact number the fix commit re-baselines it to.
2. **Fix commit (`938f6c9`).** Replaced that shape with largest-
   remainder (Hamilton) apportionment: each occurrence's floor share is
   `step.TotalCost * quantity / totalQuantity`, and the leftover
   (always strictly fewer coppers than there are occurrences - a
   standard apportionment identity, proven in the commit message) goes
   one each to the occurrences with the largest fractional remainder,
   ties broken by first-seen (DFS) order. Divergence between any two
   equal-quantity occurrences is now bounded to <=1 copper (was
   unbounded). The flagship 179-unit/"3 for 3"-Laurel regression shape
   (quantities 4/4/4/83/84, hand-verified floors 4/4/4/83/84 + the one
   leftover copper landing on the 84-quantity occurrence via its 84/179
   fraction, the largest) is **unchanged**: still 4/4/4/83/85 summing
   to 180. Re-baselined the four pinned characterization tests plus one
   pre-existing (not new) test that turned out to depend on the old
   skewed shape (`MultiItemPlanTests.
   GenerateStructuredAsync_TwoItems_SharedBulkVendorMaterial_
   BothTradable_...`: two symmetric roots sharing a "5 for 20 coin"
   material used to split 8/12 by tree-position accident, now split
   evenly 10/10).
3. **New bug (judge-found, real, unrelated to the vendor-batch math):
   `Services/PlanViewModelBuilder.cs` `BuildCurrencyTableRows` narrowed
   `CurrencyCost.Amount` (long) to int with a plain `(int)` cast. Past
   `int.MaxValue` this silently wraps NEGATIVE, and
   `fullyCovered = owned >= required` then reads true for almost any
   owned amount - the opposite of what a currency requirement that
   large should show. Class-swept (grepped Services/Models/Views for
   any other unchecked long-to-int narrowing of an Amount/TotalCost/
   UnitCost/Count-shaped field): this was the only one. Fixed with
   `ClampToInt` (clamp to `int.MaxValue`), the identical convention
   `VendorBatchSolver.ClampToInt` already uses for the same class of
   risk. New boundary test
   (`PlanViewModelBuilderSummaryTests.
   CurrencyTable_AmountExceedsIntRange_ClampsRatherThanWrapsNegative`)
   confirmed reproducing the bug pre-fix and passing post-fix.
4. **C6(b) currencyMap "overstates" claim - verified NOT a bug.** The
   quorum verdict named a prior claim that a Conflict-tier vendor
   step's `currencyMap` accumulation "overstates" cost. Searched this
   repo exhaustively (`docs/KNOWN-ISSUES.md`, `docs/ARCHITECTURE.md`,
   `docs/gw2e-considerations.md`, `docs/research/gw2e-convergence-
   matrix.md`, every other tracked doc, and code comments across
   `Services/`/`Models/`) plus every sibling worktree on this machine
   (`wt-hezone`, `wt-qp1`, `wt-valuedetail`) for the exact wording or
   any equivalent ("double-count", "inflate", "overcounts") tied to
   `currencyMap`/Conflict - found no such claim anywhere accessible to
   this stream. Recording the correct verdict here as the authoritative
   reference regardless, so any surviving reference elsewhere resolves
   against this entry: a Conflict-tier step (two tree occurrences that
   genuinely prefer different vendor offers - see
   `PlanSolverVendorBatchingTests.
   MultiOccurrenceDifferentWinningOffers_LeavesPerOccurrenceSumUnmerged`)
   never runs through `AllocateVendorNodeCosts` at all
   (`VendorOfferOutputCount` stays 0, guarded out at that method's own
   entry). Its `currencyMap`/`Required` total is exactly the sum of
   each occurrence's own genuinely-different, individually-correct
   currency cost - which is also exactly the shopping list's summed
   `PlanStep.Quantity` and the sum of the real tree leaves' own
   `Decision.TotalCost` (152 coin in that test's own 1-for-2 + 100-for-
   150 shape: 2 + 150 = 152, matching `vendorStep.TotalCost`,
   `plan.TotalCoinCost`, and `result.Decisions[tree.NodeId].TotalCost`
   all at once). This is correct, not an overstatement: there is no
   single true merged offer to ceil across two occurrences that
   genuinely used different offers, so forcing one would misrepresent
   the real purchases rather than fix anything. Changing Conflict-tier
   `currencyMap` to disagree with `Required`/the shopping list/the tree
   leaves would create the real internal inconsistency the alternative
   claim would have introduced.
5. **Review-fix commit (`0b60ceb`).** A follow-up review found the
   class sweep for item 2/938f6c9's largest-remainder apportionment had
   missed a second runtime path: `PlanSolver.RecomputeComparisonValues`'
   currency-equivalent share loop still used the deleted "last
   occurrence absorbs the remainder" shape, letting `ComparisonValue`
   diverge from the corrected `TotalCost` by up to `step.Quantity - 1`
   copper for a merged step. Converted to the same largest-remainder
   (Hamilton) apportionment `AllocateVendorNodeCosts` uses. Also fixed
   the caller comment describing the deleted shape, a dangling renamed-
   test reference, one (of several) self-contradicting "DO-NOT-TOUCH"
   line, the flagship regression test's explanatory comment, and added
   a three-equal-quantity-occurrence tie-break test. This runtime change
   shipped with no new characterization pin of its own - see item 6.
6. **Review-response commit (this one).** A further review on `0b60ceb`
   found: (a) item 5's `RecomputeComparisonValues` rewrite was still
   unpinned - the only test touching that path asserted the summed
   `ComparisonValue` across occurrences, which is identical under both
   the old and new algorithm for its 2x qty-1 shape, so the actual
   per-occurrence divergence was never exercised; added
   `MultiOccurrenceMergedVendorOffer_ValuedCurrency_
   ComparisonValueDivergesPerOccurrenceUnderOldSharingRule` (two qty-3
   occurrences, currency value 10 not evenly divisible by total
   quantity 6 - old algorithm gives 3/7, new gives 5/5) to close that
   gap. (b) item 5's DO-NOT-TOUCH sweep fixed only one of five stale
   instances (`VendorBatchSolver.cs:873`); the remaining four
   (`VendorBatchSolver.cs` class doc, `PlanSolver.cs` class doc, and two
   more `PlanSolver.cs` call-site comments) still asserted the merged-
   ceil arithmetic was frozen/unchanged when this stream had already
   rewritten it - corrected all four to note the 2026-08-17 retirement
   instead. (c) both largest-remainder apportionment sites
   (`AllocateVendorNodeCosts` and `RecomputeComparisonValues`) multiply
   a `long` total by an occurrence's `int` quantity without an overflow
   guard; on a large-enough total the product silently wraps negative,
   breaking the "shares always sum to the total" invariant both doc
   comments assert unconditionally. Widened both multiply/divide sites
   to `decimal` (whose range comfortably covers any `long` x `int`
   product either field's own type can hold), removing the overflow
   risk entirely rather than just documenting it as a limitation.

**Validation performed:**
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
  C:/Dev/Blish/wt-qceil/GW2CraftingHelper.csproj -p:Platform=x64` -
  0 errors. StyleCop warnings are pre-existing project-wide (1789 at
  HEAD, none new in this commit's own diff), but MEASURED across the
  whole `merged-ceil-remainder` stream (ce64423 baseline vs. this
  commit's HEAD, full rebuild): `PlanSolver.cs` 134->142,
  `VendorBatchSolver.cs` 50->54, `PlanViewModelBuilder.cs` 158->160 -
  +14 new SA1512/SA1513/SA1515 warnings introduced across the stream's
  five commits (mostly comment-blank-line spacing in the new blocks),
  correcting item 3/4/5's repeated "none in any touched file" claim,
  which was false for this stream from `0b60ceb` onward.
- Tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test
  C:/Dev/Blish/wt-qceil/tests/GW2CraftingHelper.Tests/
  GW2CraftingHelper.Tests.csproj` - MEASURED 1775/1775 green (1774 at
  `0b60ceb` + 1 new per-occurrence characterization test from item 6a),
  correcting the stale "1773/1773" count this entry previously carried
  (actual count at `0b60ceb` was already 1774/1774, one more than
  recorded, from the tie-break test item 5 added).
- Self-review (Code Reviewer Mode) on all runtime-affecting edits: the
  `decimal` widening cannot itself overflow for any `long`/`int` pair
  either field's own type can hold (long max ~9.2e18 x int max ~2.1e9
  ~= 1.98e28, decimal max ~7.9e28); truncation back to `long` after the
  divide is exact since both operands are whole coppers; the new test's
  expected 5/5 vs. the old algorithm's 3/7 was hand-verified against
  both algorithms' own arithmetic before asserting it.

**Repo Invariants Checklist:**
- [x] No Blish HUD references added to tests
- [x] Tests exercise real production paths (all characterization/
  regression tests are genuine `Solve()`+builder round trips, not
  mirrored logic)
- [x] No fake file I/O tests introduced
- [x] Pricing logic preserves multi-source correctness (Conflict-tier
  currency handling explicitly re-verified unchanged per item 4 above;
  the overflow fix changes no value for any realistic input, only
  removes a wrap-around failure mode)
- [x] IDs remain internal-only (not displayed)

**Risks / follow-ups:** none new. The C6(b) correction (item 4) is
recorded here as the authoritative verdict since no prior claim was
locatable to edit in place; if the original claim exists elsewhere,
that record should be updated to point back here rather than restate
the (incorrect) claim.
`VendorBatchSolver.cs:409`'s comment recounting a past review's own
"one of the six DO-NOT-TOUCH merged-ceil batching methods" wording was
left as-is (it accurately describes history at the time, not a present-
tense claim about current code) - flagged here in case a future sweep
disagrees.

**Merge note (post-review):** `origin/master` had moved
(the `high-evidence-zones` stream immediately above this entry, deleting
5 dead/vacuous tests unrelated to this stream's own changes) since this
stream's own 1775/1775 count above was measured at its pre-merge HEAD
(`81598bf`). Merged with `git merge origin/master`; only this file
conflicted (both streams appended an entry at the same location) and was
resolved both-sides, master-first, as the two entries above. All other
files (`Models/PlanViewModel.cs`, `Services/PlanContentHeightMath.cs`,
`Services/SummarySectionLayoutMath.cs`, `Views/CraftingPlanView.cs`,
`Views/Rendering/NotesSectionRenderer.cs`,
`Views/Rendering/SummarySectionRenderer.cs`, `docs/ARCHITECTURE.md`,
`docs/gw2e-considerations.md`,
`docs/research/gw2e-convergence-matrix.md`,
`tests/GW2CraftingHelper.Tests/Services/PlanContentHeightMathTests.cs`)
merged automatically with no conflict; grepped the merged
`VendorBatchSolver.cs`/`PlanSolver.cs` afterward and confirmed this
stream's largest-remainder/decimal-widened apportionment logic is
present unchanged post-merge. Rebuilt clean (0 errors) and re-ran the
full suite post-merge: 1770/1770 passed - exactly this stream's own
1775 baseline minus the 5 tests `high-evidence-zones` deleted (1775 - 5
= 1770), confirming the two streams' changes compose without
interaction. This 1770/1770 count supersedes the 1775/1775 figure in
this entry's own "Validation performed" section above, which remains
accurate as a historical record of this stream's state at `81598bf`
before the merge.

Gate: not applicable - quorum-verdict cleanup with characterization-first proof where the high-evidence zone was entered; suite-pinned. Merged under the standing merge directive (2026-08-16).

**Second merge note:** `origin/master` moved again (PR #131,
`value-detail-pipeline`, the entry immediately above this one - test-and-
docs only, no runtime code touched) while this branch's own PR #130 was
polling CI. Merged with `git merge origin/master` a second time; again only
this file conflicted (both streams appended at the same location),
resolved both-sides, master-first, as the two entries above.
`tests/GW2CraftingHelper.Tests/Services/CraftingPlanPipelineTests.cs`
merged automatically with no conflict. Rebuilt clean (0 errors) and re-ran
the full suite: 1772/1772 passed - exactly the first merge note's
1770/1770 plus the 2 new tests `value-detail-pipeline` added
(`GenerateStructuredAsync_CraftRootWithVendorChildValuedInCuratedCurrency_
VomOn_ValueDetailTooltipFires`,
`GenerateStructuredAsync_CraftRootSelectedAmongMultipleOptions_
ValueDetailTooltipFires`), confirming the three streams' changes compose
without interaction. This 1772/1772 count supersedes both this entry's own
1775/1775 figure and the first merge note's 1770/1770 figure.
