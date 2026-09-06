> **Milestone record - 2026-08-30, branch `wave7-fieldtest`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Seven display fixes from one round of in-game field reports

A field test of the module produced a list of small display faults across
the Snapshot tab, the Crafting Plan tab, every sorted table and the
confirmation dialog. Seven of them are fixed here. They are unrelated to
one another, so each has its own section below. The branch touched 28
files for 1,091 insertions and 312 deletions, and merged as pull request
238.

### Rows scrolling past a pinned section header painted over it

The Snapshot tab pins a section's header band at the top of the viewport
while that section's rows scroll under it. A field report at 815 items
showed the rows drawn over the pinned band at full brightness.

Root cause is paint order. `Views/Rendering/StickyHeaderHost.cs` parents
the pinned band's clip as a sibling of the scrolling panel at
`ClipZIndex` 1, and Blish's default `Control` ZIndex is 5, so the clip
paints first and the scrolling panel's whole subtree paints on top of it.
The low ZIndex cannot simply be raised: Blish's hit test walks children
by ZIndex descending, and the low value is what lets a mouse wheel over
the pinned band reach the scrolling panel behind it.

The fix makes the clip line a scissor bound rather than a paint-order
question. `StickyHeaderHost.Place` now returns the pinned band's bottom
edge and `Update` folds those into a new `PinnedBandBottom` property, the
lowest pinned edge of the frame or null when none is pinned.
`Views/Rendering/ClipCutoff.cs` gains `StickyClipAuthorityFlowPanel`,
which reads that property once per paint and publishes
`ClipCutoffMath.CutoffTopFor` of it, falling back to its own top edge.
`Views/MainView.cs` builds its scrolling content panel as that type. Four
containers inside the viewport that were plain `Panel` - the result grid,
a section's title panel, its divider and both row panels - became
`ClippedPanel` so they re-assert the published line instead of handing
their children a stale one.

### The plan viewport left a dead strip under the separator rule

`Services/TopRegionLayoutMath.cs` placed the scrolling viewport
`SeparatorToContentGap` 5px below the separator rule at the bottom of the
plan's top strip. Scrolled rows therefore clipped 5px below the rule and
a strip of backdrop sat between the two.

The constant is now 0, so the viewport's top edge is the rule itself. In
the single-row, no-toolbar case the computed `ContentY` moves 111 to 106
and `TopRegionHeight` 116 to 111; `SeparatorY` and every row above it are
unchanged. `Views/CraftingPlanView.cs` keeps the top strip at
`TopStripZIndex` 1, which now carries real pixels: at the sub-unity GW2
UI sizes the cutoff's slip budget lands inside the rule's own 2px, and
the strip paints over it.

### The IGNORE toggle in the recipe tree was a hand-drawn pill

The toggle was built by the same `CreatePillPanel` path as the decision
pills beside it, with a glyph face, its own label offset and hand-wired
hover and press handlers. It is now the module's own `FeedbackButton`, a
`StandardButton` subclass, sized at `GlyphButtonMetrics.RowActionSize` -
the square the Ranker and Plan History row actions already use.

`Views/Rendering/FeedbackButton.cs` gained a `PlateTint` property for
this. A tinted plate keeps the atlas face and the enabled ink even while
the control is disabled, because Blish's disabled look is a flat grey
plate that would erase the ignored state the tint carries. The toggle
draws the ignore-active amber as its plate while the item is ignored, and
`Enabled` carries inertness on a dimmed row instead. Because the button
fills its slot exactly, its hit rectangle is the same in both states.

`Views/Rendering/TreeSectionController.cs` dropped the three glyph
helpers the pill needed (`IgnoreGlyphText`, `IgnoreGlyphFont`,
`IgnoreGlyphWidth`) and the `GlyphPillLabelY` constant;
`ReservedIgnorePillWidth` is now just the row-action square. The row's
pill list changed from `List<Panel>` to `List<Control>`, since a button
is not a panel.

### The decision-pill column could claim only half the window surplus

`Services/TreePillColumnMath.Affordable` capped the pill column at its
256px floor plus half of whatever the panel had beyond the module's
minimum window width. A field report showed a row still chipped to a "+1"
overflow chip while space sat free on both sides of the column.

