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
    }
}
