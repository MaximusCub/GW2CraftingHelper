using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The persistent sort indicator's arithmetic. The whole point of the
    /// design is that a header does not change size or position when a
    /// column is clicked, so most of what follows asserts that something is
    /// EQUAL across the three states rather than what it equals.
    /// <para>
    /// Glyphs are asserted as literal codepoints rather than against
    /// UiGlyphs' own constants, which would mirror the implementation and
    /// pass no matter what it said. U+E100 and U+E101 are the sort pair in
    /// the module's shipped glyph font; GlyphFontDescriptorTests is what
    /// proves ref/glyphs.fnt actually carries them.
    /// </para>
    /// </summary>
    public class SortIndicatorLayoutTests
    {
        private static readonly TableSortDirection[] AllDirections =
        {
            TableSortDirection.None,
            TableSortDirection.Ascending,
            TableSortDirection.Descending,
        };

        [Fact]
        public void RestState_ShowsTheAscendingGlyph()
        {
            // A first click sorts ascending, so the dim mark is a preview of
            // what the control does, not a neutral ornament.
            Assert.Equal("\uE100", SortIndicatorLayout.GlyphFor(TableSortDirection.None));
            Assert.Equal("\uE100", SortIndicatorLayout.GlyphFor(TableSortDirection.Ascending));
            Assert.Equal("\uE101", SortIndicatorLayout.GlyphFor(TableSortDirection.Descending));
        }

        [Fact]
        public void OnlyTheRestStateIsDim()
        {
            Assert.Equal(
                SortIndicatorLayout.RestOpacity,
                SortIndicatorLayout.OpacityFor(TableSortDirection.None));
            Assert.Equal(
                SortIndicatorLayout.ActiveOpacity,
                SortIndicatorLayout.OpacityFor(TableSortDirection.Ascending));
            Assert.Equal(
                SortIndicatorLayout.ActiveOpacity,
                SortIndicatorLayout.OpacityFor(TableSortDirection.Descending));
        }

        [Fact]
        public void RestIsDimmerThanActive_AndBothAreDrawable()
        {
            Assert.True(SortIndicatorLayout.RestOpacity > 0f);
            Assert.True(SortIndicatorLayout.RestOpacity < SortIndicatorLayout.ActiveOpacity);
            Assert.True(SortIndicatorLayout.ActiveOpacity <= 1f);
        }

        [Fact]
        public void SlotTakesTheWiderGlyph_SoNeitherDirectionResizesTheHeader()
        {
            // The shipped pair is one advance; the ASCII fallback a corrupt
            // install degrades to ("^" against "v") is not.
            Assert.Equal(11, SortIndicatorLayout.SlotWidth(11, 9));
            Assert.Equal(11, SortIndicatorLayout.SlotWidth(9, 11));
            Assert.Equal(9, SortIndicatorLayout.SlotWidth(9, 9));
        }

        [Fact]
        public void SlotWidth_NeverNegative()
        {
            Assert.Equal(0, SortIndicatorLayout.SlotWidth(-4, -9));
        }

        [Fact]
        public void HeaderIsTheSameWidthInEveryState_EvenWhenTheGlyphsAreNot()
        {
            // The mismatched case on purpose: the ASCII fallback, where the
            // ascending mark measures wider than the descending one. Driving
            // the block off the SLOT rather than off the drawn glyph is what
            // stops a click resizing the header under the cursor.
            const int Title = 79;
            const int AscendingInk = 11;
            const int DescendingInk = 9;

            int slot = SortIndicatorLayout.SlotWidth(AscendingInk, DescendingInk);
            int expected = SortIndicatorLayout.BlockWidth(Title, slot);

            foreach (var direction in AllDirections)
            {
                int ink = direction == TableSortDirection.Descending
                    ? DescendingInk : AscendingInk;

                Assert.Equal(expected, SortIndicatorLayout.BlockWidth(Title, slot));

                // And the drawn mark stays inside the slot the width paid for.
                int slotX = SortIndicatorLayout.SlotX(0, Title);
                int glyphX = SortIndicatorLayout.GlyphX(slotX, slot, ink);
                Assert.True(glyphX >= slotX);
                Assert.True(glyphX + ink <= expected);
            }
        }

        [Fact]
        public void BlockWidth_WithoutASlot_IsJustTheWord()
        {
            // An inert column carries no indicator, so it is not charged a
            // gap for one either.
            Assert.Equal(79, SortIndicatorLayout.BlockWidth(79, 0));
            Assert.Equal(0, SortIndicatorLayout.BlockWidth(0, 0));
        }

        [Fact]
        public void BlockWidth_ClampsNegativeInputs()
        {
            Assert.Equal(0, SortIndicatorLayout.BlockWidth(-5, 0));
            Assert.Equal(SortIndicatorLayout.Gap + 9, SortIndicatorLayout.BlockWidth(-5, 9));
        }

        [Fact]
        public void SlotFollowsTheWord_AndTheBlockEndsWhereItsWidthSays()
        {
            const int BlockX = 120;
            const int Title = 40;
            const int Slot = 9;

            int slotX = SortIndicatorLayout.SlotX(BlockX, Title);

            Assert.Equal(BlockX + Title + SortIndicatorLayout.Gap, slotX);
            Assert.Equal(
                BlockX + SortIndicatorLayout.BlockWidth(Title, Slot), slotX + Slot);
        }

        [Fact]
        public void GlyphCentresInItsSlot_SoTheNarrowerDirectionDoesNotShiftToOneEdge()
        {
            const int SlotX = 200;
            const int Slot = 11;

            Assert.Equal(SlotX + 1, SortIndicatorLayout.GlyphX(SlotX, Slot, 9));
            Assert.Equal(SlotX, SortIndicatorLayout.GlyphX(SlotX, Slot, Slot));
        }

        [Fact]
        public void GlyphWiderThanItsSlot_PinsLeftRatherThanOverhangingBothWays()
        {
            Assert.Equal(200, SortIndicatorLayout.GlyphX(200, 9, 20));
        }
    }
}
