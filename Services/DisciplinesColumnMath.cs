namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure column-edge arithmetic (Blish-free, unit-testable) for the
    /// Required Disciplines table: Discipline | Characters | Level. Level
    /// right-anchors to PlanRelayoutMath.PinnedRightEdge; the other two are
    /// LEFT-ruled, because discipline names and character runs are words
    /// rather than numerics and a ragged right edge under a left rule still
    /// reads as one column (the choice Required Recipes' Discipline column
    /// and the Shopping List's Source column both make).
    /// <para>
    /// The three columns DISTRIBUTE over equal tracks, the module's shared
    /// law - see <see cref="JustifiedColumnTracks"/>. Before this class the
    /// table had no distribution at all: Discipline and Characters were
    /// packed against the row's left edge with Level alone at the far right
    /// and the whole middle of the row empty. A character run too wide for
    /// a track drops the table back to that packed placement, which is
    /// where such a run has the most room to be read in.
    /// </para>
    /// </summary>
    internal static class DisciplinesColumnMath
    {
        /// <summary>Left x of the discipline name, and of the whole track
        /// span.</summary>
        public const int NameX = 8;

        /// <summary>
        /// Gap between any two of this table's columns. Shared with the
        /// Shopping List's own so two tables in one view do not rule their
        /// columns at two different rhythms; it replaced a private 12,
        /// which left the widest discipline name all but touching the
        /// character run beside it.
        /// </summary>
        public const int ColumnGap = ShoppingColumnMath.ColumnGap;

        /// <summary>Discipline, Characters, Level - one track each.</summary>
        public const int TrackCount = 3;

        public readonly struct ColumnEdges
        {
            public readonly int LevelRightEdge;

            /// <summary>Left rule of the character run, and of its
            /// header.</summary>
            public readonly int CharX;

            /// <summary>
            /// Room between that rule and the Level band - the character
            /// run's ellipsis budget. Never negative. NOT what its header
            /// centres over or is bounded by; see <see cref="HeaderRooms"/>.
            /// </summary>
            public readonly int CharBandWidth;

            /// <summary>
            /// Whether the columns are distributed over equal tracks or
            /// packed against the row's left edge - see
            /// <see cref="ComputeEdges"/>.
            /// </summary>
            public readonly bool Distributed;

            public ColumnEdges(int levelRightEdge, int charX, int charBandWidth, bool distributed)
            {
                LevelRightEdge = levelRightEdge;
                CharX = charX;
                CharBandWidth = charBandWidth;
                Distributed = distributed;
            }
        }

        /// <summary>
        /// Every edge of one render, from the panel width plus the three
        /// columns' band widths (each already floored at its own header
        /// label by the caller - at the ColumnHeader tier a header
        /// routinely out-measures the data under it). The single entry
        /// point the header row, every data row and both of their resize
        /// closures call, so no two of them can anchor the table
        /// differently.
        /// <para>
        /// The band widths are the columns' widest values, never one row's
        /// own: a row with no character text at all still must not let its
        /// discipline name run under the column beside it.
        /// </para>
        /// </summary>
        public static ColumnEdges ComputeEdges(
            int panelWidth, int disciplineColumnWidth, int charColumnWidth, int levelColumnWidth)
        {
            int levelRightEdge = PlanRelayoutMath.PinnedRightEdge(panelWidth);

            // Where the character run starts when there is nothing to
            // distribute: one gap past the widest discipline name, which is
            // as far left as it can legally go.
            int charX = NameX + disciplineColumnWidth + ColumnGap;

            int trackSpan = levelRightEdge - NameX;
            int widestBand = Max(Max(disciplineColumnWidth, charColumnWidth), levelColumnWidth);
            bool distributed =
                JustifiedColumnTracks.FitsDistributed(trackSpan, TrackCount, widestBand, ColumnGap);
            if (distributed)
            {
                // Max, not the track edge outright: the track is wide
                // enough to clear the widest discipline name by
                // construction, but taking the larger of the two states
                // that invariant rather than relying on it.
                int trackX = JustifiedColumnTracks.LeftEdge(NameX, trackSpan, TrackCount, 1);
                if (trackX > charX)
                {
                    charX = trackX;
                }
            }

            int charBandWidth = levelRightEdge - levelColumnWidth - ColumnGap - charX;
            return new ColumnEdges(
                levelRightEdge, charX, charBandWidth > 0 ? charBandWidth : 0, distributed);
        }

        /// <summary>
        /// Where the two right-hand headers may sit: from the column on
        /// their left to the column on their right, gutters split - never
        /// their own reserve, which is floored at the header label itself
        /// and so pins a header that should be centred. Level's right-hand
        /// neighbour is the table's pinned edge. A table with no character
        /// text at all reserves no Characters column, and Level's left
        /// neighbour is then the discipline names.
        /// </summary>
        public static void HeaderRooms(
            ColumnEdges edges, int nameInk, int charInk, int levelInk,
            out JustifiedColumnTracks.HeaderRoom characters,
            out JustifiedColumnTracks.HeaderRoom level)
        {
            int nameInkRight = NameX + nameInk;
            int charInkRight = edges.CharX + charInk;
            int levelInkX = edges.LevelRightEdge - levelInk;

            characters = JustifiedColumnTracks.HeaderRoom.Between(
                JustifiedColumnTracks.RoomLeftBound(nameInkRight, edges.CharX),
                JustifiedColumnTracks.RoomRightBound(charInkRight, levelInkX));
            level = JustifiedColumnTracks.HeaderRoom.Between(
                JustifiedColumnTracks.RoomLeftBound(
                    charInk > 0 ? charInkRight : nameInkRight, levelInkX),
                edges.LevelRightEdge);
        }

        private static int Max(int a, int b)
        {
            return a > b ? a : b;
        }
    }
}
