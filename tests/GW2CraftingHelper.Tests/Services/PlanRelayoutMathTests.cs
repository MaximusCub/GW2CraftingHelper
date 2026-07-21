using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanRelayoutMathTests
    {
        // --- CenterX ---

        [Fact]
        public void CenterX_EvenRemainder_SplitsEqually()
        {
            Assert.Equal(50, PlanRelayoutMath.CenterX(containerWidth: 300, contentWidth: 200));
        }

        [Fact]
        public void CenterX_ContentWiderThanContainer_ClampsToZero()
        {
            Assert.Equal(0, PlanRelayoutMath.CenterX(containerWidth: 100, contentWidth: 400));
        }

        [Fact]
        public void CenterX_ZeroContent_CentersAtHalfContainer()
        {
            Assert.Equal(150, PlanRelayoutMath.CenterX(containerWidth: 300, contentWidth: 0));
        }

        // --- RightAlignedX ---

        [Fact]
        public void RightAlignedX_SubtractsWidthFromEdge()
        {
            Assert.Equal(742, PlanRelayoutMath.RightAlignedX(rightEdge: 792, width: 50));
        }

        [Fact]
        public void RightAlignedX_WidthExceedsEdge_CanGoNegative()
        {
            // No clamping here by design - a control wider than its
            // reserved band is a data/measurement problem the caller should
            // surface, not silently hide behind a clamp.
            Assert.Equal(-8, PlanRelayoutMath.RightAlignedX(rightEdge: 100, width: 108));
        }

        // --- NameMaxWidthBeforeColumn ---

        [Fact]
        public void NameMaxWidthBeforeColumn_TypicalUsedMaterialsRow()
        {
            // Mirrors CraftingPlanView.CreateUsedMaterialRow: panelWidth-8
            // as the trailing edge, nameX=50, gap=12.
            int panelWidth = 792;
            int qtyRightEdge = panelWidth - 8;
            int result = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                columnRightXBeforeGap: qtyRightEdge, trailingColumnWidth: 40, gapBeforeColumn: 12, nameX: 50);

            Assert.Equal(qtyRightEdge - 40 - 12 - 50, result);
        }

        [Fact]
        public void NameMaxWidthBeforeColumn_NarrowPanel_ClampsToFloor()
        {
            int result = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                columnRightXBeforeGap: 60, trailingColumnWidth: 40, gapBeforeColumn: 12, nameX: 50);

            Assert.Equal(20, result);
        }

        // --- ComputeTreeColumnEdges ---

        [Fact]
        public void ComputeTreeColumnEdges_TypicalPanelWidth_MatchesManualArithmetic()
        {
            // Mirrors CraftingPlanView's real tree constants: pillColumnWidth
            // 240, costColumnWidth 150, rightMargin 8.
            int panelWidth = 792;
            int nameX = 24 + 18 + 34 + 6; // depth-1 indent + caret col + icon frame + name gap
            int qtyPrefixWidth = 30;

            var edges = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, nameX, qtyPrefixWidth, pillColumnWidth: 240, costColumnWidth: 150, rightMargin: 8);

            int expectedPillColX = panelWidth - (8 + 150) - 240;
            int expectedCostRightEdge = panelWidth - 8;
            int expectedNameMax = System.Math.Max(20, expectedPillColX - nameX - 8) - qtyPrefixWidth;

            Assert.Equal(expectedPillColX, edges.PillColX);
            Assert.Equal(expectedCostRightEdge, edges.CostRightEdge);
            Assert.Equal(expectedNameMax, edges.NameMaxWidth);
        }

        [Fact]
        public void ComputeTreeColumnEdges_NarrowPanel_NameWidthClampsToFloor()
        {
            // panelWidth is so narrow pillColX goes negative; nameMaxWidth
            // clamps to its 20px floor before qtyPrefixWidth is subtracted.
            var edges = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth: 300, nameX: 60, qtyPrefixWidth: 0,
                pillColumnWidth: 240, costColumnWidth: 150, rightMargin: 8);

            Assert.Equal(20, edges.NameMaxWidth);
        }

        [Fact]
        public void ComputeTreeColumnEdges_NarrowPanelWithWideQtyPrefix_ClampsToTenPxFloor()
        {
            // Same narrow panel, but now qtyPrefixWidth alone exceeds the
            // already-clamped 20px nameMaxWidth - the second (10px) floor
            // kicks in.
            var edges = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth: 300, nameX: 60, qtyPrefixWidth: 30,
                pillColumnWidth: 240, costColumnWidth: 150, rightMargin: 8);

            Assert.Equal(10, edges.NameMaxWidth);
        }

        [Fact]
        public void ComputeTreeColumnEdges_WiderPanel_ColumnsShiftRightButStayFixedWidth()
        {
            var narrow = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth: 700, nameX: 50, qtyPrefixWidth: 0,
                pillColumnWidth: 240, costColumnWidth: 150, rightMargin: 8);
            var wide = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth: 1000, nameX: 50, qtyPrefixWidth: 0,
                pillColumnWidth: 240, costColumnWidth: 150, rightMargin: 8);

            Assert.Equal(300, wide.PillColX - narrow.PillColX);
            Assert.Equal(300, wide.CostRightEdge - narrow.CostRightEdge);
        }

        // --- ComputeCostTileGeometry ---

        [Fact]
        public void ComputeCostTileGeometry_TypicalWidth_TilesFillEvenly()
        {
            var geometry = PlanRelayoutMath.ComputeCostTileGeometry(
                panelWidth: 792, tileCount: 3, totalMargin: 40, minTileWidth: 80);

            int expectedTileWidth = (792 - 40) / 3;
            Assert.Equal(expectedTileWidth, geometry.TileWidth);
            Assert.Equal(PlanRelayoutMath.CenterX(792, expectedTileWidth * 3), geometry.StartX);
        }

        [Fact]
        public void ComputeCostTileGeometry_NarrowPanel_ClampsToMinTileWidth()
        {
            var geometry = PlanRelayoutMath.ComputeCostTileGeometry(
                panelWidth: 200, tileCount: 5, totalMargin: 40, minTileWidth: 80);

            Assert.Equal(80, geometry.TileWidth);
        }

        [Fact]
        public void ComputeCostTileGeometry_ZeroTiles_ReturnsZeroGeometry()
        {
            var geometry = PlanRelayoutMath.ComputeCostTileGeometry(
                panelWidth: 792, tileCount: 0, totalMargin: 40, minTileWidth: 80);

            Assert.Equal(0, geometry.TileWidth);
            Assert.Equal(0, geometry.StartX);
        }
    }
}
