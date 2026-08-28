using TaimisToolbench.Models;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Models
{
    // The plan history index's half of the compatibility contract
    // (docs/ARCHITECTURE.md section 12). PersistedPlan splits a document
    // into two layers and keeps the cheaper one; an index cannot, because
    // it is a list of independent rows - so its guarantee is instead that
    // the row graph stays additive-only forever, which is what makes a
    // file stamped at ANY shipped version readable by every later build.
    //
    // These are the teeth behind that sentence. The snapshot names the
    // property that moved; the hash lives on PlanHistoryIndex, one line
    // from both version constants, so a shape change cannot be absorbed by
    // editing the snapshot alone.
    public class PlanHistorySchemaMemberSetTests
    {
        private const string SnapshotRelativePath = "tests/shared/plan_history_schema.txt";

        [Fact]
        public void CurrentSchemaVersion_MatchesExpectedValue()
        {
            Assert.Equal(1, PlanHistoryIndex.CurrentSchemaVersion);
        }

        [Fact]
        public void MinimumReadableSchemaVersion_IsStillOne()
        {
            // The one assertion in the suite that exists to be argued with.
            // Raising this constant is the only act that silently empties a
            // user's plan history - up to 200 rows - so it may not happen
            // as a side effect of a bump, a refactor, or a merge. Changing
            // it means changing this line, in the same commit, with a
            // reason in the message; the golden fixtures for every version
            // below the new floor will fail alongside it, and they are not
            // deletable (see tests/shared/plan_fixtures/README.md).
            Assert.True(
                PlanHistoryIndex.MinimumReadableSchemaVersion == 1,
                "PlanHistoryIndex.MinimumReadableSchemaVersion is "
                + PlanHistoryIndex.MinimumReadableSchemaVersion
                + ", not 1. Every user running a build that wrote a version below "
                + "that floor loses their entire plan history on upgrade, silently. "
                + "If the row graph genuinely cannot be read any more, say so in the "
                + "commit message and change this line deliberately - but first check "
                + "that the change could not have been made additively, which is what "
                + "PlanHistoryIndex.SchemaShapeHash is there to make you notice.");
        }

        [Fact]
        public void PlanHistoryGraph_PublicMemberSignature_MatchesSnapshot()
        {
            string[] actual = ModelGraphSignatures.For(typeof(PlanHistoryIndex));
            string[] expected = ModelGraphSignatures.ReadSnapshot(SnapshotRelativePath);

            if (ModelGraphSignatures.ShouldUpdateSnapshots())
            {
                ModelGraphSignatures.WriteSnapshot(SnapshotRelativePath, actual);
                expected = actual;
            }

            Assert.Equal(string.Join("\n", expected), string.Join("\n", actual));
        }

        [Fact]
        public void PlanHistoryGraph_ShapeHash_MatchesTheOneStoredBesideTheVersion()
        {
            string actualHash = ModelGraphSignatures.Sha256(
                string.Join("\n", ModelGraphSignatures.For(typeof(PlanHistoryIndex))));

            Assert.True(
                actualHash == PlanHistoryIndex.SchemaShapeHash,
                "The plan history index graph's shape changed.\n"
                + "  expected (PlanHistoryIndex.SchemaShapeHash): " + PlanHistoryIndex.SchemaShapeHash + "\n"
                + "  actual:                                     " + actualHash + "\n"
                + "Run the suite with UPDATE_SNAPSHOTS=1 to rewrite "
                + SnapshotRelativePath + ", review that text diff, then set "
                + "SchemaShapeHash to the actual value above. An ADDITION needs "
                + "nothing else - Newtonsoft leaves an absent member at its default, "
                + "so every existing row still loads. A rename, removal or retype "
                + "does not have that property, and there is no version bump that "
                + "makes one safe: it costs every row written before it. Find the "
                + "additive route.");
        }
    }
}
