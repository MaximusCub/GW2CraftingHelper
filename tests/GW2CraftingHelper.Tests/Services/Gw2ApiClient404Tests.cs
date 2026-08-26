using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class Gw2ApiClient404Tests
    {
        private class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _body;

            public StubHandler(HttpStatusCode statusCode, string body = "")
            {
                _statusCode = statusCode;
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_body)
                };
                return Task.FromResult(response);
            }
        }

        // --- Gw2PriceApiClient ---

        [Fact]
        public async Task PriceClient_404_ReturnsEmptyBatch_WithoutProvingAbsence()
        {
            using (var handler = new StubHandler(HttpStatusCode.NotFound,
                @"{""text"":""all ids provided are invalid""}"))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2PriceApiClient(http);
                var result = await client.GetPricesAsync(
                    new[] { 99999 }, CancellationToken.None);

                Assert.Empty(result.Entries);

                // The same 404 the endpoint returns when it is simply down,
                // so it is no evidence that 99999 is untradeable.
                Assert.False(result.AbsenceProven);
            }
        }

        [Fact]
        public async Task PriceClient_200_ReturnsParsedPrices()
        {
            var json = @"[{""id"":19684,""buys"":{""unit_price"":100},""sells"":{""unit_price"":200}}]";
            using (var handler = new StubHandler(HttpStatusCode.OK, json))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2PriceApiClient(http);
                var result = await client.GetPricesAsync(
                    new[] { 19684 }, CancellationToken.None);

                Assert.Single(result.Entries);
                Assert.Equal(19684, result.Entries[0].Id);
                Assert.Equal(100, result.Entries[0].BuyUnitPrice);
                Assert.Equal(200, result.Entries[0].SellUnitPrice);

                // A parsed 2xx body IS the trading post's full answer for
                // the batch, so ids it omits are genuinely untradeable.
                Assert.True(result.AbsenceProven);
            }
        }

        [Fact]
        public async Task PriceClient_500_ThrowsWithStatusCode()
        {
            using (var handler = new StubHandler(HttpStatusCode.InternalServerError))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2PriceApiClient(http);

                var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                    client.GetPricesAsync(new[] { 1 }, CancellationToken.None));

                Assert.Contains("500", ex.Message);
            }
        }

        [Fact]
        public async Task PriceClient_429_ThrowsWithStatusCode()
        {
            using (var handler = new StubHandler((HttpStatusCode)429))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2PriceApiClient(http);

                var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                    client.GetPricesAsync(new[] { 1 }, CancellationToken.None));

                Assert.Contains("429", ex.Message);
            }
        }

        // --- Gw2ItemApiClient ---

        [Fact]
        public async Task ItemClient_404_ReturnsEmptyList()
        {
            using (var handler = new StubHandler(HttpStatusCode.NotFound,
                @"{""text"":""all ids provided are invalid""}"))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2ItemApiClient(http);
                var result = await client.GetItemsAsync(
                    new[] { 99999 }, CancellationToken.None);

                Assert.Empty(result);
            }
        }

        [Fact]
        public async Task ItemClient_200_ReturnsParsedItems()
        {
            var json = @"[{""id"":19684,""name"":""Mithril Ingot"",""icon"":""http://icon.png""}]";
            using (var handler = new StubHandler(HttpStatusCode.OK, json))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2ItemApiClient(http);
                var result = await client.GetItemsAsync(
                    new[] { 19684 }, CancellationToken.None);

                Assert.Single(result);
                Assert.Equal(19684, result[0].Id);
                Assert.Equal("Mithril Ingot", result[0].Name);
                Assert.Equal("http://icon.png", result[0].Icon);
            }
        }

        [Fact]
        public async Task ItemClient_ParsesRarity_MissingFieldYieldsEmpty()
        {
            var json = @"[
                {""id"":1,""name"":""A"",""icon"":""http://a.png"",""rarity"":""Exotic""},
                {""id"":2,""name"":""B"",""icon"":""http://b.png""}]";
            using (var handler = new StubHandler(HttpStatusCode.OK, json))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2ItemApiClient(http);
                var result = await client.GetItemsAsync(
                    new[] { 1, 2 }, CancellationToken.None);

                Assert.Equal("Exotic", result[0].Rarity);
                Assert.Equal("", result[1].Rarity);
            }
        }

        [Fact]
        public async Task ItemClient_ParsesFlags_MissingFieldYieldsEmptyList()
        {
            // Real-path coverage for
            // Gw2ItemApiClient.GetItemsAsync's "flags" array parsing -
            // previously nothing asserted this half of the account-bound
            // plumbing (only ItemMetadataService's derivation from an
            // already-built RawItem was covered).
            var json = @"[
                {""id"":1,""name"":""A"",""icon"":""http://a.png"",""flags"":[""AccountBound"",""NoSell""]},
                {""id"":2,""name"":""B"",""icon"":""http://b.png""}]";
            using (var handler = new StubHandler(HttpStatusCode.OK, json))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2ItemApiClient(http);
                var result = await client.GetItemsAsync(
                    new[] { 1, 2 }, CancellationToken.None);

                Assert.Equal(new[] { "AccountBound", "NoSell" }, result[0].Flags);
                Assert.Empty(result[1].Flags);
            }
        }

        [Fact]
        public async Task ItemClient_500_ThrowsWithStatusCode()
        {
            using (var handler = new StubHandler(HttpStatusCode.InternalServerError))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2ItemApiClient(http);

                var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                    client.GetItemsAsync(new[] { 1 }, CancellationToken.None));

                Assert.Contains("500", ex.Message);
            }
        }

        [Fact]
        public async Task ItemClient_429_ThrowsWithStatusCode()
        {
            using (var handler = new StubHandler((HttpStatusCode)429))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2ItemApiClient(http);

                var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                    client.GetItemsAsync(new[] { 1 }, CancellationToken.None));

                Assert.Contains("429", ex.Message);
            }
        }
    }
}
