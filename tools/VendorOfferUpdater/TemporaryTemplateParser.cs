using System.Text.RegularExpressions;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Parses the GW2 Wiki's {{Temporary|...}} template out of a vendor NPC
    /// page's raw wikitext, to pull out the display-name value of whichever
    /// festival or time-limited event the vendor is associated with.
    ///
    /// The template name is matched case-insensitively, and the "seasonal="
    /// and "event=" parameters are treated identically: both casings and
    /// both parameter names occur on real pages. A page carrying
    /// {{Temporary|release=...}} with neither parameter yields null, the
    /// same as a page with no template at all. The extracted value is NOT
    /// normalized against wiki markup, so "seasonal=[[Halloween]]" would
    /// extract the literal "[[Halloween]]". Deciding whether an extracted
    /// value names one of the six known festivals is
    /// Gw2Constants.ResolveSeasonalFestivalKey's job, not this parser's.
    ///
    /// The live pages each of those shapes was read off, and the regex's
    /// known stray-'}' limitation, are in docs/ARCHITECTURE.md section T.2.
    /// </summary>
    internal static class TemporaryTemplateParser
    {
        // Matches up to the first LITERAL '}' via the negated class
        // [^}]*, so a stray '}' inside a template's parameter list makes
        // that template unmatchable - see docs/ARCHITECTURE.md section T.2.
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
