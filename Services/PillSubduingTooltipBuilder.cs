using System.Collections.Generic;
using System.Text;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// source-selection-simplification (maintainer-approved redesign,
    /// docs/gw2e-considerations.md): builds the "why this pill is subdued"
    /// tooltip text for a PillSubduingResult - same Blish-free "builds the
    /// string, the View only assigns it to BasicTooltipText" split
    /// ValueDetailTooltipBuilder already established for the neighboring
    /// value-detail hover, so the wording stays directly unit-testable.
    /// Resolves currency/item ids to names via the SAME resolvers the rest
    /// of the tree renderer already uses (CurrencyDisplayResolver,
    /// PlanViewModelBuilder.ResolveName) - never surfaces a raw id (repo
    /// invariant).
    /// </summary>
    public static class PillSubduingTooltipBuilder
    {
        public static string Build(
            PillSubduingResult result,
            IReadOnlyDictionary<int, ItemMetadata> itemMetadata,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            if (result == null || result.Rule == PillSubduingRule.None)
            {
                return null;
            }

            if (result.Rule == PillSubduingRule.Weighted)
            {
                return result.ValueMarginCopper.HasValue
                    ? $"More expensive at your current currency values ({FormatCoin(result.ValueMarginCopper.Value)} more)"
                    : "More expensive at your current currency values";
            }

            // StrictDomination: "same currencies, N more X[, N more Y...]" -
            // needs no valuation, joins every kind that proved the
            // domination (almost always exactly one in practice).
            var parts = new List<string>();
            if (result.Deltas != null)
            {
                foreach (var delta in result.Deltas)
                {
                    switch (delta.Kind)
                    {
                        case "Coin":
                            parts.Add($"{FormatCoin(delta.Amount)} more");
                            break;
                        case "Currency":
                            parts.Add($"{delta.Amount} more {CurrencyDisplayResolver.ResolveName(delta.Id, currencyMetadata)}");
                            break;
                        case "Item":
                            parts.Add($"{delta.Amount} more {PlanViewModelBuilder.ResolveName(delta.Id, itemMetadata)}");
                            break;
                    }
                }
            }

            string suffix = parts.Count > 0 ? string.Join(", ", parts) : "always more expensive";
            return $"Always more expensive - same currencies, {suffix}";
        }

        // Deliberately duplicates ValueDetailTooltipBuilder's own
        // FormatCoin (which itself deliberately duplicates
        // CoinCurrencyRenderer.FormatCoinText) rather than referencing
        // either - see that method's own doc comment for why this trivial
        // three-unit format stays independently duplicated per Blish-free
        // tooltip-builder class instead of shared.
        private static string FormatCoin(long copper)
        {
            if (copper < 0)
            {
                copper = 0;
            }
            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;

            var sb = new StringBuilder();
            if (gold > 0)
            {
                sb.Append(gold).Append('g');
            }
            if (silver > 0 || gold > 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(silver).Append('s');
            }
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }
            sb.Append(cop).Append('c');
            return sb.ToString();
        }
    }
}
