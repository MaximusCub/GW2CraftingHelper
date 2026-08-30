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

        // --- Affordable: the name column's protection ---
        [Fact]
        public void Affordable_AtTheModulesMinimumWindow_IsExactlyTheFloor()
        {
            Assert.Equal(
                PlanRelayoutMath.TreePillColumnWidth,
                TreePillColumnMath.Affordable(
                    MinPanel(), PlanRelayoutMath.TreePillColumnWidth, MinPanel()));
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
                    600, PlanRelayoutMath.TreePillColumnWidth, MinPanel()));
        }

        [Fact]
        public void Affordable_GrowsByHalfTheSurplus_LeavingTheOtherHalfToTheName()
        {
            int panel = MinPanel() + 400;

            Assert.Equal(
                PlanRelayoutMath.TreePillColumnWidth + 200,
                TreePillColumnMath.Affordable(
                    panel, PlanRelayoutMath.TreePillColumnWidth, MinPanel()));
        }

        /// <summary>
        /// The property that makes the split safe at every width: widening
        /// the window can never leave the name column narrower than it was
        /// one pixel earlier, because the pill column takes at most half of
        /// each new pixel.
        /// </summary>
        [Fact]
        public void Affordable_WideningTheWindow_NeverNarrowsWhatIsLeftForTheName()
        {
            int previous = int.MinValue;
            for (int panel = MinPanel(); panel < MinPanel() + 600; panel++)
            {
                int left = panel - TreePillColumnMath.Affordable(
                    panel, PlanRelayoutMath.TreePillColumnWidth, MinPanel());
                Assert.True(left >= previous, "the name column lost width as the window grew");
                previous = left;
            }
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
            // module's minimum, of which the pills may claim half.
            int panel = WindowSizing.TabPanelWidthFor(1920);
            int column = TreePillColumnMath.ColumnWidth(
                TreePillColumnMath.RequiredWidth(run, Gap, slot),
                PlanRelayoutMath.TreePillColumnWidth,
                TreePillColumnMath.Affordable(panel, PlanRelayoutMath.TreePillColumnWidth, MinPanel()));

            var whole = Fit(run, slot, column);
            Assert.Equal(3, whole.VisibleCount);
            Assert.Equal(0, whole.HiddenCount);
            Assert.Equal(0, whole.WidthReduction);
        }

        /// <summary>
        /// And at the minimum window the same row degrades exactly as it
        /// always did - the column cannot grow there, so nothing the
        /// minimum was derived from moves.
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
                    MinPanel(), PlanRelayoutMath.TreePillColumnWidth, MinPanel()));

            Assert.Equal(PlanRelayoutMath.TreePillColumnWidth, column);
            Assert.Equal(1, Fit(run, slot, column).HiddenCount);
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
