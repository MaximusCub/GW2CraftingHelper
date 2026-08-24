using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The Recipe Tree toolbar row's Overrides/Ignored chips: a count
    /// label with its own clear button, each chip hidden entirely at zero,
    /// and the whole cluster negotiated against the five buttons the same
    /// row anchors on its right.
    /// </summary>
    public class TreeChipStripLayoutTests
    {
        private const int OverridesLabel = 90;
        private const int OverridesButton = 124;
        private const int IgnoredLabel = 70;
        private const int IgnoredButton = 110;

        /// <summary>
        /// A limit no arrangement can reach - for the tests that are about
        /// the x's rather than about the fit.
        /// </summary>
        private const int Unbounded = 10000;

        private static TreeChipStripLayout.Placement Both(int startX = 0, int limitX = Unbounded)
        {
            return TreeChipStripLayout.Fit(
                startX, limitX,
                true, OverridesLabel, OverridesButton,
                true, IgnoredLabel, IgnoredButton);
        }

        [Fact]
        public void BothChips_ReadLeftToRight_WithTheWiderGapBetweenClusters()
        {
            var slots = Both().Slots;

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
            var slots = TreeChipStripLayout.Fit(
                0, Unbounded,
                false, OverridesLabel, OverridesButton,
                true, IgnoredLabel, IgnoredButton).Slots;

            Assert.Equal(0, slots.IgnoredLabelX);
            Assert.Equal(IgnoredLabel + TreeChipStripLayout.LabelToButtonGap, slots.IgnoredButtonX);
            Assert.Equal(slots.IgnoredButtonX + IgnoredButton, slots.EndX);
        }

        [Fact]
        public void HiddenIgnoredChip_LeavesNoTrailingGap()
        {
            var slots = TreeChipStripLayout.Fit(
                0, Unbounded,
                true, OverridesLabel, OverridesButton,
                false, IgnoredLabel, IgnoredButton).Slots;

            Assert.Equal(slots.OverridesButtonX + OverridesButton, slots.EndX);
        }

        [Fact]
        public void NeitherChip_OccupiesNothingAtAll()
        {
            var placement = TreeChipStripLayout.Fit(
                12, Unbounded,
                false, OverridesLabel, OverridesButton,
                false, IgnoredLabel, IgnoredButton);

            Assert.Equal(12, placement.Slots.EndX);
            Assert.Equal(TreeChipStripLayout.ChipStripTier.Full, placement.Tier);
        }

        [Fact]
        public void EveryPositionShiftsWithTheStartX()
        {
            var atZero = Both().Slots;
            var shifted = Both(startX: 40).Slots;

            Assert.Equal(atZero.OverridesLabelX + 40, shifted.OverridesLabelX);
            Assert.Equal(atZero.OverridesButtonX + 40, shifted.OverridesButtonX);
            Assert.Equal(atZero.IgnoredLabelX + 40, shifted.IgnoredLabelX);
            Assert.Equal(atZero.IgnoredButtonX + 40, shifted.IgnoredButtonX);
            Assert.Equal(atZero.EndX + 40, shifted.EndX);
        }

        // --- The negotiation with the right-hand button cluster ---

        [Fact]
        public void AStripThatFits_KeepsItsClearButtons()
        {
            var placement = Both(limitX: Both().Slots.EndX);

            Assert.Equal(TreeChipStripLayout.ChipStripTier.Full, placement.Tier);
            Assert.True(placement.ShowCounts);
            Assert.True(placement.ShowButtons);
        }

        [Fact]
        public void OnePixelPastTheLimit_DropsTheButtonsRatherThanTheCounts()
        {
            // What the plan's state IS is the information; the buttons that
            // change it are recoverable elsewhere (Generate Plan clears
            // both, Best Path clears the overrides).
            var placement = Both(limitX: Both().Slots.EndX - 1);

            Assert.Equal(TreeChipStripLayout.ChipStripTier.CountsOnly, placement.Tier);
            Assert.True(placement.ShowCounts);
            Assert.False(placement.ShowButtons);
        }

        [Fact]
        public void WithoutButtons_TheTwoCountsSitOneChipGapApart_CarryingNoHole()
        {
            var slots = Both(limitX: Both().Slots.EndX - 1).Slots;

            Assert.Equal(0, slots.OverridesLabelX);
            Assert.Equal(OverridesLabel + TreeChipStripLayout.ChipGap, slots.IgnoredLabelX);
            Assert.Equal(slots.IgnoredLabelX + IgnoredLabel, slots.EndX);
        }

        [Fact]
        public void WhenNotEvenTheCountsFit_TheSlotIsEmpty()
        {
            var counts = Both(limitX: Both().Slots.EndX - 1).Slots;
            var placement = Both(limitX: counts.EndX - 1);

            Assert.Equal(TreeChipStripLayout.ChipStripTier.Hidden, placement.Tier);
            Assert.False(placement.ShowCounts);
            Assert.False(placement.ShowButtons);
        }

        [Fact]
        public void TheLimitIsMeasuredFromTheStartX_NotFromZero()
        {
            // The caller passes the button cluster's left edge in the row's
            // own coordinates, so a strip that starts further right has
            // less room, not the same room shifted.
            int endAtZero = Both().Slots.EndX;

            Assert.Equal(TreeChipStripLayout.ChipStripTier.Full, Both(startX: 0, limitX: endAtZero).Tier);
            Assert.Equal(
                TreeChipStripLayout.ChipStripTier.CountsOnly,
                Both(startX: 40, limitX: endAtZero).Tier);
        }

        [Theory]
        [InlineData(1024, TreeChipStripLayout.ChipStripTier.CountsOnly)]
        [InlineData(WindowSizing.NarrowScreenFloorWidth, TreeChipStripLayout.ChipStripTier.CountsOnly)]
        [InlineData(WindowSizing.MinWindowWidth, TreeChipStripLayout.ChipStripTier.Full)]
        public void TheRoomLeftByTheButtonCluster_IsWhatDecidesTheTier(
            int clientWidth, TreeChipStripLayout.ChipStripTier expected)
        {
            // The case the desktop gate only exercised at 1378 and wider.
            // WindowSizing.EffectiveMinWindowWidth falls back to the client
            // width below the designed minimum, so a 1024x768 or 1280x720
            // windowed client really does render this row at these widths.
            Assert.Equal(
                expected,
                TierAtClientWidth(clientWidth, ShippedOverridesLabel, ShippedIgnoredLabel));
        }

        [Fact]
        public void AtTheDesignedFloor_TheStripStillHasRoomToGrow()
        {
            // Not a boundary the shipped widths sit on: both counts can
            // gain a digit without the tier moving.
            Assert.Equal(
                TreeChipStripLayout.ChipStripTier.Full,
                TierAtClientWidth(
                    WindowSizing.MinWindowWidth,
                    ShippedOverridesLabel + 11, ShippedIgnoredLabel + 11));
        }

        // The five right-anchored buttons: 414px of width (96 + 92 + 70 +
        // 76 + 80) plus 32px of gaps (4 + 20 + 4 + 4), anchored
        // RightEdgePadding clear of the row's right edge. Read from
        // CraftingPlanView.CreateTreeToolbarRow's PlaceRight calls; a width
        // changed there without changing this makes the boundary cases
        // below describe a row that no longer exists.
        private const int RightClusterWidth = 414 + 32 + 20;

        /// <summary>
        /// The two clusters have to read apart, not merely not overlap -
        /// CraftingPlanView passes the same gap it groups the buttons by.
        /// </summary>
        private const int ClusterSeparation = 20;

        // The two count labels at their widest ordinary reading -
        // "Overrides: 12" and "Ignored: 3" at Menomonia 16, the widths the
        // field report this fit answers was written against. The module
        // measures its own labels at runtime (a Label's font is Blish's),
        // so these stand in for a size no Blish-free test can resolve; the
        // arithmetic under test is the fit, not the glyphs.
        private const int ShippedOverridesLabel = 90;
        private const int ShippedIgnoredLabel = 78;

        /// <summary>
        /// The tier the strip lands in on a client of the given width,
        /// through the same chain the view uses: the window minimum
        /// actually enforced there, the tab panel inside it, and the
        /// toolbar row inside that.
        /// </summary>
        private static TreeChipStripLayout.ChipStripTier TierAtClientWidth(
            int clientWidth, int overridesLabelWidth, int ignoredLabelWidth)
        {
            int windowWidth = WindowSizing.EffectiveMinWindowWidth(clientWidth);

            // The row spans the tab panel plus the RightEdgePadding term
            // WindowToTabPanelChrome already took off: the toolbar row is
            // parented to the strip, which is the whole content region, and
            // it is the button walk that steps back in from its edge.
            int rowWidth = WindowSizing.TabPanelWidthFor(windowWidth) + 20;

            return TreeChipStripLayout.Fit(
                0, rowWidth - RightClusterWidth - ClusterSeparation,
                true, overridesLabelWidth, OverridesButton,
                true, ignoredLabelWidth, IgnoredButton).Tier;
        }
    }
}
