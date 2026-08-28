using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class SettingsSaveBarLayoutTests
    {
        private const int BarWidth = 1232;
        private const int SaveWidth = 80;
        private const int DiscardWidth = 90;
        private const int ChipWidth = 140;

        [Fact]
        public void SaveIsPinnedToTheBarsRightEdge()
        {
            var slots = SettingsSaveBarLayout.Compute(BarWidth, ChipWidth, DiscardWidth, SaveWidth);

            Assert.Equal(
                PlanRelayoutMath.RightAlignedX(PlanRelayoutMath.PinnedRightEdge(BarWidth), SaveWidth),
                slots.SaveX);
            Assert.Equal(PlanRelayoutMath.PinnedRightEdge(BarWidth), slots.SaveX + SaveWidth);
        }

        [Fact]
        public void DiscardSitsLeftOfSaveByExactlyItsWidthAndTheButtonGap()
        {
            var slots = SettingsSaveBarLayout.Compute(BarWidth, ChipWidth, DiscardWidth, SaveWidth);

            Assert.Equal(
                slots.SaveX - SettingsSaveBarLayout.SettingsSaveBarButtonGap - DiscardWidth, slots.DiscardX);
        }

        [Fact]
        public void HiddenDiscardContributesNoWidthAndNoGap()
        {
            var withDiscard = SettingsSaveBarLayout.Compute(BarWidth, 0, DiscardWidth, SaveWidth);
            var without = SettingsSaveBarLayout.Compute(BarWidth, 0, 0, SaveWidth);

            Assert.Equal(withDiscard.SaveX, without.SaveX);
            Assert.Equal(without.SaveX, without.DiscardX);
            Assert.True(without.StatusMaxWidth > withDiscard.StatusMaxWidth);
        }

        [Fact]
        public void HiddenChipContributesNoWidthAndNoGap()
        {
            var without = SettingsSaveBarLayout.Compute(BarWidth, 0, DiscardWidth, SaveWidth);

            // A standing "0 unsaved changes" spends attention on the absence
            // of a thing; the status line simply starts at the inset.
            Assert.Equal(SettingsSaveBarLayout.SettingsSaveBarInset, without.StatusX);
            Assert.Equal(SettingsSaveBarLayout.SettingsSaveBarInset, without.ChipX);
        }

        [Fact]
        public void StatusBudgetShrinksByExactlyTheChipAndItsGap()
        {
            var without = SettingsSaveBarLayout.Compute(BarWidth, 0, DiscardWidth, SaveWidth);
            var with = SettingsSaveBarLayout.Compute(BarWidth, ChipWidth, DiscardWidth, SaveWidth);

            Assert.Equal(
                SettingsSaveBarLayout.SettingsSaveBarInset + ChipWidth + SettingsSaveBarLayout.ChipToStatusGap,
                with.StatusX);
            Assert.Equal(
                without.StatusMaxWidth - ChipWidth - SettingsSaveBarLayout.ChipToStatusGap,
                with.StatusMaxWidth);
        }

        [Fact]
        public void StatusStopsClearOfTheButtonCluster()
        {
            var slots = SettingsSaveBarLayout.Compute(BarWidth, ChipWidth, DiscardWidth, SaveWidth);

            Assert.Equal(
                slots.DiscardX - SettingsSaveBarLayout.ChipToStatusGap,
                slots.StatusX + slots.StatusMaxWidth);
        }

        [Theory]
        [InlineData(400)]
        [InlineData(200)]
        [InlineData(0)]
        [InlineData(-100)]
        public void StatusBudgetFloorsRatherThanCollapsing(int barWidth)
        {
            var slots = SettingsSaveBarLayout.Compute(barWidth, ChipWidth, DiscardWidth, SaveWidth);

            Assert.True(slots.StatusMaxWidth >= SettingsSaveBarLayout.MinStatusWidth);
        }

        [Fact]
        public void NegativeControlWidthsAreTreatedAsHidden()
        {
            var slots = SettingsSaveBarLayout.Compute(BarWidth, -10, -10, SaveWidth);

            Assert.Equal(SettingsSaveBarLayout.SettingsSaveBarInset, slots.StatusX);
            Assert.Equal(slots.SaveX, slots.DiscardX);
        }

        [Fact]
        public void BarPinsToTheSameEdgeTheScrollingContentBelowItUses()
        {
            // The bar does not scroll, but it is measured against the same
            // content width, so Save lands on the line the content's right
            // edge holds.
            int containerWidth = 1252;
            int contentWidth = containerWidth - WindowSizing.ScrollbarAllowance;

            var slots = SettingsSaveBarLayout.Compute(contentWidth, 0, 0, SaveWidth);

            Assert.Equal(
                PlanRelayoutMath.PinnedRightEdge(contentWidth), slots.SaveX + SaveWidth);
            Assert.Equal(
                SettingsFormLayout.ClusterRightEdge(contentWidth), slots.SaveX + SaveWidth);
        }
    }
}
