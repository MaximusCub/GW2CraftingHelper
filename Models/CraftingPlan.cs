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
    }
}
