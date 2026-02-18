using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public static class Gw2Constants
    {
        /// <summary>
        /// GW2 wallet currency ID for coins (gold/silver/copper).
        /// </summary>
        public const int CoinCurrencyId = 1;

        // TODO: Currency names are sourced from api.guildwars2.com/v2/currencies.
        // Verify against the official API if broadening coverage beyond this set.
        public static readonly Dictionary<int, string> KnownCurrencyNames = new Dictionary<int, string>
        {
            { 2, "Karma" },
            { 3, "Laurels" },
            { 4, "Gems" },
            { 5, "Ascalonian Tears" },
            { 6, "Shards of Zhaitan" },
            { 7, "Fractal Relics" },
            { 9, "Seals of Beetletun" },
            { 10, "Manifesto of the Moletariate" },
            { 11, "Deadly Blooms" },
            { 12, "Symbols of Koda" },
            { 13, "Flame Legion Charr Carvings" },
            { 14, "Knowledge Crystals" },
            { 15, "Badges of Honor" },
            { 16, "Guild Commendations" },
            { 18, "Transmutation Charges" },
            { 19, "Airship Parts" },
            { 20, "Ley Line Crystals" },
            { 22, "Lumps of Aurillium" },
            { 23, "Spirit Shards" },
            { 24, "Pristine Fractal Relics" },
            { 25, "Geodes" },
            { 26, "WvW Skirmish Claim Tickets" },
            { 27, "Bandit Crests" },
            { 28, "Magnetite Shards" },
            { 29, "Provisioner Tokens" },
            { 30, "PvP League Tickets" },
            { 32, "Unbound Magic" },
            { 33, "Ascended Shards of Glory" },
            { 34, "Trade Contracts" },
            { 36, "Elegy Mosaics" },
            { 45, "Volatile Magic" },
            { 47, "Racing Medallions" },
            { 49, "Festival Tokens" },
            { 50, "Mistborn Motes" },
            { 58, "Jade Slivers" },
            { 59, "Research Notes" },
            { 60, "Imperial Favors" },
            { 62, "Unusual Coins" },
            { 63, "Astral Acclaim" },
            { 78, "Fine Rift Essence" },
            { 79, "Rare Rift Essence" },
            { 80, "Masterwork Rift Essence" }
        };

        public static string ResolveCurrencyName(int currencyId)
        {
            if (KnownCurrencyNames.TryGetValue(currencyId, out var name))
            {
                return name;
            }
            return "Currency";
        }
    }
}
