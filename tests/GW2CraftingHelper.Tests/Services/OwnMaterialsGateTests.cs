using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class OwnMaterialsGateTests
    {
        [Fact]
        public void NoAccountData_TogglesOffAndDisables_EvenWhenTheUserWantsItOn()
        {
            // The defect this gate exists for: the view's field defaults to
            // true, so a key-less install solved as if the account owned
            // nothing while the box sat ticked.
            var state = OwnMaterialsGate.Resolve(userIntent: true, accountDataAvailable: false);

            Assert.False(state.Checked);
            Assert.False(state.Enabled);
        }

        [Fact]
        public void NoAccountData_SaysWhyTheToggleIsUnavailable()
        {
            var state = OwnMaterialsGate.Resolve(userIntent: true, accountDataAvailable: false);

            Assert.Equal(OwnMaterialsGate.NoAccountDataTooltip, state.Tooltip);
        }

        [Fact]
        public void AccountDataPresent_ToggleIsLiveAndCarriesNoExplanation()
        {
            var state = OwnMaterialsGate.Resolve(userIntent: true, accountDataAvailable: true);

            Assert.True(state.Enabled);
            Assert.True(state.Checked);
            // Cleared, not left standing from the gated state - the caller
            // applies Tooltip unconditionally.
            Assert.Null(state.Tooltip);
        }

        [Fact]
        public void AccountDataPresent_UserIntentIsNotOverridden()
        {
            var state = OwnMaterialsGate.Resolve(userIntent: false, accountDataAvailable: true);

            Assert.True(state.Enabled);
            Assert.False(state.Checked);
        }

        [Fact]
        public void IntentSurvivesTheGate_SoASnapshotArrivingRestoresTheSetting()
        {
            // (c) of the truth gate: the intent is the caller's, passed
            // through untouched, so the same intent that was gated off
            // comes back on its own when a snapshot lands - no restart, no
            // second stored copy of the user's choice to go stale.
            const bool intent = true;

            Assert.False(OwnMaterialsGate.Resolve(intent, accountDataAvailable: false).Checked);
            Assert.True(OwnMaterialsGate.Resolve(intent, accountDataAvailable: true).Checked);
        }

        [Fact]
        public void SolveInputIsTheDisplayedValue_ForEveryCombination()
        {
            // The whole point of one Checked field rather than a separate
            // "what to show" and "what to solve with": there is no
            // combination in which the box and the solver disagree.
            foreach (bool intent in new[] { true, false })
            {
                foreach (bool available in new[] { true, false })
                {
                    var state = OwnMaterialsGate.Resolve(intent, available);
                    Assert.Equal(intent && available, state.Checked);
                }
            }
        }
    }
}
