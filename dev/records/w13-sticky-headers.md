> **Milestone record - 2026-09-04, branch `w13-sticky-headers`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Sticky plan-tab headers, headings seated on the icon gutter, and Blish's own close key

Pull request 242. Three separate pieces of work. The Snapshot tab's
pinned column headers were extended to the Crafting Plan tab, which
needed a new mechanism because its tables sit in a flow. Every table
whose rows open with an icon had its left-hand column heading indented
past the icon, so the word did not line up with the column it names. And
the row-remove action, which had been an approximation of a close
control, now draws the close control's own texture.

### Sticky column headers on the Crafting Plan tab

The Snapshot tab's bands sit in an absolutely-placed grid, so lifting one
out to pin it leaves a hole and nothing moves. The Crafting Plan tab's
bands are children of a `FlowPanel`, which measures its own children: a
band lifted out of one closes the flow up by the band's height and moves
every section below it.

**The fix.** `Views/Rendering/HeaderBands.cs` gains `FlowBand`, a band
plus a fixed-height panel that holds its place in the flow, and
`CreateColumnHeaderBandInFlow` to build the pair. The flow measures the
spacer, so pinning the band moves nothing. `FlowBand.Resize` sizes both
halves together, because a Blish container clips its children to its own
bounds and a spacer narrower than its band would cut the band's right end
off.

The spacer is a `WheelTransparentClippedPanel` rather than a plain
`Panel` for two independent reasons: it is one more container inside the
viewport, where a plain one re-opens the clip drift `ClipCutoff` exists to
close; and it sits between the sticky clip and the cursor, where a
container that keeps the `MouseWheel` flag breaks the walk that lets the
wheel through.

`Views/Rendering/ISectionRelayoutSink.cs` gains `TrackStickyBand(FlowBand,
Func<int> rowsHeight)`. The row height is a delegate and read live, not
sampled: the recipe tree's rows change height when a node is expanded,
with no rebuild, and a band pinned over a stale height leaves with the
wrong row.

Six tables are wired: Shopping List, Used Materials, Required
Disciplines, Required Recipes, the Total Cost currency table and the
Recipe Tree. Crafting Steps and Notes have no band. A section's presence
is its own flow's `Visible`, so a collapsed section reports itself absent
rather than pinning a band over a table nobody can see.

The Total Cost currency table is the only one that is not the last thing
in its container - a multi-item note and any footnotes flow below it - so
its extent is `rowCount * CurrencyRowHeight` rather than the flow's
height.

**A real regression was found in the ZIndex.** This tab's viewport top IS
the separator rule (`ContentY == SeparatorY`), so a pinned band's clip and
the rule overlap by the rule's own 2px. At equal ZIndex the paint order
falls to sibling index, and the clips are created lazily when a band is
tracked, long after the separator is built, so a pinned band would have
painted over the rule and notched it. `SeparatorZIndex` in
`Views/CraftingPlanView.cs` moves from 10 to 11, one above the sticky
host's clip ZIndex, so the rule keeps painting last over a pinned band
exactly as it already does over a scrolled row. The cost is that the top
2px of a pinned band lose the hit test to the rule, the same trade
already accepted for the first scrolled row. The rule is
wheel-transparent, so the wheel is unaffected.

`CraftingPlanView` holds the host and a separate `_treeStickyBandRegistrations`
list. Only the tree needs one: every other section re-registers its band
as it re-renders it, and the tree is the section whose controls a
preserving rebuild keeps. The host is cleared before the content panel is
emptied, because a pinned band is not a child of the panel being emptied
and giving it back to its spacer is what lets the sweep dispose it.

### A column heading rules on its icon gutter, not on its text

Every table whose rows open with an icon seated its left-hand column
heading at the text x, which indents the word past the icon by the
gutter's width. The word then did not line up with the column it names.

**The fix.** New `Services/ColumnHeaderLabelMath.cs`, 43 lines, with one
method: `LabelX(textX, iconGutterX)`. A column whose rows draw an icon
owns that icon, so the heading rules on the gutter's left edge. A column
with no gutter passes the `NoIconGutter` sentinel and keeps the rule it
had. A gutter at or right of the text is ignored, so the word can only
ever move LEFT of its own text rule and never out of its column.

Headings moved on the plan tab (Used Materials, Required Recipes,
Shopping List, the Total Cost currency table and the Recipe Tree), on
both Snapshot grids, which share one chrome call so Currencies moved with
Items, on the Ranker's Item column, and on the Settings vendor cost
valuations table. The branch's commit states the shift as between 40 and
60px each.

Left alone with reason: Required Disciplines and the Log tab draw no
icons, and Plan History was already on its column's own left edge.

The Ranker's rank column sits left of Item and is a column of its own, so
Item's word lands on the icon gutter and never left of the rank column's
end.

`Services/PlanRelayoutMath.cs`'s `TableLeftHeaderX` existed because
anchoring the tree's Item heading at its own offset put it 8px right of
the identically worded heading below it, so an earlier change nudged one
table by hand. It now reads the shared rule. It stays a rail because the
tree's grid differs - a caret column before its icon puts its depth-0
gutter 10px right of the tables' - and the rail still lands inside the
tree's own Item column.

`Services/SnapshotItemGridLayout.cs` gains `CellIconX`, which had been a
bare literal 2 inside `CellTextX` with two more literal 2s at the row
icon sites. All three now read the constant. The value is unchanged.

The hit area was already correct and is not changed.
`HeaderCellMath.Partition` forces the first range to start at 0, and the
Snapshot's name cell starts at its column's left boundary, so the wash and
the click target already spanned the icon gutter. Moving the word left
moves it further into the cell it sorts. The sort indicator travels with
its word.

