using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private const int MaxRetries = 3;

        private readonly HttpClient _httpClient;

        // Per-query state (set at the start of each QueryVendorItemsAsync call)
        private QueryOptions _options;
        private QueryStats _stats;
        private Stopwatch _stopwatch;
        private int _effectiveDelay;

        public WikiSmwClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Characters used as prefixes when partitioning queries that exceed
        // the wiki's ~5500 result offset limit.
        private static readonly string[] PartitionPrefixes =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
                .Select(c => c.ToString()).ToArray();

        private static readonly string PrintoutSuffix =
            "|?Sells item.Has game id" +
            "|?Sells item" +
            "|?Has item quantity" +
            "|?Has item cost" +
            "|?Has vendor" +
            "|?Located in";

        /// <summary>
        /// Queries the wiki for items sold by vendors, returning raw parsed results
        /// and query statistics.
        ///
        /// Vendor data lives on subobject pages (e.g. "NPC#vendor1") with properties:
        ///   Sells item          – the item page
        ///   Sells item.Has game id – item's GW2 game ID (property chain)
        ///   Has item quantity    – output count
        ///   Has item cost        – record: { Has item value, Has item currency }
        ///   Has vendor           – NPC page
        ///   Located in           – location pages
        ///
        /// The wiki SMW API limits pagination to ~5500 results per query condition.
        /// When that limit is hit, the query is automatically partitioned by vendor
        /// name prefix (e.g. [[Has vendor::~A*]]) with empty prefixes probed and
        /// skipped. Safety limits prevent runaway execution.
        /// </summary>
        public async Task<(List<WikiVendorResult> Results, QueryStats Stats)> QueryVendorItemsAsync(
            string queryCondition = null, QueryOptions options = null, CancellationToken ct = default)
        {
            _options = options ?? new QueryOptions();
            _effectiveDelay = Math.Max(200, _options.DelayBetweenRequestsMs);
            _stats = new QueryStats();
            _stopwatch = Stopwatch.StartNew();

            string condition = queryCondition ?? "[[Sells item::+]]";

            if (_options.DryRun)
            {
                PrintDryRunPlan(condition);
                return (new List<WikiVendorResult>(), _stats);
            }

            var allResults = new List<WikiVendorResult>();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                await PaginateConditionAsync(condition, null, 0, allResults, seenKeys, ct);
            }
            catch (SafetyLimitException ex)
            {
                Console.WriteLine($"  SAFETY LIMIT: {ex.Message}");
                Console.WriteLine($"  Returning {seenKeys.Count} partial results collected so far.");
                _stats.WasInterrupted = true;
            }

            _stopwatch.Stop();
            _stats.Elapsed = _stopwatch.Elapsed;
            _stats.DistinctResults = seenKeys.Count;

            // Detect non-alphanumeric vendor names in collected results
            var nonAlpha = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in allResults)
            {
                if (!string.IsNullOrEmpty(r.MerchantName) &&
                    !char.IsLetterOrDigit(r.MerchantName[0]))
                {
                    nonAlpha.Add(r.MerchantName);
                }
            }
            foreach (var name in nonAlpha.OrderBy(n => n, StringComparer.Ordinal))
            {
                _stats.NonAlphaVendors.Add(name);
            }

            return (allResults, _stats);
        }

        private async Task PaginateConditionAsync(
            string baseCondition,
            string vendorPrefix,
            int depth,
            List<WikiVendorResult> allResults,
            HashSet<string> seenKeys,
            CancellationToken ct)
        {
            string condition = baseCondition;
            if (vendorPrefix != null)
            {
                condition += $"[[Has vendor::~{vendorPrefix}*]]";
            }

            string label = vendorPrefix ?? "all";

            int partitionRowsAdded = 0;
            int partitionHttpRequests = 0;
            bool hitOffsetLimit = false;
            int offset = 0;

            while (true)
            {
                CheckSafetyLimits(ct, label, depth, seenKeys.Count);

                var query = condition +
                    PrintoutSuffix +
                    $"|limit={QueryLimit}" +
                    $"|offset={offset}";

                var url = $"{WikiApiUrl}?action=ask&query={Uri.EscapeDataString(query)}&format=json";

                _stats.TotalHttpRequests++;
                partitionHttpRequests++;

                var response = await FetchWithRetryAsync(url, ct);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (!root.TryGetProperty("query", out var queryElement) ||
                    !queryElement.TryGetProperty("results", out var results))
                {
                    break;
                }

                // Empty results come back as [] instead of {}
                if (results.ValueKind != JsonValueKind.Object)
                {
                    break;
                }

                int batchAdded = 0;
                foreach (var resultProp in results.EnumerateObject())
                {
                    var parsed = ParseResult(resultProp.Name, resultProp.Value);
                    if (parsed == null) continue;

                    _stats.TotalRowsFetched++;
                    string compositeKey = ComputeCompositeKey(parsed);
                    if (seenKeys.Add(compositeKey))
                    {
                        allResults.Add(parsed);
                        partitionRowsAdded++;
                        batchAdded++;
                    }
                    else
                    {
                        _stats.DuplicatesDiscarded++;
                    }
                }

                Console.WriteLine($"  [{label}] offset={offset} +{batchAdded} new");

                if (root.TryGetProperty("query-continue-offset", out var continueOffset))
                {
                    int nextOffset = continueOffset.GetInt32();
                    if (nextOffset <= offset)
                    {
                        // SMW offset limit reached
                        hitOffsetLimit = true;
                        break;
                    }
                    offset = nextOffset;
                    await Task.Delay(_effectiveDelay, ct);
                }
                else
                {
                    break;
                }
            }

            // Record partition stats
            var pStats = new PartitionStats
            {
                Prefix = vendorPrefix,
                Depth = depth,
                RowsAdded = partitionRowsAdded,
                HttpRequests = partitionHttpRequests
            };
            _stats.Partitions.Add(pStats);

            if (!hitOffsetLimit)
            {
                Console.WriteLine(
                    $"  [{label}] done: {partitionRowsAdded} rows in {partitionHttpRequests} requests");
                return;
            }

            // OVERFLOW — check depth limit
            if (depth >= _options.MaxPrefixDepth)
            {
                Console.WriteLine(
                    $"  WARNING: Partition [{label}] overflowing at max depth {depth}. " +
                    $"{partitionRowsAdded} rows collected, remaining truncated.");
                pStats.WasTruncated = true;
                _stats.TruncatedPartitions++;
                return;
            }

            Console.WriteLine(
                $"  [{label}] overflow at depth {depth}, probing sub-partitions...");

            // Probe + paginate sub-partitions (KEEP all rows already collected)
            int skippedEmpty = 0;
            foreach (var prefix in PartitionPrefixes)
            {
                string subPrefix = (vendorPrefix ?? "") + prefix;

                // Probe with limit=1 and no printouts (minimal payload)
                CheckSafetyLimits(ct, $"probe {subPrefix}", depth + 1, seenKeys.Count);

                string probeCondition = baseCondition + $"[[Has vendor::~{subPrefix}*]]";
                string probeQuery = probeCondition + "|limit=1|offset=0";
                string probeUrl =
                    $"{WikiApiUrl}?action=ask&query={Uri.EscapeDataString(probeQuery)}&format=json";

                _stats.TotalHttpRequests++;

                string probeResponse = await FetchWithRetryAsync(probeUrl, ct);
                await Task.Delay(_effectiveDelay, ct);

                bool hasResults = false;
                using (var probeDoc = JsonDocument.Parse(probeResponse))
                {
                    var probeRoot = probeDoc.RootElement;
                    if (probeRoot.TryGetProperty("query", out var pq) &&
                        pq.TryGetProperty("results", out var pr) &&
                        pr.ValueKind == JsonValueKind.Object)
                    {
                        // Check if there's at least one result
                        using var enumerator = pr.EnumerateObject();
                        hasResults = enumerator.MoveNext();
                    }
                }

                if (!hasResults)
                {
                    skippedEmpty++;
                    continue;
                }

                // Non-empty: paginate fully (re-fetches from offset=0; dedup handles overlap)
                await PaginateConditionAsync(
                    baseCondition, subPrefix, depth + 1, allResults, seenKeys, ct);
            }

            Console.WriteLine(
                $"  [{label}] sub-partitions done, " +
                $"{skippedEmpty}/{PartitionPrefixes.Length} empty prefixes skipped");
        }

        private void CheckSafetyLimits(
            CancellationToken ct, string label, int depth, int distinctCount)
        {
            ct.ThrowIfCancellationRequested();

            if (_stats.TotalHttpRequests >= _options.MaxTotalRequests)
            {
                throw new SafetyLimitException(
                    $"Exceeded {_options.MaxTotalRequests} request limit " +
                    $"at partition [{label}] depth={depth}. " +
                    $"Requests: {_stats.TotalHttpRequests}, " +
                    $"Rows: {_stats.TotalRowsFetched} ({distinctCount} distinct).");
            }

            if (_stopwatch.Elapsed >= _options.MaxRuntime)
            {
                throw new SafetyLimitException(
                    $"Exceeded {_options.MaxRuntime.TotalMinutes:F0}min runtime limit " +
                    $"at partition [{label}] depth={depth}. " +
                    $"Requests: {_stats.TotalHttpRequests}, " +
                    $"Rows: {_stats.TotalRowsFetched} ({distinctCount} distinct).");
            }
        }

        private void PrintDryRunPlan(string condition)
        {
            Console.WriteLine("=== DRY RUN ===");
            Console.WriteLine($"Base condition: {condition}");
            Console.WriteLine();
            Console.WriteLine("Configured caps:");
            Console.WriteLine($"  Max prefix depth:  {_options.MaxPrefixDepth}");
            Console.WriteLine($"  Max total requests: {_options.MaxTotalRequests}");
            Console.WriteLine($"  Max runtime:        {_options.MaxRuntime.TotalMinutes:F0} min");
            Console.WriteLine($"  Delay between reqs: {_effectiveDelay} ms");
            Console.WriteLine();
            Console.WriteLine("Traversal structure:");
            Console.WriteLine($"  Level 0: 1 root partition");
            for (int d = 1; d <= _options.MaxPrefixDepth; d++)
            {
                int maxPartitions = (int)Math.Pow(PartitionPrefixes.Length, d);
                Console.WriteLine(
                    $"  Level {d}: up to {maxPartitions} prefixes " +
                    $"({PartitionPrefixes.Length} per overflow at level {d - 1})");
            }
            Console.WriteLine();
            Console.WriteLine(
                "Actual request count is unknown without probing — " +
                "depends on data distribution.");
        }

        private static string ComputeCompositeKey(WikiVendorResult r)
        {
            string merchant = (r.MerchantName ?? "").Trim();
            merchant = Regex.Replace(merchant, @"\s+", " ");

            var costs = r.CostEntries
                .OrderBy(c => c.Currency ?? "", StringComparer.Ordinal)
                .ThenBy(c => c.Value)
                .Select(c => $"{c.Value}:{c.Currency ?? ""}")
                .ToArray();

            return $"{r.GameId}|{r.OutputQuantity ?? 1}|{merchant}|{string.Join(";", costs)}";
        }

        private async Task<string> FetchWithRetryAsync(string url, CancellationToken ct)
        {
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    using var response = await _httpClient.GetAsync(url, ct);
                    int statusCode = (int)response.StatusCode;

                    if (statusCode == 403)
                    {
                        // 403 is often a temporary block from the wiki.
                        // Use a long cooldown (30s base) with exponential backoff + jitter.
                        if (attempt >= MaxRetries)
                        {
                            throw new HttpRequestException(
                                $"HTTP 403 Forbidden after {MaxRetries + 1} attempts. " +
                                "The wiki may be rate-limiting this IP. " +
                                "Try increasing --delay or waiting before retrying.");
                        }

                        int cooldownMs = 30_000 * (1 << attempt);
                        if (response.Headers.RetryAfter?.Delta is TimeSpan delta403)
                        {
                            cooldownMs = Math.Max(cooldownMs, (int)delta403.TotalMilliseconds);
                        }
                        // Add jitter: ±10%
                        int jitter = (int)(cooldownMs * 0.1);
                        cooldownMs += Random.Shared.Next(-jitter, jitter + 1);
                        cooldownMs = Math.Max(cooldownMs, 0);

                        Console.WriteLine(
                            $"    WARNING: HTTP 403 (possible rate-limit block), " +
                            $"cooling down {cooldownMs / 1000}s " +
                            $"(attempt {attempt + 1}/{MaxRetries})...");
                        await Task.Delay(cooldownMs, ct);
                        continue;
                    }

                    if (statusCode == 429 || statusCode >= 500)
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

                        Console.WriteLine(
                            $"    HTTP {statusCode}, retrying in {backoffMs}ms " +
                            $"(attempt {attempt + 1}/{MaxRetries})...");
                        await Task.Delay(backoffMs, ct);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (HttpRequestException) when (attempt < MaxRetries)
                {
                    int backoffMs = 1000 * (1 << attempt);
                    Console.WriteLine(
                        $"    Request failed, retrying in {backoffMs}ms " +
                        $"(attempt {attempt + 1}/{MaxRetries})...");
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
        /// Resolves item names to GW2 game IDs by querying wiki pages directly.
        /// Uses the page title as the SMW subject (e.g. [[Piece of Candy Corn]])
        /// rather than matching on property values, which is more reliable across
        /// redirects and naming variants.
        /// Names are batched using [[A||B||C]] OR syntax to minimize requests.
        /// </summary>
        public async Task<Dictionary<string, int>> ResolveItemGameIdsAsync(
            IEnumerable<string> itemNames, CancellationToken ct = default)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var names = itemNames.ToList();

            int delay = _effectiveDelay > 0 ? _effectiveDelay : 250;

            // Batch into groups — wiki SMW limits query complexity (OR conditions).
            // 50 items per batch exceeds the wiki's depth limit; 10 is safe.
            const int batchSize = 10;

            for (int i = 0; i < names.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batch = names.Skip(i).Take(batchSize).ToList();
                var condition = "[[" + string.Join("||", batch) + "]]";
                var query = condition + "|?Has game id";

                var url = $"{WikiApiUrl}?action=ask&query={Uri.EscapeDataString(query)}&format=json";

                Console.WriteLine(
                    $"  Resolving batch {i / batchSize + 1} ({batch.Count} items)...");

                string response;
                try
                {
                    response = await FetchWithRetryAsync(url, ct);
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine(
                        $"  WARNING: Item resolution interrupted at batch {i / batchSize + 1}: {ex.Message}");
                    Console.WriteLine(
                        $"  Returning {result.Count} partial results.");
                    break;
                }

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
                            int gameId = gameIds[0].GetInt32();
                            if (gameId > 0)
                            {
                                result[prop.Name] = gameId;
                            }
                        }
                    }
                }

                if (i + batchSize < names.Count)
                {
                    await Task.Delay(delay, ct);
                }
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
