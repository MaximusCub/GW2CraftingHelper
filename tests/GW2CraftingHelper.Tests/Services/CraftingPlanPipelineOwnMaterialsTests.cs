using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingPlanPipelineOwnMaterialsTests
    {
        // --- Own-materials valuation ---



        [Fact]
        public async Task Structured_ValuedMode_DeductsMaterialOpportunityCostFromProfit()
        {
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            // Ingredient: SellInstant=10 (opportunity-cost basis), BuyInstant=100 (craft-cost basis).
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Own 3 of the 5 needed; the other 2 are bought.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, PipelineBuilder.OwnIngredient(3), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            // Craft cost: (5 - 3) x 100 = 200
            Assert.Equal(200, result.Plan.TotalCoinCost);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(3, result.UsedMaterials[0].QuantityUsed);

            // Opportunity cost: selling 3 x 10c = 30 total; fees -2 (5%) -3 (10%) = 25 net.
            Assert.Equal(25, result.MaterialOpportunityCost);

            // Sell value (unchanged): 400 - 20 (5%) - 40 (10%) = 340
            Assert.Equal(340, result.NetSaleValue);
            // Profit: 340 - 200 (coin cost) - 25 (opportunity cost) = 115
            Assert.Equal(115, result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_FreeMode_MaterialOpportunityCostNullAndProfitUnchanged()
        {
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Default mode (no ownMaterialsMode argument) - Free.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, PipelineBuilder.OwnIngredient(3), CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(200, result.Plan.TotalCoinCost);
            Assert.Null(result.MaterialOpportunityCost);
            // Profit unaffected by ownership: 340 - 200 = 140
            Assert.Equal(140, result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_ValuedMode_NoSnapshot_MaterialOpportunityCostNull()
        {
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Valued mode but nothing was reduced (no snapshot) - no owned
            // materials, so there is nothing to have forgone selling.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Empty(result.UsedMaterials);
            Assert.Null(result.MaterialOpportunityCost);
            // All 5 ingredients bought at 100 each = 500; profit = 340 - 500 = -160
            Assert.Equal(500, result.Plan.TotalCoinCost);
            Assert.Equal(-160, result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_ValuedMode_UnsellableUsedMaterial_ContributesZero()
        {
            // Two ingredients are owned and consumed: item 2 is sellable,
            // item 3 has no buy orders (SellInstant 0) and must contribute
            // 0 to the opportunity cost rather than being skipped/erroring
            // or zeroing the whole sum.
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 5 },
                    new RawIngredient { Type = "Item", Id = 3, Count = 4 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // sellable, SellInstant=10
            priceApi.AddPrice(3, buyUnitPrice: 0, sellUnitPrice: 50);   // unsellable, SellInstant=0

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Sellable Ingredient", "i.png");
            itemApi.AddItem(3, "Unsellable Ingredient", "j.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 5, Source = AccountItemIndex.SourceMaterialStorage },
                    new SnapshotItemEntry { ItemId = 3, Count = 4, Source = AccountItemIndex.SourceMaterialStorage }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, snapshot, CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(2, result.UsedMaterials.Count);

            // Only item 2's 5 units count: 5x10=50 total; fees -3 (5%) -5 (10%) = 42 net.
            // Item 3 contributes 0 despite 4 units being used.
            Assert.Equal(42, result.MaterialOpportunityCost);
        }

        [Fact]
        public async Task ResolveWithOverrides_PreservesOwnMaterialsMode()
        {
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, PipelineBuilder.OwnIngredient(3), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(OwnMaterialsMode.Valued, initial.SolveContext.OwnMaterialsMode);
            Assert.Equal(25, initial.MaterialOpportunityCost);
            Assert.Equal(115, initial.CraftingProfit);

            // A no-op local re-solve must keep valuing owned materials the
            // same way the original Generate did (context-carried, like
            // CurrencyValuation).
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, null);

            Assert.Equal(25, resolved.MaterialOpportunityCost);
            Assert.Equal(115, resolved.CraftingProfit);
        }

        [Fact]
        public async Task GenerateStructuredAsync_NoOwnMaterialsModeArgument_ContextDefaultsToFree()
        {
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, PipelineBuilder.OwnIngredient(3), CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(OwnMaterialsMode.Free, result.SolveContext.OwnMaterialsMode);
            Assert.Null(result.MaterialOpportunityCost);
        }
    }
}
