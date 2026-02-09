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

        private readonly IPriceApiClient _api;
        private readonly Dictionary<int, ItemPrice> _cache = new Dictionary<int, ItemPrice>();

        public TradingPostService(IPriceApiClient api)
        {
            _api = api;
        }

        public async Task<IReadOnlyDictionary<int, ItemPrice>> GetPricesAsync(
            IEnumerable<int> itemIds, CancellationToken ct)
        {
            var uniqueIds = new HashSet<int>(itemIds);
            var toFetch = new List<int>();

            foreach (var id in uniqueIds)
            {
                if (!_cache.ContainsKey(id))
                {
                    toFetch.Add(id);
                }
            }

            for (int i = 0; i < toFetch.Count; i += BatchSize)
            {
                int count = Math.Min(BatchSize, toFetch.Count - i);
                var batch = toFetch.GetRange(i, count);
                var entries = await _api.GetPricesAsync(batch, ct);

                foreach (var entry in entries)
                {
                    var price = new ItemPrice
                    {
                        ItemId = entry.Id,
                        BuyInstant = entry.SellUnitPrice,
                        SellInstant = entry.BuyUnitPrice
                    };
                    _cache[entry.Id] = price;
                }
            }

            var result = new Dictionary<int, ItemPrice>();
            foreach (var id in uniqueIds)
            {
                if (_cache.TryGetValue(id, out var price))
                {
                    result[id] = price;
                }
            }

            return result;
        }
    }
}
