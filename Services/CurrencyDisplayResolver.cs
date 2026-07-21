using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Resolves raw currency ids (CostLine/CurrencyCost) to display-ready
    /// name/icon (CurrencyAmountViewModel), preferring live
    /// CurrencyMetadataService data and falling back to the offline
    /// Gw2Constants table - the exact same chain PlanViewModelBuilder's
    /// Summary-section currency rows have used since M30 #3, now shared so
    /// shopping-row and recipe-tree currency costs (KNOWN-ISSUES #16) never
    /// drift from it. Blish-free (plain C#, no Blish/Gw2Sharp types) so the
    /// mapping is directly unit-testable - see CurrencyDisplayResolverTests.
    /// The no-displayed-IDs invariant is enforced by construction here:
    /// CurrencyAmountViewModel has no id field at all, only Amount/Name/
    /// IconUrl, so a caller cannot accidentally surface a raw currency id.
    /// </summary>
    public static class CurrencyDisplayResolver
    {
        /// <summary>
        /// Prefers the live-fetched currency name when currencyMetadata has
        /// resolved it, falling back to the offline Gw2Constants table when
        /// metadata is null/absent for this id or came back with an empty
        /// name.
        /// </summary>
        public static string ResolveName(
            int currencyId, IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            if (currencyMetadata != null &&
                currencyMetadata.TryGetValue(currencyId, out var meta) &&
                !string.IsNullOrEmpty(meta.Name))
            {
                return meta.Name;
            }
            return Gw2Constants.ResolveCurrencyName(currencyId);
        }

        /// <summary>
        /// Icon for a currency amount; null (never a placeholder guess)
        /// when metadata is absent for this id or has no icon URL.
        /// </summary>
        public static string ResolveIconUrl(
            int currencyId, IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            if (currencyMetadata != null &&
                currencyMetadata.TryGetValue(currencyId, out var meta) &&
                !string.IsNullOrEmpty(meta.IconUrl))
            {
                return meta.IconUrl;
            }
            return null;
        }

        /// <summary>
        /// Resolves a full non-coin currency cost (CostLine list, already
        /// scaled to the caller's quantity - e.g. PlanStep/SolverDecision's
        /// VendorCurrencyCosts, or a CraftingTreeNode's) into display-ready
        /// amounts. Null/empty input yields null (never an empty-but-
        /// non-null list), so callers can use a simple null/Count==0 check
        /// for "does this row/node have a currency cost at all".
        /// </summary>
        public static List<CurrencyAmountViewModel> ResolveAmounts(
            IReadOnlyList<CostLine> costLines,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            if (costLines == null || costLines.Count == 0)
            {
                return null;
            }

            var result = new List<CurrencyAmountViewModel>(costLines.Count);
            foreach (var line in costLines)
            {
                result.Add(new CurrencyAmountViewModel
                {
                    Amount = line.Count,
                    Name = ResolveName(line.Id, currencyMetadata),
                    IconUrl = ResolveIconUrl(line.Id, currencyMetadata)
                });
            }
            return result;
        }

        /// <summary>
        /// Per-unit counterpart of ResolveAmounts: each line's total Count
        /// is integer-divided by quantity, the same truncating division
        /// PlanSolver.AggregateStep already uses for UnitCost. quantity
        /// &lt;= 0 (should not happen for a real row, but a plan step can
        /// in principle be malformed) returns null rather than dividing by
        /// zero.
        /// </summary>
        public static List<CurrencyAmountViewModel> ResolveUnitAmounts(
            IReadOnlyList<CostLine> costLines,
            int quantity,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            if (costLines == null || costLines.Count == 0 || quantity <= 0)
            {
                return null;
            }

            var result = new List<CurrencyAmountViewModel>(costLines.Count);
            foreach (var line in costLines)
            {
                result.Add(new CurrencyAmountViewModel
                {
                    Amount = line.Count / quantity,
                    Name = ResolveName(line.Id, currencyMetadata),
                    IconUrl = ResolveIconUrl(line.Id, currencyMetadata)
                });
            }
            return result;
        }
    }
}
