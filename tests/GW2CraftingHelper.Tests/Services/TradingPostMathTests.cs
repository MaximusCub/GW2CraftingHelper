using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class TradingPostMathTests
    {
        [Theory]
        [InlineData(100, 5)]
        [InlineData(30, 2)]   // 5% of 30 = 1.5 -> rounds half-up to 2
        [InlineData(10, 1)]   // below minimum -> 1c floor
        [InlineData(1, 1)]    // minimum fee applies
        [InlineData(0, 0)]
        [InlineData(-5, 0)]
        public void ListingFee_Cases(long price, long expected)
        {
            Assert.Equal(expected, TradingPostMath.ListingFee(price));
        }

        [Theory]
        [InlineData(100, 10)]
        [InlineData(15, 2)]   // 10% of 15 = 1.5 -> rounds half-up to 2
        [InlineData(5, 1)]    // below minimum -> 1c floor
        [InlineData(0, 0)]
        public void ExchangeFee_Cases(long price, long expected)
        {
            Assert.Equal(expected, TradingPostMath.ExchangeFee(price));
        }

        [Fact]
        public void NetSaleRevenue_StandardPrice_85Percent()
        {
            // 100c: -5 listing -10 exchange = 85 per unit
            Assert.Equal(255, TradingPostMath.NetSaleRevenue(100, 3));
        }

        [Fact]
        public void NetSaleRevenue_MinimumFeesCanZeroOut()
        {
            // 1c: both fees floor at 1c, net clamps to 0, never negative
            Assert.Equal(0, TradingPostMath.NetSaleRevenue(1, 10));
        }

        [Fact]
        public void NetSaleRevenue_LowPrice_FloorsAtOneCopperFees()
        {
            // 3c: 1c listing + 1c exchange = 1c net per unit
            Assert.Equal(5, TradingPostMath.NetSaleRevenue(3, 5));
        }

        [Fact]
        public void NetSaleRevenue_NonPositiveInputs_Zero()
        {
            Assert.Equal(0, TradingPostMath.NetSaleRevenue(0, 5));
            Assert.Equal(0, TradingPostMath.NetSaleRevenue(100, 0));
            Assert.Equal(0, TradingPostMath.NetSaleRevenue(-10, 5));
        }
    }
}
