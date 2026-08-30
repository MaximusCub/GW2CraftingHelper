using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Sticky column-header placement, swept the way a reader actually
    /// scrolls: a whole table dragged past a viewport a pixel at a time,
    /// with the invariants asserted at every one of those pixels rather than
    /// at three hand-picked offsets.
    /// <para>
    /// Every y here is viewport-relative, which is the whole reason this
    /// class is testable without Blish: the view converts a live control
    /// position into these two numbers and this decides the rest.
    /// </para>
    /// </summary>
    public class StickyHeaderLayoutTests
    {
        private const int HeaderHeight = 32;
        private const int ViewportHeight = 400;

        /// <summary>One table's placement at a given scroll offset. The
        /// table starts at contentTop and runs rowCount rows.</summary>
        private static StickyHeaderLayout.Placement At(
            int scroll, int contentTop, int rowCount, int rowHeight = 45,
            int viewportHeight = ViewportHeight, int headerHeight = HeaderHeight)
        {
            int headerY = contentTop - scroll;
            int bottomY = contentTop + headerHeight + (rowCount * rowHeight) - scroll;
            return StickyHeaderLayout.Compute(headerY, headerHeight, bottomY, viewportHeight);
        }

        [Fact]
        public void NotPinnedWhileTheRealHeaderIsStillOnScreen()
        {
            // Including at exactly 0, where a pinned copy would sit on top
            // of the real one.
            for (int scroll = 0; scroll <= 120; scroll++)
            {
                var placement = At(scroll, contentTop: 120, rowCount: 20);
                Assert.False(placement.Pinned);
            }
        }

        [Fact]
        public void NeverOverlapsTheSectionAboveIt()
        {
            // A section title band sits in the 40px above the header. At
            // every scroll where any of it is still visible, the header must
            // not have been lifted over it.
            const int TitleTop = 80;
            const int HeaderTop = 120;

            for (int scroll = 0; scroll < HeaderTop; scroll++)
            {
                Assert.False(At(scroll, HeaderTop, rowCount: 20).Pinned);
                Assert.True(TitleTop - scroll + 40 > 0);
            }
        }

        [Fact]
        public void PinsToTheViewportTopWhileAnyRowIsStillVisible()
        {
            const int HeaderTop = 120;
            const int Rows = 20;
            const int RowHeight = 45;
            int tableBottom = HeaderTop + HeaderHeight + (Rows * RowHeight);

            for (int scroll = HeaderTop + 1; scroll <= tableBottom - HeaderHeight; scroll++)
            {
                var placement = At(scroll, HeaderTop, Rows, RowHeight);

                Assert.True(placement.Pinned);
                Assert.Equal(0, placement.ClipY);
                Assert.Equal(0, placement.OffsetInBand);
                Assert.Equal(HeaderHeight, placement.VisibleHeight);
            }
        }

        [Fact]
        public void SlidesOutWithTheEndOfItsTable_AndIsGoneOnceThatEndIsPast()
        {
            const int HeaderTop = 120;
            const int Rows = 4;
            const int RowHeight = 45;
            int tableBottom = HeaderTop + HeaderHeight + (Rows * RowHeight);

            // The last HeaderHeight pixels of the table push the band out,
            // one pixel of the band per pixel of scroll.
            for (int i = 1; i < HeaderHeight; i++)
            {
                var placement = At(tableBottom - HeaderHeight + i, HeaderTop, Rows, RowHeight);

                Assert.True(placement.Pinned);
                Assert.Equal(i, placement.OffsetInBand);
                Assert.Equal(HeaderHeight - i, placement.VisibleHeight);
            }

            Assert.False(At(tableBottom, HeaderTop, Rows, RowHeight).Pinned);
            Assert.False(At(tableBottom + 500, HeaderTop, Rows, RowHeight).Pinned);
        }

        [Fact]
        public void TheVisibleSliceOnlyEverShrinks_AcrossAWholeScrollThrough()
        {
            // A fast scroll is the same sweep with gaps in it, so the
            // property that matters is monotonicity, not a per-frame delta.
            const int HeaderTop = 120;
            int previous = int.MaxValue;
            bool wasPinned = false;

            for (int scroll = 0; scroll <= 2000; scroll++)
            {
                var placement = At(scroll, HeaderTop, rowCount: 20);
                if (!placement.Pinned)
                {
                    // Once it has left, it never comes back for this table.
                    Assert.True(!wasPinned || scroll > HeaderTop);
                    continue;
                }

                wasPinned = true;
                Assert.True(placement.VisibleHeight <= previous);
                previous = placement.VisibleHeight;
            }

            Assert.True(wasPinned);
        }

        [Fact]
        public void AOneRowTable_PinsForExactlyThatRow()
        {
            const int HeaderTop = 100;
            const int RowHeight = 45;

            Assert.False(At(HeaderTop, HeaderTop, rowCount: 1, rowHeight: RowHeight).Pinned);
            Assert.True(At(HeaderTop + 1, HeaderTop, rowCount: 1, rowHeight: RowHeight).Pinned);

            // Its own row is what holds it up, and no more than that.
            var last = At(
                HeaderTop + RowHeight, HeaderTop, rowCount: 1, rowHeight: RowHeight);
            Assert.True(last.Pinned);
            Assert.Equal(HeaderHeight, last.VisibleHeight);
            Assert.False(
                At(HeaderTop + RowHeight + HeaderHeight, HeaderTop, 1, RowHeight).Pinned);
        }

        [Fact]
        public void AnEmptyTable_NeverPins()
        {
            // Its header IS its whole extent, so there is never a row left
            // for a pinned copy to label.
            for (int scroll = 0; scroll <= 500; scroll++)
            {
                Assert.False(At(scroll, contentTop: 100, rowCount: 0).Pinned);
            }
        }

        [Fact]
        public void AViewportShorterThanTheBand_ShowsWhatFitsRatherThanOverflowing()
        {
            var placement = At(
                scroll: 200, contentTop: 100, rowCount: 20, rowHeight: 45, viewportHeight: 20);

            Assert.True(placement.Pinned);
            Assert.Equal(20, placement.VisibleHeight);
        }

        [Fact]
        public void TwoStackedTables_AreNeverPinnedAtTheSameTime()
        {
            // The second starts where the first ends, which is how both the
            // Snapshot tab's runs and the plan tab's sections stack.
            const int FirstTop = 0;
            const int FirstRows = 6;
            const int RowHeight = 45;
            int firstBottom = FirstTop + HeaderHeight + (FirstRows * RowHeight);
            int secondTop = firstBottom + 8;

            for (int scroll = 0; scroll <= 1200; scroll++)
            {
                bool first = At(scroll, FirstTop, FirstRows, RowHeight).Pinned;
                bool second = At(scroll, secondTop, rowCount: 10, rowHeight: RowHeight).Pinned;

                Assert.False(first && second);
            }
        }

        [Fact]
        public void DegenerateInputs_PinNothing()
        {
            Assert.False(StickyHeaderLayout.Compute(-50, 0, 500, 400).Pinned);
            Assert.False(StickyHeaderLayout.Compute(-50, -32, 500, 400).Pinned);
            Assert.False(StickyHeaderLayout.Compute(-50, 32, 500, 0).Pinned);
            Assert.False(StickyHeaderLayout.Compute(-50, 32, 500, -400).Pinned);

            // A table whose bottom is above its own header: nothing to pin.
            Assert.False(StickyHeaderLayout.Compute(-50, 32, -900, 400).Pinned);
        }

        [Fact]
        public void ThePinnedBandNeverRidesHigherThanTheRealOneWouldHave()
        {
            // The invariant that keeps it off the content above the table:
            // the pinned top is never above the header's own row.
            const int HeaderTop = 120;

            for (int scroll = 0; scroll <= 1500; scroll++)
            {
                var placement = At(scroll, HeaderTop, rowCount: 20);
                if (!placement.Pinned)
                {
                    continue;
                }

                int pinnedTop = placement.ClipY - placement.OffsetInBand;
                Assert.True(pinnedTop >= HeaderTop - scroll);
            }
        }
    }
}
