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

        // --- RightBlockX (audit batch H: dead gutters) ---

        [Fact]
        public void RightBlockX_NothingMeasured_StaysPinned()
        {
            Assert.Equal(500, PlanRelayoutMath.RightBlockX(pinnedX: 500, widestNameEnd: 0));
            Assert.Equal(500, PlanRelayoutMath.RightBlockX(pinnedX: 500, widestNameEnd: -1));
        }

        [Fact]
        public void RightBlockX_ShortNamesInWidePanel_PullsBlockInBesideTheNames()
        {
            // 620px of dead gutter between a 300px-wide name column and a
            // block pinned at 600 - the whole point of the finding.
            int x = PlanRelayoutMath.RightBlockX(pinnedX: 600, widestNameEnd: 300);

            Assert.Equal(300 + PlanRelayoutMath.TableGutterBreathingRoom, x);
        }

        [Fact]
        public void RightBlockX_NamesWiderThanTheGutter_NeverPushesPastThePinnedX()
        {
            // A name long enough to reach the block already: the block must
            // not move RIGHT (it would leave the panel), so this degrades
            // to exactly the pre-fix layout.
            Assert.Equal(600, PlanRelayoutMath.RightBlockX(pinnedX: 600, widestNameEnd: 590));
            Assert.Equal(600, PlanRelayoutMath.RightBlockX(pinnedX: 600, widestNameEnd: 5000));
        }

        [Fact]
        public void RightBlockX_VeryShortNames_ClampsToTheMinimum()
        {
            int x = PlanRelayoutMath.RightBlockX(pinnedX: 900, widestNameEnd: 40);

            Assert.Equal(PlanRelayoutMath.TableRightBlockMinX, x);
        }

        [Fact]
        public void RightBlockX_NarrowPanelBelowTheMinimum_PinnedStillWins()
        {
            // Panel so narrow the pinned position is already left of the
            // floor: the floor must not push the block back out over the
            // panel edge.
            int pinned = PlanRelayoutMath.TableRightBlockMinX - 60;

            Assert.Equal(pinned, PlanRelayoutMath.RightBlockX(pinned, widestNameEnd: 40));
        }

        [Fact]
        public void RightBlockX_PulledInBlock_LeavesTheWidestNameItsFullWidth()
        {
            // The invariant the breathing room exists for: after the pull,
            // the ellipsis budget NameMaxWidthBeforeColumn hands the widest
            // row still covers that row's whole untruncated name, at every
            // per-table gap in the codebase.
            const int nameX = 50;
            const int nameWidth = 220;
            int widestNameEnd = nameX + nameWidth;

            foreach (int gap in new[] { 8, 12, 14 })
            {
                int blockX = PlanRelayoutMath.RightBlockX(pinnedX: 900, widestNameEnd: widestNameEnd);
                int budget = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                    columnRightXBeforeGap: blockX, trailingColumnWidth: 0, gapBeforeColumn: gap, nameX: nameX);

                Assert.True(budget >= nameWidth, $"gap {gap} truncated the name it was measured from");
            }
        }

        // --- RightBlockRightEdge (the flat plan tables' shared anchor) ---

        [Fact]
        public void RightBlockRightEdge_NothingMeasured_IsThePinnedPanelEdge()
        {
            // What every table built before the gutter fix, and what a
            // section with no right-hand column at all still builds: the
            // block's right edge one margin in from the panel edge.
            Assert.Equal(
                1400 - PlanRelayoutMath.TableRightMargin,
                PlanRelayoutMath.RightBlockRightEdge(panelWidth: 1400, blockWidth: 60, widestNameEnd: 0));
        }

        [Fact]
        public void RightBlockRightEdge_ShortNamesInWidePanel_PullsTheWholeBlockIn()
        {
            const int blockWidth = 60;
            int widestNameEnd = 500;

            int rightEdge = PlanRelayoutMath.RightBlockRightEdge(1400, blockWidth, widestNameEnd);

            // The block keeps its own width; only where it starts moved.
            Assert.Equal(
                widestNameEnd + PlanRelayoutMath.TableGutterBreathingRoom + blockWidth, rightEdge);

            // What the row divider and the header band are bounded to: past
            // the last column, and well inside the panel - the two together
            // are what stop the closed gutter from being re-advertised by
            // full-width chrome.
            int chromeWidth = rightEdge + PlanRelayoutMath.TableRightMargin;
            Assert.True(chromeWidth > rightEdge);
            Assert.True(chromeWidth < 1400);
        }

        [Fact]
        public void RightBlockRightEdge_NarrowPanel_NeverOverrunsThePinnedEdge()
        {
            // The degenerate direction that matters: long names in a small
            // window must not push the numbers off the panel. 930 is now
            // BELOW the enforced window minimum (Module.MinWindowWidth,
            // 1436) - kept deliberately narrow, since the invariant has to
            // hold at any panel width the arithmetic can be handed.
            int pinned = 930 - PlanRelayoutMath.TableRightMargin - 60;

            Assert.Equal(
                pinned + 60,
                PlanRelayoutMath.RightBlockRightEdge(panelWidth: 930, blockWidth: 60, widestNameEnd: 5000));
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
        public void ComputeTreeColumnEdges_ShortNamesInWidePanel_PullsPillAndCostInTogether()
        {
            int panelWidth = 1200;
            int nameX = 58;
            int widestNameEnd = nameX + 200;

            var pinned = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, nameX, qtyPrefixWidth: 0,
                pillColumnWidth: 240, costColumnWidth: 150, rightMargin: 8);
            var pulled = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, nameX, qtyPrefixWidth: 0,
                pillColumnWidth: 240, costColumnWidth: 150, rightMargin: 8, widestNameEnd: widestNameEnd);

            Assert.Equal(widestNameEnd + PlanRelayoutMath.TableGutterBreathingRoom, pulled.PillColX);
            Assert.True(pulled.PillColX < pinned.PillColX);

            // Moved as one block: the cost column keeps its exact offset
            // from the pill column, so the pill budget is untouched.
            Assert.Equal(
                pinned.CostRightEdge - pinned.PillColX,
                pulled.CostRightEdge - pulled.PillColX);
        }

        [Fact]
        public void ComputeTreeColumnEdges_LongNames_IdenticalToThePinnedLayout()
        {
            int panelWidth = 900;
            int nameX = 58;

            var pinned = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, nameX, qtyPrefixWidth: 12,
                pillColumnWidth: 240, costColumnWidth: 150, rightMargin: 8);
            var pulled = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, nameX, qtyPrefixWidth: 12,
                pillColumnWidth: 240, costColumnWidth: 150, rightMargin: 8, widestNameEnd: 880);

            Assert.Equal(pinned.PillColX, pulled.PillColX);
            Assert.Equal(pinned.CostRightEdge, pulled.CostRightEdge);
            Assert.Equal(pinned.NameMaxWidth, pulled.NameMaxWidth);
        }

        [Fact]
        public void ComputeTreeColumnEdges_PulledInBlock_WidestRowKeepsItsFullNameWidth()
        {
            // The tree's own instance of the "closing the gutter never
            // ellipsizes" invariant, including the qty prefix that shares
            // the name column with the name.
            int nameX = 58;
            int qtyPrefixWidth = 26;
            int nameWidth = 180;
            int widestNameEnd = nameX + qtyPrefixWidth + nameWidth;

            var edges = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth: 1400, nameX: nameX, qtyPrefixWidth: qtyPrefixWidth,
                pillColumnWidth: 240, costColumnWidth: 150, rightMargin: 8, widestNameEnd: widestNameEnd);

            Assert.True(edges.NameMaxWidth >= nameWidth);
        }

        // The measurement the 1472px window minimum and the 256px pill
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
        // 175 at Font16, from 165 at Font14: only the three digit runs in
        // the column scale with the font (a six-digit gold plus two
        // two-digit units, 90px at the measured Font14 digit advance of 9,
        // 100px at Font16's 10), while the 75px of coin-icon and gap chrome
        // around them is fixed pixels. Digit ADVANCES, which run ~4px over
        // what MeasureString's inked rect gives for the same run (161 at
        // Font14, 171 at Font16) - conservative in the safe direction, as a
        // wider cost column leaves the name column less room, not more.
        private const int DeepestPlanCostColumnWidth = 175;

        // widestNameEnd 0 is the PINNED layout - the fallback a tree with no
        // scanned rows gets. Pass the scanned end to exercise what
        // TreeSectionController actually hands this function on every real
        // (non-empty) tree, where the pill/cost block is pulled LEFT to the
        // widest name plus the breathing room.
        private static PlanRelayoutMath.TreeColumnEdges DeepestRowEdges(
            int panelWidth, int depth, int widestNameEnd = 0)
        {
            return PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, TreeNameX(depth), DeepestRowQtyPrefixWidth,
                PlanRelayoutMath.TreePillColumnWidth, DeepestPlanCostColumnWidth,
                rightMargin: 8, widestNameEnd: widestNameEnd);
        }

        // What the tree scans for the deepest chain: the deepest row is also
        // the widest, so its own name end is the tree's widestNameEnd.
        private static int DeepestRowNameEnd(int depth)
        {
            return TreeNameX(depth) + DeepestRowQtyPrefixWidth + DeepestRowNameWidth;
        }

        [Fact]
        public void ComputeTreeColumnEdges_DeepestRowInTheGame_KeepsTheDesignedGutterAtTheWindowMinimum()
        {
            var edges = DeepestRowEdges(PlanPanelWidthAtWindowMinimum, depth: 23);

            Assert.Equal(
                DeepestRowNameWidth + PlanRelayoutMath.TableGutterBreathingRoom,
                edges.NameMaxWidth);
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
        public void ComputeTreeColumnEdges_DeepestRowInTheScannedLayout_FitsAtTheWindowMinimum()
        {
            // The layout the renderer actually produces: TreeSectionController
            // always passes the scanned _widestNameEnd, which pulls the
            // pill/cost block left of the pinned position, so the pinned
            // cases above are NOT the configuration a user sees. The
            // guarantee has to hold here too - at both the deepest real row
            // and the synthesised vendor-leaf level below it.
            var deepest = DeepestRowEdges(
                PlanPanelWidthAtWindowMinimum, depth: 23, widestNameEnd: DeepestRowNameEnd(23));
            var vendorLeaf = DeepestRowEdges(
                PlanPanelWidthAtWindowMinimum, depth: 24, widestNameEnd: DeepestRowNameEnd(24));

            Assert.True(
                deepest.NameMaxWidth >= DeepestRowNameWidth,
                $"depth 23 name budget {deepest.NameMaxWidth} < {DeepestRowNameWidth}");
            Assert.True(
                vendorLeaf.NameMaxWidth >= DeepestRowNameWidth,
                $"depth 24 name budget {vendorLeaf.NameMaxWidth} < {DeepestRowNameWidth}");
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
