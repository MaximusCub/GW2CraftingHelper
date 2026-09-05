using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Where a recipe-tree row seats its IGNORE key. The defect these pin:
    /// pinned to the decision-pill column's right edge, the key drew 8px
    /// from the pills of a row with a wide run and 183px from the nearest
    /// cost figure, so it read as the end of the pill run rather than as
    /// the row action it is.
    /// <para>
    /// Everything below drives the production arithmetic the renderer
    /// drives - the real planner, the real fit, the real placement - so
    /// the answer here is the answer on screen.
    /// </para>
    /// </summary>
    public class TreeIgnoreKeyPlacementTests
    {
        // One pixel per character, the stand-in face TreePillRunLayoutTests
        // and TreeRowPillHitTestTests already drive these same functions
        // with.
        private static int MeasureByLength(string text)
        {
            return text.Length;
        }

        private const int Padding = 12;
        private const int TightPadding = 6;
        private const int Gap = 6;

        private const int ColumnStart = 700;
        private const int ColumnWidth = PlanRelayoutMath.TreePillColumnWidth;

        // The renderer's own right edge for the column: its reserve less
        // the clearance it keeps from the cost column.
        private const int ColumnRightEdge =
            ColumnStart + ColumnWidth - TreePillColumnMath.TrailingClearance;

        private const int KeyWidth = GlyphButtonMetrics.RowActionWidth;

        private static int PinnedX()
        {
            return TreePillRunLayout.AnchoredSlotX(ColumnRightEdge, KeyWidth);
        }

        private static CraftingTreeNode TwoSourceNode()
        {
            return new CraftingTreeNode
            {
                NodeId = 7,
                ItemId = 19721,
                Quantity = 5,
                Decision = CraftingDecision.BuyFromTp,
                CanCraft = true,
                CanBuyTp = true,
            };
        }

        /// <summary>The same item after the key was clicked: the solver
        /// returns it owned, with no sources at all.</summary>
        private static CraftingTreeNode IgnoredNode()
        {
            return new CraftingTreeNode
            {
                NodeId = 7,
                ItemId = 19721,
                Quantity = 5,
                Decision = CraftingDecision.Have,
                IsIgnored = true,
            };
        }

        /// <summary>
        /// Right edge of one row's flowed run, fitted exactly as
        /// TreeSectionController.RenderDecisionPills fits it: the leading
        /// pills against the budget the PINNED key leaves, then the "+N"
        /// chip when any were dropped.
        /// </summary>
        private static int RunRightEdge(CraftingTreeNode node, int columnRightEdge)
        {
            var specs = DecisionPillPlanner.BuildPillSpecs(node);
            bool anchored = specs.Count > 0 && specs[specs.Count - 1].Kind == PillKind.Ignore;
            int keyWidth = anchored ? KeyWidth : 0;
            int leadingCount = anchored ? specs.Count - 1 : specs.Count;

            var widths = new List<int>(leadingCount);
            for (int i = 0; i < leadingCount; i++)
            {
                widths.Add(MeasureByLength(specs[i].Text) + Padding);
            }

            var fit = PlanRelayoutMath.ComputePillFit(
                widths, Padding - TightPadding, Gap, ColumnStart,
                TreePillRunLayout.LeadingLimitX(columnRightEdge, keyWidth, Gap),
                hidden => MeasureByLength("+" + hidden) + TightPadding);

            int x = ColumnStart;
            for (int i = 0; i < fit.VisibleCount; i++)
            {
                x += PlanRelayoutMath.ReducedWidth(widths[i], fit.WidthReduction) + Gap;
            }

            if (fit.HiddenCount > 0)
            {
                return x + fit.OverflowPillWidth;
            }

            return fit.VisibleCount > 0 ? x - Gap : ColumnStart;
        }

        /// <summary>
        /// The fix in one test: the key sits midway between the pills it
        /// belongs to and the cost figures beside them, instead of against
        /// the pills with the whole band on its other side.
        /// </summary>
        [Fact]
        public void Key_CentresInTheBandTheRunAndTheCostInkLeave()
        {
            int runRightEdge = RunRightEdge(TwoSourceNode(), ColumnRightEdge);
            int costInkX = ColumnRightEdge + 200;

            int x = TreeIgnoreKeyPlacement.SlotX(
                ColumnRightEdge, costInkX, KeyWidth, Gap, runRightEdge);

            int leftClearance = x - runRightEdge;
            int rightClearance = costInkX - (x + KeyWidth);

            Assert.True(leftClearance > Gap, $"key at {x} is against a run ending at {runRightEdge}");
            Assert.InRange(leftClearance - rightClearance, -1, 1);
        }

        /// <summary>
        /// The band belongs to the row: a short run seats the key further
        /// left than a long one does, because its midpoint is further
        /// left. The column of keys is not straight, and that is the
        /// point of placing them per row.
        /// </summary>
        [Fact]
        public void Key_TracksTheRunBesideIt_RowByRow()
        {
            int costInkX = ColumnRightEdge + 200;

            int longRun = TreeIgnoreKeyPlacement.SlotX(
                ColumnRightEdge, costInkX, KeyWidth, Gap,
                RunRightEdge(TwoSourceNode(), ColumnRightEdge));
            int shortRun = TreeIgnoreKeyPlacement.SlotX(
                ColumnRightEdge, costInkX, KeyWidth, Gap,
                RunRightEdge(IgnoredNode(), ColumnRightEdge));

            Assert.True(
                shortRun < longRun,
                $"short run seated the key at {shortRun}, long run at {longRun}");
        }

        /// <summary>
        /// Both bounds, over every band a cost column can leave: the key
        /// keeps its gap from the run and the pill column's own clearance
        /// from the cost ink, or it falls back to the pinned x.
        /// </summary>
        [Fact]
        public void Key_ClearsBothNeighbours_AtEveryBandWidth()
        {
            int runRightEdge = RunRightEdge(TwoSourceNode(), ColumnRightEdge);

            for (int costInkX = ColumnStart + ColumnWidth;
                costInkX <= ColumnStart + ColumnWidth + 400;
                costInkX++)
            {
                int x = TreeIgnoreKeyPlacement.SlotX(
                    ColumnRightEdge, costInkX, KeyWidth, Gap, runRightEdge);

                Assert.True(
                    x >= runRightEdge + Gap || x == PinnedX(),
                    $"cost ink {costInkX}: key at {x}, run ends at {runRightEdge}");
                Assert.True(
                    x + KeyWidth <= costInkX - TreePillColumnMath.TrailingClearance
                        || x == PinnedX(),
                    $"cost ink {costInkX}: key ends at {x + KeyWidth}, cost ink at {costInkX}");
            }
        }

        /// <summary>
        /// A run that fills its whole budget - the rows the "+N" chip
        /// belongs to - is the case with the least room left, and the key
        /// still clears it and still clears the cost ink.
        /// </summary>
        [Fact]
        public void Key_KeepsItsGap_FromARunThatFillsItsBudget()
        {
            int budgetEnd = TreePillRunLayout.LeadingLimitX(ColumnRightEdge, KeyWidth, Gap);
            int costInkX = ColumnStart + ColumnWidth + 40;

            int x = TreeIgnoreKeyPlacement.SlotX(
                ColumnRightEdge, costInkX, KeyWidth, Gap, budgetEnd);

            Assert.True(x >= budgetEnd + Gap, $"key at {x}, run ends at {budgetEnd}");
            Assert.True(x + KeyWidth <= costInkX - TreePillColumnMath.TrailingClearance);
        }

        /// <summary>
        /// A cost column whose values reach the pill column leaves nothing
        /// to centre in, and the key stays exactly where it has always
        /// been.
        /// </summary>
        [Fact]
        public void Key_StaysPinned_WhenTheRunAndTheCostInkLeaveNoBand()
        {
            int runRightEdge = TreePillRunLayout.LeadingLimitX(ColumnRightEdge, KeyWidth, Gap);

            Assert.Equal(
                PinnedX(),
                TreeIgnoreKeyPlacement.SlotX(
                    ColumnRightEdge,
                    runRightEdge + Gap + KeyWidth + TreePillColumnMath.TrailingClearance - 1,
                    KeyWidth,
                    Gap,
                    runRightEdge));
        }

        // The tree's fixed cost-column floor (TreeSectionController.
        // TreeCostColumnWidth) and its right margin, which the module's
        // minimum window is sized around.
        private const int CostColumnFloor = 150;
        private const int RightMargin = 8;

        /// <summary>
        /// The narrowest the module ever renders. The band is whatever the
        /// cost column's reserve leaves above the ink its rows draw, and
        /// the key has to clear both neighbours there too.
        /// </summary>
        [Fact]
        public void Key_ClearsBothNeighbours_AtTheMinimumWindow()
        {
            int panelWidth = WindowSizing.TabPanelWidthFor(WindowSizing.MinWindowWidth);

            // A tree whose rows draw gold and silver but no currency band,
            // so the column reserves more than any single row reaches.
            var widths = new TreeCostColumnMath.CostColumnWidths(24, 16, 16, 0, 78, 78);
            int costColumnWidth = TreeCostColumnMath.WidthAfterClaim(widths, CostColumnFloor, 0);
            var edges = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, 100, 0, ColumnWidth, costColumnWidth, RightMargin);

            int columnRightEdge =
                edges.PillColX + ColumnWidth - TreePillColumnMath.TrailingClearance;
            int costInkX = edges.CostRightEdge - widths.LeftmostInkReach;
            int runRightEdge = RunRightEdge(TwoSourceNode(), columnRightEdge);

            int x = TreeIgnoreKeyPlacement.SlotX(
                columnRightEdge, costInkX, KeyWidth, Gap, runRightEdge);

            Assert.True(x >= runRightEdge + Gap, $"key at {x} crowds a run ending at {runRightEdge}");
            Assert.True(
                x + KeyWidth <= costInkX, $"key ends at {x + KeyWidth}, cost ink at {costInkX}");
        }

        /// <summary>
        /// Why the renderer places the key against the widest run the row
        /// has drawn rather than against the run on screen: clicking the
        /// key swaps the pills beside it, and the two runs seat the key at
        /// two different x, so against the live run the key would move out
        /// from under the cursor that had just clicked it.
        /// </summary>
        [Fact]
        public void KeyPlacedAgainstTheLiveRun_WouldMoveAcrossTheClick()
        {
            int costInkX = ColumnRightEdge + 200;
            int live = RunRightEdge(TwoSourceNode(), ColumnRightEdge);
            int ignored = RunRightEdge(IgnoredNode(), ColumnRightEdge);

            Assert.True(live > ignored);
            Assert.NotEqual(
                TreeIgnoreKeyPlacement.SlotX(ColumnRightEdge, costInkX, KeyWidth, Gap, live),
                TreeIgnoreKeyPlacement.SlotX(ColumnRightEdge, costInkX, KeyWidth, Gap, ignored));

            // The widest run either state produces is the one the renderer
            // keeps, so the click leaves the key on the x the un-ignored
            // row already put it at.
            Assert.Equal(
                TreeIgnoreKeyPlacement.SlotX(ColumnRightEdge, costInkX, KeyWidth, Gap, live),
                TreeIgnoreKeyPlacement.SlotX(
                    ColumnRightEdge, costInkX, KeyWidth, Gap, live > ignored ? live : ignored));
        }
    }
}
