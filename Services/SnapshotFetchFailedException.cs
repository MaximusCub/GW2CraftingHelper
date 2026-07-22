using System;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Thrown by Gw2AccountSnapshotService.FetchSnapshotAsync when one or
    /// more of the independent account-data sources (wallet, bank, shared
    /// inventory, material storage, character list) failed for this fetch
    /// (KNOWN-ISSUES item 31/api-degradation F1).
    ///
    /// Conservative persistence rule (documented here and in
    /// KNOWN-ISSUES.md): FetchSnapshotAsync only ever returns normally on a
    /// FULL success of all data sources. ANY failure - partial or total -
    /// throws this instead of returning a snapshot with holes, so a caller
    /// can never silently persist/replace a good cached snapshot with one
    /// that is missing categories the previous snapshot had. Module.cs's
    /// callers let this propagate to their existing generic-Exception catch
    /// (the same path used for network errors today), which already keeps
    /// the prior good snapshot in place and surfaces a "Refresh failed"
    /// status distinct from "Updated" - no new status plumbing needed.
    ///
    /// Genuine caller cancellation is unaffected: FetchSnapshotAsync's
    /// per-source catches explicitly exclude OperationCanceledException, so
    /// a real cancellation still propagates as OperationCanceledException,
    /// never wrapped in this type.
    /// </summary>
    public class SnapshotFetchFailedException : Exception
    {
        public int FailedSourceCount { get; }
        public int TotalSourceCount { get; }

        public SnapshotFetchFailedException(int failedSourceCount, int totalSourceCount)
            : base(BuildMessage(failedSourceCount, totalSourceCount))
        {
            FailedSourceCount = failedSourceCount;
            TotalSourceCount = totalSourceCount;
        }

        private static string BuildMessage(int failedSourceCount, int totalSourceCount)
        {
            return failedSourceCount >= totalSourceCount
                ? "All account data sources failed."
                : $"{failedSourceCount} of {totalSourceCount} account data sources failed.";
        }
    }
}
