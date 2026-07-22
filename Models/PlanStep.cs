using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class PlanStep
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public AcquisitionSource Source { get; set; }
        public long UnitCost { get; set; }
        public long TotalCost { get; set; }
        public int RecipeId { get; set; }

        // Non-coin currency cost of this step (Source == BuyFromVendor
        // only), already scaled/aggregated to this step's Quantity. Null
        // for every other step. See SolverDecision.VendorCurrencyCosts.
        public List<CostLine> VendorCurrencyCosts { get; set; }

        // Winning vendor offer's batch shape (Source == BuyFromVendor only,
        // and only when every tree occurrence merged into this step used
        // the IDENTICAL offer - see VendorBatchSolver.FinalizeVendorBatches):
        // OutputCount is the offer's own per-purchase output count, and
        // VendorOfferCurrencyCostLinesPerBatch is that offer's UNSCALED
        // non-coin currency cost for ONE purchase (not this step's
        // aggregated total). Used to derive the shopping list's currency
        // "Each" cell as the offer's true per-unit rate (M34-B1 #2) rather
        // than a truncated total/Quantity average. OutputCount stays 0 (and
        // VendorOfferCurrencyCostLinesPerBatch null) when not applicable -
        // a non-vendor step, or a vendor step whose occurrences resolved to
        // more than one distinct offer.
        public int VendorOfferOutputCount { get; set; }
        public List<CostLine> VendorOfferCurrencyCostLinesPerBatch { get; set; }
    }
}
