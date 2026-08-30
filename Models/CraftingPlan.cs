using System.Collections.Generic;

namespace TaimisToolbench.Models
{
    internal class CraftingPlan
    {
        public int TargetItemId { get; set; }

        public int TargetQuantity { get; set; }

        public List<PlanStep> Steps { get; set; } = new List<PlanStep>();

        public long TotalCoinCost { get; set; }

        public List<CurrencyCost> CurrencyCosts { get; set; } = new List<CurrencyCost>();

        // Whole-plan barter item costs - the untradeable vendor tokens
        // whose units ARE the price. Not part of TotalCoinCost and not
        // convertible into it (see BarterItemCost), so a consumer reading
        // TotalCoinCost as the plan's whole price has to read this and
        // CurrencyCosts as well. Empty (never null) when no winning vendor
        // offer in the plan takes barter.
        public List<BarterItemCost> BarterItemCosts { get; set; } = new List<BarterItemCost>();

        // Vendor-capped items whose merged demand exceeds the winning
        // offer's daily/weekly purchase cap - informational only, never
        // affecting Steps/TotalCoinCost/CurrencyCosts. Empty (never null)
        // when no seeded offer's cap is exceeded.
        public List<TimegatedItem> TimegatedItems { get; set; } = new List<TimegatedItem>();
    }
}
