namespace GW2CraftingHelper.Models
{
    public enum TimegatedCapType
    {
        Daily,
        Weekly
    }

    /// <summary>
    /// Informational-only record of an item whose winning vendor offer
    /// carries a positive purchase cap that the plan's total (merged)
    /// demand exceeds. Matches gw2efficiency's own treatment (M34 R3
    /// research, dailyCooldowns.ts): a cap NEVER gates offer eligibility or
    /// re-routes the solver to a different acquisition source - it is
    /// surfaced purely as a "this will take you more than one day/week"
    /// notice, computed once per merged shopping-list row after the plan is
    /// fully solved. See VendorBatchSolver.FinalizeVendorBatches.
    /// </summary>
    public class TimegatedItem
    {
        public int ItemId { get; set; }
        public TimegatedCapType CapType { get; set; }
        public int CapValue { get; set; }

        /// <summary>Total vendor purchases the merged plan needs (ceil(quantity / offer.OutputCount)).</summary>
        public int NeededCount { get; set; }
    }
}
