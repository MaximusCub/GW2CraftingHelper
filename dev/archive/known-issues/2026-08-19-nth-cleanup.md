> **Frozen record - 2026-08-19, branch `nth-cleanup`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Nice to Have batch (nth-cleanup)

The nine non-controversial Nice to Have findings from the PR #142 and
#143 adversarial reviews, applied together on branch `nth-cleanup`.
Behavior is preserved except where a bullet says otherwise.

- **Flush drain budget hoisted:** Module.Unload and
  ModuleLog.DeleteFileAndReset each spelled out 250ms, with the latter's
  doc asserting in prose that they matched. `ModuleLog.FlushDrainBudget`
  now carries it for both.
- **Log status moved to its own row (behavior change):** the auto-sized
  status label shared the toolbar row with the three right-anchored
  buttons and ran under the leftmost of them - at the enforced 930px
  minimum window width the gap between them is only ~48px, so any real
  status ("Log file deleted", "Nothing to copy") collided. It now sits
  in a full-width 24px row beneath the toolbar, the same shape
  MainView's `_statusPanel` already uses for the same reason; the
  content panel starts below it. No truncation - the full text always
  renders.
- **Source-filter re-flow skipped when its inputs have not moved:** the
  Snapshot tab re-flowed the checkbox row on every resize event,
  including height-only ones. The flow pass now runs only when the
  available width or the height-driven row cap changed - the cap
  because it decides whether the row scrolls and so re-flows narrower.
- **Outgoing checkbox references dropped with the panel:** Build
  replaces `_sourceFilterPanel` on a ThreadPool thread, but the three
  fields holding its checkboxes were only refilled from the marshaled
  tail; a resize in that window wrote Location on controls belonging to
  the replaced panel. Cleared by reference swap next to the panel
  construction, so a concurrent main-thread read still sees a
  consistent list - which holds only because each reader now takes the
  field into a local once (`SetAllCharactersChecked` hoists the
  checkbox list before its bounds check; `OnCharacterToggled` hoists the
  master before its null check; `ApplyTopRegionLayout` hoists the cell
  list before its count/indexer walk), rather than re-reading the field
  after its own guard.
- **No substring per character source:** SnapshotSearchResultBuilder.
  IsSourceEnabled compares the name half of "Character:<name>" in place
  with string.CompareOrdinal instead of allocating a Substring per
  source per item on the keystroke path. Pinned by two tests the old
  exact-hit/exact-miss pair could not catch: whole-name-only matching
  (a strict prefix, a strict extension, and a case-only variant of an
  excluded name all stay visible) and the zero-length name half.
- **Comment/doc corrections:** ApiAccessDialog's claim that ModalDialog
  has fixed Regenerate/Cancel buttons (its confirm button is
  caller-named); ApplyStatusDisplay's claim that a Settings save changes
  the label and the auto-refresh gate together (it is re-read on the
  next ApplyStatusDisplay call); the sticky-state field block's inert
  `<para>` tags and design narration; CheckboxChromeWidth's
  "reproduces the four widths" (it approximates them - only the
  single-row height is exact, corrected in this file too);
  SourceFilterFlowLayout's class-level `<paramref>` moved onto the
  method that has the parameter; stray blank lines in
  SourceFilterFlowLayoutTests.

Open by choice - the two behavioral Nice to Haves this batch
deliberately skipped:

- **Character-search minimum query length:** a one-character query still
  walks every source of every non-matching item. Left as is; the worst
  case is still bounded by the empty-search rebuild. **CLOSED
  (char-search-min2):** the maintainer set a 2-character minimum, for
  the result-list reason rather than the perf one - see that section
  below.
- **Tri-state master checkbox:** the two-state quirk recorded in the
  char-source-search section above stands.

Validation: build 0 errors and the full suite green before each commit
(1884, then 1886 once the two IsSourceEnabled boundary tests landed).
Gate: PASS (2026-08-19, Paint-dummy desktop session, branch build
3310169, capture preflight/nth1-log-status.png). The one visual
surface in the batch is the Log status row rework (review Must Fix):
clicking Copy on an empty log rendered "Nothing to copy" on its own
full-width row directly below the toolbar - full text, no overlap
with the Delete Log File / Copy / Clear view buttons, with the "No
log entries yet." empty state below it. Everything else in the batch
is comments/docs, a constant hoist, allocation removal (pinned by 2
new Blish-free tests), and the resize early-out + reader hoists,
which are code-review-verified (the verify pass caught and the
orchestrator fixed a third un-hoisted reader in ApplyTopRegionLayout
before release). Suite 1886/1886.
