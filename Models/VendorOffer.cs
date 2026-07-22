using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class VendorOffer
    {
        public string OfferId { get; set; }
        public int OutputItemId { get; set; }
        public int OutputCount { get; set; }
        public List<CostLine> CostLines { get; set; } = new List<CostLine>();
        public string MerchantName { get; set; }
        public List<string> Locations { get; set; } = new List<string>();
        public int? DailyCap { get; set; }
        public int? WeeklyCap { get; set; }

        // M37 (KNOWN-ISSUES #24, gw2e parity): the Homestead Refinement
        // efficiency tier (0/1/2) this specific offer row corresponds to,
        // or null for every non-Homestead-Refinement offer. Additive,
        // backward-compatible - existing offers deserialize with this null.
        // Wiki-sourced per-row quantities already bake in the game's own
        // per-material tier anomalies (Onion/Potato/Iron Ore), so tagging
        // existing rows rather than collapsing them into a formula avoids
        // re-deriving those bugs in code - see PlanSolver.EvaluateVendorOffers.
        public int? HomesteadTier { get; set; }
    }
}
