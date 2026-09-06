> **Milestone record - 2026-08-29, branch `wave6-ui`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## A wave of field-test fixes: viewport, tables, icons, tree rows and dialogs

`wave6-ui` is the integration branch for five concurrent topic branches
answering one round of in-game feedback, merged as pull request 236. It
touches 82 files. Three items were reported as bugs (scroll position lost on a
decision toggle, tree content overdrawing the pinned header, clicking IGNORE
expanding the node instead), two as missing table behaviour (sticky headers,
sort indicators), and the rest as visual defects across icons, the recipe
tree, the Settings grid, dialogs and the Log tab. One section below per
change.

### Scroll position was reset by the restore's own write

Toggling a node between CRAFT and VENDOR repopulates the Total Cost currency
table, which changes the content panel's height and leaves Blish's cached
`Scrollbar._scrollbarPercent` stale. Setting `ScrollDistance` calls
`Invalidate()`, which reaches `Scrollbar.RecalculateLayout` synchronously;
that method compares the cached percent against a fresh one and assigns
`ScrollDistance = 0` on any difference - inside the restore's own assignment.
The viewport landed at the top, with the recipe tree off screen.
`Services/PlanRelayoutMath.cs` already made the refresh pre-call for the
height-only resize tick, and its comment claimed the rebuild path did not need
it because the mutation churns the content panel's children. That is wrong:
`Panel.UpdateContentRegionBounds` only rewrites the scrollbar's `Height`,
`Top` and `Right`, all unchanged by a content rebuild, so the property setter
short-circuits and no layout pass runs. The rebuild path now makes the call.

### A hard top cutoff on the scrolling viewport

Tree content drew over the pinned top strip when scrolled. From the decompiled
Blish 1.3.0 binary: `Control.Draw` writes
`Intersect(scissor, AbsoluteBounds).ScaleBy(uiScale)` into
`GraphicsDevice.ScissorRectangle` and `Container.Paint` reads it back and
unscales with `ScaleBy(1f/uiScale)`. `ScaleBy` floors origins after a float32
multiply, so the round trip is `floor(floor(y*s)/s) <= y` and a clip's top
edge can only rise; `PaintChildren`'s re-intersection re-clamps it only when
the container's own top is below the inherited clip, false for every ancestor
of a row scrolled out of view. The loss therefore accumulates once per nested
container and grows with tree depth, and positioning the viewport lower does
not help, because the leaked pixels move down with it.

`Control.Draw` is public virtual, so `Views/Rendering/ClipCutoff.cs` publishes
one absolute line for a subtree's paint (`ClipAuthorityFlowPanel`) and
re-asserts it at each container (`ClippedPanel`, `ClippedFlowPanel`), bounding
the subtree's reach at `cutoff - SlipBudget` whatever the depth.
`Services/ClipCutoffMath.cs` holds the Blish-free arithmetic. `SlipBudget` is
2, the worst single round trip across the four GW2 UI Sizes - 2 at 0.81 and
0.897, 1 at 1.103, 0 at 1.0 - and the line sits one budget below the
viewport's top edge, costing its top 2 logical pixels.

### The recipe tree's own containers re-assert the cutoff

The cutoff work could not reach `Views/Rendering/TreeSectionController.cs`,
which belonged to a branch running concurrently, and that file is where depth
comes from: a tree row at depth d sits under roughly 2d containers, one row
panel and one child flow per level, all plain `Panel` and `FlowPanel`. All six
container sites in the file are now clipped types, so the chain from the
viewport down is `ClipAuthorityFlowPanel` -> `ClippedFlowPanel` (section) ->
`ClippedFlowPanel` (per depth) -> `ClippedPanel` (row) -> `ClippedPanel`
(scrim, pill). `TopStripZIndex` is kept as a cover for any plain `Panel` added
inside the viewport later, since the invariants bar a test from referencing UI
code.

### Clicking IGNORE was answered by the caret

