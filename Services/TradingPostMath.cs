using System;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Trading Post fee arithmetic. GW2 charges a 5% listing fee and a 10%
    /// exchange tax on sale, each rounded half-up with a 1 copper minimum
    /// (per the GW2 wiki's Trading Post documentation). Fees are computed
    /// per unit, matching how the game charges per listed item.
    /// </summary>
    public static class TradingPostMath
    {
        public static long ListingFee(long unitPrice)
        {
            if (unitPrice <= 0)
            {
                return 0;
            }
            return Math.Max(1L, RoundHalfUp(unitPrice, 5));
        }

        public static long ExchangeFee(long unitPrice)
        {
            if (unitPrice <= 0)
            {
                return 0;
            }
            return Math.Max(1L, RoundHalfUp(unitPrice, 10));
        }

        /// <summary>
        /// Net copper received for selling <paramref name="quantity"/> units
        /// at <paramref name="unitPrice"/> after both fees. Returns 0 for
        /// non-positive prices or quantities.
        /// </summary>
        public static long NetSaleRevenue(long unitPrice, int quantity)
        {
            if (unitPrice <= 0 || quantity <= 0)
            {
                return 0;
            }

            long netPerUnit = unitPrice - ListingFee(unitPrice) - ExchangeFee(unitPrice);
            if (netPerUnit < 0)
            {
                netPerUnit = 0;
            }
            return netPerUnit * quantity;
        }

        private static long RoundHalfUp(long value, int percent)
        {
            return (value * percent + 50) / 100;
        }
    }
}
