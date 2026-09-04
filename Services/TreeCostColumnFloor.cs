namespace TaimisToolbench.Services
{
    /// <summary>
    /// Keeps the recipe tree's cost sub-columns from NARROWING while one
    /// plan is on screen (Blish-free, unit-testable).
    /// <para>
    /// TreeCostColumnMath.ScanColumns measures the widest value each
    /// sub-column holds; TreeSectionController turns that total into the
    /// cost column's reserved width, which
    /// PlanRelayoutMath.ComputeTreeColumnEdges subtracts from the panel
    /// width to place the decision pills. So a narrower scan moves the
    /// pill column's left edge, on every row - and ignoring one row does
    /// exactly that when it owned the widest cost run.
    /// </para>
    /// <para>
    /// A scan may therefore widen the reservation and never narrow it.
    /// The floor belongs to the PLAN, not to the current decision
    /// toggles: a fresh Generate starts again from
    /// CostColumnWidths.Empty. Widening still moves the pills, but is
    /// one-way, so a decision toggled back and forth settles.
    /// </para>
    /// </summary>
    internal static class TreeCostColumnFloor
    {
        /// <summary>
        /// The widths to reserve given what this plan has already
        /// reserved: the larger of the two in each sub-column
        /// independently, because each sub-column's own right edge is what
        /// the coin icons line up on (TreeCostColumnMath.ComputeEdges) -
        /// comparing totals would let a wide currency run pay for a
        /// narrowed gold band and slide that band's icons sideways.
        /// <para>
        /// The two ink extents are carried the same way. They must be
        /// carried at all - dropping WidestRowRunWidth left the "Cost"
        /// header centring over an ink extent of 0, i.e. right-aligned on
        /// the column edge - and they are widened rather than adopted
        /// because a row's reach back from that edge grows with the
        /// reserve it is measured against (TreeCostColumnMath.RowRuns
        /// computes them off the SCAN's bands, which the floor may widen
        /// afterwards), and because a one-way value keeps a re-solve that
        /// only removes ink eligible for TreeSectionController's in-place
        /// refresh.
        /// </para>
        /// </summary>
        public static TreeCostColumnMath.CostColumnWidths Widen(
            TreeCostColumnMath.CostColumnWidths floor, TreeCostColumnMath.CostColumnWidths scanned)
        {
            return new TreeCostColumnMath.CostColumnWidths(
                Max(floor.GoldTextWidth, scanned.GoldTextWidth),
                Max(floor.SilverTextWidth, scanned.SilverTextWidth),
                Max(floor.CopperTextWidth, scanned.CopperTextWidth),
                Max(floor.CurrencyRunWidth, scanned.CurrencyRunWidth),
                Max(floor.WidestRowRunWidth, scanned.WidestRowRunWidth),
                Max(floor.LeftmostInkReach, scanned.LeftmostInkReach));
        }

        /// <summary>
        /// Whether two width sets describe the same cost column - the test
        /// an in-place refresh gates on, so it is asked in one place
        /// rather than by each caller comparing fields. Every public field
        /// of CostColumnWidths participates, header ink extent included:
        /// an unequal one means chrome the refresh preserves rather than
        /// redraws is already wrong. A field added there and not added
        /// here fails TreeCostColumnFloorTests' field-count tripwire.
        /// </summary>
        public static bool Equal(
            TreeCostColumnMath.CostColumnWidths a, TreeCostColumnMath.CostColumnWidths b)
        {
            return a.GoldTextWidth == b.GoldTextWidth
                && a.SilverTextWidth == b.SilverTextWidth
                && a.CopperTextWidth == b.CopperTextWidth
                && a.CurrencyRunWidth == b.CurrencyRunWidth
                && a.WidestRowRunWidth == b.WidestRowRunWidth
                && a.LeftmostInkReach == b.LeftmostInkReach;
        }

        private static int Max(int a, int b)
        {
            return a > b ? a : b;
        }
    }
}
