using System;
using System.IO;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class SnapshotStore
    {
        private readonly string _filePath;

        // See StatusStore's
        // matching field comment.
        private readonly Action<string, Exception> _onError;

        public SnapshotStore(string dataDirectoryPath, Action<string, Exception> onError = null)
        {
            _filePath = Path.Combine(dataDirectoryPath, "snapshot.json");
            _onError = onError;
        }

        public AccountSnapshot LoadLatest()
        {
            try
            {
                if (!File.Exists(_filePath)) return null;
                string json = File.ReadAllText(_filePath);
                return Deserialize(json);
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to load snapshot from {_filePath}", ex);
                return null;
            }
        }

        // M39 (one-store-convention, tab-roadmap-proposal Section 2.2):
        // previously a plain, non-atomic File.WriteAllText - the scout note
        // three independent M38 design proposals (D2/D3/D4) each flagged as
        // wrong (it claimed all three existing stores shared StatusStore's
        // atomic .tmp+Replace pattern; this one did not). Switched to the
        // same atomic pattern StatusStore.Save/VendorOfferStore.SaveOverlay
        // already use, so a crash/power-loss mid-write can no longer leave
        // a half-written snapshot.json that LoadLatest then fails to parse.
        public void Save(AccountSnapshot snapshot)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = Serialize(snapshot);
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
                _onError?.Invoke($"Failed to save snapshot to {_filePath}", ex);
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(_filePath)) File.Delete(_filePath);
            }
            catch (Exception ex)
            {
                _onError?.Invoke($"Failed to delete snapshot at {_filePath}", ex);
            }
        }

        internal static string Serialize(AccountSnapshot snapshot)
        {
            return SnapshotHelpers.SerializeSnapshot(snapshot);
        }

        internal static AccountSnapshot Deserialize(string json)
        {
            return SnapshotHelpers.DeserializeSnapshot(json);
        }
    }
}
