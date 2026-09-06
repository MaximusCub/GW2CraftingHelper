> **Milestone record - 2026-09-05, branch `w24-ranker-button-consistency`.**
> Point-in-time evidence - it describes the code as it stood that day and may not describe current code. Current documentation is [`docs/`](../../docs/README.md).

## Ranker reorder keys are cut from the remove key beside them

A Crafting Ranker row draws three actions at its right edge: move up, move
down, remove. They were two different kinds of control. The remove X blits
Blish's own window close control 1:1. The two carets were `FeedbackButton`
plates, which are `StandardButton` underneath, carrying a caret glyph.

### The mismatch

Measured off the shipped art, the two disagree in every state:

- Resting, the close key sits on a (231,223,214) plate inside a thick olive
  border with a drop shadow. The button plate is (202,193,176) inside thin
  square black strips.
- On hover the close key lights up gold. The button plate lights up
  near-white.
- Disabled, the key fades to 60 percent of its colour. The button repaints
  itself flat grey.

The three actions therefore lit up and faded independently, in one row.

### The fix

Blish ships no up or down key in the close key's family, so the carets are
cut from the close key's own texture rather than imitated.

The cross occupies source rows 12 to 22 of a key that runs from row 6 to row
28. The six rows at each end are border, shadow and bare plate whatever mark
the key carries. `Views/Rendering/CaretKeyButton.cs` (new, 109 lines) blits
those two end slices, repeats one bare plate row to fill the middle, and
draws the caret over the result. Frame, plate and the gold hover are then
the same pixels the X uses.

The three source rectangles come from constants rather than literals.
`Services/GlyphButtonMetrics.cs` gains `KeyCapHeight` 6, `KeyPlateRowY`
(`CloseKeySourceY + KeyCapHeight - 1`, the last bare plate row above the
cross) and `KeyPlateSize` 16, the lit plate inside the border.

The caret stays a glyph from `ref/glyphs.fnt`, drawn as text rather than as
an `Icon`, so it takes the key's ink and the key's disabled fade. It is
centred in the control, which is correct because the standalone glyph face
centres a glyph's ink in its line box rather than seating it on a baseline.
The `UiGlyphs` ASCII stand-in still applies when that atlas failed to load.
The caret ink is `Color(8, 0, 0)`, sampled from the cross in the same
texture, so it carries the weight the X does on both the resting and the
gold plate.

The box is unchanged. All three tables still reserve
`GlyphButtonMetrics.RowActionWidth` by `RowActionHeight`, the 21 by 23 ink
rectangle of the shipped texture.

### One base for the shared art

`Views/Rendering/CloseKeyButton.cs` owned the `button-exit` and
`button-exit-active` texture pair, the hover swap, the disabled fade and the
row-action size. None of that is specific to an X, and a second key drawn
from the same textures would have had to repeat all four.

`Views/Rendering/RowActionKey.cs` (new, 103 lines) is the base that holds
them, plus a `DrawSlice` helper that intersects each source rectangle with
the texture's own bounds. That clamp matters because `ContentService`
answers a name it cannot find with an error texture of a different size, and
an unclamped source rectangle samples whatever the atlas page holds next.
The disabled fade is read from `PillColors.DimmedPillFactor` (0.6) so a
tree row's IGNORE toggle and the row wash cannot drift apart.

`CloseKeyButton` keeps only what is its own: the source rectangle of the
whole ink, and the `Tint` the tree's IGNORE toggle sets. It drops from 119
lines to 64. That commit changed no rendering.

### What was removed

`FeedbackButton.SetGlyph` had no other caller and is gone.
`GlyphButtonMetrics` loses `PlateInsetX` 6 and `PlateInsetY` 5, which
described a `FeedbackButton` plate the carets no longer sit on.
`RankerTabContent`'s `CreateRowButton` and `CreateGlyphRowButton` collapse
into one `CreateCaretButton`, and the row struct's `Up` and `Down` fields
change type from `FeedbackButton` to `CaretKeyButton`. The caret controls
take no explicit `Size`, for the same reason the remove action does not:
`RowActionKey` sets it.

Both new files are registered in `TaimisToolbench.csproj`, and
`docs/file-budgets.txt` records them alongside the four whose line counts
changed.

### Regression coverage

`tests/TaimisToolbench.Tests/Services/GlyphButtonMetricsTests.cs`:

- `TheCaretsBesideIt_FitTheKeyPlate` measures the largest caret's ink
  against `KeyPlateSize` plus `GlyphMargin` on each side, rather than
  against the old button plate insets. One pixel past it lands on the key's
  border art.
- `TheKeyWithoutItsCross_RebuildsFromSlicesOfItsOwnTexture` pins that two
  end caps are shorter than the box, so a fill row exists; that the repeated
  plate row sits above the bottom cap, so the fill carries no frame art; and
  that both slices stay inside the 32 pixel texture.
- `TheKeyPlate_FitsInsideTheBox` pins the plate smaller than the box on both
  axes, which fails if the border is ever measured away.

Gate: NOT RUN - the branch's commits record no live game or sandbox check.
A reviewer should open the Crafting Ranker in Cascade mode. Confirm that a
row's up, down and remove keys look like one set at rest, that hovering any
one of them lights up the same gold, and that a first or last row's disabled
caret fades to the same weight the remove key does rather than turning flat
grey.
