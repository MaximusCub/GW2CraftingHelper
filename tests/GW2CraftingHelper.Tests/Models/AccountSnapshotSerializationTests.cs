using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;
using Newtonsoft.Json;
using Xunit;

namespace GW2CraftingHelper.Tests.Models
{

    public class AccountSnapshotSerializationTests
    {

        [Fact]
        public void RoundTrip_PreservesAllFields()
        {
            var original = new AccountSnapshot
            {
                CapturedAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                CoinCopper = 1234567,
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 100, Name = "Iron Ore", Count = 50, Source = "Bank" },
                    new SnapshotItemEntry { ItemId = 200, Name = "Gold Ore", Count = 10, Source = "Character:Ranger" }
                },
                Wallet = new List<SnapshotWalletEntry>
                {
                    new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 9999 }
                }
            };

            string json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<AccountSnapshot>(json);

            Assert.Equal(original.CapturedAt, deserialized.CapturedAt);
            Assert.Equal(original.CoinCopper, deserialized.CoinCopper);
            Assert.Equal(original.Items.Count, deserialized.Items.Count);
            Assert.Equal(original.Items[0].ItemId, deserialized.Items[0].ItemId);
            Assert.Equal(original.Items[0].Name, deserialized.Items[0].Name);
            Assert.Equal(original.Items[0].Count, deserialized.Items[0].Count);
            Assert.Equal(original.Items[0].Source, deserialized.Items[0].Source);
            Assert.Equal(original.Items[1].ItemId, deserialized.Items[1].ItemId);
            Assert.Equal(original.Wallet.Count, deserialized.Wallet.Count);
            Assert.Equal(original.Wallet[0].CurrencyId, deserialized.Wallet[0].CurrencyId);
            Assert.Equal(original.Wallet[0].CurrencyName, deserialized.Wallet[0].CurrencyName);
            Assert.Equal(original.Wallet[0].Value, deserialized.Wallet[0].Value);
        }

        [Fact]
        public void RoundTrip_EmptyItemsAndWallet_PreservesEmptyLists()
        {
            var original = new AccountSnapshot
            {
                CapturedAt = DateTime.UtcNow,
                CoinCopper = 0,
                Items = new List<SnapshotItemEntry>(),
                Wallet = new List<SnapshotWalletEntry>()
            };

            string json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<AccountSnapshot>(json);

            Assert.Empty(deserialized.Items);
            Assert.Empty(deserialized.Wallet);
            Assert.Equal(0, deserialized.CoinCopper);
        }

        [Fact]
        public void CoinCopper_Preserved()
        {
            var original = new AccountSnapshot { CoinCopper = int.MaxValue };

            string json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<AccountSnapshot>(json);

            Assert.Equal(int.MaxValue, deserialized.CoinCopper);
        }
    }

}