Blish recomputes `Control.MouseOver` only when the mouse position changes
between two frames. `HoverChainResync` re-runs that hit test after a click
rebuilds controls under a stationary cursor, and it cannot do so on a full
rebuild: a fresh row is added to its `FlowPanel` with no `Location` of its
own, and Blish defers `FlowPanel.RecalculateLayout` to the next draw, so every
new row still sits at its container's origin when the resync runs. Ignoring a
node with children re-solves it to a leaf, which changes the row set, so the
in-place refresh declines and that full rebuild is what runs - which is why
leaf materials kept working and the reported case did not.
`Services/TreeRowPillHitTest.cs` removes the dependency: the guard, and the
press-feedback suppression predicate beside it, test the pills' rectangles
against `RelativeMousePosition`, derived from live `AbsoluteBounds` at click
time.

### The same stale-hover pattern on the Required Recipes checkbox

Both of that header's predicates in `Views/CraftingPlanView.cs` - the
press-feedback suppressor and the press-time flag that stops a click on the
checkbox also collapsing the section - read `hideUnlockedCheckbox.MouseOver`.
The checkbox's `CheckedChanged` calls a full plan rebuild, so it is a new
`Checkbox` instance on every toggle, parented into a freshly built header
inside a `FlowPanel`: the shape above. A second click without moving the mouse
would have collapsed the section. Both now go through `CursorOverCheckbox`,
which reuses `TreeRowPillHitTest`'s half-open `Covers`. The sweep is recorded:
the only other live `.MouseOver` reads are `Views/SuggestionPanel.cs` lines 76
and 440, excluded because that panel is constructed once, parented straight to
`SpriteScreen` and positioned explicitly, so nothing there is rebuilt under
the cursor. All 13 `MouseEntered` and `MouseLeft` handlers cache nothing.

### Sticky column headers

Blish has no sticky primitive: a control inside a scrolling `Container` moves
with it, and a `Container` clips its children to its own bounds and nothing
else. Of the two mechanisms available - draw a second copy of the band in an
overlay, or move the real one - this moves the real one, because a copy is
either a header that cannot be clicked or a second set of header cells to keep
in step. The top-edge cut comes from a clip `Panel`: a sibling of the scroll
region, `ZIndex` above it, sized to exactly the slice of the band that should
show, with the band offset upward inside it, so it captures the mouse over the
band and nowhere else. `Services/StickyHeaderLayout.cs` is the Blish-free
placement, viewport-relative so it never has to know how a scroll offset is
applied; a pinned band never rides higher than its own row would have put it,
is pushed back out by the end of its own table, and a table with no rows never
pins. `Views/Rendering/StickyHeaderHost.cs` is the control side.

Wired on the Snapshot tab only, whose Items and Currencies runs share one
scroll. The Crafting Plan tab's bands are children of a `FlowPanel`, where
removing one reflows every section below it, so that adoption needs a
fixed-height spacer in `Views/Rendering/HeaderBands.cs` plus a host in
`Views/CraftingPlanView.cs`. That adoption is left for a later branch.

### Persistent sort indicators, and sorting on Plan History

Every sortable column now carries an indicator at all times - dim at rest,
solid and directional on the active column - so a sortable header is
distinguishable from a fixed one before it is clicked. Dim to solid is an
opacity change only: the indicator is its own `Label`, its slot is reserved
unconditionally at the width of the wider of the two glyphs, and every column
band, header-room clamp and header cell is laid out against that block width,
so nothing under the cursor moves at the instant of a press. New:
`Services/SortIndicatorLayout.cs`, `Views/Rendering/SortIndicator.cs` and
`Views/Rendering/SortableHeaderBlock.cs`, the word and its indicator as one
unit; `TableSortState.IndicatorFor` becomes `DirectionFor`, and "no indicator"
is no longer a state a sortable column can be in.

