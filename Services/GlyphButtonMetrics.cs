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
    /// 1:1 (Views/Rendering/RowActionKey), so a row's X and the window's X
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
        /// beside the Ranker's remove key are cut from the same texture at
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
        /// Height of the frame at each end of the key. The cross occupies
        /// source rows 12 to 22 of a key that runs from row 6 to row 28,
        /// so the first and last six rows carry border, shadow and bare
        /// plate and no mark at all. A key that says something other than
        /// the cross keeps both of those and refills the middle
        /// (Views/Rendering/CaretKeyButton).
        /// </summary>
        public const int KeyCapHeight = 6;

        /// <summary>
        /// Source row of the last bare plate row above the cross, which is
        /// what fills the space between the two frames.
        /// </summary>
        public const int KeyPlateRowY = CloseKeySourceY + KeyCapHeight - 1;

        /// <summary>
        /// Edge of the lit plate inside the key's ink, and so of the box a
        /// mark has to fit: the cross is 13x13 in it, and a caret that
        /// outgrew it would draw over the border art.
        /// </summary>
        public const int KeyPlateSize = 16;

        /// <summary>Plate left around a caret glyph's ink, on every side.</summary>
        public const int GlyphMargin = 1;
    }
}
