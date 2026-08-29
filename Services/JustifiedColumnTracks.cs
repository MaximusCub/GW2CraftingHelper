namespace TaimisToolbench.Services
{
    /// <summary>
    /// THE distribution law every justified table in the module shares: a
    /// row's span is divided into N EQUAL tracks and each data column is
    /// CENTRED on its own track, so the columns spread across the panel
    /// instead of stacking against its right-hand edge.
    /// <para>
    /// TRACK COUNT is one per data column plus ONE for the label, which is
    /// the leftmost and the only flexing element. Data column i (0-based)
    /// right-aligns on <c>RightEdge(..., i + 1)</c>; the label fills from
    /// startX up to the first data column's reserved band, so it absorbs
    /// whatever that column does not need.
    /// </para>
    /// <para>
    /// A column's header and its cells centre on the same track, which is
    /// what makes a header sit over the values it names.
    /// <see cref="RightEdge"/> is kept for the bands that genuinely pin to
    /// an edge (buttons, coin runs that must not ragged-right against each
    /// other). Derivation: docs/ARCHITECTURE.md section S1.2.
    /// </para>
    /// </summary>
    internal static class JustifiedColumnTracks
    {
        /// <summary>
        /// Right edge of track <paramref name="index"/> (0-based) of
        /// <paramref name="trackCount"/> equal tracks spanning
        /// <paramref name="trackSpan"/> px from <paramref name="startX"/>.
        /// Integer-exact off the span rather than accumulated from a
        /// rounded track width, so the last track's edge lands exactly on
        /// the span's own end instead of a rounding pixel short of it.
        /// </summary>
        public static int RightEdge(int startX, int trackSpan, int trackCount, int index)
        {
            if (trackCount <= 0)
            {
                return startX;
            }

            return startX + (int)((long)trackSpan * (index + 1) / trackCount);
        }

        /// <summary>
        /// Left edge of track <paramref name="index"/> - the right edge of
        /// the track before it, so adjacent tracks share an edge exactly
        /// and no rounding gap opens between them.
        /// </summary>
        public static int LeftEdge(int startX, int trackSpan, int trackCount, int index)
        {
            if (trackCount <= 0)
            {
                return startX;
            }

            return startX + (int)((long)trackSpan * index / trackCount);
        }

        /// <summary>Width of track <paramref name="index"/>.</summary>
        public static int Width(int startX, int trackSpan, int trackCount, int index)
        {
            int left = LeftEdge(startX, trackSpan, trackCount, index);
            int right = RightEdge(startX, trackSpan, trackCount, index);
            return right > left ? right - left : 0;
        }

        /// <summary>
        /// X at which content of <paramref name="contentWidth"/> centres in
        /// track <paramref name="index"/>. Content wider than its track is
        /// pinned to the track's left edge rather than allowed to overhang
        /// symmetrically into both neighbours, so an overlong cell collides
        /// in one direction only and the column to its left stays readable.
        /// </summary>
        public static int CenteredX(
            int startX, int trackSpan, int trackCount, int index, int contentWidth)
        {
            int left = LeftEdge(startX, trackSpan, trackCount, index);
            int width = Width(startX, trackSpan, trackCount, index);
            if (contentWidth >= width)
            {
                return left;
            }

            return left + (width - contentWidth) / 2;
        }

        /// <summary>
        /// X at which content centres in a column's own reserved BAND rather
        /// than in an equal track: the same law, for a column whose band -
        /// not an equal share of the row - is the thing to centre in.
        /// Content wider than its band pins left, exactly as it does in a
        /// track. NOT the rule for a header, which centres over the cells'
        /// own extent instead - see <see cref="CenteredOverContent"/>; use
        /// this only where the band IS the content.
        /// </summary>
        public static int CenteredInBand(int bandX, int bandWidth, int contentWidth)
        {
            if (contentWidth >= bandWidth)
            {
                return bandX;
            }

            return bandX + (bandWidth - contentWidth) / 2;
        }

        /// <summary>
        /// X at which a header centres over the extent its column's CELLS
        /// actually cover - contentX to contentX + contentWidth - rather
        /// than over the reserved band those cells sit in. THE header law
        /// of this module: a band is invisible to a reader, so centring in
        /// one drifts the word off the ink it names by half of however much
        /// the band exceeds it, and a band routinely does.
        /// <para>
        /// Cells keep their own justification, so the CALLER derives
        /// contentX from it: bandX for a left-ruled column,
        /// rightEdge - contentWidth for a right-aligned one. Clamped into
        /// the band, so a header wider than the content it names pins to
        /// the band's near edge instead of overhanging a neighbour - which
        /// is where a column with no content at all (contentWidth 0) lands
        /// too. Derivation: docs/ARCHITECTURE.md section S1.2.
        /// </para>
        /// </summary>
        public static int CenteredOverContent(
            int bandX, int bandWidth, int contentX, int contentWidth, int headerWidth)
        {
            int x = contentX + ((contentWidth - headerWidth) / 2);
            int rightmost = bandX + bandWidth - headerWidth;
            if (x > rightmost)
            {
                x = rightmost;
            }

            return x < bandX ? bandX : x;
        }

        /// <summary>
        /// <see cref="CenteredOverContent"/> for a column whose cells
        /// right-align on <paramref name="rightEdge"/> and whose band ends
        /// there too - the shape every numeric and coin column in the
        /// module has.
        /// </summary>
        public static int CenteredOverContentRightAligned(
            int rightEdge, int bandWidth, int contentWidth, int headerWidth)
        {
            return CenteredOverContent(
                rightEdge - bandWidth, bandWidth, rightEdge - contentWidth, contentWidth, headerWidth);
        }

        /// <summary>
        /// Whether a span is wide enough to distribute at all. A track has
        /// to hold its own reserved band plus the gap that keeps a wide
        /// value out of the column to its left; below that width there is
        /// nothing to distribute and the caller falls back to the packed
        /// right-to-left stack, which fits in less. On a narrow panel a
        /// legible cramped table beats an evenly spaced illegible one.
        /// </summary>
        public static bool FitsDistributed(int trackSpan, int trackCount, int widestBand, int gap)
        {
            return trackCount > 0 && trackSpan / trackCount >= widestBand + gap;
        }
    }
}
