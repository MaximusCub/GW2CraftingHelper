using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class LogViewFloorTests
    {
        [Fact]
        public void BeforeFloor_Hidden()
        {
            Assert.False(LogViewFloor.IsVisible(absoluteIndex: 4, clearedBeforeVersion: 5));
        }

        [Fact]
        public void AtFloor_Visible()
        {
            // Boundary inclusive: the entry AT the snapshot version itself
            // was not yet counted when "Clear View" read ModuleLog.Version
            // (a Version of N means N entries have been appended, i.e. the
            // highest existing absolute index is N-1), so it must stay
            // visible, not be treated as "before" the floor.
            Assert.True(LogViewFloor.IsVisible(absoluteIndex: 5, clearedBeforeVersion: 5));
        }

        [Fact]
        public void AfterFloor_Visible()
        {
            Assert.True(LogViewFloor.IsVisible(absoluteIndex: 6, clearedBeforeVersion: 5));
        }

        [Fact]
        public void ZeroFloor_UnclearedState_EverythingVisible()
        {
            // Default/never-cleared state (Module._logViewClearedBeforeVersion
            // starts at 0) - every real ring entry (absoluteIndex >= 0) stays
            // visible.
            Assert.True(LogViewFloor.IsVisible(absoluteIndex: 0, clearedBeforeVersion: 0));
            Assert.True(LogViewFloor.IsVisible(absoluteIndex: 1000, clearedBeforeVersion: 0));
        }

        [Fact]
        public void LargeIndices_NoOverflowMisbehavior()
        {
            long large = long.MaxValue - 1;
            Assert.True(LogViewFloor.IsVisible(large, large));
            Assert.False(LogViewFloor.IsVisible(large - 1, large));
        }
    }
}
