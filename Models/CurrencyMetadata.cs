namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Name/icon metadata for a GW2 wallet currency, fetched from
    /// api.guildwars2.com/v2/currencies. Purely presentational - currency
    /// IDs themselves remain internal-only and are never displayed.
    /// </summary>
    public class CurrencyMetadata
    {
        public int CurrencyId { get; set; }

        public string Name { get; set; }

        public string IconUrl { get; set; }
    }
}
