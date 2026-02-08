using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Views
{

    public class CoinFormattingTests
    {

        [Theory]
        [InlineData(0, "Coin: 0g 0s 0c")]
        [InlineData(1, "Coin: 0g 0s 1c")]
        [InlineData(100, "Coin: 0g 1s 0c")]
        [InlineData(10000, "Coin: 1g 0s 0c")]
        [InlineData(1234567, "Coin: 123g 45s 67c")]
        [InlineData(99, "Coin: 0g 0s 99c")]
        [InlineData(9999, "Coin: 0g 99s 99c")]
        [InlineData(10101, "Coin: 1g 1s 1c")]
        [InlineData(-1, "Coin: 0g 0s 0c")]
        [InlineData(-99999, "Coin: 0g 0s 0c")]
        public void FormatCoin_ReturnsExpectedString(int copper, string expected)
        {
            string result = SnapshotHelpers.FormatCoin(copper);

            Assert.Equal(expected, result);
        }
    }

}
