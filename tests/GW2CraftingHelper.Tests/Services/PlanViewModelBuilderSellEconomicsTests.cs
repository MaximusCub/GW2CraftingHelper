using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanViewModelBuilderSellEconomicsTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        // --- Sell-side economics rows ---

        [Fact]
        public void NoSellPrice_NoSellRows()
        {
            var result = MakeResult(totalCoinCost: 500);
            var vm = _builder.Build(result);

            var rows = vm.Sections[0].Rows;
            Assert.Single(rows);
            Assert.Equal("Total", rows[0].Label);
        }

        [Fact]
        public void SellValuePresent_AddsSellAndProfitRows()
        {
            var result = MakeResult(totalCoinCost: 300);
            result.TargetUnitSellPrice = 400;
            result.NetSaleValue = 340;
            result.CraftingProfit = 40;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal(3, rows.Count);
            Assert.Equal("Sell value (after 15% TP fees)", rows[1].Label);
            Assert.Equal(340L, rows[1].CoinValue);
            Assert.Equal("Profit if sold", rows[2].Label);
            Assert.Equal(40L, rows[2].CoinValue);
        }

        [Fact]
        public void NegativeProfit_RendersAsLossWithAbsoluteValue()
        {
            var result = MakeResult(totalCoinCost: 500);
            result.NetSaleValue = 340;
            result.CraftingProfit = -160;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal("Loss if sold", rows[2].Label);
            Assert.Equal(160L, rows[2].CoinValue);
        }

        [Fact]
        public void CurrencyCostsPresent_ProfitRowGetsCoinOnlyQualifier()
        {
            var result = MakeResult(
                totalCoinCost: 100,
                currencyCosts: new List<CurrencyCost>
                {
                    new CurrencyCost { CurrencyId = 2, Amount = 50 }
                });
            result.NetSaleValue = 340;
            result.CraftingProfit = 240;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal("Profit if sold (coin costs only)", rows[2].Label);
        }

        [Fact]
        public void OverproducedBatch_SellRowShowsActualQuantity()
        {
            var result = MakeResult(targetQuantity: 1, totalCoinCost: 300);
            result.SellableQuantity = 5;
            result.NetSaleValue = 1700;
            result.CraftingProfit = 1400;

            var vm = _builder.Build(result);

            Assert.Equal("Sell value (5x, after 15% TP fees)", vm.Sections[0].Rows[1].Label);
        }

        [Fact]
        public void BuyOrderBasis_TotalRowLabeled()
        {
            var result = MakeResult(totalCoinCost: 100);
            result.PriceBasis = PriceBasis.BuyOrder;

            var vm = _builder.Build(result);

            Assert.Equal("Total (buy-order prices)", vm.Sections[0].Rows[0].Label);
        }

        // --- Own-materials opportunity cost row (M28) ---

        [Fact]
        public void MaterialOpportunityCostPositive_AddsRowRightAfterTotal()
        {
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 25;
            result.NetSaleValue = 340;
            result.CraftingProfit = 115;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal(4, rows.Count);
            Assert.Equal("Total", rows[0].Label);
            Assert.Equal("Own materials (sell value forgone)", rows[1].Label);
            Assert.Equal(25L, rows[1].CoinValue);
            Assert.Equal(PlanRowType.CoinTotal, rows[1].RowType);
            Assert.Equal("Sell value (after 15% TP fees)", rows[2].Label);
            Assert.Equal("Profit if sold", rows[3].Label);
            Assert.Equal(115L, rows[3].CoinValue);
        }

        [Fact]
        public void MaterialOpportunityCostPositive_NoSellPrice_StillAddsRow()
        {
            // MaterialOpportunityCost can be populated even when the target
            // has no live sell price (NetSaleValue/CraftingProfit stay
            // null) - the row is not gated on target sellability.
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 25;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal(2, rows.Count);
            Assert.Equal("Own materials (sell value forgone)", rows[1].Label);
            Assert.Equal(25L, rows[1].CoinValue);
        }

        [Fact]
        public void MaterialOpportunityCostZero_NoRow()
        {
            // All used materials were unsellable - the sum is 0, not null,
            // but a 0-value row is not worth surfacing.
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 0;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Single(rows);
            Assert.Equal("Total", rows[0].Label);
        }

        [Fact]
        public void MaterialOpportunityCostNull_NoRow()
        {
            // Free mode (default) - MaterialOpportunityCost is never set.
            var result = MakeResult(totalCoinCost: 200);

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Single(rows);
            Assert.Equal("Total", rows[0].Label);
        }
    }
}
