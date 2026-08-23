using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    public enum TableSortDirection
    {
        /// <summary>
        /// The table renders in the order its data source produced -
        /// no comparator runs and no header shows an indicator.
        /// </summary>
        None,
        Ascending,
        Descending
    }

    /// <summary>
    /// Click-to-sort state for one table: which column is active, and in
    /// which direction. Blish-free and per-session (nothing here is
    /// persisted), so a plan regenerate that rebuilds every control keeps
    /// whatever sort the user last clicked - the view holds one instance
    /// per sortable table for its own lifetime and re-reads it on every
    /// render.
    /// <para>
    /// One click cycle per column: None -> Ascending -> Descending -> None.
    /// The third click restores the plan's own emission order rather than
    /// stranding the user in a sort they cannot undo. Clicking a different
    /// column starts that column at Ascending and abandons the previous
    /// one - a table has exactly one active sort column.
    /// </para>
    /// </summary>
    public sealed class TableSortState<TColumn>
        where TColumn : struct
    {
        /// <summary>ASCII sort markers - the tree's caret vocabulary.</summary>
        public const string AscendingIndicator = "^";
        public const string DescendingIndicator = "v";

        private static readonly EqualityComparer<TColumn> ColumnComparer = EqualityComparer<TColumn>.Default;

        /// <summary>
        /// The active column, or null when <see cref="Direction"/> is
        /// <see cref="TableSortDirection.None"/>.
        /// </summary>
        public TColumn? Column { get; private set; }

        public TableSortDirection Direction { get; private set; }

        public bool IsActive(TColumn column)
        {
            return Direction != TableSortDirection.None
                && Column.HasValue
                && ColumnComparer.Equals(Column.Value, column);
        }

        /// <summary>
        /// Advances the state for a click on <paramref name="column"/>.
        /// </summary>
        public void Cycle(TColumn column)
        {
            if (!IsActive(column))
            {
                Column = column;
                Direction = TableSortDirection.Ascending;
                return;
            }

            if (Direction == TableSortDirection.Ascending)
            {
                Direction = TableSortDirection.Descending;
                return;
            }

            Reset();
        }

        public void Reset()
        {
            Column = null;
            Direction = TableSortDirection.None;
        }

        /// <summary>
        /// The marker a header for <paramref name="column"/> should carry -
        /// empty string for every inactive column, so an unsorted table
        /// shows no indicator at all.
        /// </summary>
        public string IndicatorFor(TColumn column)
        {
            if (!IsActive(column))
            {
                return string.Empty;
            }

            return Direction == TableSortDirection.Ascending ? AscendingIndicator : DescendingIndicator;
        }
    }
}
