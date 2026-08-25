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
    /// Blish-free, and the same shape as <see cref="PlanTableSorter"/>
    /// except in what it returns: an ORDER over the caller's rows rather
    /// than a sorted copy of them, because the rows it sorts are already
    /// built controls. The caller's list is never mutated, and no sort at
    /// all allocates nothing.
    /// <para>
    /// Names compare with <see cref="StringComparer.OrdinalIgnoreCase"/> -
    /// what a reader scanning an alphabetical column expects, and what
    /// PlanTableSorter already uses for the same job.
    /// </para>
    /// </summary>
    public static class SnapshotTableSorter
    {
        /// <summary>
        /// The sort as a PERMUTATION of the caller's own row order:
        /// <c>order[i]</c> is the index of the row that belongs in display
        /// position i. Null means "leave them as they are" - no sort
        /// active, or nothing to reorder.
        /// <para>
        /// An order rather than a sorted copy, because the Snapshot tab's
        /// rows are built once in the search's own order and a click moves
        /// the controls it already has rather than re-running the account
        /// search (see MainView.SortSection). Returning null rather than
        /// the identity permutation is what lets the third click - the
        /// cycle back to None - restore the search's own order without the
        /// view keeping a second copy of it.
        /// </para>
        /// </summary>
        public static IReadOnlyList<int> ItemOrder(
            IReadOnlyList<SnapshotSearchRow> rows, TableSortState<SnapshotTableColumn> state)
        {
            return Order(rows, state, CompareItems);
        }

        /// <summary>The wallet run's twin of <see cref="ItemOrder"/>.</summary>
        public static IReadOnlyList<int> WalletOrder(
            IReadOnlyList<SnapshotWalletEntry> rows, TableSortState<SnapshotTableColumn> state)
        {
            return Order(rows, state, CompareWallet);
        }

        private static IReadOnlyList<int> Order<T>(
            IReadOnlyList<T> rows,
            TableSortState<SnapshotTableColumn> state,
            Func<T, T, SnapshotTableColumn, int> compare)
        {
            if (rows == null || rows.Count < 2) return null;
            if (state == null || state.Direction == TableSortDirection.None || !state.Column.HasValue) return null;

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

            return order;
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
