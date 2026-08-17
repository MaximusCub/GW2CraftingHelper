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
                // Adversarial-review finding: "at your current currency
                // values" is wrong wording for the (most common) case
                // where no Currency/Item cost is involved on EITHER side -
                // a plain-gold difference that no currency valuation ever
                // touched. Only mention currency values when a non-coin
                // cost actually participated in this comparison.
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
            // needs, plus N more X[, N more Y...]" - needs no valuation,
            // joins every kind that proved the domination (almost always
            // exactly one in practice). Adversarial-review nice-to-have:
            // deliberately NOT "same currencies" - the union in
            // TryComputeDomination treats a kind absent on the SELECTED
            // side as zero there, so e.g. selected TP 500c vs losing
            // vendor 500c + 3 Karma is a valid domination even though the
            // two sides do not use the same currencies at all.
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

        // Same gold/silver/copper split as ValueDetailTooltipBuilder's own
        // FormatCoin, but a different output format (leading zero units
        // omitted here, always three units there). Deliberately kept
        // independent per Blish-free tooltip-builder class rather than
        // shared - see that method's own doc comment for why.
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
