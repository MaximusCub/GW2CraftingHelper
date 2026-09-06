> **Milestone record - 2026-09-05, branch `w23-ignore-trailing-column`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## The Recipe Tree's ignore button gets a fixed column after Cost

The recipe tree drew its ignore button among a row's source markers.
Clicking it re-solves the node, and an ignored node comes back owned with
none of the markers it was drawn beside. Any x derived from that row's own
markers therefore moved out from under the cursor that had just clicked it,
and the next click reached the row and expanded the node.

### It replaces the earlier per-row seating

Branch `w18-ignore-x-gap` had answered the same problem by seating the
button per row, between the markers and the cost values, and holding it
still with a plan-lifetime high-water mark. Two pieces of machinery existed
only for that, and this branch deletes both: `Services/TreeIgnoreKeyPlacement.cs`
(56 lines) and `Services/TreePillRunInkFloor.cs` (51 lines), with their two
test files, 396 test lines in all, and their entries in
`TaimisToolbench.csproj`.

The slot they seated goes with them. `Services/TreePillRunLayout.cs` loses
`ReservedSlotWidth`, `AnchoredSlotX` and `LeadingLimitX` - the arithmetic
that reserved a band inside the decision column and fitted the marker run
against what the band left. It keeps only `HeaderInkWidth`, the width the
"Source" heading centres over. `TreePillColumnMath.RequiredWidth` loses its
`anchoredWidth` argument: no row reserves anything for the button in that
column now.

`Views/Rendering/TreeSectionController.cs` also drops `CostInkX`, which
existed to tell the seating rule how far left the cost values reached.

### The fix

`PlanRelayoutMath.ComputeTreeColumnEdges` gains a fourth column that closes
every row after Cost, in the shape `Services/RankerRowLayout` already uses
for the Ranker's row actions: the button takes the row's right edge and the
data columns end a gap short of it.

```
rightEdge     = panelWidth - rightMargin
costRightEdge = rightEdge - TreeActionColumnWidth - TreeActionColumnGap
pillColX      = costRightEdge - costColumnWidth - pillColumnWidth
```

`TreeActionColumnWidth` is `GlyphButtonMetrics.RowActionWidth` 21 - the
button is Blish's own window close control at its measured box, and the
column is exactly as wide as the control in it. `TreeActionColumnGap` is 4,
its own number rather than one derived from the decision column's internal
padding, so tuning that padding cannot move a column two places away.

`TreeColumnEdges.ActionButtonX` is derived as `CostRightEdge +
TreeActionColumnGap` rather than stored, so the two can never be given
different answers. It is a function of the panel edge alone, so the button
sits at one x on every row and moves only when the window does - not for a
re-solve, and not for either data column changing width.

The column carries no heading, matching the Ranker's unlabelled action
column. The hit-test guard that stops a row answering a click its button is
about to answer needed no change: it reads the controls' own live geometry.

### No window width paid for the column

The new column costs 21 + 4 = 25px of every row, and it is paid for out of
`PlanRelayoutMath.TreePillColumnWidth`, which drops 256 to 231.
`WindowSizing.MinWindowWidth` is unchanged at 1378, and the sum in
`docs/research/minimum-window-width.md` section 9.4 is arithmetically
identical:

```
629 + 24 + 256 + 335 + 8       = 1252   before
629 + 24 + 231 + 335 + 25 + 8  = 1252   after
```

The 25px was reserve the decision column was not using. 256 was derived to
hold CRAFT / TP / VENDOR / IGNORE at a 10px margin when IGNORE was a 53px
word; the button that replaced it draws 21px, so the floor had been 44px
wider than its own derivation asked for and never followed the change.

Measured at Menomonia 14 against the installed XNBs, by the method that
report's section 9.1 sets out: the widest run the column can be asked to
hold from a plan's structure alone is CRAFT / TP / VENDOR at 171px, which
231 seats at full padding with 56px to spare. The run's budget at the floor
goes from 225px to 227px, so no row can chip that did not chip before, and
every figure the window minimum is derived from is unchanged, including the
deepest realistic row's 24px clearance before the decision column.

### Regression coverage

`tests/TaimisToolbench.Tests/Services/PlanRelayoutMathTests.cs` gains two
cases for the new column. One computes the edges twice at one panel width
with both data column widths changed - 256/150 against 420/335 - and
asserts `ActionButtonX` and `CostRightEdge` are identical while `PillColX`
is not, which is the property a re-solve was breaking. The other pins the
button between the cost values and the margin: exactly `TreeActionColumnGap`
right of `CostRightEdge`, and its right edge exactly on `panelWidth -
rightMargin`.

`TreeRowPillHitTestTests` now builds its boxes from
`ComputeTreeColumnEdges` at the module's minimum window rather than from
stand-in column constants, so the guard is driven against the grid the
renderer draws. It still drives both the live and the ignored row, because
the markers beside the button still change wholesale across the click.

`docs/file-budgets.txt`: `Services/PlanRelayoutMath.cs` 453 to 479, and its
test file 778 to 828.

`docs/ARCHITECTURE.md` section V.33 and `docs/research/minimum-window-width.md`
section 9 carry the derivation above.

The branch's commits record no build or test counts.

Gate: NOT RUN - no live game session is recorded in the branch's commits.
To confirm in game, click the ignore button on a deep tree row several times
without moving the mouse: every click should toggle and none should expand
or collapse the row, and the buttons should form one straight column at the
right edge of every row.
