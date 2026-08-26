using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class SnapshotEpochGuardTests
    {
        [Fact]
        public void UnchangedEpoch_Commits()
        {
            Assert.True(SnapshotEpochGuard.ShouldCommit(myEpoch: 3, currentEpoch: 3));
        }

        [Fact]
        public void EpochBumpedByClearCache_Discarded()
        {
            // KNOWN-ISSUES #31/31a-F1: ClearCache bumped the shared epoch while
            // this fetch was still in flight - its result must never
            // commit, regardless of how far ahead the current epoch is.
            Assert.False(SnapshotEpochGuard.ShouldCommit(myEpoch: 3, currentEpoch: 4));
            Assert.False(SnapshotEpochGuard.ShouldCommit(myEpoch: 3, currentEpoch: 9));
        }

        [Fact]
        public void EpochBehindCurrent_NeverIncorrectlyCommits()
        {
            // Defensive: a captured epoch can never be numerically ahead of
            // the live counter in real usage, but the guard must not treat
            // any mismatch as a false commit either way.
            Assert.False(SnapshotEpochGuard.ShouldCommit(myEpoch: 5, currentEpoch: 3));
        }
    }
}
