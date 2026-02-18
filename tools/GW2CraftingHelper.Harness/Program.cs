using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Diagnostics;
using GW2CraftingHelper.Services.Recipes;

namespace GW2CraftingHelper.Harness
{
    // --- Null API clients for offline mode ---

    internal class NullRecipeApiClient : IRecipeApiClient
    {
        public Task<IReadOnlyList<int>> SearchByOutputAsync(int itemId, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
        }

        public Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
        {
            return Task.FromResult<RawRecipe>(null);
        }
    }

    internal class NullPriceApiClient : IPriceApiClient
    {
        public Task<IReadOnlyList<RawPriceEntry>> GetPricesAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<RawPriceEntry>>(Array.Empty<RawPriceEntry>());
        }
    }

    internal class NullItemApiClient : IItemApiClient
    {
        public Task<IReadOnlyList<RawItem>> GetItemsAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<RawItem>>(Array.Empty<RawItem>());
        }
    }

    // --- Profile item definition ---

    internal class ProfileItem
    {
        public string Name { get; set; }
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public bool RequiresLive { get; set; }
    }

    // --- Program ---

    internal class Program
    {
        private static int Main(string[] args)
        {
            return MainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task<int> MainAsync(string[] args)
        {
            // Parse CLI arguments
            int profile = -1;
            int iterations = 1;
            bool live = false;
            bool raw = false;
            bool printCacheStats = false;
            bool clearOverlayCache = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--profile":
                        if (i + 1 < args.Length)
                        {
                            profile = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        }
                        break;
                    case "--iterations":
                        if (i + 1 < args.Length)
                        {
                            iterations = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        }
                        break;
                    case "--live":
                        live = true;
                        break;
                    case "--raw":
                        raw = true;
                        break;
                    case "--print-cache-stats":
                        printCacheStats = true;
                        break;
                    case "--clear-overlay-cache":
                        clearOverlayCache = true;
                        break;
                }
            }

            if (profile < 0)
            {
                Console.Error.WriteLine(
                    "Usage: GW2CraftingHelper.Harness --profile <n> " +
                    "[--iterations <n>] [--live] [--raw] " +
                    "[--print-cache-stats] [--clear-overlay-cache]");
                return 1;
            }

            // Get profile items
            var items = GetProfileItems(profile, live);
            if (items == null || items.Count == 0)
            {
                Console.Error.WriteLine($"Unknown profile: {profile}");
                return 1;
            }

            string mode = live ? "live" : "offline";
            Console.WriteLine($"Profile {profile} | Mode: {mode} | Iterations: {iterations}");
            Console.WriteLine();

            // Build pipeline
            HttpClient httpClient = null;
            try
            {
                IRecipeApiClient recipeApi;
                IPriceApiClient priceApi;
                IItemApiClient itemApi;

                if (live)
                {
                    httpClient = new HttpClient();
                    var rawRecipeApi = new Gw2RecipeApiClient(httpClient);
                    var mfSource = new FileMysticForgeRecipeSource();
                    recipeApi = RecipeClientFactory.Create(rawRecipeApi, mfSource);
                    priceApi = new Gw2PriceApiClient(httpClient);
                    itemApi = new Gw2ItemApiClient(httpClient);
                }
                else
                {
                    var nullRecipe = new NullRecipeApiClient();
                    var mfSource = new FileMysticForgeRecipeSource();
                    recipeApi = RecipeClientFactory.Create(nullRecipe, mfSource);
                    priceApi = new NullPriceApiClient();
                    itemApi = new NullItemApiClient();
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dataDir = Path.Combine(baseDir, "harness_data");
                Directory.CreateDirectory(dataDir);

                var vendorLoader = new VendorOfferLoader();
                var vendorStore = new VendorOfferStore(dataDir, vendorLoader);
                string vendorBaseline = Path.Combine(baseDir, "ref", "vendor_offers.json");
                if (File.Exists(vendorBaseline))
                {
                    using (var stream = File.OpenRead(vendorBaseline))
                    {
                        vendorStore.LoadBaseline(stream);
                    }
                }
                else
                {
                    vendorStore.LoadBaseline(null);
                }

                // Recipe cache: seed + overlay
                var recipeSeed = new SeededRecipeCacheStore();
                string seedSearchPath = Path.Combine(baseDir, "ref", "recipe_search_seed.json");
                string seedRecipesPath = Path.Combine(baseDir, "ref", "recipes_seed.json");
                if (File.Exists(seedSearchPath) && File.Exists(seedRecipesPath))
                {
                    using (var s1 = File.OpenRead(seedSearchPath))
                    using (var s2 = File.OpenRead(seedRecipesPath))
                    {
                        recipeSeed.Load(s1, s2);
                    }
                }

                string seedManifestPath = Path.Combine(baseDir, "ref", "recipe_seed_manifest.json");
                if (File.Exists(seedManifestPath))
                {
                    using (var ms = File.OpenRead(seedManifestPath))
                    {
                        recipeSeed.LoadManifest(ms);
                    }
                }

                var recipeOverlay = new OverlayRecipeCacheStore(dataDir);

                if (clearOverlayCache)
                {
                    Console.WriteLine("Clearing overlay cache...");
                    string overlayCacheDir = Path.Combine(dataDir, "recipe_cache");
                    if (Directory.Exists(overlayCacheDir))
                    {
                        Directory.Delete(overlayCacheDir, recursive: true);
                    }
                }

                {
                    int? buildId = null;
                    if (live && httpClient != null)
                    {
                        try
                        {
                            buildId = await FetchBuildIdAsync(httpClient);
                        }
                        catch { }
                    }
                    recipeOverlay.Load(buildId);
                    if (buildId.HasValue)
                    {
                        recipeSeed.SetCurrentBuildId(buildId.Value);
                    }
                }

                var recipeCacheStore = new CompositeRecipeCacheStore(recipeSeed, recipeOverlay);

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi, cacheStore: recipeCacheStore),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    vendorStore,
                    resolver: null,
                    reducer: new InventoryReducer(),
                    accountRecipeClient: null);

                // Run each profile item
                foreach (var item in items)
                {
                    await RunItemProfile(pipeline, item, iterations, raw, mode);
                    Console.WriteLine();
                }

                if (printCacheStats)
                {
                    var stats = recipeCacheStore.Stats;
                    Console.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "Recipe cache: Search hits={0} misses={1} | Recipe hits={2} misses={3}",
                        stats.SearchHits, stats.SearchMisses,
                        stats.RecipeHits, stats.RecipeMisses));
                }
            }
            finally
            {
                httpClient?.Dispose();
            }

            return 0;
        }

        private static List<ProfileItem> GetProfileItems(int profile, bool live)
        {
            switch (profile)
            {
                case 1:
                    var items = new List<ProfileItem>
                    {
                        new ProfileItem
                        {
                            Name = "Gift of Fortune",
                            ItemId = 19626,
                            Quantity = 1,
                            RequiresLive = false
                        }
                    };
                    if (live)
                    {
                        items.Add(new ProfileItem
                        {
                            Name = "Zojja's Claymore",
                            ItemId = 46762,
                            Quantity = 1,
                            RequiresLive = true
                        });
                    }
                    return items;
                default:
                    return null;
            }
        }

        private static async Task RunItemProfile(
            CraftingPlanPipeline pipeline,
            ProfileItem item,
            int iterations,
            bool raw,
            string mode)
        {
            Console.WriteLine($"=== {item.Name} ({item.ItemId}) x{item.Quantity} -- {iterations} iteration(s) [{mode}] ===");

            var allParsed = new List<List<PlanTimingAnalyzer.ParsedPhase>>();

            for (int i = 0; i < iterations; i++)
            {
                var result = await pipeline.GenerateStructuredAsync(
                    item.ItemId, item.Quantity, null, CancellationToken.None);

                // Extract timing lines (everything before the summary header)
                var timingLines = new List<string>();
                if (result.DebugLog != null)
                {
                    foreach (var line in result.DebugLog)
                    {
                        if (line == "--- Timing Summary ---")
                        {
                            break;
                        }
                        timingLines.Add(line);
                    }
                }

                var parsed = PlanTimingAnalyzer.Parse(timingLines);
                allParsed.Add(parsed);

                if (raw)
                {
                    Console.WriteLine($"  Iteration {i + 1}:");
                    foreach (var phase in parsed)
                    {
                        Console.WriteLine($"    {phase.Name}: {phase.ElapsedMs}ms");
                    }
                }
            }

            if (allParsed.Count == 0 || allParsed[0].Count == 0)
            {
                Console.WriteLine("  No timing data collected.");
                return;
            }

            Console.WriteLine();

            // --- Cold run (iteration 1) ---
            var cold = allParsed[0];
            long coldTotal = cold.Sum(p => p.ElapsedMs);

            Console.WriteLine("[COLD RUN]");
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture, "Total: {0}ms", coldTotal));

            var coldSorted = cold.OrderByDescending(p => p.ElapsedMs).ToList();
            foreach (var phase in coldSorted)
            {
                double pct = coldTotal > 0
                    ? (double)phase.ElapsedMs / coldTotal * 100.0
                    : 0.0;
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: {1}ms ({2:F1}%)",
                    phase.Name, phase.ElapsedMs, pct));
            }

            // --- Warm median (iterations 2+) ---
            if (allParsed.Count > 1)
            {
                var warmRuns = allParsed.Skip(1).ToList();
                var phaseNames = allParsed[0].Select(p => p.Name).ToList();
                var warmPhaseData = new Dictionary<string, List<long>>();
                var warmTotals = new List<long>();

                foreach (var name in phaseNames)
                {
                    warmPhaseData[name] = new List<long>();
                }

                foreach (var run in warmRuns)
                {
                    long runTotal = 0;
                    foreach (var phase in run)
                    {
                        if (warmPhaseData.ContainsKey(phase.Name))
                        {
                            warmPhaseData[phase.Name].Add(phase.ElapsedMs);
                        }
                        runTotal += phase.ElapsedMs;
                    }
                    warmTotals.Add(runTotal);
                }

                foreach (var entry in warmPhaseData)
                {
                    entry.Value.Sort();
                }
                warmTotals.Sort();

                double warmMedianTotal = Median(warmTotals);

                Console.WriteLine();
                Console.WriteLine("[WARM MEDIAN]");
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture, "Total: {0}ms", (long)warmMedianTotal));

                var warmStats = phaseNames
                    .Where(name => warmPhaseData[name].Count > 0)
                    .Select(name =>
                    {
                        var data = warmPhaseData[name];
                        double med = Median(data);
                        double pct = warmMedianTotal > 0
                            ? med / warmMedianTotal * 100.0
                            : 0.0;
                        return new { Name = name, Med = med, Pct = pct };
                    })
                    .OrderByDescending(s => s.Med)
                    .ToList();

                foreach (var s in warmStats)
                {
                    Console.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: {1}ms ({2:F1}%)",
                        s.Name, (long)s.Med, s.Pct));
                }
            }
        }

        private static double Median(List<long> sorted)
        {
            if (sorted.Count == 0)
            {
                return 0;
            }
            int mid = sorted.Count / 2;
            if (sorted.Count % 2 == 0)
            {
                return (sorted[mid - 1] + sorted[mid]) / 2.0;
            }
            return sorted[mid];
        }

        private static double Percentile(List<long> sorted, double p)
        {
            if (sorted.Count == 0)
            {
                return 0;
            }
            if (sorted.Count == 1)
            {
                return sorted[0];
            }
            double rank = p * (sorted.Count - 1);
            int lower = (int)Math.Floor(rank);
            int upper = (int)Math.Ceiling(rank);
            if (lower == upper)
            {
                return sorted[lower];
            }
            double frac = rank - lower;
            return sorted[lower] + frac * (sorted[upper] - sorted[lower]);
        }

        private static async Task<int> FetchBuildIdAsync(HttpClient httpClient)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var response = await httpClient.GetAsync(
                    "https://api.guildwars2.com/v2/build", cts.Token);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(json))
                {
                    return doc.RootElement.GetProperty("id").GetInt32();
                }
            }
        }
    }
}
