namespace TaimisToolbench.Models
{
    /// <summary>
    /// Name/icon/description metadata for a GW2 wallet currency, fetched
    /// from api.guildwars2.com/v2/currencies. Purely presentational -
    /// currency IDs themselves remain internal-only and are never
    /// displayed.
    /// </summary>
    internal class CurrencyMetadata
    {
        public int CurrencyId { get; set; }

        public string Name { get; set; }

        public string IconUrl { get; set; }

        /// <summary>
        /// The endpoint's own prose for this currency - the paragraph the
        /// game's currency tooltip shows under the wallet balance. "" when
        /// the reply carries none; never invented.
        /// </summary>
        public string Description { get; set; }
    }
}
