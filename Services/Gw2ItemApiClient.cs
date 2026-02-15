using System.Collections.Generic;
using System.Net;
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

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            using (var response = await _http.SendAsync(request, ct))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new List<RawItem>();
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"GW2 API error {(int)response.StatusCode} from {url}");
                }

                var json = await response.Content.ReadAsStringAsync();
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
}
