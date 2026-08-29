namespace TaimisToolbench.Services
{
    /// <summary>
    /// The codepoints the module's own shipped glyph font draws, and the ASCII
    /// each one replaced. Blish-free: these are plain strings, and the font
    /// that turns them into pixels is assembled in Views/Rendering/GlyphFont.
    /// <para>
    /// Blish HUD 1.3.0 ships one text face carrying 226 codepoints and no
    /// runtime font baking, so anything geometric has to arrive in a BMFont we
    /// author (tools/build-glyph-font.py) and package in ref/. These codepoints
    /// are BMP private use from U+E100 up - U+E000 is skipped because Menomonia
    /// already defines it, and merging a glyph over a real one would shadow it.
    /// </para>
    /// <para>
    /// NOTHING else in the module may write a PUA escape. The "UI glyph escapes
    /// exist in the shipped font" step in .github/workflows/tests.yml allows
    /// U+E1xx in THIS FILE ONLY, and checks each one against ref/glyphs.fnt, so
    /// a constant naming a glyph the atlas does not carry fails the build
    /// rather than drawing nothing at all.
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
        /// The reading-size caret trio, for the affordances drawn at body
        /// size rather than inside a column header: the Crafting Ranker's
        /// reorder buttons, the recipe tree's expand/collapse column and the
        /// plan's own section headers. All three were ASCII - "^", "v" and
        /// ">" - which is a circumflex accent, a lowercase letter and a
        /// greater-than sign standing in for three triangles they do not
        /// resemble and do not match each other in weight or baseline.
        /// <para>
        /// A separate pair of codepoints from <see cref="SortAscending"/>
        /// and <see cref="SortDescending"/> because they are a separate
        /// SIZE: 12x8 of ink against the sort pair's 9x6, which is a speck
        /// beside body text. tools/build-glyph-font.py's GLYPHS table
        /// carries both rows and says why.
        /// </para>
        /// </summary>
        internal const string CaretUp = "\uE102";

        /// <summary>Down caret - see <see cref="CaretUp"/>.</summary>
        internal const string CaretDown = "\uE103";

        /// <summary>
        /// Right caret - see <see cref="CaretUp"/>. Authored 8x12 rather
        /// than 12x8 so it carries the same ink area as its collapsed/
        /// expanded partner instead of reading as the lighter of the two.
        /// </summary>
        internal const string CaretRight = "\uE104";

        /// <summary>
        /// The remove mark on a row's delete button. Was a 16px texture
        /// (Blish asset 733269) tinted (45,42,38) - a thin antialiased
        /// stroke in a grey within nine units of the 51,51,51 Blish paints
        /// DISABLED button ink in, next to two carets drawn as text in pure
        /// black. Same button, same state, two pipelines, and it read as
        /// permanently greyed out.
        /// <para>
        /// 16x16 of ink where the carets are 12x8, because a diagonal cross
        /// spends its ink over a longer path: at 16 it sums to 58 pixels of
        /// coverage against the reading carets' 61, which is what makes the
        /// three marks one weight. tools/build-glyph-font.py carries the
        /// measurement and the sizes it fails at.
        /// </para>
        /// </summary>
        internal const string RemoveMark = "\uE105";

        /// <summary>
        /// The caret an expand/collapse affordance draws, already degraded
        /// when the atlas is not there. Every tree and section-header seat
        /// asks here rather than choosing between a glyph and an ASCII
        /// stand-in itself, so the two states can never come from different
        /// vocabularies.
        /// </summary>
        internal static string ExpandCaret(bool expanded, bool glyphsAvailable)
        {
            string glyph = expanded ? CaretDown : CaretRight;
            return glyphsAvailable ? glyph : AsciiFallback(glyph);
        }

        /// <summary>
        /// What a seat draws when the font is not there: a corrupt install
        /// whose ref/glyphs.fnt or ref/glyphs_0.png failed to load. A
        /// codepoint with no region renders as nothing AND advances zero
        /// pixels, so degrading to the mismatched ASCII pair is strictly
        /// better than degrading to a header that silently loses its only
        /// sort indicator. Each caret gets back the character it replaced;
        /// <see cref="RemoveMark"/> replaced a texture and has no such
        /// character, so it takes the closest ASCII shape instead.
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
                case CaretUp: return "^";
                case CaretDown: return "v";
                case CaretRight: return ">";
                case RemoveMark: return "x";
                default: return glyph;
            }
        }
    }
}
