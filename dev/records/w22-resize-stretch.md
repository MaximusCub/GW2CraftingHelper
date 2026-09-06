> **Milestone record - 2026-09-05, branch `w22-resize-stretch`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## The plan tab's input strip reflowed in the middle of a drag

Dragging the window edge on the Crafting Plan tab still moved the
item-entry row while the drag was running. The cells stretched and then
snapped to a new packing, more than once per drag.

That row's cells have exactly one writer, `ItemInputRowStrip.ResizeRows`,
reached only from `ApplyPendingStripReflow`. So anything that moves during a
drag is a release from `Services/DeferredReflowGate`.

### The defect: a quiet interval is not the end of a drag

The gate released the deferred width on two conditions: the pointer coming
up, or `ResizeDebounceMs` 150 elapsing with no width change while it was
still down.

The second condition is wrong. 150ms is nine frames at 60fps, and a drag is
full of stretches that long where the reported width does not change: a
steady hand, a direction reversal, vertical motion on a corner grip, and
every pixel dragged past the window's own minimum width, where
`Control.Size` early-returns on the unchanged value and raises no resize
event at all. Each of those released the deferred width and repacked the
whole strip.

### The fix

`Observe` now takes `pointerHeld` and records whether a pending width
belongs to a drag. `TryTake` holds a reflow back for as long as the pointer
is held. The quiet interval survives only for a resize no pointer drove -
the sprite screen changing size under the window, or a size restored from
settings - which has no release to wait for and still needs its burst
coalesced.

Whether a pending width is a drag's is answered at `Observe`, where the
resize tick knows, rather than at the take, which also runs on the frame the
button comes up.

A second constructor argument, `heldStallMs`, bounds the wait.
`CraftingPlanView.StripReflowStallMs` passes 2000. Blish's
`MouseHandler.Update` returns before resampling the mouse when the game
loses focus or the overlay is hidden and keeps its last sample, so a button
down at that moment reads down until focus returns. Without a ceiling the
strip would sit at its pre-drag width until something else resized it.

### The settle ticker had to outlive the last width, and could die outright

`ResizeSettleStep` is the only caller of `ApplyPendingStripReflow`, so it now
runs while a strip reflow is pending, not merely for the debounce past the
last observed width. A hand held still past the debounce would otherwise
release the button with nothing left running to notice.

The same step also fixes a way the settle pass could die for a whole session
rather than for one drag: `_resizeSettlePending` gated ticker creation on its
own, and a step that throws stops its ticker without clearing the flag. The
guard now also asks the ticker whether it is still active.

### What the change trades

The deferral now lasts the whole drag instead of resetting at every pause.
While the button is held, a narrowing drag leaves the rightmost cell clipped
by the input panel and a widening one leaves dead space at the right. Both
resolve on release.

It also moves the re-ellipsis pass - the three `EllipsizeToWidth` call sites
that re-measure over the whole tree and shopping list - out of the middle of
a drag, where a pause used to run it, to once at the end.

### Regression coverage

`tests/TaimisToolbench.Tests/Services/DeferredReflowGateTests.cs` gains three
cases and passes `pointerHeld` through the existing ones.

- A pause in the middle of a drag does not release the reflow: a held
  pointer is offered ten settle intervals in a row and takes none, then the
  drag resumes and ends, and one reflow lands at the last width.
- A pointer stuck at held releases at the stall ceiling, 2000ms, and not
  before.
- A pointer-free burst still collapses on the quiet interval, to one reflow
  at the last width.

`docs/file-budgets.txt`: `Views/CraftingPlanView.cs` 5185 to 5200.

The branch's commits record no build or test counts.

Gate: NOT RUN - no live game session is recorded in the branch's commits.
To confirm in game, drag a window edge on the Crafting Plan tab, pause
mid-drag for longer than a second, and check the item-entry row does not
repack until the mouse button is released.