The cap is now the space actually available between the column's two
neighbours' minimums: the whole panel surplus past the minimum window
leftward, plus the cost column's reserve above what its rows draw
rightward. A new `RightClaim` says how much of the result came from the
cost side, clamped to that slack. `TreeSectionController` carries the
claim in `_pillColumnCostClaim` and `EffectiveCostColumnWidth` nets it
out of the cost column's reserved width, never below the scanned
sub-column total. The claim is swapped one for one, so the pill column's
left edge, every cost value and every name budget stay where the
unclaimed layout put them.

`ScannedPillColumnWidth` gained an `out int costClaim` parameter rather
than writing a field, because `TryRefreshInPlace` calls it as a pure gate
question and discards the claim.

### A currency icon beside digits still drew a border ring

Every currency icon went through one framed path. Beside a number the ring
read wrong: there the icon is a unit symbol in the gold, silver and copper
coins' role, and the coins carry no border.

`Services/IconFrameGeometry.cs` gained `CurrencyIsFramed(tier)`, which
answers true for `CurrencyListRow` and false for `CurrencyBarRun` and
throws `ArgumentOutOfRangeException` on any other tier.
`Views/Rendering/IconControls.CreateCurrencyIcon` branches on it and
builds an unframed icon for the bar tier. The change is pixel neutral for
layout: a currency tier's measured window is the whole box either way, so
no inline segment advance moves, and that advance is a term in the
minimum-window-width derivation.

### The sort indicator read as attached to its column label

`Services/SortIndicatorLayout.Gap` was 4px, and a field report said the
mark looked joined to the word. It is now 8.

One constant drives every sorted table, and the Snapshot grid derives
from it: the Amount column floor moves 95 to 99, so
`SnapshotItemGridLayout.SnapshotMinColumnWidth` moves 582 to 586. The
window width at which the Snapshot grid gains a second column moves 1290
to 1298 and a third 1872 to 1884. The
`tests/TaimisToolbench.Tests/Goldens/grid-law-sweep.txt` golden was
re-derived at the old constants byte for byte before being regenerated at
the new ones.

### The confirmation dialog's title was left-aligned

Blish paints `WindowBase2.Title` left-aligned at a fixed indent with no
alignment control. `Views/ModalDialog.cs` now sets `Title` to an empty
string and draws the word "Confirm" itself as a `Label`, in the same
display face and the same `ColonialWhite` Blish paints it in, with
`ClipsBounds` off because the seat is above the window's content region.

Two helpers back the seat. `Services/DialogLayoutMath.TitleLineY` is -11,
the line-box top Blish's own title paint uses, and `TitleX` centres a
title over the window width and pins it to 0 rather than overhanging when
the title outmeasures the window. `Views/DialogWindow.cs` gained
`ContentTopY` and `ContentLocationFor`, which convert a window-relative
point into the content-relative `Location` Blish positions children by,
so nothing outside that class assumes the content origin. At the width
`DialogLayoutMath.Measure` floors a titled dialog to, the centred title
starts at the same 80px indent the built-in one drew at and ends 80px
short of the right edge, so centring cannot reach the exit button that
reserve protects.

### Regression coverage

- `TopRegionLayoutMathTests` asserts `ContentY == SeparatorY` at every row
  count and toolbar state, replacing the strict inequality that had
  encoded the dead strip.
- `TreePillColumnMathTests` gained 319 lines pinning the wider cap and
  `RightClaim`, including the reported row that used to chip and the
  overflow chip's survival at the true minimum window.
- `IconFrameGeometryTests` pins both framing contexts and the throw on a
  non-currency tier, and the ring assertion now covers the list tier
  alone.
- `DialogLayoutMathTests` pins the centring, the left pin for an overwide
  title, and that at the measured floor the centred title lands on the
  indent Blish used.
- `SnapshotItemGridLayoutTests` and the grid-law golden moved with the
  doubled indicator gap.

### Validation

The commits record no build or test output, so this record cannot quote
either. A separate commit raised 16 entries in `docs/file-budgets.txt`
with the per-file reason for each.

Gate: NOT RUN - no live in-game confirmation of any of the seven fixes is
recorded on this branch. The failing states were reported from a field
test; a reviewer should open the Snapshot tab with enough items to scroll
a section under its pinned header, generate a plan and check that the
first row clips against the separator rule, click an IGNORE key on a
dimmed and an active row, sort any table, and open a confirmation dialog.
