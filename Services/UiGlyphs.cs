namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The codepoints the module's own shipped glyph font draws, and the
    /// ASCII each one replaced. Blish-free: these are plain strings, and the
    /// font that turns them into pixels is assembled in
    /// Views/Rendering/GlyphFont.
    /// <para>
    /// Blish HUD 1.3.0 ships one text face carrying 226 codepoints and no
    /// runtime font baking, so anything geometric has to arrive in a BMFont
    /// we author (tools/build-glyph-font.py) and package in ref/. These
    /// codepoints are BMP private use from U+E100 up - U+E000 is skipped
    /// because Menomonia already defines it, and merging a glyph over a real
    /// one would shadow it.
    /// </para>
    /// <para>
    /// NOTHING else in the module may write a PUA escape. The "UI glyph
    /// escapes exist in the shipped font" step in .github/workflows/tests.yml
    /// allows U+E1xx in THIS FILE ONLY, and checks each one against
    /// ref/glyphs.fnt, so a constant naming a glyph the atlas does not carry
    /// fails the build rather than drawing nothing at all.
    /// </para>
    /// </summary>
    internal static class UiGlyphs
    {
        /// <summary>
        /// Sort ascending. Was "^", which is a circumflex accent: 10x7 ink
        /// parked 3px down the line box, against a lowercase "v"'s 11x11 ink
        /// 6px down. The pair was mismatched by 3px of baseline and 4px of
        /// height because Menomonia has no symmetric up/down glyphs at all -
        /// the measurement this whole font exists to answer.
        /// </summary>
        internal const string SortAscending = "\uE100";

        /// <summary>Sort descending. Was "v" - see <see cref="SortAscending"/>.</summary>
        internal const string SortDescending = "\uE101";

        /// <summary>
        /// What a seat drew before the glyph font existed, for the one case
        /// where the font is not there: a corrupt install whose ref/glyphs.fnt
        /// or ref/glyphs_0.png failed to load. A codepoint with no region
        /// renders as nothing AND advances zero pixels, so degrading to the
        /// mismatched ASCII pair is strictly better than degrading to a header
        /// that silently loses its only sort indicator.
        /// <para>
        /// Total over every constant above, which is what makes the degraded
        /// path safe; an unmapped string comes back unchanged rather than
        /// disappearing, and the CI step named on this class is what keeps the
        /// two lists in step.
        /// </para>
        /// </summary>
        internal static string AsciiFallback(string glyph)
        {
            switch (glyph)
            {
                case SortAscending: return "^";
                case SortDescending: return "v";
                default: return glyph;
            }
        }
    }
}
