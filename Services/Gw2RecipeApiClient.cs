using System.Collections.Generic;
using System.Linq;
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
            var json = await _http.GetStringAsync(url);
            return JsonConvert.DeserializeObject<List<int>>(json);
        }

        public async Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
        {
            var url = $"{BaseUrl}/recipes/{recipeId}";
            var json = await _http.GetStringAsync(url);
            return ParseRecipe(json);
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
