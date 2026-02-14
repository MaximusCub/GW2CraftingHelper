using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using VendorOfferUpdater.Models;

namespace VendorOfferUpdater
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                return await RunAsync(args, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Cancelled.");
                return 130;
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"ERROR: Network request failed: {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        private static async Task<int> RunAsync(string[] args, CancellationToken ct)
        {
            string outputPath = null;
            string queryCondition = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--query" && i + 1 < args.Length)
                {
                    queryCondition = args[++i];
                }
                else if (!args[i].StartsWith("--"))
                {
                    outputPath = args[i];
                }
            }

            outputPath ??= Path.Combine(FindRepoRoot(), "ref", "vendor_offers.json");

            Console.WriteLine($"Output: {outputPath}");
            if (queryCondition != null)
            {
                Console.WriteLine($"Query:  {queryCondition}");
            }
            Console.WriteLine();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "GW2CraftingHelper-VendorOfferUpdater/1.0");

            // Step 1: Load currency mappings from GW2 API
            var apiHelper = new Gw2ApiHelper(httpClient);
            await apiHelper.LoadCurrenciesAsync();
            Console.WriteLine();

            // Step 2: Query wiki for vendor items
            var wikiClient = new WikiSmwClient(httpClient);
            Console.WriteLine("Querying GW2 Wiki for vendor items...");
            var wikiResults = await wikiClient.QueryVendorItemsAsync(queryCondition, ct);
            Console.WriteLine($"Total wiki results: {wikiResults.Count}");
            Console.WriteLine();

            // Step 3: Resolve item-based currencies via wiki
            // Some vendor costs reference items (e.g. "Piece of Candy Corn") rather
            // than wallet currencies. Collect unknown names and resolve their game IDs.
            var unknownCurrencyNames = wikiResults
                .SelectMany(r => r.CostEntries)
                .Select(c => c.Currency)
                .Where(name => !string.IsNullOrEmpty(name) && !apiHelper.ResolveCurrencyId(name).HasValue)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var itemIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (unknownCurrencyNames.Count > 0)
            {
                Console.WriteLine($"Resolving {unknownCurrencyNames.Count} item-based currencies via wiki...");
                itemIdMap = await wikiClient.ResolveItemGameIdsAsync(unknownCurrencyNames, ct);
                Console.WriteLine($"  Resolved {itemIdMap.Count} of {unknownCurrencyNames.Count} item names.");
                Console.WriteLine();
            }

            // Step 4: Convert to VendorOffers
            Console.WriteLine("Converting to vendor offers...");
            var offers = new List<VendorOffer>();
            int skippedNoId = 0;
            int skippedUnresolved = 0;

            foreach (var result in wikiResults)
            {
                if (result.GameId <= 0)
                {
                    skippedNoId++;
                    continue;
                }

                var offer = ConvertToOffer(result, apiHelper, itemIdMap);
                if (offer != null)
                {
                    offers.Add(offer);
                }
                else
                {
                    skippedUnresolved++;
                }
            }

            Console.WriteLine($"  Converted: {offers.Count} offers (skipped: {skippedNoId} no game ID, {skippedUnresolved} unresolved cost)");

            // Deduplicate by OfferId
            var uniqueOffers = offers
                .GroupBy(o => o.OfferId)
                .Select(g => g.First())
                .OrderBy(o => o.OfferId, StringComparer.Ordinal)
                .ToList();

            Console.WriteLine($"  Unique offers: {uniqueOffers.Count}");
            Console.WriteLine();

            // Step 4: Write output
            var dataset = new VendorOfferDataset
            {
                SchemaVersion = 1,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                Source = "gw2wiki-smw",
                Offers = uniqueOffers
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string json = JsonSerializer.Serialize(dataset, options);

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(outputPath, json);
            Console.WriteLine($"Written {uniqueOffers.Count} offers to {outputPath}");
            Console.WriteLine($"File size: {new FileInfo(outputPath).Length:N0} bytes");

            return 0;
        }

        /// <summary>
        /// Converts a single wiki vendor result to a VendorOffer.
        /// Returns null if any cost line cannot be resolved.
        /// </summary>
        private static VendorOffer ConvertToOffer(
            WikiVendorResult result,
            Gw2ApiHelper apiHelper,
            Dictionary<string, int> itemIdMap)
        {
            int outputCount = result.OutputQuantity ?? 1;
            if (outputCount <= 0) outputCount = 1;

            string merchant = result.MerchantName;
            if (string.IsNullOrEmpty(merchant)) return null;

            var costLines = new List<CostLine>();

            foreach (var cost in result.CostEntries)
            {
                int? currencyId = apiHelper.ResolveCurrencyId(cost.Currency);
                if (currencyId.HasValue)
                {
                    costLines.Add(new CostLine
                    {
                        Type = "Currency",
                        Id = currencyId.Value,
                        Count = cost.Value
                    });
                }
                else if (!string.IsNullOrEmpty(cost.Currency) &&
                         itemIdMap.TryGetValue(cost.Currency, out int itemId))
                {
                    costLines.Add(new CostLine
                    {
                        Type = "Item",
                        Id = itemId,
                        Count = cost.Value
                    });
                }
                else if (!string.IsNullOrEmpty(cost.Currency))
                {
                    // Unresolved currency/item name — skip this offer
                    return null;
                }
                else
                {
                    // No currency specified, assume coins
                    costLines.Add(new CostLine
                    {
                        Type = "Currency",
                        Id = Gw2Constants.CoinCurrencyId,
                        Count = cost.Value
                    });
                }
            }

            var locations = result.Locations
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            string offerId = VendorOfferHasher.ComputeOfferId(
                result.GameId,
                outputCount,
                costLines,
                merchant,
                locations,
                null,
                null);

            return new VendorOffer
            {
                OfferId = offerId,
                OutputItemId = result.GameId,
                OutputCount = outputCount,
                CostLines = costLines,
                MerchantName = merchant,
                Locations = locations.Count > 0 ? locations : null
            };
        }

        private static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir, ".git")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }

            // Fallback: current directory
            return Directory.GetCurrentDirectory();
        }
    }
}
