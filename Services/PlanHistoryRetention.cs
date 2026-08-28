using System;
using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure eviction and ordering arithmetic for Plan History. Two caps:
    /// the row cap (ModuleSettings.PlanHistoryMaxEntries, rows and blobs
    /// both deleted) and the smaller blob-only cap below it, applied
    /// first, which drops an entry's blob while keeping its row - the
    /// row degrades from Open to Re-solve instead of vanishing. Pinned
    /// entries are exempt from both.
    /// </summary>
    internal static class PlanHistoryRetention
    {
        /// <summary>
        /// The blob-only inner cap - a constant, not a setting (see the
        /// Plan History spec's sizing basis: blobs are the expensive
        /// half, tens of KB each, so only the newest 15 unpinned entries
        /// keep one).
        /// </summary>
        public const int PlanHistoryBlobCap = 15;

        /// <summary>Per-entry CostSamples cap; oldest samples dropped first.</summary>
        public const int MaxCostSamples = 20;

        /// <summary>
        /// Entry ids to evict entirely (row and blob), oldest
        /// LastGeneratedAtUtc first. PINNED ENTRIES ARE NEVER EVICTED, so
        /// the surviving unpinned count is at most
        /// max(0, maxEntries - pinnedCount): a cap smaller than the
        /// pinned count evicts every unpinned row and stops. Returns
        /// empty when under the cap.
        /// </summary>
        public static IReadOnlyList<string> SelectForEviction(
            IReadOnlyList<PlanHistoryEntry> entries, int maxEntries)
        {
            return SelectOldestUnpinnedBeyond(entries, maxEntries, e => true);
        }

        /// <summary>
        /// Ids whose BLOB should be dropped while the row is kept - the
        /// inner cap. Same pinned exemption; only entries that currently
        /// hold a blob count against it or are selected.
        /// </summary>
        public static IReadOnlyList<string> SelectForBlobEviction(
            IReadOnlyList<PlanHistoryEntry> entries, int maxBlobs)
        {
            return SelectOldestUnpinnedBeyond(entries, maxBlobs, e => e.BlobPresent);
        }

        /// <summary>
        /// Pinned first, then LastGeneratedAtUtc descending, then EntryId
        /// ordinal as the tie-break so the order is total and stable
        /// across sessions.
        /// </summary>
        public static List<PlanHistoryEntry> SortForDisplay(IReadOnlyList<PlanHistoryEntry> entries)
        {
            if (entries == null)
            {
                return new List<PlanHistoryEntry>();
            }

            return entries
                .Where(e => e != null)
                .OrderByDescending(e => e.Pinned)
                .ThenByDescending(e => e.LastGeneratedAtUtc)
                .ThenBy(e => e.EntryId, StringComparer.Ordinal)
                .ToList();
        }

        private static IReadOnlyList<string> SelectOldestUnpinnedBeyond(
            IReadOnlyList<PlanHistoryEntry> entries, int cap, Func<PlanHistoryEntry, bool> counts)
        {
            var result = new List<string>();
            if (entries == null)
            {
                return result;
            }

            var counted = entries.Where(e => e != null && counts(e)).ToList();
            int pinnedCount = counted.Count(e => e.Pinned);
            int allowedUnpinned = Math.Max(0, cap - pinnedCount);

            var unpinned = counted
                .Where(e => !e.Pinned)
                .OrderBy(e => e.LastGeneratedAtUtc)
                .ThenBy(e => e.EntryId, StringComparer.Ordinal)
                .ToList();

            int evictCount = unpinned.Count - allowedUnpinned;
            for (int i = 0; i < evictCount; i++)
            {
                result.Add(unpinned[i].EntryId);
            }

            return result;
        }
    }
}
