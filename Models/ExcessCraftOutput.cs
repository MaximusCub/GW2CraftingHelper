namespace TaimisToolbench.Models
{
    /// <summary>
    /// One item's
    /// aggregated crafting surplus across every Decision == Craft
    /// occurrence in the display tree - see
    /// Services/ExcessCraftOutputCalculator.Apply, which is the sole
    /// producer of this list (CraftingPlanResult.ExcessCraftOutputs).
    /// Mirrors UsedMaterial's shape/placement precedent: a pure, additive
    /// post-solve annotation, never fed back into any decision, cost, or
    /// total (same "advisory only" contract as
    /// CraftingPlanResult.MaterialOpportunityCost - see that field's own
    /// doc comment).
    /// </summary>
    internal class ExcessCraftOutput
    {
        public int ItemId { get; set; }

        // Sum, over every Craft occurrence of ItemId, of
        // (CraftsNeeded * RecipeExpectedOutputCount - Quantity) where
        // positive - the EV basis, not the nominal RecipeOutputCount (see
        // ExcessCraftOutputCalculator's finding-1 comment for why: using
        // RecipeOutputCount fabricates a large fake surplus for a
        // fractional-EV recipe like Mystic Clover). Always > 0 - a
        // non-positive total is never added to the list at all
        // (ExcessCraftOutputCalculator only emits items with real
        // surplus).
        public int ExcessQuantity { get; set; }

        // Net-of-TP-fee coin value of instant-selling ExcessQuantity units
        // (TradingPostMath.NetSaleRevenue), using the same SellInstant
        // price basis SellSideEconomics already uses for reclaim math. Null
        // when unpriced (no live sell price) OR IsAccountBound is true - an
        // account-bound item can never be sold on the Trading Post at all,
        // so it has no reclaim value to advertise regardless of price data.
        public long? ReclaimValue { get; set; }

        // True when ItemMetadata.IsAccountBound is true for this item - the
        // surplus is stranded (unsellable, unusable outside this account)
        // rather than reclaimable.
        public bool IsAccountBound { get; set; }
    }
}
