using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Recipes;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RepoFileLocator;

namespace GW2CraftingHelper.Tests.Services.Recipes
{
    /// <summary>
    /// Regression: the real,
    /// hand-edited ref/recipes_seed.json / ref/recipe_search_seed.json were
    /// previously never loaded through the production deserialization path
    /// (RecipeCacheSerializer.LoadRecipeSeed/LoadSearchSeed, used at
    /// runtime by SeededRecipeCacheStore.Load via Module.cs) by any
    /// committed test - only by a manual, discarded check. This pins the
    /// real files against silent drift, mirroring
    /// AcquisitionHintServiceTests' FindRepoFile pattern.
    /// </summary>
    public class RecipeCacheSerializerTests
    {
        [Fact]
        public void LoadRecipeSeed_ShippedSeedFile_ParsesAllRowsIncludingAchievementRecipes()
        {
            string path = FindRepoFile(Path.Combine("ref", "recipes_seed.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/recipes_seed.json by walking up from the test assembly's directory.");

            using (var stream = File.OpenRead(path))
            {
                var recipes = RecipeCacheSerializer.LoadRecipeSeed(stream);

                // Row count and bytes, both against what the seeder itself
                // recorded in ref/recipe_seed_manifest.json - see
                // ShippedSeedManifest for why the exact literal that used to
                // sit here (and the changelog of past reseeds above it) was
                // a tripwire nobody could read.
                ShippedSeedManifest.AssertRecipeSeedMatches(
                    "recipes_seed.json", recipes.Count);

                // Secondary sanity band, independent of the manifest: a
                // seeder run that agreed with its own manifest but emitted a
                // tenth of the corpus is still wrong.
                Assert.InRange(recipes.Count, 12000, 30000);

                // Amalgamated Rift Essence (recipe 14025 -> item 100930):
                // the concrete recipe that was invisible to every
                // unversioned recipe call before this fix - unversioned
                // /v2/recipes/14025 404s outright even though the recipe
                // fully exists. Currency ingredients key their id as "id"
                // (not "item_id" - the bug this fix closes).
                Assert.True(recipes.ContainsKey(14025));
                var riftEssence = recipes[14025];
                Assert.Equal(100930, riftEssence.OutputItemId);
                Assert.Equal(4, riftEssence.Ingredients.Count);
                Assert.Equal(3, riftEssence.Ingredients.Count(i => i.Type == "Currency"));
                var ectoIngredient = riftEssence.Ingredients.Single(i => i.Type == "Item");
                Assert.Equal(19721, ectoIngredient.Id);
                Assert.Equal(50, ectoIngredient.Count);

                // Infinite Trebuchet Blueprint achievement recipe.
                Assert.True(recipes.ContainsKey(-1592));
                var blueprint = recipes[-1592];
                Assert.Equal(103980, blueprint.OutputItemId);
                Assert.Equal(1, blueprint.OutputItemCount);
                Assert.Equal(8493, blueprint.AchievementId);
                Assert.Contains("Achievement", blueprint.Disciplines);
                Assert.Equal(4, blueprint.Ingredients.Count);

                var bit0 = blueprint.Ingredients.Single(i => i.Id == 103886);
                Assert.Equal(8493, bit0.AchievementId);
                Assert.Equal(0, bit0.AchievementBit);

                var bit1 = blueprint.Ingredients.Single(i => i.Id == 103834);
                Assert.Equal(8493, bit1.AchievementId);
                Assert.Equal(1, bit1.AchievementBit);

                var bit2 = blueprint.Ingredients.Single(i => i.Id == 103801);
                Assert.Equal(8493, bit2.AchievementId);
                Assert.Equal(2, bit2.AchievementBit);

                var bit3 = blueprint.Ingredients.Single(i => i.Id == 103974);
                Assert.Equal(8493, bit3.AchievementId);
                Assert.Equal(3, bit3.AchievementBit);

                // 3 Merchant-discipline sub-recipes for 3 of the 4 bits
                // (the 4th bit, item 103801, has no recipe at all - no
                // acquisition path per the recovered gw2efficiency data).
                Assert.True(recipes.ContainsKey(-1593));
                Assert.Equal(103886, recipes[-1593].OutputItemId);
                Assert.Contains("Merchant", recipes[-1593].Disciplines);

                Assert.True(recipes.ContainsKey(-1594));
                Assert.Equal(103834, recipes[-1594].OutputItemId);
                Assert.Contains("Merchant", recipes[-1594].Disciplines);

                Assert.True(recipes.ContainsKey(-1595));
                Assert.Equal(103974, recipes[-1595].OutputItemId);
                Assert.Contains("Merchant", recipes[-1595].Disciplines);

                // Every pre-existing negative-id (Mystic Forge) recipe must
                // still parse untouched by this addition.
                Assert.True(recipes.ContainsKey(-1591));
                Assert.True(recipes.ContainsKey(-1));

                // The reseed
                // that added the rows above silently dropped recipe
                // -1591's (Mystic Clover) fractional ExpectedOutputCount
                // (0.31 -> null) - tools/GW2CraftingHelper.RecipeSeeder's
                // MergeMysticForgeRecipes never copied the field from
                // ref/mystic_forge_recipes.json. RecipeService.
                // GetRecipeCachedAsync consults this seeded row before ever
                // reaching MysticForgeRecipeData's own correct value (the
                // seed always wins), so a null here silently defaults
                // craftsNeeded math to OutputItemCount (1) instead of
                // ceil(q/0.31) for every legendary chain that forges Mystic
                // Clovers. Pinned directly so a future reseed can never
                // drop this again without a red test.
                Assert.Equal(0.31, recipes[-1591].ExpectedOutputCount);
            }
        }

        [Fact]
        public void LoadRecipeSeed_ShippedSeedFile_PreservesEveryMysticForgeExpectedOutputCount()
        {
            // A defensive,
            // class-level guard (not just the single -1591 pin above) - for
            // every recipe ref/mystic_forge_recipes.json declares a
            // fractional ExpectedOutputCount for, the shipped
            // ref/recipes_seed.json row for that same id must carry the
            // identical value. Catches the same MergeMysticForgeRecipes
            // field-drop class for ANY future recipe, not just the one
            // instance a manual reseed happened to catch this time.
            string seedPath = FindRepoFile(Path.Combine("ref", "recipes_seed.json"));
            string mfPath = FindRepoFile(Path.Combine("ref", "mystic_forge_recipes.json"));
            Assert.False(string.IsNullOrEmpty(seedPath));
            Assert.False(string.IsNullOrEmpty(mfPath));

            Dictionary<int, RawRecipe> recipes;
            using (var stream = File.OpenRead(seedPath))
            {
                recipes = RecipeCacheSerializer.LoadRecipeSeed(stream);
            }

            var expectedById = new Dictionary<int, double>();
            using (var doc = JsonDocument.Parse(File.ReadAllText(mfPath)))
            {
                foreach (var entry in doc.RootElement.GetProperty("recipes").EnumerateArray())
                {
                    if (entry.TryGetProperty("expectedOutputCount", out var ev) &&
                        ev.ValueKind != JsonValueKind.Null)
                    {
                        expectedById[entry.GetProperty("id").GetInt32()] = ev.GetDouble();
                    }
                }
            }

            // Sanity: the source file must actually declare at least one
            // fractional override, or this test would pass vacuously.
            Assert.NotEmpty(expectedById);

            foreach (var kvp in expectedById)
            {
                Assert.True(
                    recipes.ContainsKey(kvp.Key),
                    $"ref/mystic_forge_recipes.json declares recipe {kvp.Key} but it is missing from the shipped seed.");
                Assert.Equal(kvp.Value, recipes[kvp.Key].ExpectedOutputCount);
            }
        }

        [Fact]
        public void LoadSearchSeed_ShippedSeedFile_HasAchievementRecipeSearchEntries()
        {
            string path = FindRepoFile(Path.Combine("ref", "recipe_search_seed.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/recipe_search_seed.json by walking up from the test assembly's directory.");

            using (var stream = File.OpenRead(path))
            {
                var searches = RecipeCacheSerializer.LoadSearchSeed(stream);

                ShippedSeedManifest.AssertRecipeSeedMatches(
                    "recipe_search_seed.json", searches.Count);
                Assert.InRange(searches.Count, 13000, 32000);

                // Amalgamated Rift Essence's search entry (item 100930):
                // previously a STALE NEGATIVE entry ("100930": []) - the
                // seeder had genuinely discovered every other recipe
                // producing this item was invisible, so it correctly (for
                // the data it could see) recorded "no known recipe". Now a
                // real mapping. Note this is populated ONLY because the
                // seeder walks the full /v2/recipes id list, not because
                // live /v2/recipes/search?output=100930 works - that
                // upstream search endpoint has its own, separate index gap
                // and returns empty even versioned (see
                // Gw2RecipeApiClient.SearchByOutputAsync's own doc comment).
                Assert.True(searches.ContainsKey(100930));
                Assert.Contains(14025, searches[100930]);

                Assert.True(searches.ContainsKey(103980));
                Assert.Contains(-1592, searches[103980]);

                Assert.True(searches.ContainsKey(103886));
                Assert.Contains(-1593, searches[103886]);

                Assert.True(searches.ContainsKey(103834));
                Assert.Contains(-1594, searches[103834]);

                Assert.True(searches.ContainsKey(103974));
                Assert.Contains(-1595, searches[103974]);

                // Item 103801 (Proof of Siege Expertise, bit 2) correctly
                // has an empty search entry - no acquisition path at all.
                Assert.True(searches.ContainsKey(103801));
                Assert.Empty(searches[103801]);
            }
        }

        // FindRepoFile comes from Helpers/RepoFileLocator.cs.
    }
}
