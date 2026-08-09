using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class PhaseOrdinalGuardTests
    {
        [Fact]
        public void StrictlyGreaterOrdinal_Applies()
        {
            // A genuinely later phase (BuildingTree=0 -> FetchingPrices=1)
            // always advances the strip.
            Assert.True(PhaseOrdinalGuard.ShouldApply(eventPhaseOrdinal: 1, currentPhaseOrdinal: 0));
        }

        [Fact]
        public void FirstEventEver_OrdinalZero_Applies()
        {
            // CraftingPlanView resets _currentPhaseOrdinal to -1 at the
            // start of every generation, so the very first phase event
            // (BuildingTree, ordinal 0) must still apply.
            Assert.True(PhaseOrdinalGuard.ShouldApply(eventPhaseOrdinal: 0, currentPhaseOrdinal: -1));
        }

        [Fact]
        public void SameOrdinal_Rejected()
        {
            // A duplicate/replayed event for the phase already applied must
            // not re-apply (also guards against a literal re-post of the
            // same event).
            Assert.False(PhaseOrdinalGuard.ShouldApply(eventPhaseOrdinal: 2, currentPhaseOrdinal: 2));
        }

        [Fact]
        public void EarlierOrdinal_Rejected()
        {
            // The exact race this guard exists for: an EARLIER phase event
            // (e.g. FetchingPrices=1) draining on the main thread AFTER a
            // LATER one (BuildingDisplay=4) already applied - out-of-order
            // ThreadPool posts from Progress<T> with no
            // SynchronizationContext. StatusUpdateGuard alone cannot catch
            // this since both events share the same generation.
            Assert.False(PhaseOrdinalGuard.ShouldApply(eventPhaseOrdinal: 1, currentPhaseOrdinal: 4));
        }
    }
}
