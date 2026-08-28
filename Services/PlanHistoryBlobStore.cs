using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Persists one gzipped PersistedPlan per Plan History entry under
    /// data/plan_history/&lt;EntryId&gt;.json - the expensive half of the
    /// index/blob split (see Models/PlanHistoryEntry.cs). Serialization
    /// goes through PlanStoreHelpers and the container through
    /// GzipJsonFile, so a blob is byte-for-byte the same shape as
    /// plan.json and inherits every tolerance guarantee PlanStore proved.
    /// <para>
    /// Every failure here is contained by design: a blob that cannot be
    /// read costs the user one "Open" button, never the index row, never
    /// the tab. Load fires onError at most once per call and returns
    /// null; the caller clears BlobPresent on the row and the row
    /// degrades to Re-solve.
    /// </para>
    /// </summary>
    internal class PlanHistoryBlobStore
    {
        private readonly string _directoryPath;
        private readonly Action<string, Exception> _onError;
        private readonly object _saveLock = new object();

        public PlanHistoryBlobStore(string dataDirectoryPath, Action<string, Exception> onError = null)
        {
            _directoryPath = Path.Combine(dataDirectoryPath, "plan_history");
            _onError = onError;
        }

        /// <summary>
        /// Null on missing/corrupt/schema-mismatch. Missing is silent
        /// (an evicted or never-written blob is ordinary); everything
        /// else fires onError exactly once. Never throws.
        /// </summary>
        public PersistedPlan Load(string entryId)
        {
            if (!IsValidEntryId(entryId))
            {
                return null;
            }

            string path = BlobPath(entryId);
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(path);
                string json = GzipJsonFile.IsGzip(bytes)
                    ? GzipJsonFile.DecompressToJson(bytes)
                    : Encoding.UTF8.GetString(bytes);
                return PlanStoreHelpers.DeserializePersistedPlan(json);
            }
            catch (Exception ex)
            {
                // Unlike PlanStore, a stale-schema blob routes to onError
                // with the rest: the caller reacts identically (clear
                // BlobPresent, keep the row), and the row's own visible
                // "Not saved in full" note is the user-facing half.
                _onError?.Invoke($"Failed to load the saved plan from {path}", ex);
                return null;
            }
        }

        /// <summary>
        /// Gzip + atomic .tmp + Replace under the save lock. Returns
        /// false on failure (the caller clears BlobPresent).
        /// </summary>
        public bool Save(string entryId, PersistedPlan plan)
        {
            if (!IsValidEntryId(entryId) || plan == null)
            {
                return false;
            }

            string path = BlobPath(entryId);
            lock (_saveLock)
            {
                try
                {
                    if (!Directory.Exists(_directoryPath))
                    {
                        Directory.CreateDirectory(_directoryPath);
                    }

                    string json = PlanStoreHelpers.SerializePersistedPlan(plan);
                    byte[] compressed = GzipJsonFile.Compress(json);
                    string tmpPath = path + ".tmp";
                    File.WriteAllBytes(tmpPath, compressed);

                    if (File.Exists(path))
                    {
                        File.Replace(tmpPath, path, null);
                    }
                    else
                    {
                        File.Move(tmpPath, path);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _onError?.Invoke($"Failed to save the plan history blob to {path}", ex);
                    return false;
                }
            }
        }

        /// <summary>Best-effort. A missing file is success.</summary>
        public bool Delete(string entryId)
        {
            if (!IsValidEntryId(entryId))
            {
                return false;
            }

            string path = BlobPath(entryId);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to delete the plan history blob at {path}", ex);
                return false;
            }
        }

        /// <summary>
        /// Deletes every blob whose entry id is not in
        /// <paramref name="keepIds"/> - the orphan sweep run once at
        /// module load. Returns how many files were deleted.
        /// </summary>
        public int DeleteOrphans(IReadOnlyCollection<string> keepIds)
        {
            int deleted = 0;
            try
            {
                if (!Directory.Exists(_directoryPath))
                {
                    return 0;
                }

                var keep = new HashSet<string>(keepIds ?? new string[0], StringComparer.Ordinal);
                foreach (string path in Directory.GetFiles(_directoryPath, "*.json"))
                {
                    string entryId = Path.GetFileNameWithoutExtension(path);
                    if (!IsValidEntryId(entryId) || keep.Contains(entryId))
                    {
                        continue;
                    }

                    try
                    {
                        File.Delete(path);
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        _onError?.Invoke($"Failed to delete the orphaned plan history blob at {path}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to sweep orphaned plan history blobs under {_directoryPath}", ex);
            }

            return deleted;
        }

        /// <summary>
        /// An EntryId is a Guid "N" string: exactly 32 lowercase hex
        /// characters. Anything else is rejected before any filesystem
        /// touch, so a hand-edited index can never become a path
        /// traversal.
        /// </summary>
        internal static bool IsValidEntryId(string entryId)
        {
            if (entryId == null || entryId.Length != 32)
            {
                return false;
            }

            foreach (char c in entryId)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex)
                {
                    return false;
                }
            }

            return true;
        }

        private string BlobPath(string entryId)
        {
            return Path.Combine(_directoryPath, entryId + ".json");
        }
    }
}
