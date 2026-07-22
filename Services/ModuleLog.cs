using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Module-wide structured log sink (d2-log-system.md Section 4). Two
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
    /// </summary>
    public class ModuleLog
    {
        public const int DefaultRingCapacity = 2000;

        // The floor level that reaches the file sink even when
        // DiagnosticsEnabled is false. Hardcoded rather than a settings
        // knob per the tab-roadmap-proposal synthesis's explicit rejection
        // ("LogMinFileLevel as a UI control - hardcode Info floor") - this
        // constant IS that floor. Debug's own file-sink gate below is
        // governed purely by DiagnosticsEnabled, not by this floor.
        private const ModuleLogLevel MinFileLevel = ModuleLogLevel.Info;

        private static readonly Lazy<ModuleLog> SharedInstance = new Lazy<ModuleLog>(() => new ModuleLog());

        /// <summary>The single app-wide instance production call sites use.</summary>
        public static ModuleLog Shared => SharedInstance.Value;

        private readonly object _gate = new object();
        private readonly ModuleLogEntry[] _ring;
        private readonly int _capacity;

        // Total entries ever written (monotonic, never reset - see Clear's
        // own doc comment) - doubles as both the ring's next write slot
        // (mod capacity) and the publicly observed Version counter the Log
        // tab polls to detect new arrivals without a full rebuild.
        private long _totalWritten;
        private int _count;

        private ModuleLogStore _store;
        private Action<string, Exception> _onStoreError;
        private long _maxFileSizeBytes;
        private volatile bool _diagnosticsEnabled;

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
        /// session), incremented under the same lock as every ring write.
        /// The Log tab compares its own last-seen value against this each
        /// poll and only re-reads when it changed (d2 Section 4.3).
        /// </summary>
        public long Version
        {
            get
            {
                lock (_gate)
                {
                    return _totalWritten;
                }
            }
        }

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
        /// Size cap (bytes) used by every subsequent Write's self-trim
        /// check - mirrors ModuleSettings.LogMaxSizeBytes. Exposed as its
        /// own settable property (not just a Configure(...) parameter) so
        /// the Settings tab can push a freshly-saved value live, the same
        /// way <see cref="DiagnosticsEnabled"/> already does for its own
        /// setting, without needing to re-supply the store/onError callback
        /// Configure also takes.
        /// </summary>
        public long MaxFileSizeBytes
        {
            get
            {
                lock (_gate)
                {
                    return _maxFileSizeBytes;
                }
            }

            set
            {
                lock (_gate)
                {
                    _maxFileSizeBytes = value;
                }
            }
        }

        /// <summary>
        /// Attaches (or detaches, with a null store) the file sink and its
        /// error callback, and sets the size cap used by every subsequent
        /// Write's self-trim check. Safe to call more than once. The
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
            lock (_gate)
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
        /// no-op if no store is attached. Runs entirely under the same lock
        /// Write uses, so a concurrent Write from a background continuation
        /// during startup cannot interleave with the seed and land out of
        /// order.
        /// </summary>
        public void SeedFromStore()
        {
            // Held for the whole read+seed, not just the "grab _store"
            // sliver - see Write's own doc comment for why file IO runs
            // inside this lock throughout this class: at this call volume
            // (once per session) a brief hold is free, and it guarantees a
            // concurrent Write from a background continuation (e.g. the
            // build-ID fetch's Task.Run) can never land its entry into the
            // ring in the middle of this seed, which would otherwise put a
            // brand-new entry chronologically BEFORE the seeded history in
            // ring order.
            lock (_gate)
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

                foreach (var entry in history)
                {
                    AppendToRingLocked(entry);
                }
            }
        }

        /// <summary>
        /// Once-per-session age-based prune (Module.Initialize, before
        /// SeedFromStore) against the attached store, using the persisted
        /// LogRetentionDays value. A no-op if no store is attached or
        /// retentionDays &lt;= 0. Deliberately routed through this ModuleLog
        /// instance (rather than Module.cs calling the store directly) so
        /// it serializes against concurrent Write calls under the same
        /// lock - see the class doc comment's producer/consumer separation
        /// note.
        /// </summary>
        public void PruneOlderThan(int retentionDays)
        {
            lock (_gate)
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
        /// (d2 Section 4.3). Always appends to the ring. Appends to the
        /// file sink too when the level clears the policy in
        /// <see cref="ShouldWriteToFile"/> AND a store is attached - that
        /// file append runs inside the SAME lock as the ring write
        /// (deliberately, not released early): this sink sees at most a few
        /// dozen writes per session (d2 Section 6), so serializing the tiny
        /// file IO behind the ring's own lock is simpler than a second lock
        /// and guarantees the file's line order always matches the ring's,
        /// with no measurable cost at this call volume.
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

                if (_store != null && ShouldWriteToFile(level))
                {
                    try
                    {
                        _store.AppendLine(entry, _maxFileSizeBytes);
                    }
                    catch (Exception ex)
                    {
                        _onStoreError?.Invoke("Failed to append log entry to file sink", ex);
                    }
                }
            }
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
        /// landed between.
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
        /// ModuleLogStore.DeleteAll for that). Production's only caller is
        /// Module.Unload; deliberately does NOT reset Version/_totalWritten
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

        private void AppendToRingLocked(ModuleLogEntry entry)
        {
            _ring[(int)(_totalWritten % _capacity)] = entry;
            _totalWritten++;
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
    }
}
