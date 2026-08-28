using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Small, self-contained coin-valuation helper shared by
    /// RecipeSheetSavingsCalculator (recipe-sheet vendor offers) and
    /// SeasonalVendorTipCalculator (seasonal vendor offers) - both need to
    /// answer "what does this offer's CostLines cost in coin", and both
    /// keep the strict "skip rather than guess" posture: a non-coin
    /// Currency line, an unpriced/zero-priced Item line, or any
    /// unrecognized CostLine.Type makes the WHOLE offer unpriceable rather
    /// than silently dropping that one line - repo invariant, "avoid
    /// invalid currency comparisons".
    ///
    /// Deliberately much simpler than VendorBatchSolver's own evaluation,
    /// which now DOES route an unpriced Item line into its fallback tier
    /// rather than discarding the offer (see
    /// docs/ARCHITECTURE.md section 8). It can: it has a valuation, a
    /// comparable/fallback split and a place to report non-coin costs.
    /// These two callers have none of those - each needs a single "is this
    /// priceable, and for how much" answer for ONE unscaled purchase, and
    /// nowhere to put an incomparable one - so the strict posture is right
    /// HERE and the two must not be made to converge.
    /// </summary>
    internal static class CostLineValuation
    {
        internal static bool TryGetCoinCost(
            IReadOnlyList<CostLine> costLines,
            IReadOnlyDictionary<int, ItemPrice> prices,
            PriceBasis priceBasis,
            out long coinCost)
        {
            coinCost = 0;
            if (costLines == null || costLines.Count == 0)
            {
                // Nothing to value - never invent a zero-cost offer out of
                // an empty/missing cost-line list.
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
