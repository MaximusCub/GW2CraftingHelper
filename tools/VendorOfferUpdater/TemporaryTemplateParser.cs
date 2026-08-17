using System.Text.RegularExpressions;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Parses the GW2 Wiki's {{Temporary|...}} template out of a vendor
    /// NPC page's raw wikitext, to pull out the display-name value of
    /// whichever festival or time-limited event the vendor is associated
    /// with.
    ///
    /// Live-confirmed shapes (api.guildwars2.com wiki mirror,
    /// api.php?action=parse&amp;prop=wikitext, 2026-08-16):
    ///   - Template name casing varies in the wild: both
    ///     "{{Temporary|...}}" and "{{temporary|...}}" appear verbatim on
    ///     real pages (e.g. "Mad King's Realm" uses the lowercase form) -
    ///     matched here case-insensitively.
    ///   - Parameter name varies too. The six recurring festival vendor
    ///     NPC pages this module cares about all use "seasonal=" (e.g.
    ///     "Candy Corn Vendor (Weekly)":
    ///     {{Temporary|release=Shadow of the Mad King 2019|seasonal=Halloween}}).
    ///     A minority of vendor NPC pages use "event=" for the identical
    ///     purpose instead - confirmed on "Trader" (Bazaar of the Four
    ///     Winds): {{Temporary|release=Bazaar of the Four
    ///     Winds|event=Festival of the Four Winds}}, and on non-festival
    ///     one-off vendors "Consortium Trader (Fractal Rush)" /
    ///     "Starter Equipment Vendor":
    ///     {{temporary|event=Fractal Rush}} / {{temporary|event=Fractal Incursion}}.
    ///     Both parameters are treated identically here - it is
    ///     Gw2Constants.ResolveSeasonalFestivalKey, not this parser, that
    ///     decides whether an extracted value is one of the six known
    ///     festivals or an unrecognized one-off event/release.
    ///   - A page can also carry {{Temporary|release=...}} with neither
    ///     parameter at all (a one-off, non-festival, non-"event" release
    ///     vendor) - ExtractSeasonalOrEventParameter returns null for
    ///     that shape, same as for a page with no {{Temporary}} template
    ///     at all.
    /// </summary>
    internal static class TemporaryTemplateParser
    {
        // Non-greedy up to the first "}}" is safe here: every real
        // {{Temporary|...}} usage observed is a single, non-nested
        // template call - no "{{" appears inside its parameter list.
        private static readonly Regex TemplateRegex = new Regex(
            @"\{\{\s*Temporary\s*\|([^}]*)\}\}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SeasonalParamRegex = new Regex(
            @"(?:^|\|)\s*seasonal\s*=\s*([^|}]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EventParamRegex = new Regex(
            @"(?:^|\|)\s*event\s*=\s*([^|}]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Returns the trimmed "seasonal=" (preferred) or "event=" value
        /// from the page's {{Temporary|...}} template, or null if the page
        /// has no such template, or has one with neither parameter set.
        /// Never returns an empty string (an explicit "seasonal=" with
        /// nothing after it is treated the same as the parameter being
        /// absent).
        /// </summary>
        internal static string? ExtractSeasonalOrEventParameter(string? wikitext)
        {
            if (string.IsNullOrEmpty(wikitext))
            {
                return null;
            }

            var templateMatch = TemplateRegex.Match(wikitext);
            if (!templateMatch.Success)
            {
                return null;
            }

            string body = templateMatch.Groups[1].Value;

            var seasonalMatch = SeasonalParamRegex.Match(body);
            if (seasonalMatch.Success)
            {
                string value = seasonalMatch.Groups[1].Value.Trim();
                if (value.Length > 0)
                {
                    return value;
                }
            }

            var eventMatch = EventParamRegex.Match(body);
            if (eventMatch.Success)
            {
                string value = eventMatch.Groups[1].Value.Trim();
                if (value.Length > 0)
                {
                    return value;
                }
            }

            return null;
        }
    }
}
