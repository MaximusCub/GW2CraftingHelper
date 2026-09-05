namespace TaimisToolbench.Services
{
    /// <summary>
    /// Where one recipe-tree row draws its IGNORE key (Blish-free,
    /// unit-testable).
    /// <para>
    /// The key pinned to the decision-pill column's own right edge
    /// (TreePillRunLayout.AnchoredSlotX). That put it hard against the
    /// pills of a row with a wide run while the reserve the cost column
    /// holds above its ink stayed empty on the key's other side: measured
    /// at a 1230px window, 8px of clearance to its left and 183px to its
    /// right. Placed here, the key centres in the band its own row's run
    /// and the cost ink leave it, so the two clearances match.
    /// </para>
    /// </summary>
    internal static class TreeIgnoreKeyPlacement
    {
        /// <summary>
        /// x for the key on a row whose flowed pill run ends at
        /// <paramref name="runRightEdge"/>: centred between that run and
        /// <paramref name="costInkX"/>, the leftmost pixel any row's cost
        /// value reaches.
        /// <para>
        /// Never nearer the run than <paramref name="gap"/>, and never
        /// nearer the cost ink than TreePillColumnMath.TrailingClearance,
        /// which is the clearance the pill column already keeps from that
        /// column. A row leaving no band between the two keeps the pinned
        /// x, which is where the key has always been.
        /// </para>
        /// <para>
        /// The run is fitted against the PINNED key's budget
        /// (TreePillRunLayout.LeadingLimitX), never against the answer
        /// here, so no row loses a pill to where the key ends up and the
        /// two cannot chase each other.
        /// </para>
        /// </summary>
        public static int SlotX(
            int columnRightEdge, int costInkX, int keyWidth, int gap, int runRightEdge)
        {
            int nearest = runRightEdge + (gap > 0 ? gap : 0);
            int furthest = costInkX - TreePillColumnMath.TrailingClearance - keyWidth;
            if (furthest < nearest)
            {
                return TreePillRunLayout.AnchoredSlotX(columnRightEdge, keyWidth);
            }

            int centred = runRightEdge + ((costInkX - runRightEdge - keyWidth) / 2);
            if (centred < nearest)
            {
                return nearest;
            }

            return centred > furthest ? furthest : centred;
        }
    }
}
