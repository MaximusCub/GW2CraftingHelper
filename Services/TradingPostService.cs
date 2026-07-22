using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class TradingPostService
    {
        private const int BatchSize = 200;

        // The GW2 commerce API refreshes trading post prices on its own short
        // upstream cache cycle; a 15 minute local TTL keeps this cache from
        // drifting far behind that cycle while still avoiding a re-fetch on
        // every request.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        private readonly IPriceApiClient _api;
        private readonly Func<DateTime> _utcNow;
        private readonly object _cacheLock = new object();
        private readonly Dictionary<int, (ItemPrice Price, DateTime FetchedUtc)> _cache =
            new Dictionary<int, (ItemPrice Price, DateTime FetchedUtc)>();

        // KNOWN-ISSUES 31c-1: per-id in-flight tracking so a second
        // overlapping GetPricesAsync call that needs an id this call is
        // already fetching awaits THIS call's fetch instead of starting a
        // duplicate one. Every not-yet-cached id a given call decides to
        // fetch itself is registered here, all pointing at the same
        // FetchOwnBatchesAsync Task for that call. Only ever mutated under
        // _cacheLock, and never while the lock is held across an await -
        // every access below is a plain synchronous dictionary op.
        private readonly Dictionary<int, Task> _inFlight = new Dictionary<int, Task>();

        public TradingPostService(IPriceApiClient api, Func<DateTime> utcNow = null)
        {
            _api = api;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public async Task<IReadOnlyDictionary<int, ItemPrice>> GetPricesAsync(
            IEnumerable<int> itemIds, CancellationToken ct)
        {
            var uniqueIds = new HashSet<int>(itemIds);
            var now = _utcNow();

            // joinTasks: another overlapping call's own in-flight fetch
            // that already covers one or more of our ids - we wait on it
            // instead of re-fetching (KNOWN-ISSUES 31c-1). ownTask: this
            // call's own fetch for whatever ids are neither cache-fresh nor
            // already in flight elsewhere, or null if nothing needs
            // fetching.
            //
            // Deciding which ids are fresh/in-flight/covered and
            // registering this call's own fetch into _inFlight happen
            // inside ONE lock acquisition - splitting that into two
            // separate lock statements would leave a window where a second
            // overlapping caller's own decide-phase runs before this call's
            // ids are visible in _inFlight, letting both callers claim the
            // same "fresh" id and duplicate-fetch it (defeating the point
            // of this guard). FetchOwnBatchesAsync (see its own doc
            // comment) always suspends before doing any real work, so
            // calling it from inside this lock is safe - it can never race
            // ahead of its own registration below.
            var joinTasks = new List<Task>();
            Task ownTask = null;

            lock (_cacheLock)
            {
                var freshIds = new List<int>();
                foreach (var id in uniqueIds)
                {
                    if (_cache.TryGetValue(id, out var cached) && now - cached.FetchedUtc < CacheTtl)
                    {
                        continue;
                    }

                    if (_inFlight.TryGetValue(id, out var existingFetch))
                    {
                        joinTasks.Add(existingFetch);
                    }
                    else
                    {
                        freshIds.Add(id);
                    }
                }

                if (freshIds.Count > 0)
                {
                    ownTask = FetchOwnBatchesAsync(freshIds, now, ct);
                    foreach (var id in freshIds)
                    {
                        _inFlight[id] = ownTask;
                    }
                }
            }

            // Every fetch operation relevant to THIS call's request -
            // whether it is this call's own (possibly multi-batch) fetch or
            // another overlapping caller's in-flight fetch we are joining -
            // counts toward the total-failure tally below.
            // FetchOwnBatchesAsync already degrades a single failing batch
            // to holes internally (KNOWN-ISSUES api-degradation F2) and
            // only faults if ALL of its own batches failed; a caller whose
            // entire request is satisfied purely via joined fetches must
            // still see a thrown error if every one of those also failed,
            // exactly like a caller with its own failed fetch - otherwise
            // it would silently render an all-unpriceable plan instead of
            // surfacing "Refresh failed"/"Error: ...".
            Exception lastFailure = null;
            int attempted = 0;
            int succeeded = 0;

            if (ownTask != null)
            {
                attempted++;
                try
                {
                    await ownTask;
                    succeeded++;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    lastFailure = ex;
                }
            }

            foreach (var task in joinTasks.Distinct())
            {
                attempted++;
                try
                {
                    await task;
                    succeeded++;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    // The other caller's fetch failed; the ids it covered
                    // stay missing from _cache -> unpriceable holes below,
                    // same degraded state as this call's own failed batch.
                    lastFailure = ex;
                }
            }

            if (attempted > 0 && succeeded == 0)
            {
                throw lastFailure;
            }

            var result = new Dictionary<int, ItemPrice>();
            lock (_cacheLock)
            {
                foreach (var id in uniqueIds)
                {
                    if (_cache.TryGetValue(id, out var cached) && now - cached.FetchedUtc < CacheTtl)
                    {
                        result[id] = cached.Price;
                    }
                }
            }

            return result;
        }

        // Fetches every batch of `ids` this call itself owns, strictly one
        // batch at a time in order - identical sequencing to this method's
        // pre-M37 inline for-loop, so a single caller's own batch
        // count/order/timing is unaffected by the KNOWN-ISSUES 31c-1
        // coalescing added around it.
        private async Task FetchOwnBatchesAsync(List<int> ids, DateTime fetchedUtc, CancellationToken ct)
        {
            // Always suspend here first, before doing any real work. The
            // caller (GetPricesAsync) invokes this method from inside
            // _cacheLock and then immediately registers the returned Task
            // into _inFlight for every id in `ids`. Without this yield, an
            // IPriceApiClient that completes synchronously (a fake used in
            // tests, or any future in-process fast path) could run this
            // whole method - including the finally's _inFlight cleanup
            // below - to completion before the caller has actually written
            // those _inFlight entries. The cleanup would then remove
            // nothing (the entries do not exist yet) and the caller's
            // subsequent write would leave a stale, never-cleaned-up entry
            // in _inFlight forever - silently breaking TTL re-fetches for
            // those ids on every later call.
            await Task.Yield();

            try
            {
                // KNOWN-ISSUES api-degradation F2: a single failing batch
                // degrades to missing ids (unpriceable holes downstream, an
                // already-supported state) instead of aborting the whole
                // fetch - mirroring ItemMetadataService's retry-wave catch.
                // Re-thrown at the end only if EVERY batch failed (a
                // genuine total price-API outage), so a real outage still
                // surfaces as an error instead of silently rendering an
                // all-unpriceable plan.
                Exception lastBatchFailure = null;
                int succeededBatches = 0;
                int batchCount = 0;

                for (int i = 0; i < ids.Count; i += BatchSize)
                {
                    int count = Math.Min(BatchSize, ids.Count - i);
                    var batch = ids.GetRange(i, count);
                    batchCount++;

                    try
                    {
                        var entries = await _api.GetPricesAsync(batch, ct);

                        lock (_cacheLock)
                        {
                            foreach (var entry in entries)
                            {
                                var price = new ItemPrice
                                {
                                    ItemId = entry.Id,
                                    BuyInstant = entry.SellUnitPrice,
                                    SellInstant = entry.BuyUnitPrice
                                };
                                _cache[entry.Id] = (price, fetchedUtc);
                            }
                        }
                        succeededBatches++;
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        lastBatchFailure = ex;
                    }
                }

                if (batchCount > 0 && succeededBatches == 0)
                {
                    throw lastBatchFailure;
                }
            }
            finally
            {
                lock (_cacheLock)
                {
                    foreach (var id in ids)
                    {
                        _inFlight.Remove(id);
                    }
                }
            }
        }
    }
}
