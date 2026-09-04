> **Frozen record - 2026-08-23, branch `snapshot-grid`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Snapshot item grid (snapshot-grid)

Field test: the Snapshot window should consider a multiple-column
display, because a single-column list wastes screen real estate. It did:
at the 1436px window minimum a
Snapshot result row spanned the whole 1330px content panel to show a name
line that needs roughly 420px of it, and the row below it started 52px
further down whatever was left over.

The result list is now a grid, built the way the Settings tab's currency
grid was (batch G): a Blish-free layout service
(`Services/SnapshotItemGridLayout.cs`) that owns the arithmetic and is
covered by real tests, and a view (`Views/MainView.cs`) that only copies
the placements onto controls.

### The minimum column width is 464px, and where it comes from

Derived from the cell content, not chosen:

```
  40  text column left edge - the 32px icon at x=2 plus its gap
 416  a 52-character name line: the count prefix ("9,999x ", 7 chars)
      plus a 45-character item name, at 8px/char - the repo's existing
      upper bound on DefaultFont14, which averages ~7.7px
   8  right pad, clear of the cell edge
= 464
```

Rounding 7.7 up to 8 is what pays for the breathing room; there is no
separate fudge term.

**The breakdown line is deliberately outside that budget.** A row's second
line is its source breakdown ("Character: <name> 250   Bank 250
Material Storage 2000"), whose width is unbounded in the roster's name
lengths and in how many sources hold the item. Sizing a column to it would
price the second column out of every window a player actually uses. It
already ellipsizes with the full text on the row tooltip at every width
(batch J's P2), so per column it simply ellipsizes earlier - which is the
trade this change makes, stated plainly.

### The column count is derived, not written down

`gridWidth / MinColumnWidth`, floored at one. Not capped at two either:
the count comes from the width the player gave the window, and every
column is at least MinColumnWidth across at any count.

The widths it lands on, through `WindowSizing`'s own chain rather than
literals (the tests assert against the constants, not copies):

| window | content panel | grid (less 20px scrollbar) | columns | column |
|---|---|---|---|---|
| 1436 (the minimum) | 1330 | 1310 | 2 | 655 |
| ~1518 | 1412 | 1392 | 3 | 464 |
| under ~1054 | under 948 | under 928 | 1 | the whole grid |

The single-column case is byte-for-byte the list the tab shipped with, so
the narrow path is unchanged rather than newly special-cased. It is only
reachable on a game client too narrow to enforce the 1436 minimum (see
`WindowSizing.EffectiveMinWindowWidth`) - on any client at or above the
minimum the tab is always at least two-up.

### ONE grid panel, not one per run

Item rows are 52px tall and wallet rows 36px, and the two runs share the
list with wallet after items. Three ways to do that; the third is what
shipped:

1. One uniform cell height - rejected, it stretches every wallet row to
   the taller of the two for no reason.
2. Two sibling grid panels in the scrolling FlowPanel, items then wallet -
   rejected, because it makes the wallet run's position a bet on Blish's
   FlowPanel re-flowing a LATER sibling when an earlier one changes
   height. The Settings tab's grid never tested that: its grid panel is
   the last child of its panel, so a missed re-flow there would be
   invisible.
3. One grid panel holding both runs, the wallet run laid out at the item
   run's own height (`Compute`'s `offsetY`). The order is this module's
   own arithmetic, and the FlowPanel has exactly one child whose height
   ever moves.

Reading order is left-to-right then top-to-bottom throughout, so a
two-column list reads the way the one-column list did.

### The refit machinery is grid-aware now

`RefitResultRows` (the trailing, width-only, settle-debounced repack from
the batch J fix round) no longer just re-ellipsizes rows at a new panel
width. It recomputes the grid - a widened window can gain a column, a
narrowed one drop back to the fallback - moves every cell to its new slot,
and re-ellipsizes each line against its COLUMN width, re-deciding the
per-line and row-strip tooltips through the same `FitRowTextLabel` /
`ApplyRowStripTooltip` rules as before. Still no search re-run and no
dispose-and-recreate.

**What that does and does not buy for the scroll position.** A repack that
keeps the column count leaves the scroll alone: the grid panel's WIDTH
moves, its height does not. A repack that CHANGES the column count is a
different story - the panel's height moves with it (2 -> 3 columns drops it
by about a third), and Blish's Scrollbar zeroes the scroll position a frame
after any content-height change, measured and written up under "The grid
panel holds its unfiltered height" in the Settings grid section above. So a
drag across a column boundary snaps the list to the top. That is not
defended against: the Snapshot tab has no scroll-restore machinery (the
module's only one is `CraftingPlanView.PreserveScrollAcross`, which needs a
reflection handle on Blish's private scrollbar field plus a frame-ticker
verify), and a column-count change re-flows every row anyway, so there is
no old position left to hold. The Settings grid's own answer - pin the
panel to a height the filter cannot move - does not port: there the height
is a function of a FIXED 47-cell list, here it is a function of the column
count itself.

