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
    }
}
