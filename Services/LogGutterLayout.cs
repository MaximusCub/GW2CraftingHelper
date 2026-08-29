using System.Collections.Generic;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The Log tab's scannable gutter (Blish-free, unit-testable): a Time
    /// band and a Tag band, each starting at a fixed x on every row, with
    /// the message taking everything left over.
    /// <para>
    /// The Tag band tracks content rather than a worst-case template, which
    /// only works because the view holds the widest rendered tag as a
    /// monotonic high-water mark per render generation - the incremental
    /// append path sees only the new entries, so a width derived from what
    /// IT can see would otherwise drift from what a full rebuild produced
    /// (see LogTabContent.FullPrefixWidth).
    /// Derivation: docs/ARCHITECTURE.md section S1.2.
    /// </para>
    /// <para>
    /// The message half stays <see cref="LogRowLayout"/>'s: its floor and
    /// its half-row cap now apply to the SUM of the two bands.
    /// </para>
    /// </summary>
    internal static class LogGutterLayout
    {
        /// <summary>Left gutter the tab's content starts at. Four things on
        /// this tab used to sit at x=0.</summary>
        public const int GutterX = 16;

        public const int TimeToTagGap = 8;

        /// <summary>The module's one column gap, shared with the message
        /// column so the three bands read as one rhythm.</summary>
        public const int TagToMessageGap = LogRowLayout.MessageGap;

        /// <summary>
        /// The Time band: a genuine constant, the widest
        /// "[LEVEL] &lt;stamp&gt;" over the level names. Unlike the tag, this
        /// cannot be derived from content - every level is one of a closed
        /// set, so the worst case IS the band and no row can widen it.
        /// </summary>
        public static int TimeBand(IReadOnlyList<int> perLevelWidths)
        {
            int band = 0;
            if (perLevelWidths == null)
            {
                return band;
            }

            for (int i = 0; i < perLevelWidths.Count; i++)
            {
                if (perLevelWidths[i] > band)
                {
                    band = perLevelWidths[i];
                }
            }

            return band;
        }

        /// <summary>
        /// The Tag band: max(widest tag actually rendered this generation,
        /// its own header label) - the header-floored band rule the plan
        /// tables needed once headers went to 20 bold, since a short tag run
        /// under a wider header would otherwise leave the header overhanging
        /// the message column.
        /// </summary>
        public static int TagBand(int widestRenderedTagWidth, int headerLabelWidth)
        {
            int band = widestRenderedTagWidth > headerLabelWidth
                ? widestRenderedTagWidth
                : headerLabelWidth;
            return band > 0 ? band : 0;
        }

        /// <summary>Total the gutter wants before the message column - the
        /// single number <see cref="LogRowLayout.PrefixWidth"/>'s half-row
        /// cap is applied to, so the cap governs the SUM of the two bands
        /// rather than either one.</summary>
        public static int FullGutterWidth(int timeBand, int tagBand)
        {
            return GutterX + Clamp(timeBand) + TimeToTagGap + Clamp(tagBand);
        }

        /// <summary>Resolved x and width of each of the row's three columns.</summary>
        public readonly struct Bands
        {
            public readonly int TimeX;
            public readonly int TimeWidth;
            public readonly int TagX;
            public readonly int TagWidth;
            public readonly int MessageX;
            public readonly int MessageWidth;

            public Bands(int timeX, int timeWidth, int tagX, int tagWidth, int messageX, int messageWidth)
            {
                TimeX = timeX;
                TimeWidth = timeWidth;
                TagX = tagX;
                TagWidth = tagWidth;
                MessageX = messageX;
                MessageWidth = messageWidth;
            }
        }

        /// <summary>
        /// The row's three columns at this width. Past
        /// <see cref="LogRowLayout.PrefixWidth"/>'s half-row cap the
        /// shortfall comes out of the TAG band first and the Time band only
        /// once the tag is gone: the timestamp is the column a reader
        /// navigates a log by, and a tag is already repeated on every row of
        /// its own kind.
        /// </summary>
        public static Bands Compute(int rowWidth, int timeBand, int tagBand)
        {
            int time = Clamp(timeBand);
            int tag = Clamp(tagBand);

            int full = FullGutterWidth(time, tag);
            int shortfall = full - LogRowLayout.PrefixWidth(full, rowWidth);
            if (shortfall > 0)
            {
                int fromTag = shortfall < tag ? shortfall : tag;
                tag -= fromTag;
                time = Clamp(time - (shortfall - fromTag));
            }

            int tagX = GutterX + time + TimeToTagGap;
            int gutter = tagX + tag;
            int messageX = LogRowLayout.MessageX(gutter);

            return new Bands(
                GutterX, time, tagX, tag,
                messageX, LogRowLayout.MessageMaxWidth(rowWidth, gutter));
        }

        private static int Clamp(int value)
        {
            return value > 0 ? value : 0;
        }
    }
}
