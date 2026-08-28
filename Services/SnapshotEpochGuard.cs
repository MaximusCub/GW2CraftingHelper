namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure decision for whether a completed account-snapshot fetch may
    /// still commit its result (KNOWN-ISSUES #31/31a-F1 - snapshot
    /// auto-refresh vs Clear Cache race). Mirrors StatusUpdateGuard's
    /// shape: the caller captures an epoch before starting the fetch's
    /// await, Module.ClearCache bumps the same shared epoch counter, and
    /// the fetch's continuation only commits if the epoch it captured is
    /// still current when the continuation actually runs.
    ///
    /// Unlike CraftingPlanView's myGen/_generateSequence pair (which bumps
    /// on every new generation, since overlapping generations must all be
    /// distinguishable from each other), only Clear Cache bumps this
    /// epoch: Module's own _refreshInProgress gate already guarantees at
    /// most one snapshot fetch is ever in flight at a time, so the only
    /// event this guard needs to detect is "did the user clear the cache
    /// while my fetch was still running".
    /// </summary>
    internal static class SnapshotEpochGuard
    {
        public static bool ShouldCommit(int myEpoch, int currentEpoch)
        {
            return myEpoch == currentEpoch;
        }
    }
}
