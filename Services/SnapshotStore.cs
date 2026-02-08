using System;
using System.IO;
using Blish_HUD;
using GW2CraftingHelper.Models;
using Newtonsoft.Json;

namespace GW2CraftingHelper.Services {

    public class SnapshotStore {

        private static readonly Logger Logger = Logger.GetLogger<SnapshotStore>();

        private readonly string _filePath;

        public SnapshotStore(string dataDirectoryPath) {
            _filePath = Path.Combine(dataDirectoryPath, "snapshot.json");
        }

        public AccountSnapshot LoadLatest() {
            try {
                if (!File.Exists(_filePath)) return null;
                string json = File.ReadAllText(_filePath);
                return Deserialize(json);
            } catch (Exception ex) {
                Logger.Warn(ex, "Failed to load snapshot from {FilePath}", _filePath);
                return null;
            }
        }

        public void Save(AccountSnapshot snapshot) {
            try {
                string dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = Serialize(snapshot);
                File.WriteAllText(_filePath, json);
            } catch (Exception ex) {
                Logger.Warn(ex, "Failed to save snapshot to {FilePath}", _filePath);
            }
        }

        internal static string Serialize(AccountSnapshot snapshot) {
            return SnapshotHelpers.SerializeSnapshot(snapshot);
        }

        internal static AccountSnapshot Deserialize(string json) {
            return SnapshotHelpers.DeserializeSnapshot(json);
        }
    }

}
