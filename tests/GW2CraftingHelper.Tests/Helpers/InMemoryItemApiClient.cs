using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Tests.Helpers
{
    public class InMemoryItemApiClient : IItemApiClient
    {
        private readonly Dictionary<int, RawItem> _items = new Dictionary<int, RawItem>();
        private readonly List<IReadOnlyList<int>> _calls = new List<IReadOnlyList<int>>();

        public IReadOnlyList<IReadOnlyList<int>> Calls => _calls;

        public void AddItem(int id, string name, string icon)
        {
            _items[id] = new RawItem { Id = id, Name = name, Icon = icon };
        }

        public Task<IReadOnlyList<RawItem>> GetItemsAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct)
        {
            _calls.Add(itemIds);

            var results = itemIds
                .Where(id => _items.ContainsKey(id))
                .Select(id => _items[id])
                .ToList();

            return Task.FromResult<IReadOnlyList<RawItem>>(results);
        }
    }
}
