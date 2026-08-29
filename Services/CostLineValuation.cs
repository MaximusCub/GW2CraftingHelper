using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Small, self-contained coin-valuation helper. TryGetCoinCost is
    /// shared by RecipeSheetSavingsCalculator and
    /// SeasonalVendorTipCalculator - both
    /// need "what do these CostLines cost in coin", and both keep the strict
    /// "skip rather than guess" posture: a non-coin Currency line, an
    /// unpriced/zero-priced Item line, or any unrecognized CostLine.Type
    /// makes the WHOLE offer unpriceable rather than silently dropping that
    /// one line - repo invariant, "avoid invalid currency comparisons".
    /// <para>
    /// Deliberately simpler than VendorBatchSolver's own evaluation, which
    /// DOES route an unpriced Item line into its fallback tier (see
    /// docs/ARCHITECTURE.md section 8). It can: it has a valuation, a
    /// comparable/fallback split and a place to report non-coin costs.
    /// These two callers have none of those - each needs one "is this
    /// priceable, and for how much" answer for ONE unscaled purchase - so
    /// the strict posture is right HERE and the two must not converge.
    /// HasAnyCostLine is the one rule they DO share; the solver calls it.
    /// </para>
    /// </summary>
    internal static class CostLineValuation
    {
        /// <summary>
        /// The one home of "never invent a zero-cost purchase out of an
        /// empty or missing cost-line list": a fold over no lines
        /// terminates at 0, the cheapest number there is, and outranks
        /// every priced route. Not hypothetical - 1,896 of the 59,414 rows
        /// in ref/vendor_offers.json ship with an empty costLines array,
        /// across 721 distinct output items (MEASURED 2026-08-29).
        /// <para>
        /// Shared with VendorBatchSolver.EvaluateVendorOffers, which cannot
        /// reuse TryGetCoinCost itself - see the type doc above for why the
        /// two valuations must not converge - but must apply this one rule
        /// identically.
        /// </para>
        /// </summary>
        internal static bool HasAnyCostLine(IReadOnlyList<CostLine> costLines)
        {
            return costLines != null && costLines.Count > 0;
        }

        internal static bool TryGetCoinCost(
            IReadOnlyList<CostLine> costLines,
            IReadOnlyDictionary<int, ItemPrice> prices,
            PriceBasis priceBasis,
            out long coinCost)
        {
            coinCost = 0;
            if (!HasAnyCostLine(costLines))
            {
                return false;
            }

            foreach (var line in costLines)
            {
                if (line == null)
                {
                    // reset before every false return
                    // (not just this first-line case, which happened to
                    // already leave coinCost at its initial 0) so the
                    // contract - out param is 0 whenever this returns
                    // false - holds regardless of which line in the list
                    // fails, not only a failure on line 1.
                    coinCost = 0;
                    return false;
                }

                if (string.Equals(line.Type, "Currency", StringComparison.Ordinal))
                {
                    if (line.Id == Gw2Constants.CoinCurrencyId)
                    {
                        coinCost += (long)line.Count;
                    }
                    else
                    {
                        // Non-coin wallet currency - no safe coin conversion
                        // without a currency valuation, which neither caller
                        // has (both are informational Plan Notes, not
                        // solver decisions).
                        coinCost = 0;
                        return false;
                    }
                }
                else if (string.Equals(line.Type, "Item", StringComparison.Ordinal))
                {
                    if (prices == null || !prices.TryGetValue(line.Id, out var price))
                    {
                        coinCost = 0;
                        return false;
                    }

                    int unitPrice = PlanSolver.GetUnitPrice(price, priceBasis);
                    if (unitPrice <= 0)
                    {
                        coinCost = 0;
                        return false;
                    }

                    coinCost += (long)line.Count * unitPrice;
                }
                else
                {
                    // Unrecognized CostLine.Type (future wiki-scraped
                    // shape) - never guess at a cost for a shape this
                    // helper has never seen, mirroring VendorBatchSolver's
                    // own identical posture.
                    coinCost = 0;
                    return false;
                }
            }

            return true;
        }
    }
}
