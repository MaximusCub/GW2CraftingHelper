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
    /// The checkbox flow itself is unchanged
    /// (<see cref="SourceFilterFlowLayout"/> still wraps it, still caps it,
    /// still scrolls past the cap); it is handed a reduced width here and
    /// positioned at its own start offset by the panel that holds it.
    /// </para>
    /// <para>See docs/ARCHITECTURE.md section 4.</para>
    /// </summary>
    public static class SnapshotHeaderLayout
    {
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
        /// Height of the combined search band: the search row and the
        /// source-filter run sit side by side, so the band is as tall as
        /// the taller of them. A filter run that fits beside the search box
        /// therefore costs the header exactly nothing - which is the whole
        /// saving - and a wrapped run pushes the rows below down by only
        /// what it needs beyond the search row's own height.
        /// </summary>
        public static int SearchBandHeight(int searchRowHeight, int sourceFilterHeight)
        {
            return searchRowHeight > sourceFilterHeight ? searchRowHeight : sourceFilterHeight;
        }
    }
}
