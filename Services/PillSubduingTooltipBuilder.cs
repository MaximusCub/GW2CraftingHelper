using System.Collections.Generic;
using System.Text;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Builds the "why this pill is subdued" tooltip text - Blish-free so
    /// the wording is directly unit-testable; the View only assigns it.
    /// Resolves ids to names via the shared resolvers and never surfaces
    /// a raw id (repo invariant).
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
                // Only mention "your current currency values" when a
                // non-coin cost actually participated - a plain-gold
                // difference never touched a currency valuation.
                if (!result.HasNonCoinCost)
                {
                    return result.ValueMarginCopper.HasValue
                        ? $"More expensive ({FormatCoin(result.ValueMarginCopper.Value)} more)"
                        : "More expensive";
                }
                return result.ValueMarginCopper.HasValue
                    ? $"More expensive at your current currency values ({FormatCoin(result.ValueMarginCopper.Value)} more)"
                    : "More expensive at your current currency values";
            }

            // StrictDomination: "needs everything the selected option
            // needs, plus N more X" - no valuation needed. Deliberately
            // not "same currencies": a kind absent on the selected side
            // reads as zero, so the two sides need not share currencies.
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
            return $"Always more expensive - needs everything the selected option needs, plus {suffix}";
        }

        // Shares CoinSegmentMath.Split with every other coin display site,
        // but a different output format than ValueDetailTooltipBuilder's
        // FormatCoin (leading zero units omitted here, always three units
        // there) - the formats stay deliberately independent.
        private static string FormatCoin(long copper)
        {
            var (gold, silver, cop) = CoinSegmentMath.Split(copper);

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
