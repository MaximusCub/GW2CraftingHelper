> **Frozen record - 2026-08-19, branch `log-snapshot-ux`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Log + Snapshot UX: three small items (log-snapshot-ux)

- **Staleness-threshold unification:** MainView's private 10-minute
  StaleThreshold constant (and its now-false "setting deliberately not
  added" comment) deleted; the staleness label now reads the same
  clamped SnapshotRefreshIntervalMinutes setting Module.Update()'s
  auto-refresh gate reads, re-read on every ApplyStatusDisplay call so
  a Settings save moves both together. Both sides share the new pure
  StatusText.IsStale predicate, pinned by 4 Blish-free tests
  (boundary at age == threshold; same age flips verdict under 5m vs
  10m thresholds). MainView takes ModuleSettings via its ctor
  (CraftingPlanView's DI shape).
- **Delete Log File (d2 OQ4):** ModuleLogStore.DeleteAll was dead API;
  now wired to a "Delete Log File" toolbar button on the Log tab,
  confirm-gated via the existing ModalDialog (whose Show gained a
  confirm-button label parameter - required after review, so every
  caller states its own verb; the regenerate call site passes
  "Regenerate" explicitly). Ring-reset seam: new
  ModuleLog.DeleteFileAndReset - bounded 250ms flush-queue drain,
  file delete under the file gate, ring clear via Clear() (Version
  stays monotonic), then one Info trace entry recording the deletion
  (also recreates the file). Review fix: the confirm callback no
  longer runs DeleteFileAndReset on the main thread - the drain plus
  an unbounded file-gate acquisition (FlushLoop can hold it through a
  slow append or full-file trim) could hitch a render frame - it runs
  on Task.Run, with the status/rebuild tail marshaled back via
  MainThreadMarshal.Run. 3 new Blish-free tests against a real
  ModuleLogStore/temp dir, including a next-session SeedFromStore
  proving deleted entries cannot be resurrected from the file.
- **Sticky content-type dropdown:** the Snapshot tab's All/Items/Wallet
  dropdown now session-sticky via _lastFilterSelection, matching the
  search text and four source checkboxes; the comment defending the
  reset-to-default asymmetry is deleted. Restored before the
  ValueChanged subscription, so the read-back cannot trigger a
  redundant rebuild.

Validation per commit: module build 0 errors; suite green throughout -
1847 baseline -> 1851 (IsStale tests) -> 1854 (DeleteFileAndReset
tests) -> 1854 (commit 3 is view-only). Two rendered surfaces await
the sandbox check: the Log tab's new Delete Log File
button (placement left of Copy/Clear view; confirm dialog shows a
"Delete" button; post-confirm the view shows only the trace entry) and
the Snapshot staleness label recoloring against a changed
SnapshotRefreshIntervalMinutes setting. The sticky dropdown is also a
one-look check (pick Wallet, switch tabs, return).
Gate: PASS (2026-08-19, live sandbox session over the Paint dummy,
branch build 21aa2ac). All three surfaces verified from captures
(preflight/ux1-ux6): (1) Delete Log File button renders leftmost of
the three right-aligned Log toolbar buttons with the cannot-be-undone
tooltip; clicking it raises the Confirm dialog with an explicit
"Delete" button; post-confirm the status label reads "Log file
deleted", the view rebuilds to exactly one entry ("[INFO] ... [log]
Log file deleted by user"), and the on-disk module_log.jsonl was
recreated containing exactly that one line. (2) Staleness label: the
29-day-old canned preflight snapshot rendered the status line in the
warning color with the "(29d ago)" age suffix under the
setting-driven threshold; the new ModuleSettings ctor wiring
introduced no render fault. (The line's "Aug 15" base timestamp
alongside "(29d ago)" is the pre-existing persisted-failure-status +
snapshot-age composition, not a defect - the base status was stamped
at failure time by RefreshNowAsync, the suffix from CapturedAt.)
(3) Sticky dropdown: picked Wallet, switched to Log and back -
selection held and the list showed only currencies. Boundary
behavior of the unified threshold is pinned by the 4 Blish-free
IsStale tests; the recolor-flip-on-setting-change interaction was
not exercised live (auto-refresh interference makes it observable
only under API failure; covered by tests).
