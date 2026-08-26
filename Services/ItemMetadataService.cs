using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services.Recipes;

namespace GW2CraftingHelper.Services
{
    public class ItemMetadataService
    {
        private const int BatchSize = 200;

        private readonly IItemApiClient _api;

        // Deliberately unbounded/TTL-less, unlike TradingPostService's
        // 15-minute price cache: item metadata (name/icon/rarity) does not
        // go stale the way a market price does, so there is no correctness
        // reason to ever evict or re-fetch an entry within a module session.
        // Growth is naturally rate-limited by how many distinct item ids a
        // player can look up by hand in one session (a few MB even at
        // 10,000 distinct items), so this mirrors CurrencyMetadataService's
        // own unbounded cache rather than TradingPostService's TTL pattern.
        //
        // LOCKED, like every sibling service's cache (TradingPostService's
        // _cacheLock, RecipeService's _cacheGate, CurrencyMetadataService's
        // _cacheLock). One ItemMetadataService instance is shared by every
        // plan generation in a session, and the generate path is re-entrant:
        // "Use Own Materials" stays clickable while a generation runs and its
        // confirm callback starts another one, so two GetMetadataAsync calls
        // can be inside this class at once. An earlier comment here claimed
        // the opposite and left both collections unguarded; concurrent writes
        // to a .NET Framework Dictionary/HashSet do not merely lose entries,
        // they throw out of a racing resize or spin Insert forever - a
        // 100%-CPU freeze of the host game. See
        // ItemMetadataServiceConcurrencyTests, which fails without this gate.
        private readonly object _cacheLock = new object();
        private readonly Dictionary<int, ItemMetadata> _cache = new Dictionary<int, ItemMetadata>();
        private readonly Dictionary<int, ItemNameEntry> _seedById;

        // Item stat blocks, filled from the SAME /v2/items response the
        // metadata above comes from - zero additional requests. A side
        // table rather than a field on ItemMetadata because ItemMetadata is
        // reachable from PersistedPlan and guarded against its schema
        // version; see ItemStatBlock's own doc comment.
        //
        // Its own gate rather than _cacheLock: stat blocks are read straight
        // out of here by the UI thread at render time (a tree row's tooltip)
        // while a background generation writes the next plan's items in, so
        // this lock is taken on the hover path and must not queue behind a
        // generation's metadata bookkeeping.
        private readonly object _statBlocksLock = new object();
        private readonly Dictionary<int, ItemStatBlock> _statBlocks = new Dictionary<int, ItemStatBlock>();

        // Ids confirmed absent from the API after a first-wave + retry
        // round trip. Skipped on every later toFetch so a genuinely-missing
        // id does not pay the double round-trip cost on every plan
        // generation; misses stay negative-cached for the service's
        // lifetime (module session).
        private readonly HashSet<int> _knownMissing = new HashSet<int>();

        public ItemMetadataService(IItemApiClient api, ItemNameSeedData seedFallback = null)
        {
            _api = api;
            if (seedFallback != null)
            {
                _seedById = new Dictionary<int, ItemNameEntry>(seedFallback.Items.Count);
                foreach (var entry in seedFallback.Items)
                {
                    _seedById[entry.Id] = entry;
                }
            }
        }

