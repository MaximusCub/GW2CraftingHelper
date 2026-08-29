using System.Collections.Generic;
using System.Reflection;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The cost column's no-narrowing floor, exercised through the real
    /// scan it floors: a tree scanned with a currency row, then the same
    /// tree with that row ignored away, and the decision pills' own column
    /// edge asked of PlanRelayoutMath either side. Without the floor the
    /// second scan is narrower and the whole pill run slides right - which
    /// is what "the entire button system shifts horizontally under the
    /// click" was.
    /// </summary>
    public class TreeCostColumnFloorTests
    {
        // Same stand-in as TreeCostColumnMathTests: one pixel per
        // character, with the currency-run measurement supplied separately
        // exactly as TreeSectionController supplies it.
        private static int MeasureByLength(string text)
        {
            return text.Length;
        }

        private const int CurrencyRunWidth = 90;
        private const int PanelWidth = 1200;
        private const int PillColumnWidth = PlanRelayoutMath.TreePillColumnWidth;
        private const int CostColumnFloor = 150;
        private const int RightMargin = 8;

        private static CraftingTreeNode Node(
            long? subtreeCost, IReadOnlyList<CostLine> vendorCurrencyCosts = null)
        {
            return new CraftingTreeNode
            {
                NodeId = 1,
                SubtreeCost = subtreeCost,
                VendorCurrencyCosts = vendorCurrencyCosts,
            };
        }

        private static TreeCostColumnMath.CostColumnWidths Scan(IReadOnlyList<CraftingTreeNode> roots)
        {
            return TreeCostColumnMath.Scan(roots, MeasureByLength, _ => CurrencyRunWidth);
        }

        /// <summary>A vendor-priced row: the one that owns the tree's
        /// currency sub-column.</summary>
        private static List<CraftingTreeNode> TreeWithACurrencyRow()
        {
            return new List<CraftingTreeNode>
            {
                Node(1234567),
                Node(
                    89,
                    new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 1275 } }),
            };
        }

        /// <summary>The same tree after that row was ignored: an ignored
        /// node is rebuilt as an owned leaf with no cost at all.</summary>
        private static List<CraftingTreeNode> TreeWithThatRowIgnored()
        {
            return new List<CraftingTreeNode>
            {
                Node(1234567),
                Node(null),
            };
        }

        private static int PillColumnX(TreeCostColumnMath.CostColumnWidths widths)
        {
            int scanned = TreeCostColumnMath.TotalWidth(widths);
            int costColumnWidth = scanned > CostColumnFloor ? scanned : CostColumnFloor;
            return PlanRelayoutMath.ComputeTreeColumnEdges(
                PanelWidth, 0, 0, PillColumnWidth, costColumnWidth, RightMargin).PillColX;
        }

        [Fact]
        public void IgnoringTheWidestCurrencyRow_WouldNarrowTheRawScan()
        {
            var before = Scan(TreeWithACurrencyRow());
            var after = Scan(TreeWithThatRowIgnored());

            Assert.Equal(CurrencyRunWidth, before.CurrencyRunWidth);
            Assert.Equal(0, after.CurrencyRunWidth);
            Assert.True(TreeCostColumnMath.TotalWidth(after) < TreeCostColumnMath.TotalWidth(before));
        }

        /// <summary>
        /// The regression: with the floor applied, the pill column's left
        /// edge is identical before and after the ignore, so no row's
        /// pills move.
        /// </summary>
        [Fact]
        public void FlooredWidths_KeepThePillColumnWhereItWas()
        {
            var floor = TreeCostColumnFloor.Widen(
                TreeCostColumnMath.CostColumnWidths.Empty, Scan(TreeWithACurrencyRow()));
            var afterIgnore = TreeCostColumnFloor.Widen(floor, Scan(TreeWithThatRowIgnored()));

            Assert.True(TreeCostColumnFloor.Equal(floor, afterIgnore));
            Assert.Equal(PillColumnX(floor), PillColumnX(afterIgnore));
        }

        [Fact]
        public void UnflooredWidths_MoveThePillColumn()
        {
            // The defect this floor exists for, stated as a fact about the
            // production edge math rather than as a claim in a comment.
            Assert.NotEqual(
                PillColumnX(Scan(TreeWithACurrencyRow())),
                PillColumnX(Scan(TreeWithThatRowIgnored())));
        }

        [Fact]
        public void Widen_TakesEachSubColumnIndependently()
        {
            var floor = new TreeCostColumnMath.CostColumnWidths(20, 5, 9, 0);
            var scanned = new TreeCostColumnMath.CostColumnWidths(7, 11, 9, 40);

            var widened = TreeCostColumnFloor.Widen(floor, scanned);

            Assert.Equal(20, widened.GoldTextWidth);
            Assert.Equal(11, widened.SilverTextWidth);
            Assert.Equal(9, widened.CopperTextWidth);
            Assert.Equal(40, widened.CurrencyRunWidth);
        }

        [Fact]
        public void Widen_FromEmpty_AdoptsTheScanExactly()
        {
            var scanned = Scan(TreeWithACurrencyRow());

            Assert.True(TreeCostColumnFloor.Equal(
                scanned,
                TreeCostColumnFloor.Widen(TreeCostColumnMath.CostColumnWidths.Empty, scanned)));
        }

        [Fact]
        public void Equal_SeesEveryField()
        {
            var baseline = new TreeCostColumnMath.CostColumnWidths(1, 2, 3, 4, 5);

            Assert.True(TreeCostColumnFloor.Equal(
                baseline, new TreeCostColumnMath.CostColumnWidths(1, 2, 3, 4, 5)));
            Assert.False(TreeCostColumnFloor.Equal(
                baseline, new TreeCostColumnMath.CostColumnWidths(9, 2, 3, 4, 5)));
            Assert.False(TreeCostColumnFloor.Equal(
                baseline, new TreeCostColumnMath.CostColumnWidths(1, 9, 3, 4, 5)));
            Assert.False(TreeCostColumnFloor.Equal(
                baseline, new TreeCostColumnMath.CostColumnWidths(1, 2, 9, 4, 5)));
            Assert.False(TreeCostColumnFloor.Equal(
                baseline, new TreeCostColumnMath.CostColumnWidths(1, 2, 3, 9, 5)));
            Assert.False(TreeCostColumnFloor.Equal(
                baseline, new TreeCostColumnMath.CostColumnWidths(1, 2, 3, 4, 9)));
        }

        /// <summary>
        /// The comparator and the floor are hand-written field lists, so
        /// nothing but this stops a sixth field from being added and
        /// silently ignored by both - which is exactly how the ink extent
        /// came to be dropped. Reflection lives HERE and not in the
        /// production comparator on purpose: the gate is a compile-time
        /// count, not a per-comparison cost.
        /// </summary>
        [Fact]
        public void CostColumnWidths_HasNoFieldTheFloorIgnores()
        {
            var fields = typeof(TreeCostColumnMath.CostColumnWidths)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            Assert.Equal(5, fields.Length);
        }

        /// <summary>
        /// The ink extent the "Cost" header centres over survives the
        /// floor. It did not: Widen rebuilt the struct through the
        /// four-argument constructor, so every floored width reported an
        /// extent of 0 and TreeCostColumnMath.HeaderX degenerated to
        /// right-aligning the word on the column edge.
        /// </summary>
        [Fact]
        public void Widen_CarriesTheInkExtent()
        {
            var scanned = Scan(TreeWithACurrencyRow());
            Assert.True(scanned.WidestRowRunWidth > 0);

            var floored = TreeCostColumnFloor.Widen(
                TreeCostColumnMath.CostColumnWidths.Empty, scanned);

            Assert.Equal(scanned.WidestRowRunWidth, floored.WidestRowRunWidth);

            // The extent is load-bearing, not decorative: the header lands
            // somewhere else entirely without it.
            var dropped = new TreeCostColumnMath.CostColumnWidths(
                floored.GoldTextWidth, floored.SilverTextWidth,
                floored.CopperTextWidth, floored.CurrencyRunWidth);
            var room = JustifiedColumnTracks.HeaderRoom.Between(0, 600);
            Assert.NotEqual(
                TreeCostColumnMath.HeaderX(600, dropped, 30, room),
                TreeCostColumnMath.HeaderX(600, floored, 30, room));
        }

        /// <summary>
        /// One-way, like every other field: a re-solve that only removes
        /// the widest ink keeps the header where it was, which is also
        /// what keeps that re-solve eligible for the in-place refresh
        /// (TreeSectionController.TryRefreshInPlace gates on Equal).
        /// </summary>
        [Fact]
        public void Widen_NeverNarrowsTheInkExtent()
        {
            var floor = TreeCostColumnFloor.Widen(
                TreeCostColumnMath.CostColumnWidths.Empty, Scan(TreeWithACurrencyRow()));
            var afterIgnore = TreeCostColumnFloor.Widen(floor, Scan(TreeWithThatRowIgnored()));

            Assert.True(Scan(TreeWithThatRowIgnored()).WidestRowRunWidth < floor.WidestRowRunWidth);
            Assert.Equal(floor.WidestRowRunWidth, afterIgnore.WidestRowRunWidth);
            Assert.True(TreeCostColumnFloor.Equal(floor, afterIgnore));
        }
    }
}
