namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure column-edge arithmetic (Blish-free, unit-testable) for the
    /// Required Recipes table: Recipe (flex) | Discipline | Status. Every
    /// recipe row is one line at PlanContentHeightMath.RecipeRowHeight.
    /// <para>
    /// Status right-anchors to PlanRelayoutMath.PinnedRightEdge; the
    /// Discipline column's text is LEFT-ruled at
    /// <see cref="ColumnEdges.DisciplineX"/> (discipline names are words,
    /// not numerics, and a ragged right edge under a left rule reads as one
    /// column - the same choice the Shopping List's Source column makes);
    /// the recipe name absorbs everything the two of them leave.
    /// </para>
    /// <para>Why the discipline is a column rather than a second caption
    /// line: docs/ARCHITECTURE.md, "Services Q-Z: relocated design
    /// narrative".</para>
    /// </summary>
    internal static class RecipesColumnMath
    {
        /// <summary>
        /// Gap between the Discipline and Status bands. Shared with the
        /// Shopping List's own between-columns gap so two tables in the
        /// same view do not rule their columns at two different rhythms.
        /// </summary>
        public const int ColumnGap = ShoppingColumnMath.ColumnGap;

        /// <summary>
        /// Gap the recipe name's ellipsis budget keeps before the
        /// Discipline column - the name-to-column gap every other plan
        /// table reserves.
        /// </summary>
        public const int NameToDisciplineGap = 12;

        public readonly struct ColumnEdges
        {
            public readonly int StatusRightEdge;
            public readonly int DisciplineX;
            public readonly int NameMaxWidth;

            public ColumnEdges(int statusRightEdge, int disciplineX, int nameMaxWidth)
            {
                StatusRightEdge = statusRightEdge;
                DisciplineX = disciplineX;
                NameMaxWidth = nameMaxWidth;
            }
        }

        /// <summary>
        /// Every edge of one render of the table, from the panel width plus
        /// the two data-derived band widths (each of which the caller has
        /// already floored at its own header label - a header at the
        /// ColumnHeader tier routinely out-measures the data under it).
        /// The single entry point the header row, every data row, and both
        /// of their resize closures call, so no two of them can anchor the
        /// table differently.
        /// <para>
        /// The band widths are the columns' widest values, never one row's
        /// own: a row whose status is blank still must not let its name run
        /// under the widest "Auto-learned" beside it.
        /// </para>
        /// </summary>
        public static ColumnEdges ComputeEdges(
            int panelWidth, int statusColumnWidth, int disciplineColumnWidth, int nameX)
        {
            int statusRightEdge = PlanRelayoutMath.PinnedRightEdge(panelWidth);
            int disciplineX = statusRightEdge - statusColumnWidth - ColumnGap - disciplineColumnWidth;
            int nameMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(
                disciplineX, 0, NameToDisciplineGap, nameX);

            return new ColumnEdges(statusRightEdge, disciplineX, nameMaxWidth);
        }

        /// <summary>
        /// Where the two right-hand headers may sit: from the column on
        /// their left to the column on their right, gutters split - never
        /// their own band, which is floored at the header label itself and
        /// so pins a header that should be centred over shorter values.
        /// Status's right-hand neighbour is the table's pinned edge, and
        /// the recipe name flexes, so what precedes the Discipline column
        /// is that name's ellipsis budget rather than a measured string.
        /// </summary>
        public static void HeaderRooms(
            ColumnEdges edges, int disciplineInk, int statusInk,
            out JustifiedColumnTracks.HeaderRoom discipline,
            out JustifiedColumnTracks.HeaderRoom status)
        {
            int nameBudgetRight = edges.DisciplineX - NameToDisciplineGap;
            int disciplineInkRight = edges.DisciplineX + disciplineInk;
            int statusInkX = edges.StatusRightEdge - statusInk;

            discipline = JustifiedColumnTracks.HeaderRoom.Between(
                JustifiedColumnTracks.RoomLeftBound(nameBudgetRight, edges.DisciplineX),
                JustifiedColumnTracks.RoomRightBound(disciplineInkRight, statusInkX));
            status = JustifiedColumnTracks.HeaderRoom.Between(
                JustifiedColumnTracks.RoomLeftBound(
                    disciplineInk > 0 ? disciplineInkRight : nameBudgetRight, statusInkX),
                edges.StatusRightEdge);
        }
    }
}
