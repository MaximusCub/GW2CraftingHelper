using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Direct unit tests on CostLineValuation's pure
    /// coin-valuation helper, shared by RecipeSheetSavingsCalculator and
    /// SeasonalVendorTipCalculator.
    /// </summary>
    public class CostLineValuationTests
    {
        [Fact]
        public void PureCoinLine_ReturnsSum()
        {
            var lines = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 500 },
            };

            bool ok = CostLineValuation.TryGetCoinCost(
                lines, new Dictionary<int, ItemPrice>(), PriceBasis.BuyOrder, out long coin);

            Assert.True(ok);
            Assert.Equal(500, coin);
        }

        [Fact]
        public void PricedItemLine_ReturnsScaledCost()
        {
            var lines = new List<CostLine>
            {
                new CostLine { Type = "Item", Id = 10, Count = 3 },
            };
            // PriceBasis.BuyOrder reads ItemPrice.SellInstant as its preferred
            // side - see PlanSolver.GetUnitPrice's own doc comment.
            var prices = new Dictionary<int, ItemPrice> { { 10, new ItemPrice { SellInstant = 100, BuyInstant = 90 } } };

            bool ok = CostLineValuation.TryGetCoinCost(lines, prices, PriceBasis.BuyOrder, out long coin);

            Assert.True(ok);
            Assert.Equal(300, coin);
        }

        [Fact]
        public void MixedCoinAndItemLines_Sums()
        {
            var lines = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 50 },
                new CostLine { Type = "Item", Id = 10, Count = 2 },
            };
            var prices = new Dictionary<int, ItemPrice> { { 10, new ItemPrice { SellInstant = 25 } } };

            bool ok = CostLineValuation.TryGetCoinCost(lines, prices, PriceBasis.BuyOrder, out long coin);

            Assert.True(ok);
            Assert.Equal(100, coin);
        }

        [Fact]
        public void NonCoinCurrencyLine_NotComparable_ReturnsFalse()
        {
            var lines = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = 2, Count = 100 }, // Karma
            };

            bool ok = CostLineValuation.TryGetCoinCost(
                lines, new Dictionary<int, ItemPrice>(), PriceBasis.BuyOrder, out long coin);

            Assert.False(ok);
            Assert.Equal(0, coin);
        }

        [Fact]
        public void UnpricedItemLine_ReturnsFalse()
        {
            var lines = new List<CostLine> { new CostLine { Type = "Item", Id = 10, Count = 1 } };

            bool ok = CostLineValuation.TryGetCoinCost(
                lines, new Dictionary<int, ItemPrice>(), PriceBasis.BuyOrder, out long coin);

            Assert.False(ok);
        }

        [Fact]
        public void UnrecognizedCostLineType_ReturnsFalse()
        {
            var lines = new List<CostLine> { new CostLine { Type = "GuildUpgrade", Id = 1, Count = 1 } };

            bool ok = CostLineValuation.TryGetCoinCost(
                lines, new Dictionary<int, ItemPrice>(), PriceBasis.BuyOrder, out long coin);

            Assert.False(ok);
        }

        // A failure on a LATER line (not just
        // the first) must still leave the out param at 0, not the partial
        // sum accumulated from the earlier, valid line(s) - the pre-fix
        // code only zeroed coinCost on a first-line failure (its initial
        // value), so a caller checking `ok` correctly but glancing at
        // `coin` on a false result would have seen a real-looking non-zero
        // number for a genuinely unpriceable offer.
        [Fact]
        public void FailureOnLaterLine_OutParamResetToZero_NotPartialSum()
        {
            var lines = new List<CostLine>
            {
                new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 500 },
                new CostLine { Type = "Currency", Id = 2, Count = 10 }, // Karma - fails
            };

            bool ok = CostLineValuation.TryGetCoinCost(
                lines, new Dictionary<int, ItemPrice>(), PriceBasis.BuyOrder, out long coin);

            Assert.False(ok);
            Assert.Equal(0, coin);
        }

        [Fact]
        public void EmptyOrNullCostLines_ReturnsFalse()
        {
            Assert.False(CostLineValuation.TryGetCoinCost(
                null, new Dictionary<int, ItemPrice>(), PriceBasis.BuyOrder, out _));
            Assert.False(CostLineValuation.TryGetCoinCost(
                new List<CostLine>(), new Dictionary<int, ItemPrice>(), PriceBasis.BuyOrder, out _));
        }
    }
}
