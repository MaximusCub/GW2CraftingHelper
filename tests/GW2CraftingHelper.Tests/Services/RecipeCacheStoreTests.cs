using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Recipes;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class RecipeCacheStoreTests
    {
        [Fact]
        public void SeededStore_LoadsAndServes()
        {
            // Build in-memory JSON streams
            var searches = new Dictionary<int, IReadOnlyList<int>>
            {
                { 100, new List<int> { 1, 2 } },
                { 200, new List<int> { 3 } }
            };
            var recipes = new Dictionary<int, RawRecipe>
            {
                {
                    1, new RawRecipe
                    {
                        Id = 1,
                        OutputItemId = 100,
                        OutputItemCount = 1,
                        Ingredients = new List<RawIngredient>
                        {
                            new RawIngredient { Type = "Item", Id = 200, Count = 2 }
                        },
                        Disciplines = new List<string> { "Weaponsmith" },
                        MinRating = 400,
                        Flags = new List<string> { "AutoLearned" }
                    }
                }
            };

            string searchJson = RecipeCacheSerializer.SerializeSearches(searches);
            string recipeJson = RecipeCacheSerializer.SerializeRecipes(recipes);

            var store = new SeededRecipeCacheStore();
            using (var s1 = new MemoryStream(Encoding.UTF8.GetBytes(searchJson)))
            using (var s2 = new MemoryStream(Encoding.UTF8.GetBytes(recipeJson)))
            {
                store.Load(s1, s2);
            }

            // Hits
            var searchResult = store.TryGetSearch(100);
            Assert.NotNull(searchResult);
            Assert.Equal(2, searchResult.Count);
            Assert.Equal(1, searchResult[0]);
            Assert.Equal(2, searchResult[1]);

            var recipeResult = store.TryGetRecipe(1);
            Assert.NotNull(recipeResult);
            Assert.Equal(100, recipeResult.OutputItemId);
            Assert.Single(recipeResult.Ingredients);

            // Misses
            Assert.Null(store.TryGetSearch(999));
            Assert.Null(store.TryGetRecipe(999));

            // Stats
            Assert.Equal(1, store.Stats.SearchHits);
            Assert.Equal(1, store.Stats.SearchMisses);
            Assert.Equal(1, store.Stats.RecipeHits);
            Assert.Equal(1, store.Stats.RecipeMisses);

            // Put is no-op on seeded store
            store.PutSearch(999, new List<int> { 10 });
            Assert.Null(store.TryGetSearch(999));
        }

        [Fact]
        public void CompositeCache_OverlayTakesPrecedence()
        {
            using (var tmp = new TempDirectory())
            {
                string tempDir = tmp.Path;

                // Seed has search for item 100
                var searches = new Dictionary<int, IReadOnlyList<int>>
                {
                    { 100, new List<int> { 1 } }
                };
                var recipes = new Dictionary<int, RawRecipe>
                {
                    {
                        1, new RawRecipe
                        {
                            Id = 1,
                            OutputItemId = 100,
                            OutputItemCount = 1,
                            Ingredients = new List<RawIngredient>(),
                            Disciplines = new List<string>(),
                            MinRating = 0,
                            Flags = new List<string>()
                        }
                    }
                };

                var seed = new SeededRecipeCacheStore();
                using (var s1 = new MemoryStream(
                    Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeSearches(searches))))
                using (var s2 = new MemoryStream(
                    Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeRecipes(recipes))))
                {
                    seed.Load(s1, s2);
                }

                var overlay = new OverlayRecipeCacheStore(tempDir);
                overlay.Load(currentGw2BuildId: null);

                var composite = new CompositeRecipeCacheStore(seed, overlay);

                // Seeded item returns hit via seed layer
                var result = composite.TryGetSearch(100);
                Assert.NotNull(result);
                Assert.Single(result);

                // Missing item returns null
                Assert.Null(composite.TryGetSearch(500));

                // Put goes to overlay
                composite.PutSearch(500, new List<int> { 10 });
                var overlayResult = composite.TryGetSearch(500);
                Assert.NotNull(overlayResult);
                Assert.Single(overlayResult);
                Assert.Equal(10, overlayResult[0]);

                // Overlay takes precedence over seed
                composite.PutSearch(100, new List<int> { 99, 98 });
                var overridden = composite.TryGetSearch(100);
                Assert.NotNull(overridden);
                Assert.Equal(2, overridden.Count);
                Assert.Equal(99, overridden[0]);
            }
        }

        [Fact]
        public void Overlay_Invalidates_OnBuildChange()
        {
            using (var tmp = new TempDirectory())
            {
                string tempDir = tmp.Path;

                // Create overlay with data and flush
                var overlay = new OverlayRecipeCacheStore(tempDir);
                overlay.Load(currentGw2BuildId: null);
                overlay.PutSearch(100, new List<int> { 1 });
                overlay.PutRecipe(1, new RawRecipe
                {
                    Id = 1,
                    OutputItemId = 100,
                    OutputItemCount = 1,
                    Ingredients = new List<RawIngredient>(),
                    Disciplines = new List<string>(),
                    MinRating = 0,
                    Flags = new List<string>()
                });
                overlay.Flush(force: true);

                // Verify files exist
                string cacheDir = Path.Combine(tempDir, "recipe_cache");
                Assert.True(File.Exists(Path.Combine(cacheDir, "search_overlay.json")));
                Assert.True(File.Exists(Path.Combine(cacheDir, "recipes_overlay.json")));
                Assert.True(File.Exists(Path.Combine(cacheDir, "overlay_manifest.json")));

                // Reload with same build ID (0 since null was used) - data preserved
                var overlay2 = new OverlayRecipeCacheStore(tempDir);
                overlay2.Load(currentGw2BuildId: null);
                Assert.NotNull(overlay2.TryGetSearch(100));

                // Reload with different build ID - data cleared
                var overlay3 = new OverlayRecipeCacheStore(tempDir);
                overlay3.Load(currentGw2BuildId: 12345);
                Assert.Null(overlay3.TryGetSearch(100));
                Assert.Null(overlay3.TryGetRecipe(1));
            }
        }

        [Fact]
        public async Task RecipeService_UsesCache_SkipsApiForCachedItems()
        {
            // Pre-populate a cache store with search + recipe data
            var cacheStore = new InMemoryRecipeCacheStore();

            // Root item 100 -> recipe 1 -> ingredient 200 (leaf)
            cacheStore.PutSearch(100, new List<int> { 1 });
            cacheStore.PutRecipe(1, new RawRecipe
            {
                Id = 1,
                OutputItemId = 100,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 200, Count = 2 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });
            cacheStore.PutSearch(200, Array.Empty<int>());

            // API client tracks call count
            var api = new CountingRecipeApiClient();

            var service = new RecipeService(api, cacheStore: cacheStore);
            var tree = await service.BuildTreeAsync(100, 1, CancellationToken.None);

            // Tree built correctly from cache
            Assert.Equal(100, tree.Id);
            Assert.Single(tree.Recipes);
            Assert.Equal(2, tree.Recipes[0].Ingredients[0].Quantity);

            // API was NOT called - everything came from cache
            Assert.Equal(0, api.SearchCallCount);
            Assert.Equal(0, api.RecipeCallCount);
        }

        [Fact]
        public async Task RecipeService_FallsThrough_ToApiForUncachedItems()
        {
            // Cache store has root search but NOT recipe details or leaf search
            var cacheStore = new InMemoryRecipeCacheStore();
            cacheStore.PutSearch(100, new List<int> { 1 });
            // recipe 1 and search for 200 are NOT in cache

            var api = new InMemoryRecipeApiClient();
            api.AddRecipe(new RawRecipe
            {
                Id = 1,
                OutputItemId = 100,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 200, Count = 3 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });
            // 200 is a leaf - no search results registered

            var service = new RecipeService(api, cacheStore: cacheStore);
            var tree = await service.BuildTreeAsync(100, 1, CancellationToken.None);

            // Tree built correctly via API fallback
            Assert.Equal(100, tree.Id);
            Assert.Single(tree.Recipes);
            Assert.Equal(200, tree.Recipes[0].Ingredients[0].Id);
            Assert.Equal(3, tree.Recipes[0].Ingredients[0].Quantity);

            // API was called for the uncached recipe + leaf search
            // Recipe 1 was fetched from API
            var cachedRecipe = cacheStore.TryGetRecipe(1);
            Assert.NotNull(cachedRecipe);
            Assert.Equal(100, cachedRecipe.OutputItemId);

            // Leaf search for 200 was Put into cache after API
            var leafSearch = cacheStore.TryGetSearch(200);
            Assert.NotNull(leafSearch);
            Assert.Empty(leafSearch);
        }

        [Fact]
        public void SeededStore_NegativeEntry_ReturnsNull_WhenSeedStale()
        {
            // Seed with positive entry (100 -> [1]) and negative entry (300 -> [])
            var searches = new Dictionary<int, IReadOnlyList<int>>
            {
                { 100, new List<int> { 1 } },
                { 300, new List<int>() }
            };
            var recipes = new Dictionary<int, RawRecipe>();

            var store = new SeededRecipeCacheStore();
            using (var s1 = new MemoryStream(
                Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeSearches(searches))))
            using (var s2 = new MemoryStream(
                Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeRecipes(recipes))))
            {
                store.Load(s1, s2);
            }

            var manifest = new RecipeSeedManifest
            {
                SeedVersion = 1,
                Gw2BuildId = 100,
                CreatedUtc = "2026-01-01T00:00:00Z"
            };
            using (var ms = new MemoryStream(
                Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeManifest(manifest))))
            {
                store.LoadManifest(ms);
            }

            // Mark seed as stale (different build)
            store.SetCurrentBuildId(200);
            Assert.True(store.SeedIsStale);

            // Negative entry becomes a miss when stale
            var negativeResult = store.TryGetSearch(300);
            Assert.Null(negativeResult);

            // Positive entry still returns hit
            var positiveResult = store.TryGetSearch(100);
            Assert.NotNull(positiveResult);
            Assert.Single(positiveResult);
            Assert.Equal(1, positiveResult[0]);

            // Stats: 1 hit (positive), 1 miss (stale negative)
            Assert.Equal(1, store.Stats.SearchHits);
            Assert.Equal(1, store.Stats.SearchMisses);
        }

        [Fact]
        public void SeededStore_NegativeEntry_ReturnsEmptyList_WhenSeedFresh()
        {
            // Same setup as above
            var searches = new Dictionary<int, IReadOnlyList<int>>
            {
                { 100, new List<int> { 1 } },
                { 300, new List<int>() }
            };
            var recipes = new Dictionary<int, RawRecipe>();

            var store = new SeededRecipeCacheStore();
            using (var s1 = new MemoryStream(
                Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeSearches(searches))))
            using (var s2 = new MemoryStream(
                Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeRecipes(recipes))))
            {
                store.Load(s1, s2);
            }

            var manifest = new RecipeSeedManifest
            {
                SeedVersion = 1,
                Gw2BuildId = 100,
                CreatedUtc = "2026-01-01T00:00:00Z"
            };
            using (var ms = new MemoryStream(
                Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeManifest(manifest))))
            {
                store.LoadManifest(ms);
            }

            // Same build - seed is fresh
            store.SetCurrentBuildId(100);
            Assert.False(store.SeedIsStale);

            // Negative entry is a valid hit when fresh
            var negativeResult = store.TryGetSearch(300);
            Assert.NotNull(negativeResult);
            Assert.Empty(negativeResult);

            // Positive entry still returns hit
            var positiveResult = store.TryGetSearch(100);
            Assert.NotNull(positiveResult);
            Assert.Single(positiveResult);

            // Stats: 2 hits, 0 misses
            Assert.Equal(2, store.Stats.SearchHits);
            Assert.Equal(0, store.Stats.SearchMisses);
        }

        // Minimal API client that counts calls
        private class CountingRecipeApiClient : IRecipeApiClient
        {
            private int _searchCallCount;
            private int _recipeCallCount;

            public int SearchCallCount => _searchCallCount;
            public int RecipeCallCount => _recipeCallCount;

            public Task<IReadOnlyList<int>> SearchByOutputAsync(
                int itemId, CancellationToken ct)
            {
                Interlocked.Increment(ref _searchCallCount);
                return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
            }

            public Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
            {
                Interlocked.Increment(ref _recipeCallCount);
                return Task.FromResult<RawRecipe>(null);
            }
        }
    }
}
