using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class StatusUpdateGuardTests
    {
        [Fact]
        public void CurrentGeneration_NotClosed_Applies()
        {
            Assert.True(StatusUpdateGuard.ShouldApply(tickGeneration: 5, currentGeneration: 5, currentGenerationStatusClosed: false));
        }

        [Fact]
        public void StaleGeneration_Rejected_RegardlessOfClosedFlag()
        {
            // The pre-existing cross-generation guard: a tick from a
            // superseded generation must never apply, whether or not the
            // (irrelevant, stale) closed flag happens to be set.
            Assert.False(StatusUpdateGuard.ShouldApply(tickGeneration: 4, currentGeneration: 5, currentGenerationStatusClosed: false));
            Assert.False(StatusUpdateGuard.ShouldApply(tickGeneration: 4, currentGeneration: 5, currentGenerationStatusClosed: true));
        }

        [Fact]
        public void CurrentGeneration_Closed_Rejected()
        {
            // The actual race fix - a trailing tick belonging to
            // the CURRENT generation must still be dropped once that same
            // generation has already written its own completion status.
            Assert.False(StatusUpdateGuard.ShouldApply(tickGeneration: 5, currentGeneration: 5, currentGenerationStatusClosed: true));
        }
    }
}
