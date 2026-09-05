using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The guard that stops a recipe-tree row answering a click its own
    /// IGNORE toggle is about to answer, and the behaviour asked for in
    /// game: N clicks on the toggle with a cursor that never moves
    /// produce N toggles and no expand/collapse.
    /// <para>
    /// The row was rebuilt on every one of those clicks and the guard used
    /// to read Blish's hover flag, which is only recomputed when the mouse
    /// MOVES - so after the first click it answered for a control the
    /// cursor was not on. Everything below is the production placement
    /// arithmetic the renderer itself calls, driven the same way it drives
    /// it, so the answer here is the answer on screen.
    /// </para>
    /// </summary>
    public class TreeRowPillHitTestTests
    {
        // One pixel per character, the stand-in face TreePillRunLayoutTests
        // and TreeCostColumnMathTests already use: what matters is that the
        // two toggle texts differ in width and that the pills beside the
        // toggle change wholesale across the click.
        private static int MeasureByLength(string text)
        {
            return text.Length;
        }

        private const int Padding = 12;
        private const int Gap = 6;
        private const int TightPadding = 6;

        private const int ColumnStart = 700;
        private const int ColumnRightEdge = ColumnStart + 252;

        // The renderer's own row-local pill anchors. Only their
        // consistency matters here - the boxes and the parked cursor are
        // derived from the same two numbers.
        private const int PillY = 14;
        private const int PillHeight = 24;

        /// <summary>
        /// A real item node with two sources, which is what the row holds
        /// before its IGNORE toggle is clicked.
        /// </summary>
        private static CraftingTreeNode LiveNode()
        {
            return new CraftingTreeNode
            {
                NodeId = 7,
                ItemId = 19721,
                Quantity = 5,
                Decision = CraftingDecision.BuyFromTp,
                CanCraft = true,
                CanBuyTp = true,
                OwnedQuantityUsed = 3,
            };
        }

        /// <summary>The same item after the click: the solver returns it
        /// owned, with no sources and a different pill set.</summary>
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
        /// One row's pill rectangles, built exactly as
        /// TreeSectionController.RenderDecisionPills builds them: the
        /// planner decides the specs, the anchored slot takes the column's
        /// right edge, and the leading run is fitted against what the slot
        /// leaves.
        /// </summary>
        private static List<TreeRowPillHitTest.PillBox> RowPills(bool ignored)
        {
            var specs = DecisionPillPlanner.BuildPillSpecs(ignored ? IgnoredNode() : LiveNode());

            int anchoredIndex = specs.Count > 0 && specs[specs.Count - 1].Kind == PillKind.Ignore
                ? specs.Count - 1
                : -1;
            int anchoredWidth = anchoredIndex >= 0
                ? TreePillRunLayout.ReservedSlotWidth(
                    MeasureByLength(DecisionPillPlanner.IgnorePillText),
                    MeasureByLength(DecisionPillPlanner.IgnoredPillText),
                    Padding)
                : 0;
            int leadingCount = anchoredIndex >= 0 ? anchoredIndex : specs.Count;

            var widths = new List<int>(leadingCount);
            for (int i = 0; i < leadingCount; i++)
            {
                widths.Add(MeasureByLength(specs[i].Text) + Padding);
            }

            var fit = PlanRelayoutMath.ComputePillFit(
                widths, Padding - TightPadding, Gap, ColumnStart,
                TreePillRunLayout.LeadingLimitX(ColumnRightEdge, anchoredWidth, Gap),
                hidden => MeasureByLength("+" + hidden) + TightPadding);

            var boxes = new List<TreeRowPillHitTest.PillBox>();
            int x = ColumnStart;
            for (int i = 0; i < fit.VisibleCount; i++)
            {
                int width = PlanRelayoutMath.ReducedWidth(widths[i], fit.WidthReduction);
                boxes.Add(new TreeRowPillHitTest.PillBox(x, PillY, width, PillHeight));
                x += width + Gap;
            }

            if (fit.HiddenCount > 0)
            {
                boxes.Add(new TreeRowPillHitTest.PillBox(x, PillY, fit.OverflowPillWidth, PillHeight));
            }

            if (anchoredIndex >= 0)
            {
                boxes.Add(new TreeRowPillHitTest.PillBox(
                    TreePillRunLayout.AnchoredSlotX(ColumnRightEdge, anchoredWidth),
                    PillY,
                    anchoredWidth,
                    PillHeight));
            }

            return boxes;
        }

        /// <summary>Middle of the anchored toggle, which is where a user
        /// who has just clicked it leaves the cursor.</summary>
        private static void ParkOnToggle(out int x, out int y)
        {
            int anchoredWidth = TreePillRunLayout.ReservedSlotWidth(
                MeasureByLength(DecisionPillPlanner.IgnorePillText),
                MeasureByLength(DecisionPillPlanner.IgnoredPillText),
                Padding);
            x = TreePillRunLayout.AnchoredSlotX(ColumnRightEdge, anchoredWidth) + (anchoredWidth / 2);
            y = PillY + (PillHeight / 2);
        }

        /// <summary>
        /// THE reported bug, as behaviour rather than as a mechanism.
        /// Every iteration rebuilds the row from the state the previous
        /// click left it in - which is what the re-solve does - and asks
        /// the guard the row's click handler asks. A guard that answered
        /// from anything carried across the rebuild would start reporting
        /// "no pill here" and the row would expand instead.
        /// </summary>
        [Fact]
        public void FiveClicksOnTheToggle_WithAStationaryCursor_ProduceFiveToggles()
        {
            ParkOnToggle(out int cursorX, out int cursorY);

            bool ignored = false;
            int toggles = 0;
            int rowToggles = 0;

            for (int click = 0; click < 5; click++)
            {
                var pills = RowPills(ignored);
                if (TreeRowPillHitTest.AnyCovers(pills, cursorX, cursorY))
                {
                    toggles++;
                    ignored = !ignored;
                }
                else
                {
                    rowToggles++;
                }
            }

            Assert.Equal(5, toggles);
            Assert.Equal(0, rowToggles);
            Assert.True(ignored);
        }

        /// <summary>
        /// The guard is a pure function of this row's geometry, so the two
        /// states answer identically for the same point - there is no
        /// "which control did Blish last mark" left in it to go stale.
        /// </summary>
        [Fact]
        public void TheGuardAnswersTheSame_InBothStatesOfTheToggle()
        {
            ParkOnToggle(out int cursorX, out int cursorY);

            Assert.True(TreeRowPillHitTest.AnyCovers(RowPills(false), cursorX, cursorY));
            Assert.True(TreeRowPillHitTest.AnyCovers(RowPills(true), cursorX, cursorY));
        }

        /// <summary>
        /// A click in the row's name column is the row's to answer, in
        /// either state - the guard must not swallow the expand/collapse
        /// it exists to protect.
        /// </summary>
        [Fact]
        public void APointOutsideEveryPill_IsTheRowsToAnswer()
        {
            int nameColumnX = ColumnStart - 40;
            int y = PillY + (PillHeight / 2);

            Assert.False(TreeRowPillHitTest.AnyCovers(RowPills(false), nameColumnX, y));
            Assert.False(TreeRowPillHitTest.AnyCovers(RowPills(true), nameColumnX, y));
        }

        [Fact]
        public void TheGapBetweenTwoPills_BelongsToNeither()
        {
            var pills = RowPills(false);
            var first = pills[0];
            int y = PillY + 1;

            Assert.True(TreeRowPillHitTest.Covers(first, first.X + first.Width - 1, y));
            Assert.False(TreeRowPillHitTest.Covers(first, first.X + first.Width, y));
            Assert.False(TreeRowPillHitTest.AnyCovers(pills, first.X + first.Width + 1, y));
        }

        [Fact]
        public void ABoxCoversItsOwnTopLeftAndNotItsBottomRight()
        {
            var box = new TreeRowPillHitTest.PillBox(10, 20, 30, 40);

            Assert.True(TreeRowPillHitTest.Covers(box, 10, 20));
            Assert.True(TreeRowPillHitTest.Covers(box, 39, 59));
            Assert.False(TreeRowPillHitTest.Covers(box, 40, 59));
            Assert.False(TreeRowPillHitTest.Covers(box, 39, 60));
            Assert.False(TreeRowPillHitTest.Covers(box, 9, 20));
            Assert.False(TreeRowPillHitTest.Covers(box, 10, 19));
        }

        [Fact]
        public void ARowWithNoPillsCoversNothing()
        {
            Assert.False(TreeRowPillHitTest.AnyCovers(null, 0, 0));
            Assert.False(TreeRowPillHitTest.AnyCovers(new List<TreeRowPillHitTest.PillBox>(), 0, 0));
            Assert.False(
                TreeRowPillHitTest.Covers(new TreeRowPillHitTest.PillBox(10, 10, 0, 24), 10, 10));
        }
    }
}
