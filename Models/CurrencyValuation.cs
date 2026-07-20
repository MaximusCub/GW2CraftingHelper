using System;
using System.Collections.Generic;

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
            if (copperPerUnit == null)
            {
                _copperPerUnit = new Dictionary<int, long>();
                return;
            }

            // Defensively copied: instances are stored long-term on
            // PlanSolveContext, so a caller mutating the dictionary it
            // passed in must never retroactively change an already-built
            // valuation. Validated while copying: an invalid entry here
            // would either be inert (<=0 copper never beats a coin option)
            // or nonsensical (coin priced in terms of itself), so callers
            // must fix the input rather than have it silently accepted.
            var validated = new Dictionary<int, long>(copperPerUnit.Count);
            foreach (var kvp in copperPerUnit)
            {
                if (kvp.Key == Gw2Constants.CoinCurrencyId)
                {
                    throw new ArgumentException(
                        "Currency valuation cannot be keyed on the coin currency id.",
                        nameof(copperPerUnit));
                }
                if (kvp.Value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(copperPerUnit),
                        kvp.Value,
                        $"Currency {kvp.Key} must have a positive copper-per-unit valuation.");
                }
                validated[kvp.Key] = kvp.Value;
            }

            _copperPerUnit = validated;
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
