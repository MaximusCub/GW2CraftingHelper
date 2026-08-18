using System.Collections.Generic;
using System.IO;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Loads the wiki-derived acquisition-hint seed data
    /// (ref/acquisition_hints_seed.json) into a dictionary keyed by item id.
    /// Modeled on Services/Recipes/ItemNameSeedData.cs but never throws:
    /// null/empty/malformed input degrades to an empty dictionary so a bad
    /// or missing seed file never blocks module load (same reasoning as
    /// Module.cs's other seed-load try/catch blocks, just enforced here
    /// too since this is a pure static-data seed with no network fallback
    /// to fall back to).
    /// </summary>
    public static class AcquisitionHintService
    {
        private class AcquisitionHintEnvelope
        {
            public int SchemaVersion { get; set; }
            public string GeneratedAt { get; set; }
            public string Source { get; set; }
            public List<AcquisitionHintEntry> Hints { get; set; }
        }

        private class AcquisitionHintEntry
        {
            public int ItemId { get; set; }
            public string Hint { get; set; }
            public string Badge { get; set; }
            public string SourceUrl { get; set; }
            public string LastVerified { get; set; }
        }

        public static IReadOnlyDictionary<int, AcquisitionHint> Load(Stream stream)
        {
            var result = new Dictionary<int, AcquisitionHint>();
            var envelope = JsonSeedReader.Deserialize<AcquisitionHintEnvelope>(stream);
            if (envelope?.Hints == null)
            {
                // Null/missing/unparsable seed - never invent hint data, so
                // it just means no hints today.
                return result;
            }

            foreach (var entry in envelope.Hints)
            {
                if (entry == null)
                {
                    continue;
                }
                // Last-write-wins on duplicate item ids.
                result[entry.ItemId] = new AcquisitionHint
                {
                    ItemId = entry.ItemId,
                    Hint = entry.Hint,
                    Badge = entry.Badge,
                    SourceUrl = entry.SourceUrl,
                    LastVerified = entry.LastVerified
                };
            }
            return result;
        }
    }
}
