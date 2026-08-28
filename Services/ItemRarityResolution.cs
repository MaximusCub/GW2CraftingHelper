using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// THE rarity-resolution policy: one place that decides what rarity an
    /// item row renders at, so a surface cannot grow its own answer and
    /// drift (the Snapshot tab and the Crafting Ranker had drifted into two,
    /// both landing on a neutral frame for most rows).
    ///
    /// <para>
    /// A view holds two candidate values: the one CAPTURED alongside the
    /// row's name and icon when the row's data was fetched, and the one the
    /// SESSION's item stat cache happens to hold because a plan touched that
    /// item this session. The captured value is authoritative - it came from
    /// the same /v2/items response as the name beside it - and the session
    /// cache is the fallback for rows written before the capture carried
    /// rarity at all. Neither is ever guessed: an id nobody has looked up
    /// resolves to null, and null renders the neutral frame
    /// <c>RarityColors</c> reserves for "unknown".
    /// </para>
    /// </summary>
    internal static class ItemRarityResolution
    {
        // The GW2 API's rarity vocabulary, in the exact spelling
        // RarityColors switches on. A lookup rather than an enum parse so an
        // unrecognised string - a new rarity, a typo in an old file -
        // resolves to "unknown" instead of to a wrong colour, and a
        // dictionary rather than a scan because this runs once per row while
        // the Snapshot grid builds its visible page.
        private static readonly Dictionary<string, string> Canonical =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Junk", "Junk" },
                { "Basic", "Basic" },
                { "Fine", "Fine" },
                { "Masterwork", "Masterwork" },
                { "Rare", "Rare" },
                { "Exotic", "Exotic" },
                { "Ascended", "Ascended" },
                { "Legendary", "Legendary" },
            };

        /// <summary>
        /// The canonical spelling of <paramref name="raw"/>, or null when it
        /// is absent or not a rarity this module knows. Case-insensitive:
        /// the value can arrive from a persisted file written by an older
        /// build, and Gw2Sharp's own enum name ("Unknown" for a value it did
        /// not recognise) must resolve to null rather than to a colour.
        /// </summary>
        internal static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string canonical;
            return Canonical.TryGetValue(raw.Trim(), out canonical) ? canonical : null;
        }

        /// <summary>
        /// The rarity a row renders at: its captured value, else whatever the
        /// session's stat cache knows, else null (unknown - neutral frame,
        /// neutral name).
        /// <para>
        /// Callers pass BOTH candidates and use the ONE returned value for
        /// the icon frame and the name colour. Resolving them separately is
        /// how the two came to disagree.
        /// </para>
        /// </summary>
        internal static string Resolve(string capturedRarity, string sessionRarity)
        {
            return Normalize(capturedRarity) ?? Normalize(sessionRarity);
        }
    }
}
