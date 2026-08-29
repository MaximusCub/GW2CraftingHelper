namespace TaimisToolbench.Services
{
    /// <summary>
    /// Horizontal placement inside one tree row's fixed-width decision-pill
    /// column (Blish-free, unit-testable): a left-packed leading run, and
    /// the IGNORE toggle in a slot reserved on the column's right edge.
    /// <para>
    /// The toggle is anchored rather than flowed because clicking it
    /// changes which pills the row HAS: an ignored node re-solves to an
    /// owned one, so its source pills are gone on the next render. Flowed,
    /// the toggle moved out from under the cursor that had just clicked it
    /// and the next click landed on the row instead, which expands or
    /// collapses the node. Anchored, its slot is the same rectangle in both
    /// states - <see cref="ReservedSlotWidth"/> sizes it for the longer of
    /// the two texts, so the pill neither moves nor resizes when "IGNORE"
    /// becomes "IGNORED".
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
        /// x of the reserved slot, right-aligned on the pill column's own
        /// right edge. Independent of everything to its left, which is the
        /// whole point.
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
        /// Left edge of the "Source" header, centred over the INK the
        /// pill runs cover rather than over the column's fixed reserve
        /// (PlanRelayoutMath.TreePillColumnWidth). The pills stay
        /// left-ruled at <paramref name="pillColX"/>, so the ink starts
        /// there and <paramref name="inkRunWidth"/> is how far right the
        /// widest row on screen reaches.
        /// <para>
        /// The two rules agree once any row reserves the anchored IGNORE
        /// slot, which pins to the column's right edge - but a freshly
        /// generated tree shows only plan roots, which never get that
        /// toggle (DecisionPillPlanner.AppendOwnershipPills), and there
        /// the reserve overstates the ink by most of its width: measured
        /// 2026-08-28, a header centred at x=797 over a badge run
        /// occupying 700-765. Derivation: docs/ARCHITECTURE.md section
        /// S1.2.
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
