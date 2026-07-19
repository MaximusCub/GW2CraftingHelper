using System;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Trading Post fee arithmetic. GW2 charges a 5% listing fee and a 10%
    /// exchange tax, each computed on the TOTAL sale value of the
    /// transaction, rounded half-up with a 1 copper minimum per fee (per
    /// the GW2 wiki's Trading Post documentation). Fees must not be applied
    /// per unit: a 250-stack of 1c items nets ~212c, not 0.
    /// </summary>
    public static class TradingPostMath
    {
        public static long ListingFee(long totalValue)
        {
            if (totalValue <= 0)
            {
                return 0;
            }
            return Math.Max(1L, RoundHalfUp(totalValue, 5));
        }

        public static long ExchangeFee(long totalValue)
        {
            if (totalValue <= 0)
            {
                return 0;
            }
            return Math.Max(1L, RoundHalfUp(totalValue, 10));
        }

        /// <summary>
        /// Net copper received for selling <paramref name="quantity"/> units
        /// at <paramref name="unitPrice"/> in one transaction, after both
        /// fees on the total. Returns 0 for non-positive prices/quantities;
        /// never negative.
        /// </summary>
        public static long NetSaleRevenue(long unitPrice, int quantity)
        {
            if (unitPrice <= 0 || quantity <= 0)
            {
                return 0;
            }

            long totalValue = unitPrice * quantity;
            long net = totalValue - ListingFee(totalValue) - ExchangeFee(totalValue);
            return net < 0 ? 0 : net;
        }

        private static long RoundHalfUp(long value, int percent)
        {
            return (value * percent + 50) / 100;
        }
    }
}
