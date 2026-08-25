using System;
using System.Collections.Generic;
using System.Linq;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Thrown by Gw2AccountSnapshotService.FetchSnapshotAsync when one or
    /// more of the independent account-data sources (wallet, bank, shared
    /// inventory, material storage, character list) failed for this fetch
    /// (KNOWN-ISSUES #31/api-degradation F1).
    ///
    /// Conservative persistence rule (documented here and in
    /// KNOWN-ISSUES #31): FetchSnapshotAsync only ever returns normally on a
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

        /// <summary>
        /// The .NET type name (Exception.GetType().Name, e.g.
        /// "InvalidAccessTokenException") of each individual source failure
        /// that contributed to FailedSourceCount, in no particular order.
        /// Deliberately a plain string list, not the exceptions themselves
        /// or Gw2Sharp's own exception types - this class must stay
        /// Gw2Sharp/Blish-free (see SnapshotFailureClassifier's doc
        /// comment) so it can keep being exercised by real unit tests.
        /// Never null; empty when the caller does not supply per-source
        /// detail (the pre-existing 2-arg constructor below, kept for its
        /// original call sites/tests).
        /// </summary>
        public IReadOnlyList<string> FailedSourceExceptionTypeNames { get; }

        public SnapshotFetchFailedException(int failedSourceCount, int totalSourceCount)
            : this(failedSourceCount, totalSourceCount, null)
        {
        }

        public SnapshotFetchFailedException(int failedSourceCount, int totalSourceCount, IEnumerable<string> failedSourceExceptionTypeNames)
            : base(BuildMessage(failedSourceCount, totalSourceCount))
        {
            FailedSourceCount = failedSourceCount;
            TotalSourceCount = totalSourceCount;
            FailedSourceExceptionTypeNames = failedSourceExceptionTypeNames?.ToList() ?? new List<string>();
        }

        private static string BuildMessage(int failedSourceCount, int totalSourceCount)
        {
            return failedSourceCount >= totalSourceCount
                ? "All account data sources failed."
                : $"{failedSourceCount} of {totalSourceCount} account data sources failed.";
        }
    }
}
