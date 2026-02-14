using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VendorOfferUpdater.Models;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Queries the GW2 Wiki Semantic MediaWiki API for vendor offer data.
    /// Uses the action=ask endpoint with vendor-related properties.
    /// </summary>
    public class WikiSmwClient
    {
        private const string WikiApiUrl = "https://wiki.guildwars2.com/api.php";
        private const int QueryLimit = 500;
        private const int DelayBetweenRequestsMs = 1000;

        private readonly HttpClient _httpClient;

        public WikiSmwClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Queries the wiki for items sold by vendors, returning raw parsed results.
        /// Pages through results using the continue offset.
        /// </summary>
        public async Task<List<WikiVendorResult>> QueryVendorItemsAsync()
        {
            var allResults = new List<WikiVendorResult>();
            int offset = 0;

            while (true)
            {
                var query = "[[Has game id::+]][[Sold by::+]]" +
                    "|?Has game id" +
                    "|?Sold by" +
                    "|?Has vendor cost" +
                    "|?Has vendor currency" +
                    "|?Has vendor quantity" +
                    $"|limit={QueryLimit}" +
                    $"|offset={offset}";

                var url = $"{WikiApiUrl}?action=ask&query={Uri.EscapeDataString(query)}&format=json";

                Console.WriteLine($"  Querying wiki offset={offset}...");

                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (!root.TryGetProperty("query", out var queryElement) ||
                    !queryElement.TryGetProperty("results", out var results))
                {
                    Console.WriteLine("  No results in response, stopping.");
                    break;
                }

                int batchCount = 0;
                foreach (var resultProp in results.EnumerateObject())
                {
                    var parsed = ParseResult(resultProp.Name, resultProp.Value);
                    if (parsed != null)
                    {
                        allResults.Add(parsed);
                        batchCount++;
                    }
                }

                Console.WriteLine($"  Got {batchCount} results (total: {allResults.Count})");

                // Check for continuation
                if (root.TryGetProperty("query-continue-offset", out var continueOffset))
                {
                    offset = continueOffset.GetInt32();
                    await Task.Delay(DelayBetweenRequestsMs);
                }
                else
                {
                    break;
                }
            }

            return allResults;
        }

        private static WikiVendorResult ParseResult(string pageName, JsonElement element)
        {
            if (!element.TryGetProperty("printouts", out var printouts))
            {
                return null;
            }

            var result = new WikiVendorResult { PageName = pageName };

            // Has game id
            if (printouts.TryGetProperty("Has game id", out var gameIds) &&
                gameIds.GetArrayLength() > 0)
            {
                result.GameId = gameIds[0].GetInt32();
            }

            // Sold by
            if (printouts.TryGetProperty("Sold by", out var soldBy))
            {
                foreach (var merchant in soldBy.EnumerateArray())
                {
                    if (merchant.TryGetProperty("fulltext", out var fulltext))
                    {
                        result.Merchants.Add(fulltext.GetString());
                    }
                    else if (merchant.ValueKind == JsonValueKind.String)
                    {
                        result.Merchants.Add(merchant.GetString());
                    }
                }
            }

            // Has vendor cost
            if (printouts.TryGetProperty("Has vendor cost", out var vendorCost) &&
                vendorCost.GetArrayLength() > 0)
            {
                result.VendorCost = vendorCost[0].GetInt32();
            }

            // Has vendor currency
            if (printouts.TryGetProperty("Has vendor currency", out var vendorCurrency) &&
                vendorCurrency.GetArrayLength() > 0)
            {
                var currencyVal = vendorCurrency[0];
                if (currencyVal.TryGetProperty("fulltext", out var currencyText))
                {
                    result.VendorCurrency = currencyText.GetString();
                }
                else if (currencyVal.ValueKind == JsonValueKind.String)
                {
                    result.VendorCurrency = currencyVal.GetString();
                }
            }

            // Has vendor quantity
            if (printouts.TryGetProperty("Has vendor quantity", out var vendorQty) &&
                vendorQty.GetArrayLength() > 0)
            {
                result.VendorQuantity = vendorQty[0].GetInt32();
            }

            return result;
        }
    }

    public class WikiVendorResult
    {
        public string PageName { get; set; }
        public int GameId { get; set; }
        public List<string> Merchants { get; set; } = new List<string>();
        public int? VendorCost { get; set; }
        public string VendorCurrency { get; set; }
        public int? VendorQuantity { get; set; }
    }
}