### The row-action key is Blish's own close control

The remove action on a table row was a plate carrying a mark drawn from
the module's glyph atlas, sized and weighted to approximate the close
control Blish paints in a window corner. That art is a Blish reference
texture available by name, in the same folder as the button border art the
module already loads.

**The fix.** New `Views/Rendering/CloseKeyButton.cs`, a `Control` that
blits `button-exit`, and `button-exit-active` on hover, at 1:1 pixels.
It is not a mode on `FeedbackButton`, which would have left that class's
plate atlas, four border strips, icon, text and animation sweep live but
unreachable behind a property meaning "ignore most of this class".
`Control` already carries `Click`, `MouseOver`, `Enabled`, `Opacity` and
`Tooltip`, and `PressFeedback.Wire` takes a `Control`.

The measurements are read off the shipped PNGs, not assumed: both
textures are 32x32, the opaque ink is 21x23 at (7, 6), the light plate
inside it is 16x16 and the dark cross 13x13. Blish blits the whole 32x32
into a title bar; a 24px table row has no space for it, so the control is
21x23 and samples the ink rectangle alone, unscaled, with the transparent
surround dropped. The source rectangle is intersected with the texture
bounds, so a future Blish asset of another size cannot sample past the
page, and a missing texture name that resolves to the content service's
error texture cannot either.

`Services/GlyphButtonMetrics.cs` is reworked: the texture is now the
source of the size rather than a derivation from a glyph.
`RowActionSize` is gone, because the box is no longer square, replaced by
`RowActionWidth` 21 and `RowActionHeight` 23 plus the three source-rect
constants. `RankerRowLayout` and `PlanHistoryRowLayout` each gained an
explicit height so no seat can reuse one axis for the other; two of them
had been doing so.

Three seats swapped to the new control: the Ranker row's remove, the Plan
History row's delete, and the recipe tree's ignore toggle. The two carets
beside the Ranker's remove key stay `FeedbackButton`s and keep the same
box.

The ON state is the base texture tinted amber. `PillColors` keeps
`#9C7327` for it, and the tint is a MULTIPLY rather than a replace,
because `button-exit-active` is itself gold and a replaced plate colour
would be the hover look standing still. The gold hover texture is kept
strictly for hover, so an ignored row can never read as a hovered one.

**A compounding bug was fixed on the way.** A dimmed tree row both
disabled the toggle and washed it to `PillColors.DimmedPillFactor` 0.6.
A separate 0.4 disabled fade on top of that would have given 0.24,
noticeably darker than the pills beside it. `CloseKeyButton.DisabledDim`
now READS `DimmedPillFactor`, and the tree's second wash in
`TreeSectionController` is gone. Net weight on a dimmed row is unchanged.

**The shipped glyph font was regenerated.** Nothing drew
`UiGlyphs.RemoveMark` any more, and the CI gate fails the build on an
atlas glyph `UiGlyphs` does not name, so dropping the constant forced it.
The commit records that `tools/build-glyph-font.py --fetch` reproduces
`ref/glyphs.fnt` and `ref/glyphs_0.png` byte for byte, every surviving
glyph keeps its exact metrics, and only the page shrank, 73x18 to 56x14.

### Regression coverage

- `tests/TaimisToolbench.Tests/Services/ColumnHeaderLabelMathTests.cs` is
  new. It asserts the rule against the shipped geometry of the four
  surfaces rather than against fixtures, so a column that moves its gutter
  cannot leave its heading behind. It also pins the Ranker's variance
  across the module's real widths through the production
  `RankerRowLayout.Compute`, and pins that the word stays inside the cell
  that sorts it, through the real `HeaderCellMath.Partition`. An existing
  `TableLeftHeaderX` test asserted equality with `NameX` and would have
  passed for the wrong reason under a weaker fix; it was rewritten to
  assert the gutter.
- `tests/TaimisToolbench.Tests/Services/StickyHeaderLayoutTests.cs` gains
  two cases through the real layout function: the Total Cost shape, where
  more of the section flows below the table, and a table shrinking under a
  pinned band, which is what the tree does on collapse and the one way a
  plan table changes height with no scroll and no rebuild.
- `tests/TaimisToolbench.Tests/Services/GlyphButtonMetricsTests.cs` was
  reworked for the new box, and `GlyphFontDescriptorTests.cs` lost the two
  atlas cases for the deleted glyph.

### Validation

The branch's commits record, at the last commit: suite 4161 + 242 + 3,
build 0 warnings, all 19 invariant gates green. The count moved down by
one from the previous commit because two atlas tests for a deleted glyph
went and one metrics test arrived. `docs/file-budgets.txt` was raised for
the files that grew; `Views/CraftingPlanView.cs` reached 5185 lines.

Gate: NOT RUN - no live in-game check is recorded on any commit. Several
things are not decidable outside the game. For the sticky headers: that
re-parenting a band keeps clicks on it, that the wheel falls through the
new spacer level, that the flow does not move when a band is lifted, that
the separator paints over a pinned band at ZIndex 11, and that a collapse
or a tab switch while pinned recovers within one frame. For the close
key: the blit itself, the hover texture swap, the amber multiply, the
disabled fade, and that the texture name resolves at runtime. For the
headings: whether each word now reads flush with the column under it,
which is a screen judgment. One arrangement is worth a look either way -
the up and down carets and the remove key now share a 21x23 box but not a
look, two parchment plates beside a dark close key, which is the
arrangement Blish's own title bar uses.
