> **Milestone record - 2026-09-05, branch `w18-ignore-x-gap`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## The recipe tree's IGNORE key moves into the gap between Source and Cost

In the Crafting Plan's recipe tree each row can draw an IGNORE key at the
end of its decision pills. The key was pinned to the decision-pill
column's own right edge. That column reserves the width its widest row
needs, so on a row carrying a wide annotation pill the key drew hard
against the pills while the reserve the cost column holds above its ink
stayed empty on the key's other side. It read as the end of the pill run
rather than as the row action it is. Merged as pull request 248.

### The defect, measured

At a 1230px window the key had 8px of clearance to its left and 183px to
its right.

`Services/TreePillRunLayout.AnchoredSlotX` returns one x for every row:
the pill column's right edge less the key's width. Nothing to the left of
it was consulted, which was the stated point of the rule.

### The fix

`Services/TreeIgnoreKeyPlacement.cs` (new, 56 lines, Blish-free) centres
the key in the band its own row's pill run and the cost column's leftmost
ink leave it. It never sits nearer the run than the pill gap and never
nearer the cost ink than `TreePillColumnMath.TrailingClearance`, the
clearance the pill column already keeps from that column. A row that
leaves no band between the two falls back to `AnchoredSlotX`, which is
where the key has always been.

Because the band belongs to the row, a row with a short run seats its key
further left than a row with a long one, so the column of keys is no
longer straight.

`TreeSectionController.CostInkX` supplies the right-hand bound: the cost
column's right edge less `LeftmostInkReach` from the scanned column
widths. That scan measures coin and currency runs only, so a tree with
nothing priced reports 0 and the bound falls back to the cost column's
own left edge, conceding the whole reserve to the unmeasured dash those
rows draw.

The pill run is still fitted against the PINNED key's budget
(`TreePillRunLayout.LeadingLimitX`), never against where the key ends up.
No row loses a pill it used to draw, and the run and the key cannot chase
each other.

### Holding the key still across its own click

Clicking the key re-solves the node as owned, so its source pills are
gone on the next render. Seated against the live run, the key would move
left out from under the cursor that had just clicked it, and the next
click would reach the row and expand the node instead.

`Services/TreePillRunInkFloor.cs` (new, 51 lines, Blish-free) is the
twin of `TreeCostColumnFloor` for one row's run: a dictionary keyed by
node id whose `Widen` returns the widest run that row has reported, this
one included. A row's run may widen the floor and never narrow it. The
floor belongs to the plan and `Clear` runs on a fresh generate, so a row
toggled back and forth settles on one x rather than alternating between
two. The ink is measured from the pill column's left rule rather than in
panel coordinates, so a resize neither widens nor invalidates it.

### The Source heading was centring over the key

`RenderDecisionPills` folded the row's flowed pills and its anchored key
into one right edge and fed that to the header-centring rule. With the
key pinned to the column edge, any row that drew one reported the whole
256px pill reserve, so the heading sat right of every pill under it. With
the key seated per row it would have been worse: the heading would centre
over whichever row happened to seat its key furthest right.

The accumulator moved out of the renderer into
`TreePillRunLayout.HeaderInkWidth(pillColX, runRightEdge)`, which takes
the run alone. `RenderDecisionPills` now computes `runRightEdge` once
from the fitted run and its "+N" overflow chip, and feeds it to both the
heading rule and the key placement.

### Regression coverage

`tests/TaimisToolbench.Tests/Services/TreeIgnoreKeyPlacementTests.cs`
(new, 287 lines) drives the real planner, the real pill fit and the real
placement, so its answer is the answer on screen. It pins that the two
clearances match to within a pixel, that a short run seats the key
further left than a long one, that both bounds hold at every cost-ink
position over a 400px sweep, and that a run filling its whole budget
still clears both neighbours.

`TreePillRunInkFloorTests.cs` (new, 109 lines) pins the one-way widen and
the per-plan clear. `TreePillRunLayoutTests` and `TreeRowPillHitTestTests`
were rewired to drive the extracted `HeaderInkWidth`.

### Validation

The commits record no build or test output, so this record cannot quote
either.

Gate: NOT RUN - no live in-game confirmation is recorded on this branch.
A reviewer should generate a plan whose tree mixes rows with wide and
narrow pill runs, check that each key sits about midway between its own
pills and the cost figures, click one and confirm the second click
reaches the key rather than expanding the row, and check that the
"Source" heading sits over the pills rather than to their right.
