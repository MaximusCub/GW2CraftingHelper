using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // KNOWN-ISSUES api-degradation F1: Gw2AccountSnapshotService itself is
    // Blish/Gw2Sharp-coupled (constructed from Blish_HUD.Modules.Managers.
    // Gw2ApiManager) and cannot be exercised here per the repo's "tests
    // must never reference Blish HUD/Gw2Sharp" invariant - there is no fake
    // seam to build without violating that rule. This exercises the pure,
    // Blish-free piece that carries the fix's actual decision logic: the
    // exception FetchSnapshotAsync now throws on ANY source failure
    // (partial or total) instead of returning a holed/empty snapshot.
    //
    // The resulting non-persistence behavior itself is verified by
    // construction, not by a test double: Module.FetchAndSaveSnapshotAsync
    // only reaches its _currentSnapshot/_snapshotStore.Save commit lines
    // AFTER `await _snapshotService.FetchSnapshotAsync(ct)` returns
    // successfully - since FetchSnapshotAsync now throws before its own
    // `return snapshot;` on any failure, those commit lines are
    // structurally unreachable on a failed fetch, with no seam needed to
    // prove it.
    public class SnapshotFetchFailedExceptionTests
    {
        [Fact]
        public void PartialFailure_MessageReportsFailedAndTotalCount()
        {
            var ex = new SnapshotFetchFailedException(failedSourceCount: 2, totalSourceCount: 5);

            Assert.Equal(2, ex.FailedSourceCount);
            Assert.Equal(5, ex.TotalSourceCount);
            Assert.Equal("2 of 5 account data sources failed.", ex.Message);
        }

        [Fact]
        public void TotalFailure_MessageReportsAllSourcesFailed()
        {
            var ex = new SnapshotFetchFailedException(failedSourceCount: 5, totalSourceCount: 5);

            Assert.Equal("All account data sources failed.", ex.Message);
        }

        // ---- FailedSourceExceptionTypeNames (SnapshotFailureClassifier's
        // input - field-tested pain) ----

        [Fact]
        public void TwoArgConstructor_FailedSourceExceptionTypeNames_IsEmptyNotNull()
        {
            var ex = new SnapshotFetchFailedException(failedSourceCount: 2, totalSourceCount: 5);

            Assert.NotNull(ex.FailedSourceExceptionTypeNames);
            Assert.Empty(ex.FailedSourceExceptionTypeNames);
        }

        [Fact]
        public void ThreeArgConstructor_CapturesFailedSourceExceptionTypeNames()
        {
            var ex = new SnapshotFetchFailedException(
                failedSourceCount: 2,
                totalSourceCount: 5,
                failedSourceExceptionTypeNames: new[] { "InvalidAccessTokenException", "TimeoutException" });

            Assert.Equal(new[] { "InvalidAccessTokenException", "TimeoutException" }, ex.FailedSourceExceptionTypeNames);
        }

        [Fact]
        public void ThreeArgConstructor_NullTypeNames_IsEmptyNotNull()
        {
            var ex = new SnapshotFetchFailedException(failedSourceCount: 2, totalSourceCount: 5, failedSourceExceptionTypeNames: null);

            Assert.NotNull(ex.FailedSourceExceptionTypeNames);
            Assert.Empty(ex.FailedSourceExceptionTypeNames);
        }
    }
}
