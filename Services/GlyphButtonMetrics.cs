namespace TaimisToolbench.Services
{
    /// <summary>
    /// The box a row action draws in: the Ranker row's up / down / remove
    /// trio, the Plan History row's delete, and the plan tree's IGNORE
    /// toggle. Blish-free, so the band math that reserves room for one and
    /// the view that builds it read the same numbers.
    /// <para>
    /// The box is Blish's own window close control, not a glyph. Measured
    /// off the shipped "button-exit" texture: 32x32 with transparent
    /// padding, its ink at (7, 6) and 21x23 across - a dark border around a
    /// 16x16 plate carrying a 13x13 cross. The remove action blits that ink
    /// 1:1 (Views/Rendering/CloseKeyButton), so a row's X and the window's X
    /// are the same pixels at the same size. The box is therefore NOT
    /// square, and no caller may reuse one axis for the other.
    /// </para>
    /// </summary>
    internal static class GlyphButtonMetrics
    {
        /// <summary>Edge of the square "button-exit" texture.</summary>
        public const int CloseKeyTextureSize = 32;

        /// <summary>Left edge of the key's ink inside that texture.</summary>
        public const int CloseKeySourceX = 7;

        /// <summary>Top edge of the key's ink inside that texture.</summary>
        public const int CloseKeySourceY = 6;

        /// <summary>
        /// Width of that ink, and so of every row action. The two carets
        /// beside the Ranker's remove key draw on a FeedbackButton plate at
        /// this same box: one row's actions are one control set, and a
        /// second width would be a second control.
        /// </summary>
        public const int RowActionWidth = 21;

        /// <summary>
        /// Height of that ink. Two rows taller than the key appears,
        /// because the art's own drop shadow is the bottom of it.
        /// </summary>
        public const int RowActionHeight = 23;

        /// <summary>
        /// Width a FeedbackButton plate gives up to its border art, per
        /// axis. Read off Views/Rendering/FeedbackButton's Paint, which
        /// draws the plate at (3, 3, Width - 6, Height - 5) - so a caret
        /// button narrower than its ink plus these would draw the glyph
        /// over its own border. Pinned against the shipped atlas in
        /// GlyphButtonMetricsTests, which fails if a caret grows past what
        /// the close key's box can hold.
        /// </summary>
        public const int PlateInsetX = 6;

        /// <summary>The vertical half of <see cref="PlateInsetX"/>.</summary>
        public const int PlateInsetY = 5;

        /// <summary>Plate left around a caret glyph's ink, on every side.</summary>
        public const int GlyphMargin = 1;
    }
}
