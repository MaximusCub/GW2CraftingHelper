using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
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
            catch (SafetyLimitException ex)
            {
                Console.Error.WriteLine($"SAFETY LIMIT: {ex.Message}");
                return 2;
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
            string? outputPath = null;
            string? queryCondition = null;
            string? mergeIntoPath = null;
            bool dryRun = false;
            bool skipItemResolution = false;
            bool resolveOnly = false;
            int maxDepth = 2;
            int maxRequests = 2000;
            int maxRuntimeMinutes = 30;
            int delayMs = 250;
            bool tagSeasonalFestivals = false;
            int maxSeasonalPages = 500;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--query" && i + 1 < args.Length)
                {
                    queryCondition = args[++i];
                }
                else if (args[i] == "--dry-run")
                {
                    dryRun = true;
                }
                else if (args[i] == "--max-depth" && i + 1 < args.Length)
                {
                    maxDepth = int.Parse(args[++i]);
                }
                else if (args[i] == "--max-requests" && i + 1 < args.Length)
                {
                    maxRequests = int.Parse(args[++i]);
                }
                else if (args[i] == "--max-runtime" && i + 1 < args.Length)
                {
                    maxRuntimeMinutes = int.Parse(args[++i]);
                }
                else if (args[i] == "--delay" && i + 1 < args.Length)
                {
                    delayMs = int.Parse(args[++i]);
                }
                else if (args[i] == "--skip-item-resolution")
                {
                    skipItemResolution = true;
                }
                else if (args[i] == "--resolve-item-currencies-only")
                {
                    resolveOnly = true;
                }
                else if (args[i] == "--merge-into" && i + 1 < args.Length)
                {
                    mergeIntoPath = args[++i];
                }
                else if (args[i] == "--tag-seasonal-festivals")
                {
                    tagSeasonalFestivals = true;
                }
                else if (args[i] == "--max-seasonal-pages" && i + 1 < args.Length)
                {
                    maxSeasonalPages = int.Parse(args[++i]);
                }
                else if (!args[i].StartsWith("--"))
                {
                    outputPath = args[i];
                }
            }

            // Nice-to-have fix (2026-08-18 review): unlike every other
            // int.Parse'd flag in this loop, --max-seasonal-pages is a
            // safety LIMIT - 0 or a negative value made every
            // --tag-seasonal-festivals run with any uncached page throw
            // SafetyLimitException immediately, with a message that reads
            // like a data problem ("exceeding --max-seasonal-pages (0)")
            // rather than a misconfigured argument.
            if (maxSeasonalPages <= 0)
            {
                Console.Error.WriteLine(
                    $"ERROR: --max-seasonal-pages must be a positive integer, got {maxSeasonalPages}.");
                return 1;
            }

            var queryOptions = new QueryOptions
            {
                MaxPrefixDepth = maxDepth,
                MaxTotalRequests = maxRequests,
                MaxRuntime = TimeSpan.FromMinutes(maxRuntimeMinutes),
                DelayBetweenRequestsMs = delayMs,
                DryRun = dryRun
            };

            outputPath ??= Path.Combine(FindRepoRoot(), "ref", "vendor_offers.json");

            Console.WriteLine($"Output: {outputPath}");
            if (queryCondition != null)
            {
                Console.WriteLine($"Query:  {queryCondition}");
            }
            if (dryRun)
            {
                Console.WriteLine("Mode:   DRY RUN (no HTTP calls to wiki)");
            }
            Console.WriteLine(
                $"Limits: maxDepth={queryOptions.MaxPrefixDepth}, " +
                $"maxRequests={queryOptions.MaxTotalRequests}, " +
                $"maxRuntime={queryOptions.MaxRuntime.TotalMinutes:F0}min, " +
                $"delay={Math.Max(200, queryOptions.DelayBetweenRequestsMs)}ms");
            Console.WriteLine();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "GW2CraftingHelper-VendorOfferUpdater/1.0");

            // Step 1: Load currency mappings from GW2 API
            if (!dryRun)
            {
                var apiHelper = new Gw2ApiHelper(httpClient);
                await apiHelper.LoadCurrenciesAsync();
                Console.WriteLine();

                var wikiClient = new WikiSmwClient(httpClient);
                List<WikiVendorResult> wikiResults;

                // Review fix (2026-08-18): the pages actually touched by
                // THIS run's --query (null for --resolve-item-currencies-
                // only, which has no --query and processes the whole
                // cache by design) - see ResolveSeasonalFestivalValuesAsync's
                // queryScopedResults parameter doc comment for why this
                // must NOT be wikiResults after Step 2's cache merge.
                List<WikiVendorResult>? queryScopedResults = null;

                string wikiCachePath = Path.Combine(
                    Path.GetDirectoryName(outputPath) ?? ".",
                    "wiki_vendor_cache.json");

                if (resolveOnly)
                {
                    // --resolve-item-currencies-only: load cached wiki results
                    if (!File.Exists(wikiCachePath))
                    {
                        Console.Error.WriteLine(
                            $"ERROR: Wiki cache not found at {wikiCachePath}.");
                        Console.Error.WriteLine(
                            "Run with --skip-item-resolution first to generate it.");
                        return 1;
                    }

                    Console.WriteLine($"Loading wiki vendor cache from {wikiCachePath}...");
                    string cacheJson = await File.ReadAllTextAsync(wikiCachePath);
                    wikiResults = JsonSerializer.Deserialize<List<WikiVendorResult>>(cacheJson)
                        ?? throw new InvalidOperationException(
                            $"Wiki cache at {wikiCachePath} deserialized to null.");
                    Console.WriteLine($"  Loaded {wikiResults.Count} cached wiki results.");
                    Console.WriteLine();
                }
                else
                {
                    // Step 2: Query wiki for vendor items
                    Console.WriteLine("Querying GW2 Wiki for vendor items...");
                    var (results, queryStats) =
                        await wikiClient.QueryVendorItemsAsync(queryCondition, queryOptions, ct);
                    wikiResults = results;
                    queryScopedResults = results;
                    Console.WriteLine($"Total wiki results: {wikiResults.Count}");
                    Console.WriteLine();

                    // Print query summary
                    PrintQuerySummary(queryStats);

                    if (queryStats.WasInterrupted)
                    {
                        Console.WriteLine(
                            "WARNING: Query was interrupted by safety limits. " +
                            "Results are partial. Increase --max-runtime or --max-requests.");
                        Console.WriteLine();
                    }

                    // Save wiki results cache for --resolve-item-currencies-only
                    // Merge with existing cache if present (supports multi-pass querying).
                    // Freshly-queried pages always overwrite any existing cache entry for
                    // the same PageName, so a full Pass 1 re-scrape after a WikiVendorResult
                    // schema change (e.g. new printouts/fields) is not silently discarded by
                    // stale cache data. Pages not touched by this pass keep their cached copy.
                    if (File.Exists(wikiCachePath))
                    {
                        string existingCacheJson = await File.ReadAllTextAsync(wikiCachePath);
                        var existing = JsonSerializer.Deserialize<List<WikiVendorResult>>(
                            existingCacheJson) ?? new List<WikiVendorResult>();
                        var mergeResult = MergeWikiCache(existing, wikiResults);
                        Console.WriteLine(
                            $"Merged wiki cache: {mergeResult.Added} new + " +
                            $"{mergeResult.Refreshed} refreshed + " +
                            $"{mergeResult.Unchanged} unchanged = " +
                            $"{mergeResult.Merged.Count} total");
                        wikiResults = mergeResult.Merged;
                    }
                    string cacheJson = JsonSerializer.Serialize(wikiResults);
                    await File.WriteAllTextAsync(wikiCachePath, cacheJson);
                    Console.WriteLine(
                        $"Saved wiki vendor cache ({wikiResults.Count} results) to {wikiCachePath}");
                    Console.WriteLine();
                }

                // Step 3: Resolve item-based currencies via wiki
                string cachePath = Path.Combine(
                    Path.GetDirectoryName(outputPath) ?? ".",
                    "item_id_cache.json");
                var itemIdCache = LoadItemIdCache(cachePath);

                if (!skipItemResolution)
                {
                    var unknownCurrencyNames = wikiResults
                        .SelectMany(r => r.CostEntries)
                        .Select(c => c.Currency)
                        .Where(name => !string.IsNullOrEmpty(name)
                            && !apiHelper.ResolveCurrencyId(name).HasValue
                            && !itemIdCache.ContainsKey(name))
                        // IsNullOrEmpty above already proved non-null/non-empty;
                        // the static element type just doesn't narrow across
                        // the Where lambda boundary.
                        .Select(name => name!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (unknownCurrencyNames.Count > 0)
                    {
                        Console.WriteLine(
                            $"Resolving {unknownCurrencyNames.Count} item-based currencies via wiki...");
                        var freshResolved =
                            await wikiClient.ResolveItemGameIdsAsync(unknownCurrencyNames, ct);
                        Console.WriteLine(
                            $"  Resolved {freshResolved.Count} of {unknownCurrencyNames.Count} item names.");

                        foreach (var name in unknownCurrencyNames)
                        {
                            if (freshResolved.TryGetValue(name, out int id))
                            {
                                itemIdCache[name] = id;
                            }
                            else
                            {
                                itemIdCache[name] = -1; // miss sentinel
                            }
                        }

                        SaveItemIdCache(cachePath, itemIdCache);
                        Console.WriteLine();
                    }
                    else if (itemIdCache.Count > 0)
                    {
                        Console.WriteLine(
                            $"All item-based currencies resolved from cache ({itemIdCache.Count} entries).");
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine(
                        "Skipping item-based currency resolution (--skip-item-resolution).");
                    Console.WriteLine();
                }

                // Build final map excluding misses
                var itemIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in itemIdCache)
                {
                    if (kv.Value > 0)
                    {
                        itemIdMap[kv.Key] = kv.Value;
                    }
                }

                // Step 3.5: Resolve festival-vendor seasonal tags via wiki
                // (opt-in, --tag-seasonal-festivals - see
                // ResolveSeasonalFestivalValuesAsync's own doc comment for
                // why this is a separate, explicitly-requested pass rather
                // than part of every default run).
                if (tagSeasonalFestivals)
                {
                    string seasonalCachePath = Path.Combine(
                        Path.GetDirectoryName(outputPath) ?? ".",
                        "seasonal_wikitext_cache.json");

                    await ResolveSeasonalFestivalValuesAsync(
                        wikiResults, wikiClient, seasonalCachePath, maxSeasonalPages, delayMs, ct,
                        queryScopedResults);

                    // Round-trip fix (2026-08-17): WikiVendorResult.
                    // TemporarySeasonalValue's own doc comment claims this
                    // value "still round-trips through wiki_vendor_cache.
                    // json for a later run without needing to re-fetch the
                    // page" - false as written, because Step 2 above wrote
                    // wikiCachePath BEFORE this pass populated the field,
                    // so every row in that file had it as null. Re-save
                    // now that wikiResults carries the resolved values, so
                    // a later --resolve-item-currencies-only (or any other
                    // run that loads wikiCachePath) actually gets them
                    // without re-fetching every vendor page's wikitext.
                    string cacheJsonWithSeasonalTags = JsonSerializer.Serialize(wikiResults);
                    await File.WriteAllTextAsync(wikiCachePath, cacheJsonWithSeasonalTags);
                    Console.WriteLine(
                        $"Re-saved wiki vendor cache ({wikiResults.Count} results, now including " +
                        $"resolved seasonal festival tags) to {wikiCachePath}");
                    Console.WriteLine();
                }

                // Step 4: Convert to VendorOffers
                Console.WriteLine("Converting to vendor offers...");
                var offers = new List<VendorOffer>();
                int skippedNoId = 0;
                int skippedUnresolved = 0;

                // DATA LOSS fix (2026-08-17, festival-vendor auto-tagging
                // live run post-mortem): a merchant with a GameId<=0 row
                // in THIS pass means the pass's own wiki query failed to
                // resolve a game id for at least one of that merchant's
                // items (a query defect, not proof the wiki dropped the
                // item - a scoped festival-vendor run measured the wiki
                // still serving real ids for rows its own cache recorded
                // as GameId 0). MergeIntoBaseline's per-merchant wholesale
                // replacement must not be allowed to delete that
                // merchant's baseline offers on the strength of an
                // incomplete fresh result - see the set built below and
                // its use at the --merge-into call site.
                var skippedNoIdMerchants = new HashSet<string>(StringComparer.Ordinal);

                foreach (var result in wikiResults)
                {
                    if (result.GameId <= 0)
                    {
                        skippedNoId++;
                        skippedNoIdMerchants.Add(result.MerchantName ?? string.Empty);
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

                Console.WriteLine(
                    $"  Converted: {offers.Count} offers " +
                    $"(skipped: {skippedNoId} no game ID, {skippedUnresolved} unresolved cost)");

                // Deduplicate by OfferId
                var uniqueOffers = offers
                    .GroupBy(o => o.OfferId)
                    .Select(g => g.First())
                    .OrderBy(o => o.OfferId, StringComparer.Ordinal)
                    .ToList();

                Console.WriteLine($"  Unique offers: {uniqueOffers.Count}");
                Console.WriteLine();

                // Step 5: Merge into an existing baseline, if requested
                // (M37, KNOWN-ISSUES #24: "regenerate ONLY those pages'
                // rows" - a scoped re-scrape, via --query, of a handful of
                // merchant pages should not replace the WHOLE baseline
                // dataset the way a from-scratch run does; --merge-into
                // instead removes only the merchants this pass actually
                // queried, then unions in the fresh, freshly-tagged offers
                // for exactly those merchants).
                var finalOffers = uniqueOffers;
                if (mergeIntoPath != null)
                {
                    if (!File.Exists(mergeIntoPath))
                    {
                        Console.Error.WriteLine($"ERROR: --merge-into baseline not found at {mergeIntoPath}.");
                        return 1;
                    }

                    string baselineJson = await File.ReadAllTextAsync(mergeIntoPath, ct);
                    var readOptions = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        PropertyNameCaseInsensitive = true
                    };
                    var baseline = JsonSerializer.Deserialize<VendorOfferDataset>(baselineJson, readOptions)
                                   ?? new VendorOfferDataset();

                    // VendorOffer.Locations defaults to a fresh empty List
                    // (field initializer), so deserializing an offer whose
                    // "locations" key was OMITTED (null when originally
                    // written - DefaultIgnoreCondition.WhenWritingNull only
                    // omits null, never an empty-but-present array) leaves
                    // Locations as an empty list, not null. Re-serializing
                    // would then write "locations":[] where the baseline
                    // never had the key at all - a large, spurious diff
                    // across every untouched offer with no location data.
                    // Restored to null here so an offer this pass does NOT
                    // touch round-trips byte-for-byte. CostLines needs no
                    // equivalent fix - the baseline never omits that key
                    // (confirmed: every offer has it, sometimes as `[]`).
                    foreach (var offer in baseline.Offers)
                    {
                        if (offer.Locations != null && offer.Locations.Count == 0)
                        {
                            offer.Locations = null;
                        }
                    }

                    var mergeResult = MergeIntoBaseline(baseline.Offers, uniqueOffers, skippedNoIdMerchants);
                    finalOffers = mergeResult.Merged;
                    Console.WriteLine(
                        $"Merged into baseline ({baseline.Offers.Count} offers): " +
                        $"removed {mergeResult.RemovedFromBaseline} offer(s) for " +
                        $"{mergeResult.MerchantNamesReplaced.Count} merchant(s), " +
                        $"added {finalOffers.Count - (baseline.Offers.Count - mergeResult.RemovedFromBaseline)} " +
                        $"=> {finalOffers.Count} total");

                    if (mergeResult.MerchantNamesProtected.Count > 0)
                    {
                        Console.WriteLine(
                            $"  WARNING: {mergeResult.MerchantNamesProtected.Count} merchant(s) had " +
                            "row(s) with no game id this pass, so their baseline offers were NOT " +
                            "dropped (DATA LOSS guard) - re-run once every row resolves a game id " +
                            "to clean up any now-stale baseline rows for: " +
                            string.Join(", ", mergeResult.MerchantNamesProtected));
                    }
                    Console.WriteLine();
                }

                // Step 6: Write output
                var dataset = new VendorOfferDataset
                {
                    SchemaVersion = 1,
                    GeneratedAt = DateTime.UtcNow.ToString("o"),
                    Source = "gw2wiki-smw",
                    Offers = finalOffers
                };

                // M37 (KNOWN-ISSUES #24) fix: System.Text.Json's DEFAULT
                // encoder conservatively HTML-escapes '\'', '&', '<', '>'
                // (as ' etc.) even for pure JSON output with no HTML
                // context - but the already-checked-in ref/vendor_offers.json
                // never does this (confirmed: 222 literal '&' characters, 0
                // & escapes; "Hearth's Glow" stored with a literal
                // apostrophe). UnsafeRelaxedJsonEscaping skips that extra
                // HTML-safety escaping (while still escaping the JSON-
                // mandatory '"'/'\\'/control characters) but ALSO stops
                // escaping non-ASCII text, which the existing file DOES do
                // (e.g. "Homestead Refinement-Farm"). EscapeNonAscii
                // below restores exactly that: non-ASCII escaped, everything
                // else literal - matching the existing file's convention so
                // a scoped --merge-into run's diff stays confined to the
                // offers actually changed, not every apostrophe/ampersand
                // in the whole 53k-row dataset.
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = EscapeNonAscii(JsonSerializer.Serialize(dataset, jsonOptions));

                string? dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllTextAsync(outputPath, json);
                Console.WriteLine($"Written {finalOffers.Count} offers to {outputPath}");
                Console.WriteLine($"File size: {new FileInfo(outputPath).Length:N0} bytes");

                return 0;
            }
            else
            {
                // Dry-run path: only print plan, no HTTP to wiki
                var wikiClient = new WikiSmwClient(httpClient);
                var (_, stats) =
                    await wikiClient.QueryVendorItemsAsync(queryCondition, queryOptions, ct);
                return 0;
            }
        }

        private static void PrintQuerySummary(QueryStats stats)
        {
            Console.WriteLine("=== Query Summary ===");
            Console.WriteLine($"  HTTP requests:    {stats.TotalHttpRequests}");
            Console.WriteLine($"  Rows fetched:     {stats.TotalRowsFetched}");
            Console.WriteLine($"  Distinct results: {stats.DistinctResults}");
            Console.WriteLine($"  Duplicates:       {stats.DuplicatesDiscarded}");
            Console.WriteLine($"  Truncated parts:  {stats.TruncatedPartitions}");
            Console.WriteLine($"  Elapsed:          {stats.Elapsed}");

            if (stats.NonAlphaVendors.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"  WARNING: Found {stats.NonAlphaVendors.Count} vendor(s) with " +
                    "non-alphanumeric names (not covered by prefix partitioning):");
                foreach (var name in stats.NonAlphaVendors)
                {
                    Console.WriteLine($"    - {name}");
                }
            }

            if (stats.TruncatedPartitions > 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"  WARNING: Coverage may be incomplete - " +
                    $"{stats.TruncatedPartitions} partition(s) were truncated at max depth.");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Result of merging a freshly-queried batch of wiki results into an
        /// existing wiki-results cache. See <see cref="MergeWikiCache"/>.
        /// </summary>
        internal sealed class WikiCacheMergeResult
        {
            public List<WikiVendorResult> Merged { get; set; } = new();
            public int Added { get; set; }
            public int Refreshed { get; set; }
            public int Unchanged { get; set; }
        }

        /// <summary>
        /// Merges freshly-queried wiki results into an existing wiki-results cache,
        /// keyed by PageName. A fresh result for a PageName already present in the
        /// cache always overwrites the cached entry in full (so newly-added fields,
        /// e.g. purchase caps, are never silently discarded by a stale cached copy).
        /// A cached PageName not present in the fresh batch is preserved unchanged.
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static WikiCacheMergeResult MergeWikiCache(
            List<WikiVendorResult> existing,
            List<WikiVendorResult> fresh)
        {
            existing ??= new List<WikiVendorResult>();
            fresh ??= new List<WikiVendorResult>();

            var merged = new Dictionary<string, WikiVendorResult>(StringComparer.Ordinal);
            foreach (var r in existing)
            {
                merged[r.PageName ?? string.Empty] = r;
            }

            int added = 0;
            int refreshed = 0;
            foreach (var r in fresh)
            {
                string key = r.PageName ?? string.Empty;
                if (merged.ContainsKey(key))
                {
                    refreshed++;
                }
                else
                {
                    added++;
                }
                merged[key] = r;
            }

            int unchanged = existing.Count - refreshed;

            return new WikiCacheMergeResult
            {
                Merged = merged.Values.ToList(),
                Added = added,
                Refreshed = refreshed,
                Unchanged = unchanged
            };
        }

        /// <summary>
        /// Result of merging a scoped, freshly-queried batch of offers into
        /// an existing baseline dataset. See <see cref="MergeIntoBaseline"/>.
        /// </summary>
        internal sealed class BaselineMergeResult
        {
            public List<VendorOffer> Merged { get; set; } = new();
            public int RemovedFromBaseline { get; set; }
            public List<string> MerchantNamesReplaced { get; set; } = new();

            // DATA LOSS fix (2026-08-17): merchants that appeared in the
            // fresh batch but were EXCLUDED from wholesale replacement
            // because merchantsWithSkippedRows flagged them - their
            // baseline offers were kept, not dropped. See
            // MergeIntoBaseline's own doc comment.
            public List<string> MerchantNamesProtected { get; set; } = new();
        }

        /// <summary>
        /// M37 (KNOWN-ISSUES #24) support for a --merge-into run: merges a
        /// scoped, freshly-queried batch of offers (e.g. from a --query
        /// targeting a handful of merchant pages) into an existing full
        /// baseline dataset, replacing ONLY the merchants the scoped query
        /// actually covered - every other merchant's offers in the
        /// baseline are carried through byte-for-byte untouched. This is
        /// the "regenerate ONLY those pages' rows" operation: a merchant
        /// name appearing anywhere in <paramref name="fresh"/> has every
        /// one of its baseline offers removed first (even ones the fresh
        /// query happened not to re-find, e.g. a row that became stale/was
        /// removed from the wiki since the baseline was built), then every
        /// fresh offer for that merchant is added - never a partial,
        /// offer-by-offer union that could leave stale rows alongside new
        /// ones for the same merchant.
        ///
        /// DATA LOSS fix (2026-08-17): the wholesale-replace rule above is
        /// exactly what silently deleted 6 shipped offers in a real run -
        /// the fresh batch's own wiki query had 9 rows with GameId 0 for
        /// two merchants (a query defect; the wiki still served real ids
        /// for those rows, confirmed live after the fact), Program.cs's
        /// GameId&lt;=0 filter dropped them before they ever reached this
        /// method, and wholesale replacement then deleted the baseline's
        /// only copies with no fresh row to replace them. <paramref
        /// name="merchantsWithSkippedRows"/> (merchant names with at least
        /// one such GameId&lt;=0 row this pass, built by the caller from
        /// the same wikiResults list before the GameId filter runs) opts a
        /// merchant OUT of wholesale replacement: its baseline offers are
        /// kept alongside whatever fresh offers this pass did resolve for
        /// it (union, deduplicated by OfferId with the FRESH row preferred
        /// on collision, plus a content-key pass - see ComputeContentKey -
        /// for a baseline row that predates a hash-format change and so
        /// collides on content but not OfferId), rather than dropped and
        /// replaced by a known-incomplete fresh set. This trades "some
        /// possibly-stale baseline rows survive an extra run or two" for
        /// "never silently deletes shipped data" - the former is visible
        /// and fixable by a follow-up run once every row resolves a game
        /// id; the latter is not.
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static BaselineMergeResult MergeIntoBaseline(
            List<VendorOffer> baseline,
            List<VendorOffer> fresh,
            ISet<string>? merchantsWithSkippedRows = null)
        {
            baseline ??= new List<VendorOffer>();
            fresh ??= new List<VendorOffer>();
            merchantsWithSkippedRows ??= new HashSet<string>(StringComparer.Ordinal);

            var merchantsInFresh = fresh
                .Select(o => o.MerchantName ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(m => m, StringComparer.Ordinal)
                .ToList();

            var merchantsProtected = merchantsInFresh
                .Where(m => merchantsWithSkippedRows.Contains(m))
                .ToList();
            var merchantsReplaced = merchantsInFresh
                .Where(m => !merchantsWithSkippedRows.Contains(m))
                .ToList();
            var merchantsReplacedSet = new HashSet<string>(merchantsReplaced, StringComparer.Ordinal);

            var kept = baseline
                .Where(o => !merchantsReplacedSet.Contains(o.MerchantName ?? string.Empty))
                .ToList();
            int removed = baseline.Count - kept.Count;

            // Review fix (2026-08-18, festival-scrape follow-up review):
            // fresh must come FIRST in the concat so an OfferId collision
            // resolves to the FRESH row via GroupBy(...).Select(g =>
            // g.First()) below. The old kept.Concat(fresh) order let the
            // BASELINE row win every collision - for a protected merchant
            // (kept includes its baseline rows; SeasonalFestival is
            // deliberately NOT hashed by VendorOfferHasher, so a row whose
            // content is otherwise unchanged collides on OfferId) this
            // silently discarded the freshly-derived SeasonalFestival tag,
            // i.e. exactly the merchants the protected-merchant guard
            // exists to preserve data for kept shipping untagged.
            var merged = fresh.Concat(kept)
                .GroupBy(o => o.OfferId, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            // A protected merchant's baseline row can also predate a
            // VendorOfferHasher hash-format change (see that file's own
            // doc comment: "any offer's OfferId changes the first time it
            // is recomputed") - the fresh, content-identical row then gets
            // a DIFFERENT OfferId, the GroupBy above does not catch the
            // duplicate, and the union would ship two rows (one tagged,
            // one untagged) for the same vendor+item. Only protected
            // merchants can have this cross-duplication (a replaced
            // merchant's `kept` excludes it entirely), so scope the
            // content-based dedupe to them; prefer whichever survivor
            // carries a fresh SeasonalFestival tag.
            if (merchantsProtected.Count > 0)
            {
                var protectedSet = new HashSet<string>(merchantsProtected, StringComparer.Ordinal);
                var byContentKey = new Dictionary<string, VendorOffer>(StringComparer.Ordinal);
                var result = new List<VendorOffer>();
                foreach (var offer in merged)
                {
                    if (!protectedSet.Contains(offer.MerchantName ?? string.Empty))
                    {
                        result.Add(offer);
                        continue;
                    }

                    string contentKey = ComputeContentKey(offer);
                    if (byContentKey.TryGetValue(contentKey, out var survivor))
                    {
                        if (survivor.SeasonalFestival == null && offer.SeasonalFestival != null)
                        {
                            byContentKey[contentKey] = offer;
                        }
                    }
                    else
                    {
                        byContentKey[contentKey] = offer;
                    }
                }

                result.AddRange(byContentKey.Values);
                merged = result;
            }

            merged = merged
                .OrderBy(o => o.OfferId, StringComparer.Ordinal)
                .ToList();

            return new BaselineMergeResult
            {
                Merged = merged,
                RemovedFromBaseline = removed,
                MerchantNamesReplaced = merchantsReplaced,
                MerchantNamesProtected = merchantsProtected
            };
        }

        /// <summary>
        /// Canonical content key for a VendorOffer, used by
        /// MergeIntoBaseline's protected-merchant dedupe pass to catch two
        /// rows that describe the identical offer but carry different
        /// OfferId hash strings (e.g. a baseline row that predates a
        /// VendorOfferHasher hash-format change). Mirrors the same fields
        /// VendorOfferHasher.ComputeOfferId hashes, EXCEPT OfferId itself
        /// and SeasonalFestival - the latter deliberately excluded so a
        /// freshly-tagged row and its untagged baseline counterpart are
        /// recognized as the same offer rather than two different ones.
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static string ComputeContentKey(VendorOffer offer)
        {
            var sb = new StringBuilder();

            sb.Append("merchant=");
            sb.Append(offer.MerchantName ?? "");

            sb.Append(";output=");
            sb.Append(offer.OutputItemId);
            sb.Append('/');
            sb.Append(offer.OutputCount);

            sb.Append(";costs=");
            var sortedCosts = (offer.CostLines ?? new List<CostLine>())
                .OrderBy(c => c.Type, StringComparer.Ordinal)
                .ThenBy(c => c.Id)
                .ThenBy(c => c.Count)
                .ToList();
            for (int i = 0; i < sortedCosts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(sortedCosts[i].Type);
                sb.Append(':');
                sb.Append(sortedCosts[i].Id);
                sb.Append(':');
                sb.Append(sortedCosts[i].Count);
            }

            sb.Append(";locations=");
            var sortedLocations = (offer.Locations ?? new List<string>())
                .OrderBy(l => l, StringComparer.Ordinal)
                .ToList();
            sb.Append(string.Join(",", sortedLocations));

            sb.Append(";dailyCap=");
            sb.Append(offer.DailyCap.HasValue ? offer.DailyCap.Value.ToString() : "null");

            sb.Append(";weeklyCap=");
            sb.Append(offer.WeeklyCap.HasValue ? offer.WeeklyCap.Value.ToString() : "null");

            sb.Append(";homesteadTier=");
            sb.Append(offer.HomesteadTier.HasValue ? offer.HomesteadTier.Value.ToString() : "null");

            sb.Append(";seasonalCap=");
            sb.Append(offer.SeasonalCap.HasValue ? offer.SeasonalCap.Value.ToString() : "null");

            return sb.ToString();
        }

        /// <summary>
        /// Converts a single wiki vendor result to a VendorOffer.
        /// Returns null if any cost line cannot be resolved.
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static VendorOffer? ConvertToOffer(
            WikiVendorResult result,
            Gw2ApiHelper apiHelper,
            Dictionary<string, int> itemIdMap)
        {
            int outputCount = result.OutputQuantity ?? 1;
            if (outputCount <= 0) outputCount = 1;

            string? merchant = result.MerchantName;
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
                    // Unresolved currency/item name - skip this offer
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

            var offerLocations = locations.Count > 0 ? locations : null;

            // M37 (KNOWN-ISSUES #24): null for every non-Homestead-
            // Refinement offer. Also gated on the OUTPUT being one of the
            // three known refined materials - the same three "Homestead
            // Refinement-X" merchant pages also sell unrelated rows under
            // the identical merchant name (the station's own one-time
            // efficiency/capacity Upgrade purchase items, "Has vendor" is
            // hardcoded to the page name for every row on the page
            // regardless of subsection - confirmed live: without this
            // guard, an Upgrade-purchase row's requirement-less "Has
            // requirement" would otherwise be misread as tier 0). For a
            // Homestead Refinement row with unrecognized requirement text,
            // also null (with a console warning) rather than guessing -
            // never invent a tier.
            int? homesteadTier = Gw2Constants.IsHomesteadRefinementMaterialId(result.GameId)
                ? HomesteadTierResolver.ResolveTier(merchant, result.Requirement)
                : null;
            if (homesteadTier == null &&
                Gw2Constants.IsHomesteadRefinementMaterialId(result.GameId) &&
                !string.IsNullOrEmpty(merchant) &&
                merchant.Contains("Homestead Refinement", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(result.Requirement))
            {
                Console.WriteLine(
                    $"  WARNING: Homestead Refinement row for game id {result.GameId} " +
                    $"has unrecognized requirement text \"{result.Requirement}\" - left untagged.");
            }

            // Festival-vendor auto-tagging follow-up (2026-08-16): resolves
            // the raw wiki "seasonal="/"event=" value (if any, from a
            // separate ResolveSeasonalFestivalValuesAsync pass) to the
            // internal festival key. A present-but-unrecognized value
            // (e.g. a one-off non-festival event like "Fractal Rush") is
            // deliberately left untagged with a warning rather than
            // guessed - never hashed into OfferId either way, matching
            // VendorOffer.SeasonalFestival's own doc comment (this field
            // is deliberately not hashed by VendorOfferHasher, so tagging
            // an already-shipped offer never changes its OfferId).
            string? seasonalFestival = Gw2Constants.ResolveSeasonalFestivalKey(result.TemporarySeasonalValue);
            if (seasonalFestival == null && !string.IsNullOrWhiteSpace(result.TemporarySeasonalValue))
            {
                Console.WriteLine(
                    $"  WARNING: Vendor \"{merchant}\" has an unrecognized wiki " +
                    $"seasonal/event value \"{result.TemporarySeasonalValue}\" in its " +
                    "{{Temporary}} template - left untagged (no invented festival mapping).");
            }

            string offerId = VendorOfferHasher.ComputeOfferId(
                result.GameId,
                outputCount,
                costLines,
                merchant,
                offerLocations,
                result.DailyCap,
                result.WeeklyCap,
                homesteadTier,
                result.SeasonalCap);

            return new VendorOffer
            {
                OfferId = offerId,
                OutputItemId = result.GameId,
                OutputCount = outputCount,
                CostLines = costLines,
                MerchantName = merchant,
                Locations = offerLocations,
                DailyCap = result.DailyCap,
                WeeklyCap = result.WeeklyCap,
                HomesteadTier = homesteadTier,
                SeasonalCap = result.SeasonalCap,
                SeasonalFestival = seasonalFestival
            };
        }

        /// <summary>
        /// Festival-vendor auto-tagging follow-up (2026-08-16): fetches
        /// each distinct wiki vendor PAGE's raw wikitext (via
        /// WikiSmwClient.FetchWikitextAsync) and extracts its
        /// {{Temporary|...}} template's seasonal/event value
        /// (TemporaryTemplateParser), so ConvertToOffer can resolve every
        /// vendor's offers to a festival tag - not just the three
        /// Candy Corn Vendor (Weekly) rows this module previously
        /// hand-tagged.
        ///
        /// Deliberately opt-in (--tag-seasonal-festivals), not part of
        /// every default run: unlike every other field on WikiVendorResult
        /// (which come from the SMW "ask" printouts already fetched by
        /// QueryVendorItemsAsync/ResolveItemGameIdsAsync), there is no
        /// Semantic MediaWiki property for a page's {{Temporary}} template
        /// - unioning a distinct-PageName wikitext-parse request into
        /// every full refresh would add one HTTP request per distinct
        /// vendor page (thousands, for a from-scratch scrape) on top of
        /// the existing two-pass budget, silently changing the cost/time
        /// profile of the default `./tools/refresh-vendor-data.sh` workflow.
        /// A developer who wants full coverage passes the flag explicitly.
        ///
        /// Results are cached by real wiki page title (raw wiki value, or
        /// "" for "checked - no seasonal/event tag") in a small JSON file
        /// next to the other dev-local caches (gitignored, like
        /// wiki_vendor_cache.json/item_id_cache.json) so a repeat run
        /// never re-fetches a page it has already checked.
        /// <paramref name="maxSeasonalPages"/> is a safety limit
        /// (SafetyLimitException, same pattern WikiSmwClient's own query
        /// safety limits use) on how many NEW pages a single run will
        /// fetch, so an accidental full-dataset run with the flag set does
        /// not silently attempt thousands of live requests.
        ///
        /// WikiVendorResult.PageName is the SMW subject key of the vendor's
        /// "Sells item" SUBOBJECT, not the vendor's own wiki page title -
        /// confirmed live (api.php?action=ask against
        /// "[[Has vendor::Candy Corn Vendor (Weekly)]]"): every row's
        /// subject is "Candy Corn Vendor (Weekly)#vendor1",
        /// "...#vendor2", etc. (one subobject per sold item). The real,
        /// fetchable page title is everything before the first '#' - see
        /// StripSubobjectSuffix. Caching (and fetching) by the STRIPPED
        /// title, not the raw subobject key, is also what keeps this pass
        /// cheap: one wikitext fetch per distinct VENDOR, not per sold item.
        ///
        /// Review fix (2026-08-18): <paramref name="queryScopedResults"/>
        /// (null for --resolve-item-currencies-only, which has no --query
        /// and processes the whole cache by design) scopes which pages
        /// count toward the <paramref name="maxSeasonalPages"/> fetch
        /// budget to the ones THIS RUN's --query actually returned.
        /// <paramref name="wikiResults"/> at the caller's call site is the
        /// FULL merged wiki_vendor_cache.json (Program.cs Step 2's
        /// MergeWikiCache union), not just this run's query - scoping the
        /// fetch budget to it meant a narrow --query on a real dev-machine
        /// cache (thousands of distinct vendor pages) computed thousands
        /// of "uncached" pages, exceeded --max-seasonal-pages, and threw
        /// SafetyLimitException BEFORE Steps 4-6 ever wrote output,
        /// discarding the scoped run's already-completed live work. The
        /// cache-apply loop below still runs over the full
        /// <paramref name="wikiResults"/>, since applying an
        /// already-cached tag is a cheap dictionary lookup, not a fetch.
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static async Task ResolveSeasonalFestivalValuesAsync(
            List<WikiVendorResult> wikiResults,
            WikiSmwClient wikiClient,
            string cachePath,
            int maxSeasonalPages,
            int delayMs,
            CancellationToken ct,
            IReadOnlyList<WikiVendorResult>? queryScopedResults = null)
        {
            var cache = LoadSeasonalWikitextCache(cachePath);

            var fetchScope = queryScopedResults ?? (IReadOnlyList<WikiVendorResult>)wikiResults;

            var distinctPageNames = fetchScope
                .Select(r => r.PageName)
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => StripSubobjectSuffix(p!))
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var toFetch = distinctPageNames
                .Where(p => !cache.ContainsKey(p))
                .ToList();

            if (toFetch.Count > 0)
            {
                Console.WriteLine(
                    $"Resolving seasonal festival tags for {toFetch.Count} " +
                    $"uncached vendor page(s) ({distinctPageNames.Count - toFetch.Count} already cached)...");

                if (toFetch.Count > maxSeasonalPages)
                {
                    throw new SafetyLimitException(
                        $"Seasonal festival tagging would fetch {toFetch.Count} new wiki " +
                        $"page(s), exceeding --max-seasonal-pages ({maxSeasonalPages}). " +
                        "Increase --max-seasonal-pages or narrow --query to a smaller " +
                        "set of vendors.");
                }

                int effectiveDelay = Math.Max(200, delayMs);

                // Resilience fix (2026-08-17): the ONLY save call used to
                // sit after this loop, and the per-page catch only caught
                // HttpRequestException. A JsonException from a non-JSON/
                // HTML response (JsonDocument.Parse in
                // WikiSmwClient.FetchWikitextAsync), an
                // OperationCanceledException from Ctrl-C
                // (ct.ThrowIfCancellationRequested/Task.Delay), or any
                // other exception then aborted the whole method and
                // discarded every page already fetched THIS run - a
                // re-run would re-fetch all of them, and the caller
                // (RunAsync) never even reaches Step 4/5/6 to write any
                // output. The try/finally below saves whatever this pass
                // fetched no matter how the loop exits; a parse failure is
                // now treated exactly like an HTTP failure (warn, leave
                // this one page uncached, continue with the rest).
                try
                {
                    for (int i = 0; i < toFetch.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();

                        string pageName = toFetch[i];
                        string? wikitext;
                        try
                        {
                            wikitext = await wikiClient.FetchWikitextAsync(pageName, ct);
                        }
                        catch (HttpRequestException ex)
                        {
                            Console.WriteLine(
                                $"  WARNING: Failed to fetch wikitext for \"{pageName}\": {ex.Message} - left uncached.");
                            continue;
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine(
                                $"  WARNING: Failed to parse wikitext response for \"{pageName}\": {ex.Message} - left uncached.");
                            continue;
                        }

                        // Review fix (2026-08-18): a null wikitext (missing/
                        // renamed page, or an "error" object in the API
                        // response - see WikiSmwClient.FetchWikitextAsync's
                        // own doc comment) is NOT the same thing as "page
                        // fetched fine, no {{Temporary}} template found" -
                        // the latter legitimately caches as "" below. Caching
                        // a null the same way baked a false "checked - not
                        // tagged" negative into the cache permanently, with
                        // no warning and no future retry. Warn and leave the
                        // page uncached instead, same as an HTTP/JSON failure.
                        if (wikitext == null)
                        {
                            Console.WriteLine(
                                $"  WARNING: No wikitext returned for \"{pageName}\" " +
                                "(missing/renamed page, or a wiki API error response) - left uncached.");
                            continue;
                        }

                        string? raw = TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext);
                        cache[pageName] = raw ?? string.Empty;

                        if (i + 1 < toFetch.Count)
                        {
                            await Task.Delay(effectiveDelay, ct);
                        }
                    }
                }
                finally
                {
                    SaveSeasonalWikitextCache(cachePath, cache);
                }
            }
            else if (distinctPageNames.Count > 0)
            {
                Console.WriteLine(
                    $"All {distinctPageNames.Count} vendor page(s) already checked for seasonal festival tags.");
            }

            foreach (var result in wikiResults)
            {
                if (string.IsNullOrEmpty(result.PageName))
                {
                    continue;
                }

                string pageTitle = StripSubobjectSuffix(result.PageName);
                if (pageTitle.Length > 0 && cache.TryGetValue(pageTitle, out var value))
                {
                    // Nice-to-have fix (2026-08-18 review): assign
                    // unconditionally (including "" -> null) rather than
                    // only ever ASSIGNING a non-empty value - the old
                    // guard never CLEARED one. Combined with Program.cs
                    // Step 3.5's re-save of wiki_vendor_cache.json, a
                    // value that once round-tripped into the cache could
                    // never be un-set: if the wiki later drops a
                    // {{Temporary}} template, this now un-tags the vendor
                    // instead of re-tagging it forever off a stale value.
                    result.TemporarySeasonalValue = string.IsNullOrEmpty(value) ? null : value;
                }
            }
        }

        /// <summary>
        /// Strips a Semantic MediaWiki subobject suffix ("#vendor1", etc.)
        /// off a "Sells item" subject key, returning the vendor's real,
        /// fetchable wiki page title. A subject with no '#' (already a
        /// plain page title) is returned unchanged. See
        /// ResolveSeasonalFestivalValuesAsync's own doc comment for the
        /// live-confirmed subject-key shape this un-does.
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static string StripSubobjectSuffix(string subjectKey)
        {
            int hashIndex = subjectKey.IndexOf('#');
            return hashIndex >= 0 ? subjectKey.Substring(0, hashIndex) : subjectKey;
        }

        private static Dictionary<string, string> LoadSeasonalWikitextCache(string path)
        {
            var cache = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(path))
            {
                return cache;
            }

            try
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    cache[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
                Console.WriteLine($"Loaded seasonal wikitext cache ({cache.Count} entries) from {path}");
            }
            catch
            {
                // Ignore corrupt cache
            }

            return cache;
        }

        private static void SaveSeasonalWikitextCache(string path, Dictionary<string, string> cache)
        {
            var sorted = cache
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(sorted, options);

            // Nice-to-have fix (2026-08-17 review): unlike
            // Services/VendorOfferStore.SaveOverlay's temp-file +
            // File.Replace pattern, this used to write path directly - a
            // crash mid-write left a truncated cache. Bounded impact
            // (LoadSeasonalWikitextCache swallows a parse failure and
            // returns empty, so the only cost was a silent full re-fetch
            // next run), but this pass's own resilience fix now calls
            // this method from a `finally` block specifically to survive
            // a mid-run crash/cancellation, so the write itself should be
            // no less durable than that.
            string tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, json);
            if (File.Exists(path))
            {
                File.Replace(tmpPath, path, null);
            }
            else
            {
                File.Move(tmpPath, path);
            }

            Console.WriteLine($"  Saved seasonal wikitext cache ({cache.Count} entries) to {path}");
        }

        private static Dictionary<string, int> LoadItemIdCache(string path)
        {
            var cache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
            {
                return cache;
            }

            try
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    cache[prop.Name] = prop.Value.GetInt32();
                }
                Console.WriteLine($"Loaded item ID cache ({cache.Count} entries) from {path}");
            }
            catch
            {
                // Ignore corrupt cache
            }

            return cache;
        }

        private static void SaveItemIdCache(
            string path, Dictionary<string, int> cache)
        {
            var sorted = cache
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(sorted, options);
            File.WriteAllText(path, json);
            Console.WriteLine($"  Saved item ID cache ({cache.Count} entries) to {path}");
        }

        /// <summary>
        /// Escapes every character above U+007F as a lowercase \uXXXX
        /// sequence, leaving every ASCII character (including '\'', '&amp;',
        /// '&lt;', '&gt;') exactly as JsonSerializer.UnsafeRelaxedJsonEscaping
        /// already left it. Matches ref/vendor_offers.json's established
        /// convention exactly (see the call site's doc comment) - a plain
        /// per-char scan rather than a Regex, since this runs once over the
        /// whole serialized dataset and needs no backtracking/pattern
        /// matching. Assumes the input is already valid, escaped JSON (a
        /// literal '\' is never re-escaped here, since JsonSerializer's own
        /// escaping already turned any real backslash into "\\\\" before
        /// this runs - each '\\' char in that pair is &lt;= 0x7F and passes
        /// through unchanged, which is correct).
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static string EscapeNonAscii(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return json;
            }

            var sb = new StringBuilder(json.Length);
            foreach (char c in json)
            {
                if (c > 0x7F)
                {
                    sb.Append("\\u").Append(((int)c).ToString("x4"));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static string FindRepoRoot()
        {
            string? dir = AppContext.BaseDirectory;
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
