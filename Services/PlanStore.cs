using System;
using System.IO;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// W3D (plan persistence across module restarts): loads/saves the
    /// generated Crafting Plan tab's content so it survives a module
    /// close/reopen. Mirrors SnapshotStore's shape (single-file JSON,
    /// atomic .tmp+Replace write) with one deliberate divergence: a
    /// corrupt or old-schema file is NOT silently swallowed to null the
    /// way SnapshotStore's Deserialize is - it is logged at Warn via
    /// onError, same as every I/O failure below, before falling back to
    /// null (see PlanStoreHelpers.DeserializePersistedPlan's own doc
    /// comment). A missing file is still silent - "fresh start" with no
    /// plan is the ordinary first-run case, not a failure.
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
    /// </summary>
    public class PlanStore
    {
        private readonly string _filePath;

        // M39 (WP-16 shape, d2-log-system.md Section 4.2): see StatusStore's
        // matching field comment.
        private readonly Action<string, Exception> _onError;

        // Serializes Save only - see this class's own doc comment for why
        // (two genuinely independent callers, unlike every other store in
        // this module). LoadLatest needs no lock: the atomic .tmp+Replace
        // write below means a concurrent read can only ever observe the
        // fully-old or fully-new file, never a torn one - the same
        // reasoning that motivated the atomic-write pattern in the first
        // place.
        private readonly object _saveLock = new object();

        public PlanStore(string dataDirectoryPath, Action<string, Exception> onError = null)
        {
            _filePath = Path.Combine(dataDirectoryPath, "plan.json");
            _onError = onError;
        }

        public PersistedPlan LoadLatest()
        {
            try
            {
                if (!File.Exists(_filePath)) return null;
                string json = File.ReadAllText(_filePath);
                return Deserialize(json);
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to load plan from {_filePath}", ex);
                return null;
            }
        }

        // Atomic .tmp+Replace write, matching SnapshotStore/StatusStore/
        // VendorOfferStore's own one-store-convention (M39) - a crash/
        // power-loss mid-write can never leave a half-written plan.json
        // that LoadLatest then fails to parse.
        public void Save(PersistedPlan plan)
        {
            try
            {
                lock (_saveLock)
                {
                    string dir = Path.GetDirectoryName(_filePath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    string json = Serialize(plan);
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
