namespace TaimisToolbench.Models
{
    /// <summary>
    /// A single Item cost
    /// line of a winning BuyFromVendor decision, scaled to one tree
    /// occurrence's actual purchase quantity (unitsNeeded already applied -
    /// see VendorBatchSolver.EvaluateVendorOffers). GoldValue is captured
    /// at the EXACT SAME multiplication (Quantity * that item's unit TP
    /// price) VendorBatchSolver folds into the parent decision's coin
    /// total - never recomputed anywhere downstream - so a display-tree
    /// leaf built from this can never drift from what is already folded
    /// into the parent's own SubtreeCost. ItemId is internal-only, same as
    /// every other id in this module (never displayed - only the resolved
    /// name/icon reach the UI).
    /// </summary>
    internal class VendorItemCostLine
    {
        public int ItemId { get; set; }

        public int Quantity { get; set; }

        // Null for a BARTER line: an untradeable item with no TP price,
        // whose units ARE the cost. Nothing of it was folded into the
        // parent decision's coin total, so there is no gold figure to
        // report and a display leaf must render a blank cost cell rather
        // than a 0 - exactly what a non-coin currency leaf already does.
        // Any valuation such a line carries is decision-only and must
        // never reach this field (see Models/BarterItemDecisionDefaults.cs).
        public long? GoldValue { get; set; }

        // true when this
        // line's per-unit TP price (folded into GoldValue above) came from
        // this SAME item's NON-preferred TP side because the preferred
        // side had no listings - see PlanSolver.GetUnitPrice's 3-arg
        // overload, which VendorBatchSolver.EvaluateVendorOffers now calls
        // for every Item cost line. CraftingTreeBuilder.
        // BuildVendorCostComponentLeaves reads this to flag the resulting
        // component leaf's own PriceSideFellBack, the same way a plain
        // BuyFromTp node's fallback is already flagged.
        public bool PriceSideFellBack { get; set; }
    }
}
