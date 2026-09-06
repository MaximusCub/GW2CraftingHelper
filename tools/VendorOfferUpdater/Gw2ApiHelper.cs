using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Resolves currency names (from wiki) to GW2 API currency IDs, and
    /// recipe sheet item IDs to the recipe they unlock.
    /// </summary>
    public class Gw2ApiHelper
    {
        private const string CurrenciesUrl = "https://api.guildwars2.com/v2/currencies";
        private const string ItemsUrl = "https://api.guildwars2.com/v2/items";
        private readonly HttpClient _httpClient;

        // Null until LoadCurrenciesAsync completes; ResolveCurrencyId
        // already null-guards every access below.
        private Dictionary<string, int>? _currencyNameToId;

        public Gw2ApiHelper(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Loads all currency IDs and names from the GW2 API.
        /// </summary>
        public async Task LoadCurrenciesAsync()
        {
            Console.WriteLine("Loading GW2 API currencies...");

            // First get all IDs
            var idsResponse = await _httpClient.GetStringAsync(CurrenciesUrl);

            // Deserialize<List<int>> can only return null if the response
            // body is the literal JSON token "null" - a malformed/empty
            // response the GW2 API never legitimately sends for this
            // endpoint. Fail loudly rather than let ids.Count NRE below.
            var ids = JsonSerializer.Deserialize<List<int>>(idsResponse)
                ?? throw new InvalidOperationException(
                    "GW2 API currencies response deserialized to null.");

            // Fetch in batches of 200
            _currencyNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < ids.Count; i += 200)
            {
                var batch = ids.Skip(i).Take(200);
                var batchIds = string.Join(",", batch);
                var url = $"{CurrenciesUrl}?ids={batchIds}";
                var response = await _httpClient.GetStringAsync(url);
                using var currencies = JsonDocument.Parse(response);

                foreach (var currency in currencies.RootElement.EnumerateArray())
                {
                    // The GW2 API's currencies endpoint always returns
                    // "name" as a JSON string, never JSON null.
                    var name = currency.GetProperty("name").GetString()!;
                    var id = currency.GetProperty("id").GetInt32();
                    _currencyNameToId[name] = id;
                }
            }

            Console.WriteLine($"  Loaded {_currencyNameToId.Count} currencies.");
        }

        /// <summary>
        /// Resolves a wiki currency name to a GW2 API currency ID.
        /// Returns null if the currency name is not recognized.
        /// </summary>
        public int? ResolveCurrencyId(string? currencyName)
        {
            if (string.IsNullOrEmpty(currencyName))
            {
                return null;
            }

            // Common wiki name mappings
            if (string.Equals(currencyName, "Coin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currencyName, "Coins", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currencyName, "Gold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currencyName, "Copper", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currencyName, "Silver", StringComparison.OrdinalIgnoreCase))
            {
                return Models.Gw2Constants.CoinCurrencyId;
            }

            if (_currencyNameToId != null &&
                _currencyNameToId.TryGetValue(currencyName, out int id))
            {
                return id;
            }

            return null;
        }

        /// <summary>
        /// Returns the recipe a consumable recipe sheet unlocks, or null
        /// when <paramref name="itemId"/> is not one.
        /// <para>
        /// A sheet is an item whose <c>details.type</c> is "Unlock" and
        /// whose <c>details.unlock_type</c> is "CraftingRecipe"; its
        /// <c>details.recipe_id</c> is the recipe the account learns.
        /// A sheet covering several recipes also carries
        /// <c>details.extra_recipe_ids</c> - item 101483 "Recipe: Legendary
        /// Obsidian Armor" names recipe 14083 plus 17 more, one per armour
        /// piece. Only <c>recipe_id</c> is returned: the account learns all
        /// of them together, so any single one answers "is this unlocked".
        /// </para>
        /// </summary>
        public async Task<int?> ResolveRecipeSheetRecipeIdAsync(int itemId)
        {
            string response = await _httpClient.GetStringAsync($"{ItemsUrl}/{itemId}");
            using var doc = JsonDocument.Parse(response);

            if (!doc.RootElement.TryGetProperty("details", out var details) ||
                details.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!IsStringProperty(details, "type", "Unlock") ||
                !IsStringProperty(details, "unlock_type", "CraftingRecipe"))
            {
                return null;
            }

            return details.TryGetProperty("recipe_id", out var recipeId) &&
                   recipeId.ValueKind == JsonValueKind.Number
                ? recipeId.GetInt32()
                : (int?)null;
        }

        private static bool IsStringProperty(JsonElement element, string name, string expected)
        {
            return element.TryGetProperty(name, out var value) &&
                   value.ValueKind == JsonValueKind.String &&
                   string.Equals(value.GetString(), expected, StringComparison.Ordinal);
        }
    }
}
