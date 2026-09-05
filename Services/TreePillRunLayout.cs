namespace TaimisToolbench.Services
{
    /// <summary>
    /// Horizontal placement inside one tree row's fixed-width decision-pill
    /// column (Blish-free, unit-testable): a left-packed leading run, and
    /// a slot the run is fitted around for the IGNORE key.
    /// <para>
    /// The key does not flow after the pills, because clicking it changes
    /// which pills the row HAS: an ignored node re-solves to an owned one,
    /// so its source pills are gone on the next render. Flowed, the key
    /// moved out from under the cursor that had just clicked it and the
    /// next click landed on the row instead, which expands or collapses
    /// the node. It does not resize across that click either:
    /// <see cref="ReservedSlotWidth"/> sizes ONE slot for both faces, and
    /// the renderer draws one close key, plain or amber, rather than the
    /// words "IGNORE"/"IGNORED". Where the slot lands is
    /// Services/TreeIgnoreKeyPlacement; what holds it still across the
    /// click is Services/TreePillRunInkFloor.
    /// </para>
    /// </summary>
    internal static class TreePillRunLayout
    {
        /// <summary>
        /// Width of a slot that must hold either of two texts without
        /// changing size: the wider text plus the pill's own padding.
        /// Negative measurements are treated as 0 - a slot narrower than
        /// its padding is not a state any caller can render.
        /// </summary>
        public static int ReservedSlotWidth(int firstTextWidth, int secondTextWidth, int padding)
        {
            int widest = firstTextWidth > secondTextWidth ? firstTextWidth : secondTextWidth;
            if (widest < 0)
            {
                widest = 0;
            }

            return widest + (padding > 0 ? padding : 0);
        }

        /// <summary>
        /// x of the slot right-aligned on the pill column's own right
        /// edge: the budget every leading run is fitted against
        /// (<see cref="LeadingLimitX"/>), and the x the key keeps on a row
        /// that leaves no band to seat it in
        /// (Services/TreeIgnoreKeyPlacement).
        /// </summary>
        public static int AnchoredSlotX(int maxRightEdge, int reservedWidth)
        {
            return maxRightEdge - reservedWidth;
        }

        /// <summary>
        /// The right edge the leading run must fit before, so it cannot
        /// run into the reserved slot. With no slot reserved
        /// (<paramref name="reservedWidth"/> 0 - a row with no IGNORE
        /// toggle at all) the leading run keeps the whole column.
        /// <para>
        /// Not clamped against the column's start: PlanRelayoutMath.
        /// ComputePillFit draws its first pill whatever the budget says,
        /// and a row whose first pill alone exceeds the reduced budget is
        /// the same overrun it already resolves.
        /// </para>
        /// </summary>
        public static int LeadingLimitX(int maxRightEdge, int reservedWidth, int gap)
        {
            return reservedWidth > 0 ? maxRightEdge - reservedWidth - gap : maxRightEdge;
        }

        /// <summary>
        /// Width the "Source" header centres over on one row, measured
        /// from the pill column's left rule: the row's flowed pill run,
        /// and only that.
        /// <para>
        /// The IGNORE key beside the run is not part of it. The header
        /// names the sources the pills carry, and the key is a row action
        /// rather than a source; it is also seated per row now
        /// (Services/TreeIgnoreKeyPlacement), so counting it would put the
        /// header over whichever row happened to seat its key furthest
        /// right rather than over the pills.
        /// </para>
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
