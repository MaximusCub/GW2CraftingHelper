using System.IO;
using GW2CraftingHelper.Models;
using Newtonsoft.Json;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Serialization for W3D plan persistence - mirrors SnapshotHelpers'
    /// shape, with one deliberate difference: DeserializePersistedPlan does
    /// NOT swallow a parse/schema failure into a silent null itself. The
    /// W3D spec requires a Warn log line for a corrupt or old-schema file
    /// (unlike snapshot.json's own silent-null precedent) - so this lets
    /// the exception propagate to PlanStore.LoadLatest's single try/catch,
    /// which already logs via the same onError callback every other store
    /// uses (see PlanStore.cs).
    /// </summary>
    internal static class PlanStoreHelpers
    {
        /// <summary>
        /// Serializes a PersistedPlan to a JSON string. Returns null if
        /// plan is null.
        /// </summary>
        internal static string SerializePersistedPlan(PersistedPlan plan)
        {
            if (plan == null) return null;
            return JsonConvert.SerializeObject(plan, Formatting.Indented);
        }

        /// <summary>
        /// Deserializes a PersistedPlan from a JSON string. Returns null
        /// for null/whitespace input. Throws (does not swallow) for
        /// malformed JSON or a schema too degraded to render safely (no
        /// Result/Plan at all) - see this class's own doc comment for why.
        /// </summary>
        internal static PersistedPlan DeserializePersistedPlan(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            var plan = JsonConvert.DeserializeObject<PersistedPlan>(json);

            // A structurally valid but too-degraded-to-render object (e.g.
            // an old schema missing the fields this feature actually needs,
            // or a JSON document that happened to parse but was never a
            // real PersistedPlan at all) must not be handed back as if it
            // were usable - "never partially render" (W3D spec item 4).
            if (plan?.Result?.Plan == null)
            {
                throw new InvalidDataException(
                    "Persisted plan is missing Result/Plan - corrupt or old-schema file.");
            }

            return plan;
        }
    }
}
