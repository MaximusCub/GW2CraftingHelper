namespace TaimisToolbench.Services
{
    /// <summary>
    /// The square a row-action button draws in when its whole label is one
    /// glyph: the Ranker row's up / down / remove trio and the Plan History
    /// row's delete. Blish-free so the band math that reserves room for one
    /// and the view that builds it read the same number.
    /// <para>
    /// These are NOT tab buttons. They used to draw at the module's on-tab
    /// button height, which put a 16px cross in the middle of a 28px
    /// parchment plate - a full-size button carrying a mark. The game's own
    /// window close control is a compact square whose mark nearly fills it,
    /// and that is the scale asked for.
    /// </para>
    /// </summary>
    internal static class GlyphButtonMetrics
    {
        /// <summary>
        /// Width the plate gives up to its border art, per axis. Read off
        /// Views/Rendering/FeedbackButton's Paint, which draws the plate at
        /// (3, 3, Width - 6, Height - 5) and the four border strips around
        /// it - so a button smaller than the ink plus these draws its glyph
        /// over its own border.
        /// </summary>
        public const int PlateInsetX = 6;

        /// <summary>The vertical half of <see cref="PlateInsetX"/>.</summary>
        public const int PlateInsetY = 5;

        /// <summary>Plate left around the glyph's ink, on every side.</summary>
        public const int GlyphMargin = 1;

        /// <summary>
        /// The button's edge, square. The widest ink these buttons draw is
        /// the 16x16 remove cross - the glyph atlas ships one size, so the
        /// mark cannot shrink with the button and the button is sized to it
        /// instead. The X axis binds, giving up 6px to the border against
        /// the Y axis's 5. Pinned against the shipped atlas in
        /// GlyphButtonMetricsTests, which fails if a wider glyph is added or
        /// if this is trimmed below what the current set needs.
        /// </summary>
        public const int RowActionSize = 16 + (2 * GlyphMargin) + PlateInsetX;
    }
}
