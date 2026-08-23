using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class LogRowLayoutTests
    {
        // Representative measured prefix width for "[ERROR] yyyy-MM-dd
        // HH:mm:ss [tag]" in DefaultFont14 - the view measures its own; the
        // arithmetic below is what this class owns.
        private const int PrefixWidth = 260;

        [Fact]
        public void NormalWidth_PrefixColumnUncapped_MessageTakesTheRest()
        {
            int prefix = LogRowLayout.PrefixWidth(PrefixWidth, rowWidth: 900);
            Assert.Equal(PrefixWidth, prefix);
            Assert.Equal(PrefixWidth + LogRowLayout.MessageGap, LogRowLayout.MessageX(prefix));
            Assert.Equal(
                900 - PrefixWidth - LogRowLayout.MessageGap - LogRowLayout.RightPad,
                LogRowLayout.MessageMaxWidth(900, prefix));
        }

        [Fact]
        public void WideningTheRow_GrowsOnlyTheMessageColumn()
        {
            int narrow = LogRowLayout.MessageMaxWidth(900, LogRowLayout.PrefixWidth(PrefixWidth, 900));
            int wide = LogRowLayout.MessageMaxWidth(1400, LogRowLayout.PrefixWidth(PrefixWidth, 1400));
            Assert.Equal(500, wide - narrow);
            Assert.Equal(
                LogRowLayout.PrefixWidth(PrefixWidth, 900),
                LogRowLayout.PrefixWidth(PrefixWidth, 1400));
        }

        [Fact]
        public void NarrowRow_PrefixCappedAtHalf_SoTheMessageIsNeverPushedOffRow()
        {
            // 400px row: an uncapped 260px prefix plus the gap and padding
            // would leave only 124px of message. The cap yields 200/200.
            Assert.Equal(200, LogRowLayout.PrefixWidth(PrefixWidth, rowWidth: 400));
            Assert.Equal(208, LogRowLayout.MessageX(200));
            Assert.Equal(400 - 208 - LogRowLayout.RightPad, LogRowLayout.MessageMaxWidth(400, 200));
        }

        [Fact]
        public void DegenerateWidth_MessageColumnNeverCollapsesToNothing()
        {
            // EllipsizeToWidth returns "" for maxWidth <= 0, which would
            // blank every row instead of showing it as truncated.
            foreach (int rowWidth in new[] { 0, 1, 40, 120 })
            {
                int prefix = LogRowLayout.PrefixWidth(PrefixWidth, rowWidth);
                Assert.True(LogRowLayout.MessageMaxWidth(rowWidth, prefix) >= LogRowLayout.MinMessageWidth);
            }
        }

        [Fact]
        public void ZeroRowWidth_PrefixFallsBackToFullWidthRatherThanZero()
        {
            // rowWidth 0 happens transiently during Blish layout; a 0-wide
            // prefix column would ellipsize the whole prefix away, and the
            // very next resize tick re-fits it anyway.
            Assert.Equal(PrefixWidth, LogRowLayout.PrefixWidth(PrefixWidth, rowWidth: 0));
        }

        [Fact]
        public void NegativePrefixWidth_ClampsToZero()
        {
            Assert.Equal(0, LogRowLayout.PrefixWidth(-5, rowWidth: 900));
            Assert.Equal(LogRowLayout.MessageGap, LogRowLayout.MessageX(-5));
        }
    }
}
