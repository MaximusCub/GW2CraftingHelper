using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;
using static GW2CraftingHelper.Tests.Helpers.VendorOfferBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Direct unit tests on the SellSideEconomics statics
    /// (KNOWN-ISSUES #25) - the move out of CraftingPlanPipeline made these
    /// directly testable without going through the whole pipeline. The
    /// pre-existing CraftingPlanPipelineEconomicsTests/MultiItemPlanTests
    /// byte-identical assertions remain the regression net for the move
    /// itself;
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
                new UsedMaterial { ItemId = 2, QuantityUsed = 1 }, // no price entry -> contributes 0
            };
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { SellInstant = 10 } },
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
                    { 5, new SolverDecision { Source = AcquisitionSource.Craft, RecipeId = 9, TotalCost = 777 } },
                },
            };
            var prices = new Dictionary<int, ItemPrice>
            {
                { 50, new ItemPrice { SellInstant = 20 } },
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
        public void ComputePerItemEconomics_FractionalEvRoot_UsesExpectedOutputCountNotNominalOutputCount()
        {
            // A Mystic-Clover-style root
            // recipe (nominal OutputCount 1, but ExpectedOutputCount 0.31 -
            // fractional EV) crafted 249 times to satisfy a 77-unit
            // request. The pre-fix nominal basis (CraftsNeeded *
            // OutputCount = 249 * 1 = 249) fabricated a 172-unit fake
            // surplus; the corrected EV basis (249 * 0.31 = 77.19, floored
            // to 77) matches the requested quantity almost exactly, which
            // is what a probability-adjusted expected yield should do.
            var option = Option(9, outputCount: 1, craftsNeeded: 249);
            option.ExpectedOutputCount = 0.31;
            var itemRoot = Craftable(50, 77, option);
            itemRoot.NodeId = 5;

            var solveResult = new SolveResult
            {
                Plan = new CraftingPlan(),
                Decisions = new Dictionary<int, SolverDecision>
                {
                    { 5, new SolverDecision { Source = AcquisitionSource.Craft, RecipeId = 9, TotalCost = 500 } },
                },
            };
            var prices = new Dictionary<int, ItemPrice>
            {
                { 50, new ItemPrice { SellInstant = 100 } },
            };

            var economics = SellSideEconomics.ComputePerItemEconomics(
                itemRoot, itemId: 50, requestedQuantity: 77, solveResult, prices);

            Assert.Equal(77, economics.SellableQuantity);
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
                new PlanRequestItem { ItemId = 200, Quantity = 3 },
            };

            var solveResult = new SolveResult
            {
                Plan = new CraftingPlan { TotalCoinCost = 999 },
                Decisions = new Dictionary<int, SolverDecision>
                {
                    { 10, new SolverDecision { Source = AcquisitionSource.Craft, RecipeId = 1, TotalCost = 400 } },
                    { 20, new SolverDecision { Source = AcquisitionSource.Craft, RecipeId = 2, TotalCost = 150 } },
                },
            };

            // Only root1's item (100) has a live sell price; root2's item
            // (200) has none, so it must be excluded entirely.
            var prices = new Dictionary<int, ItemPrice>
            {
                { 100, new ItemPrice { SellInstant = 100 } },
                { 300, new ItemPrice { SellInstant = 50 } },
            };

            var usedMaterials = new List<UsedMaterial>
            {
                new UsedMaterial { ItemId = 300, QuantityUsed = 2 },
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

        // --- Characterization: ApplyBatchSellSideEconomics/CraftingProfit
        // is a real downstream consumer of AllocateVendorNodeCosts' merged-
        // ceil remainder shape (quorum verdict C6, merged-ceil-remainder
        // stream) - a REAL PlanSolver.Solve() round trip (not a hand-built
        // SolveResult like the sibling tests above), since itemCraftCost
        // here must be the actual corrected memo TotalCost AllocateVendorNodeCosts
        // wrote, not a value the test invents. Item 99 is requested
        // directly as one root AND needed again inside a second root's own
        // craft (two 1-unit occurrences total, merged under the SAME
        // "100 for 1000c" bulk offer used by the VendorBatchSolver-level
        // characterization). Root 99 is the first-seen (non-last)
        // occurrence in DFS order.
        [Fact]
        public void ApplyBatchSellSideEconomics_RootIsMergedVendorLeaf_CraftingProfitUsesFairProportionalShare()
        {
            var root99 = Craftable(99, 1);
            var rootOther = Craftable(500, 1, Option(50, 1, 1, Craftable(99, 1)));
            var wrapperTree = WrapperOf(root99, rootOther);

            // No price entry at all for the solve pass: item 99 has no
            // TP-buy path (GetUnitPrice's own same-item cross-side
            // fallback means even a SellInstant-only entry would still be
            // usable as a synthetic buy cost - see GetUnitPrice's doc
            // comment - so it must be absent from THIS dictionary
            // entirely), forcing the vendor offer below to win.
            var pricesForSolve = new Dictionary<int, ItemPrice>();
            // Separate dictionary, with a live SellInstant, for the
            // economics pass below - ApplyBatchSellSideEconomics only
            // ever reads SellInstant, so reusing the solve-side prices
            // would have hidden this consumer-level characterization
            // behind the cross-side buy fallback above.
            var pricesForEconomics = new Dictionary<int, ItemPrice>
            {
                { 99, new ItemPrice { ItemId = 99, SellInstant = 1000 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 99, new List<VendorOffer> { CoinVendorOffer(99, 1000, outputCount: 100) } },
            };
            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 99, Quantity = 1 },
                new PlanRequestItem { ItemId = 500, Quantity = 1 },
            };

            var solver = new PlanSolver();
            var solveResult = solver.Solve(wrapperTree, pricesForSolve, vendorOffers);

            Assert.Equal(AcquisitionSource.BuyFromVendor, solveResult.Decisions[root99.NodeId].Source);
            // Fair proportional share (1000 * 1/2 = 500), regardless of
            // root99 being the non-last occurrence in DFS order.
            Assert.Equal(500, solveResult.Decisions[root99.NodeId].TotalCost);

            var result = new CraftingPlanResult();
            SellSideEconomics.ApplyBatchSellSideEconomics(
                result, wrapperTree, solveResult, pricesForEconomics, items,
                PriceBasis.InstantBuy, usedMaterials: null, OwnMaterialsMode.Free);

            // 1 unit @ 1000c = 1000c total; -50 listing -100 exchange = 850.
            Assert.Equal(850, result.NetSaleValue);
            // 850 - 500 = 350. Item 500 contributes nothing (no live sell
            // price).
            Assert.Equal(350, result.CraftingProfit);
        }
    }
}
