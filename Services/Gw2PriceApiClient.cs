using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GW2CraftingHelper.Services
{
    public class Gw2PriceApiClient : IPriceApiClient
    {
        private const string BaseUrl = "https://api.guildwars2.com/v2";

        private readonly HttpClient _http;

        public Gw2PriceApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<IReadOnlyList<RawPriceEntry>> GetPricesAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct)
        {
            if (itemIds == null || itemIds.Count == 0)
            {
                return new List<RawPriceEntry>();
            }

            var ids = string.Join(",", itemIds);
            var url = $"{BaseUrl}/commerce/prices?ids={ids}";

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            using (var response = await _http.SendAsync(request, ct))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new List<RawPriceEntry>();
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"GW2 API error {(int)response.StatusCode} from {url}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var array = JArray.Parse(json);

                var results = new List<RawPriceEntry>();
                foreach (var item in array)
                {
                    results.Add(new RawPriceEntry
                    {
                        Id = item.Value<int>("id"),
                        BuyUnitPrice = item["buys"]?.Value<int>("unit_price") ?? 0,
                        SellUnitPrice = item["sells"]?.Value<int>("unit_price") ?? 0
                    });
                }

                return results;
            }
        }
    }
}
