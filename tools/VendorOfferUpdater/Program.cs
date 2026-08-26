using System;
using System.Collections.Generic;
using System.Globalization;
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
            string? diffSummaryBefore = null;
            string? diffSummaryAfter = null;

            for (int i = 0; i < args.Length; i++)
            {
                // Matched on the flag alone, then arity-checked below, rather
                // than on "flag plus two operands". Every other flag here falls
                // through to the positional branch when short an operand, which
                // for this one would mean silently discarding a read-only
                // request and starting a 15-minute scrape that WRITES.
                if (args[i] == "--diff-summary")
                {
                    if (i + 2 >= args.Length)
                    {
                        Console.Error.WriteLine(
                            "ERROR: --diff-summary needs two paths: --diff-summary <old> <new>.");
                        return 1;
                    }

                    diffSummaryBefore = args[++i];
                    diffSummaryAfter = args[++i];
                }
                else if (args[i] == "--query" && i + 1 < args.Length)
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

            // Unlike every other
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

            // --diff-summary is a read-only report over two dataset files, so
            // it short-circuits before any wiki/API setup and never writes.
            if (diffSummaryBefore != null && diffSummaryAfter != null)
            {
                return await RunDiffSummaryAsync(diffSummaryBefore, diffSummaryAfter);
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

                // The pages actually touched by
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

                    // WikiVendorResult.
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

                // A merchant with a GameId<=0 row
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
                // ("regenerate ONLY those pages'
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

                // Step 5c: Drop hand-verified exclusions. The SMW scrape
                // cannot tell a live vendor from one whose sale a patch
                // removed when the wiki page is not marked historical, so
                // ref/vendor_offer_exclusions.json refuses those rows by
                // (merchant, item) with the evidence for each. Applied
                // after the merge so a baseline row cannot survive either.
                int excludedCount = ApplyExclusions(
                    ref finalOffers, Path.GetDirectoryName(outputPath) ?? ".");
                if (excludedCount > 0)
                {
                    Console.WriteLine(
                        $"Excluded {excludedCount} hand-verified stale offer(s) " +
                        "(ref/vendor_offer_exclusions.json).");
                    Console.WriteLine();
                }

                // Step 6: Write output
                var dataset = new VendorOfferDataset
                {
                    SchemaVersion = 1,
                    Source = "gw2wiki-smw",
                    Offers = finalOffers
                };

                string json = SerializeDataset(dataset);

                string? dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllTextAsync(outputPath, json);
                Console.WriteLine($"Written {finalOffers.Count} offers to {outputPath}");
                Console.WriteLine($"File size: {new FileInfo(outputPath).Length:N0} bytes");

                // The run's timestamp goes in the sibling manifest, never in
                // the data file - see VendorOfferDataset's own note. A refresh
                // that changes nothing must leave the 14.8MB blob byte-for-byte
                // untouched, so `git status` after it is the no-op signal.
                string manifestPath = ManifestPathFor(outputPath);
                var manifest = new VendorOfferManifest
                {
                    ManifestVersion = 1,
                    SchemaVersion = dataset.SchemaVersion,
                    Source = dataset.Source,
                    OfferCount = finalOffers.Count,
                    Sha256 = HashFile(outputPath),
                    GeneratedAt = DateTime.UtcNow.ToString("o")
                };
                await File.WriteAllTextAsync(manifestPath, SerializeManifest(manifest));
                Console.WriteLine($"Written provenance manifest to {manifestPath}");

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
            var existingKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in existing)
            {
                string key = r.PageName ?? string.Empty;
                merged[key] = r;
                existingKeys.Add(key);
            }

            // Quality-audit B4 (KNOWN-ISSUES #53): counted against
            // existingKeys, not merged.ContainsKey - merged mutates during
            // this same loop, so a duplicate PageName within one fresh
            // batch was double-counted as Refreshed. addedKeys/
            // refreshedKeys are sets for the same reason: two fresh
            // entries sharing a PageName are one net page, not two.
            var addedKeys = new HashSet<string>(StringComparer.Ordinal);
            var refreshedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in fresh)
            {
                string key = r.PageName ?? string.Empty;
                if (existingKeys.Contains(key))
                {
                    refreshedKeys.Add(key);
                }
                else
                {
                    addedKeys.Add(key);
                }
                merged[key] = r;
            }

            int added = addedKeys.Count;
            int refreshed = refreshedKeys.Count;
            int unchanged = existingKeys.Count - refreshed;

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

            // Merchants that appeared in the
            // fresh batch but were EXCLUDED from wholesale replacement
            // because merchantsWithSkippedRows flagged them - their
            // baseline offers were kept, not dropped. See
            // MergeIntoBaseline's own doc comment.
            public List<string> MerchantNamesProtected { get; set; } = new();
        }

        /// <summary>
        /// Merges a scoped, freshly-queried batch of offers into an
        /// existing full baseline, replacing ONLY the merchants the
        /// scoped query covered - every other merchant's offers pass
        /// through untouched. A merchant appearing in
        /// <paramref name="fresh"/> has every baseline offer removed
        /// first, then every fresh offer added - never a partial union
        /// that could leave stale rows alongside new ones.
        ///
        /// <paramref name="merchantsWithSkippedRows"/> (merchants with at
        /// least one GameId&lt;=0 row this pass, built before the GameId
        /// filter runs) opts a merchant OUT of wholesale replacement:
        /// replacing on the strength of a known-incomplete fresh set has
        /// silently deleted shipped offers before. Its baseline offers
        /// are instead unioned with the fresh ones, deduplicated by
        /// OfferId with fresh preferred - but a losing row's
        /// SeasonalFestival tag is carried onto an untagged winner - plus
        /// a content-key pass (ComputeContentKey) for rows predating a
        /// hash-format change. Possibly-stale baseline rows surviving an
        /// extra run is visible and fixable; silent deletion is not.
        ///
        /// NOTE (non-purity): this method mutates rows it does not own -
        /// it assigns onto SeasonalFestival/OfferId of instances in the
        /// caller's own <paramref name="fresh"/>/<paramref name="baseline"/>
        /// lists rather than cloning, so a caller keeping its own
        /// reference will observe the mutation.
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

            // An ORDINARY (non-protected) replaced merchant's
            // baseline rows are about to be dropped entirely by `kept`
            // below, before the fresh/kept GroupBy tag-carry-forward logic
            // further down ever runs - that logic only ever sees a
            // baseline row for PROTECTED merchants. Without this, a
            // transiently failed fetch loses a previously-shipped tag for
            // every ordinary merchant. Harvest each replaced merchant's
            // tagged baseline rows into a lookup BEFORE `kept` drops them,
            // keyed by both OfferId and ComputeContentKey - a
            // VendorOfferHasher hash-format migration can leave either as
            // the only field still matching between the baseline and fresh
            // copies of the same offer (see the protected-merchant
            // content-key dedupe pass further below for the same
            // reasoning) - then apply the harvested tag onto that
            // merchant's fresh rows that have no tag of their own. A fresh
            // row that already carries its own (possibly different) tag is
            // never overwritten - fresh always wins when both sides are
            // tagged.
            if (merchantsReplacedSet.Count > 0)
            {
                var replacedTagsByOfferId = new Dictionary<string, string>(StringComparer.Ordinal);
                var replacedTagsByContentKey = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var o in baseline)
                {
                    if (o.SeasonalFestival == null)
                    {
                        continue;
                    }
                    if (!merchantsReplacedSet.Contains(o.MerchantName ?? string.Empty))
                    {
                        continue;
                    }

                    if (o.OfferId != null)
                    {
                        replacedTagsByOfferId[o.OfferId] = o.SeasonalFestival;
                    }
                    replacedTagsByContentKey[ComputeContentKey(o)] = o.SeasonalFestival;
                }

                if (replacedTagsByOfferId.Count > 0 || replacedTagsByContentKey.Count > 0)
                {
                    foreach (var o in fresh)
                    {
                        if (o.SeasonalFestival != null)
                        {
                            continue;
                        }
                        if (!merchantsReplacedSet.Contains(o.MerchantName ?? string.Empty))
                        {
                            continue;
                        }

                        if (o.OfferId != null
                            && replacedTagsByOfferId.TryGetValue(o.OfferId, out var tagById))
                        {
                            o.SeasonalFestival = tagById;
                        }
                        else if (replacedTagsByContentKey.TryGetValue(
                            ComputeContentKey(o), out var tagByContent))
                        {
                            o.SeasonalFestival = tagByContent;
                        }
                    }
                }
            }

            var kept = baseline
                .Where(o => !merchantsReplacedSet.Contains(o.MerchantName ?? string.Empty))
                .ToList();
            int removed = baseline.Count - kept.Count;

            //
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
            // This pass used to just take
            // g.First() unconditionally, so a FRESH row with no
            // SeasonalFestival (e.g. one whose page's wikitext fetch
            // missed this run - see ResolveSeasonalFestivalValuesAsync's
            // null-wikitext handling) silently deleted a shipped, tagged
            // baseline row on an OfferId collision - the exact opposite of
            // the content-key pass below, which already prefers whichever
            // side carries the tag. Same rule now applies here: keep the
            // winning row (fresh, if present in the group, for freshness
            // of everything else), but carry a losing sibling's tag
            // forward if the winner itself has none.
            var merged = fresh.Concat(kept)
                .GroupBy(o => o.OfferId, StringComparer.Ordinal)
                .Select(g =>
                {
                    var winner = g.First();
                    if (winner.SeasonalFestival == null)
                    {
                        var taggedSibling = g.FirstOrDefault(o => o.SeasonalFestival != null);
                        if (taggedSibling != null)
                        {
                            winner.SeasonalFestival = taggedSibling.SeasonalFestival;
                        }
                    }
                    return winner;
                })
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

                // When a content-key collision
                // resolves to the baseline (kept) row because it carries
                // the tag and the fresh row does not, the swap below used
                // to keep the baseline row's OfferId wholesale - stale,
                // pre-hash-format-change - discarding the fresh row's
                // current-format OfferId even though the fresh row is
                // otherwise thrown away. Track which OfferId strings came
                // from THIS run's fresh batch so the winning row can be
                // migrated onto the current-format id instead of carrying
                // the stale one forward indefinitely.
                var freshOfferIds = new HashSet<string>(
                    fresh.Where(o => o.OfferId != null).Select(o => o.OfferId!),
                    StringComparer.Ordinal);

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
                            if (offer.OfferId != null && survivor.OfferId != null
                                && freshOfferIds.Contains(survivor.OfferId)
                                && !freshOfferIds.Contains(offer.OfferId))
                            {
                                offer.OfferId = survivor.OfferId;
                            }
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

            // Null for every non-Homestead-
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

            // Festival-vendor auto-tagging follow-up: resolves
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
        /// Festival-vendor auto-tagging follow-up: fetches
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
        /// <paramref name="maxSeasonalPages"/> is a self-healing per-run
        /// budget (only a genuinely invalid budget &lt;= 0
        /// still throws SafetyLimitException, same pattern WikiSmwClient's
        /// own query safety limits use) on how many NEW pages a single run
        /// will fetch, so an accidental full-dataset run with the flag set
        /// does not silently attempt thousands of live requests in one go.
        /// When there are more uncached pages than the budget allows, this
        /// method fetches only up to the budget, saves the cache as usual,
        /// and logs how many pages remain - it does NOT throw. The next
        /// run's own toFetch list is smaller (this run's fetches are now
        /// cached), so repeated runs converge on full coverage instead of
        /// every run past the first throwing on the same unmet budget.
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
        /// <paramref name="queryScopedResults"/>
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
            // Hard-abort fix: defense in depth - Program.cs's
            // own arg parsing already rejects --max-seasonal-pages <= 0
            // before this method is ever called from RunAsync, but this
            // method is also called directly by tests and could in
            // principle be called by a future caller that skips that
            // check. A budget of 0 or less is not a normal "run out of
            // budget, continue next time" case (see the self-healing fetch-
            // up-to-budget logic below) - it means no run could ever make
            // progress, so it stays a genuine SafetyLimitException.
            if (maxSeasonalPages <= 0)
            {
                throw new SafetyLimitException(
                    $"--max-seasonal-pages must be a positive integer, got {maxSeasonalPages}.");
            }

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
                int totalUncached = toFetch.Count;

                // Self-healing fix: a from-scratch run against
                // a real dataset (thousands of distinct vendor pages) with
                // no prior ref/seasonal_wikitext_cache.json (gitignored,
                // empty on a fresh clone) used to hit this budget on its
                // very first invocation and throw SafetyLimitException
                // BEFORE fetching anything - the run exited non-zero, Pass
                // 2 never ran, and a re-run made no progress at all (same
                // empty cache, same over-budget toFetch, same throw).
                // Instead, fetch only UP TO the budget this run, save
                // whatever was fetched (the existing try/finally below
                // already does this), and leave the rest for a later run -
                // next time, this run's own newly-cached pages shrink
                // toFetch, so repeated runs make steady forward progress
                // and the overall process converges instead of looping
                // forever on the same failure. See the maxSeasonalPages
                // <= 0 check above for the one budget shape that still
                // hard-aborts.
                if (toFetch.Count > maxSeasonalPages)
                {
                    int remaining = toFetch.Count - maxSeasonalPages;
                    Console.WriteLine(
                        $"NOTE: {toFetch.Count} vendor page(s) need seasonal tagging, but " +
                        $"the --max-seasonal-pages budget for this run is {maxSeasonalPages}. " +
                        $"Fetching {maxSeasonalPages} page(s) now; {remaining} page(s) remain " +
                        "and will be picked up by a subsequent run (this run's cache save " +
                        "covers everything fetched below, so the remaining count only shrinks " +
                        "from here).");
                    toFetch = toFetch.Take(maxSeasonalPages).ToList();
                }

                Console.WriteLine(
                    $"Resolving seasonal festival tags for {toFetch.Count} " +
                    $"uncached vendor page(s) ({distinctPageNames.Count - totalUncached} already cached)...");

                int effectiveDelay = Math.Max(200, delayMs);

                // The try/finally below saves whatever this pass fetched
                // no matter how the loop exits (Ctrl-C, parse failure,
                // transport error), so a mid-run failure keeps the pages
                // already fetched; a parse failure is treated exactly like
                // an HTTP failure (warn, leave this one page uncached,
                // continue with the rest).
                try
                {
                    for (int i = 0; i < toFetch.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();

                        string pageName = toFetch[i];

                        // Throttle-class fix: the inter-
                        // request delay used to sit ONLY after a
                        // successful fetch+parse, guarded by the same
                        // "not the last item" check now on the finally
                        // below. Every `continue` above it (HTTP failure,
                        // JSON parse failure, null wikitext) skipped the
                        // delay entirely, so a stretch of missing/failing
                        // pages issued back-to-back requests against
                        // api.guildwars2.com with no throttling at all,
                        // defeating both --delay and the 200ms floor. A
                        // `finally` runs on every exit from the try below
                        // - success, a `continue`, or an uncaught
                        // exception propagating out - so moving the delay
                        // here makes every iteration throttle uniformly,
                        // not just the ones that happen to succeed.
                        try
                        {
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

                            // A null wikitext (missing/
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
                        }
                        finally
                        {
                            if (i + 1 < toFetch.Count)
                            {
                                await Task.Delay(effectiveDelay, ct);
                            }
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
                    // Assign
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

        // Reserved dictionary key (not a real
        // wiki page title - none contain "__") that stores the cache
        // format version alongside the real page entries, so a version
        // bump can force a one-time recheck of entries written before a
        // fix that changes what "" ("checked - no tag") means. See
        // SeasonalWikitextCacheVersion's own doc comment.
        private const string SeasonalWikitextCacheVersionKey = "__cache_version__";

        // Bump this when a fix changes the MEANING of an already-cached
        // value, so LoadSeasonalWikitextCache purges the affected entries
        // instead of trusting them forever. Current bump (2 - the
        // &redirects=1 fix, WikiSmwClient.FetchWikitextAsync): before that
        // fix, a redirected vendor page's wikitext came back as
        // "#REDIRECT [[Target]]", TemplateRegex found no {{Temporary}}
        // template in that, and the caller cached it as "" - identical to
        // a real, deliberate "checked, not tagged" - so those pages were
        // never retried even though the fix would resolve them correctly.
        // A missing/older version number purges every "" entry (the only
        // ones that ambiguity could have affected; a non-empty resolved
        // value was never subject to it) so they get one clean re-fetch.
        private const int SeasonalWikitextCacheVersion = 2;

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

                bool isCurrentVersion = cache.TryGetValue(SeasonalWikitextCacheVersionKey, out var versionText)
                    && versionText == SeasonalWikitextCacheVersion.ToString(CultureInfo.InvariantCulture);
                cache.Remove(SeasonalWikitextCacheVersionKey);

                if (!isCurrentVersion)
                {
                    var staleEmptyKeys = cache
                        .Where(kv => kv.Value.Length == 0)
                        .Select(kv => kv.Key)
                        .ToList();
                    foreach (var key in staleEmptyKeys)
                    {
                        cache.Remove(key);
                    }
                    if (staleEmptyKeys.Count > 0)
                    {
                        Console.WriteLine(
                            $"Seasonal wikitext cache version bump: cleared {staleEmptyKeys.Count} " +
                            "pre-redirects-fix \"\" entr" +
                            (staleEmptyKeys.Count == 1 ? "y" : "ies") +
                            " for recheck.");
                    }
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
            var toWrite = new Dictionary<string, string>(cache, StringComparer.Ordinal)
            {
                [SeasonalWikitextCacheVersionKey] =
                    SeasonalWikitextCacheVersion.ToString(CultureInfo.InvariantCulture)
            };

            var sorted = toWrite
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(sorted, options);

            // Unlike
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
        /// --diff-summary: reports what changed between two vendor datasets.
        /// Read-only - it exists so a `data(vendor):` pull request can carry a
        /// reviewable summary of a change whose own diff is one 14.8MB line.
        /// See <see cref="VendorOfferDiff"/> for what "changed" means here.
        /// </summary>
        private static async Task<int> RunDiffSummaryAsync(string beforePath, string afterPath)
        {
            foreach (string path in new[] { beforePath, afterPath })
            {
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"ERROR: --diff-summary input not found: {path}");
                    return 1;
                }
            }

            var readOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var before = JsonSerializer.Deserialize<VendorOfferDataset>(
                await File.ReadAllTextAsync(beforePath), readOptions);
            var after = JsonSerializer.Deserialize<VendorOfferDataset>(
                await File.ReadAllTextAsync(afterPath), readOptions);

            if (before == null || after == null)
            {
                Console.Error.WriteLine(
                    "ERROR: --diff-summary input deserialized to null (empty or malformed JSON).");
                return 1;
            }

            var result = VendorOfferDiff.Compute(before.Offers, after.Offers);
            Console.Write(VendorOfferDiff.Format(
                result, Path.GetFileName(beforePath), Path.GetFileName(afterPath)));

            return 0;
        }

        /// <summary>
        /// Serializes a dataset into exactly the byte form
        /// ref/vendor_offers.json is checked in as.
        /// <para>
        /// System.Text.Json's DEFAULT encoder conservatively HTML-escapes
        /// '\'', '&amp;', '&lt;', '&gt;' (as <c>&amp;#x27;</c> etc.) even for pure JSON
        /// output with no HTML context - but the already-checked-in
        /// ref/vendor_offers.json never does this (confirmed: 222 literal
        /// '&amp;' characters, 0 <c>&amp;#x26;</c> escapes; "Hearth's Glow" stored with a
        /// literal apostrophe). UnsafeRelaxedJsonEscaping skips that extra
        /// HTML-safety escaping (while still escaping the JSON-mandatory
        /// '"'/'\\'/control characters) but ALSO stops escaping non-ASCII
        /// text, which the existing file DOES do (e.g. "Homestead
        /// Refinement-Farm"). <see cref="EscapeNonAscii"/> restores exactly
        /// that: non-ASCII escaped, everything else literal - matching the
        /// existing file's convention so a scoped --merge-into run's diff
        /// stays confined to the offers actually changed, not every
        /// apostrophe/ampersand in the whole 53k-row dataset.
        /// </para>
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static string SerializeDataset(VendorOfferDataset dataset)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            return EscapeNonAscii(JsonSerializer.Serialize(dataset, jsonOptions));
        }

        /// <summary>
        /// Lowercase hex SHA-256 of a file's bytes. Same definition the
        /// module side uses (RecipeCacheSerializer.HashFile) - this project
        /// does not reference the module, so the four lines are repeated
        /// rather than shared, and both are pinned by the seed tests that
        /// compare a manifest digest against the file it describes.
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static string HashFile(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        /// <summary>
        /// Sibling manifest path for a dataset path: ref/vendor_offers.json
        /// becomes ref/vendor_offers_manifest.json. Derived from the dataset
        /// path rather than hardcoded so a --merge-into run against a scratch
        /// copy writes its manifest next to that copy, not over the shipped one.
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static string ManifestPathFor(string datasetPath)
        {
            string? dir = Path.GetDirectoryName(datasetPath);
            string name = Path.GetFileNameWithoutExtension(datasetPath) + "_manifest.json";
            return string.IsNullOrEmpty(dir) ? name : Path.Combine(dir, name);
        }

        /// <summary>
        /// Indented, newline-terminated, camelCase - the manifest is meant to
        /// be read in a diff, unlike the dataset it describes.
        /// <para>
        /// The line endings are forced to LF. System.Text.Json's WriteIndented
        /// emits Environment.NewLine, so the same manifest content would be
        /// written as CRLF on Windows and LF elsewhere - a determinism bug in
        /// the file whose entire job is to make no-op refreshes provable.
        /// (JsonSerializerOptions.NewLine only exists from .NET 9; this project
        /// targets net8.0.) No manifest value can contain a newline, so the
        /// replace cannot touch anything but the formatting.
        /// </para>
        /// </summary>
        // internal for testability (VendorOfferUpdater.Tests)
        internal static string SerializeManifest(VendorOfferManifest manifest)
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(manifest, jsonOptions);
            return json.Replace("\r\n", "\n") + "\n";
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

        /// <summary>
        /// Removes offers named in ref/vendor_offer_exclusions.json. A
        /// missing or unreadable file is a warning, not a failure - the
        /// refresh still produces data, it just carries rows a human had
        /// refused, which the module's own agreement test then catches.
        /// </summary>
        internal static int ApplyExclusions(ref List<VendorOffer> offers, string outputDir)
        {
            string path = Path.Combine(outputDir, "vendor_offer_exclusions.json");
            if (!File.Exists(path))
            {
                Console.Error.WriteLine(
                    $"Warning: no exclusion list at {path} - shipping every scraped row.");
                return 0;
            }

            var refused = new HashSet<(string Merchant, int ItemId)>();
            try
            {
                using (var doc = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    if (doc.RootElement.TryGetProperty("exclusions", out var arr))
                    {
                        foreach (var entry in arr.EnumerateArray())
                        {
                            if (entry.TryGetProperty("merchantName", out var m) &&
                                entry.TryGetProperty("outputItemId", out var i))
                            {
                                refused.Add((m.GetString() ?? string.Empty, i.GetInt32()));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Warning: could not read the exclusion list: {ex.Message}");
                return 0;
            }

            if (refused.Count == 0)
            {
                return 0;
            }

            int before = offers.Count;
            offers = offers
                .Where(o => !refused.Contains((o.MerchantName ?? string.Empty, o.OutputItemId)))
                .ToList();
            return before - offers.Count;
        }

    }
}
