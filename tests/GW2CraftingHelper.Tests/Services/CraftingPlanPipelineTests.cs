using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingPlanPipelineTests
    {
        [Fact]
        public async Task SimpleCraftableItem_ProducesPlanWithStepsAndMetadata()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item", "target.png");
            itemApi.AddItem(2, "Ingredient", "ingredient.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateAsync(1, 1, CancellationToken.None);

            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);
            Assert.NotNull(result.ItemMetadata);
            Assert.True(result.ItemMetadata.ContainsKey(1));
            Assert.Equal("Target Item", result.ItemMetadata[1].Name);
        }

        [Fact]
        public async Task LeafOnlyItem_ProducesSingleBuyStep()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Copper Ore", "copper.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateAsync(1, 5, CancellationToken.None);

            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
            Assert.Equal(5, result.Plan.Steps[0].Quantity);
            Assert.True(result.ItemMetadata.ContainsKey(1));
        }

        [Fact]
        public async Task AllStepItemIds_HaveMetadataPopulated()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 },
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 10000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(3, buyUnitPrice: 20, sellUnitPrice: 200);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Final Item", "final.png");
            itemApi.AddItem(2, "Part A", "a.png");
            itemApi.AddItem(3, "Part B", "b.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateAsync(1, 1, CancellationToken.None);

            foreach (var step in result.Plan.Steps)
            {
                Assert.True(result.ItemMetadata.ContainsKey(step.ItemId),
                    $"Missing metadata for item {step.ItemId}");
            }
        }

        [Fact]
        public async Task MissingItemMetadata_StillProducesValidPlan()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            // No metadata for item 1

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateAsync(1, 1, CancellationToken.None);

            Assert.NotNull(result.Plan);
            Assert.Single(result.Plan.Steps);
            Assert.False(result.ItemMetadata.ContainsKey(1));
        }
    }
}
