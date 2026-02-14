using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class VendorOfferResolver
    {
        private readonly IWikiVendorClient _wiki;
        private readonly VendorOfferStore _store;
        private readonly WikiLookupOptions _options;

        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly object _rateLock = new object();
        private readonly Stopwatch _stopwatch;
        private readonly Random _jitterRng = new Random();
        private long _lastRequestMs;

        public VendorOfferResolver(
            IWikiVendorClient wiki,
            VendorOfferStore store,
            WikiLookupOptions options = null)
        {
            _wiki = wiki ?? throw new ArgumentNullException(nameof(wiki));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _options = options ?? new WikiLookupOptions();
            _concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrentRequests);
            _stopwatch = Stopwatch.StartNew();
            _lastRequestMs = -_options.MinDelayBetweenRequestsMs;
        }

        public async Task<ResolveResult> EnsureVendorOffersAsync(
            IEnumerable<int> requiredIds,
            IProgress<PlanStatus> progress,
            CancellationToken ct)
        {
            var distinct = requiredIds.Distinct().ToList();
            var missing = distinct.Where(id => !_store.HasAnyOffer(id)).ToList();

            var result = new ResolveResult
            {
                ItemsChecked = missing.Count,
                OffersAdded = 0,
                FailedItemIds = new List<int>()
            };

            if (missing.Count == 0)
            {
                return result;
            }

            progress?.Report(new PlanStatus
            {
                Message = $"Queued {missing.Count} vendor lookups",
                Current = 0,
                Total = missing.Count
            });

            var allOffers = new List<VendorOffer>();
            var failedIds = new List<int>();
            int completed = 0;
            Func<int> incrementCompleted = () => Interlocked.Increment(ref completed);

            var tasks = missing.Select(itemId => ProcessItemAsync(
                itemId, allOffers, failedIds, progress, missing.Count, incrementCompleted, ct));

            await Task.WhenAll(tasks);

            if (allOffers.Count > 0)
            {
                _store.AddOffersToOverlay(allOffers);
            }

            result.OffersAdded = allOffers.Count;
            result.FailedItemIds = failedIds;
            return result;
        }

        private async Task ProcessItemAsync(
            int itemId,
            List<VendorOffer> allOffers,
            List<int> failedIds,
            IProgress<PlanStatus> progress,
            int totalCount,
            Func<int> incrementCompleted,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            await _concurrencySemaphore.WaitAsync(ct);
            try
            {
                await WaitForRateLimitAsync(ct);

                var rawOffers = await FetchWithRetryAsync(itemId, ct);

                if (rawOffers != null)
                {
                    var converted = rawOffers
                        .Select(raw => ConvertToVendorOffer(raw))
                        .Where(o => o != null)
                        .ToList();

                    if (converted.Count > 0)
                    {
                        lock (allOffers)
                        {
                            allOffers.AddRange(converted);
                        }
                    }
                }
                else
                {
                    lock (failedIds)
                    {
                        failedIds.Add(itemId);
                    }
                }

                int current = incrementCompleted();
                progress?.Report(new PlanStatus
                {
                    Message = $"Checking vendor data ({current}/{totalCount})...",
                    Current = current,
                    Total = totalCount
                });
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }

        private async Task<IReadOnlyList<RawWikiVendorOffer>> FetchWithRetryAsync(
            int itemId, CancellationToken ct)
        {
            for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    return await _wiki.GetVendorOffersForItemAsync(itemId, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    if (attempt >= _options.MaxRetries)
                    {
                        return null;
                    }

                    int backoffMs = 500 * (1 << attempt);
                    await Task.Delay(backoffMs, ct);
                }
            }

            return null;
        }

        private async Task WaitForRateLimitAsync(CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                int delayNeeded;
                lock (_rateLock)
                {
                    long now = _stopwatch.ElapsedMilliseconds;
                    long elapsed = now - _lastRequestMs;
                    int minDelay = _options.MinDelayBetweenRequestsMs;

                    if (elapsed >= minDelay)
                    {
                        _lastRequestMs = now;
                        return;
                    }

                    delayNeeded = (int)(minDelay - elapsed);
                    if (_options.JitterMs > 0)
                    {
                        delayNeeded += _jitterRng.Next(0, _options.JitterMs);
                    }
                    delayNeeded = Math.Max(0, delayNeeded);
                }

                await Task.Delay(delayNeeded, ct);

                lock (_rateLock)
                {
                    long now = _stopwatch.ElapsedMilliseconds;
                    long elapsed = now - _lastRequestMs;
                    if (elapsed >= _options.MinDelayBetweenRequestsMs)
                    {
                        _lastRequestMs = now;
                        return;
                    }
                }
            }
        }

        private static VendorOffer ConvertToVendorOffer(RawWikiVendorOffer raw)
        {
            if (raw == null || raw.OutputItemId <= 0 || raw.OutputCount <= 0)
            {
                return null;
            }

            var costLines = raw.CostLines ?? new List<CostLine>();
            var locations = raw.Locations ?? new List<string>();

            string offerId = VendorOfferHasher.ComputeOfferId(
                raw.OutputItemId,
                raw.OutputCount,
                costLines,
                raw.MerchantName,
                locations,
                raw.DailyCap,
                raw.WeeklyCap);

            return new VendorOffer
            {
                OfferId = offerId,
                OutputItemId = raw.OutputItemId,
                OutputCount = raw.OutputCount,
                CostLines = new List<CostLine>(costLines),
                MerchantName = raw.MerchantName,
                Locations = new List<string>(locations),
                DailyCap = raw.DailyCap,
                WeeklyCap = raw.WeeklyCap
            };
        }
    }
}
