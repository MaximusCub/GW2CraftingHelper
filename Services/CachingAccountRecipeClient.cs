using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Short-lived session cache in front of another
    /// <see cref="IAccountRecipeClient"/> (in production
    /// <see cref="Gw2AccountRecipeClient"/>, i.e. /v2/account/recipes).
    /// Every plan generation asks for the learned ids once
    /// (CraftingPlanPipeline.FetchLearnedRecipeIdsAsync) and that round
    /// trip was measured at 327-4557ms, on the warm path as well as the
    /// cold one, so it is worth not repeating for back-to-back plans.
    /// <para>
    /// A separate decorator rather than a field inside
    /// <see cref="Gw2AccountRecipeClient"/>: that class holds a Blish
    /// Gw2ApiManager, and tests in this repo are Blish-free, so caching
    /// logic living there could never be exercised by a test.
    /// </para>
    /// <para>
    /// STALENESS IS COSMETIC. Learned recipe ids only drive the
    /// "already known" annotation on required recipes
    /// (PlanResultBuilder's RecipeRequirement.IsMissing); they never
    /// affect a craft-vs-buy decision, a quantity, or a cost. A recipe
    /// learned in-game inside the TTL window shows as still-missing until
    /// the window passes, and nothing else about the plan differs.
    /// </para>
    /// </summary>
    public class CachingAccountRecipeClient : IAccountRecipeClient
    {
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

        private readonly IAccountRecipeClient _inner;
        private readonly TimeSpan _ttl;
        private readonly Func<DateTime> _utcNow;
        private readonly object _cacheLock = new object();
        private HashSet<int> _cached;
        private DateTime _fetchedUtc;

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
            lock (_cacheLock)
            {
                if (_cached != null && _utcNow() - _fetchedUtc < _ttl)
                {
                    return Copy(_cached);
                }
            }

            // Deliberately outside the lock, and deliberately without an
            // in-flight latch: two overlapping generations may each fetch
            // once, the same benign duplicate CurrencyMetadataService
            // accepts. Holding a lock across this await would be the real
            // hazard.
            var fetched = await _inner.GetLearnedRecipeIdsAsync(ct);

            lock (_cacheLock)
            {
                _cached = fetched == null ? new HashSet<int>() : new HashSet<int>(fetched);
                _fetchedUtc = _utcNow();
                return Copy(_cached);
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
