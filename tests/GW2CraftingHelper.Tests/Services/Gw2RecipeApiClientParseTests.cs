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
        public void ParseRecipe_RealVersionedCurrencyRecipe_UsesIdKeyForEveryIngredientType()
        {
            // KNOWN-ISSUES recipe-ingestion bug class (2026-08-15): replaces
            // a contract-mirror test that fabricated a "hypothetical"
            // explicit-type shape keyed on "item_id" - which is NOT what
            // the real API sends for a typed ingredient and is exactly the
            // wrong shape that let the original bug (unconditional
            // ing.Value<int>("item_id")) go undetected. This is the REAL,
            // byte-for-byte captured response body from
            // `curl "https://api.guildwars2.com/v2/recipes/14025?v=2026-08-15"`
            // (recipe 14025, Amalgamated Rift Essence -> item 100930; this
            // exact recipe was the one invisible to unversioned
            // /v2/recipes/14025, which 404s outright, and to unversioned
            // /v2/recipes' own id list). Every ingredient - Currency AND
            // Item alike - keys its item id as "id", never "item_id":
            // proof that the versioned schema's ingredient shape uses "id"
            // universally, not just for Currency ingredients.
            var json = @"{
                ""id"": 14025,
                ""type"": ""Refinement"",
                ""output_item_id"": 100930,
                ""output_item_count"": 1,
                ""time_to_craft_ms"": 1500,
                ""disciplines"": [
                    ""Leatherworker"", ""Armorsmith"", ""Chef"", ""Tailor"",
                    ""Artificer"", ""Weaponsmith"", ""Scribe"", ""Huntsman"",
                    ""Jeweler""
                ],
                ""min_rating"": 400,
                ""flags"": [""LearnedFromItem""],
                ""ingredients"": [
                    { ""type"": ""Currency"", ""id"": 78, ""count"": 250 },
                    { ""type"": ""Currency"", ""id"": 80, ""count"": 100 },
                    { ""type"": ""Currency"", ""id"": 79, ""count"": 50 },
                    { ""type"": ""Item"",     ""id"": 19721, ""count"": 50 }
                ],
                ""chat_link"": ""[&Cck2AAA=]""
            }";

            var recipe = Gw2RecipeApiClient.ParseRecipe(json);

            Assert.Equal(14025, recipe.Id);
            Assert.Equal(100930, recipe.OutputItemId);
            Assert.Equal(1, recipe.OutputItemCount);
            Assert.Equal(400, recipe.MinRating);
            Assert.Contains("LearnedFromItem", recipe.Flags);
            Assert.Equal(9, recipe.Disciplines.Count);
            Assert.Equal(4, recipe.Ingredients.Count);

            Assert.Equal("Currency", recipe.Ingredients[0].Type);
            Assert.Equal(78, recipe.Ingredients[0].Id);
            Assert.Equal(250, recipe.Ingredients[0].Count);

            Assert.Equal("Currency", recipe.Ingredients[1].Type);
            Assert.Equal(80, recipe.Ingredients[1].Id);
            Assert.Equal(100, recipe.Ingredients[1].Count);

            Assert.Equal("Currency", recipe.Ingredients[2].Type);
            Assert.Equal(79, recipe.Ingredients[2].Id);
            Assert.Equal(50, recipe.Ingredients[2].Count);

            Assert.Equal("Item", recipe.Ingredients[3].Type);
            Assert.Equal(19721, recipe.Ingredients[3].Id);
            Assert.Equal(50, recipe.Ingredients[3].Count);
        }

        [Fact]
        public void ParseRecipe_LegacyItemIdKey_StillParsesAsFallback()
        {
            // The fallback half of the same fix: a hypothetical row using
            // the OLD unversioned "item_id" key (e.g. an accidental
            // unversioned call, or a future regression) must still parse
            // rather than silently defaulting Id to 0. Unlike the test
            // above, this shape is deliberately hypothetical/defensive -
            // no currently-shipped seed row or live versioned response
            // uses "item_id" (verified: every ref/recipes_seed.json
            // ingredient row already uses "id").
            var json = @"{
                ""id"": 1,
                ""output_item_id"": 10,
                ""output_item_count"": 1,
                ""disciplines"": [],
                ""min_rating"": 0,
                ""flags"": [],
                ""ingredients"": [
                    { ""type"": ""Currency"", ""item_id"": 23, ""count"": 5 }
                ]
            }";

            var recipe = Gw2RecipeApiClient.ParseRecipe(json);

            Assert.Single(recipe.Ingredients);
            Assert.Equal("Currency", recipe.Ingredients[0].Type);
            Assert.Equal(23, recipe.Ingredients[0].Id);
            Assert.Equal(5, recipe.Ingredients[0].Count);
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
