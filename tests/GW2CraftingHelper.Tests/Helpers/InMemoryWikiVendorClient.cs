using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Tests.Helpers
{
    public class InMemoryWikiVendorClient : IWikiVendorClient
    {
        private readonly ConcurrentDictionary<int, List<RawWikiVendorOffer>> _offers
            = new ConcurrentDictionary<int, List<RawWikiVendorOffer>>();

        private readonly ConcurrentBag<int> _calledItemIds = new ConcurrentBag<int>();
        private readonly ConcurrentDictionary<int, int> _failuresRemaining
            = new ConcurrentDictionary<int, int>();

        private int _currentConcurrency;
        private int _maxObservedConcurrency;

        public IReadOnlyCollection<int> CalledItemIds => _calledItemIds;
        public int MaxObservedConcurrency => _maxObservedConcurrency;
        public int LatencyMs { get; set; } = 50;

        public void AddOffers(int itemId, params RawWikiVendorOffer[] offers)
        {
            var list = _offers.GetOrAdd(itemId, _ => new List<RawWikiVendorOffer>());
            list.AddRange(offers);
        }

        public void SetFailures(int itemId, int failCount)
        {
            _failuresRemaining[itemId] = failCount;
        }

        public async Task<IReadOnlyList<RawWikiVendorOffer>> GetVendorOffersForItemAsync(
            int itemId, CancellationToken ct)
        {
            int concurrent = Interlocked.Increment(ref _currentConcurrency);
            try
            {
                int max;
                do
                {
                    max = _maxObservedConcurrency;
                    if (concurrent <= max) break;
                }
                while (Interlocked.CompareExchange(
                    ref _maxObservedConcurrency, concurrent, max) != max);

                _calledItemIds.Add(itemId);

                if (LatencyMs > 0)
                {
                    await Task.Delay(LatencyMs, ct);
                }

                if (_failuresRemaining.TryGetValue(itemId, out int remaining) && remaining > 0)
                {
                    _failuresRemaining[itemId] = remaining - 1;
                    throw new Exception($"Simulated failure for item {itemId}");
                }

                if (_offers.TryGetValue(itemId, out var offers))
                {
                    return offers;
                }

                return Array.Empty<RawWikiVendorOffer>();
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }
    }
}
