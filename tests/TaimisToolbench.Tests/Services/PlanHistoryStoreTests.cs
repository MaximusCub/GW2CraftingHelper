using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // Real PlanHistoryStore against a real temp directory - no fake file
    // I/O. Mirrors RankerStoreTests'/PlanStoreTests' own fixture shape.
    public class PlanHistoryStoreTests : IDisposable
    {
        private readonly TempDirectory _temp = new TempDirectory();

        public void Dispose()
        {
            _temp.Dispose();
        }

        private string FilePath => Path.Combine(_temp.Path, "plan_history.json");

        private static PlanHistoryIndex SampleIndex()
        {
            return new PlanHistoryIndex
            {
                Entries = new List<PlanHistoryEntry>
                {
                    new PlanHistoryEntry
                    {
                        EntryId = "0123456789abcdef0123456789abcdef",
                        CreatedAtUtc = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
                        LastGeneratedAtUtc = new DateTime(2026, 8, 2, 11, 30, 0, DateTimeKind.Utc),
                        Pinned = true,
                        RequestItems = new List<PlanRequestItem>
                        {
                            new PlanRequestItem { ItemId = 30684, Quantity = 1 },
                            new PlanRequestItem { ItemId = 19721, Quantity = 250 },
                        },
                        UseOwnMaterials = true,
                        PriceBasis = PriceBasis.BuyOrder,
                        ValueOwnMaterials = true,
                        IgnoredItemIds = new List<int> { 19721 },
                        ItemSummaries = new List<PlanHistoryItemSummary>
                        {
                            new PlanHistoryItemSummary
                            {
                                ItemId = 30684, Name = "Twilight", IconUrl = "t.png",
                                Rarity = "Legendary", Quantity = 1,
                            },
                        },
                        TotalCoinCostAtGeneration = 1234567,
                        OverrideCountAtGeneration = 2,
                        IgnoredCountAtGeneration = 1,
                        BlobPresent = true,
                        BlobSchemaVersion = PersistedPlan.CurrentSchemaVersion,
                        CostSamples = new List<PlanHistorySample>
                        {
                            new PlanHistorySample
                            {
                                TimestampUtc = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
                                TotalCoinCost = 1300000,
                            },
                            new PlanHistorySample
                            {
                                TimestampUtc = new DateTime(2026, 8, 2, 11, 30, 0, DateTimeKind.Utc),
                                TotalCoinCost = 1234567,
                            },
                        },
                    },
                },
            };
        }

        [Fact]
        public void SaveThenLoad_PreservesEveryField()
        {
            var store = new PlanHistoryStore(_temp.Path);
            store.Save(SampleIndex());

            var loaded = store.Load();

            Assert.Equal(PlanHistoryIndex.CurrentSchemaVersion, loaded.SchemaVersion);
            var entry = Assert.Single(loaded.Entries);
            Assert.Equal("0123456789abcdef0123456789abcdef", entry.EntryId);
            Assert.Equal(new DateTime(2026, 8, 1, 10, 0, 0), entry.CreatedAtUtc);
            Assert.Equal(new DateTime(2026, 8, 2, 11, 30, 0), entry.LastGeneratedAtUtc);
            Assert.True(entry.Pinned);
            Assert.Equal(2, entry.RequestItems.Count);
            Assert.Equal(30684, entry.RequestItems[0].ItemId);
            Assert.Equal(1, entry.RequestItems[0].Quantity);
            Assert.Equal(19721, entry.RequestItems[1].ItemId);
            Assert.Equal(250, entry.RequestItems[1].Quantity);
            Assert.True(entry.UseOwnMaterials);
            Assert.Equal(PriceBasis.BuyOrder, entry.PriceBasis);
            Assert.True(entry.ValueOwnMaterials);
            Assert.Equal(new[] { 19721 }, entry.IgnoredItemIds);
            var summary = Assert.Single(entry.ItemSummaries);
            Assert.Equal(30684, summary.ItemId);
            Assert.Equal("Twilight", summary.Name);
            Assert.Equal("t.png", summary.IconUrl);
            Assert.Equal("Legendary", summary.Rarity);
            Assert.Equal(1, summary.Quantity);
            Assert.Equal(1234567, entry.TotalCoinCostAtGeneration);
            Assert.Equal(2, entry.OverrideCountAtGeneration);
            Assert.Equal(1, entry.IgnoredCountAtGeneration);
            Assert.True(entry.BlobPresent);
            Assert.Equal(PersistedPlan.CurrentSchemaVersion, entry.BlobSchemaVersion);
            Assert.Equal(2, entry.CostSamples.Count);
            Assert.Equal(1300000, entry.CostSamples[0].TotalCoinCost);
            Assert.Equal(1234567, entry.CostSamples[1].TotalCoinCost);
        }

        [Fact]
        public void MissingFile_ReturnsEmptyIndexWithoutFiringOnError()
        {
            int errors = 0;
            var store = new PlanHistoryStore(_temp.Path, (_, __) => errors++);

            var loaded = store.Load();

            Assert.NotNull(loaded);
            Assert.Empty(loaded.Entries);
            Assert.Equal(0, errors);
        }

        [Fact]
        public void CorruptJson_ReturnsEmptyIndexAndFiresOnErrorExactlyOnce()
        {
            File.WriteAllText(FilePath, "{ not json");
            int errors = 0;
            var store = new PlanHistoryStore(_temp.Path, (_, __) => errors++);

            var loaded = store.Load();

            Assert.Empty(loaded.Entries);
            Assert.Equal(1, errors);

            // The bad file is left on disk for inspection, not deleted.
            Assert.True(File.Exists(FilePath));
        }

        // --- Which versions cost the user their history, and which do not.
        // Exact-match rejection used to empty the whole index on any bump,
        // so a release that changed the row shape additively would still
        // have discarded up to 200 saved plans. Load now accepts the range
        // [MinimumReadableSchemaVersion, CurrentSchemaVersion]; the tests
        // below are written against those constants rather than literals,
        // so the day the current version moves they start pinning a genuine
        // older-file read instead of a boundary that happens to coincide.
        // See docs/ARCHITECTURE.md section 12. ---
        [Fact]
        public void FileAtTheOldestReadableSchemaVersion_KeepsEveryRow()
        {
            var store = new PlanHistoryStore(_temp.Path);
            store.Save(SampleIndex());

            var json = JObject.Parse(File.ReadAllText(FilePath));
            json["SchemaVersion"] = PlanHistoryIndex.MinimumReadableSchemaVersion;
            File.WriteAllText(FilePath, json.ToString());

            int errors = 0;
            var reloaded = new PlanHistoryStore(_temp.Path, (_, __) => errors++).Load();

            Assert.Equal(0, errors);
            var entry = Assert.Single(reloaded.Entries);
            Assert.Equal("0123456789abcdef0123456789abcdef", entry.EntryId);
            Assert.Equal(2, entry.RequestItems.Count);
        }

        [Fact]
        public void FileAtTheOldestReadableSchemaVersion_IsRestampedByTheNextSave()
        {
            // The self-healing half: an accepted older file does not stay
            // older. Nothing has to migrate it, because Save always stamps
            // the current version over whatever it read.
            var store = new PlanHistoryStore(_temp.Path);
            store.Save(SampleIndex());

            var json = JObject.Parse(File.ReadAllText(FilePath));
            json["SchemaVersion"] = PlanHistoryIndex.MinimumReadableSchemaVersion;
            File.WriteAllText(FilePath, json.ToString());

            var reloaded = new PlanHistoryStore(_temp.Path);
            var loaded = reloaded.Load();
            reloaded.Save(loaded);

            var onDisk = JObject.Parse(File.ReadAllText(FilePath));
            Assert.Equal(
                PlanHistoryIndex.CurrentSchemaVersion,
                onDisk.Value<int>("SchemaVersion"));
            Assert.Single(loaded.Entries);
        }

        [Fact]
        public void FileBelowTheOldestReadableSchemaVersion_ReturnsEmptyIndexAndFiresOnErrorOnce()
        {
            var store = new PlanHistoryStore(_temp.Path);
            store.Save(SampleIndex());

            var json = JObject.Parse(File.ReadAllText(FilePath));
            json["SchemaVersion"] = PlanHistoryIndex.MinimumReadableSchemaVersion - 1;
            File.WriteAllText(FilePath, json.ToString());

            int errors = 0;
            var reloaded = new PlanHistoryStore(_temp.Path, (_, __) => errors++).Load();

            Assert.Empty(reloaded.Entries);
            Assert.Equal(1, errors);
        }

        [Fact]
        public void FileFromANewerBuild_ReturnsEmptyIndexAndFiresOnErrorOnce()
        {
            // A downgrade. This build cannot know what a newer one wrote,
            // so it keeps the file on disk and starts empty rather than
            // reading rows it may misunderstand.
            var store = new PlanHistoryStore(_temp.Path);
            store.Save(SampleIndex());

            var json = JObject.Parse(File.ReadAllText(FilePath));
            json["SchemaVersion"] = PlanHistoryIndex.CurrentSchemaVersion + 1;
            File.WriteAllText(FilePath, json.ToString());

            int errors = 0;
            var reloaded = new PlanHistoryStore(_temp.Path, (_, __) => errors++).Load();

            Assert.Empty(reloaded.Entries);
            Assert.Equal(1, errors);
        }

        [Fact]
        public void SchemaVersionFarAboveAnythingShipped_ReturnsEmptyIndexAndFiresOnErrorOnce_DoesNotThrow()
        {
            var store = new PlanHistoryStore(_temp.Path);
            store.Save(SampleIndex());

            var json = JObject.Parse(File.ReadAllText(FilePath));
            json["SchemaVersion"] = 999;
            File.WriteAllText(FilePath, json.ToString());

            int errors = 0;
            var reloaded = new PlanHistoryStore(_temp.Path, (_, __) => errors++).Load();

            Assert.Empty(reloaded.Entries);
            Assert.Equal(1, errors);
        }

        [Fact]
        public void FileOmittingSchemaVersion_DeserializesAsZeroAndIsRejected()
        {
            // What the no-property-initializer rule buys: absence stays
            // detectable as 0, which never matches CurrentSchemaVersion.
            var store = new PlanHistoryStore(_temp.Path);
            store.Save(SampleIndex());

            var json = JObject.Parse(File.ReadAllText(FilePath));
            json.Remove("SchemaVersion");
            File.WriteAllText(FilePath, json.ToString());

            int errors = 0;
            var reloaded = new PlanHistoryStore(_temp.Path, (_, __) => errors++).Load();

            Assert.Empty(reloaded.Entries);
            Assert.Equal(1, errors);
        }

        [Fact]
        public void EntryWithoutAnId_IsDroppedOnLoad()
        {
            var index = SampleIndex();
            index.Entries.Add(new PlanHistoryEntry { EntryId = null });
            var store = new PlanHistoryStore(_temp.Path);
            store.Save(index);

            var loaded = store.Load();

            var entry = Assert.Single(loaded.Entries);
            Assert.Equal("0123456789abcdef0123456789abcdef", entry.EntryId);
        }

        [Fact]
        public void Save_LeavesNoTmpFileBehind()
        {
            var store = new PlanHistoryStore(_temp.Path);
            store.Save(SampleIndex());
            store.Save(SampleIndex());

            Assert.True(File.Exists(FilePath));
            Assert.False(File.Exists(FilePath + ".tmp"));
        }

        [Fact]
        public async Task ConcurrentSaves_LeaveAFileThatRoundTrips()
        {
            var store = new PlanHistoryStore(_temp.Path);
            var first = Task.Run(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    store.Save(SampleIndex());
                }
            });
            var second = Task.Run(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    store.Save(SampleIndex());
                }
            });

            await Task.WhenAll(first, second);

            int errors = 0;
            var loaded = new PlanHistoryStore(_temp.Path, (_, __) => errors++).Load();
            Assert.Single(loaded.Entries);
            Assert.Equal(0, errors);
        }
    }
}
