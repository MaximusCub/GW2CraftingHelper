> **Frozen record - 2026-08-19, branch `final-test-polish`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Final test polish: three Nice to Have items (final-test-polish)

- InventoryReducerTests: new Reduce_PreservesRootId_IncludingWrapperSentinel
  pins CloneNode carrying the wrapper-root Id through Reduce (real path).
- MultiItemPlanTests SingleEntryList pin: dropped the inert reducer arg
  (snapshot is null), matching sibling no-reduction pipeline constructions.
- InventoryReducerTests: trimmed five near-verbatim "split across two
  sources" comment repeats; section headers state the relationship once.
Validation: module build 0 errors; suite 1847 green (1846 baseline + 1).
Gate: PASS (test-only change; the review's mutation check proved the
new root-Id test fails through the real Reduce path when CloneNode
drops the wrapper Id; suite 1847/1847; no rendered surface).
