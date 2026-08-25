using System;
using System.IO;

namespace GW2CraftingHelper.Services
{
    public class StatusStore
    {
        private readonly string _filePath;

        // Called instead of
        // a bare Debug.WriteLine on any IO failure. No-op default preserves
        // every existing caller (Module.cs, tests) unchanged; Module.cs
        // wires this to ModuleLog.
        private readonly Action<string, Exception> _onError;

        public StatusStore(string dataDirectoryPath, Action<string, Exception> onError = null)
        {
            _filePath = Path.Combine(dataDirectoryPath, "status.txt");
            _onError = onError;
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
                _onError?.Invoke($"Failed to load status from {_filePath}", ex);
                return "";
            }
        }

        // Atomic .tmp+Replace write, matching SnapshotStore/PlanStore/
        // VendorOfferStore/ModuleLogStore. Previously the .tmp was written
        // and then File.Copy'd over the target, which truncates and rewrites
        // it in place - a crash mid-copy left a partial status.txt, exactly
        // what the temp file was there to prevent.
        public void Save(string status)
        {
            try
            {
                string dir = Path.GetDirectoryName(_filePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string tmpPath = _filePath + ".tmp";
                File.WriteAllText(tmpPath, status ?? "");

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
                _onError?.Invoke($"Failed to save status to {_filePath}", ex);
            }
        }
    }
}
