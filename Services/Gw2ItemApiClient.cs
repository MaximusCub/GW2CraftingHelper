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
                    // design-plan-notes.md (Notes section, excess/reclaim
                    // account-bound exclusion): the "flags" array (e.g.
                    // "AccountBound") was previously parsed nowhere - see
                    // RawItem.Flags' own doc comment. Missing/non-array
                    // "flags" yields an empty list, never null, mirroring
                    // the Name/Icon/Rarity "" fallback convention above.
                    var flags = new List<string>();
                    var flagsToken = item["flags"] as JArray;
                    if (flagsToken != null)
                    {
                        foreach (var flag in flagsToken)
                        {
                            // a malformed array
                            // element (null, or a non-string token) must
                            // not inject a null into Flags - RawItem.Flags'
                            // own doc comment promises a never-null LIST,
                            // but a null ENTRY inside it would still be a
                            // silent contract violation for any future
                            // consumer beyond the current Contains(...) check.
                            var flagValue = flag.Value<string>();
                            if (flagValue != null)
                            {
                                flags.Add(flagValue);
                            }
                        }
                    }

                    results.Add(new RawItem
                    {
                        Id = item.Value<int>("id"),
                        Name = item.Value<string>("name") ?? "",
                        Icon = item.Value<string>("icon") ?? "",
                        Rarity = item.Value<string>("rarity") ?? "",
                        Flags = flags
                    });
                }

                return results;
            }
        }
    }
}
