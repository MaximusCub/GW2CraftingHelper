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
        private static readonly TimeSpan DefaultFetchTimeout = TimeSpan.FromSeconds(5);

        private readonly HttpClient _http;
        private readonly TimeSpan _fetchTimeout;
        private readonly object _cacheLock = new object();
        private readonly Dictionary<int, CurrencyMetadata> _cache = new Dictionary<int, CurrencyMetadata>();
        private bool _fetched;

        /// <summary>
        /// fetchTimeout bounds the internal HTTP call so a hung /v2/currencies
        /// request can never sit on the plan-generation critical path
        /// indefinitely (default 5s). Injectable so tests can exercise the
        /// timeout path without waiting on the real default.
        /// </summary>
        public CurrencyMetadataService(HttpClient http, TimeSpan? fetchTimeout = null)
        {
            _http = http;
            _fetchTimeout = fetchTimeout ?? DefaultFetchTimeout;
        }

        /// <summary>
        /// Returns cached currency metadata, fetching from the API on the
        /// first call of the module session. Graceful on failure: a
        /// non-success response, network/parse error, or internal timeout
        /// leaves the cache untouched (empty, unless a previous call
        /// already succeeded) and is retried on the next call rather than
        /// being permanently negative-cached - the currency list is a
        /// single small request, so a transient outage should not blank
        /// every currency icon for the rest of the session. Genuine caller
        /// cancellation (ct itself canceled) propagates instead of being
        /// swallowed.
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
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timeoutCts.CancelAfter(_fetchTimeout);

                    using (var request = new HttpRequestMessage(HttpMethod.Get, Url))
                    using (var response = await _http.SendAsync(request, timeoutCts.Token))
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
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                {
                    // Real caller cancellation - must propagate, not be
                    // treated as an ordinary fetch failure.
                    throw;
                }

                // The internal timeout fired, not the caller's token: an
                // ordinary fetch failure, same as a non-success response.
                return SnapshotCache();
            }
            catch (Exception)
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
