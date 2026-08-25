## Audit batch I: log entry readability (audit-i-log)

UX audit finding (M7): every Log tab entry was ONE `AutoSizeWidth`
Label built from the whole flat line, tinted end to end by
`ColorForLevel`, hard-clipped at the panel's right edge with no wrap,
no ellipsis and no indication that text had been lost - a WARN
carrying a long path plus an exception simply lost its tail, and the
level tint was the only structure in a wall of same-shaped text.

**Row split.** Each entry is now a fixed-height row `Panel` holding two
columns:

- a prefix Label at x=0 showing `[LEVEL] timestamp [tag]`, dimmed to
  70% alpha (this repo's existing `Color.White * 0.35f` idiom) but
  still carrying the level color, so severity still reads at a glance
  down the column while the chrome recedes behind the message;
- the message Label at the shared message-column x, with an explicit
  width (row width minus the prefix column, the 8px gap and the 8px
  right pad).

Both columns run through the existing `LabelHelpers.EllipsizeToWidth`,
and a row that had to shorten EITHER column gets `BasicTooltipText`
with the full line - assigned to the row Panel AND to both Labels,
because Blish resolves a tooltip on the control under the mouse and
does not bubble to the parent (the swallowed-hover class already
recorded for `ShoppingListSectionRenderer` in this file). The `...`
plus that tooltip are the truncation indicator the audit asked for.

The prefix column is sized from a worst-case template - the widest
level name, a timestamp built from the widest decimal digit, and a
14-character tag allowance - rather than from the rows currently on
screen. That is load-bearing, not decoration: the incremental
`AppendNewRows` path only ever sees the new entries, so a width derived
from what a pass can see would drift away from the rows the last full
`RebuildRows` produced and stagger the message column.

The tag allowance is counted in `'w'` glyphs and sized off the longest
tag actually written anywhere in the tree - `snapshot-fetch`, 14
characters. The margin is the glyph: every tag in the module is
lowercase ASCII plus `-`, all narrower than `'w'`, so 14 `'w'`s clear a
14-character tag with room to spare. The first draft reserved 10 on the
stated (wrong) belief that `scrolldiag` was the longest tag; at that
width `[snapshot-fetch]`, the module's most common WARN source, risked
rendering permanently truncated AND permanently tooltip-flagged at every
window width, in the very column this change exists to make readable.

**Ellipsize, not wrap (decision).** Wrapping reads better for a long
exception, but it makes row height a function of content, and this
tab's whole row model is built on uniform rows: the eviction trim, the
append path and the Follow tail-scroll (`VerticalScrollOffset =
int.MaxValue`, an overshoot that clamps) all assume the panel's content
height is settled at the moment they run. Blish measures a wrapped
`AutoSizeHeight` label during its own deferred layout pass, so the
overshoot would fire against a stale height and Follow would land short
of the bottom. Wrapping also lets one stack-trace ERROR fill the whole
viewport in what is meant to be a tail view. Ellipsize + tooltip
preserves fixed row heights, leaves every one of those mechanisms
untouched, and is what the audit accepts as the minimum.

**Resize.** The container's `Resized` handler re-fits every visible row
after resizing the content panel, walking `_renderedRows` (the same
FIFO the eviction trim uses) - the same shape the recent status-row
rework in this file uses for the toolbar/status/content panels. Two
cheap outs keep a resize drag off the hot path: a vertical-only drag
leaves the content width unchanged and returns before touching a single
row, and a row already showing its untruncated text whose column only
grew skips the `MeasureString` binary search inside `EllipsizeToWidth`.

The walk itself is wrapped in `_contentPanel.SuspendLayout()` /
`ResumeLayout(false)`, the same pair `CraftingPlanView.ReplayRelayout`
uses and for the same reason - and the reason the first draft's "worst
case is still bounded by the ring's 2000 entries" was the wrong cost
model. Assigning a row Panel's `Size` fires that Panel's own `Resized`,
which `FlowPanel` wires to a full reflow of every sibling, so an
unsuspended loop over a full ring is O(rows^2) position writes plus a
fresh children array per reflow - on every frame of a horizontal drag,
on the UI thread - not O(rows). Suspending the parent propagates down
(Blish's `IsLayoutSuspended` walks the parent chain) and
`ResumeLayout(false)` leaves the single coalesced reflow to Blish's own
next-frame `UpdateLayout`. With the suspend in place the per-drag-frame
cost is back to linear in the ring's 2000 entries.

`RebuildRows` re-parents up to 2000 rows on every search-box keystroke
and carries the same unsuspended-reflow shape. That is pre-existing (the
old label-per-row build did the same) and is deliberately left alone
here; it is the obvious next candidate if the Log tab ever needs a
second perf pass.

**Extraction.** `LogTabContent.FormatLine` moved to the Blish-free
`Services/LogLineFormat`, which also splits an entry into its prefix
and message halves; `Line()` recomposes them into exactly the string
`FormatLine` produced, so the search filter, the Copy button and the
tooltip all still work in one unchanged flat line (Copy still emits
full lines - unaffected by the split).

`Message()` has one deliberate departure from the old `FormatLine`
output: every run of CR/LF/TAB collapses to a single space (leading runs
dropped, no trailing whitespace kept). Without it a multi-line message
lost everything after its first line, silently - a fixed-height row Panel
clips lines 2..n, and `BitmapFont.MeasureString` reports a multi-line
string's WIDEST LINE rather than its full extent, so `EllipsizeToWidth`
sees a string that "fits", returns it unchanged, and the row is marked
neither shortened nor tooltipped. No in-tree call site embeds a newline
today, but any `ex.Message` interpolation is one BCL/HTTP/serialization
exception away from one (e.g. `CraftingPlanPipeline`'s generation-failure
WARN). Flattening in the formatter rather than at the label also keeps
Copy's `Environment.NewLine` join at one line per entry.

`Services/LogRowLayout` carries
the column arithmetic, so the degenerate widths that would otherwise
blank every row (a message column ellipsized to zero) are pinned by
tests rather than only observable live. Row virtualization/build
behavior - `RebuildRows`, `AppendNewRows`, the eviction trim,
`RebuildRowsIfBuilt`, the `_buildComplete` gate - is untouched; this is
a per-row presentation change.

The class doc comment's "label-per-row, no multi-column ellipsized rows
that must reflow live during a resize drag" claim is now false and was
rewritten: rows ARE multi-column and DO reflow, but the tab still does
not opt into the `PlanContentHeightMath`/relayout-registry contract,
and the comment now says why (uniform row heights, overshoot scroll -
no per-section height math and no settle/verify pass to defer into).

**Validation per commit:** module build 0 errors (pre-existing StyleCop
warnings only; no new warning class in the edited files). Suite 1886
baseline -> 1900 after commit 1 (14 new Blish-free tests:
`LogLineFormatTests` pins that prefix + " " + message is byte-identical
to the old flat line, including the no-tag and null-message cases;
`LogRowLayoutTests` pins the narrow-row prefix cap and the
never-collapse floor) -> 1900 after commit 2 (view-only) -> 1904 after
the review-fix commit (4 more, pinning the CR/LF/TAB flattening and the
unchanged-reference fast path).

**Desktop gate items** (rendered surface, outside the test-runnable
Blish-free layer):

1. A long WARN line (long path + exception) shows a dim
   `[WARN] timestamp [tag]` prefix, an ellipsized message ending in
   `...`, and a tooltip carrying the full untruncated line - hovering
   the prefix, the message and the row's empty right edge all raise it.
2. Narrowing and widening the module window re-fits the rows: the
   message re-ellipsizes to the new width, previously-truncated rows
   recover their full text when the window grows, and the tooltip
   appears/disappears with the truncation. Do this with the level filter
   on `Debug+` and a full ring (2000 entries) and watch for drag stutter -
   that is the case the `SuspendLayout` wrap above exists for, and it has
   only ever been reasoned about, never measured on hardware.
3. The level tint is still legible at a glance - ERROR/WARN rows read
   red/amber down the prefix column, and the message keeps the full
   (undimmed) level color.
4. Follow still snaps: with Follow checked, new entries append at the
   bottom and the view stays pinned there; unchecking Follow freezes it.
5. Copy still writes the full untruncated lines to the clipboard, not
   the ellipsized display text.

Gate: PASS (2026-08-22 evening desktop batch, branch build 8026242,
captures preflight/gI1-gI3). At Debug+ with the seeded session log:
every entry rendered as a dim level-tinted prefix column ([WARN]
orange, [INFO] white, [DEBUG] grey) plus an aligned message column;
the long plan-timing line ended in a visible "..." instead of the
old hard clip; hovering a row that fits showed no tooltip (correct
narrowed semantics) while hovering the ellipsized row showed the
full line in a multi-line tooltip. Follow was on and the newest
entry sat at the bottom. Drag-resize refit not exercised live
(synthetic resize drags unreliable); covered by the SuspendLayout
fix, the width-guard early-outs, and the Blish-free layout tests.
