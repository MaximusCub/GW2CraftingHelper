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
