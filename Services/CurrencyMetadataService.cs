using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using Newtonsoft.Json.Linq;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Fetches name/icon metadata for GW2 wallet currencies from
    /// api.guildwars2.com/v2/currencies. Blish-free (plain HttpClient +
    /// JSON), matching Gw2PriceApiClient/Gw2ItemApiClient so it can be
    /// exercised in tests without any Blish HUD dependency.
    ///
    /// The full currency list is small and effectively static for a
    /// module session, so - unlike ItemMetadataService's per-id batched
    /// lookups - it is fetched once with a single "ids=all" request and
    /// cached in memory for every later call.
    /// </summary>
    public class CurrencyMetadataService
    {
        private const string Url = "https://api.guildwars2.com/v2/currencies?ids=all";

        private readonly HttpClient _http;
        private readonly object _cacheLock = new object();
        private readonly Dictionary<int, CurrencyMetadata> _cache = new Dictionary<int, CurrencyMetadata>();
        private bool _fetched;

        public CurrencyMetadataService(HttpClient http)
        {
            _http = http;
        }

        /// <summary>
        /// Returns cached currency metadata, fetching from the API on the
        /// first call of the module session. Graceful on failure: a
        /// non-success response or network/parse error leaves the cache
        /// untouched (empty, unless a previous call already succeeded) and
        /// is retried on the next call rather than being permanently
        /// negative-cached - the currency list is a single small request,
        /// so a transient outage should not blank every currency icon for
        /// the rest of the session.
        /// </summary>
        public async Task<IReadOnlyDictionary<int, CurrencyMetadata>> GetAllAsync(CancellationToken ct)
        {
            lock (_cacheLock)
            {
                if (_fetched)
                {
                    return new Dictionary<int, CurrencyMetadata>(_cache);
                }
            }

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, Url))
                using (var response = await _http.SendAsync(request, ct))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return SnapshotCache();
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var array = JArray.Parse(json);

                    var parsed = new Dictionary<int, CurrencyMetadata>();
                    foreach (var entry in array)
                    {
                        int id = entry.Value<int>("id");
                        parsed[id] = new CurrencyMetadata
                        {
                            CurrencyId = id,
                            Name = entry.Value<string>("name") ?? "",
                            IconUrl = entry.Value<string>("icon") ?? ""
                        };
                    }

                    lock (_cacheLock)
                    {
                        foreach (var kvp in parsed)
                        {
                            _cache[kvp.Key] = kvp.Value;
                        }
                        _fetched = true;
                        return new Dictionary<int, CurrencyMetadata>(_cache);
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // Currency icons are a decorative addition on top of the
                // text-only row that already renders correctly without
                // them - a network or parse failure here must never abort
                // plan generation. Nothing is cached, so the next call
                // (next plan generation) retries automatically.
                return SnapshotCache();
            }
        }

        private Dictionary<int, CurrencyMetadata> SnapshotCache()
        {
            lock (_cacheLock)
            {
                return new Dictionary<int, CurrencyMetadata>(_cache);
            }
        }
    }
}
