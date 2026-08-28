using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingPlanPipelineIgnoreTests
    {
        // --- "Ignore" pill threaded through ResolveWithOverrides ---
        [Fact]
        public async Task ResolveWithOverrides_IgnoredItemIds_ZeroesIngredientCost()
        {
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out var priceApi, ingredientCount: 5);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000); // buying the target outright is far pricier - craft wins
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // BuyInstant (craft-cost basis) = 100

            // No snapshot: nothing owned via real reduction, so the baseline
            // craft cost is the full 5x100=500.
            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
            Assert.Equal(500, initial.Plan.TotalCoinCost);

            var resolved = pipeline.ResolveWithOverrides(
                initial.SolveContext, null, new HashSet<int> { 2 });

            Assert.Equal(0, resolved.Plan.TotalCoinCost);
            // Item 2 (the ignored ingredient) generates no step at all;
            // item 1 (the root) still crafts, now at zero cost.
            Assert.DoesNotContain(resolved.Plan.Steps, s => s.ItemId == 2);
            Assert.Contains(resolved.Plan.Steps, s => s.ItemId == 1 && s.Source == AcquisitionSource.Craft && s.TotalCost == 0);
            Assert.Equal(CraftingDecision.Have, resolved.CraftingTree.Children[0].Decision);
            Assert.True(resolved.CraftingTree.Children[0].IsIgnored);
        }

        [Fact]
        public async Task ResolveWithOverrides_NullIgnoredItemIds_BehavesExactlyAsBefore()
        {
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out var priceApi, ingredientCount: 5);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // BuyInstant (craft-cost basis) = 100

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, null);

            Assert.Equal(500, resolved.Plan.TotalCoinCost);
            Assert.False(resolved.CraftingTree.Children[0].IsIgnored);
        }

        [Fact]
        public async Task ResolveWithOverrides_IgnoredItemIds_ManualOverrideOnSameNodeStillApplies()
        {
            // Ignore and the craft/buy override pill are documented as
            // orthogonal (r2 report Section 3.2) - overriding the ROOT to
            // BuyFromTp while its ingredient is separately ignored must
            // still switch the root to BuyFromTp; the two mechanisms key
            // off different things (NodeId vs ItemId) and must not collide.
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out var priceApi, ingredientCount: 5);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // BuyInstant (craft-cost basis) = 100

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
            int rootNodeId = initial.SolveContext.Tree.NodeId;

            var overrides = new Dictionary<int, AcquisitionSource> { { rootNodeId, AcquisitionSource.BuyFromTp } };
            var resolved = pipeline.ResolveWithOverrides(
                initial.SolveContext, overrides, new HashSet<int> { 2 });

            Assert.Equal(CraftingDecision.BuyFromTp, resolved.CraftingTree.Decision);
            Assert.Equal(20000, resolved.Plan.TotalCoinCost); // manual override wins on the root regardless of the sibling Ignore
        }

        [Fact]
        public async Task Structured_ValuedMode_UsedMaterialPrices_AlreadyCoveredByTreeFetch()
        {
            // Design assertion: prices are fetched for
            // allItemIds, which is collected from the PRE-reduction tree
            // (Step 2 runs before Step 6's reduction), so every used
            // material - being a tree item that reduction happened to
            // remove - already has a price entry by the time
            // ApplySellSideEconomics runs. No separate fetch is needed for
            // MaterialOpportunityCost, and this test pins that: the used
            // material's price came from the ordinary tree price fetch.
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Own ALL of the required ingredient, so nothing is left to buy
            // for item 2 (any remaining step is a zero-quantity/zero-cost
            // placeholder) - its only real trace is UsedMaterials. If its
            // price had to be fetched specially for the opportunity-cost
            // calc rather than coming from the tree-wide fetch, this would
            // be null/0 instead of the expected net value.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, PipelineBuilder.OwnIngredient(5), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 2 && s.Quantity > 0);
            Assert.Equal(5, result.UsedMaterials[0].QuantityUsed);

            // 5x10=50 total; fees -3 (5%) -5 (10%) = 42 net.
            Assert.Equal(42, result.MaterialOpportunityCost);
        }
    }
}
