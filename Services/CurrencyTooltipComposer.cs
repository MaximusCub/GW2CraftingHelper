using System.Globalization;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The ONE shape a wallet currency's tooltip has, wherever the row
    /// lives - the Snapshot tab's wallet list, the plan's currency table,
    /// the Ranker's shortfall chips, the Settings valuation grid. The
    /// currency-space twin of <see cref="ItemStatTooltipComposer"/>.
    ///
    /// <para>
    /// The game's shape, in its order: the icon+name header every tooltip
    /// opens with, the wallet balance, the currency's own prose, then the
    /// type line. Every part is API data - /v2/currencies for name, icon
    /// and description, /v2/account/wallet for the balance - and a part
    /// the session does not hold is DROPPED, never substituted.
    /// </para>
    /// <para>
    /// Blish-free (repo invariant), so the line-by-line contract is
    /// unit-testable without a live control.
    /// </para>
    /// </summary>
    internal static class CurrencyTooltipComposer
    {
        /// <summary>
        /// The type line, which for a wallet currency is the same word for
        /// every id - there is no per-currency type in /v2/currencies to
        /// read one from.
        /// </summary>
        public const string TypeLine = "Currency";

        /// <summary>
        /// The wallet balance line's suffix. The game names the container
        /// rather than labelling the number ("1,234 in Wallet", not
        /// "Wallet: 1,234"), the same unlabelled style the item tooltip's
        /// vendor value row takes.
        /// </summary>
        public const string WalletSuffix = " in Wallet";

        /// <summary>
        /// Composes <paramref name="facts"/> into the game's line order.
        /// A nameless subject composes nothing at all - there is no
        /// currency to describe, and a body under an empty header would be
        /// prose about nothing.
        /// </summary>
        public static TooltipContent BuildContent(CurrencyTooltipFacts facts)
        {
            if (!facts.HasSubject)
            {
                return TooltipContent.Empty;
            }

            var builder = new TooltipContentBuilder();

            // No rarity: a currency has none, and the neutral name colour
            // is the same statement ItemIconFrame.Currency() makes about
            // the icon beside it.
            builder.Header(facts.IconUrl, facts.Name, null);

            if (facts.WalletQuantity.HasValue)
            {
                builder
                    .Text(FormatCount(facts.WalletQuantity.Value) + WalletSuffix)
                    .EndLine();
            }

            // The description's own <c=@...> runs decide the colours, the
            // same way an item description's do - flattening the string to
            // one role is what stops a flavour run reading as flavour.
            var spans = ItemDescriptionSanitizer.SanitizeToSpans(facts.Description);
            if (spans.Count > 0)
            {
                foreach (var span in spans)
                {
                    builder.Styled(span.Text, span.Role);
                }

                builder.EndLine();
            }

            return builder.Text(TypeLine).EndLine().Build();
        }

        // Thousands-separated: a wallet balance runs to seven figures where
        // an item count does not. Invariant culture, the module's standing
        // policy for its English-only strings.
        private static string FormatCount(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
