using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Module-wide structured log sink (docs/dev-notes/m38-plan/proposals/
    /// d2-log-system.md Section 4). Two
    /// responsibilities:
    /// <list type="number">
    /// <item>A thread-safe, fixed-capacity in-memory ring buffer, always
    /// populated at every level regardless of any setting - this is what
    /// backs the Log tab's live view.</item>
    /// <item>An optional, gated file sink (a <see cref="ModuleLogStore"/>
    /// attached via <see cref="Configure"/>) - Error/Warn/Info always reach
    /// it, Debug only when <see cref="DiagnosticsEnabled"/> is true (the
    /// disk-usage policy the user directive asked for - see d2 Section 6).
    /// </item>
    /// </list>
    /// <para>
    /// An ordinary instantiable class (not a static class) so tests can
    /// construct isolated instances (<c>new ModuleLog()</c>) with
    /// deterministic, non-shared state regardless of xUnit's default
    /// cross-class test parallelism. Production call sites throughout the
    /// codebase use the single app-wide <see cref="Shared"/> instance
    /// instead of threading a ModuleLog dependency through every
    /// constructor - this is deliberately the "static-or-singleton" shape
    /// d2 Section 4.1 describes, resolved as a singleton-by-default
    /// instantiable type rather than a true static class specifically so
    /// it stays testable.
    /// </para>
    /// <para>
    /// Blish-free (no Blish_HUD/Gw2Sharp/Microsoft.Xna usings) - see
    /// ModuleLogEntry's own doc comment. <see cref="Write"/> must never
    /// touch a Blish control directly; the Log tab view reads the ring on
    /// its own cadence (a Version poll), not via a push callback from here -
    /// the same producer/consumer separation Module.cs already uses for its
    /// own dirty-flag fields.
    /// </para>
    /// <para>
    /// Two independent locks guard two independent concerns, deliberately
    /// never held together by any caller other than <see cref="SeedFromStore"/>
    /// (see its own doc comment for why that one case is safe):
    /// <see cref="_gate"/> guards the in-memory ring/Version (fast, pure
    /// in-memory work, taken by every <see cref="Write"/> call and by
    /// <see cref="Snapshot()"/>) and <see cref="_fileGate"/> guards the
    /// attached file sink (slow, real disk IO). <see cref="Write"/> never
    /// performs disk IO itself - it hands the entry to a single-consumer
    /// background flush queue instead - so neither lock, and therefore
    /// neither the Log tab's every-frame <see cref="Version"/> poll nor any
    /// other caller's ring access, can ever block behind file IO regardless
    /// of which thread called <see cref="Write"/> or how large the file has
    /// grown. This was a real, live hazard prior to this fix: the
    /// [scrolldiag] Debug channel (CraftingPlanView) calls <see cref="Write"/>
    /// on the main/UI thread from inside its frame-timing-sensitive
    /// scroll-verify loop, so any synchronous disk IO
    /// performed by Write itself - never mind an occasional full-file
    /// read+rewrite trim pass - would stall that exact frame.
    /// </para>
    /// </summary>
    public class ModuleLog
    {
        public const int DefaultRingCapacity = 2000;

        // Backpressure safety valve for the background file-write queue
        // (see EnqueueFileWrite): bounds worst-case memory growth if the
        // flush loop cannot keep up with the producer (e.g. a persistently
        // locked/very slow disk) - "file I/O off the UI thread without
        // unbounded queue growth" is d2 Section 11's own explicit framing
        // of this requirement. A dropped entry under this extreme condition
        // still lands in the ring regardless (see Write); only the on-disk
        // copy is lost. Not separately unit tested (see the class's own
        // "belt-and-braces" precedent elsewhere in this file) - reliably
        // driving a real ModuleLogStore's flush loop past 5000 queued
        // entries in a deterministic test would require artificially
        // slowing down real disk IO, which the repo's real-IO-only test
        // policy does not have a means to do.
        private const int MaxQueuedFileWrites = 5000;

        // The floor level that reaches the file sink even when
        // DiagnosticsEnabled is false. Hardcoded rather than a settings
        // knob per docs/dev-notes/m38-plan/proposals/tab-roadmap-proposal.md's
        // explicit rejection
        // ("LogMinFileLevel as a UI control - hardcode Info floor") - this
        // constant IS that floor. Debug's own file-sink gate below is
        // governed purely by DiagnosticsEnabled, not by this floor.
        private const ModuleLogLevel MinFileLevel = ModuleLogLevel.Info;

        private static readonly Lazy<ModuleLog> SharedInstance = new Lazy<ModuleLog>(() => new ModuleLog());

        /// <summary>The single app-wide instance production call sites use.</summary>
        public static ModuleLog Shared => SharedInstance.Value;

        // Guards ONLY the ring/_totalWritten/_count - see the class doc
        // comment's two-lock split. Never held while calling into the file
        // sink; see Write/SeedFromStore.
        private readonly object _gate = new object();
        private readonly ModuleLogEntry[] _ring;
        private readonly int _capacity;

        // Guards ONLY the attached file sink and the fields describing it
        // (_store/_maxFileSizeBytes/_onStoreError) - see the class doc
        // comment's two-lock split. Held for the duration of every actual
        // call into _store (AppendLine/ReadAll/PruneOlderThan), which
        // serializes the background flush loop against SeedFromStore's own
        // one-time file read and against a live Configure/MaxFileSizeBytes
        // change, without ever contending with _gate.
        private readonly object _fileGate = new object();

        // Total entries ever written (monotonic, never reset - see Clear's
        // own doc comment) - doubles as both the ring's next write slot
        // (mod capacity) and the publicly observed Version counter the Log
        // tab polls to detect new arrivals without a full rebuild. Mutated
        // only under _gate (via Interlocked.Increment, in
        // AppendToRingLocked), but read lock-free by the Version property
        // via Interlocked.Read so a background flush/trim pass - or any
        // other _fileGate work - can never delay it.
        private long _totalWritten;
        private int _count;

        // volatile (not just _fileGate-guarded) specifically so Write can
        // check "is a store attached at all" without ever taking _fileGate
        // - see Write's own doc comment. _fileGate itself still guards
        // every actual call INTO the store (AppendLine/ReadAll/
        // PruneOlderThan) and the two fields below it, which is the
        // serialization that actually matters (the store is not
        // thread-safe against concurrent calls into itself); this field's
        // volatility only guarantees a prompt, lock-free, torn-read-free
        // view of the reference itself.
        private volatile ModuleLogStore _store;
        private Action<string, Exception> _onStoreError;
        private long _maxFileSizeBytes;
        private volatile bool _diagnosticsEnabled;

        // Single-consumer background flush queue for the file sink (see
        // the class doc comment). Enqueue (on whatever thread called
        // Write) is a cheap, lock-free, in-memory operation; the actual
        // disk IO always happens on a ThreadPool thread via FlushLoop,
        // never on the caller's own thread. A ConcurrentQueue is a strict
        // FIFO, and _flushScheduled below guarantees at most one FlushLoop
        // drains it at a time, so entries always reach the file in the
        // exact order Write was called in - required for a diagnostic
        // channel whose entire purpose is reconstructing a precise
        // sequence of frame events.
        private readonly ConcurrentQueue<ModuleLogEntry> _fileWriteQueue = new ConcurrentQueue<ModuleLogEntry>();

        // 0 = no flush loop currently running/scheduled, 1 = one is. Only
        // ever flipped via Interlocked.CompareExchange/Volatile.Write - see
        // ScheduleFlush/FlushLoop.
        private int _flushScheduled;

        // Count of file-write entries enqueued but not yet fully processed
        // (written or failed-with-callback) - lets WaitForPendingFileWrites
        // detect true idleness without the check-then-flag-clear race a
        // plain "is the queue empty" check would have against a
        // just-finished FlushLoop that has not yet decremented.
        private int _pendingFileWrites;

        public ModuleLog(int ringCapacity = DefaultRingCapacity)
        {
            if (ringCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ringCapacity), "Ring capacity must be positive.");
            }

            _capacity = ringCapacity;
            _ring = new ModuleLogEntry[ringCapacity];
        }

        /// <summary>
        /// Monotonically increasing count of entries ever written (seed +
        /// session). Lock-free (Interlocked.Read) so the Log tab's
        /// every-frame poll can never block behind ring or file-sink work
        /// on another thread - see the class doc comment. The Log tab
        /// compares its own last-seen value against this each poll and
        /// only re-reads when it changed (d2 Section 4.3).
        /// </summary>
        public long Version => Interlocked.Read(ref _totalWritten);

        /// <summary>
        /// Gates whether Debug-level entries reach the file sink (they
        /// always still land in the ring regardless). Mirrors
        /// ModuleSettings.LogDiagnosticsEnabled - Module.cs pushes the
        /// persisted setting value in here at load and on every checkbox
        /// change; this class itself has no Blish/settings dependency.
        /// </summary>
        public bool DiagnosticsEnabled
        {
            get { return _diagnosticsEnabled; }
            set { _diagnosticsEnabled = value; }
        }

        /// <summary>
        /// Size cap (bytes) used by every subsequent file-sink write's
        /// self-trim check - mirrors ModuleSettings.LogMaxSizeBytes.
        /// Exposed as its own settable property (not just a Configure(...)
        /// parameter) so the Settings tab can push a freshly-saved value
        /// live, the same way <see cref="DiagnosticsEnabled"/> already does
        /// for its own setting, without needing to re-supply the
        /// store/onError callback Configure also takes. Guarded by
        /// <see cref="_fileGate"/>, not <see cref="_gate"/> - see the class
        /// doc comment.
        /// </summary>
        public long MaxFileSizeBytes
        {
            get
            {
                lock (_fileGate)
                {
                    return _maxFileSizeBytes;
                }
            }

            set
            {
                lock (_fileGate)
                {
                    _maxFileSizeBytes = value;
                }
            }
        }

        /// <summary>
        /// Attaches (or detaches, with a null store) the file sink and its
        /// error callback, and sets the size cap used by every subsequent
        /// write's self-trim check. Safe to call more than once. The
        /// callback must never itself call back into this ModuleLog - see
        /// ModuleLogStore's own doc comment on why (unbounded recursion
        /// into the sink whose own write just failed).
        /// <para>
        /// Belt-and-braces, not the primary error path: every
        /// ModuleLogStore public method already has its own internal
        /// try/catch and never propagates an exception (it calls ITS OWN
        /// onError constructor parameter instead and returns normally), so
        /// in ordinary operation <paramref name="onStoreError"/> here is
        /// only ever reached if a store call somehow throws outside that
        /// internal catch (a bug in the store itself). Callers should still
        /// wire the store's OWN constructor onError to whatever they want
        /// store failures reported to (Module.cs wires both to the same
        /// target) - this parameter exists as defense-in-depth, not as the
        /// thing a caller should rely on seeing fire.
        /// </para>
        /// </summary>
        public void Configure(ModuleLogStore store, long maxFileSizeBytes, Action<string, Exception> onStoreError)
        {
            lock (_fileGate)
            {
                _store = store;
                _maxFileSizeBytes = maxFileSizeBytes;
                _onStoreError = onStoreError;
            }
        }

        /// <summary>
        /// Seeds the ring from the attached store's persisted history (d2
        /// Section 7, Open Question 2 - resolved YES: pre-session history
        /// is visible on first tab-open, not just "since this launch").
        /// Meant to be called once, at Module.Initialize (immediately after
        /// Configure, before any other store is constructed), so the
        /// seeded history sorts before anything this session writes. A
        /// no-op if no store is attached.
        /// <para>
        /// Holds <see cref="_fileGate"/> for the whole read+seed (serializing
        /// against the background flush loop and against PruneOlderThan, so
        /// this read can never race a concurrent file write/rewrite), and
        /// nests <see cref="_gate"/> only around the ring-append portion
        /// (serializing against a concurrent Write's own ring append from a
        /// background continuation during startup, e.g. the build-ID
        /// fetch's Task.Run - without this, a brand-new entry could land in
        /// the ring chronologically BEFORE the seeded history). This is the
        /// one place in the class that holds both locks at once, always in
        /// this order (_fileGate then _gate) - every other path
        /// (Write/PruneOlderThan/Configure/the file-sink property/FlushLoop)
        /// only ever holds one of the two at a time, so no other code path
        /// can complete the opposite ordering and deadlock against this.
        /// </para>
        /// </summary>
        public void SeedFromStore()
        {
            lock (_fileGate)
            {
                if (_store == null)
                {
                    return;
                }

                IReadOnlyList<ModuleLogEntry> history;
                try
                {
                    history = _store.ReadAll();
                }
                catch (Exception ex)
                {
                    _onStoreError?.Invoke("Failed to seed log ring from store", ex);
                    return;
                }

                if (history == null)
                {
                    return;
                }

                lock (_gate)
                {
                    foreach (var entry in history)
                    {
                        AppendToRingLocked(entry);
                    }
                }
            }
        }

        /// <summary>
        /// Once-per-session age-based prune (Module.Initialize, before
        /// SeedFromStore) against the attached store, using the persisted
        /// LogRetentionDays value. A no-op if no store is attached or
        /// retentionDays &lt;= 0. Guarded by <see cref="_fileGate"/> only -
        /// this never touches the ring, so it never needs <see cref="_gate"/>.
        /// </summary>
        public void PruneOlderThan(int retentionDays)
        {
            lock (_fileGate)
            {
                if (_store == null)
                {
                    return;
                }

                try
                {
                    _store.PruneOlderThan(retentionDays);
                }
                catch (Exception ex)
                {
                    _onStoreError?.Invoke("Failed to prune log file", ex);
                }
            }
        }

        /// <summary>
        /// Writes one entry. Safe to call from ANY thread - ThreadPool
        /// continuations, the main/UI thread, or a background Task.Run body
        /// (d2 Section 4.3), including a frame-timing-sensitive one (the
        /// [scrolldiag] channel). Always appends to the ring synchronously
        /// (fast, in-memory, under <see cref="_gate"/> only). When the level
        /// clears the policy in <see cref="ShouldWriteToFile"/> AND a store
        /// is attached, the entry is handed to the background flush queue
        /// instead of being written to disk here.
        /// <para>
        /// Deliberately checks <see cref="_store"/> directly (a volatile
        /// field read) rather than through <see cref="_fileGate"/>: that
        /// lock can legitimately be held for a while by the background
        /// FlushLoop (a slow disk append, or an occasional full-file trim
        /// rewrite) or by SeedFromStore/PruneOlderThan, and this method
        /// must never block waiting for it - doing so would silently
        /// reintroduce the exact cross-thread stall this design exists to
        /// remove, just against a different lock. See the class doc
        /// comment for why this method must never itself perform file IO.
        /// </para>
        /// </summary>
        public void Write(ModuleLogLevel level, string tag, string message)
        {
            var entry = new ModuleLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Level = level,
                Tag = tag,
                Message = message ?? string.Empty
            };

            lock (_gate)
            {
                AppendToRingLocked(entry);
            }

            if (_store != null && ShouldWriteToFile(level))
            {
                EnqueueFileWrite(entry);
            }
        }

        /// <summary>
        /// Blocks the calling thread (a bounded spin-wait, not a lock) until
        /// every file-sink write enqueued so far has been fully processed
        /// (written, or failed-with-callback), or until
        /// <paramref name="timeout"/> elapses. Returns true if it observed
        /// the queue go idle, false on timeout. Production use is limited
        /// to a best-effort drain in Module.Unload (so a burst of recent
        /// diagnostics gets a brief chance to reach disk before the process
        /// tears down); its main purpose is letting tests deterministically
        /// await the background flush instead of asserting on a race.
        /// </summary>
        public bool WaitForPendingFileWrites(TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (Volatile.Read(ref _pendingFileWrites) > 0)
            {
                if (stopwatch.Elapsed >= timeout)
                {
                    return false;
                }

                Thread.Sleep(1);
            }

            return true;
        }

        /// <summary>
        /// Oldest-to-newest snapshot of everything currently held in the
        /// ring (fewer than capacity early in a session, capped at capacity
        /// once it has wrapped - the oldest entries are evicted, not the
        /// file's own history, which is unaffected). A fresh list every
        /// call - callers are free to mutate/filter their own copy.
        /// </summary>
        public IReadOnlyList<ModuleLogEntry> Snapshot()
        {
            return Snapshot(out _);
        }

        /// <summary>
        /// Same as the parameterless <see cref="Snapshot()"/>, plus the
        /// Version the returned list was read at - a single lock-scoped
        /// read of both, so a caller (the Log tab's "Clear view", which
        /// needs to know exactly which absolute entry index each returned
        /// row corresponds to - see LogTabContent) never has to reconcile
        /// two separately-timed reads that a concurrent Write could have
        /// landed between. Guarded by <see cref="_gate"/> only - never
        /// blocks behind file IO.
        /// </summary>
        public IReadOnlyList<ModuleLogEntry> Snapshot(out long version)
        {
            lock (_gate)
            {
                version = _totalWritten;
                var result = new List<ModuleLogEntry>(_count);
                long start = _totalWritten - _count;
                for (long i = start; i < _totalWritten; i++)
                {
                    result.Add(_ring[(int)(i % _capacity)]);
                }

                return result;
            }
        }

        /// <summary>
        /// Clears the in-memory ring only (never the on-disk file - see
        /// ModuleLogStore.DeleteAll for that). Production callers are
        /// Module.Unload and <see cref="DeleteFileAndReset"/>; deliberately
        /// does NOT reset Version/_totalWritten
        /// - Version must stay monotonic so a Log tab mid-poll can never
        /// observe it move backwards.
        /// </summary>
        public void Clear()
        {
            lock (_gate)
            {
                Array.Clear(_ring, 0, _ring.Length);
                _count = 0;
            }
        }

        /// <summary>
        /// The destructive "clear log file" action (d2-log-system.md
        /// Section 7, Open Question 4 - distinct from the Log tab's
        /// view-only Clear): deletes the on-disk file AND clears the
        /// in-memory ring, then writes one Info entry recording the
        /// deletion so the action itself stays traceable (that entry also
        /// recreates the file, via the ordinary flush queue). Both halves
        /// are required - clearing only the view floor would let
        /// SeedFromStore resurrect every entry from the file next session,
        /// and deleting only the file would leave this session's ring
        /// intact. Version stays monotonic throughout (the ring clear is
        /// <see cref="Clear"/>'s own, which never resets it).
        /// <para>
        /// Starts with a brief, bounded drain of the pending flush queue
        /// (same 250ms budget as Module.Unload's own best-effort drain) so
        /// entries queued before this call land in the file BEFORE it is
        /// deleted rather than resurrecting it afterwards. Best-effort: an
        /// entry still in flight past the budget (a hung disk) can land in
        /// the recreated file - a stale line in the new log, not a
        /// correctness hazard. The drain is a spin-wait on the calling
        /// thread; in practice the queue is empty at the moment a user
        /// clicks the button, so the common cost is zero.
        /// </para>
        /// <para>
        /// Blocks the calling thread beyond that budget too: after the
        /// drain it acquires <see cref="_fileGate"/> with no bound and
        /// does real disk IO under it, and FlushLoop can legitimately
        /// hold that lock through a slow append or full-file trim (the
        /// stall <see cref="Write"/>'s doc comment exists to keep off
        /// latency-sensitive threads). Never call this from the main/UI
        /// thread - the Log tab runs it on Task.Run and marshals its UI
        /// tail back.
        /// </para>
        /// </summary>
        public void DeleteFileAndReset()
        {
            WaitForPendingFileWrites(TimeSpan.FromMilliseconds(250));

            lock (_fileGate)
            {
                try
                {
                    _store?.DeleteAll();
                }
                catch (Exception ex)
                {
                    _onStoreError?.Invoke("Failed to delete log file", ex);
                }
            }

            Clear();

            Write(ModuleLogLevel.Info, "log", "Log file deleted by user");
        }

        private void AppendToRingLocked(ModuleLogEntry entry)
        {
            _ring[(int)(_totalWritten % _capacity)] = entry;

            // Interlocked (not a plain increment) even though this always
            // runs under _gate - the Version property reads this field via
            // Interlocked.Read without taking _gate at all (see its own
            // doc comment), and that pairing only guarantees cross-thread
            // visibility on every platform if the write side is also
            // Interlocked.
            Interlocked.Increment(ref _totalWritten);

            if (_count < _capacity)
            {
                _count++;
            }
        }

        private bool ShouldWriteToFile(ModuleLogLevel level)
        {
            if (level == ModuleLogLevel.Debug)
            {
                // Debug's file-sink presence is governed purely by THIS
                // instance's own diagnostics toggle, not by MinFileLevel
                // (which exists to let a maintainer raise the floor for
                // Info/Warn/Error without touching the diagnostics
                // toggle's own meaning - see d2 Section 5's LogMinFileLevel
                // rationale). _diagnosticsEnabled is a plain volatile bool -
                // no lock needed on top of the caller's own _gate.
                return _diagnosticsEnabled;
            }

            return level >= MinFileLevel;
        }

        private void EnqueueFileWrite(ModuleLogEntry entry)
        {
            if (Interlocked.Increment(ref _pendingFileWrites) > MaxQueuedFileWrites)
            {
                // Back-pressure: the flush loop cannot keep up. Undo the
                // count bump and drop this entry from the file sink rather
                // than growing the queue without bound - see
                // MaxQueuedFileWrites' own comment. The entry already
                // landed in the ring (Write's earlier, unconditional
                // AppendToRingLocked call), so it is not lost from the
                // Log tab's live view, only from the on-disk copy.
                Interlocked.Decrement(ref _pendingFileWrites);
                return;
            }

            _fileWriteQueue.Enqueue(entry);
            ScheduleFlush();
        }

        private void ScheduleFlush()
        {
            // Only one FlushLoop may ever be in flight - this both
            // preserves file-write order (a strict single consumer draining
            // a FIFO queue) and means no two threads ever call into
            // _store at once from this path.
            if (Interlocked.CompareExchange(ref _flushScheduled, 1, 0) != 0)
            {
                return;
            }

            Task.Run(FlushLoop);
        }

        private void FlushLoop()
        {
            try
            {
                while (_fileWriteQueue.TryDequeue(out var entry))
                {
                    try
                    {
                        lock (_fileGate)
                        {
                            _store?.AppendLine(entry, _maxFileSizeBytes);
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (_fileGate)
                        {
                            _onStoreError?.Invoke("Failed to append log entry to file sink", ex);
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _pendingFileWrites);
                    }
                }
            }
            finally
            {
                // Clear the "a loop is running" flag BEFORE the recheck
                // below, so a Write that raced in right at the end of the
                // drain (after our last TryDequeue returned false, before
                // we get here) always succeeds at scheduling a fresh loop
                // to pick up whatever it just enqueued, rather than seeing
                // _flushScheduled still set to 1 and silently no-op'ing.
                Volatile.Write(ref _flushScheduled, 0);

                if (!_fileWriteQueue.IsEmpty)
                {
                    ScheduleFlush();
                }
            }
        }
    }
}
