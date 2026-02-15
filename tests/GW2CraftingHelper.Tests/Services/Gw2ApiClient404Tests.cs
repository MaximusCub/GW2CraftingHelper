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
        public async Task PriceClient_404_ReturnsEmptyList()
        {
            using (var handler = new StubHandler(HttpStatusCode.NotFound,
                @"{""text"":""all ids provided are invalid""}"))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2PriceApiClient(http);
                var result = await client.GetPricesAsync(
                    new[] { 99999 }, CancellationToken.None);

                Assert.Empty(result);
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

                Assert.Single(result);
                Assert.Equal(19684, result[0].Id);
                Assert.Equal(100, result[0].BuyUnitPrice);
                Assert.Equal(200, result[0].SellUnitPrice);
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
