using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Tests.Helpers
{
    /// <summary>
    /// One definition of what a default test pipeline looks like.
    ///
    /// The CraftingPlanPipeline* test classes were split out of a single
    /// 4,719-line file; these fixtures are the ones used from more than one
    /// of the resulting files, so they live here rather than being copied
    /// per file. Every method returns a FRESH object graph - nothing is
    /// cached or shared between calls, so tests never contend.
    /// </summary>
    public static class PipelineBuilder
    {
        public static CraftingPlanPipeline BuildEconomicsPipeline(
            out InMemoryPriceApiClient priceApi)
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
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            priceApi = new InMemoryPriceApiClient();

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));
        }

        public static CraftingPlanPipeline BuildOwnMaterialsPipeline(
            out InMemoryPriceApiClient priceApi, int ingredientCount = 5)
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
                    new RawIngredient { Type = "Item", Id = 2, Count = ingredientCount }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            priceApi = new InMemoryPriceApiClient();

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());
        }

        public static AccountSnapshot OwnIngredient(int count)
        {
            return new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry
                    {
                        ItemId = 2,
                        Count = count,
                        Source = AccountItemIndex.SourceMaterialStorage
                    }
                }
            };
        }
    }
}
