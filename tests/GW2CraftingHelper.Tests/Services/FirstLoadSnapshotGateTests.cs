using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The maintainer's report: "when you load for the first time we
    /// should trigger a snapshot immediately ... otherwise everything is
    /// empty and requires either waiting 10mins OR clicking manually".
    /// The rule Module.Update applies, exercised here away from Blish's
    /// timer.
    /// </summary>
    public class FirstLoadSnapshotGateTests
    {
        private static bool ShouldRefresh(
            bool hasCachedSnapshot = false,
            bool apiReady = true,
            bool alreadyAttempted = false,
            bool refreshInProgress = false,
            bool inFailureBackoff = false)
        {
            return FirstLoadSnapshotGate.ShouldRefreshNow(
                hasCachedSnapshot, apiReady, alreadyAttempted, refreshInProgress, inFailureBackoff);
        }

        [Fact]
        public void NothingCachedAndApiReady_Fetches()
        {
            Assert.True(ShouldRefresh());
        }

        [Fact]
        public void ACachedSnapshotIsLeftToTheIntervalTimer()
        {
            // The interval refresh already owns an existing snapshot's
            // ageing; this gate exists only for having none.
            Assert.False(ShouldRefresh(hasCachedSnapshot: true));
        }

        [Fact]
        public void NoApiKeyYet_WaitsWithoutSpendingTheOneShot()
        {
            // The whole reason the load-time attempt misses: Blish grants
            // the subtoken after the module loads. The gate must keep
            // saying "not yet" and then fire the moment it can.
            Assert.False(ShouldRefresh(apiReady: false));
            Assert.True(ShouldRefresh(apiReady: true));
        }

        [Fact]
        public void OnceAttempted_NeverFiresAgain()
        {
            // No loop, on success or failure: the caller records the
            // attempt as it fires.
            Assert.False(ShouldRefresh(alreadyAttempted: true));
        }

        [Fact]
        public void ARefreshAlreadyRunning_IsNotDuplicated()
        {
            Assert.False(ShouldRefresh(refreshInProgress: true));
        }

        [Fact]
        public void InsideTheFailureBackoff_WaitsWithoutSpendingTheOneShot()
        {
            // Module.LoadAsync's own attempt can have failed moments ago
            // and opened the shared 60s backoff. Firing into it would
            // burn the one shot on a call that returns immediately, so the
            // gate holds it and fires once the window closes.
            Assert.False(ShouldRefresh(inFailureBackoff: true));
            Assert.True(ShouldRefresh(inFailureBackoff: false));
        }

        [Fact]
        public void EveryBlockerHoldsIndependently()
        {
            Assert.False(ShouldRefresh(
                hasCachedSnapshot: true, apiReady: false,
                alreadyAttempted: true, refreshInProgress: true, inFailureBackoff: true));
        }
    }
}
