using System.IO;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The box a row action draws in. Reported in game: the module's
    /// X did not match the game window's close control; the answer
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
        public void TheCaretsBesideIt_FitTheKeyPlate()
        {
            // The carets draw on the close key's own plate now, so the box
            // they have to fit is that plate and not a button's. One pixel
            // of ink past it lands on the key's border art.
            LargestCaretInk(out int inkWidth, out int inkHeight);

            Assert.True(
                inkWidth + (2 * GlyphButtonMetrics.GlyphMargin) <= GlyphButtonMetrics.KeyPlateSize);
            Assert.True(
                inkHeight + (2 * GlyphButtonMetrics.GlyphMargin) <= GlyphButtonMetrics.KeyPlateSize);
        }

        [Fact]
        public void TheKeyWithoutItsCross_RebuildsFromSlicesOfItsOwnTexture()
        {
            // A caret key keeps the frame at each end of the close key and
            // repeats one bare plate row between them. Two frames that met
            // in the middle would leave no room for the fill, and the fill
            // is the only part of the key a caret can sit on.
            Assert.True(2 * GlyphButtonMetrics.KeyCapHeight < GlyphButtonMetrics.RowActionHeight);

            // The repeated row and the bottom frame must not be the same
            // rows of the texture, or the fill would carry frame art.
            int bottomSourceY = GlyphButtonMetrics.CloseKeySourceY
                + GlyphButtonMetrics.RowActionHeight - GlyphButtonMetrics.KeyCapHeight;
            Assert.True(bottomSourceY > GlyphButtonMetrics.KeyPlateRowY);

            // Every slice is sampled from inside the texture; one that
            // reached past it would pick up the next page of the atlas.
            Assert.InRange(
                GlyphButtonMetrics.KeyPlateRowY, 0, GlyphButtonMetrics.CloseKeyTextureSize - 1);
            Assert.True(
                bottomSourceY + GlyphButtonMetrics.KeyCapHeight
                    <= GlyphButtonMetrics.CloseKeyTextureSize);
        }

        [Fact]
        public void TheKeyPlate_FitsInsideTheBox()
        {
            // The plate is the lit area inside the border, so it has to be
            // smaller than the box on both axes; a plate as wide as the box
            // would mean the border had been measured away.
            Assert.True(GlyphButtonMetrics.KeyPlateSize < GlyphButtonMetrics.RowActionWidth);
            Assert.True(GlyphButtonMetrics.KeyPlateSize < GlyphButtonMetrics.RowActionHeight);
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
