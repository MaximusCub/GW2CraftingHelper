using System.Collections.Generic;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class ShoppingColumnMathTests
    {
        [Fact]
        public void TypicalValues_FallBackToFixedMinimums()
        {
            // Small coin values (well under the fixed minimums) -> edges
            // fall back to the same minimums as the old fixed-width
            // geometry, so ordinary short lists render exactly as before.
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge: 792, maxEachWidth: 40, maxTotalWidth: 60);

            Assert.Equal(792, edges.TotalRightEdge);
            Assert.Equal(792 - 150 - 20, edges.EachRightEdge);
            Assert.Equal(792 - 150 - 20 - 110 - 20, edges.QtyRightEdge);
        }

        [Fact]
        public void FourDigitGold_BothColumns_ExpandBeyondMinimums()
        {
            // Reproduces the reported bug: 4-digit-gold coin strings (e.g.
            // "1234g 56s 78c") measure wider than the fixed minimums in
            // both the Each and Total columns - this is the Mystic Coin
            // row overflow ("2502x 02 26") from the user's capture.
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge: 792, maxEachWidth: 180, maxTotalWidth: 220);

            Assert.Equal(792, edges.TotalRightEdge);
            Assert.Equal(792 - 220 - 20, edges.EachRightEdge);
            Assert.Equal(792 - 220 - 20 - 180 - 20, edges.QtyRightEdge);
        }

        [Fact]
        public void ZeroWidths_FallBackToMinimums()
        {
            // No row had a non-zero coin value in a column (e.g. an
            // all-currency shopping list) - the pre-scan yields 0 for that
            // column, and the fixed minimums keep it from collapsing to a
            // zero-width column.
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge: 792, maxEachWidth: 0, maxTotalWidth: 0);

            Assert.Equal(792 - 150 - 20, edges.EachRightEdge);
            Assert.Equal(792 - 150 - 20 - 110 - 20, edges.QtyRightEdge);
        }

        [Fact]
        public void OnlyOneColumnWide_OtherStaysAtMinimum()
        {
            // A list where only Total has wide values (e.g. large
            // quantities of a cheap item) must not widen Each too - the two
            // columns are sized independently.
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge: 792, maxEachWidth: 30, maxTotalWidth: 300);

            Assert.Equal(792 - 300 - 20, edges.EachRightEdge);
            Assert.Equal(792 - 300 - 20 - 110 - 20, edges.QtyRightEdge);
        }

        [Theory]
        [InlineData(792, 0, 0)]
        [InlineData(792, 180, 220)]
        [InlineData(400, 300, 300)]
        [InlineData(200, 0, 0)]
        public void OrderingInvariant_QtyLessThanEachLessThanTotal(
            int totalRightEdge, int maxEachWidth, int maxTotalWidth)
        {
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge, maxEachWidth, maxTotalWidth);

            Assert.True(edges.QtyRightEdge < edges.EachRightEdge);
            Assert.True(edges.EachRightEdge < edges.TotalRightEdge);
        }

        // --- Source column (the badge stopped trailing the name and
        // became an aligned column inside the pinned right-hand block) ---
        [Fact]
        public void SourceColumn_SitsOneGapAndOneAmountBandLeftOfTheAmountEdge()
        {
            var edges = ShoppingColumnMath.ComputeEdges(
                totalRightEdge: 792, maxEachWidth: 40, maxTotalWidth: 60,
                maxQtyWidth: 79, sourceColumnWidth: 96);

            Assert.Equal(
                edges.QtyRightEdge - 79 - ShoppingColumnMath.ColumnGap - 96,
                edges.SourceX);
        }

        [Fact]
        public void SourceColumn_LeftEdgeIsTheNameBudgetsStop_NotTheAmountEdge()
        {
            // The name used to budget against QtyRightEdge with its OWN
            // badge width subtracted, so no two rows' badges lined up. The
            // budget stops at one fixed x for the whole table now, and that
            // x is strictly left of the Amount column.
            var edges = ShoppingColumnMath.ComputeEdges(792, 40, 60, 79, 96);

            Assert.True(edges.SourceX < edges.QtyRightEdge);
        }

        [Fact]
        public void WiderBadge_MovesTheSourceColumnLeft_AndNothingElse()
        {
            // The badge column widens into the NAME's space, never into
            // Amount/Each/Total - every one of those hangs off the pinned
            // right edge and is unaffected.
            var narrow = ShoppingColumnMath.ComputeEdges(792, 40, 60, 79, 60);
            var wide = ShoppingColumnMath.ComputeEdges(792, 40, 60, 79, 100);

            Assert.Equal(narrow.SourceX - 40, wide.SourceX);
            Assert.Equal(narrow.QtyRightEdge, wide.QtyRightEdge);
            Assert.Equal(narrow.EachRightEdge, wide.EachRightEdge);
            Assert.Equal(narrow.TotalRightEdge, wide.TotalRightEdge);
        }

        [Fact]
        public void SourceColumn_TracksThePanelEdgeLikeEveryOtherColumn()
        {
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 1252, maxEachWidth: 40, maxTotalWidth: 60,
                maxQtyWidth: 79, sourceColumnWidth: 96);
            var wider = ShoppingColumnMath.ComputeEdgesForPanel(1452, 40, 60, 79, 96);

            Assert.Equal(edges.SourceX + 200, wider.SourceX);
            Assert.Equal(PlanRelayoutMath.PinnedRightEdge(1252), edges.TotalRightEdge);
        }

        // --- SegmentRunWidth (currency-segment width computation, KNOWN-ISSUES #16) ---
        [Fact]
        public void SegmentRunWidth_Null_ReturnsZero()
        {
            Assert.Equal(0, ShoppingColumnMath.SegmentRunWidth(null, 20, 2, 6));
        }

        [Fact]
        public void SegmentRunWidth_Empty_ReturnsZero()
        {
            Assert.Equal(0, ShoppingColumnMath.SegmentRunWidth(new List<int>(), 20, 2, 6));
        }

        [Fact]
        public void SegmentRunWidth_SingleSegment_NoTrailingGap()
        {
            // 30 (text) + 2 (label-icon gap) + 20 (icon) = 52, no trailing
            // segmentGap since there is only one segment.
            var width = ShoppingColumnMath.SegmentRunWidth(new List<int> { 30 }, 20, 2, 6);

            Assert.Equal(52, width);
        }

        [Fact]
        public void SegmentRunWidth_TwoSegments_IncludesGapBetweenNotAfter()
        {
            // Each segment is textWidth + 2 + 20; a single segmentGap (6)
            // separates them, none trails after the last one.
            var width = ShoppingColumnMath.SegmentRunWidth(new List<int> { 30, 15 }, 20, 2, 6);

            Assert.Equal((30 + 2 + 20) + 6 + (15 + 2 + 20), width);
        }

        [Fact]
        public void SegmentRunWidth_UsesCallerSuppliedConstants_NotHardcoded()
        {
            // Different icon/gap constants than CraftingPlanView's own
            // (20/2/6) must change the result - proves the arithmetic is
            // fully parameterized, not silently defaulting to a baked-in
            // set of pixel values.
            var width = ShoppingColumnMath.SegmentRunWidth(new List<int> { 10 }, iconSize: 100, labelIconGap: 5, segmentGap: 1);

            Assert.Equal(10 + 5 + 100, width);
        }

        // --- SegmentRunWidth(int[], ...) overload
        // (the per-frame resize hot path passes SegmentLayoutHandle.TextWidths,
        // a concrete int[], to a non-allocating overload rather than the
        // IReadOnlyList<int> one above; both must agree on every result) ---
        [Fact]
        public void SegmentRunWidthArrayOverload_Null_ReturnsZero()
        {
            Assert.Equal(0, ShoppingColumnMath.SegmentRunWidth((int[])null, 20, 2, 6));
        }

        [Fact]
        public void SegmentRunWidthArrayOverload_Empty_ReturnsZero()
        {
            Assert.Equal(0, ShoppingColumnMath.SegmentRunWidth(new int[0], 20, 2, 6));
        }

        [Fact]
        public void SegmentRunWidthArrayOverload_SingleSegment_NoTrailingGap()
        {
            var width = ShoppingColumnMath.SegmentRunWidth(new int[] { 30 }, 20, 2, 6);

            Assert.Equal(52, width);
        }

        [Fact]
        public void SegmentRunWidthArrayOverload_MatchesListOverload_ForSameInput()
        {
            // Both overloads implement the same formula; a resize-tick call
            // through the int[] overload must never drift from a
            // build-time call through the IReadOnlyList<int> overload for
            // the same segment widths.
            var widths = new int[] { 30, 15, 42 };

            int arrayResult = ShoppingColumnMath.SegmentRunWidth(widths, 20, 2, 6);
            int listResult = ShoppingColumnMath.SegmentRunWidth(new List<int>(widths), 20, 2, 6);

            Assert.Equal(listResult, arrayResult);
        }

        // --- ComputeEdgesForPanel (the justified-width invariant) ---
        [Fact]
        public void ComputeEdgesForPanel_AnchorsTheTotalColumnToThePinnedPanelEdge()
        {
            var fromEdge = ShoppingColumnMath.ComputeEdges(
                PlanRelayoutMath.PinnedRightEdge(1000), maxEachWidth: 0, maxTotalWidth: 0);
            var fromPanel = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 1000, maxEachWidth: 0, maxTotalWidth: 0);

            Assert.Equal(fromEdge.TotalRightEdge, fromPanel.TotalRightEdge);
            Assert.Equal(fromEdge.EachRightEdge, fromPanel.EachRightEdge);
            Assert.Equal(fromEdge.QtyRightEdge, fromPanel.QtyRightEdge);
        }

        [Fact]
        public void ComputeEdgesForPanel_WiderPanel_MovesEveryColumnByTheFullIncrease()
        {
            var narrow = ShoppingColumnMath.ComputeEdgesForPanel(1000, maxEachWidth: 0, maxTotalWidth: 0);
            var wide = ShoppingColumnMath.ComputeEdgesForPanel(1400, maxEachWidth: 0, maxTotalWidth: 0);

            Assert.Equal(400, wide.TotalRightEdge - narrow.TotalRightEdge);
            Assert.Equal(400, wide.EachRightEdge - narrow.EachRightEdge);
            Assert.Equal(400, wide.QtyRightEdge - narrow.QtyRightEdge);
        }

        [Fact]
        public void ComputeEdgesForPanel_NameBudgetGrowsWithThePanel()
        {
            // The Item column is the one that flexes: it absorbs the whole
            // width increase, measured exactly as CreateShoppingRow budgets
            // it (Amount band, NameToQtyGap 12, no source tag).
            const int nameX = 50;
            const int amountBand = 32;

            int narrow = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                ShoppingColumnMath.ComputeEdgesForPanel(1000, 0, 0).QtyRightEdge, amountBand, 12, nameX);
            int wide = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                ShoppingColumnMath.ComputeEdgesForPanel(1400, 0, 0).QtyRightEdge, amountBand, 12, nameX);

            Assert.Equal(400, wide - narrow);
        }

        // Which column a click in the band sorts by. The failure pinned:
        // a boundary between the two WORDS puts the Source cell over the
        // right-hand end of the item NAMES.
        [Fact]
        public void HeaderCellBoundaries_SplitTheGapsBetweenTheColumns()
        {
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 1000, maxEachWidth: 0, maxTotalWidth: 0,
                maxQtyWidth: 79, sourceColumnWidth: 90);

            var boundaries = new int[4];
            ShoppingColumnMath.HeaderCellBoundaries(edges, 90, 12, boundaries);

            // Item ends just before the source badges begin...
            Assert.Equal(edges.SourceX - 6, boundaries[0]);

            // ...and every other is the middle of the columns' own gap.
            Assert.Equal(edges.SourceX + 90 + 10, boundaries[1]);

            // The same boundary from the other side.
            Assert.Equal((edges.QtyRightEdge - 79) - 10, boundaries[1]);
            Assert.Equal(edges.QtyRightEdge + 10, boundaries[2]);
            Assert.Equal(edges.EachRightEdge + 10, boundaries[3]);

            for (int i = 1; i < boundaries.Length; i++)
            {
                Assert.True(boundaries[i] > boundaries[i - 1], "boundaries run left to right");
            }
        }

        [Fact]
        public void HeaderCellBoundaries_KeepTheItemCellOverTheWholeNameColumn()
        {
            // The name budget and the boundary are the same edge.
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 1400, maxEachWidth: 0, maxTotalWidth: 0,
                maxQtyWidth: 79, sourceColumnWidth: 90);

            var boundaries = new int[4];
            ShoppingColumnMath.HeaderCellBoundaries(edges, 90, 12, boundaries);

            int nameRightEdge = 50 + PlanRelayoutMath.NameMaxWidthBeforeColumn(edges.SourceX, 0, 12, 50);

            Assert.True(boundaries[0] >= nameRightEdge - 6);
            Assert.True(boundaries[0] < edges.SourceX);
        }

        [Fact]
        public void HeaderCellBoundaries_IgnoreABufferItCannotFill()
        {
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(1000, 0, 0);

            ShoppingColumnMath.HeaderCellBoundaries(edges, 90, 12, null);
            var tooShort = new int[2];
            ShoppingColumnMath.HeaderCellBoundaries(edges, 90, 12, tooShort);

            Assert.Equal(new[] { 0, 0 }, tooShort);
        }

        [Fact]
        public void ComputeEdgesForPanel_NarrowPanel_StillEndsOneMarginInFromTheEdge()
        {
            var edges = ShoppingColumnMath.ComputeEdgesForPanel(
                panelWidth: 500, maxEachWidth: 0, maxTotalWidth: 0);

            Assert.Equal(500 - PlanRelayoutMath.TableRightMargin, edges.TotalRightEdge);
        }
    }
}
