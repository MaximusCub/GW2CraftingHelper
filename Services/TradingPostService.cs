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

        public TradingPostService(IPriceApiClient api, Func<DateTime> utcNow = null)
        {
            _api = api;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public async Task<IReadOnlyDictionary<int, ItemPrice>> GetPricesAsync(
            IEnumerable<int> itemIds, CancellationToken ct)
        {
            var uniqueIds = new HashSet<int>(itemIds);
            var toFetch = new List<int>();
            var now = _utcNow();

            lock (_cacheLock)
            {
                foreach (var id in uniqueIds)
                {
                    if (!_cache.TryGetValue(id, out var cached) || now - cached.FetchedUtc >= CacheTtl)
                    {
                        toFetch.Add(id);
                    }
                }
            }

            for (int i = 0; i < toFetch.Count; i += BatchSize)
            {
                int count = Math.Min(BatchSize, toFetch.Count - i);
                var batch = toFetch.GetRange(i, count);
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
                        _cache[entry.Id] = (price, now);
                    }
                }
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
    }
}
