using System;
using System.IO;

namespace GW2CraftingHelper.Tests.Helpers
{
    /// <summary>
    /// A scratch directory for tests that exercise real file I/O
    /// (VendorOfferStore, RecipeCacheStore, etc.) without hand-rolling a
    /// create/try/finally/delete block at every call site.
    /// Mirrors the constructor-creates /
    /// Dispose-deletes idiom already used by the per-test-class fixtures
    /// in SnapshotStoreTests, StatusStoreTests, and VendorOfferStoreTests,
    /// packaged so any individual test method
    /// can opt in with a single `using` statement instead of a class-level
    /// IDisposable.
    /// </summary>
    internal sealed class TempDirectory : IDisposable
    {
        /// <summary>Absolute path to the created scratch directory.</summary>
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// Best-effort delete; never throws. Matches the existing
        /// per-class Dispose() idiom - swallowing a teardown-only delete
        /// failure means a real assertion failure inside the using block
        /// is never masked by an unrelated cleanup exception.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
