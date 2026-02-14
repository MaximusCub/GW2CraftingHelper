using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VendorOfferUpdater.Models;

namespace VendorOfferUpdater
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            string outputPath = args.Length > 0
                ? args[0]
                : Path.Combine(FindRepoRoot(), "ref", "vendor_offers.json");

            Console.WriteLine($"Output: {outputPath}");
            Console.WriteLine();

            using var httpClient = new HttpClient();

            // Step 1: Load currency mappings from GW2 API
            var apiHelper = new Gw2ApiHelper(httpClient);
            await apiHelper.LoadCurrenciesAsync();
            Console.WriteLine();

            // Step 2: Query wiki for vendor items
            var wikiClient = new WikiSmwClient(httpClient);
            Console.WriteLine("Querying GW2 Wiki for vendor items...");
            var wikiResults = await wikiClient.QueryVendorItemsAsync();
            Console.WriteLine($"Total wiki results: {wikiResults.Count}");
            Console.WriteLine();

            // Step 3: Convert to VendorOffers
            Console.WriteLine("Converting to vendor offers...");
            var offers = new List<VendorOffer>();
            int skipped = 0;

            foreach (var result in wikiResults)
            {
                if (result.GameId <= 0)
                {
                    skipped++;
                    continue;
                }

                var converted = ConvertToOffers(result, apiHelper);
                offers.AddRange(converted);
            }

            Console.WriteLine($"  Converted: {offers.Count} offers ({skipped} skipped)");

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

        private static List<VendorOffer> ConvertToOffers(
            WikiVendorResult result, Gw2ApiHelper apiHelper)
        {
            var offers = new List<VendorOffer>();

            int outputCount = result.VendorQuantity ?? 1;
            if (outputCount <= 0) outputCount = 1;

            var costLines = new List<CostLine>();

            if (result.VendorCost.HasValue && result.VendorCost.Value > 0)
            {
                int? currencyId = apiHelper.ResolveCurrencyId(result.VendorCurrency);
                if (currencyId.HasValue)
                {
                    costLines.Add(new CostLine
                    {
                        Type = "Currency",
                        Id = currencyId.Value,
                        Count = result.VendorCost.Value
                    });
                }
                else if (!string.IsNullOrEmpty(result.VendorCurrency))
                {
                    // Unknown currency — skip this offer
                    return offers;
                }
                else
                {
                    // No currency specified, assume coins
                    costLines.Add(new CostLine
                    {
                        Type = "Currency",
                        Id = Gw2Constants.CoinCurrencyId,
                        Count = result.VendorCost.Value
                    });
                }
            }

            foreach (var merchant in result.Merchants)
            {
                if (string.IsNullOrEmpty(merchant)) continue;

                string offerId = VendorOfferHasher.ComputeOfferId(
                    result.GameId,
                    outputCount,
                    costLines,
                    merchant,
                    new List<string>(),
                    null,
                    null);

                offers.Add(new VendorOffer
                {
                    OfferId = offerId,
                    OutputItemId = result.GameId,
                    OutputCount = outputCount,
                    CostLines = costLines.Select(c => new CostLine
                    {
                        Type = c.Type,
                        Id = c.Id,
                        Count = c.Count
                    }).ToList(),
                    MerchantName = merchant
                });
            }

            return offers;
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
