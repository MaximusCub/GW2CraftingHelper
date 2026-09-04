> **Frozen record - 2026-08-22, branch `audit-h-density`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Audit batch H: table density (audit-h-density)

Two UX-audit findings, one premise: every data table
in the module splits the name (pinned far left) from the numbers (pinned
far right), so widening the window widens an empty band down the middle
of every row 1:1 rather than making the table more readable. The audit
measured 330-520px of that band at ordinary widths.

- **M2, dead gutters in the plan tables.** All seven tables in the
  Crafting Plan's scroll column - the recipe tree (pill + cost columns),
  the Summary currency table (Required/Have/Needed + the full-coverage
  marker), Used Materials (Amount), the Shopping List
  (Amount/Each/Total), Required Recipes (Status), Required Disciplines
  (Level) and Crafting Steps (its right-aligned sublabel) - anchored
  their right-hand block to `panelWidth - 8`. Each now pulls that block
  in beside the names:
  `PlanRelayoutMath.RightBlockX` takes the block's pinned x and the widest
  name extent the table renders and returns the pulled-in x, clamped so it
  never moves RIGHT of the pinned position (a narrow window degrades to
  exactly the previous layout) and never left of `TableRightBlockMinX` (a
  table of two-letter names should still read as a table). The widest name
  extent comes from a per-render, data-derived measure pass over the
  UNTRUNCATED names - truncated widths would be circular, since the
  ellipsis budget is derived from the block position. It is measured once
  and cached alongside the column maxima each table already cached, so a
  resize tick re-derives edges without re-measuring anything.

  The tree rides the whole-tree pre-scan batch D introduced
  (`TreeCostColumnMath.ScanColumns` - the cost sub-column widths and the
  name extent now come out of the SAME single walk), for the same reason
  that scan covers unbuilt rows: rows are built lazily, so a
  visible-rows-only extent would move every column the first time a node
  was expanded. The Shopping List already pre-measured Each/Total per
  render and simply measures two more things in that loop; Used
  Materials, Required Recipes, Required Disciplines and Crafting Steps
  had no pre-scan and get one.

  The four flat tables share one expression for the anchor,
  `PlanRelayoutMath.RightBlockRightEdge` (pull the block in, then derive
  its right edge), and one margin constant, `TableRightMargin`. Two of
  the three later arrivals have a specific worth recording. Required
  Disciplines' Level column is bounded by the character-availability
  text rather than the discipline names, and that column's own
  "Characters" header counts into its extent; because the breathing room
  (24) exceeds the availability text's gap (12), the pull-in can only
  widen that ellipsis budget, never narrow it. Crafting Steps' name run
  is cursor-concatenated and never ellipsized, so its scan reproduces
  that concatenation exactly, and its TimegatedNotice rows - plain text
  rows with no columns - take no part in it. A section with no right
  column at all (no status tag, no level, no sublabel) measures a zero
  name extent and stays pinned, byte-identical to the previous layout.

  Fix round: the chrome had to follow the columns. A row divider or a
  header band spanning the full panel width no longer bounds the table
  it belongs to once that table's block has moved in - at 1600px the
  shopping list's rules ran ~1000px past the last number, and the
  currency table's dark header band spanned the window with its labels
  clustered in the left half. `RowRelayoutHelpers.FinishRow` takes a
  `dividerWidthForWidth`, `CTableHeaderRenderer` bounds its band by the
  `rightXForWidth` its callers already pass, and the currency table has
  `SummarySectionLayoutMath.CurrencyHeaderBandWidth`. All three resolve
  to exactly the panel width whenever the table's block is still pinned,
  and all three clamp to it, so a narrow window draws what it always
  drew and no caller's arithmetic can outrun its own row.

  Two invariants make this safe rather than merely tighter. First, the
  tree's pill and cost columns move as ONE block, so
  `maxRightEdge - pillColX` is exactly what it was before and batch E's
  `ComputePillFit` escalation (tighten, then "+N") sees an unchanged
  budget - a pill that fitted still fits, and no pill that was hidden
  becomes visible at some width, which is what its "+N" tooltip already
  promises. Second, `TableGutterBreathingRoom` (24px) exceeds every
  name-to-column gap that feeds `NameMaxWidthBeforeColumn` (8 in the tree,
  12 in Used Materials/Shopping List, 14 in the currency table), so
  closing the gutter can never ellipsize the very name it was measured
  from - asserted directly, per table, in `PlanRelayoutMathTests`,
  `ShoppingColumnMathTests` and `SummarySectionLayoutMathTests`.

  The tree's "Cost" header had to follow its column, so
  `CTableHeaderRenderer` gained `rightXForWidth` beside the
  `middleXForWidth` batch D added for "Source".

