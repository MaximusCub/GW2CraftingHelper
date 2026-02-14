using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
        private const int MaxRetries = 3;

        private readonly HttpClient _httpClient;

        public WikiSmwClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Queries the wiki for items sold by vendors, returning raw parsed results.
        /// Vendor data lives on subobject pages (e.g. "NPC#vendor1") with properties:
        ///   Sells item          – the item page
        ///   Sells item.Has game id – item's GW2 game ID (property chain)
        ///   Has item quantity    – output count
        ///   Has item cost        – record: { Has item value, Has item currency }
        ///   Has vendor           – NPC page
        ///   Located in           – location pages
        /// </summary>
        public async Task<List<WikiVendorResult>> QueryVendorItemsAsync(
            string queryCondition = null, CancellationToken ct = default)
        {
            string condition = queryCondition ?? "[[Sells item::+]]";

            var allResults = new List<WikiVendorResult>();
            int offset = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var query = condition +
                    "|?Sells item.Has game id" +
                    "|?Sells item" +
                    "|?Has item quantity" +
                    "|?Has item cost" +
                    "|?Has vendor" +
                    "|?Located in" +
                    $"|limit={QueryLimit}" +
                    $"|offset={offset}";

                var url = $"{WikiApiUrl}?action=ask&query={Uri.EscapeDataString(query)}&format=json";

                Console.WriteLine($"  Querying wiki offset={offset}...");

                var response = await FetchWithRetryAsync(url, ct);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (!root.TryGetProperty("query", out var queryElement) ||
                    !queryElement.TryGetProperty("results", out var results))
                {
                    Console.WriteLine("  No results in response, stopping.");
                    break;
                }

                // When there are zero results, the wiki returns an empty array []
                // instead of an object. Handle both cases.
                if (results.ValueKind != JsonValueKind.Object)
                {
                    Console.WriteLine("  Empty result set, stopping.");
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
                    await Task.Delay(DelayBetweenRequestsMs, ct);
                }
                else
                {
                    break;
                }
            }

            return allResults;
        }

        private async Task<string> FetchWithRetryAsync(string url, CancellationToken ct)
        {
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    using var response = await _httpClient.GetAsync(url, ct);

                    if ((int)response.StatusCode == 429 ||
                        (int)response.StatusCode >= 500)
                    {
                        if (attempt >= MaxRetries)
                        {
                            response.EnsureSuccessStatusCode();
                        }

                        int backoffMs = 1000 * (1 << attempt);
                        if (response.Headers.RetryAfter?.Delta is TimeSpan delta)
                        {
                            backoffMs = Math.Max(backoffMs, (int)delta.TotalMilliseconds);
                        }

                        Console.WriteLine($"    HTTP {(int)response.StatusCode}, retrying in {backoffMs}ms (attempt {attempt + 1}/{MaxRetries})...");
                        await Task.Delay(backoffMs, ct);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException) when (attempt < MaxRetries)
                {
                    int backoffMs = 1000 * (1 << attempt);
                    Console.WriteLine($"    Request failed, retrying in {backoffMs}ms (attempt {attempt + 1}/{MaxRetries})...");
                    await Task.Delay(backoffMs, ct);
                }
            }

            throw new HttpRequestException($"Failed after {MaxRetries + 1} attempts: {url}");
        }

        private static WikiVendorResult ParseResult(string pageName, JsonElement element)
        {
            if (!element.TryGetProperty("printouts", out var printouts))
            {
                return null;
            }

            var result = new WikiVendorResult { PageName = pageName };

            // Sells item.Has game id (property chain result)
            if (printouts.TryGetProperty("Has game id", out var gameIds) &&
                gameIds.GetArrayLength() > 0)
            {
                result.GameId = gameIds[0].GetInt32();
            }

            // Sells item (item page name, for logging)
            if (printouts.TryGetProperty("Sells item", out var sellsItem) &&
                sellsItem.GetArrayLength() > 0)
            {
                var item = sellsItem[0];
                if (item.TryGetProperty("fulltext", out var fulltext))
                {
                    result.ItemName = fulltext.GetString();
                }
            }

            // Has item quantity
            if (printouts.TryGetProperty("Has item quantity", out var qty) &&
                qty.GetArrayLength() > 0)
            {
                result.OutputQuantity = qty[0].GetInt32();
            }

            // Has item cost — record type containing nested fields
            if (printouts.TryGetProperty("Has item cost", out var costArray))
            {
                foreach (var costRecord in costArray.EnumerateArray())
                {
                    var entry = new WikiCostEntry();

                    if (costRecord.TryGetProperty("Has item value", out var valueObj) &&
                        valueObj.TryGetProperty("item", out var valueItems) &&
                        valueItems.GetArrayLength() > 0)
                    {
                        var rawVal = valueItems[0].GetString();
                        if (int.TryParse(rawVal, out int parsed))
                        {
                            entry.Value = parsed;
                        }
                    }

                    if (costRecord.TryGetProperty("Has item currency", out var currObj) &&
                        currObj.TryGetProperty("item", out var currItems) &&
                        currItems.GetArrayLength() > 0)
                    {
                        entry.Currency = currItems[0].GetString();
                    }

                    if (entry.Value > 0)
                    {
                        result.CostEntries.Add(entry);
                    }
                }
            }

            // Has vendor (NPC page)
            if (printouts.TryGetProperty("Has vendor", out var vendor) &&
                vendor.GetArrayLength() > 0)
            {
                var v = vendor[0];
                if (v.TryGetProperty("fulltext", out var vName))
                {
                    result.MerchantName = vName.GetString();
                }
            }

            // Located in (location pages)
            if (printouts.TryGetProperty("Located in", out var locations))
            {
                foreach (var loc in locations.EnumerateArray())
                {
                    if (loc.TryGetProperty("fulltext", out var locName))
                    {
                        result.Locations.Add(locName.GetString());
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves a set of item names to their GW2 game IDs by querying the wiki.
        /// Used for vendor costs that reference items rather than wallet currencies.
        /// </summary>
        public async Task<Dictionary<string, int>> ResolveItemGameIdsAsync(
            IEnumerable<string> itemNames, CancellationToken ct = default)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in itemNames)
            {
                ct.ThrowIfCancellationRequested();

                var query = $"[[Has canonical name::{name}]][[Has context::Item]]" +
                    "|?Has game id" +
                    "|limit=1";

                var url = $"{WikiApiUrl}?action=ask&query={Uri.EscapeDataString(query)}&format=json";

                Console.WriteLine($"  Resolving item \"{name}\"...");

                var response = await FetchWithRetryAsync(url, ct);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.TryGetProperty("query", out var queryElement) &&
                    queryElement.TryGetProperty("results", out var results) &&
                    results.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in results.EnumerateObject())
                    {
                        if (prop.Value.TryGetProperty("printouts", out var printouts) &&
                            printouts.TryGetProperty("Has game id", out var gameIds) &&
                            gameIds.GetArrayLength() > 0)
                        {
                            result[name] = gameIds[0].GetInt32();
                        }
                        break; // only need first result
                    }
                }

                await Task.Delay(DelayBetweenRequestsMs, ct);
            }

            return result;
        }
    }

    public class WikiCostEntry
    {
        public int Value { get; set; }
        public string Currency { get; set; }
    }

    public class WikiVendorResult
    {
        public string PageName { get; set; }
        public int GameId { get; set; }
        public string ItemName { get; set; }
        public int? OutputQuantity { get; set; }
        public List<WikiCostEntry> CostEntries { get; set; } = new List<WikiCostEntry>();
        public string MerchantName { get; set; }
        public List<string> Locations { get; set; } = new List<string>();
    }
}
