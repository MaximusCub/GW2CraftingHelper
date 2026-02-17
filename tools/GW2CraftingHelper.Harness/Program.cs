using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Diagnostics;

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
                }
            }

            if (profile < 0)
            {
                Console.Error.WriteLine("Usage: GW2CraftingHelper.Harness --profile <n> [--iterations <n>] [--live] [--raw]");
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

                string dataDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "harness_data");
                Directory.CreateDirectory(dataDir);

                var vendorLoader = new VendorOfferLoader();
                var vendorStore = new VendorOfferStore(dataDir, vendorLoader);
                string vendorBaseline = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "ref", "vendor_offers.json");
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

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
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

            // Compute aggregated statistics
            if (allParsed.Count == 0 || allParsed[0].Count == 0)
            {
                Console.WriteLine("  No timing data collected.");
                return;
            }

            // Collect per-phase data across iterations
            var phaseNames = allParsed[0].Select(p => p.Name).ToList();
            var phaseData = new Dictionary<string, List<long>>();
            var totalTimes = new List<long>();

            foreach (var name in phaseNames)
            {
                phaseData[name] = new List<long>();
            }

            foreach (var run in allParsed)
            {
                long runTotal = 0;
                foreach (var phase in run)
                {
                    if (phaseData.ContainsKey(phase.Name))
                    {
                        phaseData[phase.Name].Add(phase.ElapsedMs);
                    }
                    runTotal += phase.ElapsedMs;
                }
                totalTimes.Add(runTotal);
            }

            // Sort each phase's data for percentile computation
            foreach (var entry in phaseData)
            {
                entry.Value.Sort();
            }
            totalTimes.Sort();

            double medianTotal = Median(totalTimes);

            Console.WriteLine($"Median total: {medianTotal:F0}ms");
            Console.WriteLine();

            // Phase ranking sorted by median descending
            var phaseStats = phaseNames
                .Where(name => phaseData[name].Count > 0)
                .Select(name =>
                {
                    var data = phaseData[name];
                    double med = Median(data);
                    return new
                    {
                        Name = name,
                        Min = data[0],
                        Med = med,
                        P95 = Percentile(data, 0.95),
                        Max = data[data.Count - 1],
                        Avg = data.Average(),
                        Pct = medianTotal > 0 ? med / medianTotal * 100.0 : 0.0
                    };
                })
                .OrderByDescending(s => s.Med)
                .ToList();

            // Print header
            Console.WriteLine(
                "{0,-30} {1,8} {2,8} {3,8} {4,8} {5,8} {6,8}",
                "Phase", "Min", "Median", "P95", "Max", "Avg", "%");

            foreach (var s in phaseStats)
            {
                Console.WriteLine(
                    "{0,-30} {1,6}ms {2,6}ms {3,6}ms {4,6}ms {5,6}ms {6,6:F1}%",
                    s.Name, s.Min, (long)s.Med, (long)s.P95, s.Max,
                    (long)s.Avg, s.Pct);
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
    }
}
