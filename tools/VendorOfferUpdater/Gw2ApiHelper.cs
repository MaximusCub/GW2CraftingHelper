using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Resolves currency names (from wiki) to GW2 API currency IDs.
    /// </summary>
    public class Gw2ApiHelper
    {
        private const string CurrenciesUrl = "https://api.guildwars2.com/v2/currencies";
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
    }
}
