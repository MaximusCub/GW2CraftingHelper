using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Tests.Helpers
{
    internal class InMemoryPriceApiClient : IPriceApiClient
    {
        private readonly Dictionary<int, RawPriceEntry> _prices = new Dictionary<int, RawPriceEntry>();
        private readonly List<IReadOnlyList<int>> _calls = new List<IReadOnlyList<int>>();

        public IReadOnlyList<IReadOnlyList<int>> Calls => _calls;

        /// <summary>
        /// 1-based call number (counted across this client's lifetime) on
        /// which GetPricesAsync should throw instead of returning,
        /// simulating one bad batch amid otherwise-healthy ones. 0
        /// (default) disables this. Mirrors InMemoryItemApiClient's
        /// ThrowOnCallNumber.
        /// </summary>
        public int ThrowOnCallNumber { get; set; }

        /// <summary>
        /// When true, every call throws - simulating a total price-API
        /// outage across every batch, not just one.
        /// </summary>
        public bool ThrowAlways { get; set; }

        /// <summary>
        /// 1-based call number on which GetPricesAsync returns an empty
        /// batch that does NOT prove absence - the real client's 404
        /// branch, which an endpoint-level outage produces just as readily
        /// as "every id in this batch is untradeable". 0 (default) disables
        /// this.
        /// </summary>
        public int UnprovenEmptyOnCallNumber { get; set; }

        /// <summary>
        /// When set, every call awaits this task before deciding whether to
        /// throw or return - lets a test deterministically hold a fetch
        /// "in flight" while it starts a second overlapping call, then
        /// release both together. _calls is recorded before the gate is
        /// awaited, so callers can already observe an in-flight call count
        /// while the gate is held.
        /// </summary>
        public Task Gate { get; set; }

        public void AddPrice(int itemId, int buyUnitPrice, int sellUnitPrice)
        {
            _prices[itemId] = new RawPriceEntry
            {
                Id = itemId,
                BuyUnitPrice = buyUnitPrice,
                SellUnitPrice = sellUnitPrice,
            };
        }

        public async Task<PriceBatchResult> GetPricesAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct)
        {
            _calls.Add(itemIds);

            if (Gate != null)
            {
                await Gate;
            }

            if (ThrowAlways || (ThrowOnCallNumber > 0 && _calls.Count == ThrowOnCallNumber))
            {
                throw new HttpRequestException("Simulated transient API failure.");
            }

            if (UnprovenEmptyOnCallNumber > 0 && _calls.Count == UnprovenEmptyOnCallNumber)
            {
                return new PriceBatchResult(new List<RawPriceEntry>(), absenceProven: false);
            }

            var results = itemIds
                .Where(id => _prices.ContainsKey(id))
                .Select(id => _prices[id])
                .ToList();

            return new PriceBatchResult(results, absenceProven: true);
        }
    }
}
