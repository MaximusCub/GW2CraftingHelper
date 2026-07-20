namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure column-edge arithmetic (Blish-free, unit-testable) for the
    /// shopping list's Amount/Each/Total table columns. The Each and Total
    /// price columns are right-aligned and grow leftward from a fixed right
    /// edge; their reserved band widths are derived per-render from the
    /// widest actual coin-value string in each column (measured in the view
    /// via BitmapFont.MeasureString, which is Blish-bound and therefore not
    /// tested here), clamped to fixed minimums so short/low-value lists
    /// don't look cramped. See CraftingPlanView.CreateShoppingListBody for
    /// the pre-scan that produces maxEachWidth/maxTotalWidth.
    /// </summary>
    public static class ShoppingColumnMath
    {
        public const int TotalMinWidth = 150;
        public const int EachMinWidth = 110;
        public const int ColumnGap = 20;

        public struct ColumnEdges
        {
            public int TotalRightEdge;
            public int EachRightEdge;
            public int QtyRightEdge;
        }

        /// <summary>
        /// Right edges for the Amount/Each/Total columns, derived
        /// right-to-left off totalRightEdge so header and data rows stay in
        /// lockstep by construction (both are handed the same ColumnEdges
        /// instance for a given render). Total's band width is
        /// max(TotalMinWidth, maxTotalWidth); Each's band width is
        /// max(EachMinWidth, maxEachWidth) - each band plus a ColumnGap is
        /// reserved to its right neighbor's left.
        /// </summary>
        public static ColumnEdges ComputeEdges(int totalRightEdge, int maxEachWidth, int maxTotalWidth)
        {
            int totalColWidth = maxTotalWidth > TotalMinWidth ? maxTotalWidth : TotalMinWidth;
            int eachColWidth = maxEachWidth > EachMinWidth ? maxEachWidth : EachMinWidth;

            int eachRightEdge = totalRightEdge - totalColWidth - ColumnGap;
            int qtyRightEdge = eachRightEdge - eachColWidth - ColumnGap;

            return new ColumnEdges
            {
                TotalRightEdge = totalRightEdge,
                EachRightEdge = eachRightEdge,
                QtyRightEdge = qtyRightEdge
            };
        }
    }
}
