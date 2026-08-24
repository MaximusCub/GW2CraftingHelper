using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The Recipe Tree toolbar row's Overrides/Ignored chips: a count
    /// label with its own clear button, each chip hidden entirely at zero.
    /// </summary>
    public class TreeChipStripLayoutTests
    {
        private const int OverridesLabel = 90;
        private const int OverridesButton = 124;
        private const int IgnoredLabel = 70;
        private const int IgnoredButton = 110;

        [Fact]
        public void BothChips_ReadLeftToRight_WithTheWiderGapBetweenClusters()
        {
            var slots = TreeChipStripLayout.Compute(
                0, true, OverridesLabel, OverridesButton, true, IgnoredLabel, IgnoredButton);

            Assert.Equal(0, slots.OverridesLabelX);
            Assert.Equal(OverridesLabel + TreeChipStripLayout.LabelToButtonGap, slots.OverridesButtonX);
            Assert.Equal(
                slots.OverridesButtonX + OverridesButton + TreeChipStripLayout.ChipGap,
                slots.IgnoredLabelX);
            Assert.Equal(
                slots.IgnoredLabelX + IgnoredLabel + TreeChipStripLayout.LabelToButtonGap,
                slots.IgnoredButtonX);
            Assert.Equal(slots.IgnoredButtonX + IgnoredButton, slots.EndX);
        }

        [Fact]
        public void ChipGapIsWiderThanTheWithinChipGap()
        {
            // The row has to read as two clusters, not four controls -
            // the same grouping the five buttons on the right already use.
            Assert.True(TreeChipStripLayout.ChipGap > TreeChipStripLayout.LabelToButtonGap);
        }

        [Fact]
        public void HiddenOverridesChip_CostsNoWidth_SoTheIgnoredChipLeadsTheRow()
        {
            var slots = TreeChipStripLayout.Compute(
                0, false, OverridesLabel, OverridesButton, true, IgnoredLabel, IgnoredButton);

            Assert.Equal(0, slots.IgnoredLabelX);
            Assert.Equal(IgnoredLabel + TreeChipStripLayout.LabelToButtonGap, slots.IgnoredButtonX);
            Assert.Equal(slots.IgnoredButtonX + IgnoredButton, slots.EndX);
        }

        [Fact]
        public void HiddenIgnoredChip_LeavesNoTrailingGap()
        {
            var slots = TreeChipStripLayout.Compute(
                0, true, OverridesLabel, OverridesButton, false, IgnoredLabel, IgnoredButton);

            Assert.Equal(slots.OverridesButtonX + OverridesButton, slots.EndX);
        }

        [Fact]
        public void NeitherChip_OccupiesNothingAtAll()
        {
            var slots = TreeChipStripLayout.Compute(
                12, false, OverridesLabel, OverridesButton, false, IgnoredLabel, IgnoredButton);

            Assert.Equal(12, slots.EndX);
        }

        [Fact]
        public void EveryPositionShiftsWithTheStartX()
        {
            var atZero = TreeChipStripLayout.Compute(
                0, true, OverridesLabel, OverridesButton, true, IgnoredLabel, IgnoredButton);
            var shifted = TreeChipStripLayout.Compute(
                40, true, OverridesLabel, OverridesButton, true, IgnoredLabel, IgnoredButton);

            Assert.Equal(atZero.OverridesLabelX + 40, shifted.OverridesLabelX);
            Assert.Equal(atZero.OverridesButtonX + 40, shifted.OverridesButtonX);
            Assert.Equal(atZero.IgnoredLabelX + 40, shifted.IgnoredLabelX);
            Assert.Equal(atZero.IgnoredButtonX + 40, shifted.IgnoredButtonX);
            Assert.Equal(atZero.EndX + 40, shifted.EndX);
        }
    }
}
