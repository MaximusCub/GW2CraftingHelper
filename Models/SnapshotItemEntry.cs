using System.Collections.Generic;
using Newtonsoft.Json;

namespace TaimisToolbench.Models
{
    internal class SnapshotItemEntry
    {
        public int ItemId { get; set; }

        public string Name { get; set; } = "";

        public string IconUrl { get; set; } = "";

        // Captured from the SAME /v2/items response Name and IconUrl come
        // from, so it costs no additional request. Schema-additive: a
        // snapshot.json written before this field existed deserializes to
        // the "" initializer, which the rarity policy reads as unknown.
        public string Rarity { get; set; } = "";

        public int Count { get; set; }

        public string Source { get; set; } = "";

        /// <summary>
        /// Item ids of the upgrade components socketed into this stack, or
        /// null when nothing is socketed.
        /// <para>
        /// Null rather than an empty list, so the pair costs no bytes on
        /// the large majority of rows and a snapshot.json written before
        /// these fields existed loads to the value it would have been
        /// written with. /v2/account/materials carries neither field, so
        /// material storage rows are always null.
        /// </para>
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<int> Upgrades { get; set; }

        /// <summary>
        /// Item ids of the infusions socketed into this stack, or null when
        /// none are. Same shape as <see cref="Upgrades"/>.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<int> Infusions { get; set; }
    }
}
