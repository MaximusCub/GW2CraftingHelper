using System;
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

        /// <summary>
        /// Ids omitted from the next response that requests them (then
        /// cleared), simulating the API's transient partial responses.
        /// </summary>
        public HashSet<int> DropOnce { get; } = new HashSet<int>();

        /// <summary>
        /// 1-based call number (counted across this client's lifetime) on
        /// which GetItemsAsync should throw instead of returning, simulating
        /// a transient API failure. 0 (default) disables this.
        /// </summary>
        public int ThrowOnCallNumber { get; set; }

        public void AddItem(int id, string name, string icon, string rarity = null, List<string> flags = null)
        {
            _items[id] = new RawItem { Id = id, Name = name, Icon = icon, Rarity = rarity, Flags = flags };
        }

        public Task<IReadOnlyList<RawItem>> GetItemsAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct)
        {
            _calls.Add(itemIds);

            if (ThrowOnCallNumber > 0 && _calls.Count == ThrowOnCallNumber)
            {
                throw new InvalidOperationException("Simulated transient API failure.");
            }

            var results = itemIds
                .Where(id => _items.ContainsKey(id) && !DropOnce.Contains(id))
                .Select(id => _items[id])
                .ToList();

            foreach (var id in itemIds)
            {
                DropOnce.Remove(id);
            }

            return Task.FromResult<IReadOnlyList<RawItem>>(results);
        }
    }
}
