using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanHistoryRowLayoutTests
    {
        // The five desktop-gate widths, as PANEL widths (the window minus
        // its chrome), matching RankerRowLayoutTests' own convention.
        public static TheoryData<int> GateWidths => new TheoryData<int>
        {
            WindowSizing.TabPanelWidthFor(1378),
            WindowSizing.TabPanelWidthFor(1638),
            WindowSizing.TabPanelWidthFor(1836),
            WindowSizing.TabPanelWidthFor(2406),
            WindowSizing.TabPanelWidthFor(2560),
        };

        private const int CostWidth = 120;
        private const int WhenWidth = 150;

        [Theory]
        [MemberData(nameof(GateWidths))]
        public void AtEveryGateWidth_NameFlexes_NothingOverlaps_ClusterEndsAtTheRightEdge(int rowWidth)
        {
            var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);

            // The name band is real and never runs under the cost cell.
            Assert.True(bands.NameWidth > 0);
            int costLeftEdge = bands.CostRightEdge - CostWidth;
            Assert.True(bands.NameX + bands.NameWidth <= costLeftEdge);

            // Cost sits left of the timestamp, timestamp left of the cluster.
            Assert.True(bands.CostRightEdge < bands.WhenX);
            Assert.True(bands.WhenX + bands.WhenWidth < bands.ViewX);

            // The five buttons run View, Open, Re-solve, Pin, Delete with
            // no overlap...
            Assert.True(bands.ViewX + PlanHistoryRowLayout.ActionButtonWidth <= bands.OpenX);
            Assert.True(bands.OpenX + PlanHistoryRowLayout.ActionButtonWidth <= bands.ResolveX);
            Assert.True(bands.ResolveX + PlanHistoryRowLayout.ActionButtonWidth <= bands.PinX);
            Assert.True(bands.PinX + PlanHistoryRowLayout.IconButtonWidth <= bands.DeleteX);

            // ...and the rightmost button's right edge lands exactly at
            // rowWidth - Inset: no band of empty space to the right.
            Assert.Equal(rowWidth - PlanHistoryRowLayout.Inset,
                bands.DeleteX + PlanHistoryRowLayout.IconButtonWidth);
        }

        [Theory]
        [MemberData(nameof(GateWidths))]
        public void NameConsumesEveryPixelThePinnedBlockDoesNot(int rowWidth)
        {
            var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);

            // The flexing law: NameWidth is exactly the space between the
            // name's left edge and the cost cell's left edge minus one
            // cell gap - nothing is left stranded.
            Assert.Equal(
                bands.CostRightEdge - CostWidth - PlanHistoryRowLayout.CellGap - bands.NameX,
                bands.NameWidth);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-50)]
        [InlineData(200)]
        public void DegenerateWidths_ClampRatherThanGoingNegative(int rowWidth)
        {
            var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);

            Assert.True(bands.NameWidth >= 0);
            Assert.True(bands.WhenWidth >= 0);
            Assert.True(bands.RowWidth >= 0);
        }

        [Fact]
        public void CostAndWhenBands_AreFlooredSoAnEmptyTableCannotCollapseThem()
        {
            // The header-label collision RankerRowLayout documents: a
            // measured width of 0 must behave exactly like the floor.
            var floored = PlanHistoryRowLayout.Compute(1200, 0, 0);
            var atFloor = PlanHistoryRowLayout.Compute(
                1200, PlanHistoryRowLayout.MinCostCellWidth, PlanHistoryRowLayout.MinWhenWidth);

            Assert.Equal(atFloor.NameWidth, floored.NameWidth);
            Assert.Equal(atFloor.WhenX, floored.WhenX);
            Assert.Equal(PlanHistoryRowLayout.MinWhenWidth, floored.WhenWidth);

            // Above the floor, every extra cost pixel comes out of the
            // flexing name band, nothing else moves.
            var wide = PlanHistoryRowLayout.Compute(1200, PlanHistoryRowLayout.MinCostCellWidth + 40, 0);
            Assert.Equal(40, floored.NameWidth - wide.NameWidth);
            Assert.Equal(floored.WhenX, wide.WhenX);
        }

        [Fact]
        public void DetailHeight_IsMonotonicInItemCount()
        {
            int previous = -1;
            for (int items = 0; items <= 10; items++)
            {
                int height = PlanHistoryRowLayout.DetailHeight(
                    items, hasChips: false, hasSampleLine: false, hasBlobNote: false, hasOverridesNote: false);
                Assert.True(height > previous);
                previous = height;
            }
        }

        [Fact]
        public void DetailHeight_AddsExactlyOneLinePerOptionalBlock()
        {
            int baseline = PlanHistoryRowLayout.DetailHeight(2, false, false, false, false);

            Assert.Equal(baseline + PlanHistoryRowLayout.DetailChipsLineHeight,
                PlanHistoryRowLayout.DetailHeight(2, true, false, false, false));
            Assert.Equal(baseline + PlanHistoryRowLayout.DetailNoteLineHeight,
                PlanHistoryRowLayout.DetailHeight(2, false, true, false, false));
            Assert.Equal(baseline + PlanHistoryRowLayout.DetailNoteLineHeight,
                PlanHistoryRowLayout.DetailHeight(2, false, false, true, false));
            Assert.Equal(baseline + PlanHistoryRowLayout.DetailNoteLineHeight,
                PlanHistoryRowLayout.DetailHeight(2, false, false, false, true));
        }
    }
}
