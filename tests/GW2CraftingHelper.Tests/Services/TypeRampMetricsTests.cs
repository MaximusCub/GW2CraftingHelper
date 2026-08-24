using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The ramp's own invariants. These do not re-measure the font (the
    /// XNB parse is recorded in TypeRampMetrics' doc comment and in
    /// plan-redesign/typography.md); they pin the RELATIONSHIPS that make
    /// the ramp a hierarchy rather than four sizes in a row, so a future
    /// tier swap that breaks one fails here instead of on a screenshot.
    /// </summary>
    public class TypeRampMetricsTests
    {
        [Fact]
        public void PromotedTiers_AreARealStepOverBody_NotTheFlatRampTheyReplaced()
        {
            // The rule the old 14/16/18-regular ramp failed: a hierarchy
            // step is at least 1.25x, and it is carried by cap height, not
            // just by nominal point size.
            const int bodyPointSize = 16;

            Assert.True(
                TypeRampMetrics.ColumnHeaderPointSize >= bodyPointSize * 1.25,
                $"column header {TypeRampMetrics.ColumnHeaderPointSize}pt is under a 1.25 step over body");
            Assert.True(
                TypeRampMetrics.SectionTitlePointSize > TypeRampMetrics.ColumnHeaderPointSize,
                "section titles must outrank column headers");
            Assert.True(
                TypeRampMetrics.SectionTitleInk.CapHeight > TypeRampMetrics.ColumnHeaderInk.CapHeight,
                "the title/header step has to survive in ink, not only in nominal size");
            Assert.True(
                TypeRampMetrics.ColumnHeaderInk.CapHeight > TypeRampMetrics.BodyInk.CapHeight,
                "the body/header step has to survive in ink, not only in nominal size");
        }

        [Fact]
        public void CaptionIsTheFloor_AndBodyIsTheOnlyReadingSizeAboveIt()
        {
            Assert.True(TypeRampMetrics.CaptionInk.CapHeight < TypeRampMetrics.BodyInk.CapHeight);
            Assert.True(TypeRampMetrics.BodyInk.CapHeight < TypeRampMetrics.ColumnHeaderInk.CapHeight);
        }

        [Fact]
        public void StatusSitsBetweenBodyAndTheSectionTitles()
        {
            // A transient line must read above the rows and below the
            // headings that structure the page.
            Assert.True(TypeRampMetrics.StatusInk.CapHeight > TypeRampMetrics.BodyInk.CapHeight);
            Assert.True(TypeRampMetrics.StatusInk.CapHeight < TypeRampMetrics.SectionTitleInk.CapHeight);
        }

        [Theory]
        [InlineData(14)]
        [InlineData(16)]
        [InlineData(18)]
        [InlineData(20)]
        [InlineData(22)]
        [InlineData(24)]
        [InlineData(32)]
        public void EveryMeasuredEntry_IsInternallyConsistent(int pointSize)
        {
            var ink = InkFor(pointSize);

            // Cap ink sits inside the line box, above the baseline; the
            // lowest ink is a descender, at or below the baseline. A typo
            // in the measured table almost always breaks one of these.
            Assert.True(ink.CapTopY >= 0);
            Assert.True(ink.CapTopY + ink.CapHeight <= ink.BaselineY);
            Assert.True(ink.LowestInk >= ink.BaselineY);
            Assert.True(ink.LineHeight > ink.CapHeight);
        }

        [Fact]
        public void InkBottom_IsWhereADividerHasToClear()
        {
            var header = TypeRampMetrics.ColumnHeaderInk;

            Assert.Equal(header.LowestInk + 4, TypeRampMetrics.InkBottom(header, 4));
        }

        [Fact]
        public void BaselineAlignedY_PutsTwoTiersOnOneReadingLine()
        {
            // The section header's caret (Body) against its title.
            var title = TypeRampMetrics.SectionTitleInk;
            var caret = TypeRampMetrics.BodyInk;

            int titleY = 3;
            int baseline = titleY + title.BaselineY;
            int caretY = TypeRampMetrics.BaselineAlignedY(caret, baseline);

            Assert.Equal(baseline, caretY + caret.BaselineY);
        }

        private static TypeRampMetrics.FontInk InkFor(int pointSize)
        {
            switch (pointSize)
            {
                case 14: return TypeRampMetrics.Regular14;
                case 16: return TypeRampMetrics.Regular16;
                case 18: return TypeRampMetrics.Bold18;
                case 20: return TypeRampMetrics.Bold20;
                case 22: return TypeRampMetrics.Bold22;
                case 24: return TypeRampMetrics.Bold24;
                default: return TypeRampMetrics.Regular32;
            }
        }
    }
}