- **M8, Snapshot header density.** The header spent ~179px on five sparse
  rows before the first result: title+buttons, status, a search row empty
  for everything right of the dropdown, a full-width checkbox row, and a
  24px unlabelled coin row. The source checkboxes now occupy the search
  row's empty right half - but only while the whole run fits there in ONE
  row. Sharing the row halves the width the run flows into, and the fix
  round measured what that costs a real roster: 19 cells (15 characters
  plus the storage locations and the master toggle) flowed ~6 per row at
  full width - 4 rows, exactly the cap, every filter visible - and ~3 per
  row beside the search box, putting roughly a third of the filter set
  behind a scrollbar inside a 117px box to save 38px of header. Past one
  row the run therefore falls back to its own full-width row below the
  search box, gap included, exactly as it sat before. The saving that
  motivated the move is untouched, since a run that fits beside the
  search box is precisely the case where sharing costs nothing.

  `SourceFilterFlowLayout` remains the layout engine - its 4-row cap and
  past-the-cap scrolling are untouched - and is simply handed the
  placement's width, with the panel carrying the start offset so cells
  still flow from 0 in their own coordinates. Cells are still laid out
  sequentially from their own measured widths (verified, not changed).
  `Services/SnapshotHeaderLayout` holds what follows: the reduced width,
  the shared/own-row placement (x, offset y, width), and the band height
  - the taller of the search row and the run while they share, the search
  row plus the gap plus the run when they do not. The mode is the flow's
  OUTCOME, not an input, so `ApplyTopRegionLayout` flows beside the
  search box first and re-flows full width when that wrapped; both modes'
  caps are read up front and both join the resize early-out's cache key,
  which is the container width. The search row's own panel is sized there
  too: it stops at the run's start x while they share (two overlapping
  full-width panels would leave which one receives a checkbox click to
  child ordering) and spans the row when they do not. The coin total
  gained a dim "Coin" caption (rebuilt with the segments, since the
  refresh disposes that panel's children) so it stops reading as a stray
  list row.

Known limit, deliberate: below a content width of ~470px a SHARED filter
run would have no width to flow into. The window enforces a 930px minimum
(884px content region), so that state is unreachable; `SourceFilterWidth`
floors at 0 rather than going negative, and `SourceFilterFlowLayout`
already degrades to one cell per row - wrapped, then scrolled, never
clipped away. A run that wrapped would in any case have dropped to its
own full-width row before reaching that width.

Height-math check at this HEAD: no renderer-emitted height changes.
Batch H moves columns horizontally only - every row height, every
`PlanContentHeightMath` contract and the Summary section's own
`BodyHeight` are untouched. The Snapshot tab's Y arithmetic does change,
but it is view-local (`CoinRowY`/`ContentY`) and now routed through
`SnapshotHeaderLayout.SearchBandHeight`, pinned by tests.

Validation: build 0 errors, full suite 2113 passed / 0 failed (2072
baseline). No new test references Blish.

What the desktop gate should look at:

1. **Tree gutter closed:** generate a plan and look at the Recipe Tree at
   the default window width. The pill column must sit just right of the
   longest item name rather than out at the panel edge, with no wide empty
   band between name and pills. Expand a deep branch: the columns must NOT
   jump when previously-unbuilt rows appear.
2. **Header row tracking:** the "Item / Source / Cost" header must sit
   over the columns it names in both states, and must stay over them while
   the window is dragged wider and narrower - including at the 930px
   minimum, where the layout should look exactly as it did before this
   branch.
3. **Shopping List and Used Materials:** the Amount/Each/Total block (and
   Used Materials' Amount) must be pulled in beside the names, header
   labels still aligned with their columns. A long item name must still
   ellipsize and keep its source badge out of the Amount column - and a
   name that was NOT truncated before must not have become truncated.
4. **Currency table:** Required must start relative to the currency name
   column, and the green "OK" marker must stay at the block's right end,
   not the panel's.
5. **Required Recipes, Required Disciplines, Crafting Steps:** scroll the
   whole plan at a wide window (1400px+) and watch the right-hand
   columns. Status, Level and the craft-step sublabels must sit beside
   their names like the other four tables - no column may still be out at
   the panel edge, and nothing may zig-zag between two anchors down the
   scroll. The "Recipe / Status" and "Discipline / Characters / Level"
   headers must stay over their columns at every width, and the
   "Characters" header must not be covered by the Level column when the
   availability text is short.
6. **Row rules and header bands:** the 2px rule under each shopping /
   used-material / recipe / discipline / craft-step row must stop just
   past that table's last column, not run on to the panel edge; the dark
   header bands (currency table, Recipes, Disciplines, the tree) must
   likewise end just past their last column. At the 930px minimum both
   should look exactly as they did before this branch.
7. **Snapshot search row holding the checkboxes:** with a small roster the
   source checkboxes must sit to the right of the content-type dropdown
   on the same row, clickable (each click must still filter the results),
   with the result list starting visibly higher than before. With a large
   roster (10+ characters) the run must instead drop to its own
   full-width row below the search box and use the full width there - the
   pre-branch layout - rather than wrapping into the narrow half. Check
   the handover both ways by dragging the window wider and narrower: the
   run must move between the two rows cleanly, with no overlap of the
   search box or dropdown at any width, and every character reachable in
   both modes.
8. **Coin caption:** the wallet total must read as a dim "Coin" label
   followed by the gold/silver/copper figures, with each coin icon still
   to the RIGHT of its number.

Gate: PASS (2026-08-23 desktop session, branch build at the
review-fix HEAD, captures preflight/gH1-gH3). (1) The dead gutter is
visibly closed across the plan's scroll column on the restored x77
plan: Used Materials quantities sit directly beside the names
(x~338 vs the old ~955), Shopping List's Amount/Each/Total columns
pulled in with their HEADERS tracking the moved positions, tree
pills beside the names, and - the fix-round's work - the row
dividers end at their table's right edge instead of running the
full panel. (2) Snapshot header: the dim "Coin" caption renders
before the total, and with the 10-source test roster the checkbox
run correctly took the fall-back-below-the-search-row path (the
run does not fit in the shared-row width at 1400px), preserving
the wrap + cap behavior - the shared-row density win applies to
smaller rosters and was not demonstrable with this fixture.
Recipes/Disciplines sections have no live coverage in the fixture
plans (Mystic Forge plans render neither) - their migration stands
on the shared RightBlockX primitive and its per-table tests.
