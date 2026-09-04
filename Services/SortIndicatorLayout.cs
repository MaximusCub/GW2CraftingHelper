namespace TaimisToolbench.Services
{
    /// <summary>
    /// The persistent sort indicator: every SORTABLE column header carries
    /// one at all times, dim at rest and solid on the column the table is
    /// actually sorted by. Blish-free arithmetic; the control that draws it
    /// is Views/Rendering/SortIndicator.
    /// <para>
    /// It used to appear only on the active column, which made a sortable
    /// header indistinguishable from a fixed one until it was clicked, and
    /// meant a click GREW the header by an indicator's width - moving the
    /// very cell the cursor was over at the instant of the press. Both are
    /// answered by the same rule: the slot is reserved unconditionally and
    /// a click changes only opacity and which glyph is drawn.
    /// </para>
    /// <para>
    /// Which is why <see cref="SlotWidth"/> takes the WIDER of the pair.
    /// The shipped glyphs are one advance (ref/glyphs.fnt: xadvance=9 for
    /// both); the ASCII fallback a corrupt install degrades to, "^" against
    /// "v", is not.
    /// </para>
    /// </summary>
    internal static class SortIndicatorLayout
    {
        /// <summary>Gap between a header's word and its indicator. Wider
        /// than a space's advance at the ColumnHeader tier: at that width
        /// the mark measured ~5px from the label in a field capture
        /// and read as attached to the word. Kept as a number because the
        /// two are separate controls and no string joins them.</summary>
        public const int Gap = 8;

        /// <summary>
        /// Opacity of a sortable column's indicator at rest. Low enough to
        /// read as scenery beside the active column's solid mark, high
        /// enough to survive the header band's own dark texture.
        /// </summary>
        public const float RestOpacity = 0.3f;

        public const float ActiveOpacity = 1f;

        /// <summary>
        /// The glyph a header in <paramref name="direction"/> draws. An
        /// unsorted column takes the ASCENDING mark: a first click sorts
        /// ascending, so the rest state is an honest preview of what the
        /// control does rather than a neutral ornament.
        /// </summary>
        public static string GlyphFor(TableSortDirection direction)
        {
            return direction == TableSortDirection.Descending
                ? UiGlyphs.SortDescending
                : UiGlyphs.SortAscending;
        }

        public static float OpacityFor(TableSortDirection direction)
        {
            return direction == TableSortDirection.None ? RestOpacity : ActiveOpacity;
        }

        /// <summary>
        /// Width of the reserved slot: the wider of the two glyphs, so
        /// neither direction nor the degraded ASCII pair can change it.
        /// </summary>
        public static int SlotWidth(int ascendingWidth, int descendingWidth)
        {
            int width = ascendingWidth > descendingWidth ? ascendingWidth : descendingWidth;
            return width > 0 ? width : 0;
        }

        /// <summary>
        /// Width a sortable header occupies: its word, the gap and the slot.
        /// This is what every column band is floored at and what every
        /// centring rule in <see cref="JustifiedColumnTracks"/> is handed,
        /// so a table's columns are laid out once for all three states.
        /// </summary>
        public static int BlockWidth(int titleWidth, int slotWidth)
        {
            int title = titleWidth > 0 ? titleWidth : 0;
            int slot = slotWidth > 0 ? slotWidth : 0;
            return slot == 0 ? title : title + Gap + slot;
        }

        /// <summary>Left edge of the indicator slot for a header block whose
        /// word starts at <paramref name="blockX"/>.</summary>
        public static int SlotX(int blockX, int titleWidth)
        {
            return blockX + (titleWidth > 0 ? titleWidth : 0) + Gap;
        }

        /// <summary>
        /// X the glyph itself draws at inside its slot. Centred, because the
        /// slot is sized for the wider of the pair and a narrower glyph
        /// pinned to one edge would still visibly shift when the direction
        /// flipped - the shift this class exists to remove, one level down.
        /// </summary>
        public static int GlyphX(int slotX, int slotWidth, int glyphWidth)
        {
            return JustifiedColumnTracks.CenteredInBand(slotX, slotWidth, glyphWidth);
        }
    }
}
