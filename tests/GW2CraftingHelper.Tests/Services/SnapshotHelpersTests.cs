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

        [Fact]
        public void AggregateItems_NullEntriesInList_SkipsNulls()
        {
            var items = new List<SnapshotItemEntry>
            {
                new SnapshotItemEntry { ItemId = 1, Name = "A", Count = 5, Source = "Bank" },
                null,
                new SnapshotItemEntry { ItemId = 1, Name = "A", Count = 3, Source = "Character:Ranger" }
            };

            var result = SnapshotHelpers.AggregateItems(items);

            Assert.Single(result);
            Assert.Equal(8, result[0].Count);
        }

        [Fact]
        public void AggregateItems_PrefersNonEmptyName()
        {
            var items = new List<SnapshotItemEntry>
            {
                new SnapshotItemEntry { ItemId = 10, Name = "", Count = 2, Source = "Bank" },
                new SnapshotItemEntry { ItemId = 10, Name = "Gold Ore", Count = 4, Source = "MaterialStorage" }
            };

            var result = SnapshotHelpers.AggregateItems(items);

            Assert.Single(result);
            Assert.Equal("Gold Ore", result[0].Name);
        }

        [Fact]
        public void AggregateItems_PreservesIconUrl()
        {
            var items = new List<SnapshotItemEntry>
            {
                new SnapshotItemEntry { ItemId = 7, Name = "Mithril Ore", IconUrl = "https://render.guildwars2.com/file/ABC/123.png", Count = 10, Source = "Bank" },
                new SnapshotItemEntry { ItemId = 7, Name = "Mithril Ore", IconUrl = "https://render.guildwars2.com/file/ABC/123.png", Count = 5, Source = "MaterialStorage" }
            };

            var result = SnapshotHelpers.AggregateItems(items);

            Assert.Single(result);
            Assert.Equal(15, result[0].Count);
            Assert.Equal("https://render.guildwars2.com/file/ABC/123.png", result[0].IconUrl);
        }

        [Fact]
        public void AggregateItems_PrefersNonEmptyIconUrl()
        {
            var items = new List<SnapshotItemEntry>
            {
                new SnapshotItemEntry { ItemId = 8, Name = "Orichalcum", IconUrl = "", Count = 3, Source = "Bank" },
                new SnapshotItemEntry { ItemId = 8, Name = "Orichalcum", IconUrl = "https://render.guildwars2.com/file/DEF/456.png", Count = 2, Source = "Character:Ranger" }
            };

            var result = SnapshotHelpers.AggregateItems(items);

            Assert.Single(result);
            Assert.Equal("https://render.guildwars2.com/file/DEF/456.png", result[0].IconUrl);
        }

        [Fact]
        public void AggregateItems_AllEmptyIconUrl_ReturnsEmpty()
        {
            var items = new List<SnapshotItemEntry>
            {
                new SnapshotItemEntry { ItemId = 9, Name = "X", IconUrl = "", Count = 1, Source = "Bank" },
                new SnapshotItemEntry { ItemId = 9, Name = "X", IconUrl = "", Count = 1, Source = "Bank" }
            };

            var result = SnapshotHelpers.AggregateItems(items);

            Assert.Single(result);
            Assert.Equal("", result[0].IconUrl);
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

        [Fact]
        public void SplitWalletAndCoins_NullEntriesInList_SkipsNulls()
        {
            var entries = new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 100 },
                null,
                new SnapshotWalletEntry { CurrencyId = 3, CurrencyName = "Gems", Value = 50 }
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
                new SnapshotWalletEntry { CurrencyId = 1, CurrencyName = "Coin", Value = 5000 }
            };

            var (coinCopper, wallet) = SnapshotHelpers.SplitWalletAndCoins(entries);

            Assert.Equal(15000, coinCopper);
            Assert.Empty(wallet);
        }
    }

}
