using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class SnapshotCommitGateTests
    {
        [Fact]
        public void TryCommit_UnchangedEpoch_RunsCommitAndReturnsTrue()
        {
            var gate = new SnapshotCommitGate();
            int myEpoch = gate.Epoch;
            bool ran = false;

            bool committed = gate.TryCommit(myEpoch, () => ran = true);

            Assert.True(committed);
            Assert.True(ran);
        }

        [Fact]
        public void Clear_BumpsEpoch_SoAStaleTryCommitIsSkipped()
        {
            // KNOWN-ISSUES #31/31a-F1: mirrors SnapshotEpochGuardTests, but
            // through the gate's own bump (Clear) rather than a bare int,
            // proving the gate's epoch actually advances.
            var gate = new SnapshotCommitGate();
            int myEpoch = gate.Epoch;

            gate.Clear(() => { });

            bool ran = false;
            bool committed = gate.TryCommit(myEpoch, () => ran = true);

            Assert.False(committed);
            Assert.False(ran);
        }

        [Fact]
        public void Clear_RunsItsCallbackEvenWithNoPriorFetch()
        {
            var gate = new SnapshotCommitGate();
            bool cleared = false;

            gate.Clear(() => cleared = true);

            Assert.True(cleared);
        }

        [Fact]
        public void TryCommit_NullCommit_Throws()
        {
            var gate = new SnapshotCommitGate();
            Assert.Throws<ArgumentNullException>(() => gate.TryCommit(gate.Epoch, null));
        }

        [Fact]
        public void Clear_NullCallback_Throws()
        {
            var gate = new SnapshotCommitGate();
            Assert.Throws<ArgumentNullException>(() => gate.Clear(null));
        }

        // The two lock-ordering tests below prove that one operation is
        // PARKED on the gate's lock while the other holds it. They used to
        // do that with Assert.False(task.Wait(200ms)), which is a negative
        // timing assertion: it spends 200ms of every run, and on a runner
        // slow enough to not have scheduled the second task yet it passes
        // even with the lock deleted - it can only ever weaken, never fail
        // loudly. They now run the second operation on a real Thread and
        // wait for it to report WaitSleepJoin, which is what a thread
        // blocked on Monitor.Enter reports and nothing else here can
        // produce. That is a positive observation, it is immediate, and
        // deleting the lock makes it impossible.
        //
        // xUnit1031 ("await instead of blocking") cannot apply: the parked
        // thread IS the assertion, and awaiting it would wait for the
        // completion the test exists to prove has not happened yet.
#pragma warning disable xUnit1031
        [Fact]
        public void ClearDuringInFlightCommit_BlocksUntilCommitFinishes_ThenSeesBumpedEpoch()
        {
            // KNOWN-ISSUES #31/31a-F1 audit-of-fix: the original check-then-act
            // (a bare volatile epoch re-check followed by several
            // unsynchronized field writes) left a window where a Clear
            // Cache landing between the check and the writes could
            // resurrect just-cleared data, or interleave with the clear's
            // own field resets. This proves the two are now mutually
            // exclusive: a TryCommit already inside its critical section
            // runs to completion before a concurrent Clear can start (and
            // therefore before Clear's epoch bump is visible to anything).
            var gate = new SnapshotCommitGate();
            int myEpoch = gate.Epoch;

            var commitEntered = new ManualResetEventSlim(false);
            var releaseCommit = new ManualResetEventSlim(false);
            var events = new List<string>();

            bool committed = false;
            var commitTask = Task.Run(() =>
            {
                committed = gate.TryCommit(myEpoch, () =>
                {
                    events.Add("commit-start");
                    commitEntered.Set();
                    // Hold the lock open long enough for a concurrent Clear
                    // to prove it blocks rather than interleaving.
                    releaseCommit.Wait();
                    events.Add("commit-end");
                });
            });

            Assert.True(commitEntered.Wait(TimeSpan.FromSeconds(5)));

            var clearReachedTheCall = new ManualResetEventSlim(false);
            var clearBodyRan = new ManualResetEventSlim(false);
            var clearThread = StartBlocking(() =>
            {
                clearReachedTheCall.Set();
                gate.Clear(() =>
                {
                    events.Add("clear");
                    clearBodyRan.Set();
                });
            });

            Assert.True(clearReachedTheCall.Wait(TimeSpan.FromSeconds(5)));
            AssertParkedOnTheLock(clearThread, clearBodyRan, "Clear");

            releaseCommit.Set();

            Assert.True(commitTask.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(clearThread.Join(TimeSpan.FromSeconds(5)));

            Assert.True(committed);
            Assert.Equal(new[] { "commit-start", "commit-end", "clear" }, events);
            Assert.NotEqual(myEpoch, gate.Epoch);
        }

        [Fact]
        public void ClearDuringInFlightClear_BlocksSubsequentTryCommit_NoResurrection()
        {
            // The other interleaving direction: Clear already holds the
            // lock (epoch bump + its own field-reset callback in
            // progress) when a fetch's post-await TryCommit call for an
            // epoch captured before the clear arrives. It must block, then
            // see the bumped epoch and discard - never resurrecting the
            // data the clear is in the middle of wiping.
            var gate = new SnapshotCommitGate();
            int myEpoch = gate.Epoch;

            var clearEntered = new ManualResetEventSlim(false);
            var releaseClear = new ManualResetEventSlim(false);
            var events = new List<string>();

            var clearTask = Task.Run(() =>
            {
                gate.Clear(() =>
                {
                    events.Add("clear-start");
                    clearEntered.Set();
                    releaseClear.Wait();
                    events.Add("clear-end");
                });
            });

            Assert.True(clearEntered.Wait(TimeSpan.FromSeconds(5)));

            bool committed = false;
            var commitReachedTheCall = new ManualResetEventSlim(false);
            var commitBodyRan = new ManualResetEventSlim(false);
            var commitThread = StartBlocking(() =>
            {
                commitReachedTheCall.Set();
                committed = gate.TryCommit(myEpoch, () =>
                {
                    events.Add("commit");
                    commitBodyRan.Set();
                });
            });

            Assert.True(commitReachedTheCall.Wait(TimeSpan.FromSeconds(5)));
            AssertParkedOnTheLock(commitThread, commitBodyRan, "TryCommit");

            releaseClear.Set();

            Assert.True(clearTask.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(commitThread.Join(TimeSpan.FromSeconds(5)));

            Assert.False(committed);
            Assert.Equal(new[] { "clear-start", "clear-end" }, events);
        }
#pragma warning restore xUnit1031

        private static Thread StartBlocking(Action body)
        {
            var thread = new Thread(new ThreadStart(body));
            thread.IsBackground = true;
            thread.Start();
            return thread;
        }

        /// <summary>
        /// Spins until <paramref name="thread"/> reports WaitSleepJoin - the
        /// state a thread blocked on Monitor.Enter is in, and the only thing
        /// it can be blocked on here. If the gate's lock were removed the
        /// thread would run straight through instead, which the in-loop
        /// check on <paramref name="bodyRan"/> catches on its first pass.
        /// The elapsed ceiling is a hang guard only: it turns a genuine
        /// deadlock into a failing test rather than a stalled run, and no
        /// verdict here depends on how fast the runner is.
        /// </summary>
        private static void AssertParkedOnTheLock(
            Thread thread, ManualResetEventSlim bodyRan, string what)
        {
            var guard = Stopwatch.StartNew();
            while ((thread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) == 0)
            {
                Assert.False(
                    bodyRan.IsSet,
                    what + " ran while the other operation held the gate's lock");
                Assert.True(
                    guard.ElapsedMilliseconds < 5000,
                    what + " never parked on the gate's lock (state "
                        + thread.ThreadState + ")");
                Thread.Yield();
            }

            Assert.False(
                bodyRan.IsSet,
                what + " ran while the other operation held the gate's lock");
        }
    }
}
