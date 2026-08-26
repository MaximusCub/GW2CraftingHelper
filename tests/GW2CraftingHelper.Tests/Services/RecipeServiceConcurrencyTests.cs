using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class RecipeServiceConcurrencyTests
    {
        // Deterministic graph:
        //   Root (100) -- Recipe 1
        //     +-- Mid A (200) x1 -- Recipe 2
        //     |     +-- Leaf X (300) x2
        //     |     +-- Leaf Y (301) x1
        //     +-- Mid B (201) x1 -- Recipe 3
        //           +-- Leaf Y (301) x3  (shared with Mid A)
        //           +-- Leaf Z (302) x1
        private const int Root = 100;
        private const int MidA = 200;
        private const int MidB = 201;
        private const int LeafX = 300;
        private const int LeafY = 301;
        private const int LeafZ = 302;
        private const int RecipeRoot = 1;
        private const int RecipeMidA = 2;
        private const int RecipeMidB = 3;

        private static InMemoryRecipeApiClient BuildSmallGraph()
        {
            var api = new InMemoryRecipeApiClient();

            api.AddSearchResult(Root, RecipeRoot);
            api.AddRecipe(new RawRecipe
            {
                Id = RecipeRoot,
                OutputItemId = Root,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = MidA, Count = 1 },
                    new RawIngredient { Type = "Item", Id = MidB, Count = 1 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" },
            });

            api.AddSearchResult(MidA, RecipeMidA);
            api.AddRecipe(new RawRecipe
            {
                Id = RecipeMidA,
                OutputItemId = MidA,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = LeafX, Count = 2 },
                    new RawIngredient { Type = "Item", Id = LeafY, Count = 1 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 300,
                Flags = new List<string> { "AutoLearned" },
            });

            api.AddSearchResult(MidB, RecipeMidB);
            api.AddRecipe(new RawRecipe
            {
                Id = RecipeMidB,
                OutputItemId = MidB,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = LeafY, Count = 3 },
                    new RawIngredient { Type = "Item", Id = LeafZ, Count = 1 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 300,
                Flags = new List<string> { "AutoLearned" },
            });

            // Leaves have no recipes (SearchByOutput returns empty)
            return api;
        }

        [Fact]
        public async Task PreWarmDoesNotChangeTreeStructure()
        {
            var api = BuildSmallGraph();
            var service = new RecipeService(api);

            var tree = await service.BuildTreeAsync(Root, 1, CancellationToken.None);

            // Root node
            Assert.Equal(Root, tree.Id);
            Assert.Equal(1, tree.Quantity);
            Assert.Single(tree.Recipes);

            var rootRecipe = tree.Recipes[0];
            Assert.Equal(RecipeRoot, rootRecipe.RecipeId);
            Assert.Equal(2, rootRecipe.Ingredients.Count);

            // Mid A
            var midA = rootRecipe.Ingredients[0];
            Assert.Equal(MidA, midA.Id);
            Assert.Equal(1, midA.Quantity);
            Assert.Single(midA.Recipes);

            var midARecipe = midA.Recipes[0];
            Assert.Equal(RecipeMidA, midARecipe.RecipeId);
            Assert.Equal(2, midARecipe.Ingredients.Count);

            // Leaf X under Mid A
            var leafX = midARecipe.Ingredients[0];
            Assert.Equal(LeafX, leafX.Id);
            Assert.Equal(2, leafX.Quantity);
            Assert.Empty(leafX.Recipes);

            // Leaf Y under Mid A
            var leafYA = midARecipe.Ingredients[1];
            Assert.Equal(LeafY, leafYA.Id);
            Assert.Equal(1, leafYA.Quantity);
            Assert.Empty(leafYA.Recipes);

            // Mid B
            var midB = rootRecipe.Ingredients[1];
            Assert.Equal(MidB, midB.Id);
            Assert.Equal(1, midB.Quantity);
            Assert.Single(midB.Recipes);

            var midBRecipe = midB.Recipes[0];
            Assert.Equal(RecipeMidB, midBRecipe.RecipeId);
            Assert.Equal(2, midBRecipe.Ingredients.Count);

            // Leaf Y under Mid B
            var leafYB = midBRecipe.Ingredients[0];
            Assert.Equal(LeafY, leafYB.Id);
            Assert.Equal(3, leafYB.Quantity);
            Assert.Empty(leafYB.Recipes);

            // Leaf Z under Mid B
            var leafZ = midBRecipe.Ingredients[1];
            Assert.Equal(LeafZ, leafZ.Id);
            Assert.Equal(1, leafZ.Quantity);
            Assert.Empty(leafZ.Recipes);
        }

        [Fact]
        public async Task ConcurrencyDoesNotExceedMaxDegreeOfParallelism()
        {
            // Build a wide tree: root with 8 leaf ingredients
            var api = new InMemoryRecipeApiClient { LatencyMs = 50 };

            var ingredients = new List<RawIngredient>();
            for (int i = 0; i < 8; i++)
            {
                int leafId = 500 + i;
                ingredients.Add(new RawIngredient { Type = "Item", Id = leafId, Count = 1 });
                // Leaves: no search results registered = empty list returned
            }

            api.AddSearchResult(100, 1);
            api.AddRecipe(new RawRecipe
            {
                Id = 1,
                OutputItemId = 100,
                OutputItemCount = 1,
                Ingredients = ingredients,
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" },
            });

            var service = new RecipeService(api, maxConcurrency: 3);
            await service.BuildTreeAsync(100, 1, CancellationToken.None);

            Assert.True(api.MaxObservedConcurrency <= 3,
                $"Max concurrency was {api.MaxObservedConcurrency}, expected <= 3");
            Assert.True(api.MaxObservedConcurrency >= 2,
                $"Max concurrency was {api.MaxObservedConcurrency}, expected >= 2 (parallelism should occur)");
        }

        [Fact]
        public async Task CancellationStopsPreWarm()
        {
            // Build a deep tree so pre-warm takes multiple BFS levels
            var api = new InMemoryRecipeApiClient { LatencyMs = 100 };

            // Chain: 100 -> 101 -> 102 -> 103 -> 104 (each a recipe with 1 ingredient)
            for (int i = 0; i < 5; i++)
            {
                int itemId = 100 + i;
                int recipeId = 1 + i;
                int nextItemId = 100 + i + 1;

                api.AddSearchResult(itemId, recipeId);
                api.AddRecipe(new RawRecipe
                {
                    Id = recipeId,
                    OutputItemId = itemId,
                    OutputItemCount = 1,
                    Ingredients = new List<RawIngredient>
                    {
                        new RawIngredient { Type = "Item", Id = nextItemId, Count = 1 },
                    },
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    Flags = new List<string> { "AutoLearned" },
                });
            }

            // Item 105 is a leaf (no search result)
            using (var cts = new CancellationTokenSource())
            {
                // Cancel after 150ms - enough for ~1 BFS level but not all 5
                cts.CancelAfter(150);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => new RecipeService(api).BuildTreeAsync(100, 1, cts.Token));
            }
        }
    }
}
