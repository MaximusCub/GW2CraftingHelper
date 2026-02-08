using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{

    public class SnapshotHelpersTests
    {

        // ── AggregateItems ──────────────────────────────────────────

        [Fact]
        public void AggregateItems_SameItemId_SumsCountsAndSetsSourceToTotal()
        {
            var items = new List<SnapshotItemEntry>
            {
                new SnapshotItemEntry { ItemId = 42, Name = "Iron Ore", Count = 10, Source = "Bank" },
                new SnapshotItemEntry { ItemId = 42, Name = "Iron Ore", Count = 5, Source = "Character:Warrior" }
            };

            var result = SnapshotHelpers.AggregateItems(items);

            Assert.Single(result);
            Assert.Equal(42, result[0].ItemId);
            Assert.Equal("Iron Ore", result[0].Name);
            Assert.Equal(15, result[0].Count);
            Assert.Equal("Total", result[0].Source);
        }

        [Fact]
        public void AggregateItems_DifferentItemIds_RetainsSeparateEntries()
        {
            var items = new List<SnapshotItemEntry>
            {
                new SnapshotItemEntry { ItemId = 1, Name = "A", Count = 3, Source = "Bank" },
                new SnapshotItemEntry { ItemId = 2, Name = "B", Count = 7, Source = "Bank" }
            };

            var result = SnapshotHelpers.AggregateItems(items);

            Assert.Equal(2, result.Count);
            Assert.Equal(3, result.First(i => i.ItemId == 1).Count);
            Assert.Equal(7, result.First(i => i.ItemId == 2).Count);
        }

        [Fact]
        public void AggregateItems_EmptyList_ReturnsEmpty()
        {
            var result = SnapshotHelpers.AggregateItems(new List<SnapshotItemEntry>());

            Assert.Empty(result);
        }

        [Fact]
        public void AggregateItems_Null_ReturnsEmpty()
        {
            var result = SnapshotHelpers.AggregateItems(null);

            Assert.Empty(result);
        }

        // ── SplitWalletAndCoins ─────────────────────────────────────

        [Fact]
        public void SplitWalletAndCoins_WithCurrencyId1_ExtractsCoinCopper()
        {
            var entries = new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 1, CurrencyName = "Coin", Value = 123456 },
                new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 5000 },
                new SnapshotWalletEntry { CurrencyId = 3, CurrencyName = "Gems", Value = 100 }
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
                new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 5000 }
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
    }

}
