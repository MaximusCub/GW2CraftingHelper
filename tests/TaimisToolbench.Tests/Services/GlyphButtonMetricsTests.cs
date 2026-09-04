using System.IO;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The box a row action draws in. The owner's report was that the
    /// module's X did not match the game window's close control; the answer
    /// was to stop drawing an X at all and blit Blish's own key, so the box
    /// is now the texture's measurement and no longer a glyph's. What is
    /// pinned here is that the blit stays 1:1, that the two carets still
    /// beside it still fit, and that every table reserves the one box.
    /// <para>
    /// The blit itself is Blish-bound and cannot be exercised here.
    /// </para>
    /// </summary>
    public class GlyphButtonMetricsTests
    {
        /// <summary>The Ranker's reorder pair, the only glyphs left in a plate.</summary>
        private static readonly int[] CaretGlyphs = { 0xE102, 0xE103 };

        private static GlyphFontDescriptor Shipped()
        {
            using (var stream = File.OpenRead(Path.Combine("ref", "glyphs.fnt")))
            {
                return GlyphFontDescriptor.Parse(stream);
            }
        }

        private static void LargestCaretInk(out int width, out int height)
        {
            var font = Shipped();
            width = 0;
            height = 0;
            foreach (int codepoint in CaretGlyphs)
            {
                Assert.True(font.TryGet(codepoint, out var glyph));
                width = System.Math.Max(width, glyph.Width);
                height = System.Math.Max(height, glyph.Height);
            }
        }

        [Fact]
        public void TheCloseKey_IsSampledFromInsideItsOwnTexture()
        {
            // A source rectangle reaching past the texture samples whatever
            // the atlas page holds next, and one that is not the box's own
            // size would scale the key - which is exactly the mismatch with
            // the window's close control that the report was about.
            Assert.True(
                GlyphButtonMetrics.CloseKeySourceX + GlyphButtonMetrics.RowActionWidth
                    <= GlyphButtonMetrics.CloseKeyTextureSize);
            Assert.True(
                GlyphButtonMetrics.CloseKeySourceY + GlyphButtonMetrics.RowActionHeight
                    <= GlyphButtonMetrics.CloseKeyTextureSize);
        }

        [Fact]
        public void TheCaretsBesideIt_StillFitTheirPlate()
        {
            // The carets are still a glyph on a FeedbackButton plate, and
            // they now take the close key's box rather than setting it. One
            // pixel of ink past the plate is drawn over the button's own
            // border art, so this is the floor the shrink had to clear.
            LargestCaretInk(out int inkWidth, out int inkHeight);

            int plateWidth = GlyphButtonMetrics.RowActionWidth - GlyphButtonMetrics.PlateInsetX;
            int plateHeight = GlyphButtonMetrics.RowActionHeight - GlyphButtonMetrics.PlateInsetY;

            Assert.True(plateWidth >= inkWidth + (2 * GlyphButtonMetrics.GlyphMargin));
            Assert.True(plateHeight >= inkHeight + (2 * GlyphButtonMetrics.GlyphMargin));
        }

        [Fact]
        public void TheRankerAndPlanHistory_ReserveTheSameBox()
        {
            // It is ONE control in two tabs, and the two tabs reserve room
            // for it from separate constants. Leaving one behind would put
            // two sizes of the same X in the same module.
            Assert.Equal(GlyphButtonMetrics.RowActionWidth, RankerRowLayout.ButtonWidth);
            Assert.Equal(GlyphButtonMetrics.RowActionHeight, RankerRowLayout.ButtonHeight);
            Assert.Equal(GlyphButtonMetrics.RowActionWidth, PlanHistoryRowLayout.IconButtonWidth);
            Assert.Equal(GlyphButtonMetrics.RowActionHeight, PlanHistoryRowLayout.IconButtonHeight);
        }

        [Fact]
        public void BothTables_CentreTheBoxOnItsOwnHeight()
        {
            // The two axes differ, so a seat that took the WIDTH would
            // silently sit one pixel high in every row of both tables. Both
            // seats are checked against the box's height and against the
            // row they have to stay inside.
            // The row is 60 and the box is odd, so the halves cannot be
            // equal; one spare pixel below is the whole tolerance.
            int rankerSeat = RankerRowLayout.MainLineY(RankerRowLayout.ButtonHeight);
            Assert.InRange(
                RankerRowLayout.RowHeight - RankerRowLayout.ButtonHeight - (2 * rankerSeat),
                0,
                1);

            int historySeat =
                (PlanHistoryRowLayout.RowHeight - PlanHistoryRowLayout.IconButtonHeight) / 2;
            Assert.InRange(
                PlanHistoryRowLayout.RowHeight - PlanHistoryRowLayout.IconButtonHeight
                    - (2 * historySeat),
                0,
                1);
        }
    }
}
