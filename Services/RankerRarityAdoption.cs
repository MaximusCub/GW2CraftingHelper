using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Where a Crafting Ranker row's rarity comes from, and when it is worth
    /// keeping.
    ///
    /// The Add-time search result carries no rarity (see
    /// <c>ItemSearchResult</c>), and the bundled name seed carries none
    /// either, so a freshly added row genuinely cannot know its rarity and
    /// renders neutral - never guessed. Two sources fill it in afterwards,
    /// both free of any extra request:
    /// <list type="bullet">
    /// <item><description>a refresh's own solve, whose
    /// <c>ItemMetadata</c> came from /v2/items;</description></item>
    /// <item><description>the session stat cache, which any plan generated
    /// this session already filled for that item.</description></item>
    /// </list>
    ///
    /// Adoption is one-way and never clears: a known rarity is a fact about
    /// the item, and a source that has nothing to say must not overwrite one
    /// that did.
    /// </summary>
    internal static class RankerRarityAdoption
    {
        /// <summary>Returns true when the entry took a rarity it did not already carry.</summary>
        internal static bool TryAdopt(RankerWatchlistEntry entry, string rarity)
        {
            if (entry == null || string.IsNullOrEmpty(rarity))
            {
                return false;
            }

            if (string.Equals(entry.Rarity, rarity, StringComparison.Ordinal))
            {
                return false;
            }

            entry.Rarity = rarity;
            return true;
        }

        /// <summary>Adopts from a solve's item metadata, keyed by the entry's own item id.</summary>
        internal static bool AdoptFromMetadata(
            RankerWatchlistEntry entry, IReadOnlyDictionary<int, ItemMetadata> metadata)
        {
            if (entry == null || metadata == null)
            {
                return false;
            }

            return metadata.TryGetValue(entry.ItemId, out var meta) && TryAdopt(entry, meta?.Rarity);
        }

        /// <summary>
        /// Fills in every entry the session stat cache already knows about,
        /// for rows that have never been through a refresh of their own.
        /// Returns true when at least one entry changed, which is the
        /// caller's cue to persist.
        /// </summary>
        internal static bool AdoptFromStatCache(
            IReadOnlyList<RankerWatchlistEntry> entries, Func<int, ItemStatBlock> getStatBlock)
        {
            if (entries == null || getStatBlock == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !string.IsNullOrEmpty(entry.Rarity) || entry.ItemId <= 0)
                {
                    continue;
                }

                changed |= TryAdopt(entry, getStatBlock(entry.ItemId)?.Rarity);
            }

            return changed;
        }
    }
}
