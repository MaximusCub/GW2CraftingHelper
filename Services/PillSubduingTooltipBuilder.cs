using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Builds the "why this pill is subdued" tooltip text - Blish-free so
    /// the wording is directly unit-testable; the View only assigns it.
    /// Resolves ids to names via the shared resolvers and never surfaces
    /// a raw id (repo invariant).
    /// </summary>
    internal static class PillSubduingTooltipBuilder
    {
        /// <summary>
        /// The tooltip for a subdued pill, or null when there is no reason
        /// to show one. The gold margin stays a coin span so the rich
        /// tooltip surface can draw it with real coin icons; every other
        /// part is prose.
        /// <para>
        /// Unwrapped: the surface that renders this wraps against a real
        /// font at a real pixel width.
        /// </para>
        /// </summary>
        public static TooltipContent BuildContent(
            PillSubduingResult result,
            IReadOnlyDictionary<int, ItemMetadata> itemMetadata,
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata)
        {
            if (result == null || result.Rule == PillSubduingRule.None)
            {
                return null;
            }

            var builder = new TooltipContentBuilder();

            if (result.Rule == PillSubduingRule.Weighted)
            {
                // Only mention "your current currency values" when a
                // non-coin cost actually participated - a plain-gold
                // difference never touched a currency valuation.
                builder.Text(result.HasNonCoinCost
                    ? "More expensive at your current currency values"
                    : "More expensive");
                if (result.ValueMarginCopper.HasValue)
                {
                    AppendCoin(builder.Text(" ("), result.ValueMarginCopper.Value).Text(" more)");
                }

                return builder.Build();
            }

            // StrictDomination: "needs everything the selected option
            // needs, plus N more X" - no valuation needed. Deliberately
            // not "same currencies": a kind absent on the selected side
            // reads as zero, so the two sides need not share currencies.
            builder.Text("Always more expensive - needs everything the selected option needs, plus ");

            int written = 0;
            if (result.Deltas != null)
            {
                foreach (var delta in result.Deltas)
                {
                    switch (delta.Kind)
                    {
                        case "Coin":
                            AppendCoin(AppendSeparator(builder, written), delta.Amount).Text(" more");
                            written++;
                            break;
                        case "Currency":
                            AppendSeparator(builder, written).Text(
                                $"{delta.Amount} more {CurrencyDisplayResolver.ResolveName(delta.Id, currencyMetadata)}");
                            written++;
                            break;
                        case "Item":
                            AppendSeparator(builder, written).Text(
                                $"{delta.Amount} more {PlanViewModelBuilder.ResolveName(delta.Id, itemMetadata)}");
                            written++;
                            break;
                    }
                }
            }

            if (written == 0)
            {
                builder.Text("always more expensive");
            }

            return builder.Build();
        }

        private static TooltipContentBuilder AppendSeparator(TooltipContentBuilder builder, int written)
        {
            return written > 0 ? builder.Text(", ") : builder;
        }

        private static TooltipContentBuilder AppendCoin(TooltipContentBuilder builder, long copper)
        {
            return builder.Coin(copper, CoinSegmentMath.GameStyleText(copper));
        }
    }
}
