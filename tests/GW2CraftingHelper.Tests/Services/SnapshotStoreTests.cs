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

        // --- One-store convention: atomic .tmp+Replace, matching
        // StatusStore/VendorOfferStore - previously a plain, non-atomic
        // File.WriteAllText (tab-roadmap-proposal.md Section 2.2's
        // correction). ---

        [Fact]
        public void Save_LeavesNoTmpFileBehind()
        {
            _store.Save(CreateSnapshot(42));

            string tmpPath = Path.Combine(_tempDir, "snapshot.json.tmp");
            Assert.False(File.Exists(tmpPath));
        }

        [Fact]
        public void Save_Overwrite_LeavesNoTmpFileBehindEither()
        {
            _store.Save(CreateSnapshot(1));
            _store.Save(CreateSnapshot(2));

            string tmpPath = Path.Combine(_tempDir, "snapshot.json.tmp");
            Assert.False(File.Exists(tmpPath));
            Assert.Equal(2, _store.LoadLatest().CoinCopper);
        }

        // --- onError callback: real IO failure. ---

        [Fact]
        public void Save_DirectoryCreationFails_InvokesOnErrorInsteadOfThrowing()
        {
            string blockingPath = Path.Combine(_tempDir, "blocked-data-dir");
            File.WriteAllText(blockingPath, "not a directory");

            string capturedMessage = null;
            Exception capturedException = null;
            var store = new SnapshotStore(blockingPath, (message, ex) =>
            {
                capturedMessage = message;
                capturedException = ex;
            });

            store.Save(CreateSnapshot());

            Assert.NotNull(capturedMessage);
            Assert.NotNull(capturedException);
        }

        // --- Per-character discipline display: real SnapshotStore
        // round-trip with the per-character discipline data. ---

        [Fact]
        public void Save_Load_RoundTripsCharacterDisciplines()
        {
            var snapshot = CreateSnapshot();
            snapshot.CharacterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Anna", Discipline = "Weaponsmith", Rating = 500, Active = true },
                new SnapshotCharacterDiscipline { CharacterName = "Anna", Discipline = "Huntsman", Rating = 400, Active = false },
                new SnapshotCharacterDiscipline { CharacterName = "Bob", Discipline = "Weaponsmith", Rating = 250, Active = true }
            };
            _store.Save(snapshot);

            var loaded = _store.LoadLatest();

            Assert.NotNull(loaded);
            Assert.NotNull(loaded.CharacterDisciplines);
            Assert.Equal(3, loaded.CharacterDisciplines.Count);

            var annaWeaponsmith = loaded.CharacterDisciplines.Find(
                cd => cd.CharacterName == "Anna" && cd.Discipline == "Weaponsmith");
            Assert.NotNull(annaWeaponsmith);
            Assert.Equal(500, annaWeaponsmith.Rating);
            Assert.True(annaWeaponsmith.Active);

            var annaHuntsman = loaded.CharacterDisciplines.Find(
                cd => cd.CharacterName == "Anna" && cd.Discipline == "Huntsman");
            Assert.NotNull(annaHuntsman);
            Assert.Equal(400, annaHuntsman.Rating);
            Assert.False(annaHuntsman.Active);
        }

        [Fact]
        public void Save_Load_NullCharacterDisciplines_RoundTripsAsNull()
        {
            // CreateSnapshot() never sets CharacterDisciplines - mirrors a
            // legacy snapshot object built by code that has not been
            // updated to populate it (distinct from a newer capture
            // that legitimately came back empty - see AccountSnapshot.
            // CharacterDisciplines' own doc comment).
            var snapshot = CreateSnapshot();
            _store.Save(snapshot);

            var loaded = _store.LoadLatest();

            Assert.NotNull(loaded);
            Assert.Null(loaded.CharacterDisciplines);
        }
    }

}
