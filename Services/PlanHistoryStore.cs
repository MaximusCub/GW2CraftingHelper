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
    /// Schema mismatch is deliberately FORGIVING, unlike PlanStore's
    /// throw: a corrupt plan.json costs the user one plan, but a thrown
    /// index load would cost them the whole tab. One Warn through onError,
    /// then an empty index the next Save overwrites. The bad file is left
    /// on disk for inspection, never deleted here.
    /// </para>
    /// <para>
    /// Serialization is Indented, following RankerStore/SnapshotHelpers'
    /// precedent rather than PlanStoreHelpers' compact one: the index is
    /// single-digit KB and rewritten once per Generate, not per pill
    /// click, so the compact decision's rationale does not apply.
    /// </para>
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
        /// SchemaVersion mismatch fires onError exactly once and returns
        /// an empty index.
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

                if (loaded == null || loaded.SchemaVersion != PlanHistoryIndex.CurrentSchemaVersion)
                {
                    _onError?.Invoke(
                        $"Plan history index at {_filePath} is not a version this module can read; starting from an empty history",
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

        private static PlanHistoryIndex EmptyIndex()
        {
            return new PlanHistoryIndex { SchemaVersion = PlanHistoryIndex.CurrentSchemaVersion };
        }
    }
}
