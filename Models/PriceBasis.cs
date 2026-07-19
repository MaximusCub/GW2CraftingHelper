namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Which Trading Post price is used to cost material acquisition.
    /// </summary>
    public enum PriceBasis
    {
        /// <summary>
        /// Buy instantly from the lowest sell listing (sells.unit_price).
        /// Immediate but more expensive.
        /// </summary>
        InstantBuy = 0,

        /// <summary>
        /// Place buy orders at the highest current buy order
        /// (buys.unit_price). Cheaper but not instant.
        /// </summary>
        BuyOrder = 1
    }
}
