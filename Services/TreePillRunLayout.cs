namespace TaimisToolbench.Services
{
    /// <summary>
    /// Horizontal placement inside one tree row's fixed-width decision
    /// column (Blish-free, unit-testable): a left-packed run of source
    /// markers that gets the whole column, and the "Source" header centred
    /// over what those runs actually cover.
    /// </summary>
    internal static class TreePillRunLayout
    {
        /// <summary>
        /// Width the "Source" header centres over on one row, measured
        /// from the decision column's left rule: the row's flowed run of
        /// source markers. The ignore button is not part of it and is not
        /// in this column - it draws in the row's own trailing action
        /// column (PlanRelayoutMath.TreeActionColumnWidth).
        /// </summary>
        public static int HeaderInkWidth(int pillColX, int runRightEdge)
        {
            return runRightEdge > pillColX ? runRightEdge - pillColX : 0;
        }

        /// <summary>
        /// Left edge of the "Source" header, centred over the INK the
        /// pill runs cover rather than over the column's fixed reserve
        /// (PlanRelayoutMath.TreePillColumnWidth). The pills stay
        /// left-ruled at <paramref name="pillColX"/>, so the ink starts
        /// there and <paramref name="inkRunWidth"/> is how far right the
        /// widest row on screen reaches.
        /// <para>
        /// The reserve overstates the ink by most of its width on a
        /// freshly generated tree, which shows only plan roots and their
        /// two or three badges: measured 2026-08-28, a header centred at
        /// x=797 over a badge run occupying 700-765. Derivation:
        /// docs/ARCHITECTURE.md section S1.2.
        /// </para>
        /// </summary>
        public static int HeaderX(
            int pillColX, int inkRunWidth, int headerWidth,
            JustifiedColumnTracks.HeaderRoom room)
        {
            return JustifiedColumnTracks.CenteredOverContent(
                pillColX, inkRunWidth, headerWidth, room);
        }
    }
}
