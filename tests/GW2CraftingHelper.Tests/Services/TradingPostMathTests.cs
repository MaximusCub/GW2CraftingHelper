using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class TradingPostMathTests
    {
        [Theory]
        [InlineData(100, 5)]
        [InlineData(30, 2)] // 5% of 30 = 1.5 -> rounds half-up to 2
        [InlineData(10, 1)] // below minimum -> 1c floor
        [InlineData(1, 1)] // minimum fee applies
        [InlineData(0, 0)]
        [InlineData(-5, 0)]
        public void ListingFee_Cases(long price, long expected)
        {
            Assert.Equal(expected, TradingPostMath.ListingFee(price));
        }

        [Theory]
        [InlineData(100, 10)]
        [InlineData(15, 2)] // 10% of 15 = 1.5 -> rounds half-up to 2
        [InlineData(5, 1)] // below minimum -> 1c floor
        [InlineData(0, 0)]
        public void ExchangeFee_Cases(long price, long expected)
        {
            Assert.Equal(expected, TradingPostMath.ExchangeFee(price));
        }

        [Fact]
        public void NetSaleRevenue_StandardPrice_85Percent()
        {
            // Total 300c: -15 listing -30 exchange = 255
            Assert.Equal(255, TradingPostMath.NetSaleRevenue(100, 3));
        }

        [Fact]
        public void NetSaleRevenue_BulkCheapStack_FeesOnTransactionTotal()
        {
            // Wiki reference case: 250 units at 1c = 250c total;
            // listing round(12.5)=13, exchange 25 -> nets 212, NOT 0.
            // Fees are per transaction, never per unit.
            Assert.Equal(212, TradingPostMath.NetSaleRevenue(1, 250));
        }

        [Fact]
        public void NetSaleRevenue_SmallCheapStack_MinimumFeesOnTotal()
        {
            // Total 10c: both fees floor at 1c -> 8
            Assert.Equal(8, TradingPostMath.NetSaleRevenue(1, 10));
            // Total 15c: listing 1c (floor), exchange 2c (round 1.5 up) -> 12
            Assert.Equal(12, TradingPostMath.NetSaleRevenue(3, 5));
        }

        [Fact]
        public void NetSaleRevenue_SingleOneCopper_ClampsAtZero()
        {
            // Total 1c: 1c + 1c minimum fees exceed value -> 0, never negative
            Assert.Equal(0, TradingPostMath.NetSaleRevenue(1, 1));
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