Sorting is extended to Plan History (Plan, Cost, Generated) through
`Services/PlanHistoryTableSorter.cs`; clicking a header overrides the
pin-first default and the third click restores it. Ruled out elsewhere: the
Crafting Ranker, whose row order is already an answer; the Log, chronological
and filtered through its level dropdown; Crafting Steps, an ordered procedure;
the Recipe Tree, a hierarchy with inert columns; and the two reference lists.
The Snapshot tab's Amount column widens for its indicator: the band
is floored at the header block, so `SnapshotItemGridLayout.AmountColumnFloor`
grows 79 -> 95, the minimum column width 566 -> 582, and the three-column
window threshold 1824 -> 1872. A sort click there can no longer move a column
edge, so it no longer re-ellipsizes anything.

### An inline currency icon seats on the digits, not on the line box

Reported in game: the currency icon beside an amount is not vertically centred
against the number. The seat came from nowhere - `LayoutCoinSegments` took an
icon Y offset the caller supplied and defaulted it to 0, and
`LayoutCurrencySegments` had no such parameter at all, so every inline run in
the module drew its icon flush with the top of the number's line box, which
carries ascender and descender space the digits never reach. The seat is now
the renderer's: `CoinCurrencyRenderer.DigitSeat` reads the `0` region off the
face itself and centres the icon box on the digits' ink, and the two call
sites that computed their own offset drop it. The arithmetic is
`CoinSegmentMath.InlineIconY`, clamped to zero so an icon taller than the
digits cannot start above the line box the row reserved its height from.
Measured against the module's own face: Menomonia 16 seats a 16px icon at y+2
where it drew at y+0, spanning 2..18 inside a 20px line box, so no band height
moves and no x term changes.

### A currency icon's frame is a border, not a background

Reported in game as a grey background behind the currency icons, where only a
gentle grey border had been asked for. `Views/Rendering/IconControls.cs` had
one frame builder, which makes a `Panel` the size of the whole framed box,
fills it with the frame colour, and lays the art inside it inset by the border
width. That is a filled plate, and always was; a previous change had simply
routed currency icons onto it for the first time. Item art is a full-bleed
bag-slot square and hides the plate, so only currency art, mostly transparent,
showed it. `ItemIconFrame.IsOutline` is now true for `Currency()` and false
for every item frame, so a call site cannot pick the wrong shape for the
colour it asked for. The ring is four `DrawOnCtrl` calls inside one control,
`Views/Rendering/OutlineFramePanel.cs`, rather than four child panels, which
would triple the control count of every inline coin run and give the 1px edges
their own hover. Geometry does not move: the frame panel is still
`iconSize + 2 * border` on a side, so the inline coin-run advance - a term in
the minimum-window-width derivation - is unchanged.

### Row-action glyph buttons drop to a compact square

Asked for in game: the row X buttons should be closer in scale to the close
control in the corner of the Trading Post window. They drew at 28x28, the
module's on-tab button height, carrying a 16px cross. They now draw at 24x24.
The glyph atlas ships one size and the remove cross is 16x16 there, so the
mark cannot shrink with the button and the button is sized to the mark
instead. `Services/GlyphButtonMetrics.cs` states the derivation:
`FeedbackButton` paints its plate at `(3, 3, Width - 6, Height - 5)`, so 24 is
the smallest square that still holds a 16px glyph with a pixel of margin, and
the X axis binds. Plan History's delete X and the Ranker's up and down carets
are the same control and move with it. Not measured against the game: no
Trading Post capture is in this repo.

### The pill column takes the width the window has

The decision-pill column was a flat 256px at every panel width, so a row whose
pills did not fit showed a "+N" chip however much room the window had, and the
name column - the only thing that flexes - absorbed every pixel the pills were
denied. `Services/TreePillColumnMath.cs` derives the width the way the cost
column already derives its own: the widest full run any node in the tree
needs, floored at `PlanRelayoutMath.TreePillColumnWidth` so nothing narrows,
and capped at that floor plus half of whatever the panel has beyond the
module's minimum width. Half is what makes the split safe - the name column
keeps what it holds at the minimum plus half of every pixel past it, so
widening the window can never leave it narrower than it was one pixel earlier,
and at the minimum the column cannot grow at all, so nothing in
`docs/research/minimum-window-width.md` moves. The scan covers the whole tree
rather than the expanded rows, because rows are built lazily and a
visible-rows-only scan would move the column on the first expand; the result
is a one-way floor for the life of the plan.

