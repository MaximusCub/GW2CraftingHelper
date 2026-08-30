using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>Columns the Plan History table exposes.</summary>
    internal enum PlanHistoryTableColumn
    {
        Plan,
        Cost,
        Generated,
    }

    /// <summary>
    /// Comparators behind the Plan History tab's clickable column headers,
    /// the same shape as <see cref="PlanTableSorter"/>: a reordered copy,
    /// stable in ties, and the caller's own list back untouched when no sort
    /// is active.
    /// <para>
    /// A user sort overrides the pin-first rule
    /// <see cref="PlanHistoryRetention.SortForDisplay"/> applies: asked for
    /// the cheapest plan, a reader means the cheapest plan, not the cheapest
    /// pinned one. The third click of the cycle restores the default order,
    /// pins and all.
    /// </para>
    /// </summary>
    internal static class PlanHistoryTableSorter
    {
        public static IReadOnlyList<PlanHistoryEntry> Sort(
            IReadOnlyList<PlanHistoryEntry> entries, TableSortState<PlanHistoryTableColumn> state)
        {
            if (entries == null || entries.Count < 2)
            {
                return entries;
            }

            if (state == null || state.Direction == TableSortDirection.None || !state.Column.HasValue)
            {
                return entries;
            }

            PlanHistoryTableColumn column = state.Column.Value;
            int sign = state.Direction == TableSortDirection.Descending ? -1 : 1;

            var order = new int[entries.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int compared = sign * Compare(entries[a], entries[b], column);

                // Index order last, so equal keys keep the display order
                // they arrived in - Array.Sort is not itself stable.
                return compared != 0 ? compared : a.CompareTo(b);
            });

            var sorted = new List<PlanHistoryEntry>(entries.Count);
            for (int i = 0; i < order.Length; i++)
            {
                sorted.Add(entries[order[i]]);
            }

            return sorted;
        }

        private static int Compare(
            PlanHistoryEntry a, PlanHistoryEntry b, PlanHistoryTableColumn column)
        {
            switch (column)
            {
                case PlanHistoryTableColumn.Cost:
                    return Cost(a).CompareTo(Cost(b));
                case PlanHistoryTableColumn.Generated:
                    return Generated(a).CompareTo(Generated(b));
                default:
                    // The row's own visible text, so the order a reader
                    // checks the sort against is the one they can see.
                    // Case-insensitive, like every other name column here.
                    return string.Compare(
                        PlanHistoryLabels.RowLabel(a) ?? string.Empty,
                        PlanHistoryLabels.RowLabel(b) ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>A missing entry sorts as a free, undated plan rather
        /// than throwing: the list this reads is rebuilt from disk and a
        /// null has reached row-building code before.</summary>
        private static long Cost(PlanHistoryEntry entry)
        {
            return entry?.TotalCoinCostAtGeneration ?? 0L;
        }

        private static DateTime Generated(PlanHistoryEntry entry)
        {
            return entry?.LastGeneratedAtUtc ?? DateTime.MinValue;
        }
    }
}
