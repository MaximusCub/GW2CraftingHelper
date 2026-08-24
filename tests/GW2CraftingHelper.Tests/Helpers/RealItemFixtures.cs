using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Tests.Helpers
{
    /// <summary>
    /// Runs verbatim live /v2/items JSON (see <see cref="RealItemJson"/>)
    /// through the REAL <see cref="Gw2ItemApiClient"/> parser. Every stat
    /// test downstream therefore starts from production-parsed data rather
    /// than from a hand-built RawItem that could quietly disagree with what
    /// the API actually sends.
    /// </summary>
    public static class RealItemFixtures
    {
        private class StubHandler : HttpMessageHandler
        {
            private readonly string _body;

            public StubHandler(string body)
            {
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_body)
                });
            }
        }

        public static async Task<Dictionary<int, RawItem>> ParseAsync(params string[] itemJson)
        {
            using (var handler = new StubHandler(RealItemJson.Array(itemJson)))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2ItemApiClient(http);
                var items = await client.GetItemsAsync(new[] { 1 }, CancellationToken.None);
                return items.ToDictionary(i => i.Id);
            }
        }

        public static async Task<RawItem> ParseOneAsync(string itemJson)
        {
            var items = await ParseAsync(itemJson);
            return items.Values.Single();
        }
    }
}
