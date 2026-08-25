using GW2CraftingHelper.Models;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>Columns the Snapshot tab's two runs expose - one enum,
    /// because they are the same two columns over different data.</summary>
    public enum SnapshotTableColumn
    {
        Name,
        Amount
    }

    /// <summary>
    /// Comparators behind the Snapshot tab's clickable column headers, the
    /// same shape as <see cref="PlanTableSorter"/> (case-insensitive names
    /// included) except in what they return - see <see cref="ItemOrder"/>.
    /// </summary>
    public static class SnapshotTableSorter
    {
        /// <summary>
        /// The sort as a PERMUTATION of the caller's rows: <c>order[i]</c>
        /// is the index of the row belonging in display position i, null
        /// for "leave them as they are". An order and not a sorted copy
        /// because the rows are built controls a click only re-places
        /// (MainView.SortSection); null and not the identity because that
        /// is how the cycle back to None restores the search's own order.
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

            // Index-keyed: ties keep their original relative order without
            // depending on Array.Sort being stable.
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
