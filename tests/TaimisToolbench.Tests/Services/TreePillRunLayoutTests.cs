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
    /// <para>
    /// The renderer now draws one MARK in both states rather than the two
    /// words below, so it feeds ReservedSlotWidth one measurement twice
    /// and the rectangle is identical by construction. These tests keep
    /// driving it with the two words: the harder input is the one worth
    /// pinning, and the words are still what the toggle's tooltip is named
    /// from.
    /// </para>
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

        // --- The "Source" header (HeaderX): centred over the pill INK,
        // never over the 256px reserve behind it.
        private const int ColumnWidth = PlanRelayoutMath.TreePillColumnWidth;

        /// <summary>The header word at the stand-in face, so it stays in
        /// proportion to the badge runs it is compared against.</summary>
        private static readonly int SourceHeaderWidth = MeasureByLength("Source");

        // The room the tree actually gives its "Source" header: the name
        // column stops PlanRelayoutMath.TreeNameGap short of the pill rule,
        // and the Cost column's coin runs start well to its right. The pill
        // column's own fixed reserve is not a bound at all - clamping into
        // it is the defect these tests pin.
        private const int CostRightEdge = ColumnRightEdge + 200;
        private const int CostInk = 120;

        private static JustifiedColumnTracks.HeaderRoom SourceRoom(int ink)
        {
            PlanRelayoutMath.ComputeTreeHeaderRooms(
                new PlanRelayoutMath.TreeColumnEdges(ColumnStart, CostRightEdge, 0),
                ink, CostInk, out var source, out _);
            return source;
        }

        /// <summary>Right edge the flowed run reaches, from the specs the
        /// real planner emitted - the same left-packed walk the renderer
        /// does, with the one-pixel-per-character face.</summary>
        private static int FlowedRunRightEdge(System.Collections.Generic.List<PillSpec> specs)
        {
            int x = ColumnStart;
            for (int i = 0; i < specs.Count; i++)
            {
                x += MeasureByLength(specs[i].Text) + Padding + Gap;
            }

            return x - Gap;
        }

        private static CraftingTreeNode TwoSourceRoot()
        {
            var node = TwoSourceNode();
            node.IsPlanRoot = true;
            return node;
        }

        /// <summary>
        /// The reported defect: a freshly generated tree shows only its
        /// plan roots, which get no IGNORE toggle, so their badges occupy
        /// a fraction of the column and the header floats right of every
        /// one of them. Centring over the ink puts it back.
        /// </summary>
        [Fact]
        public void SourceHeader_CentresOverTheBadgeRun_NotOverTheReserve()
        {
            var specs = DecisionPillPlanner.BuildPillSpecs(TwoSourceRoot());
            Assert.DoesNotContain(specs, spec => spec.Kind == PillKind.Ignore);

            int ink = FlowedRunRightEdge(specs) - ColumnStart;
            Assert.True(ink < ColumnWidth / 2);

            int headerX = TreePillRunLayout.HeaderX(
                ColumnStart, ink, SourceHeaderWidth, SourceRoom(ink));

            Assert.Equal(ColumnStart + ((ink - SourceHeaderWidth) / 2), headerX);
            Assert.True(
                headerX < JustifiedColumnTracks.CenteredInBand(
                    ColumnStart, ColumnWidth, SourceHeaderWidth));
        }

        /// <summary>
        /// A row that reserves the anchored toggle draws ink all the way
        /// to the column's right edge, so there the band IS the content
        /// and the two rules agree - the header does not lurch left the
        /// moment such a row is built.
        /// </summary>
        [Fact]
        public void SourceHeader_MatchesTheBand_OnceARowReachesTheColumnsRightEdge()
        {
            var specs = DecisionPillPlanner.BuildPillSpecs(TwoSourceNode());
            Assert.Equal(PillKind.Ignore, specs[specs.Count - 1].Kind);

            // The anchored slot ends on the column's right edge whatever
            // the run to its left does, so the row's ink is the column.
            int ink = TreePillRunLayout.AnchoredSlotX(ColumnWidth, ReservedWidth()) + ReservedWidth();

            Assert.Equal(
                JustifiedColumnTracks.CenteredInBand(ColumnStart, ColumnWidth, SourceHeaderWidth),
                TreePillRunLayout.HeaderX(ColumnStart, ink, SourceHeaderWidth, SourceRoom(ink)));
        }

        /// <summary>
        /// No pills built yet (the header is created before the first row)
        /// and no pills at all are the same case: the header centres on the
        /// rule its badges would have used and the item name beside it is
        /// what stops it going further, so it lands one pixel proud of the
        /// column rather than half a word into the names.
        /// </summary>
        [Fact]
        public void SourceHeader_WithNoInk_StopsAtTheNameColumnsBound()
        {
            var room = SourceRoom(0);

            Assert.Equal(ColumnStart - 1, room.Left);
            Assert.Equal(
                room.Left,
                TreePillRunLayout.HeaderX(ColumnStart, 0, SourceHeaderWidth, room));
            Assert.Equal(
                room.Left,
                TreePillRunLayout.HeaderX(ColumnStart, -5, SourceHeaderWidth, SourceRoom(-5)));
        }

        /// <summary>
        /// The high-water rule the view applies (TreeSectionController.
        /// NoteSourceHeaderInk) is monotone, so the header only ever moves
        /// one way as rows are built - never back and forth under a
        /// collapse.
        /// </summary>
        [Fact]
        public void SourceHeader_MovesRightAsTheInkWidens_AndNeverPastTheColumn()
        {
            int previous = TreePillRunLayout.HeaderX(
                ColumnStart, 0, SourceHeaderWidth, SourceRoom(0));
            for (int ink = 1; ink <= ColumnWidth; ink++)
            {
                var room = SourceRoom(ink);
                int x = TreePillRunLayout.HeaderX(ColumnStart, ink, SourceHeaderWidth, room);
                Assert.True(x >= previous, $"ink {ink}: {x} < {previous}");
                Assert.True(x >= room.Left, $"ink {ink}: {x} left of {room.Left}");
                Assert.True(
                    x + SourceHeaderWidth <= room.Right,
                    $"ink {ink}: {x + SourceHeaderWidth} past {room.Right}");
                previous = x;
            }
        }
    }
}
