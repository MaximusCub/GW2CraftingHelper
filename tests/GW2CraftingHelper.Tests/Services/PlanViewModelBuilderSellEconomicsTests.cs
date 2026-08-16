using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    // W4A (Total Cost section redesign): rewritten for the two-formula-band
    // shape (CostFormulaTile/ProfitFormulaTile) - see
    // PlanViewModelBuilderSummaryTests for the primary band/collapse-rule/
    // currency-table coverage. This file keeps its original name/focus
    // (sell-side economics rows and the own-materials opportunity-cost row)
    // since that is still exactly what these tests exercise, just through
    // the new row shape.
    public class PlanViewModelBuilderSellEconomicsTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        // --- Sell-side economics rows ---

        [Fact]
        public void NoSellPrice_NoProfitBandRows()
        {
            var result = MakeResult(totalCoinCost: 500);
            var vm = _builder.Build(result);

            var rows = vm.Sections[0].Rows;
            Assert.DoesNotContain(rows, r => r.RowType == PlanRowType.ProfitFormulaTile);

            var costTile = rows.Single(r => r.RowType == PlanRowType.CostFormulaTile);
            Assert.Equal("Actual Cost to Craft", costTile.Label);
            Assert.Equal(500L, costTile.CoinValue);
        }

        [Fact]
        public void SellValuePresent_AddsSellAndProfitTiles()
        {
            var result = MakeResult(totalCoinCost: 300);
            result.TargetUnitSellPrice = 400;
            result.NetSaleValue = 340;
            result.CraftingProfit = 40;

            var vm = _builder.Build(result);
            var profitTiles = vm.Sections[0].Rows.Where(r => r.RowType == PlanRowType.ProfitFormulaTile).ToList();

            Assert.Equal(3, profitTiles.Count);
            Assert.Equal("Sell Value", profitTiles[0].Label);
            Assert.Equal(340L, profitTiles[0].CoinValue);
            Assert.Equal("Profit if Sold", profitTiles[2].Label);
            Assert.Equal(40L, profitTiles[2].CoinValue);
        }

        [Fact]
        public void NegativeProfit_RendersAsLossWithAbsoluteValue()
        {
            var result = MakeResult(totalCoinCost: 500);
            result.NetSaleValue = 340;
            result.CraftingProfit = -160;

            var vm = _builder.Build(result);
            var profitTile = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.ProfitFormulaTile && r.Label == "Loss if Sold");

            Assert.Equal("Loss if Sold", profitTile.Label);
            Assert.Equal(160L, profitTile.CoinValue);
        }

        [Fact]
        public void CurrencyCostsPresent_ProfitTileTooltipGetsCoinOnlyQualifier()
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
            var profitTile = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.ProfitFormulaTile && r.Label == "Profit if Sold");

            Assert.Contains("coin costs only", profitTile.TooltipText);
        }

        [Fact]
        public void OverproducedBatch_SellTileTooltipShowsActualQuantity()
        {
            var result = MakeResult(targetQuantity: 1, totalCoinCost: 300);
            result.SellableQuantity = 5;
            result.NetSaleValue = 1700;
            result.CraftingProfit = 1400;

            var vm = _builder.Build(result);
            var sellTile = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.ProfitFormulaTile && r.Label == "Sell Value");

            Assert.Equal("Sell Value", sellTile.Label);
            Assert.Contains("5x", sellTile.TooltipText);
        }

        [Fact]
        public void BuyOrderBasis_ActualCostTileTooltipLabeled()
        {
            var result = MakeResult(totalCoinCost: 100);
            result.PriceBasis = PriceBasis.BuyOrder;

            var vm = _builder.Build(result);
            var costTile = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CostFormulaTile);

            Assert.Equal("Actual Cost to Craft", costTile.Label);
            Assert.Contains("buy-order prices", costTile.TooltipText);
        }

        // --- Own-materials opportunity cost (M28) - now the cost band's
        // middle "Your Materials Used" tile, per the W4A collapse rule ---

        [Fact]
        public void MaterialOpportunityCostPositive_ExpandsCostBandToThreeTiles()
        {
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 25;
            result.NetSaleValue = 340;
            result.CraftingProfit = 115;

            var vm = _builder.Build(result);
            var costTiles = vm.Sections[0].Rows.Where(r => r.RowType == PlanRowType.CostFormulaTile).ToList();

            Assert.Equal(3, costTiles.Count);
            Assert.Equal("Total Materials Value", costTiles[0].Label);
            Assert.Equal(225L, costTiles[0].CoinValue);
            Assert.Equal("Your Materials Used", costTiles[1].Label);
            Assert.Equal(25L, costTiles[1].CoinValue);
            Assert.Equal("Actual Cost to Craft", costTiles[2].Label);
            Assert.Equal(200L, costTiles[2].CoinValue);

            var profitTile = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.ProfitFormulaTile && r.Label == "Profit if Sold");
            Assert.Equal(115L, profitTile.CoinValue);
        }

        [Fact]
        public void MaterialOpportunityCostPositive_NoSellPrice_StillExpandsCostBand()
        {
            // MaterialOpportunityCost can be populated even when the target
            // has no live sell price (NetSaleValue/CraftingProfit stay
            // null) - the cost band is not gated on target sellability.
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 25;

            var vm = _builder.Build(result);
            var costTiles = vm.Sections[0].Rows.Where(r => r.RowType == PlanRowType.CostFormulaTile).ToList();

            Assert.Equal(3, costTiles.Count);
            Assert.Equal("Your Materials Used", costTiles[1].Label);
            Assert.Equal(25L, costTiles[1].CoinValue);
            Assert.DoesNotContain(vm.Sections[0].Rows, r => r.RowType == PlanRowType.ProfitFormulaTile);
        }

        [Fact]
        public void MaterialOpportunityCostZero_CostBandStaysCollapsed()
        {
            // All used materials were unsellable - the sum is 0, not null,
            // but a 0-value middle term still collapses the band.
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 0;

            var vm = _builder.Build(result);
            var costTiles = vm.Sections[0].Rows.Where(r => r.RowType == PlanRowType.CostFormulaTile).ToList();

            Assert.Single(costTiles);
            Assert.Equal("Actual Cost to Craft", costTiles[0].Label);
        }

        [Fact]
        public void MaterialOpportunityCostNull_CostBandStaysCollapsed()
        {
            // Free mode (default) - MaterialOpportunityCost is never set.
            var result = MakeResult(totalCoinCost: 200);

            var vm = _builder.Build(result);
            var costTiles = vm.Sections[0].Rows.Where(r => r.RowType == PlanRowType.CostFormulaTile).ToList();

            Assert.Single(costTiles);
            Assert.Equal("Actual Cost to Craft", costTiles[0].Label);
        }
    }
}
