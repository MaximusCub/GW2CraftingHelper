namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// W4B (vendor cost-component leaves): a single TP-valued Item cost
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
    public class VendorItemCostLine
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public long GoldValue { get; set; }

        // AUDIT ROW 20/38 review-fix (DISPLAY CAVEAT gap): true when this
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
