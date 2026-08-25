using System;
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

        private static readonly TimeSpan Frame = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);

        [Fact]
        public void ShouldCheckNow_HoldsUntilAFullIntervalHasAccumulated()
        {
            var interval = TimeSpan.FromSeconds(2);
            var carried = TimeSpan.Zero;

            Assert.False(FirstLoadSnapshotGate.ShouldCheckNow(carried, TimeSpan.FromSeconds(0.5), interval, out carried));
            Assert.Equal(TimeSpan.FromSeconds(0.5), carried);

            Assert.False(FirstLoadSnapshotGate.ShouldCheckNow(carried, TimeSpan.FromSeconds(1.4), interval, out carried));
            Assert.Equal(TimeSpan.FromSeconds(1.9), carried);

            Assert.True(FirstLoadSnapshotGate.ShouldCheckNow(carried, TimeSpan.FromSeconds(0.1), interval, out carried));
            Assert.Equal(TimeSpan.Zero, carried);
        }

        [Fact]
        public void ShouldCheckNow_KeepsANoApiKeySessionOffThePerFrameProbe()
        {
            // The regression this exists for: with no API key the shot is
            // never spent, so nothing else stops Module.Update re-reading
            // the gate's live inputs (a permission probe, a clock read) on
            // every single frame for the whole session.
            var interval = TimeSpan.FromSeconds(2);
            var carried = interval;
            int checks = 0;

            // One minute at 60 fps.
            for (int frame = 0; frame < 3600; frame++)
            {
                if (FirstLoadSnapshotGate.ShouldCheckNow(carried, Frame, interval, out carried))
                {
                    checks++;
                    Assert.False(ShouldRefresh(apiReady: false));
                }
            }

            // 3600 frames, one check per two seconds of them.
            Assert.InRange(checks, 25, 35);
        }

        [Fact]
        public void ShouldCheckNow_FiresOnTheFirstTickWhenSeededFull()
        {
            // How Module seeds the accumulator, and how Clear Cache resets
            // it: a re-armed shot must not wait out an interval first.
            var interval = TimeSpan.FromSeconds(2);
            Assert.True(FirstLoadSnapshotGate.ShouldCheckNow(interval, Frame, interval, out TimeSpan carried));
            Assert.Equal(TimeSpan.Zero, carried);
        }

        [Fact]
        public void ShouldCheckNow_SurvivesAWildFrameDelta()
        {
            var interval = TimeSpan.FromSeconds(2);

            // A resumed game can hand back an enormous delta - fire, do
            // not overflow the accumulator.
            Assert.True(FirstLoadSnapshotGate.ShouldCheckNow(TimeSpan.Zero, TimeSpan.MaxValue, interval, out TimeSpan carried));
            Assert.Equal(TimeSpan.Zero, carried);

            // A negative one must not walk the accumulator backwards into
            // never firing again.
            Assert.False(FirstLoadSnapshotGate.ShouldCheckNow(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(-5), interval, out carried));
            Assert.Equal(TimeSpan.FromSeconds(1), carried);
        }
    }
}
