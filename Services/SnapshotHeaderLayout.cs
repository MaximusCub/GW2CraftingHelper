namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Geometry for the Snapshot tab's header block (Blish-free,
    /// unit-testable), where the source-filter checkboxes share the search
    /// row instead of owning a full-width row of their own: the header used
    /// five sparse rows to say what four can, and the widest of them - the
    /// search row - was empty for everything right of the content-type
    /// dropdown.
    /// <para>
    /// They share it only while the whole run fits beside the search box in
    /// ONE row. Sharing halves the width the run has to flow into, so a
    /// roster that used to fit inside the 4-row cap can wrap past it and
    /// hide filters behind a scrollbar - paying a third of the filter set
    /// for 38px of header. Past one row the run falls back to its own
    /// full-width row below the search box, which is exactly the layout it
    /// had before it moved up.
    /// </para>
    /// <para>
    /// The checkbox flow itself is unchanged
    /// (<see cref="SourceFilterFlowLayout"/> still wraps it, still caps it,
    /// still scrolls past the cap); it is handed the placement's width here
    /// and positioned at its start offset by the panel that holds it.
    /// </para>
    /// <para>See docs/ARCHITECTURE.md section 4.</para>
    /// </summary>
    public static class SnapshotHeaderLayout
    {
        /// <summary>
        /// Height of the status row beneath the header buttons. MainView
        /// sizes that panel from here rather than from a literal of its own,
        /// so anything that has to fit inside the row (the inline spinner)
        /// can be checked against the value the row is actually built from.
        /// </summary>
        public const int StatusRowHeight = 24;

        /// <summary>
        /// Width available to the source-filter flow once it starts at
        /// startX instead of the panel's left edge. Floors at 0 rather than
        /// going negative - SourceFilterFlowLayout already degrades a
        /// too-narrow run to one cell per row, which keeps every filter
        /// reachable (wrapped, then scrolled) instead of clipped.
        /// </summary>
        public static int SourceFilterWidth(int panelWidth, int startX)
        {
            int width = panelWidth - startX;
            return width > 0 ? width : 0;
        }

        /// <summary>
        /// Whether a run that flowed into <paramref name="flowedRowCount"/>
        /// rows beside the search box may stay there. One row (or none at
        /// all, before the first snapshot) shares; anything that wrapped
        /// takes its own full-width row instead - see the class doc comment.
        /// </summary>
        public static bool SharesSearchRow(int flowedRowCount)
        {
            return flowedRowCount <= 1;
        }

        /// <summary>
        /// Where the source-filter run sits, and how much width it has to
        /// flow into, in each of the two modes. OffsetY is measured from the
        /// search row's own y.
        /// </summary>
        public readonly struct SourceFilterPlacement
        {
            public readonly bool SharesSearchRow;
            public readonly int X;
            public readonly int OffsetY;
            public readonly int Width;

            public SourceFilterPlacement(bool sharesSearchRow, int x, int offsetY, int width)
            {
                SharesSearchRow = sharesSearchRow;
                X = x;
                OffsetY = offsetY;
                Width = width;
            }
        }

        /// <summary>
        /// The placement for one mode. Callers flow the run at the shared
        /// placement's width first and re-place it here when
        /// <see cref="SharesSearchRow(int)"/> rejects the resulting row
        /// count - the mode cannot be decided before the flow, since it IS
        /// the flow's outcome.
        /// </summary>
        public static SourceFilterPlacement PlaceSourceFilterRun(
            int panelWidth, int startX, int searchRowHeight, int rowGap, bool sharesSearchRow)
        {
            if (sharesSearchRow)
            {
                return new SourceFilterPlacement(true, startX, 0, SourceFilterWidth(panelWidth, startX));
            }

            return new SourceFilterPlacement(
                false, 0, searchRowHeight + rowGap, panelWidth > 0 ? panelWidth : 0);
        }

        /// <summary>
        /// Height of the search band. A shared run sits beside the search
        /// box, so the band is as tall as the taller of the two and a run
        /// that fits there costs the header exactly nothing - which is the
        /// whole saving. A run on its own row costs the search row plus the
        /// gap plus itself, i.e. exactly what the header spent before.
        /// </summary>
        public static int SearchBandHeight(
            int searchRowHeight, int sourceFilterHeight, SourceFilterPlacement placement)
        {
            if (!placement.SharesSearchRow)
            {
                return placement.OffsetY + sourceFilterHeight;
            }

            return searchRowHeight > sourceFilterHeight ? searchRowHeight : sourceFilterHeight;
        }
    }
}
