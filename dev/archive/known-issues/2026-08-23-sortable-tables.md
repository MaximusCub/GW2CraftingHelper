> **Frozen record - 2026-08-23, branch `sortable-tables`.** Moved verbatim out of `docs/KNOWN-ISSUES.md`; the heading below is the section's own.
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../../docs/README.md).

## Sortable plan tables (sortable-tables)

Reported in game: the Used Materials section must be column
sortable by clicking its column headers, with visual indicators, sorting
by item and by amount; the Shopping List must be column sortable too.

Both tables' column headers are now their own sort controls. Used
Materials sorts on Item/Amount, the Shopping List on Item/Amount/Each/
Total. One click cycle per column - **None -> Ascending -> Descending ->
None**: the third click restores the plan's own emission order rather
than stranding a reader in a sort they cannot undo, and clicking a
different column starts that column ascending and abandons the previous
one (a table has exactly one active sort column). The default order is
the plan's own, with no indicator drawn anywhere.

- **State and comparators are Blish-free.**
  `Services/TableSortState<TColumn>` holds one table's active column and
  direction plus the click cycle; `Services/PlanTableSorter` orders the
  already-built `PlanRowViewModel`s. Sorting never mutates the caller's
  list and hands the same instance back when no sort is active, so the
  default path allocates nothing. 26 tests.
- **Item sorts ordinal-ignore-case; Amount sorts numerically.** A string
  sort would put 111 before 9; the Amount test pins 9/111/136/816 in
  both directions. Ties keep their original relative order (stable) in
  both directions.
- **The Shopping List's Each/Total columns are not one scale, so they
  sort in three blocks.** A cell there is a coin price, a price paid in
  some non-coin currency (spirit shards, karma), or a genuinely
  unpriceable dash - and a copper amount is not comparable to a
  spirit-shard amount, since the module refuses to invent an exchange
  rate between them. The order is: coin rows (including mixed
  coin+currency rows, keyed on their copper part - the one magnitude
  every coin row shares), then currency-only rows (keyed by currency
  name, then amount within that currency, so every karma row lands
  beside every other karma row), then the unpriceable rows. The BLOCK
  order is deliberately direction-invariant and only the order WITHIN a
  block flips: reversing the blocks would express nothing - 5 spirit
  shards is neither more nor less than 3 gold - and it would float the
  dash rows to the top, where they are pure noise. A row carrying more
  than one currency keys on its ordinally-first currency name and that
  entry's amount, which is stable regardless of the order the resolver
  emitted them in; no attempt is made to add amounts across currencies.
  Within a currency the numeric key is the amount's exact per-unit rate
  where one exists (`CurrencyAmountViewModel.UnitRate`, set by
  `CurrencyDisplayResolver` beside every "Each" amount), NOT `Amount`: a
  rate that does not divide evenly deliberately leaves `Amount` at 0 and
  shows the rate as bundle text ("912 for 92" - the live Philosopher's
  Stone case), so keying on `Amount` would sort every bundle-priced row
  as if it were free and tie them all with each other. `UnitRate` is a
  sort key only; nothing renders it.
