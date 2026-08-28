using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Services.Recipes;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services.Recipes
{
    /// <summary>
    /// The phase-2 content sweep over a REAL OverlayRecipeCacheStore in a
    /// temp directory, with the HTTP layer faked at the handler
    /// (RecipeCorpusVerifierTests' idiom) - so every repair lands through
    /// the production put/flush/reload path.
    /// </summary>
    public class RecipeCorpusRefresherTests
    {
        private const int OldBuild = 205780;
        private const int NewBuild = 205999;

        // Recipe 14025 -> Amalgamated Rift Essence (KNOWN-ISSUES #48): the
        // recipe whose rift-essence ingredients the game converted from
        // items into wallet currencies in place, keeping the same recipe
        // id. Ids match the real captured row so the shape under test is
        // the one that actually shipped.
        private const int AreRecipeId = 14025;
        private const int AreOutputItem = 100930;
        private const int GlobEcto = 19721;
        private static readonly int[] AreEssenceIds = { 78, 80, 79 };
        private static readonly int[] AreEssenceCounts = { 250, 100, 50 };

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

        /// <summary>
        /// Answers ?ids= out of a live-side corpus, exactly as the API
        /// does: only the requested ids come back, in request order.
        /// </summary>
        private static HttpResponseMessage ServeIds(
            Uri uri, IReadOnlyDictionary<int, string> live)
        {
            var body = new StringBuilder("[");
            bool first = true;
            foreach (int id in RequestedIds(uri))
            {
                if (!live.TryGetValue(id, out string json))
                {
                    continue;
                }

                if (!first)
                {
                    body.Append(',');
                }

                body.Append(json);
                first = false;
            }

            body.Append(']');
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body.ToString()),
            };
        }

        private static List<int> RequestedIds(Uri uri)
        {
            var ids = new List<int>();
            foreach (string pair in uri.Query.TrimStart('?').Split('&'))
            {
                if (!pair.StartsWith("ids=", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string raw in pair.Substring(4).Split(','))
                {
                    if (int.TryParse(raw, out int id))
                    {
                        ids.Add(id);
                    }
                }
            }

            return ids;
        }

        private static string RecipeJson(
            int id, int outputItemId, IEnumerable<string> ingredients, int outputCount = 1)
        {
            return $"{{\"id\":{id},\"output_item_id\":{outputItemId},"
                   + $"\"output_item_count\":{outputCount},\"min_rating\":400,"
                   + "\"disciplines\":[\"Chef\"],\"flags\":[\"LearnedFromItem\"],"
                   + "\"ingredients\":[" + string.Join(",", ingredients) + "]}";
        }

        private static string Ingredient(string type, int id, int count)
        {
            return $"{{\"type\":\"{type}\",\"id\":{id},\"count\":{count}}}";
        }

        private static string AreJson(string essenceType)
        {
            var parts = new List<string>();
            for (int i = 0; i < AreEssenceIds.Length; i++)
            {
                parts.Add(Ingredient(essenceType, AreEssenceIds[i], AreEssenceCounts[i]));
            }

            parts.Add(Ingredient("Item", GlobEcto, 50));
            return RecipeJson(AreRecipeId, AreOutputItem, parts);
        }

        private static SeededRecipeCacheStore SeedFrom(
            IEnumerable<RawRecipe> recipes,
            IReadOnlyDictionary<int, IReadOnlyList<int>> extraSearchRows = null)
        {
            var byId = recipes.ToDictionary(r => r.Id);
            var searches = new Dictionary<int, IReadOnlyList<int>>();
            foreach (var recipe in byId.Values)
            {
                searches[recipe.OutputItemId] = new List<int> { recipe.Id };
            }

            if (extraSearchRows != null)
            {
                foreach (var row in extraSearchRows)
                {
                    searches[row.Key] = row.Value;
                }
            }

            var seed = new SeededRecipeCacheStore();
            using (var s1 = new MemoryStream(
                Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeSearches(searches))))
            using (var s2 = new MemoryStream(
                Encoding.UTF8.GetBytes(RecipeCacheSerializer.SerializeRecipes(byId))))
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
                Disciplines = new List<string> { "Chef" },
                MinRating = 400,
                Flags = new List<string> { "LearnedFromItem" },
            };
        }

        private static RawIngredient Ing(string type, int id, int count)
        {
            return new RawIngredient { Type = type, Id = id, Count = count };
        }

        // The pre-mutation shape: the three rift essences were ordinary
        // ITEM ingredients before the game update moved them to the wallet.
        private static RawRecipe HeldAreRecipeWithItemEssences()
        {
            var ingredients = new List<RawIngredient>();
            for (int i = 0; i < AreEssenceIds.Length; i++)
            {
                ingredients.Add(Ing("Item", AreEssenceIds[i], AreEssenceCounts[i]));
            }

            ingredients.Add(Ing("Item", GlobEcto, 50));
            return Recipe(AreRecipeId, AreOutputItem, ingredients.ToArray());
        }

        private static CompositeRecipeCacheStore Composite(
            string dataDir, SeededRecipeCacheStore seed, out OverlayRecipeCacheStore overlay)
        {
            overlay = new OverlayRecipeCacheStore(dataDir);
            overlay.Load();
            return new CompositeRecipeCacheStore(seed, overlay);
        }

        private static RecipeCorpusRefresher NewRefresher(
            HttpClient http, CompositeRecipeCacheStore store, Action<int> onRepair = null)
        {
            // Zero delay: the production 1s pause between batches is
            // politeness towards the API, not behaviour under test.
            return new RecipeCorpusRefresher(
                http, store, onRepair, null, TimeSpan.Zero);
        }

        /// <summary>
        /// The motivating case, end to end: a held recipe whose ingredients
        /// are item-typed becomes currency-typed under a new build id. The
        /// verifier cannot see this (recipe 14025's id never left the live
        /// list), so before the sweep existed the module served the stale
        /// shape forever.
        /// </summary>
        [Fact]
        public async Task AmalgamatedRiftEssence_IngredientsTurnFromItemsIntoCurrencies_IsRepairedAtTheNewBuild()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                var seed = SeedFrom(new[] { HeldAreRecipeWithItemEssences() });

                // Build 1: the live API still agrees with the seed, so the
                // sweep finds nothing to repair and writes no recipe rows.
                var live = new Dictionary<int, string>
                {
                    { AreRecipeId, AreJson("Item") },
                };
                handler.Responder = uri => ServeIds(uri, live);

                var store = Composite(tmp.Path, seed, out _);
                var first = await NewRefresher(http, store).RefreshAsync(
                    OldBuild, store.GetKnownPositiveRecipeIds(), null, CancellationToken.None);

                Assert.Equal(CorpusRefreshStatus.Completed, first.Status);
                Assert.Equal(0, first.RecipesUpdated);
                Assert.All(
                    store.TryGetRecipe(AreRecipeId).Ingredients,
                    i => Assert.Equal("Item", i.Type));

                // The game update lands: same recipe id, same output, but
                // the three essences are now wallet currencies.
                live[AreRecipeId] = AreJson("Currency");

                var repaired = new List<int>();
                var store2 = Composite(tmp.Path, SeedFrom(new[] { HeldAreRecipeWithItemEssences() }), out _);
                var second = await NewRefresher(http, store2, repaired.Add).RefreshAsync(
                    NewBuild, store2.GetKnownPositiveRecipeIds(), null, CancellationToken.None);

                Assert.Equal(CorpusRefreshStatus.Completed, second.Status);
                Assert.Equal(1, second.RecipesUpdated);
                Assert.Equal(new[] { AreOutputItem }, repaired);

                // What the store serves now is the CURRENT shape: three
                // currency ingredients plus the unchanged ecto.
                var served = store2.TryGetRecipe(AreRecipeId);
                var currencies = served.Ingredients
                    .Where(i => i.Type == "Currency")
                    .ToList();
                Assert.Equal(3, currencies.Count);
                for (int i = 0; i < AreEssenceIds.Length; i++)
                {
                    Assert.Contains(
                        currencies,
                        c => c.Id == AreEssenceIds[i] && c.Count == AreEssenceCounts[i]);
                }

                var ecto = served.Ingredients.Single(i => i.Type == "Item");
                Assert.Equal(GlobEcto, ecto.Id);
                Assert.Equal(50, ecto.Count);

                // And it survived the production flush: a fresh overlay off
                // the same directory serves the repaired row.
                var reloaded = new OverlayRecipeCacheStore(tmp.Path);
                reloaded.Load();
                Assert.Equal(
                    3,
                    reloaded.TryGetRecipe(AreRecipeId).Ingredients.Count(i => i.Type == "Currency"));
                Assert.Equal(NewBuild, reloaded.CorpusRefreshBuildId);
                Assert.True(reloaded.CorpusRefreshComplete);
            }
        }

        /// <summary>
        /// The same repair, carried all the way through the real plan
        /// pipeline: after the sweep the tree renders three currency leaves
        /// where it previously rendered three priced item leaves.
        /// </summary>
        [Fact]
        public async Task RepairedRiftEssenceRecipe_RendersCurrencyLeaves_ThroughTheRealPipeline()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                var live = new Dictionary<int, string>
                {
                    { AreRecipeId, AreJson("Currency") },
                };
                handler.Responder = uri => ServeIds(uri, live);

                var store = Composite(
                    tmp.Path, SeedFrom(new[] { HeldAreRecipeWithItemEssences() }), out _);

                var priceApi = new InMemoryPriceApiClient();
                priceApi.AddPrice(GlobEcto, buyUnitPrice: 200, sellUnitPrice: 400);
                // Prices for the essences as ITEMS. Once repaired they are
                // currencies, so these must stop being consulted - the same
                // mis-costing shape KNOWN-ISSUES #54 guards against.
                foreach (int essenceId in AreEssenceIds)
                {
                    priceApi.AddPrice(essenceId, buyUnitPrice: 999999, sellUnitPrice: 999999);
                }

                var itemApi = new InMemoryItemApiClient();
                itemApi.AddItem(AreOutputItem, "Amalgamated Rift Essence", "icon.png");
                itemApi.AddItem(GlobEcto, "Glob of Ectoplasm", "icon.png");

                var recipeService = new RecipeService(
                    new InMemoryRecipeApiClient(), cacheStore: store);

                await NewRefresher(http, store, recipeService.InvalidateSearch).RefreshAsync(
                    NewBuild, store.GetKnownPositiveRecipeIds(), null, CancellationToken.None);

                var pipeline = new CraftingPlanPipeline(
                    recipeService,
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    reducer: new InventoryReducer());

                var result = await pipeline.GenerateStructuredAsync(
                    AreOutputItem, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);

                Assert.Equal(CraftingDecision.Craft, result.CraftingTree.Decision);
                Assert.Equal(AreRecipeId, result.CraftingTree.RecipeId);

                var currencyLeaves = result.CraftingTree.Children
                    .Where(c => c.Decision == CraftingDecision.Currency)
                    .ToList();
                Assert.Equal(3, currencyLeaves.Count);

                // The ecto is the only remaining priced item, so the root's
                // cost is 50 of it at the instant-buy (sell listing) price
                // and nothing else - the 999999 item "prices" for the
                // essences can no longer leak in.
                var ectoNode = result.CraftingTree.Children
                    .Single(c => c.Decision != CraftingDecision.Currency);
                Assert.Equal(GlobEcto, ectoNode.ItemId);
                Assert.Equal(50 * 400, result.CraftingTree.SubtreeCost);
            }
        }

        [Fact]
        public async Task CompletedSweep_IsNotRepeatedAtTheSameBuild_ButReArmsWhenTheBuildMoves()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                var live = new Dictionary<int, string>
                {
                    { AreRecipeId, AreJson("Item") },
                };
                handler.Responder = uri => ServeIds(uri, live);

                var seedRows = new[] { HeldAreRecipeWithItemEssences() };
                var store = Composite(tmp.Path, SeedFrom(seedRows), out _);
                await NewRefresher(http, store).RefreshAsync(
                    OldBuild, store.GetKnownPositiveRecipeIds(), null, CancellationToken.None);
                Assert.Single(handler.Requests);

                // Relaunch inside the same patch: the stamp means 0
                // requests, which is the owner's firm requirement.
                var store2 = Composite(tmp.Path, SeedFrom(seedRows), out _);
                var again = await NewRefresher(http, store2).RefreshAsync(
                    OldBuild, store2.GetKnownPositiveRecipeIds(), null, CancellationToken.None);
                Assert.Equal(CorpusRefreshStatus.Skipped, again.Status);
                Assert.Single(handler.Requests);

                // A new build id re-arms it.
                var store3 = Composite(tmp.Path, SeedFrom(seedRows), out _);
                var rearmed = await NewRefresher(http, store3).RefreshAsync(
                    NewBuild, store3.GetKnownPositiveRecipeIds(), null, CancellationToken.None);
                Assert.Equal(CorpusRefreshStatus.Completed, rearmed.Status);
                Assert.Equal(2, handler.Requests.Count);
            }
        }

        [Fact]
        public async Task UnchangedCorpus_WritesNoRecipeRowsIntoTheOverlay()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                var live = new Dictionary<int, string>
                {
                    { AreRecipeId, AreJson("Item") },
                };
                handler.Responder = uri => ServeIds(uri, live);

                var store = Composite(
                    tmp.Path, SeedFrom(new[] { HeldAreRecipeWithItemEssences() }), out _);
                var result = await NewRefresher(http, store).RefreshAsync(
                    OldBuild, store.GetKnownPositiveRecipeIds(), null, CancellationToken.None);

                Assert.Equal(1, result.RecipesFetched);
                Assert.Equal(0, result.RecipesUpdated);

                // The storage decision, asserted rather than assumed: a
                // sweep that finds nothing changed must not turn the
                // overlay into a copy of the shipped seed.
                string recipesOverlay = Path.Combine(
                    tmp.Path, "recipe_cache", "recipes_overlay.json");
                Assert.False(File.Exists(recipesOverlay));
            }
        }

        [Fact]
        public async Task NegativeIdRecipes_AreNeverRequested()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                handler.Responder = uri => ServeIds(uri, new Dictionary<int, string>());

                // A hand-authored Mystic Forge row alongside a real one:
                // the live API has no such recipe and asking for it would
                // 404 the whole batch.
                var seed = SeedFrom(new[]
                {
                    Recipe(7, 700, Ing("Item", 19700, 1)),
                    Recipe(-5, 300, Ing("Item", 19701, 1)),
                });

                var store = Composite(tmp.Path, seed, out _);
                await NewRefresher(http, store).RefreshAsync(
                    OldBuild, store.GetKnownPositiveRecipeIds(), null, CancellationToken.None);

                var asked = handler.Requests.SelectMany(RequestedIds).ToList();
                Assert.Equal(new[] { 7 }, asked);
            }
        }

        [Fact]
        public async Task InterruptedSweep_PersistsItsCursor_AndTheNextRunResumesRatherThanRestarting()
        {
            using (var tmp = new TempDirectory())
            using (var http1Handler = new RoutingHandler())
            using (var http1 = new HttpClient(http1Handler))
            {
                // 500 recipes: three batches at the API's 200-per-request
                // cap, so there is a middle for the network to drop out in.
                var rows = Enumerable.Range(1, 500)
                    .Select(id => Recipe(id, 10000 + id, Ing("Item", 19700, 1)))
                    .ToList();
                var live = rows.ToDictionary(
                    r => r.Id,
                    r => RecipeJson(r.Id, r.OutputItemId, new[] { Ingredient("Item", 19700, 1) }));

                int served = 0;
                http1Handler.Responder = uri => ++served == 1
                    ? ServeIds(uri, live)
                    : new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent(string.Empty),
                    };

                var store = Composite(tmp.Path, SeedFrom(rows), out var overlay);
                var interrupted = await NewRefresher(http1, store).RefreshAsync(
                    OldBuild, store.GetKnownPositiveRecipeIds(), null, CancellationToken.None);

                Assert.Equal(CorpusRefreshStatus.Interrupted, interrupted.Status);
                Assert.NotNull(interrupted.Error);
                Assert.Equal(0, interrupted.ResumedFromCursorId);

                // One batch completed, so the cursor sits on its last id
                // and the sweep is explicitly not complete.
                Assert.Equal(200, overlay.CorpusRefreshCursorId);
                Assert.Equal(OldBuild, overlay.CorpusRefreshBuildId);
                Assert.False(overlay.CorpusRefreshComplete);

                using (var http2Handler = new RoutingHandler())
                using (var http2 = new HttpClient(http2Handler))
                {
                    http2Handler.Responder = uri => ServeIds(uri, live);

                    var store2 = Composite(tmp.Path, SeedFrom(rows), out var overlay2);
                    var resumed = await NewRefresher(http2, store2).RefreshAsync(
                        OldBuild, store2.GetKnownPositiveRecipeIds(), null, CancellationToken.None);

                    Assert.Equal(CorpusRefreshStatus.Completed, resumed.Status);
                    Assert.Equal(200, resumed.ResumedFromCursorId);

                    // The work already done is not repeated: the resumed
                    // run covers 201..500 and nothing below.
                    var asked = http2Handler.Requests.SelectMany(RequestedIds).ToList();
                    Assert.Equal(300, asked.Count);
                    Assert.Equal(201, asked.Min());
                    Assert.Equal(500, asked.Max());
                    Assert.True(overlay2.CorpusRefreshComplete);
                }
            }
        }

        [Fact]
        public async Task PriorityRecipes_AreFetchedInTheFirstBatch_AndStillCoveredByTheAscendingPass()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                var rows = Enumerable.Range(1, 300)
                    .Select(id => Recipe(id, 10000 + id, Ing("Item", 19700, 1)))
                    .ToList();
                var live = rows.ToDictionary(
                    r => r.Id,
                    r => RecipeJson(r.Id, r.OutputItemId, new[] { Ingredient("Item", 19700, 1) }));
                handler.Responder = uri => ServeIds(uri, live);

                var store = Composite(tmp.Path, SeedFrom(rows), out _);

                // Id 290 would otherwise be swept last; as a priority id it
                // is repaired first instead.
                await NewRefresher(http, store).RefreshAsync(
                    OldBuild,
                    store.GetKnownPositiveRecipeIds(),
                    new[] { 290 },
                    CancellationToken.None);

                Assert.Equal(290, RequestedIds(handler.Requests[0]).First());

                // Ordering only: the ascending pass still covers it, which
                // is what keeps the resume cursor's meaning total.
                var asked = handler.Requests.SelectMany(RequestedIds).ToList();
                Assert.Equal(2, asked.Count(id => id == 290));
                Assert.Equal(300, asked.Distinct().Count());
            }
        }

        [Fact]
        public async Task Cancellation_PropagatesAndLeavesResumableProgress()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            using (var cts = new CancellationTokenSource())
            {
                handler.Responder = uri => ServeIds(uri, new Dictionary<int, string>());
                cts.Cancel();

                var store = Composite(
                    tmp.Path, SeedFrom(new[] { HeldAreRecipeWithItemEssences() }), out var overlay);

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => NewRefresher(http, store).RefreshAsync(
                        OldBuild, store.GetKnownPositiveRecipeIds(), null, cts.Token));

                Assert.Empty(handler.Requests);
                Assert.False(overlay.CorpusRefreshComplete);
            }
        }

        [Fact]
        public async Task RepairingARow_KeepsTheSeedOnlyFieldsTheApiNeverServes()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                // A seed row carrying the two things only the seeder knows:
                // the fractional expected-output override PlanSolver prices
                // with, and an ingredient's achievement bit the dedup
                // pre-pass keys on. /v2/recipes returns neither.
                var held = Recipe(7, 700, Ing("Item", 19700, 1));
                held.ExpectedOutputCount = 0.31;
                held.AchievementId = 4242;
                held.Ingredients[0].AchievementBit = 3;
                held.Ingredients[0].AchievementId = 99;

                // The live row changes the ingredient COUNT, forcing a
                // write of a row the API describes only partially.
                var live = new Dictionary<int, string>
                {
                    { 7, RecipeJson(7, 700, new[] { Ingredient("Item", 19700, 5) }) },
                };
                handler.Responder = uri => ServeIds(uri, live);

                var store = Composite(tmp.Path, SeedFrom(new[] { held }), out _);
                var result = await NewRefresher(http, store).RefreshAsync(
                    OldBuild, store.GetKnownPositiveRecipeIds(), null, CancellationToken.None);

                Assert.Equal(1, result.RecipesUpdated);

                var served = store.TryGetRecipe(7);
                Assert.Equal(5, served.Ingredients[0].Count);
                Assert.Equal(0.31, served.ExpectedOutputCount);
                Assert.Equal(4242, served.AchievementId);
                Assert.Equal(3, served.Ingredients[0].AchievementBit);
                Assert.Equal(99, served.Ingredients[0].AchievementId);
            }
        }

        [Fact]
        public async Task ChangedOutputItem_IsRemovedFromTheOldSearchRow()
        {
            using (var tmp = new TempDirectory())
            using (var handler = new RoutingHandler())
            using (var http = new HttpClient(handler))
            {
                // Recipes 7 and 9 both make item 700; recipe 7 moves to
                // making item 800, which recipe 8 already makes.
                var seed = SeedFrom(
                    new[]
                    {
                        Recipe(7, 700, Ing("Item", 19700, 1)),
                        Recipe(8, 800, Ing("Item", 19701, 1)),
                    },
                    new Dictionary<int, IReadOnlyList<int>>
                    {
                        { 700, new List<int> { 7, 9 } },
                    });

                var live = new Dictionary<int, string>
                {
                    { 7, RecipeJson(7, 800, new[] { Ingredient("Item", 19700, 1) }) },
                    { 8, RecipeJson(8, 800, new[] { Ingredient("Item", 19701, 1) }) },
                };
                handler.Responder = uri => ServeIds(uri, live);

                var store = Composite(tmp.Path, seed, out _);
                var repaired = new List<int>();
                var result = await NewRefresher(http, store, repaired.Add).RefreshAsync(
                    OldBuild, store.GetKnownPositiveRecipeIds(), null, CancellationToken.None);

                Assert.Equal(1, result.RecipesUpdated);
                Assert.Equal(new[] { 700, 800 }, repaired);

                // The new output's row gained it and the old output's row
                // no longer claims a recipe that does not make that item.
                Assert.Equal(new[] { 8, 7 }, store.TryGetSearch(800));
                Assert.Equal(new[] { 9 }, store.TryGetSearch(700));
            }
        }
    }
}
