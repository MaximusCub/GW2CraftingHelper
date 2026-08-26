> **Frozen record - 2026-08-17, branch `high-evidence-zones`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## High-evidence zones: policy rewrite + PlanContentHeightMath dead-code sweep (high-evidence-zones, 2026-08-17)

Two-part change, the first application of the new high-evidence-zone
policy (maintainer, 2026-08-17, replacing the M38-era DO-NOT-TOUCH
freeze - see this file's own policy note near the top).

**Part 1 (policy docs).** Swept `docs/KNOWN-ISSUES.md` and
`docs/ARCHITECTURE.md` for DO-NOT-TOUCH/frozen-file language that
functions as an active, current-state statement (as opposed to a
historical gate/review record narrating what applied to a specific past
change at the time it was made, which was left untouched to preserve the
record): added the policy note near the top of this file; reworded item
23's and the concurrency-audit table's `CraftingPlanView` row's
DO-NOT-TOUCH phrasing to "high-evidence zone (formerly DO-NOT-TOUCH)";
reworded `docs/ARCHITECTURE.md` section 7's two do-not-touch statements
about `VendorBatchSolver.cs`'s merged-ceil arithmetic the same way, and
added the proof requirement to that section. `CLAUDE.md` (in-repo) was
checked and does not carry DO-NOT-TOUCH/frozen-file language, so it
needed no change. Historical narration of specific past PRs' DO-NOT-TOUCH
compliance (roughly two dozen entries throughout this file's numbered
catalog and PR write-ups) was deliberately left in its original wording -
rewriting those would misrepresent what rule actually applied at the
time.

**Part 2 (first application - the freeze-trapped dead code).**
Characterized before touching anything, per the new policy's proof
requirement: grepped every reference to `SummaryBodyHeight` and
`CoinTotal` across `src` and `tests`; confirmed `PlanViewModelBuilder`
never emits `PlanRowType.CoinTotal` and that `Views/CraftingPlanView.cs`
always special-cases `PlanSectionType.Summary` to
`SummarySectionLayoutMath.BodyHeight` before it would ever reach
`PlanContentHeightMath.SectionBodyHeight`, so the private
`SummaryBodyHeight` method (and the `PlanSectionType.Summary` case that
called it) has been unreachable from any live path since W4A. Ran the
full suite first: 1765 passed. Then deleted
`PlanContentHeightMath.SummaryBodyHeight`, its `SectionBodyHeight` switch
case, the `PlanRowType.CoinTotal` enum member, and updated the
comments in `Models/PlanViewModel.cs`, `Services/PlanContentHeightMath.cs`,
`Services/SummarySectionLayoutMath.cs`, `Views/CraftingPlanView.cs`, and
`Views/Rendering/SummarySectionRenderer.cs` that referenced them as
pending deletion or still-live. Characterization surfaced one more dead
test than this file's own W4A follow-up bullet had estimated:
`Summary_CoinRowPlusCurrencyRows` (outside the bullet's ~348-390
estimate) also referenced `PlanRowType.CoinTotal` directly and would not
compile once the member was removed, so it was deleted alongside the
three tests the bullet did name
(`Summary_MultiItemNoteRow_AddsFallbackTextRowHeight`,
`Summary_NoMultiItemNoteRow_UnaffectedByNewBranch`,
`Summary_MultiItemFourCoinRowsPlusNoteRow_StillOneCostTileRowHeight`) -
4 deleted tests total. `Summary_NoCoinRow_OmitsTileRow` does not
reference `CoinTotal`, still compiles, and still passes (the
`PlanSectionType.Summary` case now falls through to
`SectionBodyHeight`'s existing `default` branch, which happens to return
the same value for its one-`CurrencyCost`-row input since
`CurrencyRowHeight`/`FallbackTextRowHeight` are both 28) - initially left
unchanged on the theory that the larger `SummarySectionLayoutMath`
fold-back is out of scope for this branch. Re-ran the full suite after:
1761 passed - exactly baseline (1765) minus the 4 deleted dead tests,
nothing else changed.

**Follow-up correction (same day, code review).** Leaving
`Summary_NoCoinRow_OmitsTileRow` in place was itself a defect, not a
scope call: with the `Summary` switch case gone, the test exercises only
`default`'s `rows.Count * FallbackTextRowHeight` arithmetic, passing
solely because `CurrencyRowHeight` and `FallbackTextRowHeight` happen to
both equal 28 - a coincidence with no connection to `Summary`. It had
become a duplicate of `UnknownSectionType_FallsBackToTextRowHeightPerRow`
under a name asserting Summary-specific tile-omission behavior that no
longer exists anywhere in the codebase, and would produce a confusing
false failure (naming `Summary`/`OmitsTileRow`) the first time either
constant is retuned independently of the other. Deleted. Full suite:
1760 passed after this correction (5 dead/vacuous tests removed from the
1765 baseline total). The `SummaryBodyHeight` deletion did not remove
`PlanRowType.CoinTotal`'s underlying enum value from a serialized/
persisted format anywhere: `PlanRowType`/`PlanRowViewModel` are rebuilt
fresh on every render by `PlanViewModelBuilder` and never
serialized/persisted, and grep found no `(int)PlanRowType`/
`Enum.GetValues(typeof(PlanRowType))` call sites, so the enum's ordinal
values shifting by one position (removed member was first) has no
runtime effect.

Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build
C:/Dev/Blish/wt-hezone/GW2CraftingHelper.csproj -p:Platform=x64` - 0
errors both before and after (pre-existing StyleCop warnings only).
Tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test
C:/Dev/Blish/wt-hezone/tests/GW2CraftingHelper.Tests/
GW2CraftingHelper.Tests.csproj` - 1765 before, 1761 after the first
deletion pass, 1760 after the same-day follow-up correction that deleted
`Summary_NoCoinRow_OmitsTileRow` (see above), 0 failed at every step.

Repo Invariants Checklist:
- [x] No Blish HUD references added to tests.
- [x] Tests exercise real production paths (deleted tests exercised only
  the now-removed dead code; the remaining suite is unchanged production
  coverage).
- [x] No fake file I/O tests introduced.
- [x] Pricing logic preserves multi-source correctness (untouched by this
  change).
- [x] IDs remain internal-only (untouched by this change).

Not pushed; not committed to `master`. This branch does not attempt the
larger `SummarySectionLayoutMath`/`PlanContentHeightMath` fold-back - that
remains a real behavior-bearing change awaiting the audit fleet's
architecture findings, per this task's own instruction.

Gate: PASS (non-visual change: docs policy rewrite plus dead-code
deletion, so no desktop gate applies; evidence is the unreachability
grep proof plus the suite arithmetic above - 1765 baseline to 1760
passing with only the 5 dead/vacuous tests removed, 0 failures, build
clean at b420460).
