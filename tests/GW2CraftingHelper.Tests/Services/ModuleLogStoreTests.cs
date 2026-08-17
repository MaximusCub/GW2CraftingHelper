using System;
using System.IO;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // Real temp-dir file IO,
    // no mocked/fake I/O - same shape as VendorOfferStoreTests/StatusStoreTests.
    public class ModuleLogStoreTests
    {
        private static ModuleLogEntry MakeEntry(string tag, string message, DateTime? timestampUtc = null, ModuleLogLevel level = ModuleLogLevel.Info)
        {
            return new ModuleLogEntry
            {
                TimestampUtc = timestampUtc ?? DateTime.UtcNow,
                Level = level,
                Tag = tag,
                Message = message
            };
        }

        [Fact]
        public void ReadAll_NoFile_ReturnsEmptyList()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                Assert.Empty(store.ReadAll());
            }
        }

        [Fact]
        public void AppendLine_NullEntry_DoesNotCreateFile()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                store.AppendLine(null);
                Assert.False(File.Exists(store.FilePath));
            }
        }

        [Fact]
        public void AppendLine_Then_ReadAll_RoundTripsAllFields()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var ts = new DateTime(2026, 7, 22, 3, 14, 7, DateTimeKind.Utc);
                var entry = MakeEntry("snapshot", "Failed to refresh account snapshot: TimeoutException", ts, ModuleLogLevel.Warn);

                store.AppendLine(entry);
                var result = store.ReadAll();

                Assert.Single(result);
                Assert.Equal(ts, result[0].TimestampUtc);
                Assert.Equal(ModuleLogLevel.Warn, result[0].Level);
                Assert.Equal("snapshot", result[0].Tag);
                Assert.Equal("Failed to refresh account snapshot: TimeoutException", result[0].Message);
            }
        }

        [Fact]
        public void AppendLine_WritesShortPropertyNameJsonlWithLevelAsString()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                store.AppendLine(MakeEntry("snapshot", "msg", new DateTime(2026, 7, 22, 3, 14, 7, DateTimeKind.Utc), ModuleLogLevel.Warn));

                string line = File.ReadAllText(store.FilePath).TrimEnd('\n', '\r');

                // Short property names deliberately (d2-log-system.md
                // Section 4.1) - this file is written far more often than
                // snapshot.json, so every byte compounds across the
                // retention window.
                Assert.Contains("\"t\":", line);
                Assert.Contains("\"lvl\":\"Warn\"", line);
                Assert.Contains("\"tag\":\"snapshot\"", line);
                Assert.Contains("\"msg\":\"msg\"", line);
            }
        }

        [Fact]
        public void AppendLine_MultipleEntries_ReadAllReturnsInAppendOrder()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                for (int i = 0; i < 5; i++)
                {
                    store.AppendLine(MakeEntry("t" + i, "m" + i));
                }

                var result = store.ReadAll();

                Assert.Equal(5, result.Count);
                for (int i = 0; i < 5; i++)
                {
                    Assert.Equal("t" + i, result[i].Tag);
                }
            }
        }

        [Fact]
        public void ReadAll_MalformedTrailingLine_SkipsItWithoutThrowing()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                store.AppendLine(MakeEntry("good", "fine", level: ModuleLogLevel.Warn));

                // Simulates a crash mid-append leaving a truncated last
                // line - exactly the failure JSONL (vs. one big JSON array)
                // is chosen to tolerate, per d2-log-system.md Section 4.1.
                File.AppendAllText(store.FilePath, "{\"t\":\"not valid json truncat");

                var entries = store.ReadAll();

                Assert.Single(entries);
                Assert.Equal("good", entries[0].Tag);
            }
        }

        [Fact]
        public void AppendLine_ExceedsMaxSize_TrimsOldestQuarterAndRewritesAtomically()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);

                // Write 8 entries with no cap (maxSizeBytes: 0 disables the
                // self-trim check entirely).
                for (int i = 0; i < 8; i++)
                {
                    store.AppendLine(MakeEntry("entry-" + i, "m"), maxSizeBytes: 0);
                }

                long sizeBefore9th = new FileInfo(store.FilePath).Length;

                // The cap is set to the size BEFORE this 9th append - after
                // appending, the file is strictly larger than that cap
                // (any non-empty appended line grows it), so the trim is
                // guaranteed to fire exactly once here regardless of the
                // exact serialized byte lengths.
                store.AppendLine(MakeEntry("entry-8", "m"), maxSizeBytes: sizeBefore9th);

                // 9 entries total pre-trim; dropCount = max(1, 9/4) = 2.
                var remaining = store.ReadAll();
                Assert.Equal(7, remaining.Count);
                Assert.Equal("entry-2", remaining[0].Tag);
                Assert.Equal("entry-8", remaining[remaining.Count - 1].Tag);
            }
        }

        [Fact]
        public void PruneOlderThan_DropsEntriesOlderThanCutoff_KeepsRecent()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                store.AppendLine(MakeEntry("old", "old", DateTime.UtcNow.AddDays(-30)));
                store.AppendLine(MakeEntry("recent", "recent", DateTime.UtcNow.AddDays(-1)));

                store.PruneOlderThan(14);

                var remaining = store.ReadAll();
                Assert.Single(remaining);
                Assert.Equal("recent", remaining[0].Tag);
            }
        }

        [Fact]
        public void PruneOlderThan_NonPositiveDays_IsNoOp()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                store.AppendLine(MakeEntry("ancient", "m", DateTime.UtcNow.AddYears(-5)));

                store.PruneOlderThan(0);

                Assert.Single(store.ReadAll());
            }
        }

        [Fact]
        public void PruneOlderThan_NothingToDrop_DoesNotRewrite()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                store.AppendLine(MakeEntry("recent", "m", DateTime.UtcNow));

                DateTime before = File.GetLastWriteTimeUtc(store.FilePath);
                System.Threading.Thread.Sleep(20);
                store.PruneOlderThan(14);
                DateTime after = File.GetLastWriteTimeUtc(store.FilePath);

                // Nothing was old enough to drop - PruneOlderThan should not
                // have touched the file at all (RewriteAtomic only runs
                // when the entry count actually changed).
                Assert.Equal(before, after);
            }
        }

        [Fact]
        public void DeleteAll_RemovesFile()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                store.AppendLine(MakeEntry("t", "m"));
                Assert.True(File.Exists(store.FilePath));

                store.DeleteAll();

                Assert.False(File.Exists(store.FilePath));
            }
        }

        [Fact]
        public void DeleteAll_NoFile_DoesNotThrow()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                store.DeleteAll();
            }
        }

        // --- onError callback: real IO failures, not mocked. ---

        [Fact]
        public void AppendLine_DirectoryCreationFails_InvokesOnErrorInsteadOfThrowing()
        {
            using (var tmp = new TempDirectory())
            {
                // A FILE already occupies the exact path the store would
                // need to create as its data directory - Directory.
                // CreateDirectory on that path throws a real IOException.
                string blockingPath = Path.Combine(tmp.Path, "blocked-data-dir");
                File.WriteAllText(blockingPath, "not a directory");

                string capturedMessage = null;
                Exception capturedException = null;
                var store = new ModuleLogStore(blockingPath, (message, ex) =>
                {
                    capturedMessage = message;
                    capturedException = ex;
                });

                store.AppendLine(MakeEntry("t", "m"));

                Assert.NotNull(capturedMessage);
                Assert.NotNull(capturedException);
            }
        }

        [Fact]
        public void ReadAll_FileLockedByAnotherHandle_InvokesOnErrorAndReturnsEmpty()
        {
            using (var tmp = new TempDirectory())
            {
                var seedStore = new ModuleLogStore(tmp.Path);
                seedStore.AppendLine(MakeEntry("t", "m"));

                Exception capturedException = null;
                var store = new ModuleLogStore(tmp.Path, (message, ex) => capturedException = ex);

                using (new FileStream(store.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var result = store.ReadAll();

                    Assert.NotNull(capturedException);
                    Assert.Empty(result);
                }
            }
        }
    }
}
