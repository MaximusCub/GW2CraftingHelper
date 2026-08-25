using System;
using System.IO;
using GW2CraftingHelper.Models;
using Newtonsoft.Json;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Serialization for plan persistence - mirrors SnapshotHelpers'
    /// shape, with two deliberate differences: (1) DeserializePersistedPlan
    /// does NOT swallow a parse/schema failure into a silent null itself.
    /// An unreadable file requires a log line (unlike snapshot.json's own
    /// silent-null precedent) - so this
    /// lets the exception propagate to PlanStore.LoadLatest's single
    /// try/catch, which reports a corrupt file at Warn via the same onError
    /// callback every other store uses and drift from an older SHIPPED
    /// schema version at Info via its own onInfo callback (see PlanStore.cs).
    /// (2) Compact (not Indented)
    /// formatting - see SerializePersistedPlan's own doc comment.
    /// </summary>
    internal static class PlanStoreHelpers
    {
        /// <summary>
        /// Serializes a PersistedPlan to a JSON string. Returns null if
        /// plan is null.
        /// <para>
        /// compact
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
        /// malformed JSON, a document too degraded to render safely (no
        /// Result/Plan at all), a SchemaVersion mismatch (as
        /// PlanSchemaVersionMismatchException for an older shipped version,
        /// as InvalidDataException for an unrecorded (0) or newer-than-this-
        /// build one - see PersistedPlan.CurrentSchemaVersion's own doc
        /// comment), or a
        /// structurally-valid-but-degraded object graph (round 4 review-fix,
        /// critical - see PlanStructuralValidator's own doc comment) - see
        /// this class's own doc comment for why.
        /// </summary>
        internal static PersistedPlan DeserializePersistedPlan(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            // MaxDepth raised from Newtonsoft's default 64: a persisted
            // +24 Agony Infusion plan (23 recipe levels, the deepest chain
            // in the game per docs/research/minimum-window-width.md) nests
            // ~3 JSON levels per tree node and failed to load with the
            // default - saving is unaffected because Json.NET only
            // enforces MaxDepth on read. 512 covers the validator's
            // 200-domain-level bound at ~3 JSON levels each.
            var plan = JsonConvert.DeserializeObject<PersistedPlan>(
                json, new JsonSerializerSettings { MaxDepth = 512 });

            // A structurally valid but too-degraded-to-render object (e.g.
            // a JSON document that happened to parse but was never a real
            // PersistedPlan at all) must not be handed back as if it were
            // usable - "never partially render".
            //
            // Damage and drift are two different verdicts and must never
            // share one message: they were merged until 2026-08-23, when a
            // routine rejection of an 8-day-old file logged as a possible
            // corruption and took a forensic reconstruction to explain.
            // Result/Plan has existed since schema 1, so a document without
            // it was never a valid plan file at ANY version - that is
            // damage, and only damage may say "corrupt". Checked first for
            // exactly that reason.
            if (plan?.Result?.Plan == null)
            {
                throw new InvalidDataException(
                    "Persisted plan is missing Result/Plan - corrupt file.");
            }

            // Drift: a recognizable plan file written by another build at a
            // version this one actually shipped. Expected, benign, and
            // self-healing on the next Generate, so it carries its own
            // exception type - PlanStore.LoadLatest reports it at Info, not
            // Warn. The check is also what makes the tolerance contract
            // enforceable going forward: a future member rename/removal
            // elsewhere on this graph would otherwise pass the structural
            // check above while coming back silently defaulted.
            //
            // Two versions are deliberately NOT drift and take the error
            // channel instead, each with its own wording:
            //   0 - never a shipped version. It is what Newtonsoft leaves
            //       when the field is absent (PersistedPlan.SchemaVersion
            //       carries no initializer precisely so absence stays
            //       detectable), so it means either a file older than the
            //       version gate itself or a construction site that forgot
            //       to stamp it - a module defect whose every save is
            //       silently unrestorable. Info would bury that.
            //   anything else - a version this build never shipped, i.e.
            //       above current (a newer build wrote it, and this one
            //       cannot know what is in it) or negative (only a
            //       hand-edited file reaches that).
            int observed = plan.SchemaVersion;
            int expected = PersistedPlan.CurrentSchemaVersion;
            if (observed >= 1 && observed < expected)
            {
                throw new PlanSchemaVersionMismatchException(observed, expected);
            }

            if (observed != expected)
            {
                throw new InvalidDataException(observed == 0
                    ? $"Persisted plan records no schema version at all, this build expects {expected} - it predates the version gate, or whatever wrote it never set PersistedPlan.SchemaVersion."
                    : $"Persisted plan is schema {observed}, which this build ({expected}) never shipped - written by a newer build, or the field was damaged.");
            }

            // a single, class-level walk of
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

            MarkPlanRoots(plan.Result);
            return plan;
        }

        /// <summary>
        /// Re-derives CraftingTreeNode.IsPlanRoot, which is internal and
        /// therefore never serialized (see the member's own comment for
        /// why it is kept off the persisted graph). Restore is the one path
        /// that produces a CraftingPlanResult without going through
        /// CraftingTreeBuilder.BuildTree, so without this a restored plan's
        /// root rows would offer the IGNORE pill the flag exists to
        /// suppress until the next Generate or override re-solve. The roots
        /// are exactly what BuildTree was called with: CraftingTree for a
        /// single-item plan, every MultiItemRoots entry for a batch.
        /// </summary>
        private static void MarkPlanRoots(CraftingPlanResult result)
        {
            if (result.CraftingTree != null)
            {
                result.CraftingTree.IsPlanRoot = true;
            }

            if (result.MultiItemRoots == null)
            {
                return;
            }

            foreach (var root in result.MultiItemRoots)
            {
                if (root != null)
                {
                    root.IsPlanRoot = true;
                }
            }
        }
    }

    /// <summary>
    /// A recognizable plan file written by a build at an OLDER shipped
    /// PersistedPlan.CurrentSchemaVersion (an unrecorded or newer version is
    /// not this, and does not come here - see the throw site's own comment
    /// in DeserializePersistedPlan). Kept distinct from the plain
    /// InvalidDataException the corrupt/degraded paths throw so the ONE
    /// caller that can tell a user anything - PlanStore.LoadLatest - can
    /// report it as the routine, self-healing event it is (Info, no
    /// "corrupt") rather than as damage. Derives straight from Exception
    /// only because InvalidDataException is sealed on .NET Framework; every
    /// other handler on this path is a catch-all, so it still degrades to
    /// the same null + one log line. The message names both versions,
    /// which is what the 2026-08-23 incident had to reconstruct from commit
    /// timestamps.
    /// </summary>
    internal sealed class PlanSchemaVersionMismatchException : Exception
    {
        internal PlanSchemaVersionMismatchException(int observedVersion, int expectedVersion)
            : base($"Saved plan file is schema {observedVersion}, this build expects {expectedVersion} - starting fresh.")
        {
        }
    }
}
