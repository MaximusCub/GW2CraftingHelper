using System;
using System.IO;

namespace VendorOfferUpdater.Tests.Helpers
{
    /// <summary>
    /// Locates real repo files from the running test assembly's output
    /// directory. Mirrors GW2CraftingHelper.Tests' Helpers/RepoFileLocator.cs
    /// (M38 WP-01) exactly; duplicated rather than shared because this
    /// project (net8.0, VendorOfferUpdater.Tests) does not reference
    /// GW2CraftingHelper.Tests (net48, ProjectReference to the Blish
    /// module) - the two test assemblies are intentionally kept from
    /// depending on each other so this project stays Blish-free.
    /// </summary>
    public static class RepoFileLocator
    {
        /// <summary>
        /// Walks up from the running test assembly's directory looking for
        /// relativePath, so this test finds the repo root regardless of
        /// build configuration or platform subfolder depth. Returns null
        /// if not found within a reasonable number of levels, rather than
        /// throwing or scanning unrelated directories.
        /// </summary>
        public static string FindRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; dir != null && i < 12; i++)
            {
                string candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
