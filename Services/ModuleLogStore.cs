using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// File-IO half of the module logging system (dev/proposals/d2-log-system.md Section
    /// 4.1/4.2): persists ModuleLogEntry lines to a single append-only
    /// newline-delimited JSON file, data/module_log.jsonl. JSONL rather than
    /// one big JSON array (the SnapshotStore/StatusStore/VendorOfferStore
    /// shape) because a log is fundamentally append-heavy and a crash
    /// mid-append to JSONL loses at most the last, tolerated-as-partial
    /// line, whereas a crash mid-write to one big array can corrupt the
    /// entire file.
    /// <para>
    /// Rotation is split into two independently callable, independently
    /// testable operations rather than d2's single combined
    /// "RotateIfNeeded(maxBytes, maxAgeDays)" signature, matching how they
    /// actually fire at different cadences: <see cref="AppendLine"/> self-
    /// trims by SIZE on every call (cheap - one FileInfo.Length syscall),
    /// while <see cref="PruneOlderThan"/> is meant to be called once per
    /// session (Module.LoadAsync) for the AGE-based check. Both use the
    /// same atomic .tmp + File.Replace rewrite every other store in the
    /// module shares (SnapshotStore.Save, StatusStore.Save, PlanStore.Save,
    /// VendorOfferStore.SaveOverlay).
    /// </para>
    /// <para>
    /// Blish-free (see ModuleLogEntry's own doc comment for why) and takes
    /// an <paramref name="onError"/> callback from day one -
    /// called instead of throwing on any IO failure, so a log store failure
    /// can never itself crash the caller. Never logs through ModuleLog on
    /// its own failure (that would be unbounded recursion into the sink
    /// whose own write just failed) - callers should route this callback to
    /// Blish's own Logger only, see Module.cs's wiring.
    /// </para>
    /// </summary>
    internal class ModuleLogStore
    {
        private const string FileName = "module_log.jsonl";

        // Propose 25% per d2 Section 4.2: "drop the oldest N% (propose 25%)
        // of lines by count". Math.Max(1, ...) below guarantees a size trim
        // always makes progress (a handful of very large lines could
        // otherwise round down to a 0-line drop and never converge).
        private const int TrimDivisor = 4;

        private readonly string _filePath;
        private readonly Action<string, Exception> _onError;

        public ModuleLogStore(string dataDirectoryPath, Action<string, Exception> onError = null)
        {
            if (string.IsNullOrEmpty(dataDirectoryPath))
            {
                throw new ArgumentException("Data directory path is required.", nameof(dataDirectoryPath));
            }

            _filePath = Path.Combine(dataDirectoryPath, FileName);
            _onError = onError;
        }

        /// <summary>Absolute path to the backing JSONL file (for tests/diagnostics).</summary>
        public string FilePath => _filePath;

        /// <summary>
        /// Appends one entry as a single JSONL line. A null entry is
        /// ignored. When <paramref name="maxSizeBytes"/> is positive and the
        /// file exceeds it after this append, trims the oldest ~25% of
        /// lines (by count) and rewrites atomically. Any IO failure is
        /// reported via <see cref="_onError"/> and swallowed - never throws
        /// into the caller (ModuleLog.Write must never blow up a caller
        /// mid-generation over a full disk or a locked file).
        /// </summary>
        public void AppendLine(ModuleLogEntry entry, long maxSizeBytes = 0)
        {
            if (entry == null)
            {
                return;
            }

            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(_filePath, SerializeLine(entry) + "\n", Encoding.UTF8);

                if (maxSizeBytes > 0)
                {
                    TrimBySizeIfNeeded(maxSizeBytes);
                }
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to append to log file {_filePath}", ex);
            }
        }

        /// <summary>
        /// Reads every entry currently in the file, oldest first. Returns an
        /// empty list (never null) if the file does not exist. A malformed
        /// or partial line (e.g. a crash mid-append left the last line
        /// truncated) is silently skipped rather than aborting the whole
        /// read - exactly the tolerance JSONL was chosen for. Any IO failure
        /// (file locked, permissions) is reported via onError and returns
        /// whatever was successfully read before the failure.
        /// </summary>
        public IReadOnlyList<ModuleLogEntry> ReadAll()
        {
            var result = new List<ModuleLogEntry>();

            try
            {
                if (!File.Exists(_filePath))
                {
                    return result;
                }

                foreach (var line in File.ReadLines(_filePath, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var entry = TryDeserializeLine(line);
                    if (entry != null)
                    {
                        result.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to read log file {_filePath}", ex);
            }

            return result;
        }

        /// <summary>
        /// Drops every entry older than <paramref name="retentionDays"/>
        /// and rewrites the file atomically if anything changed. A
        /// non-positive <paramref name="retentionDays"/> is a no-op (no
        /// retention limit). Meant to be called once per session
        /// (Module.LoadAsync), not on every append - age-based pruning does
        /// not need per-write cost (d2 Section 4.2).
        /// </summary>
        public void PruneOlderThan(int retentionDays)
        {
            if (retentionDays <= 0)
            {
                return;
            }

            try
            {
                if (!File.Exists(_filePath))
                {
                    return;
                }

                var entries = new List<ModuleLogEntry>(ReadAll());
                DateTime cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);
                int before = entries.Count;
                entries.RemoveAll(e => e.TimestampUtc < cutoffUtc);

                if (entries.Count != before)
                {
                    RewriteAtomic(entries);
                }
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to prune log file {_filePath}", ex);
            }
        }

        /// <summary>
        /// Deletes the on-disk log file entirely (not the in-memory ring -
        /// see ModuleLog.Clear for that). A missing file is not an error.
        /// </summary>
        public void DeleteAll()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to delete log file {_filePath}", ex);
            }
        }

        private void TrimBySizeIfNeeded(long maxSizeBytes)
        {
            var info = new FileInfo(_filePath);
            if (!info.Exists || info.Length <= maxSizeBytes)
            {
                return;
            }

            var entries = new List<ModuleLogEntry>(ReadAll());
            if (entries.Count == 0)
            {
                return;
            }

            int dropCount = Math.Max(1, entries.Count / TrimDivisor);
            dropCount = Math.Min(dropCount, entries.Count);
            entries.RemoveRange(0, dropCount);

            RewriteAtomic(entries);
        }

        private void RewriteAtomic(List<ModuleLogEntry> entries)
        {
            string dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                sb.Append(SerializeLine(entry));
                sb.Append('\n');
            }

            string tmpPath = _filePath + ".tmp";
            File.WriteAllText(tmpPath, sb.ToString(), Encoding.UTF8);

            if (File.Exists(_filePath))
            {
                File.Replace(tmpPath, _filePath, null);
            }
            else
            {
                File.Move(tmpPath, _filePath);
            }
        }

        private static string SerializeLine(ModuleLogEntry entry)
        {
            return JsonConvert.SerializeObject(entry);
        }

        private static ModuleLogEntry TryDeserializeLine(string line)
        {
            try
            {
                return JsonConvert.DeserializeObject<ModuleLogEntry>(line);
            }
            catch (JsonException)
            {
                // Malformed/partial line (crash mid-append, or hand-edited
                // file) - tolerated, see ReadAll's own doc comment.
                return null;
            }
        }
    }
}
