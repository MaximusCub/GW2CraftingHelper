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
    }
}
