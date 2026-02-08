using System;
using System.Diagnostics;
using System.IO;

namespace GW2CraftingHelper.Services
{
    public class StatusStore
    {
        private readonly string _filePath;

        public StatusStore(string dataDirectoryPath)
        {
            _filePath = Path.Combine(dataDirectoryPath, "status.txt");
        }

        public string Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return "";
                return File.ReadAllText(_filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load status from {_filePath}: {ex.Message}");
                return "";
            }
        }

        public void Save(string status)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string tmpPath = _filePath + ".tmp";
                File.WriteAllText(tmpPath, status ?? "");
                File.Copy(tmpPath, _filePath, true);
                File.Delete(tmpPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save status to {_filePath}: {ex.Message}");
            }
        }
    }
}
