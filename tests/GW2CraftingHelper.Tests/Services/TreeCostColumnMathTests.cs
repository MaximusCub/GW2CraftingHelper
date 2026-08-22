using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Exercises the real TreeCostColumnMath used by the recipe tree's cost
    /// column. The two Blish-bound measurements are supplied as delegates
    /// exactly as TreeSectionController supplies them (a text measurement
    /// and a currency-run measurement); everything under test - which nodes
    /// widen which sub-column, and where the sub-columns land - is the
    /// production code path itself.
    /// </summary>
    public class TreeCostColumnMathTests
    {
        // Stand-in for a proportional font: one pixel per character. Real
        // widths come from BitmapFont.MeasureString in the view; what
        // matters here is that the WIDEST string per denomination wins.
        private static int MeasureByLength(string text)
        {
            return text.Length;
        }

        private static CraftingTreeNode Node(
            int nodeId, long? subtreeCost = null,
            IReadOnlyList<CraftingTreeNode> children = null,
            IReadOnlyList<CostLine> vendorCurrencyCosts = null,
            bool isCostComponent = false)
        {
            return new CraftingTreeNode
            {
                NodeId = nodeId,
                SubtreeCost = subtreeCost,
                Children = children,
                VendorCurrencyCosts = vendorCurrencyCosts,
                IsCostComponent = isCostComponent
            };
        }

        private static IReadOnlyList<CostLine> OneCurrencyLine()
        {
            return new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 1275 } };
        }

        private static TreeCostColumnMath.CostColumnWidths Scan(
            IReadOnlyList<CraftingTreeNode> roots, int currencyRunWidth = 0)
        {
            return TreeCostColumnMath.Scan(roots, MeasureByLength, _ => currencyRunWidth);
        }

        // --- Scan ---

        [Fact]
        public void Scan_NullOrEmptyRoots_IsAllZero()
        {
            var fromNull = Scan(null);
            Assert.Equal(0, fromNull.GoldTextWidth);
            Assert.Equal(0, fromNull.SilverTextWidth);
            Assert.Equal(0, fromNull.CopperTextWidth);
            Assert.Equal(0, fromNull.CurrencyRunWidth);

            Assert.Equal(0, Scan(new List<CraftingTreeNode>()).GoldTextWidth);
        }

        [Fact]
        public void Scan_TakesTheWidestValuePerDenominationAcrossTheWholeTree()
        {
            // 41g26s80c -> "41"/"26"/"80"; 1234g07s05c -> "1234"/"07"/"05".
            // Gold's widest comes from the deep child, silver/copper are
            // always two padded characters once gold is present.
            var deep = Node(3, subtreeCost: 12340705L);
            var mid = Node(2, subtreeCost: 412680L, children: new List<CraftingTreeNode> { deep });
            var root = Node(1, subtreeCost: 412680L, children: new List<CraftingTreeNode> { mid });

            var widths = Scan(new List<CraftingTreeNode> { root });

            Assert.Equal(4, widths.GoldTextWidth);
            Assert.Equal(2, widths.SilverTextWidth);
            Assert.Equal(2, widths.CopperTextWidth);
        }

        [Fact]
        public void Scan_SubGoldAmount_LeavesTheGoldSubColumnUnreserved()
        {
            // 45s39c has no gold unit at all, so nothing may reserve a
            // gold band - the column has to collapse, not sit empty.
            var root = Node(1, subtreeCost: 4539L);

            var widths = Scan(new List<CraftingTreeNode> { root });

            Assert.Equal(0, widths.GoldTextWidth);
            Assert.Equal(2, widths.SilverTextWidth);
            Assert.Equal(2, widths.CopperTextWidth);
        }

        [Fact]
        public void Scan_NodeWithoutASubtreeCost_WidensNothing()
        {
            // A HAVE/UNKNOWN node keeps the cost column blank; it must not
            // reserve width for a value it never draws.
            var root = Node(1, children: new List<CraftingTreeNode> { Node(2) });

            var widths = Scan(new List<CraftingTreeNode> { root });

            Assert.Equal(0, widths.GoldTextWidth);
            Assert.Equal(0, widths.SilverTextWidth);
            Assert.Equal(0, widths.CopperTextWidth);
        }

        [Fact]
        public void Scan_ZeroAndUncostedNode_ReservesNoCopperBand()
        {
            // A genuinely zero cost renders the unpriceable dash, not a
            // "0" copper segment.
            var root = Node(1, subtreeCost: 0L);

            var widths = Scan(new List<CraftingTreeNode> { root });

            Assert.Equal(0, widths.CopperTextWidth);
        }

        [Fact]
        public void Scan_CurrencyBearingNode_ReservesTheCurrencyBand()
        {
            var root = Node(1, subtreeCost: 0L, vendorCurrencyCosts: OneCurrencyLine());

            Assert.Equal(88, Scan(new List<CraftingTreeNode> { root }, currencyRunWidth: 88).CurrencyRunWidth);
        }

        [Fact]
        public void Scan_CostComponentChildren_ReserveNoCurrencyBand()
        {
            // Such a node shows only its compact coin total - the currency
            // breakdown lives one expand-click away as real child rows -
            // so reserving a currency band for it would be dead space.
            var root = Node(
                1, subtreeCost: 1000L,
                children: new List<CraftingTreeNode> { Node(2, isCostComponent: true) },
                vendorCurrencyCosts: OneCurrencyLine());

            Assert.Equal(0, Scan(new List<CraftingTreeNode> { root }, currencyRunWidth: 88).CurrencyRunWidth);
        }

        [Fact]
        public void Scan_MultipleRoots_AreAllVisited()
        {
            var roots = new List<CraftingTreeNode>
            {
                Node(1, subtreeCost: 4539L),      // silver/copper only
                Node(2, subtreeCost: 12340705L)   // 4-character gold
            };

            Assert.Equal(4, Scan(roots).GoldTextWidth);
        }

        [Fact]
        public void ShowsCurrencySegments_MatchesWhatTheRowActuallyDraws()
        {
            Assert.False(TreeCostColumnMath.ShowsCurrencySegments(null));
            Assert.False(TreeCostColumnMath.ShowsCurrencySegments(Node(1)));
            Assert.True(TreeCostColumnMath.ShowsCurrencySegments(
                Node(1, vendorCurrencyCosts: OneCurrencyLine())));
            Assert.False(TreeCostColumnMath.ShowsCurrencySegments(
                Node(1,
                    children: new List<CraftingTreeNode> { Node(2, isCostComponent: true) },
                    vendorCurrencyCosts: OneCurrencyLine())));
        }

        // --- ComputeEdges / TotalWidth ---
        //
        // Pinned as absolute pixel offsets from the column's right edge
        // rather than recomputed from the same constants the formula
        // reads. Segment width is text + CoinLabelIconGap(2) +
        // CoinIconSize(20); sub-columns are separated by
        // CoinSegmentGap(6). A deliberate geometry change re-baselines the
        // literals here.

        [Fact]
        public void ComputeEdges_AllThreeDenominations_StackRightToLeft()
        {
            var widths = new TreeCostColumnMath.CostColumnWidths(30, 20, 20, 0);
            var edges = TreeCostColumnMath.ComputeEdges(1000, widths);

            Assert.Equal(1000, edges.CurrencyRightEdge);
            Assert.Equal(1000, edges.CopperRightEdge);          // no currency band
            Assert.Equal(1000 - 42 - 6, edges.SilverRightEdge); // copper: 20+2+20
            Assert.Equal(1000 - 42 - 6 - 42 - 6, edges.GoldRightEdge);
        }

        [Fact]
        public void ComputeEdges_CurrencyBand_PushesEveryCoinSubColumnLeft()
        {
            var widths = new TreeCostColumnMath.CostColumnWidths(30, 20, 20, 88);
            var edges = TreeCostColumnMath.ComputeEdges(1000, widths);

            Assert.Equal(1000, edges.CurrencyRightEdge);
            Assert.Equal(1000 - 88 - 6, edges.CopperRightEdge);
        }

        [Fact]
        public void ComputeEdges_AbsentDenomination_CostsNoReservedWidth()
        {
            // No row in the tree renders gold. Its edge still exists (it
            // is simply where a gold segment WOULD end), but nothing is
            // reserved for it, so the column as a whole is exactly the
            // silver and copper bands - a plan whose costs never reach a
            // gold does not get a gold-sized hole in its layout.
            var widths = new TreeCostColumnMath.CostColumnWidths(0, 20, 20, 0);
            var edges = TreeCostColumnMath.ComputeEdges(1000, widths);

            Assert.Equal(1000 - 42 - 6, edges.SilverRightEdge);
            Assert.Equal(42 + 6 + 42, edges.TotalWidth);
        }

        [Fact]
        public void ComputeEdges_NoCoinRowsAtAll_CollapsesToTheCurrencyBandAlone()
        {
            var widths = new TreeCostColumnMath.CostColumnWidths(0, 0, 0, 88);
            var edges = TreeCostColumnMath.ComputeEdges(1000, widths);

            Assert.Equal(1000, edges.CurrencyRightEdge);
            Assert.Equal(1000 - 88 - 6, edges.CopperRightEdge);
            Assert.Equal(88, edges.TotalWidth);
        }

        [Fact]
        public void ComputeEdges_IconsLandOnTheSameXForEveryRow()
        {
            // The whole point of the sub-columns: a segment right-aligned
            // to its own sub-column edge puts its fixed-width icon at the
            // same x whatever the number's own width is.
            var widths = new TreeCostColumnMath.CostColumnWidths(40, 20, 20, 0);
            var edges = TreeCostColumnMath.ComputeEdges(1000, widths);

            int narrowIconLeft = edges.GoldRightEdge - TreeCostColumnMath.SegmentWidth(10)
                + 10 + CoinSegmentMath.CoinLabelIconGap;
            int wideIconLeft = edges.GoldRightEdge - TreeCostColumnMath.SegmentWidth(40)
                + 40 + CoinSegmentMath.CoinLabelIconGap;

            Assert.Equal(wideIconLeft, narrowIconLeft);
            Assert.Equal(edges.GoldRightEdge - CoinSegmentMath.CoinIconSize, narrowIconLeft);
        }

        [Fact]
        public void TotalWidth_IsEveryPopulatedBandPlusItsGaps()
        {
            Assert.Equal(0, TreeCostColumnMath.TotalWidth(TreeCostColumnMath.CostColumnWidths.Empty));

            // copper only: 20 + 2 + 20
            Assert.Equal(42, TreeCostColumnMath.TotalWidth(
                new TreeCostColumnMath.CostColumnWidths(0, 0, 20, 0)));

            // gold(52) + gap + silver(42) + gap + copper(42) + gap + currency(88)
            Assert.Equal(52 + 6 + 42 + 6 + 42 + 6 + 88, TreeCostColumnMath.TotalWidth(
                new TreeCostColumnMath.CostColumnWidths(30, 20, 20, 88)));
        }

        [Fact]
        public void TotalWidth_AgreesWithTheSpanComputeEdgesActuallyLaysOut()
        {
            var widths = new TreeCostColumnMath.CostColumnWidths(30, 20, 20, 88);
            var edges = TreeCostColumnMath.ComputeEdges(1000, widths);

            int leftmostX = edges.GoldRightEdge - TreeCostColumnMath.SegmentWidth(widths.GoldTextWidth);
            Assert.Equal(1000 - leftmostX, edges.TotalWidth);
            Assert.Equal(TreeCostColumnMath.TotalWidth(widths), edges.TotalWidth);
        }

        [Fact]
        public void SegmentWidth_AbsentText_IsZero()
        {
            Assert.Equal(0, TreeCostColumnMath.SegmentWidth(0));
            Assert.Equal(
                12 + CoinSegmentMath.CoinLabelIconGap + CoinSegmentMath.CoinIconSize,
                TreeCostColumnMath.SegmentWidth(12));
        }
    }
}
