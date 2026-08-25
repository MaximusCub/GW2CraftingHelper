namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// How the plan values materials the player already owns and that
    /// inventory reduction consumes. An owned unit is always consumed
    /// first, at zero acquisition cost - this enum never makes an owned
    /// unit "cost" anything. What it DOES control:
    /// 1. Valued runs a zero-owned decision pass BEFORE the real solve
    ///    (reusing the same force-buy pre-pass baseline): a node is
    ///    excluded from crafting when buying it outright costs less than
    ///    85% of what its own components would cost to buy fresh (gw2e's
    ///    getCheaperToBuyItemIds) - i.e. a bad trade even though the
    ///    components happen to be in-hand right now.
    /// 2. Valued's reduction is now DECISION-GUIDED, not merely gated: owned
    ///    stock only ever discounts the recipe option that zero-owned
    ///    decision pass actually chose to Craft - never a never-chosen
    ///    option or a node decided Buy/Vendor - so owned stock can no
    ///    longer flip a decision toward a chain that is worse at market
    ///    prices, and a Buy-decided node's ingredients are never
    ///    phantom-consumed into UsedMaterials.
    /// 3. Valued also deducts owned materials' trading-post sell
    ///    opportunity cost from CraftingProfit, computed from the
    ///    decision-guided UsedMaterials list.
    /// In Free mode, reduction falls back to the legacy primary-recipe-
    /// option heuristic unchanged.
    /// All of the above only takes effect when an account snapshot actually
    /// drove reduction (CraftingPlanPipeline's own gate) - with no
    /// snapshot, this setting is inert regardless of its value.
    /// </summary>
    public enum OwnMaterialsMode
    {
        /// <summary>Owned materials are free; no zero-owned decision pass runs, reduction uses the legacy primary-option heuristic, and profit ignores their opportunity cost.</summary>
        Free = 0,

        /// <summary>
        /// The zero-owned decision pass runs (force-buy guard + decision-
        /// invariant reduction guide), and owned materials are valued at
        /// their instant-sell opportunity cost (net of Trading Post fees);
        /// CraftingProfit is reduced by that amount.
        /// </summary>
        Valued = 1,
    }
}
