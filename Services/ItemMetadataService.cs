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
        private readonly Dictionary<int, ItemMetadata> _cache = new Dictionary<int, ItemMetadata>();
        private readonly Dictionary<int, ItemNameEntry> _seedById;

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
                await FetchBatchIntoCacheAsync(batch, ct);
            }

            // The items endpoint can return partial results (206) or drop
            // ids transiently; retry just the stragglers once so a single
            // flaky response does not leave permanent icon/name holes.
            var missing = toFetch.Where(id => !_cache.ContainsKey(id)).ToList();
            if (missing.Count > 0)
            {
                for (int i = 0; i < missing.Count; i += BatchSize)
                {
                    int count = Math.Min(BatchSize, missing.Count - i);
                    await FetchBatchIntoCacheAsync(missing.GetRange(i, count), ct);
                }
            }

            var result = new Dictionary<int, ItemMetadata>();
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
                        IconUrl = seed.Icon
                    };
                }
            }

            return result;
        }

        private async Task FetchBatchIntoCacheAsync(List<int> batch, CancellationToken ct)
        {
            var entries = await _api.GetItemsAsync(batch, ct);

            foreach (var entry in entries)
            {
                var meta = new ItemMetadata
                {
                    ItemId = entry.Id,
                    Name = entry.Name,
                    IconUrl = entry.Icon,
                    Rarity = entry.Rarity
                };
                _cache[entry.Id] = meta;
            }
        }
    }
}
