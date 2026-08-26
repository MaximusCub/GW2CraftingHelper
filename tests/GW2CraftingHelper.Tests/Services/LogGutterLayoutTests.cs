using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class LogGutterLayoutTests
    {
        // Stand-ins for the widths the view measures. The tests assert
        // relationships and floors, never a measured pixel.
        private const int TimeBand = 227;
        private const int TagBand = 134;
        private const int FloorRowWidth = 1212;

        [Fact]
        public void TimeBand_IsTheMaxOverTheLevelWidths()
        {
            var perLevel = new List<int> { 210, 227, 219, 224 };

            Assert.Equal(227, LogGutterLayout.TimeBand(perLevel));
        }

        [Fact]
        public void TimeBand_IsIndependentOfWhatTheRowsContain()
        {
            // Closed set: every level name is one of four, so the worst case
            // IS the band and no rendered row can widen it.
            var perLevel = new List<int> { 210, 227, 219, 224 };

            Assert.Equal(
                LogGutterLayout.TimeBand(perLevel),
                LogGutterLayout.Compute(FloorRowWidth, LogGutterLayout.TimeBand(perLevel), 0).TimeWidth);
            Assert.Equal(
                LogGutterLayout.TimeBand(perLevel),
                LogGutterLayout.Compute(FloorRowWidth, LogGutterLayout.TimeBand(perLevel), 500).TimeWidth);
        }

        [Fact]
        public void TimeBand_EmptyOrNullLevelList_IsZero()
        {
            Assert.Equal(0, LogGutterLayout.TimeBand(new List<int>()));
            Assert.Equal(0, LogGutterLayout.TimeBand(null));
        }

        [Fact]
        public void TagBand_IsTheWidestRenderedTagFlooredAtItsOwnHeader()
        {
            Assert.Equal(134, LogGutterLayout.TagBand(134, 40));

            // A short-tag view still leaves room for the header that names
            // the column - the header-floored band rule.
            Assert.Equal(40, LogGutterLayout.TagBand(18, 40));
            Assert.Equal(40, LogGutterLayout.TagBand(0, 40));
        }

        [Fact]
        public void TagBand_NeverGoesNegative()
        {
            Assert.Equal(0, LogGutterLayout.TagBand(-5, -5));
        }

        [Fact]
        public void MessageStartsAfterBothBandsAndBothGaps()
        {
            var bands = LogGutterLayout.Compute(FloorRowWidth, TimeBand, TagBand);

            Assert.Equal(LogGutterLayout.GutterX, bands.TimeX);
            Assert.Equal(
                LogGutterLayout.GutterX + TimeBand + LogGutterLayout.TimeToTagGap, bands.TagX);
            Assert.Equal(
                bands.TagX + TagBand + LogGutterLayout.TagToMessageGap, bands.MessageX);
        }

        [Fact]
        public void FullGutterWidth_IsTheTwoBandsAndTheGapBeforeTheMessage()
        {
            // Resolved pixels rather than the method's own expression
            // restated: 16 + 227 + 8 + 134.
            Assert.Equal(385, LogGutterLayout.FullGutterWidth(TimeBand, TagBand));

            // A wider band moves the message column by exactly that much,
            // and a negative band is clamped rather than shrinking it.
            Assert.Equal(
                LogGutterLayout.FullGutterWidth(TimeBand, TagBand) + 40,
                LogGutterLayout.FullGutterWidth(TimeBand, TagBand + 40));
            Assert.Equal(
                LogGutterLayout.GutterX + LogGutterLayout.TimeToTagGap,
                LogGutterLayout.FullGutterWidth(-5, -5));
        }

        [Fact]
        public void ANarrowerTagBandHandsTheDifferenceStraightToTheMessage()
        {
            // The whole point of retiring the worst-case template: a view
            // whose tags are short gets those pixels back in the message
            // column rather than leaving them blank.
            var wide = LogGutterLayout.Compute(FloorRowWidth, TimeBand, TagBand);
            var narrow = LogGutterLayout.Compute(FloorRowWidth, TimeBand, 50);

            Assert.Equal(TagBand - 50, wide.MessageX - narrow.MessageX);
            Assert.Equal(TagBand - 50, narrow.MessageWidth - wide.MessageWidth);
        }

        [Fact]
        public void MessageWidth_FloorsAtLogRowLayoutsOwnMinimum()
        {
            foreach (int rowWidth in new[] { 200, 100, 40, 0, -50 })
            {
                Assert.True(
                    LogGutterLayout.Compute(rowWidth, TimeBand, TagBand).MessageWidth
                        >= LogRowLayout.MinMessageWidth);
            }
        }

        [Fact]
        public void HalfRowCapAppliesToTheSumOfBothBands()
        {
            const int RowWidth = 500;
            var bands = LogGutterLayout.Compute(RowWidth, TimeBand, TagBand);

            int gutter = bands.TagX + bands.TagWidth;

            Assert.True(LogGutterLayout.FullGutterWidth(TimeBand, TagBand) > RowWidth / 2);
            Assert.Equal(RowWidth / 2, gutter);
        }

        [Fact]
        public void UnderTheCapTheTagGivesUpItsWidthBeforeTheTimestampDoes()
        {
            // The timestamp is the column a reader navigates a log by; a tag
            // is already repeated on every row of its own kind.
            var bands = LogGutterLayout.Compute(560, TimeBand, TagBand);

            Assert.Equal(TimeBand, bands.TimeWidth);
            Assert.True(bands.TagWidth < TagBand);
            Assert.True(bands.TagWidth >= 0);
        }

        [Fact]
        public void PastTheTagsWholeWidthTheTimeBandGivesUpTheRest()
        {
            var bands = LogGutterLayout.Compute(200, TimeBand, TagBand);

            Assert.Equal(0, bands.TagWidth);
            Assert.True(bands.TimeWidth < TimeBand);
            Assert.True(bands.TimeWidth >= 0);
        }

        [Fact]
        public void NoBandEverGoesNegativeAtAnyWidth()
        {
            for (int rowWidth = -100; rowWidth <= 1400; rowWidth += 37)
            {
                var bands = LogGutterLayout.Compute(rowWidth, TimeBand, TagBand);

                Assert.True(bands.TimeWidth >= 0);
                Assert.True(bands.TagWidth >= 0);
                Assert.True(bands.MessageWidth >= LogRowLayout.MinMessageWidth);
                Assert.True(bands.TagX >= bands.TimeX + bands.TimeWidth);
                Assert.True(bands.MessageX >= bands.TagX + bands.TagWidth);
            }
        }

        [Fact]
        public void NegativeBandsAreTreatedAsAbsent()
        {
            var bands = LogGutterLayout.Compute(FloorRowWidth, -10, -10);

            Assert.Equal(0, bands.TimeWidth);
            Assert.Equal(0, bands.TagWidth);
            Assert.Equal(LogGutterLayout.GutterX, bands.TimeX);
        }

        [Fact]
        public void EveryColumnStartsAtTheSameXWhateverTheRowHolds()
        {
            // The band inputs are per-generation, not per-row, so two rows
            // in one pass resolve to identical geometry - which IS what
            // "scannable gutter" means.
            var first = LogGutterLayout.Compute(FloorRowWidth, TimeBand, TagBand);
            var second = LogGutterLayout.Compute(FloorRowWidth, TimeBand, TagBand);

            Assert.Equal(first.TagX, second.TagX);
            Assert.Equal(first.MessageX, second.MessageX);
            Assert.Equal(first.MessageWidth, second.MessageWidth);
        }
    }
}
