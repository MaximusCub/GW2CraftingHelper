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

        // --- PinnedRightEdge (the justified-width invariant) ---
        //
        // Replaces the pull-in family (RightBlockX/RightBlockRightEdge):
        // a table's right-hand block used to be dragged LEFT to sit just
        // past the widest name it rendered, which stranded the recovered
        // space to the block's right instead of giving it to the name
        // column. Every block is now pinned to the panel edge and the name
        // column is the only one that flexes.

        [Fact]
        public void PinnedRightEdge_IsThePanelEdgeLessOneMargin()
        {
            Assert.Equal(
                1400 - PlanRelayoutMath.TableRightMargin,
                PlanRelayoutMath.PinnedRightEdge(1400));
        }

        [Fact]
        public void PinnedRightEdge_TracksWidthOneForOne()
        {
            // The justification property: every pixel the panel gains
            // moves the right block by exactly one pixel, so no width
            // produces a stranded band beside it.
            Assert.Equal(
                300,
                PlanRelayoutMath.PinnedRightEdge(1400) - PlanRelayoutMath.PinnedRightEdge(1100));
        }

        [Fact]
        public void PinnedRightEdge_DependsOnNothingButTheWidth()
        {
            // Two tables with wildly different content anchor identically -
            // the property the deleted widestNameEnd parameter broke.
            foreach (int width in new[] { 400, 930, 1352, 4000 })
            {
                Assert.Equal(width - PlanRelayoutMath.TableRightMargin, PlanRelayoutMath.PinnedRightEdge(width));
            }
        }

        [Fact]
        public void PinnedRightEdge_PlusItsMargin_IsExactlyTheFullPanelWidth()
        {
            // What makes header bands and row dividers full-width for
            // free: CTableHeaderRenderer.BandWidth and
            // RowRelayoutHelpers.FinishRow both compute
            // "right edge + TableRightMargin".
            foreach (int width in new[] { 500, 1352 })
            {
                Assert.Equal(
                    width,
                    PlanRelayoutMath.PinnedRightEdge(width) + PlanRelayoutMath.TableRightMargin);
            }
        }

        [Fact]
        public void PinnedRightEdge_NameBudgetAbsorbsEveryPixelOfAWiderPanel()
        {
            // The point of the change, stated as arithmetic: widening the
            // panel by 300px widens the name column by 300px rather than
            // the dead gutter beside a pulled-in block.
            const int nameX = 50;
            const int blockWidth = 60;
            const int gap = 12;

            int narrow = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                PlanRelayoutMath.PinnedRightEdge(900), blockWidth, gap, nameX);
            int wide = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                PlanRelayoutMath.PinnedRightEdge(1200), blockWidth, gap, nameX);

            Assert.Equal(300, wide - narrow);
        }

        // --- ComputeTreeColumnEdges ---

        [Fact]
        public void ComputeTreeColumnEdges_TypicalPanelWidth_MatchesManualArithmetic()
        {
            // Mirrors TreeSectionController's real tree constants:
            // TreePillColumnWidth 256, TreeCostColumnWidth 150 (its floor),
            // TreeRightMargin 8. The other cases below keep 240 - they
            // exercise the arithmetic, not the tree's current sizing.
            int panelWidth = 792;
            int nameX = 24 + 18 + 34 + 6; // depth-1 indent + caret col + icon frame + name gap
            int qtyPrefixWidth = 30;

            var edges = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, nameX, qtyPrefixWidth, pillColumnWidth: 256, costColumnWidth: 150, rightMargin: 8);

            int expectedPillColX = panelWidth - (8 + 150) - 256;
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

        [Fact]
        public void ComputeTreeColumnEdges_WiderPanel_GivesEveryNewPixelToTheNameColumn()
        {
            // The tree's own instance of the justified-width invariant: the
            // pill and cost columns keep their widths and their offsets
            // from each other, and the whole width increase lands in the
            // name budget.
            const int nameX = 58;

            var narrow = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth: 1100, nameX: nameX, qtyPrefixWidth: 26,
                pillColumnWidth: 256, costColumnWidth: 150, rightMargin: 8);
            var wide = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth: 1400, nameX: nameX, qtyPrefixWidth: 26,
                pillColumnWidth: 256, costColumnWidth: 150, rightMargin: 8);

            Assert.Equal(300, wide.NameMaxWidth - narrow.NameMaxWidth);
            Assert.Equal(
                narrow.CostRightEdge - narrow.PillColX,
                wide.CostRightEdge - wide.PillColX);
        }

        // The measurement the 1478px window minimum and the 256px pill
        // column were derived from - docs/research/minimum-window-width.md.
        // "+24 Agony Infusion" is the deepest chain in the game (23 forced
        // levels, one recipe each); its deepest row renders "4194304x
        // Thermocatalytic Reagent".
        //
        // Re-measured at Menomonia 16 for the +2pt body bump, against the
        // same installed XNBs and in the same convention the Font14 figures
        // (65 / 174) were taken in - MonoGame.Extended's own
        // advance / XOffset+Width rule, which is what
        // TreeSectionController's nameFont.MeasureString computes. Both
        // figures are direct measurements: "4194304x " is 73 and
        // "Thermocatalytic Reagent" is 192.
        private const int DeepestRowQtyPrefixWidth = 73;
        private const int DeepestRowNameWidth = 192;

        // Tree row geometry: nameX = depth * TreeIndentPer + (caret column
        // + icon frame + name gap) = depth * 24 + 58.
        private static int TreeNameX(int depth) => depth * 24 + 58;

        // Read from the shipped constants, not copied: the whole point of
        // these cases is that raising or lowering the enforced minimum, or
        // retuning the pill column, moves them with it.
        private static readonly int PlanPanelWidthAtWindowMinimum =
            WindowSizing.TabPanelWidthFor(WindowSizing.MinWindowWidth);

        // Historical literal, deliberately not a production constant: the
        // width the module shipped with before the raise.
        private const int OldWindowMinimumWidth = 930;

        // Live-priced cost column behind a six-digit gold total, which is
        // what the deepest chain costs; 150 is only the floor.
        //
        // Measured at Font16, and taken at the WIDEST digits rather than at
        // one example total: Menomonia's digits are not one width, so the
        // column a plan reserves depends on which digits its total happens
        // to contain. TreeCostColumnMath.SegmentWidth adds
        // CoinLabelIconGap(2) + CoinIconSize(20) to each of three segments
        // and CoinSegmentGap(6) sits twice between them, so the column is
        // the three digit runs' MeasureString widths + 78 of fixed chrome.
        // At Font16 '0' advances 10px and inks 12, '2' and '7' advance 10
        // and ink 11, '1' advances 6, and every other digit advances 9 and
        // inks 11. A run's rect is the leading digits' advances plus the
        // last digit's ink, so the widest six-digit gold plus two two-digit
        // units is drawn from 0/2/7 and ends in '0': 171 (Font14) / 181
        // (Font16), against 161 / 171 for all-nines and 168 / 178 for
        // all-twos. 181 is the worst case, so a minimum derived from it
        // holds for every total; a figure taken at one example would not.
        private const int DeepestPlanCostColumnWidth = 181;

        // The gutter the window minimum was DESIGNED around
        // (docs/research/minimum-window-width.md): the slack the deepest
        // row keeps between its name and the pill column at the minimum
        // width. A research figure, not a production constant - the
        // pull-in machinery that once held it as TableGutterBreathingRoom
        // is gone.
        private const int DesignedNameGutter = 24;

        private static PlanRelayoutMath.TreeColumnEdges DeepestRowEdges(int panelWidth, int depth)
        {
            return PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, TreeNameX(depth), DeepestRowQtyPrefixWidth,
                PlanRelayoutMath.TreePillColumnWidth, DeepestPlanCostColumnWidth,
                rightMargin: 8);
        }

        [Fact]
        public void ComputeTreeColumnEdges_DeepestRowInTheGame_KeepsTheDesignedGutterAtTheWindowMinimum()
        {
            var edges = DeepestRowEdges(PlanPanelWidthAtWindowMinimum, depth: 23);

            Assert.Equal(DeepestRowNameWidth + DesignedNameGutter, edges.NameMaxWidth);
        }

        [Fact]
        public void ComputeTreeColumnEdges_OneVendorLeafBelowTheDeepestRow_StillFitsAtTheWindowMinimum()
        {
            // CraftingTreeBuilder.BuildVendorCostComponentLeaves can
            // synthesise a leaf one indent level below the recipe graph.
            // That level is the headroom the minimum was rounded up for: it
            // spends the gutter exactly, and nothing is ellipsized.
            var edges = DeepestRowEdges(PlanPanelWidthAtWindowMinimum, depth: 24);

            Assert.Equal(DeepestRowNameWidth, edges.NameMaxWidth);
        }

        [Fact]
        public void ComputeTreeColumnEdges_DeepestRowAtTheOldMinimum_WasSeverelyTruncated()
        {
            // Why the minimum moved: at the old 930px window (804px panel)
            // the same row had no name column left at all and clamped to
            // the 10px floor - a bare ellipsis.
            var edges = DeepestRowEdges(
                WindowSizing.TabPanelWidthFor(OldWindowMinimumWidth), depth: 23);

            Assert.Equal(10, edges.NameMaxWidth);
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
            var widths = new[] { 50, 60, 40 };

            var fit = PlanRelayoutMath.ComputePillFit(
                widths, widthReduction: 6, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(3, fit.VisibleCount);
            Assert.Equal(0, fit.HiddenCount);
            Assert.Equal(0, fit.WidthReduction);
            Assert.Equal(0, fit.OverflowPillWidth);
        }

        [Fact]
        public void ComputePillFit_TighteningIsEnough_KeepsEveryPill()
        {
            // 60+6+60+6+60+6+60 = 258 at full width (one pill over 240),
            // 54+6+54+6+54+6+54 = 234 once each pill loses 6px. Squeezing
            // beats hiding a real option, so nothing is dropped and no
            // "+N" appears.
            var widths = new[] { 60, 60, 60, 60 };

            var fit = PlanRelayoutMath.ComputePillFit(
                widths, widthReduction: 6, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(4, fit.VisibleCount);
            Assert.Equal(0, fit.HiddenCount);
            Assert.Equal(6, fit.WidthReduction);
            Assert.Equal(0, fit.OverflowPillWidth);
        }

        [Fact]
        public void ComputePillFit_StillOverflowsAfterTightening_ReservesOverflowPill()
        {
            // Mirrors the live shape: CRAFT/TP/VENDOR then the wide
            // "HAVE 12/20 NEEDED" annotation and IGNORE. Even tightened the
            // set overruns, so the row announces the remainder instead of
            // ending early.
            var widths = new[] { 60, 55, 60, 120, 55 };

            var fit = PlanRelayoutMath.ComputePillFit(
                widths, widthReduction: 6, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(6, fit.WidthReduction);
            Assert.True(fit.HiddenCount > 0);
            Assert.Equal(5, fit.VisibleCount + fit.HiddenCount);
            Assert.Equal(OverflowWidth(fit.HiddenCount), fit.OverflowPillWidth);

            // The visible run plus the reserved "+N" must actually fit the
            // budget - the whole point of reserving it up front.
            int used = 0;
            for (int i = 0; i < fit.VisibleCount; i++)
            {
                used += PlanRelayoutMath.ReducedWidth(widths[i], fit.WidthReduction) + 6;
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
                widths, widthReduction: 0, gap: 6, startX: 0, maxRightEdge: 240,
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
            var widths = new[] { 60, 55, 60, 120, 55 };

            var fit = PlanRelayoutMath.ComputePillFit(
                widths, widthReduction: 6, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: null);

            Assert.Equal(0, fit.HiddenCount);
            Assert.Equal(0, fit.OverflowPillWidth);
            Assert.True(fit.VisibleCount > 0);
        }

        [Fact]
        public void ComputePillFit_NoTighteningAvailable_SkipsStraightToOverflow()
        {
            var widths = new[] { 60, 55, 60, 120, 55 };

            var fit = PlanRelayoutMath.ComputePillFit(
                widths, widthReduction: 0, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(0, fit.WidthReduction);
            Assert.Equal(5, fit.VisibleCount + fit.HiddenCount);
            Assert.True(fit.HiddenCount > 0);
        }

        [Fact]
        public void ComputePillFit_SinglePillWiderThanBudget_StillDrawsItAndHidesNothing()
        {
            var widths = new[] { 400 };

            var fit = PlanRelayoutMath.ComputePillFit(
                widths, widthReduction: 6, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(1, fit.VisibleCount);
            Assert.Equal(0, fit.HiddenCount);
        }

        [Fact]
        public void ComputePillFit_EmptyOrNull_ReturnsNothing()
        {
            var empty = PlanRelayoutMath.ComputePillFit(
                new int[0], widthReduction: 6, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);
            var none = PlanRelayoutMath.ComputePillFit(
                null, widthReduction: 6, gap: 6, startX: 0, maxRightEdge: 240,
                overflowPillWidthForHidden: OverflowWidth);

            Assert.Equal(0, empty.VisibleCount);
            Assert.Equal(0, empty.HiddenCount);
            Assert.Equal(0, none.VisibleCount);
            Assert.Equal(0, none.HiddenCount);
        }

        [Fact]
        public void ReducedWidth_NeverGoesBelowOnePixel()
        {
            Assert.Equal(54, PlanRelayoutMath.ReducedWidth(60, 6));
            Assert.Equal(60, PlanRelayoutMath.ReducedWidth(60, 0));
            Assert.Equal(1, PlanRelayoutMath.ReducedWidth(4, 20));
        }
    }
}
