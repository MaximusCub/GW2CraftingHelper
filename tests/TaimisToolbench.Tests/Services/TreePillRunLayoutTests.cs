using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The reserved slot the Recipe Tree's IGNORE toggle is drawn into,
    /// and the DecisionPillPlanner invariants the renderer anchors it on.
    /// The behaviour under test is what the reported bug needed: the slot
    /// is the same rectangle before and after the click that flips the
    /// pill, whatever happens to the pills beside it, so repeated clicks
    /// keep landing on the toggle instead of falling through to the row
    /// (which expands and collapses the node).
    /// </summary>
    public class TreePillRunLayoutTests
    {
        // Stand-in for the Caption face: one pixel per character, the same
        // shape TreeCostColumnMathTests uses. Only the ORDER of the two
        // toggle texts matters here, and "IGNORED" is the longer in any
        // proportional font.
        private static int MeasureByLength(string text)
        {
            return text.Length;
        }

        private const int Padding = 12;
        private const int Gap = 6;

        private const int ColumnStart = 700;
        private const int ColumnRightEdge = ColumnStart + 252;

        private static int ReservedWidth()
        {
            return TreePillRunLayout.ReservedSlotWidth(
                MeasureByLength(DecisionPillPlanner.IgnorePillText),
                MeasureByLength(DecisionPillPlanner.IgnoredPillText),
                Padding);
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

        /// <summary>The same item after its IGNORE pill was clicked: the
        /// solver returns it as owned, with no sources at all.</summary>
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
        /// The renderer anchors the toggle only when it is the LAST spec,
        /// and reserves one slot for both of its texts. Both halves of
        /// that are DecisionPillPlanner's contract, and both states of the
        /// toggle have to keep it or the anchoring silently stops
        /// applying.
        /// </summary>
        [Fact]
        public void PlannerPutsTheToggleLast_InBothStates()
        {
            var live = DecisionPillPlanner.BuildPillSpecs(TwoSourceNode());
            var ignored = DecisionPillPlanner.BuildPillSpecs(IgnoredNode());

            Assert.Equal(PillKind.Ignore, live[live.Count - 1].Kind);
            Assert.Equal(DecisionPillPlanner.IgnorePillText, live[live.Count - 1].Text);
            Assert.Equal(PillKind.Ignore, ignored[ignored.Count - 1].Kind);
            Assert.Equal(DecisionPillPlanner.IgnoredPillText, ignored[ignored.Count - 1].Text);
        }

        /// <summary>
        /// Where the toggle WOULD sit if it still flowed after the pills
        /// beside it - the arithmetic the renderer used before the slot
        /// was reserved, kept here as the counter-example the anchored x
        /// is measured against.
        /// </summary>
        private static int FlowedToggleX(System.Collections.Generic.List<PillSpec> specs)
        {
            int x = ColumnStart;
            for (int i = 0; i < specs.Count - 1; i++)
            {
                x += MeasureByLength(specs[i].Text) + Padding + Gap;
            }

            return x;
        }

        /// <summary>
        /// The bug in one test: the pills BESIDE the toggle change
        /// wholesale across the click - CRAFT/TP/IGNORE become
        /// HAVE/IGNORED - which moves a flowed toggle out from under the
        /// cursor. The anchored slot is the same rectangle either side,
        /// because its x is derived from the column's right edge rather
        /// than from the run to its left.
        /// </summary>
        [Fact]
        public void ToggleKeepsItsRectangle_AcrossTheClickThatRebuildsTheRow()
        {
            var live = DecisionPillPlanner.BuildPillSpecs(TwoSourceNode());
            var ignored = DecisionPillPlanner.BuildPillSpecs(IgnoredNode());
            Assert.NotEqual(live.Count, ignored.Count);
            Assert.NotEqual(FlowedToggleX(live), FlowedToggleX(ignored));

            int reserved = ReservedWidth();
            Assert.Equal(
                TreePillRunLayout.AnchoredSlotX(ColumnRightEdge, reserved),
                ColumnRightEdge - reserved);

            // And the slot holds either text, so the pill does not resize
            // under the cursor either.
            Assert.True(reserved >= MeasureByLength(DecisionPillPlanner.IgnorePillText) + Padding);
            Assert.True(reserved >= MeasureByLength(DecisionPillPlanner.IgnoredPillText) + Padding);
        }

        [Fact]
        public void ReservedSlot_TakesTheWiderText_WhicheverOrderItIsAsked()
        {
            Assert.Equal(
                ReservedWidth(),
                TreePillRunLayout.ReservedSlotWidth(
                    MeasureByLength(DecisionPillPlanner.IgnoredPillText),
                    MeasureByLength(DecisionPillPlanner.IgnorePillText),
                    Padding));
        }

        [Fact]
        public void LeadingRun_StopsAGapShortOfTheReservedSlot()
        {
            int limit = TreePillRunLayout.LeadingLimitX(ColumnRightEdge, ReservedWidth(), Gap);

            Assert.Equal(TreePillRunLayout.AnchoredSlotX(ColumnRightEdge, ReservedWidth()) - Gap, limit);
        }

        [Fact]
        public void LeadingRun_KeepsTheWholeColumnWhenNoSlotIsReserved()
        {
            Assert.Equal(ColumnRightEdge, TreePillRunLayout.LeadingLimitX(ColumnRightEdge, 0, Gap));
        }

        /// <summary>
        /// A leading run fitted against the reduced limit cannot reach the
        /// reserved slot - the two never draw on the same pixels, which is
        /// what stops a click landing on whichever control Blish
        /// hit-tests last.
        /// </summary>
        [Fact]
        public void FittedLeadingRun_NeverReachesTheReservedSlot()
        {
            int reserved = ReservedWidth();
            int limit = TreePillRunLayout.LeadingLimitX(ColumnRightEdge, reserved, Gap);
            var widths = new[] { 60, 40, 52, 130 };

            int visible = PlanRelayoutMath.ComputeVisiblePillCount(widths, Gap, ColumnStart, limit);

            int runEnd = ColumnStart;
            for (int i = 0; i < visible; i++)
            {
                runEnd += widths[i] + Gap;
            }

            Assert.True(visible > 0);
            Assert.True(runEnd - Gap <= TreePillRunLayout.AnchoredSlotX(ColumnRightEdge, reserved));
        }
    }
}
