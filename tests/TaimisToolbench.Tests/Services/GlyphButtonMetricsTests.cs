using System.IO;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The row-action button's square, against the glyph atlas it has to
    /// hold. The owner's report was that these draw too large beside the
    /// game's own window close control; the floor below is what stops the
    /// correction going one step too far, where the mark would draw over the
    /// button's own border art.
    /// </summary>
    public class GlyphButtonMetricsTests
    {
        private static readonly int[] RowActionGlyphs =
        {
            0xE102, // CaretUp - reorder
            0xE103, // CaretDown - reorder
            0xE105, // RemoveMark - the X the report is about
        };

        private static GlyphFontDescriptor Shipped()
        {
            using (var stream = File.OpenRead(Path.Combine("ref", "glyphs.fnt")))
            {
                return GlyphFontDescriptor.Parse(stream);
            }
        }

        private static int LargestInk()
        {
            var font = Shipped();
            int largest = 0;
            foreach (int codepoint in RowActionGlyphs)
            {
                Assert.True(font.TryGet(codepoint, out var glyph));
                largest = System.Math.Max(largest, System.Math.Max(glyph.Width, glyph.Height));
            }

            return largest;
        }

        [Fact]
        public void TheRowActionButton_HoldsItsWidestGlyphInsideThePlate()
        {
            int ink = LargestInk();
            int plateWidth = GlyphButtonMetrics.RowActionSize - GlyphButtonMetrics.PlateInsetX;
            int plateHeight = GlyphButtonMetrics.RowActionSize - GlyphButtonMetrics.PlateInsetY;

            Assert.True(plateWidth >= ink + (2 * GlyphButtonMetrics.GlyphMargin));
            Assert.True(plateHeight >= ink + (2 * GlyphButtonMetrics.GlyphMargin));
        }

        [Fact]
        public void TheRowActionButton_IsTheSmallestSquareThatDoes()
        {
            // The correction is a shrink, so the assertion that matters is
            // the floor: one pixel smaller and the widest mark would be
            // drawn over the border art rather than on the plate.
            int ink = LargestInk();
            int smaller = GlyphButtonMetrics.RowActionSize - 1;

            Assert.False(
                smaller - GlyphButtonMetrics.PlateInsetX >= ink + (2 * GlyphButtonMetrics.GlyphMargin));
        }

        [Fact]
        public void TheRankerAndPlanHistory_ReserveTheSameSquare()
        {
            // It is ONE control in two tabs, and the two tabs reserve room
            // for it from separate constants. V4 named the Ranker's; leaving
            // Plan History's behind would put two sizes of the same X in the
            // same module.
            Assert.Equal(GlyphButtonMetrics.RowActionSize, RankerRowLayout.ButtonWidth);
            Assert.Equal(GlyphButtonMetrics.RowActionSize, PlanHistoryRowLayout.IconButtonWidth);
        }
    }
}
