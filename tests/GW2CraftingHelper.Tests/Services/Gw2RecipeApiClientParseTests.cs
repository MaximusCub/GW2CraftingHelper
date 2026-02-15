using System.Linq;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class Gw2RecipeApiClientParseTests
    {
        [Fact]
        public void ParseRecipe_IngredientsWithoutTypeField_DefaultsToItem()
        {
            // Real GW2 API JSON for recipe 7785 (Zojja's Claymore).
            // The API does NOT include a "type" field on ingredients.
            var json = @"{
                ""id"": 7785,
                ""type"": ""Greatsword"",
                ""output_item_id"": 46762,
                ""output_item_count"": 1,
                ""time_to_craft_ms"": 10000,
                ""disciplines"": [""Weaponsmith""],
                ""min_rating"": 500,
                ""flags"": [""LearnedFromItem""],
                ""ingredients"": [
                    { ""item_id"": 46695, ""count"": 1 },
                    { ""item_id"": 45847, ""count"": 1 },
                    { ""item_id"": 45855, ""count"": 1 },
                    { ""item_id"": 46746, ""count"": 1 }
                ],
                ""guild_ingredients"": []
            }";

            var recipe = Gw2RecipeApiClient.ParseRecipe(json);

            Assert.Equal(7785, recipe.Id);
            Assert.Equal(46762, recipe.OutputItemId);
            Assert.Equal(1, recipe.OutputItemCount);
            Assert.Equal(500, recipe.MinRating);
            Assert.Single(recipe.Disciplines);
            Assert.Equal("Weaponsmith", recipe.Disciplines[0]);
            Assert.Contains("LearnedFromItem", recipe.Flags);
            Assert.Equal(4, recipe.Ingredients.Count);

            // Every ingredient must default to Type = "Item"
            Assert.All(recipe.Ingredients, ing =>
                Assert.Equal("Item", ing.Type));

            // Verify individual ingredient IDs and counts
            Assert.Equal(46695, recipe.Ingredients[0].Id);
            Assert.Equal(1, recipe.Ingredients[0].Count);
            Assert.Equal(46746, recipe.Ingredients[3].Id);
        }

        [Fact]
        public void ParseRecipe_IngredientsWithExplicitType_PreservesType()
        {
            // Hypothetical recipe with explicit "type" on ingredients
            // (e.g. Mystic Forge local data or future API change).
            var json = @"{
                ""id"": 9999,
                ""output_item_id"": 100,
                ""output_item_count"": 1,
                ""disciplines"": [],
                ""min_rating"": 0,
                ""flags"": [],
                ""ingredients"": [
                    { ""type"": ""Currency"", ""item_id"": 23, ""count"": 5 },
                    { ""type"": ""Item"",     ""item_id"": 200, ""count"": 3 }
                ]
            }";

            var recipe = Gw2RecipeApiClient.ParseRecipe(json);

            Assert.Equal(2, recipe.Ingredients.Count);
            Assert.Equal("Currency", recipe.Ingredients[0].Type);
            Assert.Equal("Item", recipe.Ingredients[1].Type);
        }

        [Fact]
        public void ParseRecipe_NoIngredients_ReturnsEmptyList()
        {
            var json = @"{
                ""id"": 1,
                ""output_item_id"": 10,
                ""output_item_count"": 1,
                ""disciplines"": [],
                ""min_rating"": 0,
                ""flags"": []
            }";

            var recipe = Gw2RecipeApiClient.ParseRecipe(json);

            Assert.Empty(recipe.Ingredients);
        }
    }
}
