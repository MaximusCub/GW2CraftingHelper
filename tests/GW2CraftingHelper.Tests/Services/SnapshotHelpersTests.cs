using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class SnapshotHelpersTests
    {
        // -- SplitWalletAndCoins -------------------------------------
        [Fact]
        public void SplitWalletAndCoins_WithCurrencyId1_ExtractsCoinCopper()
        {
            var entries = new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 1, CurrencyName = "Coin", Value = 123456 },
                new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 5000 },
                new SnapshotWalletEntry { CurrencyId = 3, CurrencyName = "Gems", Value = 100 },
            };

            var (coinCopper, wallet) = SnapshotHelpers.SplitWalletAndCoins(entries);

            Assert.Equal(123456, coinCopper);
            Assert.Equal(2, wallet.Count);
            Assert.DoesNotContain(wallet, e => e.CurrencyId == 1);
        }

        [Fact]
        public void SplitWalletAndCoins_WithoutCurrencyId1_CoinCopperIsZero()
        {
            var entries = new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 5000 },
            };

            var (coinCopper, wallet) = SnapshotHelpers.SplitWalletAndCoins(entries);

            Assert.Equal(0, coinCopper);
            Assert.Single(wallet);
        }

        [Fact]
        public void SplitWalletAndCoins_EmptyList_ReturnsZeroAndEmpty()
        {
            var (coinCopper, wallet) = SnapshotHelpers.SplitWalletAndCoins(new List<SnapshotWalletEntry>());

            Assert.Equal(0, coinCopper);
            Assert.Empty(wallet);
        }

        [Fact]
        public void SplitWalletAndCoins_Null_ReturnsZeroAndEmpty()
        {
            var (coinCopper, wallet) = SnapshotHelpers.SplitWalletAndCoins(null);

            Assert.Equal(0, coinCopper);
            Assert.Empty(wallet);
        }

        [Fact]
        public void SplitWalletAndCoins_NullEntriesInList_SkipsNulls()
        {
            var entries = new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 100 },
                null,
                new SnapshotWalletEntry { CurrencyId = 3, CurrencyName = "Gems", Value = 50 },
            };

            var (coinCopper, wallet) = SnapshotHelpers.SplitWalletAndCoins(entries);

            Assert.Equal(0, coinCopper);
            Assert.Equal(2, wallet.Count);
        }

        [Fact]
        public void SplitWalletAndCoins_MultipleCoinEntries_SumsValues()
        {
            var entries = new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 1, CurrencyName = "Coin", Value = 10000 },
                new SnapshotWalletEntry { CurrencyId = 1, CurrencyName = "Coin", Value = 5000 },
            };

            var (coinCopper, wallet) = SnapshotHelpers.SplitWalletAndCoins(entries);

            Assert.Equal(15000, coinCopper);
            Assert.Empty(wallet);
        }
    }
}
