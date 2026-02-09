using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class TradingPostServiceTests
    {
        [Fact]
        public async Task SingleItem_ReturnsBuyInstantAndSellInstant()
        {
            var api = new InMemoryPriceApiClient();
            // buys.unit_price=350 (sell-instant), sells.unit_price=400 (buy-instant)
            api.AddPrice(19684, buyUnitPrice: 350, sellUnitPrice: 400);
            var svc = new TradingPostService(api);

            var result = await svc.GetPricesAsync(new[] { 19684 }, CancellationToken.None);

            Assert.True(result.ContainsKey(19684));
            var price = result[19684];
            Assert.Equal(19684, price.ItemId);
            Assert.Equal(400, price.BuyInstant);   // sells.unit_price
            Assert.Equal(350, price.SellInstant);   // buys.unit_price
        }

        [Fact]
        public async Task ItemAbsentFromApi_NotInDictionary()
        {
            var api = new InMemoryPriceApiClient();
            // Item 99999 not added — simulates account-bound / non-tradeable item
            var svc = new TradingPostService(api);

            var result = await svc.GetPricesAsync(new[] { 99999 }, CancellationToken.None);

            Assert.False(result.ContainsKey(99999));
        }

        [Fact]
        public async Task ItemPresentWithZeroPrices_IncludedInDictionary()
        {
            var api = new InMemoryPriceApiClient();
            // Tradeable item but no current orders
            api.AddPrice(50000, buyUnitPrice: 0, sellUnitPrice: 0);
            var svc = new TradingPostService(api);

            var result = await svc.GetPricesAsync(new[] { 50000 }, CancellationToken.None);

            Assert.True(result.ContainsKey(50000));
            Assert.Equal(0, result[50000].BuyInstant);
            Assert.Equal(0, result[50000].SellInstant);
        }

        [Fact]
        public async Task MultipleItems_AllReturnedCorrectly()
        {
            var api = new InMemoryPriceApiClient();
            api.AddPrice(1, buyUnitPrice: 100, sellUnitPrice: 200);
            api.AddPrice(2, buyUnitPrice: 300, sellUnitPrice: 400);
            api.AddPrice(3, buyUnitPrice: 500, sellUnitPrice: 600);
            var svc = new TradingPostService(api);

            var result = await svc.GetPricesAsync(new[] { 1, 2, 3 }, CancellationToken.None);

            Assert.Equal(3, result.Count);
            Assert.Equal(200, result[1].BuyInstant);
            Assert.Equal(400, result[2].BuyInstant);
            Assert.Equal(600, result[3].BuyInstant);
        }

        [Fact]
        public async Task Deduplication_DuplicateIdsDoNotCauseDuplicateApiCalls()
        {
            var api = new InMemoryPriceApiClient();
            api.AddPrice(1, buyUnitPrice: 100, sellUnitPrice: 200);
            var svc = new TradingPostService(api);

            var result = await svc.GetPricesAsync(new[] { 1, 1, 1 }, CancellationToken.None);

            Assert.Single(result);
            Assert.Single(api.Calls);
            Assert.Single(api.Calls[0]); // only 1 unique ID sent
        }

        [Fact]
        public async Task Batching_LargeSetSplitIntoChunks()
        {
            var api = new InMemoryPriceApiClient();
            var ids = new List<int>();
            for (int i = 1; i <= 250; i++)
            {
                api.AddPrice(i, buyUnitPrice: i, sellUnitPrice: i * 2);
                ids.Add(i);
            }
            var svc = new TradingPostService(api);

            var result = await svc.GetPricesAsync(ids, CancellationToken.None);

            Assert.Equal(250, result.Count);
            Assert.Equal(2, api.Calls.Count);
            Assert.Equal(200, api.Calls[0].Count);
            Assert.Equal(50, api.Calls[1].Count);
        }

        [Fact]
        public async Task Caching_SecondCallOnlyFetchesNewIds()
        {
            var api = new InMemoryPriceApiClient();
            api.AddPrice(1, buyUnitPrice: 100, sellUnitPrice: 200);
            api.AddPrice(2, buyUnitPrice: 300, sellUnitPrice: 400);
            api.AddPrice(3, buyUnitPrice: 500, sellUnitPrice: 600);
            var svc = new TradingPostService(api);

            // First call fetches 1 and 2
            await svc.GetPricesAsync(new[] { 1, 2 }, CancellationToken.None);

            // Second call with 2 (cached) and 3 (new)
            var result = await svc.GetPricesAsync(new[] { 2, 3 }, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, api.Calls.Count);
            // Second call should only contain item 3
            Assert.Single(api.Calls[1]);
            Assert.Equal(3, api.Calls[1][0]);
        }
    }
}
