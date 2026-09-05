using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The decision-pill column's width, and the reported defect it
    /// answers: an Obsidian Heavy Breastplate row showed a "+N" chip while
    /// hundreds of pixels sat unused in the name column beside it, because
    /// the column was a flat 256px at every window width.
    /// <para>
    /// The follow-up report on the same plan: even the derived column
    /// chipped the "1x Obsidian Shard" row, because the cap let the pills
    /// claim only HALF the panel's surplus. The cap is now the space
    /// actually available between the two neighbours' minimums (Affordable);
    /// RightClaim says how much came from the cost side.
    /// </para>
    /// <para>
    /// Everything here is the production arithmetic. The pill widths are
    /// the real ones the renderer measures, at the Caption face calibrated
    /// from the module's own recorded measurement (docs/ARCHITECTURE.md:
    /// the CRAFT/TP/VENDOR/IGNORE run is 222px at PillPadding 12 and
    /// PillGap 6, so its four texts sum to 156px over 19 characters).
    /// </para>
    /// </summary>
    public class TreePillColumnMathTests
    {
        private const int Padding = 12;
        private const int TightPadding = 6;
        private const int Gap = 6;

        /// <summary>The pill Caption face at 8.21px per character.</summary>
        private static int Cap(string text)
        {
            return (int)System.Math.Ceiling(text.Length * 8.21);
        }

        /// <summary>The anchored slot the toggle's remove mark sits in -
        /// xadvance 17 in ref/glyphs.fnt, plus the pill's padding.</summary>
        private static int ToggleSlot()
        {
            return TreePillRunLayout.ReservedSlotWidth(17, 17, Padding);
        }

        private static List<int> Run(params string[] texts)
        {
            var widths = new List<int>(texts.Length);
            foreach (var text in texts)
            {
                widths.Add(Cap(text) + Padding);
            }

            return widths;
        }

        private static int MinPanel()
        {
            return WindowSizing.TabPanelWidthFor(WindowSizing.MinWindowWidth);
        }

        // --- RequiredWidth ---
        [Fact]
        public void RequiredWidth_IsTheRunPlusItsGapsPlusTheSlotAndTheClearance()
        {
            var run = Run("CRAFT", "TP");
            int slot = ToggleSlot();

            Assert.Equal(
                run[0] + Gap + run[1] + Gap + slot + TreePillColumnMath.TrailingClearance,
                TreePillColumnMath.RequiredWidth(run, Gap, slot));
        }

        [Fact]
        public void RequiredWidth_ARowWithNoToggle_PaysForNeitherTheSlotNorTheGapBeforeIt()
        {
            var run = Run("CRAFT", "TP");

            Assert.Equal(
                run[0] + Gap + run[1] + TreePillColumnMath.TrailingClearance,
                TreePillColumnMath.RequiredWidth(run, Gap, 0));
        }

        [Fact]
        public void RequiredWidth_AToggleWithNoRunBesideIt_PaysNoLeadingGap()
        {
            Assert.Equal(
                29 + TreePillColumnMath.TrailingClearance,
                TreePillColumnMath.RequiredWidth(new List<int>(), Gap, 29));
        }

        [Fact]
        public void RequiredWidth_NothingToDraw_NeedsNoColumn()
        {
            Assert.Equal(0, TreePillColumnMath.RequiredWidth(null, Gap, 0));
            Assert.Equal(0, TreePillColumnMath.RequiredWidth(new List<int>(), Gap, 0));
        }

        // --- Affordable: the space between the two neighbours' minimums ---
        [Fact]
        public void Affordable_AtTheModulesMinimumWindow_IsExactlyTheFloor()
        {
            Assert.Equal(
                PlanRelayoutMath.TreePillColumnWidth,
                TreePillColumnMath.Affordable(
                    MinPanel(), PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 0));
        }

        [Fact]
        public void Affordable_BelowTheMinimum_StillOnlyTheFloor()
        {
            // The enforced minimum falls back to the client's own width on
            // a narrow game client (WindowSizing.EffectiveMinWindowWidth),
            // so a panel narrower than the nominal minimum is reachable and
            // must not produce a negative allowance.
            Assert.Equal(
                PlanRelayoutMath.TreePillColumnWidth,
                TreePillColumnMath.Affordable(
                    600, PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 0));
        }

        /// <summary>
        /// The correction: the WHOLE surplus past the module's minimum
        /// is the status column's to claim leftward. The name column keeps
        /// the budget it holds at the minimum window - the budgets
        /// docs/research/minimum-window-width.md was derived from - and
        /// that budget is the item side's minimum, not a half share of
        /// every new pixel.
        /// </summary>
        [Fact]
        public void Affordable_TheWholeSurplusPastTheMinimum_IsTheStatusColumnsToClaim()
        {
            int panel = MinPanel() + 400;

            Assert.Equal(
                PlanRelayoutMath.TreePillColumnWidth + 400,
                TreePillColumnMath.Affordable(
                    panel, PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 0));
        }

        /// <summary>
        /// The other direction reported in game: the cost column's
        /// reserve above what its rows actually draw is slack every row
        /// leaves empty, so the status column may claim it rightward. A
        /// negative slack (content already wider than the reserve's floor)
        /// is no room at all.
        /// </summary>
        [Fact]
        public void Affordable_TheCostColumnsSlackAboveItsContent_IsClaimableTowardIt()
        {
            int panel = MinPanel() + 400;

            Assert.Equal(
                PlanRelayoutMath.TreePillColumnWidth + 445,
                TreePillColumnMath.Affordable(
                    panel, PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 45));
            Assert.Equal(
                PlanRelayoutMath.TreePillColumnWidth + 400,
                TreePillColumnMath.Affordable(
                    panel, PlanRelayoutMath.TreePillColumnWidth, MinPanel(), -45));
        }

        /// <summary>
        /// At the minimum window the surplus term is zero - no leftward
        /// growth, so nothing the minimum was derived from moves. The cost
        /// side's slack is the one exception, and it moves nothing the
        /// minimum depends on either: the claim swaps cost reserve for
        /// pill width one-for-one (RightClaim), so PillColX - and with it
        /// every name budget - holds exactly where the flat floor put it.
        /// </summary>
        [Fact]
        public void Affordable_AtTheMinimum_OnlyTheCostColumnsSlackAddsAnything()
        {
            Assert.Equal(
                PlanRelayoutMath.TreePillColumnWidth + 45,
                TreePillColumnMath.Affordable(
                    MinPanel(), PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 45));
        }

        /// <summary>
        /// The property that makes the split safe at every width: widening
        /// the window can never leave the name column narrower than it was
        /// one pixel earlier, with a cost-side slack fixed - the surplus
        /// and the slack reach the pills only while the cap binds, and
        /// every pixel past that goes to the name.
        /// </summary>
        [Fact]
        public void Affordable_WideningTheWindow_NeverNarrowsWhatIsLeftForTheName()
        {
            const int costSlack = 45;
            int previous = int.MinValue;
            for (int panel = MinPanel(); panel < MinPanel() + 600; panel++)
            {
                int left = panel - TreePillColumnMath.Affordable(
                    panel, PlanRelayoutMath.TreePillColumnWidth, MinPanel(), costSlack);
                Assert.True(left >= previous, "the name column lost width as the window grew");
                previous = left;
            }
        }

        // --- RightClaim: how much of the column came from the cost side ---
        [Fact]
        public void RightClaim_WithinTheSurplus_TakesNothingFromTheCostColumn()
        {
            Assert.Equal(0, TreePillColumnMath.RightClaim(256 + 50, 256, 50, 45));
        }

        [Fact]
        public void RightClaim_TheExcessOverTheSurplus_ComesFromTheCostColumn()
        {
            Assert.Equal(20, TreePillColumnMath.RightClaim(256 + 50 + 20, 256, 50, 45));
        }

        /// <summary>
        /// The claim stops at the slack: the cost column keeps what its
        /// rows actually draw (TreeCostColumnMath.TotalWidth), which is
        /// the whole point of "within each side's minimum".
        /// </summary>
        [Fact]
        public void RightClaim_NeverBeyondTheSlack_TheCostColumnKeepsItsContent()
        {
            Assert.Equal(45, TreePillColumnMath.RightClaim(256 + 50 + 90, 256, 50, 45));
        }

        [Fact]
        public void RightClaim_DegenerateInputs_ClaimNothingOrStopAtTheSlack()
        {
            // A column at or below the floor claims nothing, whatever the
            // surplus says; a negative surplus is no surplus; a negative
            // slack is no room toward the cost column.
            Assert.Equal(0, TreePillColumnMath.RightClaim(200, 256, 50, 45));
            Assert.Equal(45, TreePillColumnMath.RightClaim(326, 256, -5, 45));
            Assert.Equal(0, TreePillColumnMath.RightClaim(326, 256, 50, -45));
            Assert.Equal(0, TreePillColumnMath.RightClaim(326, 256, -5, -45));
        }

        // --- ColumnWidth ---
        [Fact]
        public void ColumnWidth_ARowThatFitsTheFloor_LeavesTheColumnAtTheFloor()
        {
            Assert.Equal(256, TreePillColumnMath.ColumnWidth(180, 256, 800));
        }

        [Fact]
        public void ColumnWidth_TakesWhatTheWidestRowAsksFor_AndNoMore()
        {
            Assert.Equal(338, TreePillColumnMath.ColumnWidth(338, 256, 800));
        }

        [Fact]
        public void ColumnWidth_MoreThanThePanelCanSpare_StopsAtWhatItCan()
        {
            Assert.Equal(300, TreePillColumnMath.ColumnWidth(500, 256, 300));
        }

        [Fact]
        public void ColumnWidth_APanelThatCannotEvenAffordTheFloor_KeepsTheFloor()
        {
            Assert.Equal(256, TreePillColumnMath.ColumnWidth(500, 256, 100));
        }

        // --- Scan ---
        private static CraftingTreeNode Node(int id, params CraftingTreeNode[] children)
        {
            return new CraftingTreeNode
            {
                NodeId = id,
                Children = new List<CraftingTreeNode>(children),
            };
        }

        [Fact]
        public void Scan_VisitsCollapsedChildrenToo_SoAnExpandNeverMovesTheColumn()
        {
            var roots = new List<CraftingTreeNode> { Node(1, Node(2, Node(3))) };

            Assert.Equal(90, TreePillColumnMath.Scan(roots, node => node.NodeId * 30));
        }

        [Fact]
        public void Scan_VisitsEveryRootOfAMultiItemPlan()
        {
            var roots = new List<CraftingTreeNode> { Node(1), Node(4) };

            Assert.Equal(40, TreePillColumnMath.Scan(roots, node => node.NodeId * 10));
        }

        [Fact]
        public void Scan_NoTree_NeedsNothing()
        {
            Assert.Equal(0, TreePillColumnMath.Scan(null, _ => 500));
            Assert.Equal(0, TreePillColumnMath.Scan(new List<CraftingTreeNode>(), _ => 500));
        }

        // --- The reported row, end to end ---

        /// <summary>
        /// An Obsidian Heavy Breastplate ingredient row: two sources, an
        /// owned-materials annotation, and the toggle. Against the flat
        /// 256px column it lost a pill to a "+N" chip even on a window with
        /// hundreds of spare pixels; against the derived column it draws
        /// all three at full padding.
        /// </summary>
        [Fact]
        public void TheReportedRow_FitsWholeOnceTheColumnIsAllowedTheWidthTheWindowHas()
        {
            var run = Run("CRAFT", "TP", "HAVE 12/50 NEEDED");
            int slot = ToggleSlot();

            var chipped = Fit(run, slot, PlanRelayoutMath.TreePillColumnWidth);
            Assert.Equal(2, chipped.VisibleCount);
            Assert.Equal(1, chipped.HiddenCount);

            // A 1920px window: 1794px of panel, 542 of them past the
            // module's minimum, all of them the pills' to claim - the run
            // needs 286, well inside the space between the neighbours.
            int panel = WindowSizing.TabPanelWidthFor(1920);
            int column = TreePillColumnMath.ColumnWidth(
                TreePillColumnMath.RequiredWidth(run, Gap, slot),
                PlanRelayoutMath.TreePillColumnWidth,
                TreePillColumnMath.Affordable(
                    panel, PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 0));

            var whole = Fit(run, slot, column);
            Assert.Equal(3, whole.VisibleCount);
            Assert.Equal(0, whole.HiddenCount);
            Assert.Equal(0, whole.WidthReduction);
        }

        /// <summary>
        /// And at the minimum window the same row degrades exactly as it
        /// always did - with no surplus and a cost column already at or
        /// above its floor there is genuinely no room on either side, so
        /// the column cannot grow and nothing the minimum was derived
        /// from moves.
        /// </summary>
        [Fact]
        public void AtTheMinimumWindow_TheColumnIsStillTheFlatFloor()
        {
            var run = Run("CRAFT", "TP", "HAVE 12/50 NEEDED");
            int slot = ToggleSlot();

            int column = TreePillColumnMath.ColumnWidth(
                TreePillColumnMath.RequiredWidth(run, Gap, slot),
                PlanRelayoutMath.TreePillColumnWidth,
                TreePillColumnMath.Affordable(
                    MinPanel(), PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 0));

            Assert.Equal(PlanRelayoutMath.TreePillColumnWidth, column);
            Assert.Equal(1, Fit(run, slot, column).HiddenCount);
        }

        // --- The second report: the 1x Obsidian Shard row, again ---

        // The plan reported in game, as two nodes: a
        // currency-priced purchase whose three-pill run is the widest
        // status text in the tree, and the short "1x Obsidian Shard" row
        // (two sources, an ownership annotation, the toggle) beside it,
        // which lost its third pill to a "+1" chip on windows with room
        // to spare on both sides of the column.
        private const int ChestNodeId = 1;
        private const int ShardNodeId = 2;

        private static List<CraftingTreeNode> ReportedPlanNodes()
        {
            return new List<CraftingTreeNode>
            {
                Node(ChestNodeId),
                Node(ShardNodeId),
            };
        }

        private static List<int> ShardRun()
        {
            return Run("CRAFT", "VENDOR", "HAVE 12/50 NEEDED");
        }

        /// <summary>The tree's widest measured run, the way
        /// TreeSectionController.ScannedPillColumnWidth derives it.</summary>
        private static int ScannedRequired(IReadOnlyList<CraftingTreeNode> roots)
        {
            int chest = TreePillColumnMath.RequiredWidth(
                Run("CURRENCY", "GUILD UPGRADE", "HAVE 125/500 TOTAL"), Gap, ToggleSlot());
            int shard = TreePillColumnMath.RequiredWidth(ShardRun(), Gap, ToggleSlot());

            return TreePillColumnMath.Scan(roots, node =>
                node.NodeId == ChestNodeId ? chest : shard);
        }

        /// <summary>The depth-8 row's name origin: indent 24 a level plus
        /// the caret, icon and name columns (PlanRelayoutMathTests's
        /// TreeNameX).</summary>
        private static int ReportedNameX()
        {
            return 8 * 24 + 58;
        }

        /// <summary>
        /// The leftward half of the rule: with no cost-side slack, the
        /// short row fits whole - no chip, no tightened padding - as soon
        /// as the measured run fits the surplus, and while it claims the
        /// pill column's left edge, and so every name budget, holds
        /// exactly where the minimum window put it.
        /// </summary>
        [Fact]
        public void TheReportedShortRow_FitsWhole_WhenTheSurplusFitsTheRun()
        {
            var roots = ReportedPlanNodes();
            int required = ScannedRequired(roots);

            var atMinimum = PlanRelayoutMath.ComputeTreeColumnEdges(
                MinPanel(), ReportedNameX(), 0, PlanRelayoutMath.TreePillColumnWidth, 150, 8);

            int panel = MinPanel() + 63;
            int affordable = TreePillColumnMath.Affordable(
                panel, PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 0);
            int column = TreePillColumnMath.ColumnWidth(
                required, PlanRelayoutMath.TreePillColumnWidth, affordable);
            int claim = TreePillColumnMath.RightClaim(
                column, PlanRelayoutMath.TreePillColumnWidth, panel - MinPanel(), 0);
            Assert.Equal(0, claim);

            var fit = Fit(ShardRun(), ToggleSlot(), column);
            Assert.Equal(3, fit.VisibleCount);
            Assert.Equal(0, fit.HiddenCount);
            Assert.Equal(0, fit.WidthReduction);

            var edges = PlanRelayoutMath.ComputeTreeColumnEdges(
                panel, ReportedNameX(), 0, column, 150, 8);
            Assert.Equal(atMinimum.PillColX, edges.PillColX);
            Assert.Equal(atMinimum.NameMaxWidth, edges.NameMaxWidth);
        }

        /// <summary>
        /// The rightward half: on a narrower window whose surplus alone
        /// does not fit the run, the cost column's reserve above its
        /// content (150 reserved, 130 its rows draw) is the room the
        /// report points at, and claiming it leaves PillColX - every name
        /// budget with it - exactly where it was.
        /// </summary>
        [Fact]
        public void TheOwnersShortRow_FitsWhole_WhenTheRoomComesFromTheCostColumnsSlack()
        {
            var roots = ReportedPlanNodes();
            int required = ScannedRequired(roots);

            var atMinimum = PlanRelayoutMath.ComputeTreeColumnEdges(
                MinPanel(), ReportedNameX(), 0, PlanRelayoutMath.TreePillColumnWidth, 150, 8);

            int panel = MinPanel() + 43;
            int costSlack = 20;
            int affordable = TreePillColumnMath.Affordable(
                panel, PlanRelayoutMath.TreePillColumnWidth, MinPanel(), costSlack);
            int column = TreePillColumnMath.ColumnWidth(
                required, PlanRelayoutMath.TreePillColumnWidth, affordable);
            int claim = TreePillColumnMath.RightClaim(
                column, PlanRelayoutMath.TreePillColumnWidth, panel - MinPanel(), costSlack);
            Assert.Equal(costSlack, claim);

            var fit = Fit(ShardRun(), ToggleSlot(), column);
            Assert.Equal(3, fit.VisibleCount);
            Assert.Equal(0, fit.HiddenCount);
            Assert.Equal(0, fit.WidthReduction);

            var edges = PlanRelayoutMath.ComputeTreeColumnEdges(
                panel, ReportedNameX(), 0, column, 150 - claim, 8);
            Assert.Equal(atMinimum.PillColX, edges.PillColX);
            Assert.Equal(atMinimum.NameMaxWidth, edges.NameMaxWidth);
        }

        /// <summary>
        /// Once every run is satisfied the claims stop - ColumnWidth never
        /// returns more than the widest row asked for - and every further
        /// pixel goes to the name column, where the extra width was
        /// normally FOR.
        /// </summary>
        [Fact]
        public void OnceTheRunsAreSatisfied_EveryFurtherPixelGoesToTheNameColumn()
        {
            int required = ScannedRequired(ReportedPlanNodes());

            int column = TreePillColumnMath.ColumnWidth(
                required, PlanRelayoutMath.TreePillColumnWidth,
                TreePillColumnMath.Affordable(
                    MinPanel() + 400, PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 0));

            Assert.Equal(required, column);

            var atMinimum = PlanRelayoutMath.ComputeTreeColumnEdges(
                MinPanel(), ReportedNameX(), 0, PlanRelayoutMath.TreePillColumnWidth, 150, 8);
            var satisfied = PlanRelayoutMath.ComputeTreeColumnEdges(
                MinPanel() + 400, ReportedNameX(), 0, column, 150, 8);

            Assert.True(satisfied.NameMaxWidth > atMinimum.NameMaxWidth);
        }

        /// <summary>
        /// The legitimate no-room case: at the module's minimum window,
        /// with no surplus and a cost column already at its floor, the
        /// short row's third pill is genuinely unfittable and the "+1"
        /// chip - with its "No room to show" tooltip - is where it belongs.
        /// </summary>
        [Fact]
        public void AtTheMinimumWindow_WithNoRoomOnEitherSide_TheShortRowStillChips()
        {
            int required = ScannedRequired(ReportedPlanNodes());

            int affordable = TreePillColumnMath.Affordable(
                MinPanel(), PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 0);
            int column = TreePillColumnMath.ColumnWidth(
                required, PlanRelayoutMath.TreePillColumnWidth, affordable);

            Assert.Equal(PlanRelayoutMath.TreePillColumnWidth, column);

            var fit = Fit(ShardRun(), ToggleSlot(), column);
            Assert.Equal(2, fit.VisibleCount);
            Assert.Equal(1, fit.HiddenCount);
            Assert.Equal(Cap("+1") + TightPadding, fit.OverflowPillWidth);
        }

        /// <summary>
        /// The one place a chip is not owed at the minimum: a cost column
        /// narrower than its floor leaves slack the claim may take, and
        /// that claim moves no edge the minimum was derived from - the row
        /// fits, tightened, exactly as a surplus of its own would have
        /// let it.
        /// </summary>
        [Fact]
        public void AtTheMinimumWindow_TheCostColumnsSlackStillFitsTheRow_Tightened()
        {
            int required = ScannedRequired(ReportedPlanNodes());

            int affordable = TreePillColumnMath.Affordable(
                MinPanel(), PlanRelayoutMath.TreePillColumnWidth, MinPanel(), 45);
            int column = TreePillColumnMath.ColumnWidth(
                required, PlanRelayoutMath.TreePillColumnWidth, affordable);

            int claim = TreePillColumnMath.RightClaim(
                column, PlanRelayoutMath.TreePillColumnWidth, 0, 45);
            Assert.Equal(45, claim);

            var fit = Fit(ShardRun(), ToggleSlot(), column);
            Assert.Equal(3, fit.VisibleCount);
            Assert.Equal(0, fit.HiddenCount);
            Assert.Equal(Padding - TightPadding, fit.WidthReduction);
        }

        // --- Resolve: width and claim settled together ---
        private const int Floor = PlanRelayoutMath.TreePillColumnWidth;

        /// <summary>
        /// The defect: the plan-lifetime ratchet held the GRANTED width, so
        /// a width earned at a wide window survived the window narrowing
        /// again and the name column lost the budget it is supposed to keep
        /// at the minimum. Ratcheting the required ink instead gives the
        /// column back on the way down.
        /// </summary>
        [Fact]
        public void Resolve_WidenThenNarrow_GivesTheNameColumnItsBudgetBack()
        {
            int required = Floor + 300;

            var wide = TreePillColumnMath.Resolve(
                required, 0, Floor, MinPanel() + 400, MinPanel(), 0);
            Assert.Equal(Floor + 300, wide.Width);

            var narrow = TreePillColumnMath.Resolve(
                required, wide.RequiredFloor, Floor, MinPanel(), MinPanel(), 0);

            Assert.Equal(Floor, narrow.Width);
        }

        /// <summary>
        /// The other half of the same defect: with the granted width frozen
        /// by the ratchet, RightClaim was re-derived from the CURRENT
        /// surplus and re-attributed the frozen pixels to the cost column's
        /// slack, which moves PillColX at constant pill width.
        /// </summary>
        [Fact]
        public void Resolve_WidenThenNarrow_DoesNotReattributeTheClaim()
        {
            int required = Floor + 300;
            int slack = 40;

            var wide = TreePillColumnMath.Resolve(
                required, 0, Floor, MinPanel() + 400, MinPanel(), slack);
            Assert.Equal(0, wide.CostClaim);

            var narrow = TreePillColumnMath.Resolve(
                required, wide.RequiredFloor, Floor, MinPanel(), MinPanel(), slack);

            // Whatever it claims, it is claimed by a width the panel can
            // actually afford: claim is exactly what the width holds above
            // the floor and the (zero) surplus.
            Assert.Equal(narrow.Width - Floor, narrow.CostClaim);
            Assert.Equal(Floor + slack, narrow.Width);
        }

        /// <summary>
        /// The invariant the ratchet exists for, unchanged: an ignore click
        /// shrinks the widest required run, and the column must not narrow
        /// under the cursor because of it.
        /// </summary>
        [Fact]
        public void Resolve_RequiredShrinksAtAConstantPanelWidth_ColumnHolds()
        {
            int panel = MinPanel() + 400;

            var before = TreePillColumnMath.Resolve(
                Floor + 300, 0, Floor, panel, MinPanel(), 0);

            var after = TreePillColumnMath.Resolve(
                Floor + 40, before.RequiredFloor, Floor, panel, MinPanel(), 0);

            Assert.Equal(before.Width, after.Width);
            Assert.Equal(before.CostClaim, after.CostClaim);
        }

        /// <summary>
        /// TryRefreshInPlace gates an in-place row refresh on this answer.
        /// It compares the WIDTH and the CLAIM, because the claim is netted
        /// out of the cost column; two calls with the same inputs must
        /// therefore agree on both, and the previous composition could
        /// return the same width with a different claim.
        /// </summary>
        [Fact]
        public void Resolve_SameInputs_AgreeOnWidthAndClaimAlike()
        {
            int panel = MinPanel() + 120;

            var first = TreePillColumnMath.Resolve(
                Floor + 300, Floor + 500, Floor, panel, MinPanel(), 60);
            var second = TreePillColumnMath.Resolve(
                Floor + 300, Floor + 500, Floor, panel, MinPanel(), 60);

            Assert.Equal(first.Width, second.Width);
            Assert.Equal(first.CostClaim, second.CostClaim);
            Assert.Equal(first.Width - Floor - 120, first.CostClaim);
        }

        /// <summary>
        /// At a constant panel width the new rule and the old one agree
        /// exactly, because clamping is monotonic: max(clamp(a), clamp(b))
        /// is clamp(max(a, b)). Only a resize parts them.
        /// </summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(200, 0)]
        [InlineData(400, 45)]
        [InlineData(900, 45)]
        public void Resolve_AtAConstantPanelWidth_MatchesTheClampThenRatchetOrder(
            int extraPanel, int slack)
        {
            int panel = MinPanel() + extraPanel;
            int required = Floor + 120;
            int inkFloor = Floor + 380;

            int affordable = TreePillColumnMath.Affordable(panel, Floor, MinPanel(), slack);
            int oldOrder = TreePillColumnMath.ColumnWidth(required, Floor, affordable);
            int oldFloorHeld = TreePillColumnMath.ColumnWidth(inkFloor, Floor, affordable);
            oldOrder = oldOrder > oldFloorHeld ? oldOrder : oldFloorHeld;

            Assert.Equal(
                oldOrder,
                TreePillColumnMath.Resolve(
                    required, inkFloor, Floor, panel, MinPanel(), slack).Width);
        }

        /// <summary>
        /// Panel width 0 is CraftingPlanView.GetCurrentPanelWidth's "no
        /// content panel" answer. It pins nothing: the ink carried forward
        /// is the ink, so the next render at a real width is free.
        /// </summary>
        [Fact]
        public void Resolve_NoContentPanel_TakesTheFloorAndPinsNothing()
        {
            var none = TreePillColumnMath.Resolve(
                Floor + 300, 0, Floor, 0, MinPanel(), 40);

            Assert.Equal(Floor, none.Width);
            Assert.Equal(0, none.CostClaim);
            Assert.Equal(Floor + 300, none.RequiredFloor);

            var next = TreePillColumnMath.Resolve(
                Floor + 300, none.RequiredFloor, Floor, MinPanel() + 400, MinPanel(), 40);
            Assert.Equal(Floor + 300, next.Width);
        }

        // --- The third report: the same "+1" chip, on a plan with
        // a currency band ---

        /// <summary>The tree's fixed cost-column floor
        /// (TreeSectionController.TreeCostColumnWidth), which the cost
        /// column takes as its own floor and not as its reserve.</summary>
        private const int CostFloor = 150;

        /// <summary>
        /// A plan with a currency band: one coin-priced row and one
        /// currency-priced row, so the cost sub-column maxima come from
        /// different rows and no row's ink reaches the reserve's left
        /// edge. Digit advance 8px, currency run 88px.
        /// </summary>
        private static TreeCostColumnMath.CostColumnWidths CurrencyBandCostWidths()
        {
            var roots = new List<CraftingTreeNode>
            {
                new CraftingTreeNode { NodeId = 11, SubtreeCost = 1234567 },
                new CraftingTreeNode
                {
                    NodeId = 12,
                    SubtreeCost = 0,
                    VendorCurrencyCosts = new List<CostLine>
                    {
                        new CostLine { Type = "Currency", Id = 23, Count = 1275 },
                    },
                },
            };

            return TreeCostColumnMath.Scan(roots, text => text.Length * 8, _ => 88);
        }

        /// <summary>
        /// The two columns' arithmetic joined. Read as the fixed floor
        /// less TreeCostColumnMath.TotalWidth, the cost slack was a
        /// negative clamped to zero on every plan with a currency band, so
        /// the short row chipped with the room sitting beside it. Read as
        /// the reserve less the leftmost ink any row draws, the row fits
        /// and the claim still stops short of every cost figure.
        /// </summary>
        [Fact]
        public void ACurrencyBandsCostSlack_FitsTheShortRow_AndStopsShortOfTheInk()
        {
            var costWidths = CurrencyBandCostWidths();

            Assert.True(CostFloor - TreeCostColumnMath.TotalWidth(costWidths) < 0);

            int slack = TreeCostColumnMath.RightSlack(costWidths, CostFloor);
            Assert.Equal(94, slack);

            var resolved = TreePillColumnMath.Resolve(
                ScannedRequired(ReportedPlanNodes()), 0, Floor, MinPanel(), MinPanel(), slack);

            var fit = Fit(ShardRun(), ToggleSlot(), resolved.Width);
            Assert.Equal(3, fit.VisibleCount);
            Assert.Equal(0, fit.HiddenCount);

            Assert.True(
                TreeCostColumnMath.WidthAfterClaim(costWidths, CostFloor, resolved.CostClaim)
                    >= costWidths.LeftmostInkReach);
        }

        /// <summary>
        /// The claim moves the pill column's right edge and nothing else:
        /// PillColX, and so every name budget, is where an unclaimed
        /// layout at the same window width puts it, because
        /// PlanRelayoutMath.ComputeTreeColumnEdges anchors the cost
        /// column's right edge to the panel.
        /// </summary>
        [Fact]
        public void ACurrencyBandsCostSlack_MovesNoNameBudget()
        {
            var costWidths = CurrencyBandCostWidths();
            int reserve = TreeCostColumnMath.Reserve(costWidths, CostFloor);
            int slack = TreeCostColumnMath.RightSlack(costWidths, CostFloor);

            var resolved = TreePillColumnMath.Resolve(
                ScannedRequired(ReportedPlanNodes()), 0, Floor, MinPanel(), MinPanel(), slack);

            var unclaimed = PlanRelayoutMath.ComputeTreeColumnEdges(
                MinPanel(), ReportedNameX(), 0, Floor, reserve, 8);
            var claimed = PlanRelayoutMath.ComputeTreeColumnEdges(
                MinPanel(), ReportedNameX(), 0, resolved.Width,
                TreeCostColumnMath.WidthAfterClaim(costWidths, CostFloor, resolved.CostClaim), 8);

            Assert.Equal(resolved.CostClaim, resolved.Width - Floor);
            Assert.Equal(unclaimed.PillColX, claimed.PillColX);
            Assert.Equal(unclaimed.NameMaxWidth, claimed.NameMaxWidth);
            Assert.Equal(unclaimed.CostRightEdge, claimed.CostRightEdge);
        }

        private static PlanRelayoutMath.PillFitPlan Fit(List<int> run, int slot, int columnWidth)
        {
            int maxRightEdge = columnWidth - TreePillColumnMath.TrailingClearance;
            return PlanRelayoutMath.ComputePillFit(
                run, Padding - TightPadding, Gap, 0,
                TreePillRunLayout.LeadingLimitX(maxRightEdge, slot, Gap),
                hidden => Cap("+" + hidden) + TightPadding);
        }
    }
}
