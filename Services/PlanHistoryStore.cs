using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Persists the Plan History index to data/plan_history.json - the
    /// cheap half of the index/blob split (see Models/PlanHistoryEntry.cs
    /// for why the split exists). Atomic .tmp + Replace under a save lock,
    /// matching PlanStore, because it too has two genuinely independent
    /// writers (the capture path's ThreadPool continuation and the tab's
    /// main-thread mutations).
    /// <para>
    /// Schema handling is deliberately FORGIVING, unlike PlanStore's throw:
    /// a file stamped anywhere in [PlanHistoryIndex.
    /// MinimumReadableSchemaVersion, CurrentSchemaVersion] loads its rows
    /// and is restamped by the next Save, silently. Only a version outside
    /// that range, or a file that will not parse, costs the history: one
    /// Warn through onError, then an empty index the next Save overwrites.
    /// The bad file is left on disk for inspection, never deleted here.
    /// </para>
    /// <para>Derivation: docs/ARCHITECTURE.md section 12.</para>
    /// </summary>
    internal class PlanHistoryStore
    {
        private readonly string _filePath;
        private readonly Action<string, Exception> _onError;
        private readonly object _saveLock = new object();

        public PlanHistoryStore(string dataDirectoryPath, Action<string, Exception> onError = null)
        {
            _filePath = Path.Combine(dataDirectoryPath, "plan_history.json");
            _onError = onError;
        }

        /// <summary>
        /// Never null, never throws. A missing file is first run, not a
        /// failure, and does not fire onError. Corrupt JSON or a
        /// SchemaVersion this build cannot read fires onError exactly once
        /// and returns an empty index; a version it CAN read returns the
        /// rows, whatever version stamped them.
        /// </summary>
        public PlanHistoryIndex Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return EmptyIndex();
                }

                string json = File.ReadAllText(_filePath);
                var loaded = JsonConvert.DeserializeObject<PlanHistoryIndex>(json);

                if (loaded == null || !IsReadableVersion(loaded.SchemaVersion))
                {
                    // A RANGE, not an equality. Exact-match was what made a
                    // version bump cost every row a user had: the rows are
                    // additive-only (PlanHistoryIndex.SchemaShapeHash is
                    // what holds them to that), so a file stamped at any
                    // version this build shipped is readable, and Save
                    // restamps it. Only 0/absent, a negative, or a version
                    // from a newer build lands here.
                    _onError?.Invoke(
                        $"Plan history index at {_filePath} is schema {loaded?.SchemaVersion.ToString() ?? "unreadable"}, and this module reads {PlanHistoryIndex.MinimumReadableSchemaVersion} to {PlanHistoryIndex.CurrentSchemaVersion}; starting from an empty history",
                        null);
                    return EmptyIndex();
                }

                if (loaded.Entries == null)
                {
                    loaded.Entries = new List<PlanHistoryEntry>();
                }

                // A row without an id cannot link to a blob, be pinned, or
                // be deleted individually; drop it rather than carry it.
                loaded.Entries.RemoveAll(e => e == null || string.IsNullOrEmpty(e.EntryId));

                return loaded;
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to load the plan history index from {_filePath}", ex);
                return EmptyIndex();
            }
        }

        /// <summary>
        /// Atomic .tmp + File.Replace/File.Move under the save lock. Sets
        /// SchemaVersion explicitly, never via an initializer, so an index
        /// built by deserialization cannot carry a stale value forward.
        /// </summary>
        public void Save(PlanHistoryIndex index)
        {
            if (index == null)
            {
                return;
            }

            index.SchemaVersion = PlanHistoryIndex.CurrentSchemaVersion;

            lock (_saveLock)
            {
                try
                {
                    string dir = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    string json = JsonConvert.SerializeObject(index, Formatting.Indented);
                    string tmpPath = _filePath + ".tmp";
                    File.WriteAllText(tmpPath, json);

                    if (File.Exists(_filePath))
                    {
                        File.Replace(tmpPath, _filePath, null);
                    }
                    else
                    {
                        File.Move(tmpPath, _filePath);
                    }
                }
                catch (Exception ex)
                {
                    _onError?.Invoke($"Failed to save the plan history index to {_filePath}", ex);
                }
            }
        }

        private static bool IsReadableVersion(int schemaVersion)
        {
            return schemaVersion >= PlanHistoryIndex.MinimumReadableSchemaVersion
                && schemaVersion <= PlanHistoryIndex.CurrentSchemaVersion;
        }

        private static PlanHistoryIndex EmptyIndex()
        {
            return new PlanHistoryIndex { SchemaVersion = PlanHistoryIndex.CurrentSchemaVersion };
        }
    }
}
