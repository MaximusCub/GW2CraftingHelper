using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
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

        [Fact]
        public void WideningAColumnShowingItsWholeString_SkipsTheRemeasure()
        {
            Assert.True(LogRowLayout.KeepsFitting(showingWholeString: true, fittedWidth: 200, newWidth: 400));
            Assert.True(LogRowLayout.KeepsFitting(showingWholeString: true, fittedWidth: 200, newWidth: 200));
        }

        [Fact]
        public void NarrowingAColumn_ForcesTheRemeasure()
        {
            // The drag case the split has to get right: a narrowing column
            // may have started to overflow, so the string must be re-fitted.
            Assert.False(LogRowLayout.KeepsFitting(showingWholeString: true, fittedWidth: 200, newWidth: 199));
        }

        [Fact]
        public void AlreadyShortenedColumn_AlwaysRemeasures()
        {
            // A "..." string carries no evidence about how much of the whole
            // string a wider column could now hold.
            Assert.False(LogRowLayout.KeepsFitting(showingWholeString: false, fittedWidth: 200, newWidth: 400));
        }

        [Fact]
        public void NeverFittedColumn_AlwaysRemeasures()
        {
            // A row's columns start at fittedWidth -1, before any measuring.
            Assert.False(LogRowLayout.KeepsFitting(showingWholeString: true, fittedWidth: -1, newWidth: 400));
        }

        [Fact]
        public void ColumnWidthTrackedApartFromTheControl_DragBackDownStillSkips()
        {
            // A resize drag moves the columns live and re-fits the text only
            // at settle, so the control is already at the new width while
            // its string still belongs to the width it was fitted at. Both
            // halves of a 200 -> 900 -> 200 drag must skip: the string is
            // known to fit at 200, whatever the control is currently sized
            // to in between.
            const int fitted = 200;
            Assert.True(LogRowLayout.KeepsFitting(true, fitted, newWidth: 900));
            Assert.True(LogRowLayout.KeepsFitting(true, fitted, newWidth: fitted));
        }

        [Fact]
        public void SingleLineRow_KeepsTheHeightItAlwaysHad()
        {
            // The pitch every unwrapped row in the tab renders at is
            // untouched by the wrap: one line is still the measured text
            // height plus its descender clearance, and nothing else.
            Assert.Equal(22, LogRowLayout.RowHeight(1, singleLineHeight: 22, lineAdvance: 20));
        }

        [Fact]
        public void WrappedRow_GrowsByOneAdvancePerExtraLine()
        {
            // The descender clearance the single-line height carries is
            // counted once, at the bottom of the row - not once per line, or
            // a four-line row would gain 6px of dead space its own text
            // never occupies.
            Assert.Equal(42, LogRowLayout.RowHeight(2, 22, 20));
            Assert.Equal(62, LogRowLayout.RowHeight(3, 22, 20));
            Assert.Equal(82, LogRowLayout.RowHeight(4, 22, 20));
        }

        [Fact]
        public void RowHeight_IsCappedAtTheLineCapWhateverTheCallerCounted()
        {
            // Second line of defence: the wrap is already capped, so a
            // caller reaching this with more lines has a bug - but the row
            // must not become the only thing on screen because of it.
            int capped = LogRowLayout.RowHeight(LogRowLayout.MaxMessageLines, 22, 20);
            Assert.Equal(capped, LogRowLayout.RowHeight(40, 22, 20));
            Assert.Equal(capped, LogRowLayout.RowHeight(int.MaxValue, 22, 20));
        }

        [Fact]
        public void RowHeight_TreatsADegenerateCountOrAdvanceAsOneLine()
        {
            // Zero lines is not a zero-height row: a row with no message at
            // all still has a timestamp to draw, and a zero-height child
            // would collapse the flow panel's spacing around it.
            Assert.Equal(22, LogRowLayout.RowHeight(0, 22, 20));
            Assert.Equal(22, LogRowLayout.RowHeight(-3, 22, 20));
            Assert.Equal(22, LogRowLayout.RowHeight(3, 22, lineAdvance: 0));
            Assert.Equal(0, LogRowLayout.RowHeight(3, singleLineHeight: 0, lineAdvance: 0));
        }
    }
}
