using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// One item on the Crafting Ranker's priority list. Name/IconUrl/Rarity
    /// are denormalized for the same reason SnapshotItemEntry duplicates
    /// them: the list renders before any solve has run, with no metadata
    /// round trip available.
    /// </summary>
    public class RankerWatchlistEntry
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }

        /// <summary>GW2 API rarity string, for the icon frame colour; null = unknown.</summary>
        public string Rarity { get; set; }
    }

    /// <summary>The whole ranker.json payload.</summary>
    public class RankerWatchlist
    {
        public const int CurrentSchemaVersion = 1;

        // No property initializer: a file that omits the field must
        // deserialize to 0 and be rejected, exactly as PersistedPlan does.
        public int SchemaVersion { get; set; }

        /// <summary>
        /// LIST ORDER IS THE PRIORITY ORDER. Index 0 is highest priority and
        /// has first claim on the account's materials, currencies, coin and
        /// daily crafts (see RankerPriorityCascade). There is deliberately no
        /// stored rank field: a rank int can drift out of sync with the
        /// list's actual order, and the list is what renders and what the
        /// cascade walks.
        /// </summary>
        public List<RankerWatchlistEntry> Entries { get; set; } = new List<RankerWatchlistEntry>();
    }
}
