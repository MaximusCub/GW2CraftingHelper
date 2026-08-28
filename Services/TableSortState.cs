using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    internal enum TableSortDirection
    {
        /// <summary>
        /// The table renders in the order its data source produced -
        /// no comparator runs and no header shows an indicator.
        /// </summary>
        None,
        Ascending,
        Descending,
    }

    /// <summary>
    /// Click-to-sort state for one table: which column is active, and in
    /// which direction. Blish-free and per-session (nothing here is
    /// persisted). The view holds one instance per sortable table for its
    /// own lifetime and re-reads it on every render, so every re-render of
    /// the same plan - a re-sort, a tree pill override, a re-solve - keeps
    /// whatever sort the user last clicked. Arriving at a DIFFERENT plan
    /// calls <see cref="Reset"/> instead: the sort described a table that
    /// no longer exists (maintainer decision, field-test round; see
    /// CraftingPlanView.ResetPerPlanSortState, which lists the sites).
    /// <para>
    /// One click cycle per column: None -> Ascending -> Descending -> None.
    /// The third click restores the plan's own emission order rather than
    /// stranding the user in a sort they cannot undo. Clicking a different
    /// column starts that column at Ascending and abandons the previous
    /// one - a table has exactly one active sort column.
    /// </para>
    /// </summary>
    internal sealed class TableSortState<TColumn>
        where TColumn : struct
    {
        /// <summary>
        /// The sort markers, from the module's own shipped glyph font. A
        /// matched pair: same ink, one mirrored, same advance, same seat on
        /// the cap band. The ASCII pair they replaced was not - "^" is a
        /// circumflex accent sitting near the cap line and "v" is a lowercase
        /// letter on the x-height, 3px apart vertically and 4px apart in
        /// height, because Menomonia has no symmetric up/down glyphs at all.
        /// See Services/UiGlyphs, and Views/Rendering/SortableHeaderLabel for
        /// what happens if the font did not load.
        /// </summary>
        public const string AscendingIndicator = UiGlyphs.SortAscending;
        public const string DescendingIndicator = UiGlyphs.SortDescending;

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

        /// <summary>
        /// Back to the data source's own order and no indicator anywhere -
        /// the state a fresh instance starts in, so a table reset this way
        /// is indistinguishable from one never clicked. Idempotent.
        /// </summary>
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