        public async Task<IReadOnlyDictionary<int, ItemMetadata>> GetMetadataAsync(
            IEnumerable<int> itemIds, CancellationToken ct)
        {
            var uniqueIds = new HashSet<int>(itemIds);
            var toFetch = new List<int>();

            // Every _cache/_knownMissing touch in this method takes
            // _cacheLock, and none of them spans an await - the batch loops
            // below deliberately sit outside the lock so a network round trip
            // never holds it.
            lock (_cacheLock)
            {
                foreach (var id in uniqueIds)
                {
                    if (!_cache.ContainsKey(id) && !_knownMissing.Contains(id))
                    {
                        toFetch.Add(id);
                    }
                }
            }

            // KNOWN-ISSUES #31/api-degradation F3: a single hard-failing batch
            // must degrade to "treat this batch's ids as missing, fall
            // through to the retry wave/seed fallback below" instead of
            // aborting GetMetadataAsync entirely - mirroring the retry
            // wave's own per-batch catch a few lines down. An exception is
            // re-thrown below only if EVERY batch in this wave failed (a
            // genuine total outage), so a real outage still surfaces as an
            // error instead of silently rendering the whole plan with
            // Unknown Item/seed-fallback metadata.
            Exception firstWaveFailure = null;
            int firstWaveBatchCount = 0;
            int firstWaveSucceeded = 0;
            for (int i = 0; i < toFetch.Count; i += BatchSize)
            {
                int count = Math.Min(BatchSize, toFetch.Count - i);
                var batch = toFetch.GetRange(i, count);
                firstWaveBatchCount++;
                try
                {
                    await FetchBatchIntoCacheAsync(batch, ct);
                    firstWaveSucceeded++;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    firstWaveFailure = ex;
                }
            }

            if (firstWaveBatchCount > 0 && firstWaveSucceeded == 0)
            {
                throw firstWaveFailure;
            }

            // The items endpoint can return partial results (206) or drop
            // ids transiently; retry just the stragglers once so a single
            // flaky response does not leave permanent icon/name holes.
            List<int> missing;
            lock (_cacheLock)
            {
                missing = toFetch.Where(id => !_cache.ContainsKey(id)).ToList();
            }

            if (missing.Count > 0)
            {
                for (int i = 0; i < missing.Count; i += BatchSize)
                {
                    int count = Math.Min(BatchSize, missing.Count - i);
                    try
                    {
                        await FetchBatchIntoCacheAsync(missing.GetRange(i, count), ct);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        // The retry wave is best-effort: a transient failure
                        // here degrades to seed fallback/omission instead of
                        // aborting the whole plan generation. A total outage
                        // still surfaces via the first wave's throw above.
                    }
                }

                // Ids still missing after the retry wave are treated as
                // genuinely absent from the API for the rest of this
                // service's lifetime.
                lock (_cacheLock)
                {
                    foreach (var id in missing)
                    {
                        if (!_cache.ContainsKey(id))
                        {
                            _knownMissing.Add(id);
                        }
                    }
                }
            }

            var result = new Dictionary<int, ItemMetadata>();
            lock (_cacheLock)
            {
                foreach (var id in uniqueIds)
                {
                    if (_cache.TryGetValue(id, out var meta))
                    {
                        result[id] = meta;
                    }
                    else if (_seedById != null && _seedById.TryGetValue(id, out var seed))
                    {
                        // Last resort: bundled seed name/icon (no rarity). Not
                        // inserted into _cache so a later call retries the API.
                        result[id] = new ItemMetadata
                        {
                            ItemId = id,
                            Name = seed.Name,
                            IconUrl = seed.Icon,
                        };
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// This session's stat block for an item, or null when nothing has
        /// fetched it yet. A PURE CACHE READ - it never fetches, because
        /// its caller is a hover on the UI thread and a network round trip
        /// inside a hover window is not something the tooltip facility can
        /// cancel. Stats ride the plan's own metadata fetch instead.
        /// <para>
        /// Null is the normal answer for a plan restored from disk (nothing
        /// re-fetched its items), and callers must degrade to their
        /// pre-existing tooltip rather than showing an empty box - see
        /// KNOWN-ISSUES #40.
        /// </para>
        /// </summary>
        public ItemStatBlock GetCachedStatBlock(int itemId)
        {
            lock (_statBlocksLock)
            {
                return _statBlocks.TryGetValue(itemId, out var block) ? block : null;
            }
        }

        /// <summary>
        /// Fills the SESSION STAT CACHE for ids that have none, and does
        /// nothing else - the background top-up a plan restored from disk
        /// runs so its rows can show item tooltips at all (Q13). Returns
        /// how many blocks it added.
        /// <para>
        /// Deliberately NOT <see cref="GetMetadataAsync"/>: that method also
        /// fills the metadata cache and the negative cache, and a restore-time
        /// top-up has no business making a plan's items look freshly resolved
        /// or negative-caching an id the plan never asked for. Both caches are
        /// gated by <c>_cacheLock</c>, so calling it here would be safe - it
        /// would just do the wrong thing.
        /// </para>
        /// <para>
        /// Best effort by design: a failing batch is skipped, not thrown -
        /// the outcome of failing is exactly the pre-existing behaviour (a
        /// restored row with no stat block falls back to its plain
        /// tooltip), so an outage must not surface as an error here.
        /// </para>
        /// </summary>
        public async Task<int> WarmStatBlocksAsync(IEnumerable<int> itemIds, CancellationToken ct)
        {
            if (itemIds == null)
            {
                return 0;
            }

            var toFetch = new List<int>();
            lock (_statBlocksLock)
            {
                foreach (var id in new HashSet<int>(itemIds))
                {
                    if (id > 0 && !_statBlocks.ContainsKey(id))
                    {
                        toFetch.Add(id);
                    }
                }
            }

            int filled = 0;
            for (int i = 0; i < toFetch.Count; i += BatchSize)
            {
                ct.ThrowIfCancellationRequested();
                int count = Math.Min(BatchSize, toFetch.Count - i);
                IReadOnlyList<RawItem> entries;
                try
                {
                    entries = await _api.GetItemsAsync(toFetch.GetRange(i, count), ct);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    var statBlock = ItemStatBlockFactory.Build(entry);
                    if (statBlock == null)
                    {
                        continue;
                    }

                    lock (_statBlocksLock)
                    {
                        _statBlocks[entry.Id] = statBlock;
                    }

                    filled++;
                }
            }

            return filled;
        }

        private async Task FetchBatchIntoCacheAsync(List<int> batch, CancellationToken ct)
        {
            var entries = await _api.GetItemsAsync(batch, ct);

            foreach (var entry in entries)
            {
                var statBlock = ItemStatBlockFactory.Build(entry);
                if (statBlock != null)
                {
                    lock (_statBlocksLock)
                    {
                        _statBlocks[entry.Id] = statBlock;
                    }
                }

                var meta = new ItemMetadata
                {
                    ItemId = entry.Id,
                    Name = entry.Name,
                    IconUrl = entry.Icon,
                    Rarity = entry.Rarity,
                    // design-plan-notes.md (Notes section, excess/reclaim
                    // account-bound exclusion): null-tolerant even though
                    // the production Gw2ItemApiClient parser never returns
                    // a null Flags list - a test fixture or future client
                    // implementation might.
                    IsAccountBound = entry.Flags != null && entry.Flags.Contains("AccountBound"),
                };
                lock (_cacheLock)
                {
                    _cache[entry.Id] = meta;
                }
            }
        }
    }
}