- **The indicator rides inside the clickable header label.** The label
  IS the click target, and its text carries the ASCII "^"/"v" (the
  tree's caret vocabulary - M12 unified the module on ASCII). That keeps
  `CTableHeaderRenderer`'s and the Shopping List header's relayout
  closures correct for free: both right-align off the label control's
  own `Width`, which already includes the indicator, so the x-tracking
  that follows a drag-resize never sees a separate control to miss.
  `CTableHeaderRenderer` gained two optional click actions, omitted by
  every other c-table caller (Required Recipes, Required Disciplines,
  the tree), whose labels stay inert exactly as before. Since an
  unsorted column deliberately shows no indicator, a hover tint and a
  one-line tooltip are what say "clickable" before the first click. That
  tooltip is load-bearing, not decoration: a Blish `Label` only captures
  the mouse while it carries one (this file's repeated finding that a
  label swallows its container's tooltip), so removing it would silently
  kill click delivery to every sort header - stated in
  `SortableHeaderLabel.MakeClickable` so a future edit cannot drop it
  unaware. Gate item 1 exercises a real click on a `Label`, which is the
  one assumption in this branch that is inferred rather than measured.
- **A click re-renders the plan.** Section rows are a `FlowPanel`'s
  children in flow order, which is not reorderable in place, so the
  sort is applied the one way it can be: `PreserveScrollAcross(() =>
  RenderPlan(_currentPlan))` - the same synchronous full rebuild the
  "Hide Unlocked Recipes" checkbox and a tree pill's re-solve already
  run from inside their own event handlers, rather than a second
  deferred mechanism. Row COUNT and row heights are identical before and
  after, so `PlanContentHeightMath` lands on exactly the same section
  height and the reader keeps their scroll position.
- **Sort state survives a re-render of the SAME plan, and only that.**
  ~~It lives on the view for the session (never persisted), unlike
  `_sectionExpansion`, which a new Generate deliberately resets to the
  section defaults.~~ **Superseded by the font-and-polish round** - the
  in-game feedback asked for sort state to reset to defaults when a new plan is
  generated, and the claim above was the behaviour objected to. It
  now has exactly `_sectionExpansion`'s lifetime and resets in the same
  place: a re-sort, a tree pill override and a re-solve all keep it (they
  re-render the same plan and never reach TriggerGenerate's commit
  point), a new Generate clears both tables to `None`. Still never
  persisted. See the "Font bump and decision-round polish" section.

Build 0 errors, 2147 StyleCop warnings (2135 before; the 12 added sit in
the same rule families the codebase trips throughout). Suite 2229 passed
/ 0 failed (2203 baseline, +26: eight on the click cycle, eighteen on
the comparators; existing resolver tests gained assertions pinning the
per-unit rate), tree clean, nothing pushed.

Sandbox check items:

1. Generate a plan with a long Used Materials list. Click the "Item"
   header: the rows reorder A-Z and the header reads "Item ^". Click it
   again: the order reverses and the header reads "Item v". Click a
   third time: the plan's own order is back and no header carries an
   indicator. Hovering any of the two headers tints it before any click.
2. Click "Amount" on the same table: rows sort by quantity NUMERICALLY -
   the fixture's 111x/136x/816x rows land in that order ascending and
   816/136/111 descending, and a single-digit row (9x) sorts below 111x,
   never above it. The Amount column and its header stay aligned on the
   same right edge as before, and still do after a window drag-resize.
3. Shopping List: each of Item / Amount / Each / Total sorts on click and
   shows its indicator, and only one header carries an indicator at a
   time. On Each/Total, coin-priced rows come first ordered by value,
   then rows priced in a currency (grouped per currency), then any dash
   row - and the dash rows stay at the BOTTOM when the direction is
   flipped to descending.
4. With a sort active on both tables, press Generate again for the same
   item: both tables come back sorted the same way with the same
   indicators showing. Scroll down to the Shopping List, click a header
   there, and the view stays where it was rather than jumping to the top.

Gate: PASS (2026-08-23 sandbox session, branch build at the
review-fix HEAD, captures preflight/gD1-gD3, restored x77 plan).
Clicking the Shopping List's Amount header sorted ascending with the
"^" indicator (111x Mystic Coin, 136x Obsidian Shard, 136x Glob of
Ectoplasm, 816x Philosopher's Stone - the 136x tie kept source
order, stability live); the second click flipped to "v" descending
with the tie STILL in source order both directions; scroll position
held across both rebuilds and the label-disposing click handler
(the repo's first Label doing the rebuild-from-own-event pattern)
survived repeatedly. Used Materials' headers carry the same wiring
(same factory path); the bundle-rate Each ordering ("1 for 10"
Philosopher's Stone keyed on its true 0.1/unit rate) is pinned by
the new comparator tests and visible implicitly in the ascending
capture (816x at 82 currency total sorts within its currency
block). Third-click reset-to-plan-order not captured (two clicks
shown); pinned by TableSortStateTests.
