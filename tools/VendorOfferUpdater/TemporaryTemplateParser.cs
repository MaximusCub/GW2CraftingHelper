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
    /// api.php?action=parse&amp;prop=wikitext, ):
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
    ///   - Known, untested shape (no live page
    ///     has shown it yet): the extracted value is NOT normalized
    ///     against wiki markup - "seasonal=[[Halloween]]" would extract
    ///     the literal "[[Halloween]]", which is correctly left untagged
    ///     with a warning by Gw2Constants.ResolveSeasonalFestivalKey
    ///     rather than fuzzy-matched or guessed (per the never-guess
    ///     repo invariant), but is worth knowing about if a future wiki
    ///     edit ever introduces wikilink-wrapped parameter values.
    /// </summary>
    internal static class TemporaryTemplateParser
    {
        // The comment here used to
        // say "Non-greedy up to the first }}", but the pattern actually
        // matches up to the first LITERAL '}' character via the negated
        // class [^}]* - a single stray '}' anywhere inside a real
        // template's parameter list (not observed in practice, but not
        // impossible either) would make that template unmatchable rather
        // than just truncate its captured body early. Kept as-is (not
        // hardened further) since no real page has shown this shape -
        // documented accurately instead of describing regex behavior
        // that isn't what the pattern does.
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
        /// has no such template, or has none with either parameter set.
        /// Never returns an empty string (an explicit "seasonal=" with
        /// nothing after it is treated the same as the parameter being
        /// absent).
        ///
        /// This used to look only
        /// at the FIRST {{Temporary}} match on the page (Regex.Match) - a
        /// page with two such templates, where only the first carries
        /// release= and the second carries seasonal=/event=, would
        /// return null despite genuinely being a festival vendor. Every
        /// match is now checked in order (seasonal=/event= within each
        /// match, same preference as before); the first non-empty value
        /// found across the whole page wins.
        /// </summary>
        internal static string? ExtractSeasonalOrEventParameter(string? wikitext)
        {
            if (string.IsNullOrEmpty(wikitext))
            {
                return null;
            }

            foreach (Match templateMatch in TemplateRegex.Matches(wikitext))
            {
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
            }

            return null;
        }
    }
}
