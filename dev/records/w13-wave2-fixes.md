> **Milestone record - 2026-09-03, branch `w13-wave2-fixes`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Three unrelated plan-tab defects reported from in-game use

Pull request 241. Each of the three was reported separately and each has
its own root cause. The recipe tree chipped its decision pills down to a
"+1" while empty pixels sat beside them. A currency icon in a tooltip
header drew on a filled grey plate that showed through its transparent
art. The item input strip at the top of the Crafting Plan tab re-packed
itself on every pixel of a window resize drag.

### The pill column claimed a cost reserve no row draws into

The Source column in the recipe tree collapsed its decision pills to a
"+1" chip on rows that had room beside them. This was the third report of
the same symptom; the two earlier fixes both adjusted the surplus to the
LEFT of the pills, and the rightward term had been structurally zero the
whole time.

`Views/Rendering/TreeSectionController.cs` asked for the room toward the
cost column as `TreeCostColumnWidth - TotalWidth(_costColumnWidths)`.
Both halves are wrong:

- `TreeCostColumnWidth` is 150, the cost column's fixed FLOOR, not the
  reserve the column actually holds.
- `TotalWidth` sums the per-denomination maxima. No single row draws all
  of those together whenever they come from different rows, which a
  currency band guarantees: a coin-only row collapses that band for
  itself.

So the subtraction went negative and clamped to zero on essentially every
real plan. The measurement in the branch's own commit: 123 pixels sat
unused between the ignore button and the leftmost cost ink of the widest
cost row, while a pill run needing about 130 was being chipped.

**The fix.** `Services/TreeCostColumnMath.cs` gains a sixth field on
`CostColumnWidths`, `LeftmostInkReach`, which is the larger of the two
layout regimes' ink runs. The existing `WidestRowRunWidth` is unchanged
and still places the "Cost" header, because a header sits over one
extent and follows the regime with more rows. The old private
`WidestRowRun` splits into `RowRuns` plus `HeaderRun` with no behaviour
change. `Services/TreeCostColumnFloor.cs` carries the new field through
its `Widen` and its equality check.

The whole budget rule moves into four pure entry points on
`TreeCostColumnMath` - `Reserve`, `InkFloor`, `RightSlack` and
`WidthAfterClaim` - so the effective column width and the slack
calculation cannot disagree. `TreeSectionController` now calls
`WidthAfterClaim` and `RightSlack` instead of doing the arithmetic
itself.

A first attempt floored on `WidestRowRunWidth` and was rejected. On the
common shape - many coin rows, a few vendor rows - that value follows the
coin-only regime, and flooring there would have let the pill column
advance about 94px past the mixed rows' cost ink.

A tree with nothing priced measures no ink. `InkFloor` hands back the
whole reserve in that case, so the pills claim nothing; the shipped code
gave away all 150px there.

### A currency header icon in a tooltip got a filled plate

Currency icons inside a mouseover drew with a grey background instead of
transparency, for some currencies and not others.
`Views/Rendering/RichTooltipSurface.cs` built every header icon with
`ItemIconFrame.Explicit`, which is `outline: false`, so the icon layer
gave it a filled plate. Currency art is mostly transparent, so the plate
showed through as a background. Only transparent art exposes it, which is
why it looked like "some currencies".

**The fix, one layer up.** `TooltipContentBuilder.Header` in
`Services/TooltipContent.cs` took a rarity string, and
`Services/CurrencyTooltipComposer.cs` passed null for it. Null is
indistinguishable from an item whose rarity nobody looked up.

`Header` now takes a new `TooltipHeaderSubject` struct with three
factories: `Currency`, `ItemOfRarity` and `ItemOfUnknownRarity`. The old
signature is gone, so every caller had to restate its subject:
`ItemRowTooltipComposer`, `ItemStatTooltipComposer` and
`MultiItemHeaderTooltipComposer` all pass `ItemOfRarity`. The subject
rides through `Services/TooltipLayoutMath.cs` onto the laid-out row,
because the surface never sees the content object. The name span still
takes its colour from the subject's rarity key, so name colouring is
unchanged.

`Views/Rendering/RarityColors.cs` gains `ItemIconFrame.ExplicitOutline`:
the same call-site-owned colour as `Explicit`, drawn as a border ring.
The surface picks it for a currency and `Explicit` for an item. Same
colour, same art size, same border thickness, same geometry; only the
shape changes.

`ItemIconFrame.Explicit` was swept for other call sites. It has exactly
one, the dimmed not-crafted tree row, which is item art under a
deliberate scrim and is correct as a plate. Every other currency icon in
the module already routed through `ItemIconFrame.Currency`.

`ItemOfUnknownRarity` has no caller and is kept deliberately: without it
a rarity-less item has only `ItemOfRarity(null)`, which is the silent-null
path the change exists to close.

