using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Short-lived session cache in front of another
    /// <see cref="IAccountRecipeClient"/>. Every plan generation asks for the
    /// learned ids once, and that round trip was measured at 327-4557ms on
    /// the warm path as well as the cold one, so it is worth not repeating
    /// for back-to-back plans. A separate decorator rather than a field
    /// inside <see cref="Gw2AccountRecipeClient"/>, which holds a Blish
    /// Gw2ApiManager no test in this Blish-free repo can construct.
    /// <para>
    /// STALENESS IS ADVISORY, NEVER A SOLVER INPUT. Learned recipe ids never
    /// affect a craft-vs-buy decision, a quantity or a cost - the plan is
    /// solved before they are consulted. But a recipe learned in-game inside
    /// the TTL window still reads as missing, and the plan may keep
    /// recommending the priced recipe sheet that taught it until the window
    /// passes. That is the cost of this cache; raising the TTL raises it.
    /// Derivation: docs/ARCHITECTURE.md section S1.9.
    /// </para>
    /// </summary>
    internal class CachingAccountRecipeClient : IAccountRecipeClient
    {
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

        private readonly IAccountRecipeClient _inner;
        private readonly TimeSpan _ttl;
        private readonly Func<DateTime> _utcNow;
        private readonly object _cacheLock = new object();
        private HashSet<int> _cached;
        private DateTime _fetchedUtc;

        // Bumped by Invalidate. A fetch that was already in flight when the
        // credential changed carries the OLD account's ids, so it must not
        // be allowed to write them back into the cache the invalidation just
        // cleared - it compares the epoch it started under before storing.
        private int _epoch;

        /// <summary>
        /// ttl/utcNow are injectable so tests can cross the expiry
        /// boundary without waiting, matching TradingPostService's own
        /// utcNow seam.
        /// </summary>
        public CachingAccountRecipeClient(
            IAccountRecipeClient inner, TimeSpan? ttl = null, Func<DateTime> utcNow = null)
        {
            _inner = inner;
            _ttl = ttl ?? DefaultTtl;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public bool HasRequiredPermission()
        {
            return _inner.HasRequiredPermission();
        }

        /// <summary>
        /// Serves the cached id set while it is inside the TTL, otherwise
        /// fetches through and caches the result. A failing fetch is not
        /// cached in any form: the exception propagates to the caller
        /// (CraftingPlanPipeline already degrades it to "unknown") and the
        /// next call retries, exactly like CurrencyMetadataService's own
        /// never-negative-cache rule.
        /// </summary>
        public async Task<ISet<int>> GetLearnedRecipeIdsAsync(CancellationToken ct)
        {
            int startedUnderEpoch;

            lock (_cacheLock)
            {
                if (_cached != null && _utcNow() - _fetchedUtc < _ttl)
                {
                    return Copy(_cached);
                }

                startedUnderEpoch = _epoch;
            }

            // Deliberately outside the lock, and deliberately without an
            // in-flight latch: two overlapping generations may each fetch
            // once, the same benign duplicate CurrencyMetadataService
            // accepts. Holding a lock across this await would be the real
            // hazard.
            var fetched = await _inner.GetLearnedRecipeIdsAsync(ct);

            lock (_cacheLock)
            {
                var ids = fetched == null ? new HashSet<int>() : new HashSet<int>(fetched);

                // Answered for the caller that asked before the credential
                // changed, but not stored: see _epoch.
                if (_epoch != startedUnderEpoch)
                {
                    return Copy(ids);
                }

                _cached = ids;
                _fetchedUtc = _utcNow();
                return Copy(_cached);
            }
        }

        /// <summary>
        /// Drops the cached ids so the next call queries again. Called when
        /// the API key changes (Module.OnSubtokenUpdated): the cached set
        /// belongs to whichever account the previous subtoken addressed, and
        /// no amount of wall-clock freshness makes it the new account's.
        /// </summary>
        public void Invalidate()
        {
            lock (_cacheLock)
            {
                _cached = null;
                _epoch++;
            }
        }

        // Callers hand the returned set into a CraftingPlanResult that
        // outlives the call; the cache must not alias anything reachable
        // from a plan.
        private static ISet<int> Copy(HashSet<int> ids)
        {
            return new HashSet<int>(ids);
        }
    }
}
