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

        // This case previously pinned the defect: it flushed an overlay that
        // had never been told the live build, then asserted the manifest read
        // back as "the same build ID (0 since null was used)" - the exact
        // state that made InvalidateIfStale delete the overlay on every
        // launch, before it was ever read. It now asserts the real contract.
        [Fact]
        public void Overlay_Invalidates_OnBuildChange()
        {
            using (var tmp = new TempDirectory())
            {
                const int buildId = 205780;
                string tempDir = tmp.Path;

                // Create overlay with data, stamped with the live build, and flush
                var overlay = new OverlayRecipeCacheStore(tempDir);
                overlay.Load(currentGw2BuildId: null);
                overlay.SetCurrentBuildId(buildId);
                overlay.PutSearch(100, new List<int> { 1 });
                overlay.PutRecipe(1, NewRecipe(1, 100));
                overlay.Flush(force: true);

                // Verify files exist and the manifest carries the real build id
                string cacheDir = Path.Combine(tempDir, "recipe_cache");
                Assert.True(File.Exists(Path.Combine(cacheDir, "search_overlay.json")));
                Assert.True(File.Exists(Path.Combine(cacheDir, "recipes_overlay.json")));
                Assert.True(File.Exists(Path.Combine(cacheDir, "overlay_manifest.json")));
                Assert.Equal(buildId, ReadOverlayManifestBuildId(tempDir));

                // Reload at the same build - data preserved
                var overlay2 = new OverlayRecipeCacheStore(tempDir);
                overlay2.Load(currentGw2BuildId: null);
                overlay2.InvalidateIfStale(buildId);
                Assert.NotNull(overlay2.TryGetSearch(100));
                Assert.NotNull(overlay2.TryGetRecipe(1));

                // Reload at a different build - data cleared and files removed
                var overlay3 = new OverlayRecipeCacheStore(tempDir);
                overlay3.Load(currentGw2BuildId: null);
                overlay3.InvalidateIfStale(buildId + 1);
                Assert.Null(overlay3.TryGetSearch(100));
                Assert.Null(overlay3.TryGetRecipe(1));
                Assert.False(File.Exists(Path.Combine(cacheDir, "search_overlay.json")));
                Assert.False(File.Exists(Path.Combine(cacheDir, "recipes_overlay.json")));
                Assert.False(File.Exists(Path.Combine(cacheDir, "overlay_manifest.json")));
            }
        }

        // Walks four "sessions" in Module.cs's own order - Load(null), then
        // InvalidateIfStale(build), then SetCurrentBuildId(build) - because
        // InvalidateIfStale clears the stored build, so a stamp placed before
        // it would be discarded and the overlay would persist unstamped.
        [Fact]
        public void Overlay_SurvivesRestart_AtSameBuild_AndRestampsAfterBuildChange()
        {
            using (var tmp = new TempDirectory())
            {
                const int buildA = 205780;
                const int buildB = 205781;
                string tempDir = tmp.Path;

                var session1 = new OverlayRecipeCacheStore(tempDir);
                session1.Load(currentGw2BuildId: null);
                session1.InvalidateIfStale(buildA);
                session1.SetCurrentBuildId(buildA);
                session1.PutSearch(100, new List<int> { 1 });
                session1.PutRecipe(1, NewRecipe(1, 100));
                session1.Flush(force: true);
                Assert.Equal(buildA, ReadOverlayManifestBuildId(tempDir));

                // Restart at the same build: the overlay is served from disk,
                // and adding to it keeps the stamp.
                var session2 = new OverlayRecipeCacheStore(tempDir);
                session2.Load(currentGw2BuildId: null);
                session2.InvalidateIfStale(buildA);
                session2.SetCurrentBuildId(buildA);
                var search = session2.TryGetSearch(100);
                Assert.NotNull(search);
                Assert.Equal(1, search[0]);
                Assert.Equal(100, session2.TryGetRecipe(1).OutputItemId);
                session2.PutSearch(200, new List<int> { 2 });
                session2.PutRecipe(2, NewRecipe(2, 200));
                session2.Flush(force: true);
                Assert.Equal(buildA, ReadOverlayManifestBuildId(tempDir));

                // Restart on a new game build: the stale overlay is wiped, and
                // what the session rebuilds is stamped with the NEW build - so
                // the next restart keeps it instead of wiping again.
                var session3 = new OverlayRecipeCacheStore(tempDir);
                session3.Load(currentGw2BuildId: null);
                session3.InvalidateIfStale(buildB);
                session3.SetCurrentBuildId(buildB);
                Assert.Null(session3.TryGetSearch(100));
                Assert.Null(session3.TryGetRecipe(1));
                session3.PutSearch(300, new List<int> { 3 });
                session3.PutRecipe(3, NewRecipe(3, 300));
                session3.Flush(force: true);
                Assert.Equal(buildB, ReadOverlayManifestBuildId(tempDir));

                var session4 = new OverlayRecipeCacheStore(tempDir);
                session4.Load(currentGw2BuildId: null);
                session4.InvalidateIfStale(buildB);
                session4.SetCurrentBuildId(buildB);
                Assert.NotNull(session4.TryGetSearch(300));
                Assert.NotNull(session4.TryGetRecipe(3));
                Assert.Null(session4.TryGetSearch(100));
            }
        }

        // Module.cs cannot pass a build id to Load - it learns the live build
        // from an async /v2/build call that lands seconds later - so a plan
        // generated in those seconds must not be built from recipes cached
        // under a different build.
        [Fact]
        public void Overlay_WithheldFromReads_UntilTheBuildCheckResolves()
        {
            using (var tmp = new TempDirectory())
            {
                const int buildId = 205780;
                string tempDir = tmp.Path;

                var session1 = new OverlayRecipeCacheStore(tempDir);
                session1.Load(currentGw2BuildId: null);
                session1.SetCurrentBuildId(buildId);
                session1.PutSearch(100, new List<int> { 1 });
                session1.PutRecipe(1, NewRecipe(1, 100));
                session1.Flush(force: true);

                var session2 = new OverlayRecipeCacheStore(tempDir);
                session2.Load(currentGw2BuildId: null);

                // Vintage still unproven - a miss, exactly as if the overlay
                // were empty.
                Assert.Null(session2.TryGetSearch(100));
                Assert.Null(session2.TryGetRecipe(1));

                // Whatever this session fetched itself is current-build by
                // construction, so it is served throughout.
                session2.PutSearch(200, new List<int> { 2 });
                Assert.NotNull(session2.TryGetSearch(200));

                session2.InvalidateIfStale(buildId);

                Assert.NotNull(session2.TryGetSearch(100));
                Assert.Equal(100, session2.TryGetRecipe(1).OutputItemId);
                Assert.NotNull(session2.TryGetSearch(200));
            }
        }

        // A /v2/build call that times out or throws leaves Module.cs's
        // background task in its catch, so the overlay is never resolved at
        // all. That session must neither serve the persisted overlay nor
        // destroy it - re-stamping it with this session's fetches under the
        // OLD build id would make cross-build recipes survive indefinitely.
        [Fact]
        public void Overlay_BuildCheckNeverResolves_LeavesPersistedOverlayUntouched()
        {
            using (var tmp = new TempDirectory())
            {
                const int buildId = 205780;
                string tempDir = tmp.Path;

                var session1 = new OverlayRecipeCacheStore(tempDir);
                session1.Load(currentGw2BuildId: null);
                session1.SetCurrentBuildId(buildId);
                session1.PutSearch(100, new List<int> { 1 });
                session1.Flush(force: true);

                var session2 = new OverlayRecipeCacheStore(tempDir);
                session2.Load(currentGw2BuildId: null);
                session2.PutSearch(300, new List<int> { 3 });
                session2.Flush(force: true);

                Assert.Equal(buildId, ReadOverlayManifestBuildId(tempDir));

                // What is on disk is still session 1's overlay, unchanged and
                // still stamped with the build it was cached from.
                var session3 = new OverlayRecipeCacheStore(tempDir);
                session3.Load(currentGw2BuildId: buildId);
                Assert.NotNull(session3.TryGetSearch(100));
                Assert.Null(session3.TryGetSearch(300));
            }
        }

        // Load's own mismatch branch - the path the offline harness uses,
        // which passes the build id straight to Load instead of calling
        // InvalidateIfStale afterwards.
        [Fact]
        public void Overlay_Load_WithMismatchedBuildId_ClearsOverlay()
        {
            using (var tmp = new TempDirectory())
            {
                const int buildId = 205780;
                string tempDir = tmp.Path;

                var overlay = new OverlayRecipeCacheStore(tempDir);
                overlay.Load(currentGw2BuildId: buildId);
                overlay.SetCurrentBuildId(buildId);
                overlay.PutSearch(100, new List<int> { 1 });
                overlay.Flush(force: true);

                var sameBuild = new OverlayRecipeCacheStore(tempDir);
                sameBuild.Load(currentGw2BuildId: buildId);
                Assert.NotNull(sameBuild.TryGetSearch(100));

                var otherBuild = new OverlayRecipeCacheStore(tempDir);
                otherBuild.Load(currentGw2BuildId: 12345);
                Assert.Null(otherBuild.TryGetSearch(100));
            }
        }

        private static RawRecipe NewRecipe(int recipeId, int outputItemId)
        {
            return new RawRecipe
            {
                Id = recipeId,
                OutputItemId = outputItemId,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>(),
                Disciplines = new List<string>(),
                MinRating = 0,
                Flags = new List<string>()
            };
        }

        private static int ReadOverlayManifestBuildId(string dataDir)
        {
            string manifestPath = Path.Combine(dataDir, "recipe_cache", "overlay_manifest.json");
            using (var fs = File.OpenRead(manifestPath))
            {
                return RecipeCacheSerializer
                    .LoadManifest<RecipeOverlayManifest>(fs)
                    .Gw2BuildId;
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

        // An item the search endpoint genuinely has no recipe for is worth
        // remembering across sessions: without a persisted negative row every
        // raw material in a plan costs a live search on every launch. This
        // pins that optimization so a later change to what gets persisted
        // cannot quietly drop it.
        [Fact]
        public async Task RecipeService_ProvenEmptySearch_IsServedFromDisk_NextSession()
        {
            using (var tmp = new TempDirectory())
            {
                const int buildId = 205780;

                var overlay1 = new OverlayRecipeCacheStore(tmp.Path);
                overlay1.Load(currentGw2BuildId: null);
                overlay1.InvalidateIfStale(buildId);
                overlay1.SetCurrentBuildId(buildId);

                var api1 = new CountingRecipeApiClient();
                var tree1 = await new RecipeService(api1, cacheStore: overlay1)
                    .BuildTreeAsync(100, 1, CancellationToken.None);

                Assert.Empty(tree1.Recipes);
                Assert.Equal(1, api1.SearchCallCount);

                var overlay2 = new OverlayRecipeCacheStore(tmp.Path);
                overlay2.Load(currentGw2BuildId: null);
                overlay2.InvalidateIfStale(buildId);
                overlay2.SetCurrentBuildId(buildId);

                var api2 = new CountingRecipeApiClient();
                var tree2 = await new RecipeService(api2, cacheStore: overlay2)
                    .BuildTreeAsync(100, 1, CancellationToken.None);

                Assert.Empty(tree2.Recipes);
                Assert.Equal(0, api2.SearchCallCount);
            }
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
