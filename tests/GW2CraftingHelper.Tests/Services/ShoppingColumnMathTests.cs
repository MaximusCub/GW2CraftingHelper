using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class ShoppingColumnMathTests
    {
        [Fact]
        public void TypicalValues_FallBackToFixedMinimums()
        {
            // Small coin values (well under the fixed minimums) -> edges
            // fall back to the same minimums as the pre-M32-#? fixed-width
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

        // --- SegmentRunWidth(int[], ...) overload (M33 UX-wave fix-pass:
        // the per-frame resize hot path passes SegmentLayoutHandle.TextWidths,
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
    }
}
