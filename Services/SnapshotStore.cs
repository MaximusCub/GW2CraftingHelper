using System;
using System.Diagnostics;
using System.IO;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class SnapshotStore
    {
        private readonly string _filePath;

        public SnapshotStore(string dataDirectoryPath)
        {
            _filePath = Path.Combine(dataDirectoryPath, "snapshot.json");
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
                Debug.WriteLine($"Failed to load snapshot from {_filePath}: {ex.Message}");
                return null;
            }
        }

        public void Save(AccountSnapshot snapshot)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = Serialize(snapshot);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save snapshot to {_filePath}: {ex.Message}");
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
                Debug.WriteLine($"Failed to delete snapshot at {_filePath}: {ex.Message}");
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
