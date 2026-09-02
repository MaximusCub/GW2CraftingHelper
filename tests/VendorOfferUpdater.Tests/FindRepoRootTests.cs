using System.IO;
using VendorOfferUpdater;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    // FindRepoRoot resolves where ref/vendor_offers.json is written. A
    // linked git worktree marks its root with a ".git" FILE rather than a
    // directory, so the probe must accept both; the directory-only version
    // walked past every worktree root and rewrote ref/ in the next repo up.
    public class FindRepoRootTests
    {
        private sealed class TempTree : System.IDisposable
        {
            internal readonly string Root;

            internal TempTree()
            {
                Root = Path.Combine(Path.GetTempPath(), "vou-reporoot-" + Path.GetRandomFileName());
                Directory.CreateDirectory(Root);
            }

            internal string MakeDescendant(params string[] segments)
            {
                string path = Path.Combine(Root, Path.Combine(segments));
                Directory.CreateDirectory(path);
                return path;
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(Root, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        [Fact]
        public void GitAsFile_WorktreeRootIsFound()
        {
            using (var tree = new TempTree())
            {
                string worktree = tree.MakeDescendant("wt");
                File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: /somewhere/.git/worktrees/wt\n");
                string start = Path.Combine(worktree, "bin", "Debug", "net8.0");
                Directory.CreateDirectory(start);

                Assert.Equal(worktree, Program.FindRepoRoot(start));
            }
        }

        [Fact]
        public void GitAsDirectory_CloneRootIsFound()
        {
            using (var tree = new TempTree())
            {
                string clone = tree.MakeDescendant("clone");
                Directory.CreateDirectory(Path.Combine(clone, ".git"));
                string start = Path.Combine(clone, "bin", "Debug", "net8.0");
                Directory.CreateDirectory(start);

                Assert.Equal(clone, Program.FindRepoRoot(start));
            }
        }

        [Fact]
        public void GitAsFileBelowAnEnclosingClone_StopsAtTheWorktree()
        {
            // The exact shape of the bug: a worktree nested inside a checkout
            // that has a real .git directory. The directory-only probe skipped
            // the worktree and answered with the enclosing clone.
            using (var tree = new TempTree())
            {
                Directory.CreateDirectory(Path.Combine(tree.Root, ".git"));
                string worktree = tree.MakeDescendant("worktrees", "wt");
                File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: /somewhere\n");
                string start = Path.Combine(worktree, "bin");
                Directory.CreateDirectory(start);

                Assert.Equal(worktree, Program.FindRepoRoot(start));
            }
        }

        [Fact]
        public void NoMarkerAnywhere_FallsBackToTheWorkingDirectory()
        {
            using (var tree = new TempTree())
            {
                string start = tree.MakeDescendant("bare", "bin");

                Assert.Equal(Directory.GetCurrentDirectory(), Program.FindRepoRoot(start));
            }
        }
    }
}
