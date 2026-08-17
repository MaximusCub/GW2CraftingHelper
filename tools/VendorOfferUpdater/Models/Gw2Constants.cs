using System;
using System.Collections.Generic;

namespace VendorOfferUpdater.Models
{
    public static class Gw2Constants
    {
        public const int CoinCurrencyId = 1;

        // The three Homestead Refinement output
        // materials. Mirrors Models/Gw2Constants.cs's identical constants
        // in the main app - kept as a separate copy here since this tool
        // does not reference the main app's assembly (see the existing
        // duplicated VendorOffer/VendorOfferHasher pattern in this project).
        public const int RefinedHomesteadFiberItemId = 102306;
        public const int RefinedHomesteadMetalItemId = 102205;
        public const int RefinedHomesteadWoodItemId = 103049;

        public static bool IsHomesteadRefinementMaterialId(int itemId)
        {
            return itemId == RefinedHomesteadFiberItemId ||
                   itemId == RefinedHomesteadMetalItemId ||
                   itemId == RefinedHomesteadWoodItemId;
        }

        /// <summary>
        /// The GW2 Wiki's display-name text for each of the six festivals
        /// Blish HUD's FestivalContext recognizes, as it appears in a
        /// vendor page's {{Temporary|...|seasonal=...}} (or event=)
        /// template parameter, mapped to the internal lowercase festival
        /// key. Matched EXACTLY, never fuzzy: a wiki value not listed here
        /// must leave the offer untagged (ResolveSeasonalFestivalKey).
        /// Both sides of every mapping were measured (live wiki fetches
        /// and a raw string scan of Blish HUD.exe), not invented - add a
        /// new entry only with both sides verified.
        /// </summary>
        public static readonly Dictionary<string, string> FestivalKeysByWikiDisplayName =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Halloween", "halloween" },
                { "Dragon Bash", "dragonbash" },
                { "Wintersday", "wintersday" },
                { "Festival of the Four Winds", "festivalofthefourwinds" },
                { "Lunar New Year", "lunarnewyear" },
                { "Super Adventure Festival", "superadventurefestival" }
            };

        /// <summary>
        /// Resolves a raw wiki-page seasonal/event display-name string
        /// (from TemporaryTemplateParser.ExtractSeasonalOrEventParameter)
        /// to the internal festival name key this module compares
        /// against, or null if the value is not one of the six known
        /// festivals - e.g. a one-off non-festival event such as "Fractal
        /// Rush" or "Fractal Incursion" (both confirmed live on real
        /// vendor NPC pages: "Consortium Trader (Fractal Rush)",
        /// "Starter Equipment Vendor"), or a page with no seasonal/event
        /// parameter at all. Callers must leave the offer untagged (never
        /// guess a festival) and log a warning for a non-null-but-
        /// unrecognized value specifically, per repo invariant (no
        /// invented data) - see ConvertToOffer in Program.cs.
        /// </summary>
        public static string? ResolveSeasonalFestivalKey(string? wikiDisplayName)
        {
            if (string.IsNullOrWhiteSpace(wikiDisplayName))
            {
                return null;
            }

            string trimmed = wikiDisplayName.Trim();
            return FestivalKeysByWikiDisplayName.TryGetValue(trimmed, out var key) ? key : null;
        }
    }
}
