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

        // KNOWN-ISSUES recipe-ingestion bug class (2026-08-15): every
        // /v2/recipes call in this class previously omitted the schema-
        // version query parameter entirely. The GW2 API hides the whole
        // currency-ingredient era of recipes (e.g. 14025, Amalgamated Rift
        // Essence -> item 100930) from UNVERSIONED /v2/recipes,
        // /v2/recipes/search, and /v2/recipes/{id} responses - verified via
        // live probes: unversioned /v2/recipes lists 13,183 ids, versioned
        // ?v=latest lists 13,371 (188 missing), and an unversioned
        // /v2/recipes/14025 404s outright while the versioned request
        // returns the full recipe. Pinned to a literal date rather than
        // "v=latest": the module always wants "the schema version that
        // exists today" so an upstream schema bump can never silently
        // change ingredient JSON shape (Currency vs. Item key names) for
        // this client without a deliberate review of this constant - a
        // literal date keeps returning the same shape it returns today,
        // permanently, exactly like v=latest would UNTIL a future schema
        // revision, at which point v=latest would silently start returning
        // the new shape and this constant would not. Re-pin (bump the
        // date) only alongside a verified review of the new schema's
        // ingredient shape.
        private const string SchemaVersion = "2026-08-15";

        private readonly HttpClient _http;

        public Gw2RecipeApiClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<IReadOnlyList<int>> SearchByOutputAsync(int itemId, CancellationToken ct)
        {
            // KNOWN-ISSUES recipe-ingestion bug class (2026-08-15): even
            // WITH the schema version pinned above, this endpoint's own
            // upstream search INDEX has a gap independent of the missing-
            // recipes bug fixed here - verified live: even
            // /v2/recipes/search?output=100930&v=latest (the exact
            // currency-ingredient recipe this fix restores visibility for)
            // returns an EMPTY array, while the recipe itself fully exists
            // and is fetchable by id. Versioning this URL fixes every
            // recipe this client can otherwise SEE via search; it cannot
            // fix recipes upstream's own search index never indexed in the
            // first place. Those recipes remain discoverable only through
            // the seeded search index (ref/recipe_search_seed.json, built
            // by tools/GW2CraftingHelper.RecipeSeeder by walking the full
            // /v2/recipes id list rather than this search endpoint) - a
            // live cache-miss fallback through THIS method alone cannot
            // discover them.
            var url = $"{BaseUrl}/recipes/search?output={itemId}&v={SchemaVersion}";
            string json = await GetJsonAsync(url, ct);
            if (json == null)
            {
                return new List<int>();
            }
            return JsonConvert.DeserializeObject<List<int>>(json);
        }

        public async Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
        {
            var url = $"{BaseUrl}/recipes/{recipeId}?v={SchemaVersion}";
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
                        // KNOWN-ISSUES recipe-ingestion bug class
                        // (2026-08-15): the versioned schema pinned above
                        // keys EVERY ingredient's item id as "id" - verified
                        // live for both a Currency ingredient (recipe
                        // 14025) and a plain, type-less Item ingredient
                        // (recipe 7785, versioned) - "item_id" is only the
                        // UNVERSIONED shape's key. "item_id" is kept as a
                        // fallback purely for defense (an accidental
                        // unversioned call, or a future regression); it is
                        // not required by any row currently in
                        // ref/recipes_seed.json (checked: every existing
                        // seed row's ingredients already use "id", since
                        // that file stores RawIngredient's own C# property
                        // name, not the raw API key).
                        Id = ing.Value<int?>("id") ?? ing.Value<int>("item_id"),
                        Count = ing.Value<int>("count")
                    });
                }
            }

            return recipe;
        }
    }
}
