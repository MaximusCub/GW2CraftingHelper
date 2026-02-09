using System.Collections.Generic;
using System.Linq;
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
            var json = await _http.GetStringAsync(url);
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
