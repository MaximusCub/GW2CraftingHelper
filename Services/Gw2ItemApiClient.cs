using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GW2CraftingHelper.Services
{
    public class Gw2ItemApiClient : IItemApiClient
    {
        private const string BaseUrl = "https://api.guildwars2.com/v2";

        private readonly HttpClient _http;

        public Gw2ItemApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<IReadOnlyList<RawItem>> GetItemsAsync(
            IReadOnlyList<int> itemIds, CancellationToken ct)
        {
            if (itemIds == null || itemIds.Count == 0)
            {
                return new List<RawItem>();
            }

            var ids = string.Join(",", itemIds);
            var url = $"{BaseUrl}/items?ids={ids}";
            var json = await _http.GetStringAsync(url);
            var array = JArray.Parse(json);

            var results = new List<RawItem>();
            foreach (var item in array)
            {
                results.Add(new RawItem
                {
                    Id = item.Value<int>("id"),
                    Name = item.Value<string>("name") ?? "",
                    Icon = item.Value<string>("icon") ?? ""
                });
            }

            return results;
        }
    }
}
