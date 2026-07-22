using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
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
            // KNOWN-ISSUES 31a-F1: mirrors SnapshotEpochGuardTests, but
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

        [Fact]
        public void ClearDuringInFlightCommit_BlocksUntilCommitFinishes_ThenSeesBumpedEpoch()
        {
            // KNOWN-ISSUES 31a-F1 audit-of-fix: the original check-then-act
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

            var clearTask = Task.Run(() =>
            {
                gate.Clear(() => events.Add("clear"));
            });

            // Clear must be blocked behind the commit's lock - it cannot
            // have run (and therefore cannot have bumped the epoch or
            // touched cleared-state) while the commit is still in flight.
            Assert.False(clearTask.Wait(TimeSpan.FromMilliseconds(200)));

            releaseCommit.Set();

            Assert.True(commitTask.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(clearTask.Wait(TimeSpan.FromSeconds(5)));

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
            var commitTask = Task.Run(() =>
            {
                committed = gate.TryCommit(myEpoch, () => events.Add("commit"));
            });

            Assert.False(commitTask.Wait(TimeSpan.FromMilliseconds(200)));

            releaseClear.Set();

            Assert.True(clearTask.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(commitTask.Wait(TimeSpan.FromSeconds(5)));

            Assert.False(committed);
            Assert.Equal(new[] { "clear-start", "clear-end" }, events);
        }
    }
}
