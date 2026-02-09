namespace GW2CraftingHelper.Models
{
    public class ItemPrice
    {
        public int ItemId { get; set; }

        /// <summary>
        /// Cost to buy instantly from the lowest sell listing (copper).
        /// Sourced from sells.unit_price in the GW2 API.
        /// 0 means no sell listings exist.
        /// </summary>
        public int BuyInstant { get; set; }

        /// <summary>
        /// Revenue from selling instantly to the highest buy order (copper).
        /// Sourced from buys.unit_price in the GW2 API.
        /// 0 means no buy orders exist.
        /// </summary>
        public int SellInstant { get; set; }
    }
}
