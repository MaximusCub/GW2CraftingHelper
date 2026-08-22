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

        // --- ComputeVisiblePillCount ---
        // Regression: DecisionPillPlanner's now-unconditional
        // OwnedInfo/Ignore pills regularly overflow the tree row's fixed
        // 240px pill column, overlapping the right-aligned cost column.
        // CraftingPlanView.RenderDecisionPills uses this pure helper to
        // decide how many (already width-measured, emission-order) pills
        // to actually render.

        [Fact]
        public void ComputeVisiblePillCount_AllPillsFit_ReturnsFullCount()
        {
            var widths = new[] { 50, 60, 40 };

            int count = PlanRelayoutMath.ComputeVisiblePillCount(
                widths, gap: 6, startX: 0, maxRightEdge: 300);

            Assert.Equal(3, count);
        }

        [Fact]
        public void ComputeVisiblePillCount_TrailingPillsOverflow_TruncatesFromFirstThatDoesNotFit()
        {
            // Mirrors the live regression: CRAFT/TP/VENDOR (fits) followed
            // by "USING 12 OWNED" and "IGNORE" (the pair that overflows).
            var widths = new[] { 60, 55, 60, 120, 55 };

            int count = PlanRelayoutMath.ComputeVisiblePillCount(
                widths, gap: 6, startX: 0, maxRightEdge: 240);

            Assert.Equal(3, count);
        }

        [Fact]
        public void ComputeVisiblePillCount_ExactFit_IncludesTheExactlyFittingPill()
        {
            // Two 50-wide pills with a 6px gap need exactly 106px; a budget
            // of precisely 106 must include both, not truncate at the
            // boundary.
            var widths = new[] { 50, 50 };

            int count = PlanRelayoutMath.ComputeVisiblePillCount(
                widths, gap: 6, startX: 0, maxRightEdge: 106);

            Assert.Equal(2, count);
        }

        [Fact]
        public void ComputeVisiblePillCount_FirstPillAloneExceedsBudget_StillRendersIt()
        {
            // A completely empty pill column reads worse than a single
            // pill that slightly overflows a pathologically narrow panel.
            var widths = new[] { 500 };

            int count = PlanRelayoutMath.ComputeVisiblePillCount(
                widths, gap: 6, startX: 0, maxRightEdge: 240);

            Assert.Equal(1, count);
        }

        [Fact]
        public void ComputeVisiblePillCount_EmptyWidths_ReturnsZero()
        {
            int count = PlanRelayoutMath.ComputeVisiblePillCount(
                new int[0], gap: 6, startX: 0, maxRightEdge: 240);

            Assert.Equal(0, count);
        }

        [Fact]
        public void ComputeVisiblePillCount_NullWidths_ReturnsZero()
        {
            int count = PlanRelayoutMath.ComputeVisiblePillCount(
                null, gap: 6, startX: 0, maxRightEdge: 240);

            Assert.Equal(0, count);
        }

        // --- ComputePillFit ---
        //
        // The escalation the tree row actually runs: draw everything, else
        // tighten padding, else tighten AND announce the remainder with a
        // "+N" pill. The old behaviour stopped at "draw as many as fit and
        // say nothing".

        // Stand-in for the renderer's MeasureString of "+N": a fixed base
        // plus one unit per digit, so a width that grows with the digit
        // count (the only thing that can move the fixed point) is exercised
        // rather than assumed away.
        private static int OverflowWidth(int hidden)
        {
            return 20 + hidden.ToString().Length * 6;
        }

        [Fact]
        public void ComputePillFit_AllFitAtFullPadding_NoTighteningNoOverflow()
        {
            var full = new[] { 50, 60, 40 };
            var tight = new[] { 44, 54, 34 };

            var fit = PlanRelayoutMath.ComputePillFit(
                full, tight, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(3, fit.VisibleCount);
            Assert.Equal(0, fit.HiddenCount);
            Assert.False(fit.ReducedPadding);
            Assert.Equal(0, fit.OverflowPillWidth);
        }

        [Fact]
        public void ComputePillFit_TighteningIsEnough_KeepsEveryPill()
        {
            // 60+6+60+6+60+6+60 = 258 at full padding (one pill over 240),
            // 54+6+54+6+54+6+54 = 234 tightened. Squeezing beats hiding a
            // real option, so nothing is dropped and no "+N" appears.
            var full = new[] { 60, 60, 60, 60 };
            var tight = new[] { 54, 54, 54, 54 };

            var fit = PlanRelayoutMath.ComputePillFit(
                full, tight, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(4, fit.VisibleCount);
            Assert.Equal(0, fit.HiddenCount);
            Assert.True(fit.ReducedPadding);
            Assert.Equal(0, fit.OverflowPillWidth);
        }

        [Fact]
        public void ComputePillFit_StillOverflowsAfterTightening_ReservesOverflowPill()
        {
            // Mirrors the live shape: CRAFT/TP/VENDOR then the wide
            // "HAVE 12/20 NEEDED" annotation and IGNORE. Even tightened the
            // set overruns, so the row announces the remainder instead of
            // ending early.
            var full = new[] { 60, 55, 60, 120, 55 };
            var tight = new[] { 54, 49, 54, 114, 49 };

            var fit = PlanRelayoutMath.ComputePillFit(
                full, tight, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.True(fit.ReducedPadding);
            Assert.True(fit.HiddenCount > 0);
            Assert.Equal(5, fit.VisibleCount + fit.HiddenCount);
            Assert.Equal(OverflowWidth(fit.HiddenCount), fit.OverflowPillWidth);

            // The visible run plus the reserved "+N" must actually fit the
            // budget - the whole point of reserving it up front.
            int used = 0;
            for (int i = 0; i < fit.VisibleCount; i++)
            {
                used += tight[i] + 6;
            }
            Assert.True(used + fit.OverflowPillWidth <= 240);
        }

        [Fact]
        public void ComputePillFit_ReservingOverflowDisplacesAnotherPill_CountsStayConsistent()
        {
            // The reserved "+N" is wide enough to push out the pill that
            // only just fit, so HiddenCount must reflect the post-reserve
            // truth, not the pre-reserve estimate.
            var widths = new[] { 100, 100, 100, 100 };

            var fit = PlanRelayoutMath.ComputePillFit(
                widths, widths, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(4, fit.VisibleCount + fit.HiddenCount);
            Assert.True(fit.VisibleCount >= 1);
            Assert.True(fit.HiddenCount >= 1);
        }

        [Fact]
        public void ComputePillFit_NoOverflowMeasurer_DegradesToSilentDrop()
        {
            // Defensive: a null measurer must not throw or invent a pill it
            // cannot size - it reverts to the pre-existing behaviour.
            var full = new[] { 60, 55, 60, 120, 55 };
            var tight = new[] { 54, 49, 54, 114, 49 };

            var fit = PlanRelayoutMath.ComputePillFit(
                full, tight, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: null);

            Assert.Equal(0, fit.HiddenCount);
            Assert.Equal(0, fit.OverflowPillWidth);
            Assert.True(fit.VisibleCount > 0);
        }

        [Fact]
        public void ComputePillFit_MismatchedReducedWidths_IgnoresThemRatherThanIndexing()
        {
            var full = new[] { 60, 55, 60, 120, 55 };

            var fit = PlanRelayoutMath.ComputePillFit(
                full, new[] { 54, 49 }, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.False(fit.ReducedPadding);
            Assert.Equal(5, fit.VisibleCount + fit.HiddenCount);
        }

        [Fact]
        public void ComputePillFit_SinglePillWiderThanBudget_StillDrawsItAndHidesNothing()
        {
            var widths = new[] { 400 };

            var fit = PlanRelayoutMath.ComputePillFit(
                widths, widths, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(1, fit.VisibleCount);
            Assert.Equal(0, fit.HiddenCount);
        }

        [Fact]
        public void ComputePillFit_EmptyOrNull_ReturnsNothing()
        {
            var empty = PlanRelayoutMath.ComputePillFit(
                new int[0], new int[0], gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);
            var none = PlanRelayoutMath.ComputePillFit(
                null, null, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(0, empty.VisibleCount);
            Assert.Equal(0, empty.HiddenCount);
            Assert.Equal(0, none.VisibleCount);
            Assert.Equal(0, none.HiddenCount);
        }
    }
}
