using System;
using System.IO;

namespace GW2CraftingHelper.Tests.Helpers
{
    /// <summary>
    /// Locates real repo files (e.g. ref/*.json seed files) from the
    /// running test assembly's output directory (M38 WP-01, tests T3 /
    /// simplify #6). Consolidated from an identical private FindRepoFile
    /// helper duplicated verbatim in AcquisitionHintServiceTests and
    /// RecipeCacheSerializerTests - the latter's own doc comment noted it
    /// was written "mirroring AcquisitionHintServiceTests' FindRepoFile
    /// pattern," i.e. the duplication was already self-acknowledged.
    /// </summary>
    public static class RepoFileLocator
    {
        /// <summary>
        /// Walks up from the running test assembly's directory looking for
        /// relativePath, so this test finds the repo's ref/ folder
        /// regardless of build configuration (Debug/Release) or platform
        /// subfolder depth. Returns null if not found within a reasonable
        /// number of levels, rather than throwing or scanning unrelated
        /// directories.
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
