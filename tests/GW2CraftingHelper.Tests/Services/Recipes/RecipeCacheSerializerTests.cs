using System;
using System.IO;
using System.Linq;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Recipes;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RepoFileLocator;

namespace GW2CraftingHelper.Tests.Services.Recipes
{
    /// <summary>
    /// Adversarial-review fix-pass (M37, KNOWN-ISSUES #26): the real,
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

                Assert.Equal(14736, recipes.Count);

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

                Assert.Equal(15774, searches.Count);

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
