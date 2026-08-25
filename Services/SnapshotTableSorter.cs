using GW2CraftingHelper.Models;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Columns the Snapshot tab's two runs expose. Name covers both an
    /// item's name and a currency's - one enum, because the two runs are
    /// the same two columns over different data.
    /// </summary>
    public enum SnapshotTableColumn
    {
        Name,
        Amount
    }

    /// <summary>
    /// Comparators behind the Snapshot tab's clickable column headers.
    /// Blish-free, and the same shape as <see cref="PlanTableSorter"/>:
    /// it reorders already-built rows, never mutates the caller's list, and
    /// returns the very same instance when no sort is active, so the
    /// default path allocates nothing.
    /// <para>
    /// Names compare with <see cref="StringComparer.OrdinalIgnoreCase"/> -
    /// what a reader scanning an alphabetical column expects, and what
    /// PlanTableSorter already uses for the same job.
    /// </para>
    /// </summary>
    public static class SnapshotTableSorter
    {
        public static IReadOnlyList<SnapshotSearchRow> SortItems(
            IReadOnlyList<SnapshotSearchRow> rows, TableSortState<SnapshotTableColumn> state)
        {
            return Sort(rows, state, CompareItems);
        }

        public static IReadOnlyList<SnapshotWalletEntry> SortWallet(
            IReadOnlyList<SnapshotWalletEntry> rows, TableSortState<SnapshotTableColumn> state)
        {
            return Sort(rows, state, CompareWallet);
        }

        private static IReadOnlyList<T> Sort<T>(
            IReadOnlyList<T> rows,
            TableSortState<SnapshotTableColumn> state,
            Func<T, T, SnapshotTableColumn, int> compare)
        {
            if (rows == null || rows.Count < 2) return rows;
            if (state == null || state.Direction == TableSortDirection.None || !state.Column.HasValue) return rows;

            SnapshotTableColumn column = state.Column.Value;
            int sign = state.Direction == TableSortDirection.Descending ? -1 : 1;

            var order = new int[rows.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            // Index-keyed, so ties keep their original relative order - a
            // stable sort without depending on Array.Sort being one.
            Array.Sort(order, (a, b) =>
            {
                int compared = sign * compare(rows[a], rows[b], column);
                return compared != 0 ? compared : a.CompareTo(b);
            });

            var sorted = new List<T>(rows.Count);
            for (int i = 0; i < order.Length; i++)
            {
                sorted.Add(rows[order[i]]);
            }

            return sorted;
        }

        private static int CompareItems(SnapshotSearchRow left, SnapshotSearchRow right, SnapshotTableColumn column)
        {
            if (column == SnapshotTableColumn.Amount)
            {
                return left.TotalCount.CompareTo(right.TotalCount);
            }

            return string.Compare(left.Name ?? "", right.Name ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareWallet(SnapshotWalletEntry left, SnapshotWalletEntry right, SnapshotTableColumn column)
        {
            if (column == SnapshotTableColumn.Amount)
            {
                return left.Value.CompareTo(right.Value);
            }

            return string.Compare(
                left.CurrencyName ?? "", right.CurrencyName ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }
}