`LayoutResultGrid` is the single writer for the grid's geometry, shared by
the rebuild and the repack; the rebuild passes `refitText: false` because
its cells were just built at that same column width, and re-ellipsizing
every label twice would double the MeasureString work of a rebuild that
already runs once per pause in typing over a list that can reach into the
thousands of rows.

### Two incidental corrections

- The grid is laid out inside the content panel's width less a 20px
  scrollbar allowance (`LogTabContent`'s own precedent), so the rightmost
  column ellipsizes before it runs under the scrollbar rather than behind
  it. The single-column list used to run under it by 12px.
- `RowTextX` / `RowTextRightPad` now come from the layout service, the way
  `SettingsTabContent` shares its cell constants, so the minimum column
  width cannot drift from the geometry the cells are actually built with.

### Unchanged, deliberately

The empty-state line ("No snapshot available...") and the no-match lines
("No items match \"x\" in the selected sources.", "No currencies match
...") are still parented to the content panel and span it - they are
messages about the whole list, not cells in it. The search box, the
content-type dropdown, the source-filter checkboxes and their master
toggle, and the coin row above the list are all untouched.

### Desktop gate (live, required)

1. Snapshot tab at the window minimum (1436): the result list renders
   **two columns**, and reading it left-to-right then down matches the
   order the single-column list had - the first four items are 1, 2 on
   the top row and 3, 4 on the second, NOT 1, 3 / 2, 4. With the list
   long enough to show the scrollbar, confirm the RIGHTMOST column's text
   stops clear of it. That is the one live check on the chain the unit
   tests cannot make: they can pin the arithmetic, but only the running
   tab can confirm MainView's content panel really is
   `TabPanelWidthFor(window) + 20` wide (i.e. that this tab still adds no
   right-edge padding of its own).
2. Type into the search box and watch the list repack: the grid refills
   from the top left with no gaps and the last row is the only partial
   one. Toggle a source checkbox and a content-type dropdown value for
   the same check. Scroll position is deliberately NOT part of this step
   - a search rebuilds the row set, which moves the content height, and
   Blish resets the scroll to top on that. Pre-existing behaviour of this
   tab, unchanged by the grid.
3. An item whose breakdown is too long for one column (search for a
   material held by several characters plus bank and material storage):
   the second line ends in an ellipsis and hovering the row shows the
   FULL breakdown text in a tooltip. Confirm the same for a long item
   name on the first line.
4. Drag the window narrower than ~1054px (only possible on a client that
   cannot enforce the 1436 minimum): the grid falls back to one column,
   every row full width, nothing clipped at the right edge, and the
   tooltips still carry whatever no longer fits.
5. Drag the window wider, past ~1518px: a third column appears and the
   rows repack into it without a rebuild - no row is left stranded at an
   old column position and no cell overlaps its neighbour. Two separate
   scroll checks here, per "What that does and does not buy for the
   scroll position" above: a drag that stays inside one column band
   leaves the scroll where it was; the drag that adds the third column
   snaps it to the top. Both are the expected result.
6. With the content-type dropdown on "All" and a search that matches both
   (e.g. a term hitting an item and a currency): the wallet rows still
   render **below** the last item row, never interleaved with it, and the
   wallet run starts its own new grid row rather than filling the gap
   beside a half-empty item row.
7. Empty states: clear the cache (no snapshot) and separately search for
   nonsense text - both messages span the panel at the top left, not
   inside a column-width cell.

Gate: PASS (2026-08-24 desktop session, branch build at the
review-fix HEAD, captures preflight/gSG1-gSG3). At the 1436 window
minimum the result list rendered as TWO ~655px columns in
left-to-right reading order (Augur's Stone | Green Wood Log /
Mystic Clover | Mystic Coin / ...), the wallet run starting its own
row below the items with its shorter cells, and the whole
7-item + 4-wallet fixture occupying roughly half its former
vertical space. Green Wood Log's six-holder breakdown ellipsized at
the COLUMN edge with the row tooltip carrying the full text
(gSG2). Filtering "essence" repacked to three wallet cells in
reading order with no scroll jump at the unchanged column count
(gSG3). Not staged live: the 3-column layout (needs a >1518px
window - the dummy maxes near 1490 effective), the one-column
fallback (unreachable at the enforced minimum), and the documented
scroll-reset-on-column-count-change (accepted, recorded with its
measured Blish cause rather than defended against).
