using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Recipes;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services.Recipes
{
    /// <summary>
    /// The corpus probe over a REAL OverlayRecipeCacheStore in a temp
    /// directory, with the HTTP layer faked at the handler (the
    /// Gw2RecipeApiClientHttpTests idiom) - so every repair lands through
    /// the production put/flush/reload path.
    /// </summary>
    public class RecipeCorpusVerifierTests
    {
        private const int Build = 205780;

        // The GW2 API's versioned recipe shape, as the live endpoint
        // returns it.
        private const string Recipe901Json =
            "{\"id\":901,\"output_item_id\":100,\"output_item_count\":1," +
            "\"min_rating\":0,\"disciplines\":[\"Weaponsmith\"],\"flags\":[]," +
            "\"ingredients\":[{\"type\":\"Item\",\"id\":19700,\"count\":2}]}";

        private const string Recipe902Json =
            "{\"id\":902,\"output_item_id\":9000,\"output_item_count\":1," +
            "\"min_rating\":0,\"disciplines\":[],\"flags\":[]," +
            "\"ingredients\":[{\"type\":\"Item\",\"id\":19701,\"count\":1}]}";

        private sealed class RoutingHandler : HttpMessageHandler
        {
            public List<Uri> Requests { get; } = new List<Uri>();

            public Func<Uri, HttpResponseMessage> Responder { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request.RequestUri);
                return Task.FromResult(Responder(request.RequestUri));
            }
        }

        private static HttpResponseMessage Json(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };
        }

        // Corpus: recipes 1 -> item 100 and 2 -> item 200, plus the
        // negative-id forge-style recipe -5 -> item 300 that must never be
        // treated as removed by the live list (which cannot contain it).
        private static SeededRecipeCacheStore NewSeed()
        {
            var searches = new Dictionary<int, IReadOnlyList<int>>
            {
                { 100, new List<int> { 1 } },
                { 200, new List<int> { 2 } },
                { 300, new List<int> { -5 } }
            };
            var recipes = new Dictionary<int, RawRecipe>
            {
                { 1, NewRecipe(1, 100) },
                { 2, NewRecipe(2, 200) },
                { -5, NewRecipe(-5, 300) }
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

        private static CompositeRecipeCacheStore NewComposite(
            string dataDir, out OverlayRecipeCacheStore overlay)
        {
            overlay = new OverlayRecipeCacheStore(dataDir);
            overlay.Load();
            return new CompositeRecipeCacheStore(NewSeed(), overlay);
        }

        [Fact]
        public async Task Verify_FetchesNewIds_RepairsRows_AndConvertsADerivedNegativeToAPositive()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                handler.Responder = uri =>
                    uri.Query.Contains("ids=")
                        ? Json("[" + Recipe901Json + "," + Recipe902Json + "]")
                        : Json("[1,2,901,902]");

                var store = NewComposite(tmp.Path, out _);
                var repaired = new List<int>();

                // Before the probe, item 9000 is a derived negative.
                Assert.Empty(store.TryGetSearch(9000));

                var verifier = new RecipeCorpusVerifier(http, store, repaired.Add);
                var result = await verifier.VerifyAsync(
                    Build, store.GetKnownPositiveRecipeIds(), CancellationToken.None);

                Assert.Equal(CorpusVerificationStatus.Verified, result.Status);
                Assert.Equal(new[] { 901, 902 }, result.AddedRecipeIds);
                Assert.Empty(result.RemovedRecipeIds);
                Assert.Equal(2, handler.Requests.Count);
                Assert.Equal(new[] { 100, 9000 }, repaired);

                // The new recipe converted the derived negative into a
                // positive, and an existing output's row gained the new id.
                Assert.Equal(new[] { 902 }, store.TryGetSearch(9000));
                Assert.Equal(new[] { 1, 901 }, store.TryGetSearch(100));

                // Everything landed on disk through the production flush.
                var reloaded = new OverlayRecipeCacheStore(tmp.Path);
                reloaded.Load();
                Assert.NotNull(reloaded.TryGetRecipe(901));
                Assert.NotNull(reloaded.TryGetRecipe(902));
                Assert.Equal(new[] { 1, 901 }, reloaded.TryGetSearch(100));
                Assert.Equal(new[] { 902 }, reloaded.TryGetSearch(9000));
                Assert.Equal(Build, reloaded.NegativesVerifiedBuildId);
                Assert.Equal(4, reloaded.VerifiedKnownRecipeCount);
            }
        }

        [Fact]
        public async Task Verify_GreenProbe_IssuesExactlyOneRequest_AndWarmRelaunchIssuesZero()
        {
            using (var tmp = new TempDirectory())
            {
                using (var handler = new RoutingHandler())
                using (var http = new HttpClient(handler))
                {
                    handler.Responder = uri => Json("[1,2]");

                    var store = NewComposite(tmp.Path, out _);
                    var verifier = new RecipeCorpusVerifier(http, store);
                    var result = await verifier.VerifyAsync(
                        Build, store.GetKnownPositiveRecipeIds(), CancellationToken.None);

                    Assert.Equal(CorpusVerificationStatus.Verified, result.Status);
                    Assert.Empty(result.AddedRecipeIds);
                    Assert.Empty(result.RemovedRecipeIds);
                    Assert.Single(handler.Requests);
                }

                // "Relaunch" inside the same patch: fresh stores off the
                // same disk, fresh verifier - the manifest cheap-out means
                // 0 requests.
                using (var handler2 = new RoutingHandler())
                using (var http2 = new HttpClient(handler2))
                {
                    handler2.Responder = uri => Json("[1,2]");

                    var store2 = NewComposite(tmp.Path, out _);
                    var verifier2 = new RecipeCorpusVerifier(http2, store2);
                    var result2 = await verifier2.VerifyAsync(
                        Build, store2.GetKnownPositiveRecipeIds(), CancellationToken.None);

                    Assert.Equal(CorpusVerificationStatus.Skipped, result2.Status);
                    Assert.Empty(handler2.Requests);
                }
            }
        }

        [Fact]
        public async Task Verify_NegativeIdRecipes_AreNeverTreatedAsRemoved()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                handler.Responder = uri => Json("[1,2]");

                var store = NewComposite(tmp.Path, out _);
                var verifier = new RecipeCorpusVerifier(http, store);
                var result = await verifier.VerifyAsync(
                    Build, store.GetKnownPositiveRecipeIds(), CancellationToken.None);

                Assert.Equal(CorpusVerificationStatus.Verified, result.Status);
                Assert.Empty(result.RemovedRecipeIds);

                // The forge recipe still resolves after the probe.
                Assert.Equal(new[] { -5 }, store.TryGetSearch(300));
                Assert.NotNull(store.TryGetRecipe(-5));
            }
        }

        [Fact]
        public async Task Verify_RemovedPositiveId_IsDroppedFromServedRows_AndReArmsTheProbe()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                // Recipe 2 is gone from the live list.
                handler.Responder = uri => Json("[1]");

                var store = NewComposite(tmp.Path, out _);
                var verifier = new RecipeCorpusVerifier(http, store);
                var result = await verifier.VerifyAsync(
                    Build, store.GetKnownPositiveRecipeIds(), CancellationToken.None);

                Assert.Equal(CorpusVerificationStatus.Verified, result.Status);
                Assert.Equal(new[] { 2 }, result.RemovedRecipeIds);

                // Served answers no longer offer the removed recipe: its
                // output falls back to the derived negative.
                Assert.Empty(store.TryGetSearch(200));
                Assert.Null(store.TryGetRecipe(2));
                Assert.NotNull(store.TryGetSearch(100));

                // The stamp records the LIVE corpus size, which the held
                // corpus does not match while the removed id is still on
                // disk - so the next launch re-runs the probe instead of
                // cheaping out and forgetting the removal.
                var store2 = NewComposite(tmp.Path, out _);
                var verifier2 = new RecipeCorpusVerifier(http, store2);
                int before = handler.Requests.Count;
                var result2 = await verifier2.VerifyAsync(
                    Build, store2.GetKnownPositiveRecipeIds(), CancellationToken.None);

                Assert.Equal(CorpusVerificationStatus.Verified, result2.Status);
                Assert.Equal(new[] { 2 }, result2.RemovedRecipeIds);
                Assert.True(handler.Requests.Count > before);
                Assert.Empty(store2.TryGetSearch(200));
            }
        }

        [Fact]
        public async Task Verify_ApiFailure_DegradesNothingServed_AndLeavesTheManifestUnstamped()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                handler.Responder = uri =>
                    new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("")
                    };

                var store = NewComposite(tmp.Path, out var overlay);
                var verifier = new RecipeCorpusVerifier(http, store);
                var result = await verifier.VerifyAsync(
                    Build, store.GetKnownPositiveRecipeIds(), CancellationToken.None);

                Assert.Equal(CorpusVerificationStatus.Failed, result.Status);
                Assert.NotNull(result.Error);

                // Positives and derived negatives are served exactly as
                // before the attempt, and nothing was stamped - the probe
                // re-arms.
                Assert.Equal(new[] { 1 }, store.TryGetSearch(100));
                Assert.Empty(store.TryGetSearch(9000));
                Assert.Equal(0, overlay.NegativesVerifiedBuildId);
            }
        }

        [Fact]
        public async Task Verify_FailureDuringTheIdsSweep_KeepsWhatLanded_ButDoesNotStamp()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                handler.Responder = uri =>
                    uri.Query.Contains("ids=")
                        ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                        {
                            Content = new StringContent("")
                        }
                        : Json("[1,2,901]");

                var store = NewComposite(tmp.Path, out var overlay);
                var verifier = new RecipeCorpusVerifier(http, store);
                var result = await verifier.VerifyAsync(
                    Build, store.GetKnownPositiveRecipeIds(), CancellationToken.None);

                Assert.Equal(CorpusVerificationStatus.Failed, result.Status);
                Assert.Equal(new[] { 1 }, store.TryGetSearch(100));
                Assert.Equal(0, overlay.NegativesVerifiedBuildId);
            }
        }

        [Fact]
        public async Task Verify_Cancellation_PropagatesAndStampsNothing()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            using (var cts = new CancellationTokenSource())
            {
                handler.Responder = uri => Json("[1,2,901,902]");
                cts.Cancel();

                var store = NewComposite(tmp.Path, out var overlay);
                var verifier = new RecipeCorpusVerifier(http, store);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => verifier.VerifyAsync(
                        Build, store.GetKnownPositiveRecipeIds(), cts.Token));

                Assert.Equal(0, overlay.NegativesVerifiedBuildId);
                Assert.Equal(new[] { 1 }, store.TryGetSearch(100));
            }
        }

        [Fact]
        public async Task GetRecipesAsync_ChunksRequestsAt200Ids()
        {
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                handler.Responder = uri => Json("[]");

                var client = new Gw2RecipeApiClient(http);
                var ids = Enumerable.Range(1, 201).ToList();
                await client.GetRecipesAsync(ids, CancellationToken.None);

                Assert.Equal(2, handler.Requests.Count);
                Assert.Contains("ids=1,", handler.Requests[0].Query);
                Assert.Contains("ids=201", handler.Requests[1].Query);
                Assert.All(handler.Requests, u =>
                    Assert.Contains("v=" + Gw2RecipeApiClient.SchemaVersion, u.Query));
            }
        }
    }
}
