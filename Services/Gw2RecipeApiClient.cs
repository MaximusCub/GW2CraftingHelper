using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GW2CraftingHelper.Services
{
    public class Gw2RecipeApiClient : IRecipeApiClient
    {
        private const string BaseUrl = "https://api.guildwars2.com/v2";

        private readonly HttpClient _http;

        public Gw2RecipeApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<IReadOnlyList<int>> SearchByOutputAsync(int itemId, CancellationToken ct)
        {
            var url = $"{BaseUrl}/recipes/search?output={itemId}";
            string json = await GetJsonAsync(url, ct);
            if (json == null)
            {
                return new List<int>();
            }
            return JsonConvert.DeserializeObject<List<int>>(json);
        }

        public async Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
        {
            var url = $"{BaseUrl}/recipes/{recipeId}";
            string json = await GetJsonAsync(url, ct);
            if (json == null)
            {
                return null;
            }
            return ParseRecipe(json);
        }

        // KNOWN-ISSUES api-degradation F5: the previous implementation used
        // HttpClient.GetStringAsync(url) - the classic overload with no
        // CancellationToken parameter (net472 has no ct-accepting
        // GetStringAsync overload) - so `ct` was silently a no-op for
        // every recipe search/detail call. GetAsync(url, ct) threads
        // cancellation through properly and matches
        // Gw2PriceApiClient/Gw2ItemApiClient's own SendAsync(request, ct)
        // pattern, including their 404-handling, which this class
        // previously lacked entirely.
        private async Task<string> GetJsonAsync(string url, CancellationToken ct)
        {
            using (var response = await _http.GetAsync(url, ct))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"GW2 API error {(int)response.StatusCode} from {url}");
                }

                return await response.Content.ReadAsStringAsync();
            }
        }

        internal static RawRecipe ParseRecipe(string json)
        {
            var obj = JObject.Parse(json);

            var recipe = new RawRecipe
            {
                Id = obj.Value<int>("id"),
                OutputItemId = obj.Value<int>("output_item_id"),
                OutputItemCount = obj.Value<int>("output_item_count"),
                MinRating = obj.Value<int?>("min_rating") ?? 0
            };

            var disciplines = obj["disciplines"];
            if (disciplines != null)
            {
                foreach (var d in disciplines)
                {
                    recipe.Disciplines.Add(d.Value<string>());
                }
            }

            var flags = obj["flags"];
            if (flags != null)
            {
                foreach (var f in flags)
                {
                    recipe.Flags.Add(f.Value<string>());
                }
            }

            var ingredients = obj["ingredients"];
            if (ingredients != null)
            {
                foreach (var ing in ingredients)
                {
                    recipe.Ingredients.Add(new RawIngredient
                    {
                        Type = ing.Value<string>("type") ?? "Item",
                        Id = ing.Value<int>("item_id"),
                        Count = ing.Value<int>("count")
                    });
                }
            }

            return recipe;
        }
    }
}