Measured at a 1920px window: a CRAFT / TP / "HAVE 12/50 NEEDED" row goes from
2 of 3 pills tightened plus a "+1" chip to 3 of 3 at full padding, and a
CRAFT / TP / VENDOR / HAVE row from 3 of 4 plus a chip to 4 of 4. The column
takes 82px and the depth-0 name budget goes 1314 -> 1232. At the 1378px
minimum every one of those rows degrades exactly as before.

### The IGNORE toggle becomes a mark in a raised or pressed key

The control carried the words IGNORE and IGNORED, which is language to
translate on a control that needs none. It now draws `UiGlyphs.RemoveMark`
through the same degraded path `FeedbackButton.SetGlyph` takes. A mark alone
cannot say which of two states a row is in, so the key around it does: off is
the outlined grey ring it already had, and on is that key pushed in, filled
solid with the amber the ring used to draw, edged darker, with the mark
punched into it near-black. The words survive as state names in the tooltip,
and the anchored slot drops from the width of "IGNORED" plus padding to the
mark's, which is width the pill column above wanted. One contrast defect was
fixed in the same pass: the punched-out mark reads 4.19:1 against the filled
on key at full strength, but `DimmedPillFactor` multiplies mark and key toward
black together and compresses that to 2.17:1, under the 3:1 non-text minimum,
on a row that is reachable - ignores are keyed by item id, so an occurrence
under a bought parent draws the on key while dimmed. A dimmed on key keeps the
light mark, 3.05:1.

### The tree's Cost header follows the regime most of its rows are in

The header already centred over ink rather than over a reserve. What it
centred over was the widest run of any single row, and the cost column lays
rows out in two regimes that do not share an extent: a row with no currency
ink collapses the shared currency band for itself, so every coin row's ink
starts a whole band-plus-gap right of where a mixed coin-and-currency row's
does. One vendor row therefore set the extent for a column of coin rows.
Measured against the real scan with a 96px currency band and a 130px coin run,
the extent read 216 instead of 130 and the header sat 43px left of the centre
of every coin figure under it. `Services/TreeCostColumnMath.cs` now counts
rows per regime and the larger one wins, a tie going to the coin-only regime,
which is what the shared sub-columns are laid out for. Two pinned vectors were
re-derived: one coin row plus one currency row is now a tie and reports the
coin run, 73 where it was 88, and the floor's no-narrowing fixture needed a
tree whose widest row is the one being ignored away.

### The tree's "Item" header anchors on the plan's shared rule

The Recipe Tree headed its flexing column on its own depth-0 name rule, which
is 8px right of every other plan table's, because tree rows carry a caret
column the other tables have no equivalent of. Used Materials sits directly
under it with the identical word, so the pair read as misaligned.
`PlanRelayoutMath.TableLeftHeaderX` names the shared rule where the tree can
reach it, aliasing `ShoppingColumnMath.NameX` rather than restating its
arithmetic. The tree's rows are untouched; only the header word moves, and it
still sits inside the item cell it names.

### "Copper per unit" centres over its cluster's ink

The Settings tab's Vendor Cost Valuations grid was never converted to the
centred-over-content header law: its unit header sat on the amount box's own
left edge, flush against the left of a 256px value cluster.
`Services/SettingsCurrencyGridLayout.cs` centres it over what the cluster
inks - the amount box, the Ignore checkbox and the tag - rather than over the
box alone, which barely moves the word, or over the reserved band, which the
header law exists to stop. The tag's ink is measured off the two curated
defaults tables rather than off the rows' live text, so ticking Ignore or
typing an amount cannot move a header while it is being read.

### Every dialog body line is centred, not just the block

A confirm's body was one multi-line `Label` centred by its measured width.
Blish paints that in a single `DrawString` and the pen returns to the block's
own left edge at each newline, so a second sentence sat left-aligned under the
first. Each physical line is now its own `Label` placed at
`DialogLayoutMath.LineX`, which centres a line in the content box and pins an
over-wide line to the left edge rather than letting it overhang both sides. A
one-line body is centred by exactly the arithmetic that centred the block, so
every single-line dialog is unmoved; Clear Overrides, Best Path, the
own-materials regenerate, Clear Cache and Discard changes gain a centred
second line. `ApiAccessDialog` is deliberately untouched - its body is three
independent checks on a common left rule.

