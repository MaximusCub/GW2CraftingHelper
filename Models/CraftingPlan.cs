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
        // offer's daily/weekly purchase cap - informational only, never
        // affecting Steps/TotalCoinCost/CurrencyCosts. Empty (never null)
        // when no seeded offer's cap is exceeded.
        public List<TimegatedItem> TimegatedItems { get; set; } = new List<TimegatedItem>();
    }
}
