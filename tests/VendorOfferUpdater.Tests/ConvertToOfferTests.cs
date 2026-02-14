using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using VendorOfferUpdater.Tests.Helpers;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    public class ConvertToOfferTests
    {
        private static async Task<(Gw2ApiHelper helper, HttpClient httpClient)> CreateLoadedHelper()
        {
            var handler = new FakeHttpHandler();
            handler.MapUrl(
                url => url.Contains("/v2/currencies") && !url.Contains("ids="),
                "[2,23]");
            handler.MapUrl(
                url => url.Contains("/v2/currencies?ids="),
                "[{\"id\":2,\"name\":\"Karma\"},{\"id\":23,\"name\":\"Spirit Shard\"}]");

            var httpClient = new HttpClient(handler);
            var helper = new Gw2ApiHelper(httpClient);
            await helper.LoadCurrenciesAsync();
            return (helper, httpClient);
        }

        private static WikiVendorResult MakeResult(
            int gameId = 19685,
            string merchantName = "Merchant",
            int? outputQuantity = 1,
            List<WikiCostEntry> costEntries = null,
            List<string> locations = null)
        {
            return new WikiVendorResult
            {
                GameId = gameId,
                MerchantName = merchantName,
                OutputQuantity = outputQuantity,
                CostEntries = costEntries ?? new List<WikiCostEntry>(),
                Locations = locations ?? new List<string>()
            };
        }

        [Fact]
        public async Task CurrencyCost_ResolvedToCurrencyLine()
        {
            var (helper, httpClient) = await CreateLoadedHelper();
            using var _ = httpClient;
            var result = MakeResult(costEntries: new List<WikiCostEntry>
            {
                new WikiCostEntry { Value = 500, Currency = "Karma" }
            });

            var offer = Program.ConvertToOffer(result, helper, new Dictionary<string, int>());

            Assert.NotNull(offer);
            Assert.Single(offer.CostLines);
            Assert.Equal("Currency", offer.CostLines[0].Type);
            Assert.Equal(2, offer.CostLines[0].Id);
            Assert.Equal(500, offer.CostLines[0].Count);
        }

        [Fact]
        public async Task CoinAlias_ResolvedToCurrencyId1()
        {
            var (helper, httpClient) = await CreateLoadedHelper();
            using var _ = httpClient;
            var result = MakeResult(costEntries: new List<WikiCostEntry>
            {
                new WikiCostEntry { Value = 10000, Currency = "Coin" }
            });

            var offer = Program.ConvertToOffer(result, helper, new Dictionary<string, int>());

            Assert.NotNull(offer);
            Assert.Equal("Currency", offer.CostLines[0].Type);
            Assert.Equal(Gw2Constants.CoinCurrencyId, offer.CostLines[0].Id);
        }

        [Fact]
        public async Task ItemCost_ResolvedToItemLine()
        {
            var (helper, httpClient) = await CreateLoadedHelper();
            using var _ = httpClient;
            var itemIdMap = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["Glob of Ectoplasm"] = 19721
            };
            var result = MakeResult(costEntries: new List<WikiCostEntry>
            {
                new WikiCostEntry { Value = 3, Currency = "Glob of Ectoplasm" }
            });

            var offer = Program.ConvertToOffer(result, helper, itemIdMap);

            Assert.NotNull(offer);
            Assert.Single(offer.CostLines);
            Assert.Equal("Item", offer.CostLines[0].Type);
            Assert.Equal(19721, offer.CostLines[0].Id);
            Assert.Equal(3, offer.CostLines[0].Count);
        }

        [Fact]
        public async Task EmptyCurrency_DefaultsToCoins()
        {
            var (helper, httpClient) = await CreateLoadedHelper();
            using var _ = httpClient;
            var result = MakeResult(costEntries: new List<WikiCostEntry>
            {
                new WikiCostEntry { Value = 256, Currency = "" }
            });

            var offer = Program.ConvertToOffer(result, helper, new Dictionary<string, int>());

            Assert.NotNull(offer);
            Assert.Equal("Currency", offer.CostLines[0].Type);
            Assert.Equal(Gw2Constants.CoinCurrencyId, offer.CostLines[0].Id);
        }

        [Fact]
        public async Task UnresolvedCurrency_ReturnsNull()
        {
            var (helper, httpClient) = await CreateLoadedHelper();
            using var _ = httpClient;
            var result = MakeResult(costEntries: new List<WikiCostEntry>
            {
                new WikiCostEntry { Value = 10, Currency = "Unknown Token" }
            });

            var offer = Program.ConvertToOffer(result, helper, new Dictionary<string, int>());

            Assert.Null(offer);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task NullOrEmptyMerchant_ReturnsNull(string merchantName)
        {
            var (helper, httpClient) = await CreateLoadedHelper();
            using var _ = httpClient;
            var result = MakeResult(merchantName: merchantName);

            var offer = Program.ConvertToOffer(result, helper, new Dictionary<string, int>());

            Assert.Null(offer);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task InvalidOutputQuantity_DefaultsTo1(int? qty)
        {
            var (helper, httpClient) = await CreateLoadedHelper();
            using var _ = httpClient;
            var result = MakeResult(outputQuantity: qty);

            var offer = Program.ConvertToOffer(result, helper, new Dictionary<string, int>());

            Assert.NotNull(offer);
            Assert.Equal(1, offer.OutputCount);
        }

        [Fact]
        public async Task OfferIdIsPopulated()
        {
            var (helper, httpClient) = await CreateLoadedHelper();
            using var _ = httpClient;
            var result = MakeResult();

            var offer = Program.ConvertToOffer(result, helper, new Dictionary<string, int>());

            Assert.NotNull(offer);
            Assert.Matches("^[0-9a-f]{64}$", offer.OfferId);
        }

        [Fact]
        public async Task EmptyLocations_BecomesNull()
        {
            var (helper, httpClient) = await CreateLoadedHelper();
            using var _ = httpClient;
            var result = MakeResult(locations: new List<string>());

            var offer = Program.ConvertToOffer(result, helper, new Dictionary<string, int>());

            Assert.NotNull(offer);
            Assert.Null(offer.Locations);
        }

        [Fact]
        public async Task NonEmptyLocations_Preserved()
        {
            var (helper, httpClient) = await CreateLoadedHelper();
            using var _ = httpClient;
            var result = MakeResult(locations: new List<string> { "Lion's Arch", "Divinity's Reach" });

            var offer = Program.ConvertToOffer(result, helper, new Dictionary<string, int>());

            Assert.NotNull(offer);
            Assert.Equal(2, offer.Locations.Count);
            Assert.Contains("Lion's Arch", offer.Locations);
            Assert.Contains("Divinity's Reach", offer.Locations);
        }
    }
}
