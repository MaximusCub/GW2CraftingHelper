using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The Snapshot header's shared formulas, plus the two properties that
    /// motivated them: a source-filter run that fits beside the search box
    /// costs the header no height at all, and a run that does NOT fit there
    /// drops to its own full-width row rather than being squeezed into half
    /// the width and hidden behind the cap's scrollbar. Exercises the real
    /// SnapshotHeaderLayout and the real SourceFilterFlowLayout together -
    /// the width handed to the flow is the only thing that changed about it,
    /// so what matters is what the flow does when handed each one.
    /// </summary>
    public class SnapshotHeaderLayoutTests
    {
        // MainView's own constants for the rows this shares.
        private const int SearchRowHeight = 35;
        private const int SourceFilterX = 470;
        private const int SearchToFilterGapY = 3;
        private const int CellHeight = 25;
        private const int CellGapX = 10;
        private const int RowGapY = 4;
        private const int TopPad = 3;
        private const int BottomPad = 2;

        private static int FilterHeight(SourceFilterFlowResult flow)
        {
            return TopPad + flow.TotalHeight + BottomPad;
        }

        private static SnapshotHeaderLayout.SourceFilterPlacement Place(int panelWidth, bool shares)
        {
            return SnapshotHeaderLayout.PlaceSourceFilterRun(
                panelWidth, SourceFilterX, SearchRowHeight, SearchToFilterGapY, shares);
        }

        private static SourceFilterFlowResult Flow(IReadOnlyList<int> widths, int availableWidth)
        {
            return SourceFilterFlowLayout.Layout(widths, availableWidth, CellHeight, CellGapX, RowGapY);
        }

        /// <summary>
        /// The two-pass resolution MainView.ApplyTopRegionLayout runs: flow
        /// beside the search box, and re-flow on the run's own row when that
        /// wrapped it.
        /// </summary>
        private static (SnapshotHeaderLayout.SourceFilterPlacement Placement, SourceFilterFlowResult Flow) Resolve(
            IReadOnlyList<int> widths, int panelWidth)
        {
            var placement = Place(panelWidth, shares: true);
            var flow = Flow(widths, placement.Width);
            if (SnapshotHeaderLayout.SharesSearchRow(flow.RowCount))
            {
                return (placement, flow);
            }

            placement = Place(panelWidth, shares: false);
            return (placement, Flow(widths, placement.Width));
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
            // One row of checkboxes (30px) is shorter than the search row it
            // shares, so the band - and therefore every row below it - is
            // exactly where it would be with no filter row at all.
            Assert.Equal(
                SearchRowHeight,
                SnapshotHeaderLayout.SearchBandHeight(SearchRowHeight, 30, Place(1400, shares: true)));
        }

        [Fact]
        public void SearchBandHeight_RunOnItsOwnRow_CostsTheSearchRowPlusTheGapPlusItself()
        {
            // The pre-share layout, reproduced exactly: the run starts one
            // gap below the search row and the band covers both.
            Assert.Equal(
                SearchRowHeight + SearchToFilterGapY + 30,
                SnapshotHeaderLayout.SearchBandHeight(SearchRowHeight, 30, Place(1400, shares: false)));
        }

        [Fact]
        public void PlaceSourceFilterRun_OwnRow_SpansThePanelBelowTheSearchRow()
        {
            var placement = Place(884, shares: false);

            Assert.False(placement.SharesSearchRow);
            Assert.Equal(0, placement.X);
            Assert.Equal(884, placement.Width);
            Assert.Equal(SearchRowHeight + SearchToFilterGapY, placement.OffsetY);
        }

        [Fact]
        public void SingleRowRoster_SharesTheSearchRowForFree()
        {
            // Three storage checkboxes and one character: fits in the right
            // half at a wide window, so the header spends zero extra pixels
            // on the filter.
            var resolved = Resolve(new List<int> { 70, 150, 150, 110 }, 1400);

            Assert.True(resolved.Placement.SharesSearchRow);
            Assert.Equal(1, resolved.Flow.RowCount);
            Assert.Equal(
                SearchRowHeight,
                SnapshotHeaderLayout.SearchBandHeight(
                    SearchRowHeight, FilterHeight(resolved.Flow), resolved.Placement));
        }

        [Fact]
        public void RosterThatWouldWrapBesideTheSearchBox_TakesItsOwnRowInstead()
        {
            // The 15-character account the audit measured: 19 cells at the
            // window's minimum content width. Beside the search box that is
            // several rows deep; on its own row it is far fewer, which is the
            // whole point - the same cells, visible rather than scrolled.
            var widths = new List<int> { 70, 150, 150, 140 };
            for (int i = 0; i < 15; i++)
            {
                widths.Add(124);
            }

            var beside = Flow(widths, Place(884, shares: true).Width);
            var resolved = Resolve(widths, 884);

            Assert.False(SnapshotHeaderLayout.SharesSearchRow(beside.RowCount));
            Assert.False(resolved.Placement.SharesSearchRow);
            Assert.True(resolved.Flow.RowCount < beside.RowCount);
            Assert.Equal(widths.Count, resolved.Flow.Cells.Count);
        }

        [Fact]
        public void OwnRowFallback_FitsInsideTheFourRowCapWhereSharingWouldNot()
        {
            // The failure the fallback exists for: at the shared width this
            // roster runs past the 4-row cap, so a third of the filters end
            // up behind a scrollbar inside a 117px box. At full width it fits
            // the cap outright.
            const int maxRows = 4;
            var widths = new List<int> { 70, 150, 150, 140 };
            for (int i = 0; i < 15; i++)
            {
                widths.Add(124);
            }

            var beside = Flow(widths, Place(884, shares: true).Width);
            var resolved = Resolve(widths, 884);

            Assert.True(beside.RowCount > maxRows);
            Assert.True(resolved.Flow.RowCount <= maxRows);
        }

        [Fact]
        public void LargeRoster_StillWrapsAndNothingIsDropped()
        {
            var widths = new List<int> { 70, 150, 150, 140 };
            for (int i = 0; i < 12; i++)
            {
                widths.Add(120);
            }

            var resolved = Resolve(widths, 884);

            // Every cell still placed (nothing dropped), and the band grows
            // by exactly the run's own height plus the row it now owns.
            Assert.Equal(widths.Count, resolved.Flow.Cells.Count);
            int band = SnapshotHeaderLayout.SearchBandHeight(
                SearchRowHeight, FilterHeight(resolved.Flow), resolved.Placement);
            Assert.Equal(
                SearchRowHeight + SearchToFilterGapY + FilterHeight(resolved.Flow), band);
        }

        [Fact]
        public void ZeroWidth_PlacesEveryCellOnItsOwnRowRatherThanDroppingAny()
        {
            // The degenerate window: SourceFilterWidth floors at 0, and the
            // flow degrades to one cell per row - wrapped and then scrolled,
            // never clipped away.
            var flow = Flow(
                new List<int> { 70, 150, 150 },
                SnapshotHeaderLayout.SourceFilterWidth(300, SourceFilterX));

            Assert.Equal(3, flow.RowCount);
            Assert.Equal(3, flow.Cells.Count);
        }

        [Fact]
        public void NoCellsYet_SharesTheSearchRow()
        {
            // The state before the first snapshot lands: nothing to flow, so
            // nothing to move off the search row.
            var flow = Flow(new List<int>(), Place(884, shares: true).Width);

            Assert.Equal(0, flow.RowCount);
            Assert.True(SnapshotHeaderLayout.SharesSearchRow(flow.RowCount));
        }
    }
}
