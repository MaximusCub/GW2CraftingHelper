using System;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // The Ranker's rebuild-on-view rule, driven through the real object in
    // the sequences the tab actually produces. The cost being rationed is
    // two plan solves per row over up to RankerWatchlistLimits.MaxEntries
    // rows, so "how many rebuilds does this sequence ask for" is the whole
    // question these answer.
    public class RankerSnapshotWatchTests
    {
        private static readonly DateTime S1 = new DateTime(2026, 8, 29, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime S2 = S1.AddMinutes(10);
        private static readonly DateTime S3 = S1.AddMinutes(20);
        private static readonly DateTime S4 = S1.AddMinutes(30);

        [Fact]
        public void TheFirstSnapshotOfASession_AsksForNothing()
        {
            var watch = new RankerSnapshotWatch();

            // A table nobody has calculated yet: the stamp is new, so the
            // (empty) answer sets are dropped, but no run is owed - the
            // first one stays the user's to start.
            Assert.True(watch.Observe(S1, hadResults: false));
            Assert.False(watch.RebuildPending);
            Assert.False(watch.TryTakeRebuild(isRefreshing: false, hasEntries: true));
        }

        [Fact]
        public void AnUnchangedSnapshot_IsNotAChange()
        {
            var watch = new RankerSnapshotWatch();
            watch.MeasuredAgainst(S1);

            // The per-tick case, and the reason the poll is cheap.
            Assert.False(watch.Observe(S1, hadResults: true));
            Assert.False(watch.RebuildPending);
        }

        [Fact]
        public void AChangeAfterResultsExist_AsksForOneRebuild()
        {
            var watch = new RankerSnapshotWatch();
            watch.MeasuredAgainst(S1);

            Assert.True(watch.Observe(S2, hadResults: true));
            Assert.True(watch.RebuildPending);
            Assert.True(watch.TryTakeRebuild(isRefreshing: false, hasEntries: true));

            // Taken once. The next tick must not ask again.
            Assert.False(watch.RebuildPending);
            Assert.False(watch.TryTakeRebuild(isRefreshing: false, hasEntries: true));
        }

        [Fact]
        public void ABurstDuringARun_CollapsesToOneRebuildAfterIt()
        {
            var watch = new RankerSnapshotWatch();
            watch.MeasuredAgainst(S1);

            // The run for S2 starts...
            Assert.True(watch.Observe(S2, hadResults: true));
            Assert.True(watch.TryTakeRebuild(isRefreshing: false, hasEntries: true));
            watch.MeasuredAgainst(S2);

            // ...and S3 and S4 land while it is still in flight. Each drops
            // the answer sets; neither can start anything. The second
            // Observe reports hadResults false, because the first already
            // emptied the cache - the request has to survive that.
            Assert.True(watch.Observe(S3, hadResults: true));
            Assert.False(watch.TryTakeRebuild(isRefreshing: true, hasEntries: true));
            Assert.True(watch.Observe(S4, hadResults: false));
            Assert.False(watch.TryTakeRebuild(isRefreshing: true, hasEntries: true));

            // The run ends: ONE rebuild, against the newest stamp.
            Assert.True(watch.RebuildPending);
            Assert.True(watch.TryTakeRebuild(isRefreshing: false, hasEntries: true));
            Assert.Equal(S4, watch.Stamp);
            Assert.False(watch.TryTakeRebuild(isRefreshing: false, hasEntries: true));
        }

        [Fact]
        public void AChangeWhileTheTabIsHidden_IsSpentWhenItIsNextShown()
        {
            var watch = new RankerSnapshotWatch();
            watch.MeasuredAgainst(S1);

            // Nothing polls while the tab is hidden; the change is seen on
            // the rebuild the next visit runs, and starts a run then.
            Assert.True(watch.Observe(S2, hadResults: true));
            Assert.True(watch.RebuildPending);
            Assert.True(watch.TryTakeRebuild(isRefreshing: false, hasEntries: true));
        }

        [Fact]
        public void AnEmptyWatchlist_ConsumesTheRequestAndStartsNothing()
        {
            var watch = new RankerSnapshotWatch();
            watch.MeasuredAgainst(S1);
            watch.Observe(S2, hadResults: true);

            Assert.False(watch.TryTakeRebuild(isRefreshing: false, hasEntries: false));

            // Consumed, not parked: there are no rows to measure, so adding
            // one later must not inherit a rebuild owed to a list it was
            // never in.
            Assert.False(watch.RebuildPending);
        }

        [Fact]
        public void AStartingRun_DoesNotSwallowARequestThatOutlivesIt()
        {
            var watch = new RankerSnapshotWatch();
            watch.MeasuredAgainst(S1);
            Assert.True(watch.Observe(S2, hadResults: true));

            // MeasuredAgainst records what a run READ, which is not an
            // answer to a request raised by a snapshot it did not read.
            watch.MeasuredAgainst(S1);
            Assert.True(watch.RebuildPending);
        }

        [Fact]
        public void NoSnapshotAtAll_IsAStampLikeAnyOther()
        {
            var watch = new RankerSnapshotWatch();
            watch.MeasuredAgainst(S1);

            // Clear Cache drops the snapshot outright. That invalidates the
            // numbers exactly as a newer one does, and coming back is
            // another change rather than a return to where it started.
            Assert.True(watch.Observe(null, hadResults: true));
            Assert.True(watch.TryTakeRebuild(isRefreshing: false, hasEntries: true));
            Assert.True(watch.Observe(S3, hadResults: true));
            Assert.True(watch.TryTakeRebuild(isRefreshing: false, hasEntries: true));
        }
    }
}
