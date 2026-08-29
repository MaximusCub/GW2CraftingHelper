namespace TaimisToolbench.Models
{
    /// <summary>
    /// How the plan values materials the player already owns and that
    /// inventory reduction consumes. An owned unit is always consumed
    /// first, at zero acquisition cost - this enum never makes an owned
    /// unit "cost" anything.
    /// <para>
    /// Valued runs a zero-owned decision pass BEFORE the real solve: a node
    /// is excluded from crafting when buying it outright costs less than
    /// 85% of what its own components would cost to buy fresh (gw2e's
    /// getCheaperToBuyItemIds). Its reduction is then decision-GUIDED, not
    /// merely gated, and it deducts owned materials' trading-post sell
    /// opportunity cost from CraftingProfit. Free falls back to the legacy
    /// primary-recipe-option heuristic unchanged.
    /// </para>
    /// <para>
    /// All of the above is inert unless an account snapshot actually drove
    /// reduction (CraftingPlanPipeline's own gate), whatever the value.
    /// Derivation: docs/ARCHITECTURE.md section 8.2.
    /// </para>
    /// </summary>
    internal enum OwnMaterialsMode
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
