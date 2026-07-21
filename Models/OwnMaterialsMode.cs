namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// How the plan values materials the player already owns and that
    /// inventory reduction consumes. Reduction's OWN mechanics are
    /// UNCHANGED in both modes: an owned unit is always consumed first, at
    /// zero acquisition cost - this enum never makes an owned unit "cost"
    /// anything. What it DOES control (M34-B2a #3, gw2efficiency parity -
    /// see OwnedMaterialsForceBuyPrePass):
    /// 1. Valued runs a force-buy pre-pass BEFORE the real solve: a node is
    ///    excluded from crafting when buying it outright costs less than
    ///    85% of what its own components would cost to buy fresh (gw2e's
    ///    getCheaperToBuyItemIds) - i.e. a bad trade even though the
    ///    components happen to be in-hand right now.
    /// 2. Valued also deducts owned materials' trading-post sell
    ///    opportunity cost from CraftingProfit (the original M28 behavior).
    /// Both only take effect when an account snapshot actually drove
    /// reduction (CraftingPlanPipeline's own gate) - with no snapshot, this
    /// setting is inert regardless of its value.
    /// </summary>
    public enum OwnMaterialsMode
    {
        /// <summary>Owned materials are free; no force-buy pre-pass runs, and profit ignores their opportunity cost.</summary>
        Free = 0,

        /// <summary>
        /// The force-buy pre-pass runs, and owned materials are valued at
        /// their instant-sell opportunity cost (net of Trading Post fees);
        /// CraftingProfit is reduced by that amount.
        /// </summary>
        Valued = 1
    }
}
