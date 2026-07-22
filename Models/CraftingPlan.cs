using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class CraftingPlan
    {
        public int TargetItemId { get; set; }
        public int TargetQuantity { get; set; }
        public List<PlanStep> Steps { get; set; } = new List<PlanStep>();
        public long TotalCoinCost { get; set; }
        public List<CurrencyCost> CurrencyCosts { get; set; } = new List<CurrencyCost>();

        // Vendor-capped items whose merged demand exceeds the winning
        // offer's daily/weekly purchase cap - informational only, per
        // gw2efficiency parity (M34-B1 #3). Never affects Steps/
        // TotalCoinCost/CurrencyCosts above. Empty (never null) when no
        // seeded offer's cap is exceeded; caps are live data as of M37
        // (689 of 53,530 seeded vendor offers carry a real DailyCap/
        // WeeklyCap - see docs/KNOWN-ISSUES.md "FIXED in M37 (cap seeding)").
        public List<TimegatedItem> TimegatedItems { get; set; } = new List<TimegatedItem>();
    }
}
