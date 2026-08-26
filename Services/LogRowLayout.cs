using System;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Column arithmetic for one Log tab row: where the message column
    /// starts and how much width it may occupy, given the row's current
    /// width and the measured width of the widest possible prefix.
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
        /// Floor for the message column. Below this the message would
        /// ellipsize to nothing and the row would read as blank rather than
        /// as truncated; the row's own panel clips whatever overflows
        /// instead, exactly as an over-long line did before the split.
        /// </summary>
        public const int MinMessageWidth = 40;

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
        /// True when a column is showing its whole string and has not
        /// narrowed since that string was fitted, so it cannot have started
        /// to overflow and the MeasureString binary search inside
        /// EllipsizeToWidth can be skipped.
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
