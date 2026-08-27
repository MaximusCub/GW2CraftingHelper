using System;
using System.IO;
using System.Text;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Loads/saves the generated Crafting Plan tab's content so it
    /// survives a module
    /// close/reopen. Mirrors SnapshotStore's shape (single-file JSON,
    /// atomic .tmp+Replace write) with one deliberate divergence: an
    /// unreadable file is NOT silently swallowed to null the
    /// way SnapshotStore's Deserialize is - it is logged before falling
    /// back to null (see PlanStoreHelpers.DeserializePersistedPlan's own
    /// doc comment). A missing file is still silent - "fresh start" with no
    /// plan is the ordinary first-run case, not a failure.
    /// <para>
    /// Two unreadable-file verdicts, two severities, because merging them
    /// once cost a full forensic investigation (2026-08-23): a corrupt or
    /// otherwise unparseable file goes to onError (Warn, same as every I/O
    /// failure below), while a file written at an older SHIPPED schema
    /// version - expected, benign, and repaired by the next Generate - goes
    /// to onInfo (Info). Any caller wiring one and not the other silently
    /// drops half the story.
    /// </para>
    /// <para>
    /// Unlike SnapshotStore/StatusStore (whose callers are already
    /// serialized by a higher-level in-flight guard - see Module's own
    /// _refreshInProgress), Save has two genuinely independent call sites
    /// (a Generate's own post-await persist, and a pill-click override
    /// re-solve's fire-and-forget background persist - see Module.cs's
    /// PersistAfterGenerateAsync/PersistResolvedPlanInBackground) that can
    /// race each other (a decision pill on an OLD plan stays clickable
    /// while a NEW Generate is in flight). Save takes an internal lock so
    /// two overlapping writers can never both be mid-write to the same
    /// .tmp path at once - see the field's own comment.
    /// </para>
    /// <para>
    /// The on-disk container is gzip (a large plan's compact JSON runs
    /// ~700 KB, and this file is rewritten on every override-resolve
    /// pill click, not just once per Generate). The plan.json name is
    /// kept as-is (no .gz rename) - LoadLatest sniffs the first two
    /// bytes for the gzip magic number (0x1F 0x8B) so an existing
    /// plain-JSON plan.json from before this change (PR #107) still
    /// loads. Save always writes gzip going forward. The payload schema
    /// (SchemaVersion, PlanStructuralValidator's gate) is completely
    /// unchanged - only the container encoding differs, so every
    /// existing tolerance guarantee (truncated/corrupt data, one Warn,
    /// return null, never partial) is preserved by construction: both
    /// decompression and JSON parsing happen inside LoadLatest's single
    /// try/catch below.
    /// </para>
    /// </summary>
    internal class PlanStore
    {
        private readonly string _filePath;

        // See StatusStore's
        // matching field comment.
        private readonly Action<string, Exception> _onError;

        // The benign counterpart of _onError, unique to this store: it
        // carries the one load outcome that is neither a failure nor
        // silent. Severity lives at the wiring site (Module.cs), same as
        // _onError - the store itself stays logging-framework-free.
        private readonly Action<string> _onInfo;

        // Serializes Save only - see this class's own doc comment for why
        // (two genuinely independent callers, unlike every other store in
        // this module). LoadLatest needs no lock: the atomic .tmp+Replace
        // write below means a concurrent read can only ever observe the
        // fully-old or fully-new file, never a torn one - the same
        // reasoning that motivated the atomic-write pattern in the first
        // place.
        private readonly object _saveLock = new object();

        public PlanStore(
            string dataDirectoryPath,
            Action<string, Exception> onError = null,
            Action<string> onInfo = null)
        {
            _filePath = Path.Combine(dataDirectoryPath, "plan.json");
            _onError = onError;
            _onInfo = onInfo;
        }

        public PersistedPlan LoadLatest()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(_filePath);
                string json = GzipJsonFile.IsGzip(bytes)
                    ? GzipJsonFile.DecompressToJson(bytes)
                    : Encoding.UTF8.GetString(bytes);
                return Deserialize(json);
            }
            catch (PlanSchemaVersionMismatchException ex)
            {
                // Deliberately NOT routed through _onError: nothing failed.
                // The message already names both versions and says what
                // happens next, so it needs no "Failed to load" framing.
                _onInfo?.Invoke(ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to load plan from {_filePath}", ex);
                return null;
            }
        }

        // Atomic .tmp+Replace write, matching SnapshotStore/StatusStore/
        // VendorOfferStore's own one-store-convention - a crash/
        // power-loss mid-write can never leave a half-written plan.json
        // that LoadLatest then fails to parse.
        public void Save(PersistedPlan plan)
        {
            try
            {
                lock (_saveLock)
                {
                    string dir = Path.GetDirectoryName(_filePath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    string json = Serialize(plan);
                    byte[] compressed = GzipJsonFile.Compress(json);
                    string tmpPath = _filePath + ".tmp";
                    File.WriteAllBytes(tmpPath, compressed);

                    if (File.Exists(_filePath))
                    {
                        File.Replace(tmpPath, _filePath, null);
                    }
                    else
                    {
                        File.Move(tmpPath, _filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to save plan to {_filePath}", ex);
            }
        }

        internal static string Serialize(PersistedPlan plan)
        {
            return PlanStoreHelpers.SerializePersistedPlan(plan);
        }

        internal static PersistedPlan Deserialize(string json)
        {
            return PlanStoreHelpers.DeserializePersistedPlan(json);
        }
    }
}
