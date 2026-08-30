using System.IO;
using System.Linq;
using TaimisToolbench.Services.Recipes;
using Xunit;
using static TaimisToolbench.Tests.Helpers.RepoFileLocator;

namespace TaimisToolbench.Tests.Services.Recipes
{
    /// <summary>
    /// ref/recipes_seed.json carries negative-id recipes from two producers
    /// that never see each other: the block tools/MysticForgeSeeder
    /// regenerates whole, and rows hand-authored into the seed that
    /// tools/TaimisToolbench.RecipeSeeder's Step 5a carries forward. Step 5
    /// merges the forge block first and Step 5a skips any id already taken,
    /// so an overlap does not conflict - it replaces a hand-authored row
    /// with a forge one and says nothing.
    /// <para>
    /// The producers therefore own disjoint halves: hand-authored rows take
    /// [-99999, -1], the generated block takes -100000 and below, and the
    /// generated block grows away from the hand-authored half rather than
    /// into it. This is the executable half of that partition - without it
    /// the rule lives only in a comment in a tool nobody runs on CI.
    /// </para>
    /// </summary>
    public class MysticForgeSeedIdSpaceTests
    {
        private const int GeneratedBlockBase = -100000;

        [Fact]
        public void ShippedForgeFile_UsesOnlyTheGeneratedHalfOfTheIdSpace()
        {
            using (var stream = File.OpenRead(
                Locate(Path.Combine("ref", "mystic_forge_recipes.json"))))
            {
                var data = TaimisToolbench.Services.MysticForgeRecipeData.Load(stream);

                Assert.NotEmpty(data.AllRecipes);
                Assert.All(
                    data.AllRecipes,
                    r => Assert.True(
                        r.Id <= GeneratedBlockBase,
                        "forge recipe " + r.Id + " sits in the hand-authored"
                            + " half of the negative id space"));
            }
        }

        [Fact]
        public void ShippedRecipeSeed_KeepsTheTwoProducersInDisjointHalves()
        {
            using (var stream = File.OpenRead(
                Locate(Path.Combine("ref", "recipes_seed.json"))))
            {
                var recipes = RecipeCacheSerializer.LoadRecipeSeed(stream);

                var negatives = recipes.Values.Where(r => r.Id < 0).ToList();
                var generated = negatives
                    .Where(r => r.Disciplines.Contains("MysticForge"))
                    .ToList();
                var handAuthored = negatives.Except(generated).ToList();

                Assert.NotEmpty(generated);
                Assert.NotEmpty(handAuthored);

                Assert.All(
                    generated,
                    r => Assert.True(
                        r.Id <= GeneratedBlockBase,
                        "forge recipe " + r.Id + " sits in the hand-authored"
                            + " half of the negative id space"));

                Assert.All(
                    handAuthored,
                    r => Assert.True(
                        r.Id > GeneratedBlockBase,
                        "hand-authored recipe " + r.Id + " sits in the"
                            + " generated half and a reseed will overwrite it"));
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
