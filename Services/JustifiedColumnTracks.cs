namespace TaimisToolbench.Services
{
    /// <summary>
    /// THE distribution law every justified table in the module shares: a
    /// row's span is divided into N EQUAL tracks and each data column is
    /// CENTRED on its own track, so the columns spread across the panel
    /// instead of stacking against its right-hand edge.
    ///
    /// <para>
    /// A column's header and its cells centre on the same track, which is
    /// what makes a header sit over the values it names. Right-aligning
    /// both to a shared edge also lines them up, but only at that edge:
    /// a short header over wide cells then reads as belonging to the
    /// column on its right. <see cref="RightEdge"/> is kept for the bands
    /// that genuinely pin to an edge (buttons, coin runs that must not
    /// ragged-right against each other).
    /// </para>
    ///
    /// <para>
    /// It was written for the currency table, whose packed stack left
    /// ~1000px of nothing between a currency's name and its first number
    /// with no anchor for the eye between them. The Plan History table had
    /// the same shape and the same complaint, so the arithmetic lives here
    /// rather than in two places: a second copy is how two tables that are
    /// supposed to read alike drift apart.
    /// </para>
    ///
    /// <para>
    /// TRACK COUNT is one per data column plus ONE for the label, which is
    /// the leftmost and the only flexing element. Data column i (0-based)
    /// right-aligns on <c>RightEdge(..., i + 1)</c>; the label fills from
    /// startX up to the first data column's reserved band, so it absorbs
    /// whatever that column does not need.
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
