using System;

namespace VendorOfferUpdater
{
    /// <summary>
    /// Resolves a wiki-scraped vendor row's Homestead Refinement efficiency
    /// tier from its merchant name and raw "Has requirement" SMW property
    /// text. A row participates in tier gating only when its merchant name
    /// contains the literal substring "Homestead Refinement"; the tier is
    /// then read off the requirement text, which is empty for tier 0 and
    /// carries exactly one "one [[Homestead Upgrade: ...]]" or "two
    /// [[Homestead Upgrade: ...]]" value for tiers 1 and 2 respectively.
    ///
    /// The gw2efficiency parity shape, the live SMW probe those readings
    /// come from, and why this is a separate class are in
    /// docs/ARCHITECTURE.md section T.1.
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
