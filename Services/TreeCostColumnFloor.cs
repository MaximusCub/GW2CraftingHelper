namespace TaimisToolbench.Services
{
    /// <summary>
    /// Keeps the recipe tree's cost sub-columns from NARROWING while one
    /// plan is on screen (Blish-free, unit-testable).
    /// <para>
    /// TreeCostColumnMath.ScanColumns measures the widest value each
    /// sub-column holds, and TreeSectionController turns that total into
    /// the cost column's reserved width - which
    /// PlanRelayoutMath.ComputeTreeColumnEdges subtracts from the panel
    /// width to place the decision pills. So a scan that comes back
    /// narrower moves the pill column's left edge RIGHT, on every row of
    /// the tree. Ignoring one row does exactly that whenever that row
    /// owned the widest currency run or gold figure: the user clicks one
    /// pill and the whole pill run jumps sideways, including on rows the
    /// click never touched.
    /// </para>
    /// <para>
    /// So a scan may widen the reservation and never narrow it, per
    /// sub-column, for as long as the plan is rendered. The floor is a
    /// property of the PLAN, not of the current decision toggles: a fresh
    /// Generate (or a restored plan) starts from
    /// CostColumnWidths.Empty and re-derives everything. Widening still
    /// moves the pills - a re-solve can introduce a currency run no
    /// earlier state had - but it is one-way, so a toggled-back-and-forth
    /// decision settles instead of oscillating.
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
        /// </summary>
        public static TreeCostColumnMath.CostColumnWidths Widen(
            TreeCostColumnMath.CostColumnWidths floor, TreeCostColumnMath.CostColumnWidths scanned)
        {
            return new TreeCostColumnMath.CostColumnWidths(
                Max(floor.GoldTextWidth, scanned.GoldTextWidth),
                Max(floor.SilverTextWidth, scanned.SilverTextWidth),
                Max(floor.CopperTextWidth, scanned.CopperTextWidth),
                Max(floor.CurrencyRunWidth, scanned.CurrencyRunWidth));
        }

        /// <summary>
        /// Whether two width sets reserve the same sub-columns - the test
        /// an in-place refresh gates on, so it is asked in one place
        /// rather than by each caller comparing four fields.
        /// </summary>
        public static bool Equal(
            TreeCostColumnMath.CostColumnWidths a, TreeCostColumnMath.CostColumnWidths b)
        {
            return a.GoldTextWidth == b.GoldTextWidth
                && a.SilverTextWidth == b.SilverTextWidth
                && a.CopperTextWidth == b.CopperTextWidth
                && a.CurrencyRunWidth == b.CurrencyRunWidth;
        }

        private static int Max(int a, int b)
        {
            return a > b ? a : b;
        }
    }
}
