using System.Collections.Generic;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanStripStatusBoardTests
    {
        [Fact]
        public void Begin_MarksInFlight_ClearsPriorState()
        {
            var board = new PlanStripStatusBoard();

            board.Begin(1);
            var snapshot = board.Snapshot();

            Assert.Equal(1, snapshot.Sequence);
            Assert.True(snapshot.InFlight);
            Assert.Null(snapshot.PhaseText);
            Assert.Null(snapshot.FinalStatusText);
        }

        [Fact]
        public void UpdatePhase_CurrentGeneration_Applies()
        {
            var board = new PlanStripStatusBoard();
            board.Begin(1);

            board.UpdatePhase(1, 0, "Building recipe tree...");

            var snapshot = board.Snapshot();
            Assert.True(snapshot.InFlight);
            Assert.Equal("Building recipe tree...", snapshot.PhaseText);
        }

        [Fact]
        public void UpdatePhase_LaterOrdinalSupersedesEarlier()
        {
            var board = new PlanStripStatusBoard();
            board.Begin(1);

            board.UpdatePhase(1, 0, "Building recipe tree...");
            board.UpdatePhase(1, 1, "Fetching prices (418 items)...");

            Assert.Equal("Fetching prices (418 items)...", board.Snapshot().PhaseText);
        }

        [Fact]
        public void UpdatePhase_StaleSequence_Rejected()
        {
            // A trailing phase event from a SUPERSEDED generation must
            // never clobber the CURRENT generation's phase text - the
            // exact race StatusUpdateGuard exists for, now folded into the
            // board's own write side.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.UpdatePhase(1, 2, "Solving decisions...");
            board.Begin(2);

            board.UpdatePhase(1, 3, "Fetching item details (900 items)...");

            var snapshot = board.Snapshot();
            Assert.Equal(2, snapshot.Sequence);
            Assert.Null(snapshot.PhaseText);
        }

        [Fact]
        public void UpdatePhase_EarlierOrdinalAfterLater_Rejected()
        {
            // The out-of-order-drain race PhaseOrdinalGuard exists for: two
            // Progress<T> posts for the SAME generation racing each other
            // onto independent ThreadPool.QueueUserWorkItem calls, with the
            // earlier phase's post landing second.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.UpdatePhase(1, 4, "Building display...");

            board.UpdatePhase(1, 1, "Fetching prices (418 items)...");

            Assert.Equal("Building display...", board.Snapshot().PhaseText);
        }

        [Fact]
        public void UpdatePhase_AfterFinish_Rejected()
        {
            // A trailing phase-event tick draining after this SAME
            // generation's own Finish() already ran must not resurrect a
            // stale phase text over the already-written final status - the
            // M34-B1 #4 "already closed" case, folded into the board.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.Finish(1, "Plan generated - Aug 8, 2026 3:00 PM");

            board.UpdatePhase(1, 4, "Building display...");

            var snapshot = board.Snapshot();
            Assert.False(snapshot.InFlight);
            Assert.Null(snapshot.PhaseText);
            Assert.Equal("Plan generated - Aug 8, 2026 3:00 PM", snapshot.FinalStatusText);
        }

        [Fact]
        public void Finish_CurrentGeneration_SetsFinalStatusAndClearsInFlight()
        {
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.UpdatePhase(1, 0, "Building recipe tree...");

            board.Finish(1, "Plan generated - Aug 8, 2026 3:00 PM");

            var snapshot = board.Snapshot();
            Assert.False(snapshot.InFlight);
            Assert.Equal("Plan generated - Aug 8, 2026 3:00 PM", snapshot.FinalStatusText);
        }

        [Fact]
        public void Finish_StaleSequence_Rejected()
        {
            // A superseded generation's own completion (success/cancel/
            // failure) must never overwrite a newer generation's
            // in-progress or already-finished state.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.Begin(2);
            board.UpdatePhase(2, 0, "Building recipe tree...");

            board.Finish(1, "Error: stale generation");

            var snapshot = board.Snapshot();
            Assert.Equal(2, snapshot.Sequence);
            Assert.True(snapshot.InFlight);
            Assert.Null(snapshot.FinalStatusText);
        }

        [Fact]
        public void Finish_OnVirginBoard_Rejected()
        {
            // Gate round 2 review-fix: a raw `sequence != _sequence` check
            // alone would have accepted Finish(0, ...) on a never-Begin()'d
            // board, since a virgin board's own _sequence defaults to 0 -
            // relying entirely on the caller's myGen always being
            // ++_generateSequence (and therefore never 0) to avoid this.
            // Finish must reject any write while the board is not in
            // flight, exactly like UpdatePhase already does.
            var board = new PlanStripStatusBoard();

            board.Finish(0, "Plan generated - Aug 8, 2026 3:00 PM");

            var snapshot = board.Snapshot();
            Assert.False(snapshot.InFlight);
            Assert.Null(snapshot.FinalStatusText);
        }

        [Fact]
        public void Finish_CalledTwiceForSameGeneration_SecondCallRejected()
        {
            // Gate round 2 review-fix: a raw sequence-only check would have
            // let a second Finish() for the same, already-closed generation
            // silently overwrite the first-recorded wording (a future
            // cancel-plus-failure or retry path could plausibly complete
            // twice). The first recorded text must win.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.Finish(1, "Plan generated - Aug 8, 2026 3:00 PM");

            board.Finish(1, "Error: should not overwrite");

            var snapshot = board.Snapshot();
            Assert.False(snapshot.InFlight);
            Assert.Equal("Plan generated - Aug 8, 2026 3:00 PM", snapshot.FinalStatusText);
        }

        [Fact]
        public void FinalStatus_ReadableByFreshSnapshotConsumer_AfterViewRebuild()
        {
            // Simulates the exact round-1 gate scenario: a completion lands
            // (Finish), then a LATER, entirely separate Snapshot() call
            // (standing in for a rebuilt CraftingPlanView's Build() reading
            // the board fresh, e.g. after Plan -> Snapshot -> Plan) must
            // still see the final status text - it was never lost because
            // it was never gated on any view's own liveness in the first
            // place.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.UpdatePhase(1, 0, "Building recipe tree...");
            board.Finish(1, "Plan generated - Aug 8, 2026 3:00 PM");

            // A brand new read, unrelated to any of the calls above.
            var freshRead = board.Snapshot();

            Assert.False(freshRead.InFlight);
            Assert.Equal("Plan generated - Aug 8, 2026 3:00 PM", freshRead.FinalStatusText);
        }

        [Fact]
        public void SecondGeneration_Begin_ClearsFirstGenerationsFinishedState()
        {
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.UpdatePhase(1, 0, "Building recipe tree...");
            board.Finish(1, "Plan generated - Aug 8, 2026 3:00 PM");

            board.Begin(2);

            var snapshot = board.Snapshot();
            Assert.Equal(2, snapshot.Sequence);
            Assert.True(snapshot.InFlight);
            Assert.Null(snapshot.PhaseText);
            Assert.Null(snapshot.FinalStatusText);
        }

        // --- W3D (plan persistence): SeedRestored - review-fix, mustFix
        // (new public production surface with zero prior test coverage) ---

        [Fact]
        public void SeedRestored_OnVirginBoard_SetsSequenceZeroNotInFlightAndFinalStatusText()
        {
            var board = new PlanStripStatusBoard();

            board.SeedRestored("Generated Aug 9, 2026 10:30 AM - prices may have changed - Regenerate");

            var snapshot = board.Snapshot();
            Assert.Equal(0, snapshot.Sequence);
            Assert.False(snapshot.InFlight);
            Assert.Null(snapshot.PhaseText);
            Assert.Equal(
                "Generated Aug 9, 2026 10:30 AM - prices may have changed - Regenerate",
                snapshot.FinalStatusText);
        }

        [Fact]
        public void SeedRestored_ThenRealBegin_SupersedesSeededState()
        {
            // The whole reason SeedRestored uses sequence 0 (a value
            // CraftingPlanView's own ++_generateSequence convention can
            // never produce): a genuine first Generate must unconditionally
            // replace the restored banner, exactly like Begin already
            // supersedes any earlier generation's state.
            var board = new PlanStripStatusBoard();
            board.SeedRestored("Generated Aug 9, 2026 10:30 AM - prices may have changed - Regenerate");

            board.Begin(1);

            var snapshot = board.Snapshot();
            Assert.Equal(1, snapshot.Sequence);
            Assert.True(snapshot.InFlight);
            Assert.Null(snapshot.PhaseText);
            Assert.Null(snapshot.FinalStatusText);
        }

        [Fact]
        public void SeedRestored_WhileGenerationInFlight_Rejected()
        {
            // Review-fix (critical): Module.LoadAsync can still be awaiting
            // its own network refresh when a user opens the window and
            // clicks Generate, so a real Begin(1) can land BEFORE the
            // restore drain calls SeedRestored. Unconditionally stomping
            // _sequence back to 0 in that window would silently reject
            // every subsequent UpdatePhase(1,...)/Finish(1,...) for the
            // in-flight generation (StatusUpdateGuard sees sequence 0, not
            // 1) and freeze its spinner - the exact W3B "lost completion
            // status" bug this board exists to prevent.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.UpdatePhase(1, 0, "Building recipe tree...");

            board.SeedRestored("Generated Aug 9, 2026 10:30 AM - prices may have changed - Regenerate");

            var snapshot = board.Snapshot();
            Assert.Equal(1, snapshot.Sequence);
            Assert.True(snapshot.InFlight);
            Assert.Equal("Building recipe tree...", snapshot.PhaseText);
            Assert.Null(snapshot.FinalStatusText);
        }

        [Fact]
        public void SeedRestored_AfterGenerationAlreadyFinished_Rejected()
        {
            // Same protection as the in-flight case above, for the
            // "finished before the drain ran" ordering: a completed
            // generation's OWN final status (success/error wording) must
            // not be silently replaced by a stale on-disk restore banner.
            var board = new PlanStripStatusBoard();
            board.Begin(1);
            board.Finish(1, "Plan generated - Aug 9, 2026 10:35 AM");

            board.SeedRestored("Generated Aug 9, 2026 10:30 AM - prices may have changed - Regenerate");

            var snapshot = board.Snapshot();
            Assert.Equal(1, snapshot.Sequence);
            Assert.False(snapshot.InFlight);
            Assert.Equal("Plan generated - Aug 9, 2026 10:35 AM", snapshot.FinalStatusText);
        }

        [Fact]
        public void Snapshot_UnderConcurrentWriters_NeverThrowsAndEndsConsistent()
        {
            // Not a race-detection test (the lock makes torn reads
            // structurally impossible - see the class's own doc comment) -
            // this proves the board tolerates many real concurrent writers
            // (matching UpdatePhase/Finish's actual ThreadPool-thread
            // callers) without exceptions, and that the generation which
            // actually wins Finish() is reflected consistently afterward.
            var board = new PlanStripStatusBoard();
            board.Begin(1);

            var tasks = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                int ordinal = i % 5;
                tasks.Add(Task.Run(() => board.UpdatePhase(1, ordinal, $"Phase {ordinal}...")));
            }

            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() => board.Snapshot()));
            }

            Task.WaitAll(tasks.ToArray());
            board.Finish(1, "Plan generated - Aug 8, 2026 3:00 PM");

            var snapshot = board.Snapshot();
            Assert.Equal(1, snapshot.Sequence);
            Assert.False(snapshot.InFlight);
            Assert.Equal("Plan generated - Aug 8, 2026 3:00 PM", snapshot.FinalStatusText);
        }
    }
}
