using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The Recipe Tree's decision column: what a row's run of source
    /// markers covers, and where the "Source" header sits over it.
    /// </summary>
    public class TreePillRunLayoutTests
    {
        // Stand-in for the Caption face: one pixel per character, the same
        // shape TreeCostColumnMathTests uses.
        private static int MeasureByLength(string text)
        {
            return text.Length;
        }

        private const int Padding = 12;
        private const int Gap = 6;

        private const int ColumnStart = 700;
        private const int ColumnRightEdge = ColumnStart + 252;

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

        /// <summary>The same item after its ignore button was clicked: the
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
        /// The renderer takes the ignore button out of the run only when
        /// it is the LAST spec, and both states of the button have to keep
        /// that or it silently rejoins the run and the "+N" chip starts
        /// naming the wrong entries.
        /// </summary>
        [Fact]
        public void PlannerPutsTheIgnoreButtonLast_InBothStates()
        {
            var live = DecisionPillPlanner.BuildPillSpecs(TwoSourceNode());
            var ignored = DecisionPillPlanner.BuildPillSpecs(IgnoredNode());

            Assert.Equal(PillKind.Ignore, live[live.Count - 1].Kind);
            Assert.Equal(DecisionPillPlanner.IgnorePillText, live[live.Count - 1].Text);
            Assert.Equal(PillKind.Ignore, ignored[ignored.Count - 1].Kind);
            Assert.Equal(DecisionPillPlanner.IgnoredPillText, ignored[ignored.Count - 1].Text);
        }

        /// <summary>
        /// Everything BESIDE the ignore button changes wholesale across
        /// the click - CRAFT/TP/IGNORE become HAVE/IGNORED - so any x
        /// derived from the run moves out from under the cursor that just
        /// clicked it. The button's column is derived from the panel edge
        /// instead, and holds for both sets and at every window width.
        /// </summary>
        [Fact]
        public void TheIgnoreButtonsColumn_IsUnmovedByTheClickThatRebuildsTheRow()
        {
            var live = DecisionPillPlanner.BuildPillSpecs(TwoSourceNode());
            var ignored = DecisionPillPlanner.BuildPillSpecs(IgnoredNode());
            Assert.NotEqual(live.Count, ignored.Count);
            Assert.NotEqual(FlowedRunRightEdge(live), FlowedRunRightEdge(ignored));

            var narrow = PlanRelayoutMath.ComputeTreeColumnEdges(
                1252, 400, 0, PlanRelayoutMath.TreePillColumnWidth, 150, 8);
            var wide = PlanRelayoutMath.ComputeTreeColumnEdges(
                1794, 400, 0, PlanRelayoutMath.TreePillColumnWidth, 150, 8);

            Assert.Equal(
                narrow.CostRightEdge + PlanRelayoutMath.TreeActionColumnGap, narrow.ActionButtonX);
            Assert.Equal(
                narrow.ActionButtonX - narrow.PillColX, wide.ActionButtonX - wide.PillColX);
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

            int ink = TreePillRunLayout.HeaderInkWidth(
                ColumnStart, FlowedRunRightEdge(specs));
            Assert.True(ink < ColumnWidth / 2);

            int headerX = TreePillRunLayout.HeaderX(
                ColumnStart, ink, SourceHeaderWidth, SourceRoom(ink));

            Assert.Equal(ColumnStart + ((ink - SourceHeaderWidth) / 2), headerX);
            Assert.True(
                headerX < JustifiedColumnTracks.CenteredInBand(
                    ColumnStart, ColumnWidth, SourceHeaderWidth));
        }

        /// <summary>
        /// A row that draws the ignore button reports the run and not the
        /// button: the header names the sources the markers carry, and the
        /// button is a row action in a column of its own. Counting it put
        /// the header over the whole reserve, well right of everything
        /// under it.
        /// </summary>
        [Fact]
        public void SourceHeader_LeavesTheIgnoreButtonOutOfTheInkItCentresOver()
        {
            var specs = DecisionPillPlanner.BuildPillSpecs(TwoSourceNode());
            Assert.Equal(PillKind.Ignore, specs[specs.Count - 1].Kind);

            int runRightEdge = FlowedRunRightEdge(specs.GetRange(0, specs.Count - 1));
            int ink = TreePillRunLayout.HeaderInkWidth(ColumnStart, runRightEdge);

            Assert.True(ink < ColumnWidth / 2);
            Assert.True(
                TreePillRunLayout.HeaderX(ColumnStart, ink, SourceHeaderWidth, SourceRoom(ink))
                    < JustifiedColumnTracks.CenteredInBand(
                        ColumnStart, ColumnWidth, SourceHeaderWidth));
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
