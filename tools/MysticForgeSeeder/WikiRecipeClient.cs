using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MysticForgeSeeder
{
    public class WikiIngredientEntry
    {
        public int? Index { get; set; }
        public int Quantity { get; set; }

        // Always set at construction (ParseIngredientRecord's one object
        // initializer, guarded by an IsNullOrEmpty check just before it).
        public string Name { get; set; } = string.Empty;
    }

    public class WikiRecipeEntry
    {
        // Always set at construction (ParseRecipeResult's one object
        // initializer, guarded by an IsNullOrEmpty check just before it).
        public string OutputName { get; set; } = string.Empty;
        public int OutputQuantity { get; set; } = 1;
        public List<WikiIngredientEntry> Ingredients { get; set; } = new();
    }

    public class WikiRecipeClient
    {
        private const string WikiApiUrl = "https://wiki.guildwars2.com/api.php";
        private const int MaxRetries = 3;
        private const int QueryLimit = 500;

        private readonly HttpClient _httpClient;
        private readonly int _delayMs;
        private readonly int _maxRequests;
        private int _requestCount;

        public int RequestCount => _requestCount;

        public WikiRecipeClient(HttpClient httpClient, int delayMs = 250, int maxRequests = 200)
        {
            _httpClient = httpClient;
            _delayMs = delayMs;
            _maxRequests = maxRequests;
        }

        /// <summary>
        /// Queries wiki for all Mystic Forge recipes via SMW.
        /// Paginates via query-continue-offset, deduplicates by OutputName.
        /// </summary>
        public async Task<List<WikiRecipeEntry>> QueryMysticForgeRecipesAsync(
            CancellationToken ct = default)
        {
            var allEntries = new List<WikiRecipeEntry>();
            int offset = 0;
            int pagesFetched = 0;

            string baseQuery =
                "[[Has recipe source::Mystic forge]]" +
                "|?Has canonical name" +
                "|?Has output quantity" +
                "|?Has ingredient" +
                $"|limit={QueryLimit}";

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                CheckRequestLimit();

                string query = baseQuery + $"|offset={offset}";
                string responseJson = await PostSmwQueryAsync(query, ct);
                pagesFetched++;

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("query", out var queryEl) ||
                    !queryEl.TryGetProperty("results", out var results))
                {
                    break;
                }

                // Empty results come back as [] instead of {}
                if (results.ValueKind != JsonValueKind.Object)
                {
                    break;
                }

                int batchCount = 0;
                foreach (var prop in results.EnumerateObject())
                {
                    var entry = ParseRecipeResult(prop.Name, prop.Value);
                    if (entry != null)
                    {
                        allEntries.Add(entry);
                        batchCount++;
                    }
                }

                Console.WriteLine(
                    $"  Page {pagesFetched}: offset={offset}, +{batchCount} recipes");

                if (batchCount == 0)
                {
                    break;
                }

                if (root.TryGetProperty("query-continue-offset", out var continueEl))
                {
                    if (!TryReadInt(continueEl, out int nextOffset))
                    {
                        break;
                    }
                    if (nextOffset <= offset)
                    {
                        break;
                    }
                    offset = nextOffset;
                    await Task.Delay(_delayMs, ct);
                }
                else
                {
                    break;
                }
            }

            Console.WriteLine(
                $"  Total: {allEntries.Count} raw recipes in {pagesFetched} pages, " +
                $"{_requestCount} HTTP requests used");

            // Dedup: same OutputName (case-insensitive), keep most ingredients
            var deduped = allEntries
                .GroupBy(e => e.OutputName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(e => e.Ingredients.Count).First())
                .ToList();

            if (deduped.Count < allEntries.Count)
            {
                Console.WriteLine(
                    $"  After dedup: {deduped.Count} recipes " +
                    $"({allEntries.Count - deduped.Count} duplicates removed)");
            }

            return deduped;
        }

        /// <summary>
        /// Resolves item names to GW2 item IDs via wiki SMW queries.
        /// Batches names in groups of 10 using [[A]]OR[[B]] syntax.
        /// Returns canonical fulltext as key (trimmed, case-preserved).
        /// </summary>
        public async Task<Dictionary<string, int>> ResolveItemIdsAsync(
            IEnumerable<string> names,
            CancellationToken ct = default)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var nameList = names
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            const int batchSize = 10;
            int totalBatches = (nameList.Count + batchSize - 1) / batchSize;

            for (int i = 0; i < nameList.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                CheckRequestLimit();

                var batch = nameList.Skip(i).Take(batchSize).ToList();
                var condition = string.Join("OR", batch.Select(n => $"[[{n}]]"));
                var query = condition + "|?Has game id";

                Console.WriteLine(
                    $"  Resolving batch {i / batchSize + 1}/{totalBatches} " +
                    $"({batch.Count} names)...");

                string responseJson;
                try
                {
                    responseJson = await PostSmwQueryAsync(query, ct);
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine(
                        $"  WARNING: Resolution interrupted at batch " +
                        $"{i / batchSize + 1}: {ex.Message}");
                    break;
                }

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("query", out var queryEl) &&
                    queryEl.TryGetProperty("results", out var results) &&
                    results.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in results.EnumerateObject())
                    {
                        // Use canonical fulltext from response as key
                        string canonicalName = prop.Name;
                        if (prop.Value.TryGetProperty("fulltext", out var ft))
                        {
                            canonicalName = ft.GetString()?.Trim() ?? prop.Name;
                        }

                        if (prop.Value.TryGetProperty("printouts", out var printouts) &&
                            printouts.TryGetProperty("Has game id", out var gameIds) &&
                            gameIds.GetArrayLength() > 0)
                        {
                            if (TryReadInt(gameIds[0], out int gameId) &&
                                gameId > 0)
                            {
                                // Log case collisions
                                if (result.TryGetValue(canonicalName, out int existing) &&
                                    existing != gameId)
                                {
                                    Console.WriteLine(
                                        $"    COLLISION: '{canonicalName}' had ID " +
                                        $"{existing}, now {gameId}");
                                }
                                result[canonicalName] = gameId;
                            }
                        }
                    }
                }

                if (i + batchSize < nameList.Count)
                {
                    await Task.Delay(_delayMs, ct);
                }
            }

            return result;
        }

        private WikiRecipeEntry? ParseRecipeResult(string pageName, JsonElement element)
        {
            if (!element.TryGetProperty("printouts", out var printouts))
            {
                return null;
            }

            // Output name: Has canonical name[0], fallback fulltext with #recipe suffix stripped
            string? outputName = null;
            if (printouts.TryGetProperty("Has canonical name", out var canonicalArr) &&
                canonicalArr.GetArrayLength() > 0)
            {
                outputName = canonicalArr[0].GetString()?.Trim();
            }

            if (string.IsNullOrEmpty(outputName))
            {
                string fulltext = pageName;
                if (element.TryGetProperty("fulltext", out var ft))
                {
                    fulltext = ft.GetString() ?? pageName;
                }
                int hashIdx = fulltext.IndexOf('#');
                outputName = hashIdx >= 0
                    ? fulltext.Substring(0, hashIdx).Trim()
                    : fulltext.Trim();
            }

            if (string.IsNullOrEmpty(outputName))
            {
                return null;
            }

            // Output quantity: default 1
            int outputQuantity = 1;
            if (printouts.TryGetProperty("Has output quantity", out var qtyArr) &&
                qtyArr.GetArrayLength() > 0)
            {
                if (TryReadInt(qtyArr[0], out int val) && val > 0)
                {
                    outputQuantity = val;
                }
            }

            // Ingredients from Has ingredient records
            var ingredients = new List<WikiIngredientEntry>();
            if (printouts.TryGetProperty("Has ingredient", out var ingArray))
            {
                foreach (var record in ingArray.EnumerateArray())
                {
                    var ing = ParseIngredientRecord(record);
                    if (ing != null)
                    {
                        ingredients.Add(ing);
                    }
                }
            }

            // Deterministic sort: indexed first (ascending), then unindexed (by name)
            ingredients = ingredients
                .OrderBy(i => i.Index.HasValue ? 0 : 1)
                .ThenBy(i => i.Index ?? int.MaxValue)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new WikiRecipeEntry
            {
                OutputName = outputName,
                OutputQuantity = outputQuantity,
                Ingredients = ingredients
            };
        }

        private static WikiIngredientEntry? ParseIngredientRecord(JsonElement record)
        {
            // Name: Has ingredient name.item[0].fulltext
            string? name = null;
            if (record.TryGetProperty("Has ingredient name", out var nameObj) &&
                nameObj.TryGetProperty("item", out var nameItems) &&
                nameItems.GetArrayLength() > 0)
            {
                var first = nameItems[0];
                if (first.ValueKind == JsonValueKind.Object &&
                    first.TryGetProperty("fulltext", out var ft))
                {
                    name = ft.GetString()?.Trim();
                }
                else if (first.ValueKind == JsonValueKind.String)
                {
                    name = first.GetString()?.Trim();
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            // Quantity: Has ingredient quantity.item[0], default 1
            int quantity = 1;
            if (record.TryGetProperty("Has ingredient quantity", out var qtyObj) &&
                qtyObj.TryGetProperty("item", out var qtyItems) &&
                qtyItems.GetArrayLength() > 0)
            {
                if (TryReadInt(qtyItems[0], out int val) && val > 0)
                {
                    quantity = val;
                }
            }

            // Index: Has ingredient index.item[0] (nullable)
            int? index = null;
            if (record.TryGetProperty("Has ingredient index", out var idxObj) &&
                idxObj.TryGetProperty("item", out var idxItems) &&
                idxItems.GetArrayLength() > 0)
            {
                if (TryReadInt(idxItems[0], out int idx))
                {
                    index = idx;
                }
            }

            return new WikiIngredientEntry
            {
                Index = index,
                Quantity = quantity,
                Name = name
            };
        }

        /// <summary>
        /// POSTs an SMW ask query. All wiki queries use POST to avoid URL length limits.
        /// Includes retry with exponential backoff + jitter and Retry-After support.
        /// </summary>
        private async Task<string> PostSmwQueryAsync(string query, CancellationToken ct)
        {
            _requestCount++;

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    using var content = new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["action"] = "ask",
                            ["format"] = "json",
                            ["query"] = query
                        });

                    using var response = await _httpClient.PostAsync(
                        WikiApiUrl, content, ct);
                    int statusCode = (int)response.StatusCode;

                    if (statusCode == 403)
                    {
                        if (attempt >= MaxRetries)
                        {
                            throw new HttpRequestException(
                                $"HTTP 403 after {MaxRetries + 1} attempts. " +
                                "Wiki may be rate-limiting.");
                        }

                        int cooldownMs = 30_000 * (1 << attempt);
                        if (response.Headers.RetryAfter?.Delta is TimeSpan d403)
                        {
                            cooldownMs = Math.Max(
                                cooldownMs, (int)d403.TotalMilliseconds);
                        }
                        cooldownMs = AddJitter(cooldownMs);

                        Console.WriteLine(
                            $"    HTTP 403, cooling down {cooldownMs / 1000}s " +
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
                            backoffMs = Math.Max(
                                backoffMs, (int)delta.TotalMilliseconds);
                        }
                        backoffMs = AddJitter(backoffMs);

                        Console.WriteLine(
                            $"    HTTP {statusCode}, retrying in {backoffMs}ms " +
                            $"(attempt {attempt + 1}/{MaxRetries})...");
                        await Task.Delay(backoffMs, ct);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync(ct);
                }
                catch (HttpRequestException) when (attempt < MaxRetries)
                {
                    int backoffMs = 1000 * (1 << attempt);
                    backoffMs = AddJitter(backoffMs);

                    Console.WriteLine(
                        $"    Request failed, retrying in {backoffMs}ms " +
                        $"(attempt {attempt + 1}/{MaxRetries})...");
                    await Task.Delay(backoffMs, ct);
                }
            }

            throw new HttpRequestException(
                $"Failed after {MaxRetries + 1} attempts");
        }

        /// <summary>
        /// Safely reads an integer from a JsonElement.
        /// The GW2 Wiki SMW API serialises all numeric printout values as
        /// JSON floats (e.g. 1.0 instead of 1) because the underlying
        /// MediaWiki property type is "Quantity". System.Text.Json treats
        /// these as non-integer, so TryGetInt32 fails and we must fall back
        /// to TryGetDouble. We still validate that the value is a whole
        /// number within int range to avoid silently truncating bad data.
        /// </summary>
        private static bool TryReadInt(JsonElement el, out int value)
        {
            if (el.TryGetInt32(out value))
            {
                return true;
            }
            if (el.TryGetDouble(out double d))
            {
                if (d < int.MinValue || d > int.MaxValue)
                {
                    value = 0;
                    return false;
                }
                double rounded = Math.Round(d);
                if (Math.Abs(d - rounded) > 1e-9)
                {
                    value = 0;
                    return false;
                }
                value = (int)rounded;
                return true;
            }
            value = 0;
            return false;
        }

        private static int AddJitter(int baseMs)
        {
            int jitter = (int)(baseMs * 0.1);
            int result = baseMs + Random.Shared.Next(-jitter, jitter + 1);
            return Math.Max(result, 0);
        }

        private void CheckRequestLimit()
        {
            if (_requestCount >= _maxRequests)
            {
                throw new InvalidOperationException(
                    $"Reached request limit ({_maxRequests}). " +
                    "Use --max-requests to increase.");
            }
        }
    }
}
