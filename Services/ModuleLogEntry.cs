using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// One line of module log history: written by ModuleLog.Write, held in
    /// its in-memory ring buffer, and optionally appended to the on-disk
    /// module_log.jsonl file via ModuleLogStore. Deliberately Blish-free (no
    /// Blish_HUD/Gw2Sharp/Microsoft.Xna usings - only this file's own
    /// namespace and Newtonsoft.Json, already used elsewhere in Services)
    /// so ModuleLog/ModuleLogStore stay independently testable and the "no
    /// Blish HUD in tests" repo invariant is trivially satisfiable - see
    /// dev/proposals/d2-log-system.md Section 9.
    /// <para>
    /// Property names on the wire are short (t/lvl/tag/msg) deliberately:
    /// this file is written far more often than snapshot.json/status.txt,
    /// so every byte compounds across the retention window (d2 Section
    /// 4.1). Level is written as its enum NAME (not ordinal) so a hand
    /// look at the raw file is legible without a lookup table.
    /// </para>
    /// </summary>
    public class ModuleLogEntry
    {
        [JsonProperty("t")]
        public DateTime TimestampUtc { get; set; }

        [JsonProperty("lvl")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ModuleLogLevel Level { get; set; }

        // Free-form, not an enum - e.g. "scrolldiag", "snapshot", "plan".
        // Nullable/blank is tolerated everywhere this is read; keeping it a
        // plain string means a new call site never needs a schema change
        // (d2 Section 4.1).
        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("msg")]
        public string Message { get; set; }
    }
}
