using System.Collections.Generic;
using System.IO;
using System.Linq;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Recipes;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RepoFileLocator;

namespace GW2CraftingHelper.Tests.Services.Recipes
{
    /// <summary>
    /// Runs SeededRecipeCacheStore.FinalizeIndex over the REAL shipped
    /// seed corpus (the MysticForgeSeedStalenessTests precedent of testing
    /// against what actually ships), pinning that the pass is a pure
    /// removal of stored negatives: it drops exactly the empty rows and
    /// leaves one row per distinct output item of the recipes the module
    /// holds. Measured on the 2026-08 seed: 16,024 rows in, 1,219 empty
    /// rows dropped, 0 rows added, 14,805 rows out. The assertions below
    /// are structural rather than those literals, so a legitimate reseed
    /// moves them together (see ShippedSeedManifest for why).
    /// </summary>
    public class SeedFinalizeIndexTests
    {
        private const int GiftOfRays = 107040;
        private const int GiftOfRaysRecipe = -1587;

        [Fact]
        public void FinalizeIndex_OnTheShippedSeed_DropsEmptyRowsAndAddsNothing()
        {
            string searchPath = Locate(Path.Combine("ref", "recipe_search_seed.json"));
            string recipesPath = Locate(Path.Combine("ref", "recipes_seed.json"));
            string mfPath = Locate(Path.Combine("ref", "mystic_forge_recipes.json"));

            // The same production loaders the store itself uses, run
            // separately so the test can see the pre-pass shape.
            Dictionary<int, IReadOnlyList<int>> rawSearches;
            using (var stream = File.OpenRead(searchPath))
            {
                rawSearches = RecipeCacheSerializer.LoadSearchSeed(stream);
            }

            Dictionary<int, RawRecipe> rawRecipes;
            using (var stream = File.OpenRead(recipesPath))
            {
                rawRecipes = RecipeCacheSerializer.LoadRecipeSeed(stream);
            }

            MysticForgeRecipeData mfData;
            using (var stream = File.OpenRead(mfPath))
            {
                mfData = MysticForgeRecipeData.Load(stream);
            }

            var store = new SeededRecipeCacheStore();
            using (var s1 = File.OpenRead(searchPath))
            using (var s2 = File.OpenRead(recipesPath))
            {
                store.Load(s1, s2);
            }

            store.MergeMysticForgeRecipes(mfData);

            // Gift of Rays resolves through its forge row before the pass...
            Assert.Contains(GiftOfRaysRecipe, store.TryGetSearch(GiftOfRays));

            store.FinalizeIndex();

            // ...and still does after it.
            Assert.Contains(GiftOfRaysRecipe, store.TryGetSearch(GiftOfRays));

            var emptyKeys = rawSearches
                .Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key)
                .ToList();
            var nonEmptyKeys = new HashSet<int>(
                rawSearches.Keys.Except(emptyKeys));

            // The seeder does write stored negatives today; if this ever
            // goes to zero the pass has nothing left to migrate.
            Assert.NotEmpty(emptyKeys);

            // Every surviving row is an output of a held recipe and every
            // held recipe's output has a row: the fill added no rows the
            // corpus does not back, and the drop removed only empties.
            var outputs = new HashSet<int>(
                rawRecipes.Values.Select(r => r.OutputItemId));
            outputs.UnionWith(mfData.AllRecipes.Select(r => r.OutputItemId));
            Assert.Subset(outputs, nonEmptyKeys);
            Assert.Equal(outputs.Count, store.SearchRowCount);

            foreach (int key in emptyKeys)
            {
                Assert.Null(store.TryGetSearch(key));
            }
        }

        private static string Locate(string relativePath)
        {
            string path = FindRepoFile(relativePath);
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate " + relativePath.Replace('\\', '/')
                    + " by walking up from the test assembly's directory.");
            return path;
        }
    }
}
