using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // ModuleLog is an
    // ordinary instantiable class specifically so tests can construct
    // isolated instances (new ModuleLog()) instead of touching the shared
    // ModuleLog.Shared singleton - see ModuleLog's own class doc comment on
    // why (deterministic, non-shared state regardless of xUnit's default
    // cross-class test parallelism; production call sites elsewhere in the
    // pipeline/Module.cs/Views also write through ModuleLog.Shared, which
    // would otherwise make any exact-count assertion here flaky).
    public class ModuleLogTests
    {
        [Fact]
        public void Constructor_NonPositiveCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModuleLog(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModuleLog(-5));
        }

        [Fact]
        public void Write_AppendsToRing_SnapshotReturnsInOrder()
        {
            var log = new ModuleLog();

            log.Write(ModuleLogLevel.Info, "t", "one");
            log.Write(ModuleLogLevel.Warn, "t", "two");
            log.Write(ModuleLogLevel.Error, "t", "three");

            var snapshot = log.Snapshot();

            Assert.Equal(3, snapshot.Count);
            Assert.Equal("one", snapshot[0].Message);
            Assert.Equal("two", snapshot[1].Message);
            Assert.Equal("three", snapshot[2].Message);
            Assert.Equal(ModuleLogLevel.Error, snapshot[2].Level);
        }

        [Fact]
        public void Write_NullMessage_StoresEmptyStringNotNull()
        {
            var log = new ModuleLog();
            log.Write(ModuleLogLevel.Info, "t", null);

            Assert.Equal(string.Empty, log.Snapshot()[0].Message);
        }

        [Fact]
        public void Write_IncrementsVersionByOnePerCall()
        {
            var log = new ModuleLog();
            Assert.Equal(0, log.Version);

            log.Write(ModuleLogLevel.Info, "t", "one");
            Assert.Equal(1, log.Version);

            log.Write(ModuleLogLevel.Info, "t", "two");
            Assert.Equal(2, log.Version);
        }

        [Fact]
        public void Write_ExceedsCapacity_EvictsOldestKeepsNewest()
        {
            var log = new ModuleLog(ringCapacity: 3);

            log.Write(ModuleLogLevel.Info, "t", "one");
            log.Write(ModuleLogLevel.Info, "t", "two");
            log.Write(ModuleLogLevel.Info, "t", "three");
            log.Write(ModuleLogLevel.Info, "t", "four");

            var snapshot = log.Snapshot();

            Assert.Equal(3, snapshot.Count);
            Assert.Equal("two", snapshot[0].Message);
            Assert.Equal("three", snapshot[1].Message);
            Assert.Equal("four", snapshot[2].Message);
            // Version keeps counting every write ever made, independent of
            // ring eviction - it is not "how many are currently held".
            Assert.Equal(4, log.Version);
        }

        [Fact]
        public void Snapshot_WithVersion_StartIndexMatchesAbsolutePosition()
        {
            var log = new ModuleLog(ringCapacity: 5);
            for (int i = 0; i < 3; i++)
            {
                log.Write(ModuleLogLevel.Info, "t", "m" + i);
            }

            var entries = log.Snapshot(out long version);

            Assert.Equal(3, version);
            Assert.Equal(3, entries.Count);
            Assert.Equal(0, version - entries.Count);
        }

        [Fact]
        public void Clear_EmptiesRingButKeepsVersionMonotonic()
        {
            var log = new ModuleLog();
            log.Write(ModuleLogLevel.Info, "t", "one");
            log.Write(ModuleLogLevel.Info, "t", "two");
            long versionBeforeClear = log.Version;

            log.Clear();

            Assert.Empty(log.Snapshot());
            Assert.Equal(versionBeforeClear, log.Version);

            // Version must never move backwards, even across a Clear - a
            // Log tab view mid-poll must never see it decrease.
            log.Write(ModuleLogLevel.Info, "t", "three");
            Assert.Equal(versionBeforeClear + 1, log.Version);
        }

        // --- Concurrency: deterministic coordination (a start gate +
        // Task.WhenAll), not a sleep-based race - failure would show up as
        // a wrong final count/version, not a timing-dependent flake. ---
        [Fact]
        public async Task Write_ConcurrentFromMultipleThreads_NoLostUpdatesNoCorruption()
        {
            var log = new ModuleLog(ringCapacity: 500);
            const int threadCount = 8;
            const int writesPerThread = 50;
            var startGate = new ManualResetEventSlim(false);

            var tasks = new Task[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                int threadIndex = t;
                tasks[t] = Task.Run(() =>
                {
                    startGate.Wait();
                    for (int i = 0; i < writesPerThread; i++)
                    {
                        log.Write(ModuleLogLevel.Info, "concurrency", $"thread={threadIndex} i={i}");
                    }
                });
            }

            startGate.Set();
            await Task.WhenAll(tasks);

            int expectedTotal = threadCount * writesPerThread;
            Assert.Equal(expectedTotal, log.Version);
            Assert.Equal(expectedTotal, log.Snapshot().Count);
        }

        [Fact]
        public async Task Write_ConcurrentExceedingCapacity_RingStaysWithinCapacity()
        {
            var log = new ModuleLog(ringCapacity: 20);
            const int threadCount = 4;
            const int writesPerThread = 25; // 100 total, well over the 20-capacity ring
            var startGate = new ManualResetEventSlim(false);

            var tasks = new Task[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    startGate.Wait();
                    for (int i = 0; i < writesPerThread; i++)
                    {
                        log.Write(ModuleLogLevel.Info, "concurrency", "m");
                    }
                });
            }

            startGate.Set();
            await Task.WhenAll(tasks);

            Assert.Equal(threadCount * writesPerThread, log.Version);
            Assert.Equal(20, log.Snapshot().Count);
        }

        // --- File-sink gating policy (dev/proposals/d2-log-system.md Section 6): tested
        // against a REAL ModuleLogStore/temp dir, not a fake. ---
        [Fact]
        public void Write_DebugLevel_OnlyReachesFileWhenDiagnosticsEnabled()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);

                log.DiagnosticsEnabled = false;
                log.Write(ModuleLogLevel.Debug, "scrolldiag", "hidden");
                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(30)));
                Assert.Empty(store.ReadAll());

                log.DiagnosticsEnabled = true;
                log.Write(ModuleLogLevel.Debug, "scrolldiag", "visible");
                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(30)));
                var afterEnabled = store.ReadAll();
                Assert.Single(afterEnabled);
                Assert.Equal("visible", afterEnabled[0].Message);

                // Both Debug writes land in the ring regardless of the
                // file-sink gate - the ring is always-on, at every level.
                Assert.Equal(2, log.Snapshot().Count);
            }
        }

        [Fact]
        public void Write_InfoWarnError_AlwaysReachFileRegardlessOfDiagnostics()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, 0, null);
                log.DiagnosticsEnabled = false;

                log.Write(ModuleLogLevel.Info, "t", "i");
                log.Write(ModuleLogLevel.Warn, "t", "w");
                log.Write(ModuleLogLevel.Error, "t", "e");

                // The file-sink append happens on a background flush queue
                // (never on the calling thread - see ModuleLog's own class
                // doc comment on why), so the write is only guaranteed to
                // have landed once this returns true.
                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(30)));
                Assert.Equal(3, store.ReadAll().Count);
            }
        }

        // --- Background file-sink flush queue: order preservation and the
        // WaitForPendingFileWrites synchronization helper. ---
        [Fact]
        public void Write_ManyEntriesToFileSink_BackgroundFlushPreservesCallOrder()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, 0, null);

                const int entryCount = 50;
                for (int i = 0; i < entryCount; i++)
                {
                    log.Write(ModuleLogLevel.Info, "t", "m" + i);
                }

                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(30)));

                var result = store.ReadAll();
                Assert.Equal(entryCount, result.Count);
                for (int i = 0; i < entryCount; i++)
                {
                    Assert.Equal("m" + i, result[i].Message);
                }
            }
        }

        [Fact]
        public void WaitForPendingFileWrites_NoStoreAttached_ReturnsTrueImmediately()
        {
            var log = new ModuleLog();
            log.Write(ModuleLogLevel.Info, "t", "m"); // never configured - nothing can be queued
            Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromMilliseconds(50)));
        }

        [Fact]
        public void WaitForPendingFileWrites_NothingEverWritten_ReturnsTrueImmediately()
        {
            var log = new ModuleLog();
            Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromMilliseconds(50)));
        }

        [Fact]
        public void Write_NoStoreAttached_RingOnlyNoFileNoThrow()
        {
            var log = new ModuleLog();
            log.Write(ModuleLogLevel.Warn, "t", "m"); // never configured - must not throw
            Assert.Single(log.Snapshot());
        }

        [Fact]
        public void MaxFileSizeBytes_CanBeUpdatedLiveAfterConfigure()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 999_999_999, onStoreError: null);

                Assert.Equal(999_999_999, log.MaxFileSizeBytes);

                log.MaxFileSizeBytes = 12345;
                Assert.Equal(12345, log.MaxFileSizeBytes);
            }
        }

        // The guarantee Module's LogMaxSizeBytes SettingChanged handler
        // depends on: a cap lowered after Configure governs the very next
        // write, this session, without a reload.
        [Fact]
        public void MaxFileSizeBytes_LoweredAfterConfigure_TrimsOnTheNextWrite()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 999_999_999, onStoreError: null);

                for (int i = 0; i < 20; i++)
                {
                    log.Write(ModuleLogLevel.Info, "t", "entry " + i);
                }

                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(30)));
                Assert.Equal(20, store.ReadAll().Count);

                log.MaxFileSizeBytes = 200;
                log.Write(ModuleLogLevel.Info, "t", "after the cap dropped");
                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(30)));

                var onDisk = store.ReadAll();
                Assert.True(onDisk.Count < 21, "the lowered cap did not trim: " + onDisk.Count + " entries");
                Assert.Equal("after the cap dropped", onDisk[onDisk.Count - 1].Message);
            }
        }

        // --- Store integration: end-to-end guarantees, not "which layer
        // caught the exception" - ModuleLogStore's own public methods
        // already catch internally and never propagate (see its own doc
        // comment), so these exercise the real, reachable guarantee: a
        // file-sink failure never throws out of ModuleLog.Write. ---
        [Fact]
        public void Write_FileSinkAppendFails_DoesNotThrowAndRingStillReceivesEntry()
        {
            using (var tmp = new TempDirectory())
            {
                string blockingPath = Path.Combine(tmp.Path, "blocked");
                File.WriteAllText(blockingPath, "not a directory");

                var store = new ModuleLogStore(blockingPath);
                var log = new ModuleLog();
                log.Configure(store, 0, onStoreError: null);

                log.Write(ModuleLogLevel.Warn, "t", "m");

                Assert.Single(log.Snapshot());
            }
        }

        [Fact]
        public void SeedFromStore_LoadsHistoryBeforeAnySessionWrites()
        {
            using (var tmp = new TempDirectory())
            {
                var seedStore = new ModuleLogStore(tmp.Path);
                seedStore.AppendLine(new ModuleLogEntry
                {
                    TimestampUtc = DateTime.UtcNow.AddHours(-1),
                    Level = ModuleLogLevel.Info,
                    Tag = "history",
                    Message = "old",
                });

                var log = new ModuleLog();
                var store = new ModuleLogStore(tmp.Path);
                log.Configure(store, 0, null);
                log.SeedFromStore();
                log.Write(ModuleLogLevel.Info, "session", "new");

                var snapshot = log.Snapshot();
                Assert.Equal(2, snapshot.Count);
                Assert.Equal("history", snapshot[0].Tag);
                Assert.Equal("session", snapshot[1].Tag);
            }
        }

        [Fact]
        public void SeedFromStore_NoStoreAttached_IsNoOp()
        {
            var log = new ModuleLog();
            log.SeedFromStore();
            Assert.Empty(log.Snapshot());
        }

        [Fact]
        public void PruneOlderThan_DelegatesToAttachedStore()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                store.AppendLine(new ModuleLogEntry
                {
                    TimestampUtc = DateTime.UtcNow.AddDays(-30),
                    Level = ModuleLogLevel.Info,
                    Tag = "old",
                    Message = "m",
                });

                var log = new ModuleLog();
                log.Configure(store, 0, null);
                log.PruneOlderThan(14);

                Assert.Empty(store.ReadAll());
            }
        }

        [Fact]
        public void PruneOlderThan_NoStoreAttached_IsNoOp()
        {
            var log = new ModuleLog();
            log.PruneOlderThan(14);
        }

        // --- DeleteFileAndReset: the destructive "clear log file" action
        // (file + ring together, plus a trace entry), against a REAL
        // ModuleLogStore/temp dir. ---
        [Fact]
        public void DeleteFileAndReset_ClearsRingAndFile_LeavesOnlyTraceEntry()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, 0, null);

                log.Write(ModuleLogLevel.Info, "t", "one");
                log.Write(ModuleLogLevel.Warn, "t", "two");
                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(30)));
                Assert.Equal(2, store.ReadAll().Count);
                long versionBefore = log.Version;

                log.DeleteFileAndReset();
                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(30)));

                // Ring holds only the trace entry; Version stayed monotonic
                // (the delete's own trace write bumped it, nothing reset it).
                var snapshot = log.Snapshot();
                Assert.Single(snapshot);
                Assert.Equal(ModuleLogLevel.Info, snapshot[0].Level);
                Assert.Contains("deleted", snapshot[0].Message, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(versionBefore + 1, log.Version);

                // File was recreated with exactly the trace entry.
                var fileEntries = store.ReadAll();
                Assert.Single(fileEntries);
                Assert.Contains("deleted", fileEntries[0].Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void DeleteFileAndReset_NextSessionSeed_DoesNotResurrectDeletedEntries()
        {
            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, 0, null);

                log.Write(ModuleLogLevel.Info, "t", "pre-delete");
                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(30)));

                log.DeleteFileAndReset();
                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(30)));

                // Simulate the next session: a fresh ModuleLog seeding from
                // the same on-disk store. This is exactly why a view-only
                // floor is not enough - the file seed must have nothing to
                // resurrect except the trace entry.
                var nextSession = new ModuleLog();
                nextSession.Configure(new ModuleLogStore(tmp.Path), 0, null);
                nextSession.SeedFromStore();

                var seeded = nextSession.Snapshot();
                Assert.Single(seeded);
                Assert.Contains("deleted", seeded[0].Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void DeleteFileAndReset_NoStoreAttached_ClearsRingAndWritesTraceEntry()
        {
            var log = new ModuleLog();
            log.Write(ModuleLogLevel.Info, "t", "one");

            log.DeleteFileAndReset();

            var snapshot = log.Snapshot();
            Assert.Single(snapshot);
            Assert.Contains("deleted", snapshot[0].Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
