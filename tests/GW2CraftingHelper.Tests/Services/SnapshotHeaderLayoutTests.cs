using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The Snapshot header's two shared formulas, plus the property that
    /// motivated them: the source-filter run moved into the search row's
    /// empty right half, so a run that fits there costs the header no
    /// height at all. Exercises the real SnapshotHeaderLayout and the real
    /// SourceFilterFlowLayout together - the reduced width is the only
    /// thing that changed about the flow, so what matters is that the flow
    /// still wraps (and still reports a height the band can absorb) when
    /// handed it.
    /// </summary>
    public class SnapshotHeaderLayoutTests
    {
        // MainView's own constants for the row this shares.
        private const int SearchRowHeight = 35;
        private const int SourceFilterX = 470;
        private const int CellHeight = 25;
        private const int CellGapX = 10;
        private const int RowGapY = 4;
        private const int TopPad = 3;
        private const int BottomPad = 2;

        private static int FilterHeight(SourceFilterFlowResult flow)
        {
            return TopPad + flow.TotalHeight + BottomPad;
        }

        [Fact]
        public void SourceFilterWidth_IsWhatIsLeftOfThePanelPastTheStartOffset()
        {
            Assert.Equal(414, SnapshotHeaderLayout.SourceFilterWidth(884, SourceFilterX));
        }

        [Fact]
        public void SourceFilterWidth_PanelNarrowerThanTheOffset_FloorsAtZero()
        {
            Assert.Equal(0, SnapshotHeaderLayout.SourceFilterWidth(300, SourceFilterX));
        }

        [Fact]
        public void SearchBandHeight_FilterFitsBesideTheSearchBox_CostsNothing()
        {
            // One row of checkboxes (30px) is shorter than the search row
            // it now shares, so the band - and therefore every row below
            // it - is exactly where it would be with no filter row at all.
            Assert.Equal(SearchRowHeight, SnapshotHeaderLayout.SearchBandHeight(SearchRowHeight, 30));
        }

        [Fact]
        public void SearchBandHeight_WrappedFilter_DrivesTheBand()
        {
            Assert.Equal(117, SnapshotHeaderLayout.SearchBandHeight(SearchRowHeight, 117));
        }

        [Fact]
        public void SingleRowRoster_SharesTheSearchRowForFree()
        {
            // Three storage checkboxes and one character: fits in the
            // 414px right half at the window's minimum content width, so
            // the header spends zero extra pixels on the filter.
            var flow = SourceFilterFlowLayout.Layout(
                new List<int> { 70, 150, 150, 110 },
                SnapshotHeaderLayout.SourceFilterWidth(1400, SourceFilterX),
                CellHeight, CellGapX, RowGapY);

            Assert.Equal(1, flow.RowCount);
            Assert.Equal(
                SearchRowHeight,
                SnapshotHeaderLayout.SearchBandHeight(SearchRowHeight, FilterHeight(flow)));
        }

        [Fact]
        public void LargeRoster_StillWrapsAtTheReducedWidthAndNothingIsDropped()
        {
            var widths = new List<int> { 70, 150, 150, 140 };
            for (int i = 0; i < 12; i++)
            {
                widths.Add(120);
            }

            var flow = SourceFilterFlowLayout.Layout(
                widths,
                SnapshotHeaderLayout.SourceFilterWidth(884, SourceFilterX),
                CellHeight, CellGapX, RowGapY);

            Assert.True(flow.RowCount > 1);

            // Every cell still placed (nothing dropped), and the band only
            // grows by what the run needs past the search row's height.
            Assert.Equal(widths.Count, flow.Cells.Count);
            int band = SnapshotHeaderLayout.SearchBandHeight(SearchRowHeight, FilterHeight(flow));
            Assert.Equal(FilterHeight(flow), band);
            Assert.True(band - SearchRowHeight < FilterHeight(flow));
        }

        [Fact]
        public void ZeroWidth_PlacesEveryCellOnItsOwnRowRatherThanDroppingAny()
        {
            // The degenerate window: SourceFilterWidth floors at 0, and the
            // flow degrades to one cell per row - wrapped and then
            // scrolled, never clipped away.
            var flow = SourceFilterFlowLayout.Layout(
                new List<int> { 70, 150, 150 },
                SnapshotHeaderLayout.SourceFilterWidth(300, SourceFilterX),
                CellHeight, CellGapX, RowGapY);

            Assert.Equal(3, flow.RowCount);
            Assert.Equal(3, flow.Cells.Count);
        }
    }
}
