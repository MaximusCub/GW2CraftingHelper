using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{

    public class SnapshotSerializationTests
    {

        [Fact]
        public void Serialize_Deserialize_RoundTrip()
        {
            var original = new AccountSnapshot
            {
                CapturedAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                CoinCopper = 500000,
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 42, Name = "Test Item", Count = 10, Source = "Bank" }
                },
                Wallet = new List<SnapshotWalletEntry>
                {
                    new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 3000 }
                }
            };

            string json = SnapshotHelpers.SerializeSnapshot(original);
            var result = SnapshotHelpers.DeserializeSnapshot(json);

            Assert.NotNull(result);
            Assert.Equal(original.CapturedAt, result.CapturedAt);
            Assert.Equal(original.CoinCopper, result.CoinCopper);
            Assert.Equal(original.Items.Count, result.Items.Count);
            Assert.Equal(original.Items[0].ItemId, result.Items[0].ItemId);
            Assert.Equal(original.Items[0].Name, result.Items[0].Name);
            Assert.Equal(original.Items[0].Count, result.Items[0].Count);
            Assert.Equal(original.Wallet.Count, result.Wallet.Count);
            Assert.Equal(original.Wallet[0].CurrencyId, result.Wallet[0].CurrencyId);
            Assert.Equal(original.Wallet[0].Value, result.Wallet[0].Value);
        }

        [Fact]
        public void Deserialize_NullString_ReturnsNull()
        {
            var result = SnapshotHelpers.DeserializeSnapshot(null);

            Assert.Null(result);
        }

        [Fact]
        public void Deserialize_EmptyString_ReturnsNull()
        {
            var result = SnapshotHelpers.DeserializeSnapshot("");

            Assert.Null(result);
        }

        [Fact]
        public void Deserialize_CorruptJson_ReturnsNull()
        {
            var result = SnapshotHelpers.DeserializeSnapshot("{this is not valid json!!!");

            Assert.Null(result);
        }

        [Fact]
        public void Deserialize_WhitespaceOnly_ReturnsNull()
        {
            var result = SnapshotHelpers.DeserializeSnapshot("   ");

            Assert.Null(result);
        }

        [Fact]
        public void Serialize_NullSnapshot_ReturnsNull()
        {
            var result = SnapshotHelpers.SerializeSnapshot(null);

            Assert.Null(result);
        }

        [Fact]
        public void Serialize_Deserialize_IconUrl_RoundTrips()
        {
            var original = new AccountSnapshot
            {
                CapturedAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                CoinCopper = 100,
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry
                    {
                        ItemId = 1,
                        Name = "Copper Ore",
                        IconUrl = "https://render.guildwars2.com/file/ABC/123.png",
                        Count = 10,
                        Source = "Bank"
                    }
                },
                Wallet = new List<SnapshotWalletEntry>
                {
                    new SnapshotWalletEntry
                    {
                        CurrencyId = 2,
                        CurrencyName = "Karma",
                        IconUrl = "https://render.guildwars2.com/file/DEF/456.png",
                        Value = 5000
                    }
                }
            };

            string json = SnapshotHelpers.SerializeSnapshot(original);
            var result = SnapshotHelpers.DeserializeSnapshot(json);

            Assert.NotNull(result);
            Assert.Equal("https://render.guildwars2.com/file/ABC/123.png", result.Items[0].IconUrl);
            Assert.Equal("https://render.guildwars2.com/file/DEF/456.png", result.Wallet[0].IconUrl);
        }

        [Fact]
        public void Serialize_Deserialize_EmptyIconUrl_RoundTrips()
        {
            var original = new AccountSnapshot
            {
                CapturedAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                CoinCopper = 0,
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 1, Name = "X", IconUrl = "", Count = 1, Source = "Bank" }
                },
                Wallet = new List<SnapshotWalletEntry>
                {
                    new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Y", IconUrl = "", Value = 1 }
                }
            };

            string json = SnapshotHelpers.SerializeSnapshot(original);
            var result = SnapshotHelpers.DeserializeSnapshot(json);

            Assert.NotNull(result);
            Assert.Equal("", result.Items[0].IconUrl);
            Assert.Equal("", result.Wallet[0].IconUrl);
        }

        [Fact]
        public void Deserialize_OldJsonMissingIconUrl_DefaultsToEmpty()
        {
            // Simulate an older snapshot.json that doesn't have the IconUrl field
            string oldJson = @"{
                ""CapturedAt"": ""2025-06-15T12:00:00Z"",
                ""CoinCopper"": 0,
                ""Items"": [
                    { ""ItemId"": 1, ""Name"": ""Old Item"", ""Count"": 5, ""Source"": ""Bank"" }
                ],
                ""Wallet"": [
                    { ""CurrencyId"": 2, ""CurrencyName"": ""Karma"", ""Value"": 100 }
                ]
            }";

            var result = SnapshotHelpers.DeserializeSnapshot(oldJson);

            Assert.NotNull(result);
            Assert.Equal("", result.Items[0].IconUrl);
            Assert.Equal("", result.Wallet[0].IconUrl);
        }
    }

}
