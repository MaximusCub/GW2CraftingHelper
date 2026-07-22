using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Direct unit tests on the SellSideEconomics statics (M38 WP-12,
    /// KNOWN-ISSUES #25) - the move out of CraftingPlanPipeline made these
    /// directly testable without going through the whole pipeline. The
    /// pre-existing CraftingPlanPipelineTests/MultiItemPlanTests byte-
    /// identical assertions remain the regression net for the move itself;
    /// these tests target the arithmetic on its own.
    /// </summary>
    public class SellSideEconomicsTests
    {
        [Fact]
        public void ComputeMaterialOpportunityCost_ModeGatesWhetherOwnedMaterialsAreValued()
        {
            var usedMaterials = new List<UsedMaterial>
            {
                new UsedMaterial { ItemId = 1, QuantityUsed = 4 },
                new UsedMaterial { ItemId = 2, QuantityUsed = 1 } // no price entry -> contributes 0
            };
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { SellInstant = 10 } }
            };

            // Free mode: reduction never has an opportunity cost.
            Assert.Null(SellSideEconomics.ComputeMaterialOpportunityCost(
                usedMaterials, prices, OwnMaterialsMode.Free));

            // Valued mode: sums NetSaleRevenue over priced materials; an
            // unsellable material (no live sell price) contributes 0
            // rather than being excluded from the sum entirely.
            // Item 1: 4 units @ 10c = 40c total; -2 listing -4 exchange = 34.
            // Item 2: no price entry -> contributes 0.
            long? valued = SellSideEconomics.ComputeMaterialOpportunityCost(
                usedMaterials, prices, OwnMaterialsMode.Valued);
            Assert.Equal(34, valued);

            // Null/empty usedMaterials always yields null, even in Valued mode.
            Assert.Null(SellSideEconomics.ComputeMaterialOpportunityCost(
                null, prices, OwnMaterialsMode.Valued));
            Assert.Null(SellSideEconomics.ComputeMaterialOpportunityCost(
                new List<UsedMaterial>(), prices, OwnMaterialsMode.Valued));
        }

        [Fact]
        public void ComputePerItemEconomics_RecipeOverProduces_BumpsSellableQuantityPastRequested()
        {
            // Root recipe crafts 4 times at 3 output each = 12, but only
            // 10 were requested - the extra 2 must be sellable too, and
            // the craft cost attributed is the root's own committed
            // SolverDecision.TotalCost, not derived here.
            var itemRoot = Craftable(50, 10, Option(9, outputCount: 3, craftsNeeded: 4));
            itemRoot.NodeId = 5;

            var solveResult = new SolveResult
            {
                Plan = new CraftingPlan(),
                Decisions = new Dictionary<int, SolverDecision>
                {
                    { 5, new SolverDecision { Source = AcquisitionSource.Craft, RecipeId = 9, TotalCost = 777 } }
                }
            };
            var prices = new Dictionary<int, ItemPrice>
            {
                { 50, new ItemPrice { SellInstant = 20 } }
            };

            var economics = SellSideEconomics.ComputePerItemEconomics(
                itemRoot, itemId: 50, requestedQuantity: 10, solveResult, prices);

            Assert.Equal(12, economics.SellableQuantity);
            Assert.Equal(777, economics.ItemCraftCost);
            Assert.Equal(20, economics.TargetUnitSellPrice);
            // 12 units @ 20c = 240c total; -12 listing -24 exchange = 204.
            Assert.Equal(204, economics.NetSaleValue);
        }

        [Fact]
        public void ApplyBatchSellSideEconomics_MixedSellableAndUnsellableRoots_SumsOnlySellableIntoBatchTotals()
        {
            // Two requested roots under the synthetic multi-item wrapper:
            // root1 has a live TP sell price and contributes; root2 has
            // none and must contribute NOTHING - not even its own craft
            // cost - per the method's documented "excluded entirely" rule.
            var root1 = Craftable(100, 5, Option(1, outputCount: 1, craftsNeeded: 5));
            root1.NodeId = 10;
            var root2 = Craftable(200, 3, Option(2, outputCount: 1, craftsNeeded: 3));
            root2.NodeId = 20;
            var wrapperTree = WrapperOf(root1, root2);

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 100, Quantity = 5 },
                new PlanRequestItem { ItemId = 200, Quantity = 3 }
            };

            var solveResult = new SolveResult
            {
                Plan = new CraftingPlan { TotalCoinCost = 999 },
                Decisions = new Dictionary<int, SolverDecision>
                {
                    { 10, new SolverDecision { Source = AcquisitionSource.Craft, RecipeId = 1, TotalCost = 400 } },
                    { 20, new SolverDecision { Source = AcquisitionSource.Craft, RecipeId = 2, TotalCost = 150 } }
                }
            };

            // Only root1's item (100) has a live sell price; root2's item
            // (200) has none, so it must be excluded entirely.
            var prices = new Dictionary<int, ItemPrice>
            {
                { 100, new ItemPrice { SellInstant = 100 } },
                { 300, new ItemPrice { SellInstant = 50 } }
            };

            var usedMaterials = new List<UsedMaterial>
            {
                new UsedMaterial { ItemId = 300, QuantityUsed = 2 }
            };

            var result = new CraftingPlanResult();

            SellSideEconomics.ApplyBatchSellSideEconomics(
                result, wrapperTree, solveResult, prices, items,
                PriceBasis.BuyOrder, usedMaterials, OwnMaterialsMode.Valued);

            // root1: 5 units @ 100c = 500c total; -25 listing -50 exchange = 425.
            // Material 300: 2 units @ 50c = 100c total; -5 listing -10 exchange = 85.
            // Profit = 425 (root1 net sale) - 400 (root1 craft cost) - 85 (material
            // opportunity cost) = -60. root2 contributes nothing (no live price).
            Assert.Equal(PriceBasis.BuyOrder, result.PriceBasis);
            Assert.Equal(5, result.SellableQuantity);
            Assert.Equal(425, result.NetSaleValue);
            Assert.Equal(85, result.MaterialOpportunityCost);
            Assert.Equal(-60, result.CraftingProfit);
        }
    }
}
