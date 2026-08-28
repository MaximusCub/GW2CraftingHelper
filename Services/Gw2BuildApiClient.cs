using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The outcome of a bounded attempt to learn the live game build id.
    /// </summary>
    internal class Gw2BuildIdResult
    {
        public Gw2BuildIdResult(int? buildId, int attempts, Exception lastError)
        {
            BuildId = buildId;
            Attempts = attempts;
            LastError = lastError;
        }

        public int? BuildId { get; }

        public int Attempts { get; }

        /// <summary>
        /// The failure from the final attempt, or null on success.
        /// </summary>
        public Exception LastError { get; }
    }

    /// <summary>
    /// Reads /v2/build - which game build the API is currently serving.
    /// </summary>
    /// <remarks>
    /// Retried rather than attempted once: without this id the session's
    /// learned recipes carry no provenance stamp and the recipe corpus
    /// cannot be verified against the live build. It is a single tiny
    /// unauthenticated GET, so a second and third attempt cost little and
    /// close almost all of that window.
    /// </remarks>
    internal class Gw2BuildApiClient
    {
        private const string BuildUrl = "https://api.guildwars2.com/v2/build";
        private const int MaxAttempts = 3;

        private static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

        private readonly HttpClient _http;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly TimeSpan _attemptTimeout;

        public Gw2BuildApiClient(
            HttpClient http,
            Func<TimeSpan, CancellationToken, Task> delay = null,
            TimeSpan? attemptTimeout = null)
        {
            _http = http;
            _delay = delay ?? ((d, ct) => Task.Delay(d, ct));
            _attemptTimeout = attemptTimeout ?? DefaultAttemptTimeout;
        }

        /// <summary>
        /// One attempt, with its own timeout. Throws on any failure.
        /// </summary>
        public async Task<int> GetBuildIdAsync(CancellationToken ct)
        {
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeoutCts.CancelAfter(_attemptTimeout);

                using (var response = await _http.GetAsync(BuildUrl, timeoutCts.Token))
                {
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(json))
                    {
                        return doc.RootElement.GetProperty("id").GetInt32();
                    }
                }
            }
        }

        /// <summary>
        /// Up to <see cref="MaxAttempts"/> attempts. Never throws except for
        /// genuine cancellation of <paramref name="ct"/> (module unload) - a
        /// failed lookup is reported as a null
        /// <see cref="Gw2BuildIdResult.BuildId"/> so the caller can log the
        /// degradation it causes rather than guess at it.
        /// </summary>
        public async Task<Gw2BuildIdResult> TryGetBuildIdAsync(CancellationToken ct)
        {
            Exception lastError = null;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    int buildId = await GetBuildIdAsync(ct);
                    return new Gw2BuildIdResult(buildId, attempt, null);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Includes the per-attempt timeout, which surfaces as an
                    // OperationCanceledException on a token that is NOT the
                    // caller's - exactly the case worth retrying.
                    lastError = ex;
                }

                if (attempt < MaxAttempts)
                {
                    await _delay(RetryDelay, ct);
                }
            }

            return new Gw2BuildIdResult(null, MaxAttempts, lastError);
        }
    }
}
