using System;
using System.Collections.Generic;
using System.IO;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{

    public class SnapshotStoreTests : IDisposable
    {

        private readonly string _tempDir;
        private readonly SnapshotStore _store;

        public SnapshotStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GW2CraftingHelper_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _store = new SnapshotStore(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private static AccountSnapshot CreateSnapshot(int coinCopper = 0)
        {
            return new AccountSnapshot
            {
                CapturedAt = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                CoinCopper = coinCopper,
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 1, Name = "Item", Count = 5, Source = "Bank" }
                },
                Wallet = new List<SnapshotWalletEntry>
                {
                    new SnapshotWalletEntry { CurrencyId = 2, CurrencyName = "Karma", Value = 1000 }
                }
            };
        }

        [Fact]
        public void Save_Load_PreservesCapturedAtAndCoinCopper()
        {
            var snapshot = CreateSnapshot(123456);
            _store.Save(snapshot);

            var loaded = _store.LoadLatest();
            Assert.NotNull(loaded);
            Assert.Equal(snapshot.CapturedAt, loaded.CapturedAt);
            Assert.Equal(123456, loaded.CoinCopper);
        }

        [Fact]
        public void Save_Load_ProducesNewInstance()
        {
            var snapshot = CreateSnapshot();
            _store.Save(snapshot);

            var loaded = _store.LoadLatest();
            Assert.NotNull(loaded);
            Assert.NotSame(snapshot, loaded);
        }

        [Fact]
        public void Save_Overwrite_LoadReturnsLatest()
        {
            _store.Save(CreateSnapshot(100));
            _store.Save(CreateSnapshot(999));

            var loaded = _store.LoadLatest();
            Assert.NotNull(loaded);
            Assert.Equal(999, loaded.CoinCopper);
        }

        [Fact]
        public void Delete_RemovesSnapshot_LoadReturnsNull()
        {
            _store.Save(CreateSnapshot());
            Assert.NotNull(_store.LoadLatest());

            _store.Delete();
            Assert.Null(_store.LoadLatest());
        }

        [Fact]
        public void Delete_NoFile_DoesNotThrow()
        {
            _store.Delete();
        }
    }

}
