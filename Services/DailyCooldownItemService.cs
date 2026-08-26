using System.Collections.Generic;
using System.IO;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Loads the wiki-verified daily-craft-cooldown seed data
    /// (ref/daily_cooldown_items.json) into a dictionary keyed by item id.
    /// Byte-for-byte the same load shape as AcquisitionHintService.Load
    /// (see that class's own doc comment for the full rationale) - never
    /// throws: null/empty/malformed input degrades to an empty dictionary
    /// so a bad or missing seed file never blocks module load or produces
    /// an invented cooldown notice.
    /// </summary>
    internal static class DailyCooldownItemService
    {
        private class DailyCooldownEnvelope
        {
            public int SchemaVersion { get; set; }

            public string GeneratedAt { get; set; }

            public string Source { get; set; }

            public List<DailyCooldownEntry> Items { get; set; }
        }

        private class DailyCooldownEntry
        {
            public int ItemId { get; set; }

            public int PerDayCap { get; set; }

            public string SourceUrl { get; set; }

            public string LastVerified { get; set; }
        }

        public static IReadOnlyDictionary<int, DailyCooldownItem> Load(Stream stream)
        {
            var result = new Dictionary<int, DailyCooldownItem>();
            var envelope = JsonSeedReader.Deserialize<DailyCooldownEnvelope>(stream);
            if (envelope?.Items == null)
            {
                return result;
            }

            foreach (var entry in envelope.Items)
            {
                if (entry == null || entry.ItemId <= 0 || entry.PerDayCap <= 0)
                {
                    // A zero/negative cap is not a real recipe limit (and
                    // would divide-by-zero the "days needed" math
                    // downstream); an itemId <= 0 is equally malformed (no
                    // PlanStep ever carries one). Skip rather than invent a
                    // notice from malformed seed data. Stricter than
                    // AcquisitionHintService.Load, which does not validate
                    // ItemId; harmless divergence.
                    continue;
                }

                // Last-write-wins on duplicate item ids, matching
                // AcquisitionHintService.
                result[entry.ItemId] = new DailyCooldownItem
                {
                    ItemId = entry.ItemId,
                    PerDayCap = entry.PerDayCap,
                    SourceUrl = entry.SourceUrl,
                    LastVerified = entry.LastVerified,
                };
            }

            return result;
        }
    }
}
