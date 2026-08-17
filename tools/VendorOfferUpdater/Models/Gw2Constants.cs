using System;
using System.Collections.Generic;

namespace VendorOfferUpdater.Models
{
    public static class Gw2Constants
    {
        public const int CoinCurrencyId = 1;

        // M37 (KNOWN-ISSUES #24): the three Homestead Refinement output
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
        /// opportunity-notes follow-up (2026-08-16, festival-vendor
        /// auto-tagging): the GW2 Wiki's own display-name text for each of
        /// the six festivals Blish HUD's FestivalContext recognizes, as it
        /// appears in a vendor NPC page's {{Temporary|...|seasonal=...}}
        /// (or, on a minority of pages, {{Temporary|...|event=...}} - see
        /// TemporaryTemplateParser) template parameter - mapped to the
        /// internal lowercase festival name key each value matches
        /// EXACTLY. Never fuzzy-matched or guessed: a wiki value not
        /// present as a key here (e.g. a one-off non-festival event) must
        /// leave the offer untagged, see ResolveSeasonalFestivalKey below.
        ///
        /// Both sides of every mapping were independently MEASURED, not
        /// invented:
        ///   - Wiki display names: fetched live via api.guildwars2.com's
        ///     wiki mirror, api.php?action=parse&amp;prop=wikitext,
        ///     2026-08-16, against one real festival vendor NPC page per
        ///     festival: "Candy Corn Vendor (Weekly)"
        ///     (seasonal=Halloween), "Dragon Bash Merchant (Weekly)"
        ///     (seasonal=Dragon Bash), "Wintersday Trader (Weekly)"
        ///     (seasonal=Wintersday), "Festival Rewards Vendor (Weekly)"
        ///     (seasonal=Festival of the Four Winds), "New Year Vendor"
        ///     (seasonal=Lunar New Year), "Super Adventure Box Weekly
        ///     Trader" (seasonal=Super Adventure Festival).
        ///   - Internal key strings: MEASURED via `strings -e l` (raw
        ///     UTF-16LE text scan) against
        ///     packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe - the
        ///     literal lowercase user-string-heap values "halloween",
        ///     "dragonbash", "wintersday", "festivalofthefourwinds",
        ///     "lunarnewyear", "superadventurefestival" all appear
        ///     verbatim next to their matching get_Festival_* property-
        ///     getter method names - the same technique the runtime
        ///     Models/Gw2Constants.cs's HalloweenFestivalName doc comment
        ///     used to measure "halloween" alone (via .NET reflection
        ///     there instead of a raw string scan, but confirming the
        ///     identical value).
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
