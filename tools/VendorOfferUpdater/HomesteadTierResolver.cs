using System;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Resolves a wiki-scraped vendor row's Homestead Refinement efficiency
    /// tier from its merchant name and raw "Has
    /// requirement" SMW property text. Kept separate from ConvertToOffer so
    /// this pure resolution logic is covered by direct unit tests without a
    /// Gw2ApiHelper/HttpClient fixture.
    ///
    /// Mirrors gw2efficiency's own cheapestTree.ts matching shape (docs/
    /// research/m37-r1-homestead.md Section 1.2): a row only participates
    /// in tier gating when its merchant name contains the literal substring
    /// "Homestead Refinement" (gw2e: tree.merchant.name.includes('Homestead
    /// Refinement')) - matching all three station pages ("...-Farm",
    /// "...-Lumber Mill", "...-Metal Forge") the same way a plain
    /// `.includes()` substring test would.
    ///
    /// Confirmed live (direct SMW ask probe against Homestead
    /// Refinement-Metal Forge, ): a tier-0 row's "Has requirement"
    /// printout returns an empty array; a tier-1/tier-2 row returns exactly
    /// one _txt value, "one [[Homestead Upgrade: ...]]" or "two [[Homestead
    /// Upgrade: ...]]" respectively - the wiki's {{vendor table row}}
    /// template parameter is literally `requirement=one [[...]]` /
    /// `requirement=two [[...]]`.
    /// </summary>
    public static class HomesteadTierResolver
    {
        private const string HomesteadRefinementMarker = "Homestead Refinement";

        /// <summary>
        /// Returns the resolved tier (0/1/2), or null when
        /// <paramref name="merchantName"/> is not a Homestead Refinement
        /// station, or when it is but <paramref name="requirement"/> is
        /// non-empty and does not match either recognized pattern (never
        /// invent a tier for unrecognized requirement text - a caller
        /// should log and leave the row untagged in that case).
        /// </summary>
        public static int? ResolveTier(string? merchantName, string? requirement)
        {
            if (string.IsNullOrEmpty(merchantName) ||
                merchantName.IndexOf(HomesteadRefinementMarker, StringComparison.Ordinal) < 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(requirement))
            {
                return 0;
            }

            string trimmed = requirement.TrimStart();

            if (StartsWithWord(trimmed, "one"))
            {
                return 1;
            }

            if (StartsWithWord(trimmed, "two"))
            {
                return 2;
            }

            return null;
        }

        private static bool StartsWithWord(string text, string word)
        {
            if (!text.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Require a word boundary (exact match or followed by
            // whitespace) so e.g. "onerous" never matches "one".
            return text.Length == word.Length || char.IsWhiteSpace(text[word.Length]);
        }
    }
}
