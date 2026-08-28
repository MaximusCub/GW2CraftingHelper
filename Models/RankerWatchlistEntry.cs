using System.Collections.Generic;
using Newtonsoft.Json;

namespace TaimisToolbench.Models
{
    /// <summary>
    /// Which question the Crafting Ranker's table answers. Internal (with
    /// the persisted property below carrying an explicit JsonProperty) so
    /// the mode does not widen the module's pinned public surface.
    /// </summary>
    internal enum RankerMode
    {
        /// <summary>
        /// "Given my order, how close is each item?" - every row is measured
        /// after the rows above it claim materials, currencies, coin and
        /// daily crafts (RankerPriorityCascade).
        /// </summary>
        Cascade = 0,

        /// <summary>
        /// "Which is closest to done right now?" - every row is measured
        /// against the full account, ignoring the other rows, and the table
        /// displays by readiness. The stored priority order is untouched.
        /// </summary>
        Independent = 1,
    }

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

        /// <summary>
        /// The selected comparison mode. Additive: a file written before the
        /// field existed deserializes to Cascade, the original behaviour, so
        /// no schema bump and no list loss. JsonProperty is load-bearing -
        /// the property is internal (see RankerMode) and Json.NET skips
        /// non-public members without it.
        /// </summary>
        [JsonProperty]
        internal RankerMode Mode { get; set; }

        /// <summary>
        /// Whether the table hides each row's currency detail and notes,
        /// showing the headline and the gate percentages alone. Additive in
        /// exactly the same way as Mode: a file written before the field
        /// existed deserializes to false, which is the full breakdown the
        /// tab has always shown.
        /// </summary>
        public bool Compact { get; set; }
    }
}