### The Log tab wraps long messages

The message column ellipsized to a fixed-pitch row, so a long entry was
readable only through a hover. It now wraps inside the column and the row
takes the height its wrapped text needs. The fixed pitch was the module's own,
not Blish's: the rows live in a `SingleTopToBottom` `FlowPanel`, which
positions each child by its own height, so nothing downstream had to change.
`Services/LogRowLayout.cs` is the single place a row's height is derived and
counts the single-line descender clearance once, at the bottom of the row.

Bounds taken deliberately. The wrap is capped at
`LogRowLayout.MaxMessageLines` (4) with the tail ellipsized into the last
line, so one pasted stack trace cannot own the viewport. A message that
already fits one line and carries no hard break takes a one-measurement fast
path, the same measurement the ellipsize early-out took, so a full rebuild is
not slower for the entries that dominate a log. The wrap memo is exact width
equality rather than the narrowing-only asymmetry the fit check gets, because
widening a wrapped column changes its answer too. `VerticalAlignment` is
pinned to Top on all three column labels, or a multi-line row would float its
timestamp down the side of the message, and the tooltip is kept and widened
for any row that could not be read in one pass. One cost is recorded rather
than defended against: a resize settle can now change the panel's content
height, which Blish answers by zeroing the scroll position a frame later.

### Budgets and comment trims at integration

`docs/file-budgets.txt` gains 21 raised entries for files the topic branches
grew without budgeting, plus entries for the new files; two branches had
raised `PlanHistoryTabContent` from different bases, and the conflict was
resolved to the merged file's real size rather than either side's figure. The
whole file's number column was also realigned, which is why its diff is large.
Eight over-length comment blocks were shortened under the 12-inline and 20-XML
limits rather than re-pinned, with the derivations they carried left in the
`docs/ARCHITECTURE.md` sections that already state them. One of the eight was
an orphan `<summary>` describing a member that was no longer there.

### Regression coverage

New Blish-free test files pin the arithmetic each change moved into a service.
`ClipCutoffMathTests` proves the clip bound over a 4000px screen-position
sweep at 64 levels of nesting at every UI scale, and proves the unclamped
counterfactual keeps growing, so the guarantee never mentions depth.
`StickyHeaderLayoutTests` sweeps a whole scroll pixel by pixel rather than
sampling offsets. `TreeRowPillHitTestTests`, `TreePillColumnMathTests`,
`SortIndicatorLayoutTests`, `PlanHistoryTableSorterTests`,
`GlyphButtonMetricsTests`, `IconFrameGeometryTests`, `DialogLayoutMathTests`,
`LogRowLayoutTests` and `CoinSegmentMathTests` cover the rest;
`GlyphButtonMetricsTests` reads the shipped atlas and pins both the ceiling
and the floor of the 24px square. Two guards do not fit a test:
`.github/workflows/tests.yml` gains a step that fails the build if a second
background assignment appears in the icon frame builder or if the outline
panel grows one, since the invariants bar a test from referencing view code
and the filled plate is what shipped once. The grid-law golden's snapshot
columns were re-captured for the widened Amount floor, the second such
re-capture, and the golden's own comment now says so.

Gate: NOT RUN - no live game session was available while this branch was
written, and the numbers above come from the shipped font and atlas, the
vendor binary and the layout arithmetic rather than from a capture. In game,
confirm on a plan with a deep tree that scrolling never draws a tree row over
the pinned top strip; that toggling a node between CRAFT and VENDOR leaves the
viewport where it was; that repeated IGNORE clicks without moving the mouse
never expand the node; that the Snapshot tab's two header bands pin and
release with their own tables; and that coin and currency icons sit level with
the digits beside them, with a ring rather than a grey plate behind them.
