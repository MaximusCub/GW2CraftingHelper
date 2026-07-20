namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// How the plan values materials the player already owns and that
    /// inventory reduction consumes. Reduction itself is UNCHANGED in
    /// both modes: owned materials are always consumed first, at zero
    /// acquisition cost. This only controls whether their trading-post
    /// opportunity cost (what selling them would have netted) is
    /// deducted from CraftingProfit - matching gw2efficiency's default
    /// behavior of valuing owned materials at their sell price instead
    /// of treating them as free.
    /// </summary>
    public enum OwnMaterialsMode
    {
        /// <summary>Owned materials are free; profit ignores their opportunity cost.</summary>
        Free = 0,

        /// <summary>
        /// Owned materials are valued at their instant-sell opportunity
        /// cost (net of Trading Post fees); CraftingProfit is reduced by
        /// that amount.
        /// </summary>
        Valued = 1
    }
}
