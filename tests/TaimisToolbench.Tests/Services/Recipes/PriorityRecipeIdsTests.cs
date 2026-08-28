using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TaimisToolbench.Services;
using TaimisToolbench.Services.Recipes;
using Xunit;

namespace TaimisToolbench.Tests.Services.Recipes
{
    /// <summary>
    /// The sweep's ordering input, over a real seeded store: which recipe
    /// ids are reachable from the items the user depends on.
    /// </summary>
    public class PriorityRecipeIdsTests
    {
        // A two-level tree: item 100 is made by recipe 1 from item 200,
        // which is made by recipe 2 from raw item 300. Recipe 3 makes an
        // unrelated item nothing points at, and recipe -5 is a
        // hand-authored Mystic Forge row.
        private static SeededRecipeCacheStore Seed()
        {
            var recipes = new Dictionary<int, RawRecipe>
            {
                { 1, Recipe(1, 100, Ing("Item", 200, 2), Ing("Currency", 78, 50)) },
                { 2, Recipe(2, 200, Ing("Item", 300, 4)) },
                { 3, Recipe(3, 900, Ing("Item", 300, 1)) },
                { -5, Recipe(-5, 100, Ing("Item", 300, 1)) },
            };

            var searches = new Dictionary<int, IReadOnlyList<int>>
            {
                { 100, new List<int> { 1, -5 } },
                { 200, new List<int> { 2 } },
                { 900, new List<int> { 3 } },
            };

            var seed = new SeededRecipeCacheStore();
            using (var s1 = new MemoryStream(
                Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeSearches(searches))))
            using (var s2 = new MemoryStream(
                Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeRecipes(recipes))))
            {
                seed.Load(s1, s2);
            }

            seed.FinalizeIndex();
            return seed;
        }

        private static RawRecipe Recipe(
            int id, int outputItemId, params RawIngredient[] ingredients)
        {
            return new RawRecipe
            {
                Id = id,
                OutputItemId = outputItemId,
                OutputItemCount = 1,
                Ingredients = ingredients.ToList(),
                Disciplines = new List<string>(),
                Flags = new List<string>(),
            };
        }

        private static RawIngredient Ing(string type, int id, int count)
        {
            return new RawIngredient { Type = type, Id = id, Count = count };
        }

        [Fact]
        public void WalksIngredientsTransitively_SoADeepStaleRowIsPrioritisedToo()
        {
            var ids = PriorityRecipeIds.FromItemIds(Seed(), new[] { 100 });

            // Recipe 2 is reached only through recipe 1's item ingredient.
            Assert.Contains(1, ids);
            Assert.Contains(2, ids);

            // Nothing reachable from item 100 makes item 900.
            Assert.DoesNotContain(3, ids);
        }

        [Fact]
        public void SkipsNegativeIds_WhichTheLiveApiHasNoRecipeFor()
        {
            var ids = PriorityRecipeIds.FromItemIds(Seed(), new[] { 100 });

            Assert.DoesNotContain(-5, ids);
            Assert.All(ids, id => Assert.True(id > 0));
        }

        [Fact]
        public void NeverFollowsCurrencyIngredientsAsItems()
        {
            // Currency 78 shares its number with no recipe here, but the
            // walk must not enqueue it as an item at all - currency ids are
            // a different id space (KNOWN-ISSUES #54's bug class).
            var ids = PriorityRecipeIds.FromItemIds(Seed(), new[] { 100 });

            Assert.Equal(new[] { 1, 2 }, ids);
        }

        [Fact]
        public void DeduplicatesAcrossOverlappingSources()
        {
            // The watchlist, the plan and history routinely name the same
            // item; the sweep must not pay for it three times.
            var ids = PriorityRecipeIds.FromItemIds(Seed(), new[] { 100, 200, 100, 200 });

            Assert.Equal(new[] { 1, 2 }, ids);
        }

        [Fact]
        public void ToleratesUnknownAndNonPositiveItemIds()
        {
            var ids = PriorityRecipeIds.FromItemIds(Seed(), new[] { 0, -1, 424242, 100 });

            Assert.Equal(new[] { 1, 2 }, ids);
        }

        [Fact]
        public void ReturnsEmptyRatherThanThrowing_WhenThereIsNothingToWalk()
        {
            Assert.Empty(PriorityRecipeIds.FromItemIds(Seed(), null));
            Assert.Empty(PriorityRecipeIds.FromItemIds(null, new[] { 100 }));
            Assert.Empty(PriorityRecipeIds.FromItemIds(Seed(), new int[0]));
        }
    }
}
