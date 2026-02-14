using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class RecipeServiceTests
    {
        [Fact]
        public async Task LeafNode_NoRecipe_ReturnsLeafWithQuantity()
        {
            var api = new InMemoryRecipeApiClient();
            var svc = new RecipeService(api);

            var node = await svc.BuildTreeAsync(100, 5, CancellationToken.None);

            Assert.Equal(100, node.Id);
            Assert.Equal("Item", node.IngredientType);
            Assert.Equal(5, node.Quantity);
            Assert.True(node.IsLeaf);
            Assert.Empty(node.Recipes);
        }

        [Fact]
        public async Task SingleLevelRecipe_IngredientsAreLeaves()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 },
                    new RawIngredient { Type = "Item", Id = 3, Count = 1 }
                }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            Assert.False(node.IsLeaf);
            Assert.Single(node.Recipes);

            var option = node.Recipes[0];
            Assert.Equal(10, option.RecipeId);
            Assert.Equal(1, option.OutputCount);
            Assert.Equal(1, option.CraftsNeeded);
            Assert.Equal(2, option.Ingredients.Count);

            Assert.Equal(2, option.Ingredients[0].Id);
            Assert.Equal(3, option.Ingredients[0].Quantity);
            Assert.True(option.Ingredients[0].IsLeaf);

            Assert.Equal(3, option.Ingredients[1].Id);
            Assert.Equal(1, option.Ingredients[1].Quantity);
            Assert.True(option.Ingredients[1].IsLeaf);
        }

        [Fact]
        public async Task MultiLevelChain_ThreeLevelsDeep()
        {
            var api = new InMemoryRecipeApiClient();

            // A (item 1) -> recipe 10 -> ingredient B (item 2)
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
            });

            // B (item 2) -> recipe 20 -> ingredient C (item 3, leaf)
            api.AddSearchResult(2, 20);
            api.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            // Level 1: A
            Assert.False(node.IsLeaf);
            var bNode = node.Recipes[0].Ingredients[0];

            // Level 2: B
            Assert.Equal(2, bNode.Id);
            Assert.False(bNode.IsLeaf);
            var cNode = bNode.Recipes[0].Ingredients[0];

            // Level 3: C (leaf)
            Assert.Equal(3, cNode.Id);
            Assert.Equal(2, cNode.Quantity);
            Assert.True(cNode.IsLeaf);
        }

        [Fact]
        public async Task QuantityPropagation_CeilDivision()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 2,  // makes 2 per craft
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 4 }
                }
            });

            var svc = new RecipeService(api);
            // Need 3, recipe makes 2 -> ceil(3/2) = 2 crafts
            var node = await svc.BuildTreeAsync(1, 3, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.Equal(2, option.CraftsNeeded);
            // 2 crafts * 4 per craft = 8
            Assert.Equal(8, option.Ingredients[0].Quantity);
        }

        [Fact]
        public async Task MultipleRecipes_BothPresent()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10, 11);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
            });
            api.AddRecipe(new RawRecipe
            {
                Id = 11,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            Assert.Equal(2, node.Recipes.Count);
            Assert.Equal(10, node.Recipes[0].RecipeId);
            Assert.Equal(11, node.Recipes[1].RecipeId);
        }

        [Fact]
        public async Task CurrencyIngredient_IsLeaf()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 },
                    new RawIngredient { Type = "Currency", Id = 99, Count = 50 }
                }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            var currencyNode = node.Recipes[0].Ingredients[1];
            Assert.Equal(99, currencyNode.Id);
            Assert.Equal("Currency", currencyNode.IngredientType);
            Assert.Equal(50, currencyNode.Quantity);
            Assert.True(currencyNode.IsLeaf);
        }

        [Fact]
        public async Task OutputCountGreaterThanOne_CraftsNeededRoundsUp()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 5,  // makes 5 per craft
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 }
                }
            });

            var svc = new RecipeService(api);
            // Need 7, recipe makes 5 -> ceil(7/5) = 2 crafts
            var node = await svc.BuildTreeAsync(1, 7, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.Equal(5, option.OutputCount);
            Assert.Equal(2, option.CraftsNeeded);
            // 2 crafts * 3 per craft = 6
            Assert.Equal(6, option.Ingredients[0].Quantity);
        }

        [Fact]
        public async Task RecipeOption_CarriesDisciplinesFromRawRecipe()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Disciplines = new List<string> { "Weaponsmith", "Huntsman" }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.Equal(2, option.Disciplines.Count);
            Assert.Contains("Weaponsmith", option.Disciplines);
            Assert.Contains("Huntsman", option.Disciplines);
        }

        [Fact]
        public async Task RecipeOption_CarriesMinRatingAndFlags()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Disciplines = new List<string> { "Armorsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.Equal(400, option.MinRating);
            Assert.Single(option.Flags);
            Assert.Contains("AutoLearned", option.Flags);
        }

        [Fact]
        public async Task RecipeOption_DefaultsWhenFieldsAbsent()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
                // No Disciplines, MinRating, or Flags set — use defaults
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.Empty(option.Disciplines);
            Assert.Equal(0, option.MinRating);
            Assert.Empty(option.Flags);
        }

        [Fact]
        public async Task RecipeOption_MissingFlags_DefaultsToNotAutoLearned()
        {
            var api = new InMemoryRecipeApiClient();
            api.AddSearchResult(1, 10);
            api.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Flags = new List<string>()  // empty flags
            });

            var svc = new RecipeService(api);
            var node = await svc.BuildTreeAsync(1, 1, CancellationToken.None);

            var option = node.Recipes[0];
            Assert.DoesNotContain("AutoLearned", option.Flags);
        }
    }
}
