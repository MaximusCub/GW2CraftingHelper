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
    }
}
