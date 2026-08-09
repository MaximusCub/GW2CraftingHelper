using System.IO;
using GW2CraftingHelper.Models;
using Newtonsoft.Json;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Serialization for W3D plan persistence - mirrors SnapshotHelpers'
    /// shape, with two deliberate differences: (1) DeserializePersistedPlan
    /// does NOT swallow a parse/schema failure into a silent null itself.
    /// The W3D spec requires a Warn log line for a corrupt or old-schema
    /// file (unlike snapshot.json's own silent-null precedent) - so this
    /// lets the exception propagate to PlanStore.LoadLatest's single
    /// try/catch, which already logs via the same onError callback every
    /// other store uses (see PlanStore.cs). (2) Compact (not Indented)
    /// formatting - see SerializePersistedPlan's own doc comment.
    /// </summary>
    internal static class PlanStoreHelpers
    {
        /// <summary>
        /// Serializes a PersistedPlan to a JSON string. Returns null if
        /// plan is null.
        /// <para>
        /// Review-fix (W3D adversarial review, mustFix): compact
        /// (Formatting.None), NOT Indented like SnapshotHelpers'/
        /// StatusStore's own precedent - a PersistedPlan carries the FULL
        /// SolveContext (the whole reduced crafting tree, every priced
        /// item, every vendor offer), so indentation is not a flat per-file
        /// constant the way it is for snapshot.json; a synthetic
        /// 364-node/400-priced-item tree measured 527 KB indented vs. 216
        /// KB compact. This runs on every override-resolve pill click, not
        /// just once per Generate (see Module.PersistResolvedPlanInBackground),
        /// so the readability Indented buys elsewhere is not worth doubling
        /// the bytes serialized/written on an interactive path.
        /// </para>
        /// </summary>
        internal static string SerializePersistedPlan(PersistedPlan plan)
        {
            if (plan == null) return null;
            return JsonConvert.SerializeObject(plan, Formatting.None);
        }

        /// <summary>
        /// Deserializes a PersistedPlan from a JSON string. Returns null
        /// for null/whitespace input. Throws (does not swallow) for
        /// malformed JSON, a schema too degraded to render safely (no
        /// Result/Plan at all, or a SchemaVersion mismatch - see
        /// PersistedPlan.CurrentSchemaVersion's own doc comment), or a
        /// structurally-valid-but-degraded object graph (round 4 review-fix,
        /// critical - see PlanStructuralValidator's own doc comment) - see
        /// this class's own doc comment for why.
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
            // The SchemaVersion check (review-fix, mustFix) is what makes
            // this actually enforceable going forward: a future member
            // rename/removal elsewhere on this graph would otherwise still
            // pass the structural Result/Plan check below while coming back
            // silently defaulted.
            if (plan?.Result?.Plan == null || plan.SchemaVersion != PersistedPlan.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    "Persisted plan is missing Result/Plan or has an unsupported SchemaVersion - corrupt or old-schema file.");
            }

            // Round 4 review-fix (critical): a single, class-level walk of
            // the ENTIRE restored object graph - the display tree, the
            // solve tree, and every collection the restore-render and local
            // override re-solve paths dereference without a null guard -
            // rather than relying on individual render call sites to each
            // guard themselves (rounds 1-3 proved that approach never
            // converges: every fix revealed exactly one more unguarded
            // site). See PlanStructuralValidator's own doc comment for the
            // full inventory and the exact crash sites this closes.
            if (!PlanStructuralValidator.IsStructurallyValid(plan, out string invalidReason))
            {
                throw new InvalidDataException(
                    $"Persisted plan failed structural validation ({invalidReason}) - corrupt or degraded file.");
            }

            return plan;
        }
    }
}