### The input strip re-packed on every drag pixel

Dragging the window edge on the Crafting Plan tab stretched the item
input textbox elastically and snapped it back repeatedly. The strip's ROW
COUNT is a step function of the window width, and within a row the cells
stretch to fill, so every drag pixel that crossed a "one more row fits"
boundary re-packed the whole strip.

**The fix.** New `Services/DeferredReflowGate.cs`, Blish-free and
clock-injected. It holds "a reflow is pending at width W" and hands that
width back exactly once: on pointer release, or after a quiet interval
with no further observation. It reuses the existing `ResizeDebounceMs`
of 150ms rather than adding a second debounce.

In `Views/CraftingPlanView.cs`, `OnPanelResized` now calls `Observe` on
the gate instead of calling `_inputRows.ResizeRows(w)`. The new
`ApplyPendingStripReflow` runs inside `ResizeSettleStep` BEFORE that
method's elapsed check, so a release lands the reflow without waiting the
interval out. `PointerHeld` reads Blish's left mouse button state. The
settle ticker's arming condition widened to include a pending reflow,
because the strip exists whether or not a plan does and a no-plan tab
would otherwise never settle.

The reserved height follows the DEFERRED width, not the live one:
`ComputeTopRegionLayout` is called with `_stripReflow.AppliedWidth`, so
the strip and the viewport top are frozen together during a drag and move
together at settle. A live height over a frozen strip would open a gap or
an overlap against the separator for the whole drag. The input panel
itself still takes the live width, because it is the clipping frame for
cells a narrowing drag has not re-seated yet.

The cost of reflowing once at the end is visible and was accepted: while
the button is held, a narrowing drag can clip the rightmost cell and a
widening drag leaves dead space at the right. Both resolve on release.

Lifetime is closed at three points. `StopLiveTickers` and the settle
step's dead-panel bail call `CancelPending`; `Build` and a row add or
remove call `Reset`, since a rebuild has no in-progress layout to smooth.

### Two changes with no runtime surface

`.gitignore` regains its `worktrees/` and `.cyboflow/worktrees/` lines,
which a past change had removed, so another checkout's tree stops showing
up as untracked. `CLAUDE.md` records in a tracked file that pull requests
are not reviewed; the rule previously lived only in an untracked notes
file, which a git worktree never checks out.

### Regression coverage

- `tests/TaimisToolbench.Tests/Services/TreeCostColumnMathTests.cs` pins
  the new field and the four entry points directly: on a coin row plus a
  currency row, total width 167, `LeftmostInkReach` 88, reserve 167 and
  right slack 79 where the old reading gave 0. A tree with nothing priced
  keeps `InkFloor` at the fixed floor and slack 0.
- `tests/TaimisToolbench.Tests/Services/TreePillColumnMathTests.cs` joins
  the two columns' arithmetic on a plan with an 88px currency band: the
  old reading of the slack is asserted negative, the new one is 94, and
  the short row fits three pills with none hidden. A second test pins that
  the claim moves the pill column's right edge only - `PillColX`,
  `NameMaxWidth` and `CostRightEdge` are identical to an unclaimed layout
  at the same width, because the cost column's right edge is anchored to
  the panel.
- `tests/TaimisToolbench.Tests/Services/DeferredReflowGateTests.cs` is
  new. The gate's row count is stable across a 500px to 1800px sweep that
  crosses two column-count boundaries, and exactly one reflow happens, at
  the settled width.
- `tests/TaimisToolbench.Tests/Services/TooltipHeaderSubjectTests.cs` is
  new and pins that the subject reaches the row the surface draws.
- `tests/TaimisToolbench.Tests/Services/TreeCostColumnFloorTests.cs` and
  `TooltipLayoutMathTests.cs` extend for the carried field and the carried
  subject.

### Validation

The branch's commits record, at the last commit: suite 4144 + 242 + 3,
build 0 warnings, all 19 invariant gates green.
`docs/file-budgets.txt` was raised for the files that grew, including
`Views/CraftingPlanView.cs` at 5028 -> 5097 lines, and
`docs/comment-budgets.txt` re-pinned.

Gate: NOT RUN - no live in-game check is recorded on any commit. Three
things need an eye in game. That the tooltip header's currency icon draws
as a ring with no grey plate behind it, which the tests cannot reach
because it is a Blish paint. That a window resize drag on the Crafting
Plan tab no longer shows the input strip stretching and snapping, and
that the strip re-seats on mouse release; the tests prove the row count
and the single reflow, not that Blish reports the button held for the
whole drag. And that a recipe tree row with a currency band now shows its
decision pills instead of a "+1" chip, with no pill overprinting a cost
figure.
