using System.Collections.Generic;
using System.Linq;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// User-provided coin valuations for non-coin currencies (karma,
    /// laurels, Spirit Shards, ...). The GW2 API defines no exchange rate
    /// for these, so the solver never invents one (repo invariant): only
    /// currencies the user explicitly priced here are usable for cost
    /// comparison. Currencies with no entry remain unvalued, and vendor
    /// offers charging them stay fallback-tier only (see
    /// PlanSolver.EvaluateVendorOffers).
    /// </summary>
    public class CurrencyValuation
    {
        /// <summary>No user-provided valuations. The default when none is configured.</summary>
        public static readonly CurrencyValuation None = new CurrencyValuation(new Dictionary<int, long>());

        private readonly IReadOnlyDictionary<int, long> _copperPerUnit;

        public CurrencyValuation(IReadOnlyDictionary<int, long> copperPerUnit)
        {
            // Defensively copied: instances are stored long-term on
            // PlanSolveContext, so a caller mutating the dictionary it
            // passed in must never retroactively change an already-built
            // valuation.
            _copperPerUnit = copperPerUnit == null
                ? new Dictionary<int, long>()
                : copperPerUnit.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>CurrencyId -> copper value of a single unit of that currency.</summary>
        public IReadOnlyDictionary<int, long> CopperPerUnit => _copperPerUnit;

        /// <summary>
        /// Looks up the user-provided copper value of one unit of
        /// <paramref name="currencyId"/>. Returns false when the user has
        /// not set a valuation for that currency.
        /// </summary>
        public bool TryGetCopperValue(int currencyId, out long copperPerUnit)
        {
            return _copperPerUnit.TryGetValue(currencyId, out copperPerUnit);
        }
    }
}
