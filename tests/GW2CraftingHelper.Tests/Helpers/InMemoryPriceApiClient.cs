using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Tests.Helpers
{
    public class InMemoryPriceApiClient : IPriceApiClient
    {
        private readonly Dictionary<int, RawPriceEntry> _prices = new Dictionary<int, RawPriceEntry>();
        private readonly List<IReadOnlyList<int>> _calls = new List<IReadOnlyList<int>>();

        public IReadOnlyList<IReadOnlyList<int>> Calls => _calls;

        public void AddPrice(int itemId, int buyUnitPrice, int sellUnitPrice)
        {
            _prices[itemId] = new RawPriceEntry
            {
                Id = itemId,
                BuyUnitPrice = buyUnitPrice,
                SellUnitPrice = sellUnitPrice
            };
        }

        public Task<IReadOnlyList<RawPriceEntry>> GetPricesAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct)
        {
            _calls.Add(itemIds);

            var results = itemIds
                .Where(id => _prices.ContainsKey(id))
                .Select(id => _prices[id])
                .ToList();

            return Task.FromResult<IReadOnlyList<RawPriceEntry>>(results);
        }
    }
}
