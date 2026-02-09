using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class ItemMetadataService
    {
        private const int BatchSize = 200;

        private readonly IItemApiClient _api;
        private readonly Dictionary<int, ItemMetadata> _cache = new Dictionary<int, ItemMetadata>();

        public ItemMetadataService(IItemApiClient api)
        {
            _api = api;
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
                var entries = await _api.GetItemsAsync(batch, ct);

                foreach (var entry in entries)
                {
                    var meta = new ItemMetadata
                    {
                        ItemId = entry.Id,
                        Name = entry.Name,
                        IconUrl = entry.Icon
                    };
                    _cache[entry.Id] = meta;
                }
            }

            var result = new Dictionary<int, ItemMetadata>();
            foreach (var id in uniqueIds)
            {
                if (_cache.TryGetValue(id, out var meta))
                {
                    result[id] = meta;
                }
            }

            return result;
        }
    }
}
