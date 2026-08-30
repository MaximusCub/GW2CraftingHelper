using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Column and height arithmetic for one Log tab row: where the message
    /// column starts, how much width it may occupy given the row's current
    /// width and the measured width of the widest possible prefix, and how
    /// tall the row is once that message has wrapped.
    /// Blish-free (plain ints - the font measuring that produces
    /// <c>fullPrefixWidth</c> stays in the view) so the degenerate-width
    /// behavior this has to get right - a narrow window must never leave a
    /// zero-width message column, which would blank every row - is pinned by
    /// tests rather than only observable live.
    /// </summary>
    internal static class LogRowLayout
    {
        /// <summary>Gap between the prefix column and the message column.</summary>
        public const int MessageGap = 8;

        /// <summary>Right-hand padding kept clear inside the row.</summary>
        public const int RightPad = 8;

        /// <summary>
        /// Floor for the message column. Below this the message would wrap
        /// to one character a line and the row would read as a vertical
        /// stripe rather than as text; the row's own panel clips whatever
        /// overflows instead, exactly as an over-long line did before the
        /// split.
        /// </summary>
        public const int MinMessageWidth = 40;

        /// <summary>
        /// Physical lines one message may wrap to before the tail is
        /// ellipsized into the last of them and the row's tooltip carries
        /// the rest. The cap is not about cost - the rows are built, not
        /// virtualised, and a wrap costs one measurement per word - but
        /// about one entry's share of the tab: four lines is about 80px at
        /// the module's body face, and the Log viewport at
        /// WindowSizing.MinWindowHeight holds roughly twenty-five
        /// single-line rows (INFERRED from that constant, not measured
        /// live). Uncapped, one stack trace pasted into a log line would be
        /// the only thing on screen.
        /// </summary>
        public const int MaxMessageLines = 4;

        /// <summary>
        /// Height of a row whose message wrapped to
        /// <paramref name="messageLineCount"/> lines: the single-line row
        /// height the tab has always used, plus one line advance for each
        /// line after the first. The clearance the single-line height
        /// carries below the glyphs is therefore counted once, at the
        /// bottom of the row, and not once per line.
        /// <para>
        /// Clamped to <see cref="MaxMessageLines"/> as well as to one, so a
        /// caller that wrapped without the cap cannot produce a row taller
        /// than the cap admits.
        /// </para>
        /// </summary>
        public static int RowHeight(int messageLineCount, int singleLineHeight, int lineAdvance)
        {
            int lines = messageLineCount < 1 ? 1 : messageLineCount;
            if (lines > MaxMessageLines)
            {
                lines = MaxMessageLines;
            }

            int height = (singleLineHeight > 0 ? singleLineHeight : 0)
                + ((lines - 1) * (lineAdvance > 0 ? lineAdvance : 0));
            return height > 0 ? height : 0;
        }

        /// <summary>
        /// The prefix never takes more than half a narrow row - past that
        /// point the timestamp column would push the message off-row
        /// entirely. At the module's normal widths the cap is inactive and
        /// every row shares the same aligned prefix column.
        /// </summary>
        public static int PrefixWidth(int fullPrefixWidth, int rowWidth)
        {
            if (fullPrefixWidth < 0)
            {
                return 0;
            }

            int cap = rowWidth / 2;
            return cap > 0 && cap < fullPrefixWidth ? cap : fullPrefixWidth;
        }

        public static int MessageX(int prefixWidth)
        {
            return Math.Max(prefixWidth, 0) + MessageGap;
        }

        public static int MessageMaxWidth(int rowWidth, int prefixWidth)
        {
            int available = rowWidth - MessageX(prefixWidth) - RightPad;
            return available < MinMessageWidth ? MinMessageWidth : available;
        }

        /// <summary>
        /// True when an ELLIPSIZED column is showing its whole string and
        /// has not narrowed since that string was fitted, so it cannot have
        /// started to overflow and the MeasureString binary search inside
        /// EllipsizeToWidth can be skipped. Not the memo for the WRAPPED
        /// message column: widening that one changes its answer too, by
        /// pulling a word back up a line, so it memoises on exact width
        /// equality instead.
        /// <para>
        /// Against the width the TEXT was fitted at, NOT the control's: a
        /// resize drag moves the Log tab's columns live and re-fits their
        /// text only at settle, so in between the control already carries
        /// the new width while its string still belongs to the old one, and
        /// comparing against the control would skip exactly the re-fit a
        /// narrowing drag needs. Below zero means "never fitted".
        /// </para>
        /// </summary>
        public static bool KeepsFitting(bool showingWholeString, int fittedWidth, int newWidth)
        {
            return showingWholeString && fittedWidth >= 0 && newWidth >= fittedWidth;
        }
    }
}
