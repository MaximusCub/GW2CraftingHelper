using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
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
        private const int OverridesButton = TreeToolbarRowLayout.ClearOverridesButtonWidth;
        private const int IgnoredLabel = 70;
        private const int IgnoredButton = TreeToolbarRowLayout.ClearIgnoredButtonWidth;

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

        // Every width the row is actually rendered at:
        // EffectiveMinWindowWidth falls back to the client width below the
        // designed minimum, so a 1024x768 windowed client really does render
        // this row, and 930 is the floor that fallback stops at.
        public static TheoryData<int> WidthsTheRowRendersAt => new TheoryData<int>
        {
            WindowSizing.NarrowScreenFloorWidth,
            1024,
            WindowSizing.MinWindowWidth,
        };

        [Theory]
        [MemberData(nameof(WidthsTheRowRendersAt))]
        public void AtEveryWidth_TheCountsFitAndStopShortOfTheButtons(int clientWidth)
        {
            // The two claims that hold whatever the count labels actually
            // measure, which is why they are asserted at every width and the
            // TIER is not: the strip never reaches the buttons (two live
            // controls on the same pixels is a click landing on whichever
            // Blish hit-tests last), and what the plan's state IS never
            // degrades away. The narrowest row the module renders (930 -> an
            // 824px row) leaves 338px against 188px of counts, so the second
            // claim clears its boundary by 150px at the worst width and the
            // first would break only if the button cluster grew past the
            // whole row.
            var (placement, limitX) = FitAtClientWidth(
                clientWidth, ShippedOverridesLabel, ShippedIgnoredLabel);

            Assert.True(placement.ShowCounts);
            Assert.True(
                placement.Slots.EndX <= limitX,
                "strip ends at " + placement.Slots.EndX + ", cluster starts at " + limitX);
        }

        // The TIER is asserted only where the shipped widths sit far from
        // the boundary, because the two count labels are measured from a
        // font no Blish-free test can resolve (see ShippedOverridesLabel).
        // At 1024 the full strip wants 438px against a 432px limit - a 6px
        // margin, which is a real degradation on screen and an unfalsifiable
        // assertion in here, so that width is covered by the two properties
        // above instead.
        [Fact]
        public void BelowTheDesignedFloor_TheClearButtonsAreDroppedFirst()
        {
            // The 930 narrow-screen floor: an 824px row, 338px of it left
            // of the buttons, against 438px of full strip. 100px past the
            // tier boundary in one direction and 150px in the other.
            var (placement, _) = FitAtClientWidth(
                WindowSizing.NarrowScreenFloorWidth, ShippedOverridesLabel, ShippedIgnoredLabel);

            Assert.Equal(TreeChipStripLayout.ChipStripTier.CountsOnly, placement.Tier);
            Assert.False(placement.ShowButtons);
        }

        [Fact]
        public void AtTheDesignedFloor_BothChipsKeepTheirClearButtons()
        {
            // 1378 -> a 1272px row leaving 786px, against 438px of full
            // strip: 348px of slack, so this is a statement about the
            // designed minimum and not about a glyph.
            var (placement, _) = FitAtClientWidth(
                WindowSizing.MinWindowWidth, ShippedOverridesLabel, ShippedIgnoredLabel);

            Assert.Equal(TreeChipStripLayout.ChipStripTier.Full, placement.Tier);
            Assert.True(placement.ShowButtons);
        }

        [Fact]
        public void AtTheDesignedFloor_TheStripStillHasRoomToGrow()
        {
            // Not a boundary the shipped widths sit on: both counts can
            // gain a digit without the tier moving.
            var (placement, _) = FitAtClientWidth(
                WindowSizing.MinWindowWidth,
                ShippedOverridesLabel + 11, ShippedIgnoredLabel + 11);

            Assert.Equal(TreeChipStripLayout.ChipStripTier.Full, placement.Tier);
        }

        // The two count labels at their widest ordinary reading -
        // "Overrides: 12" and "Ignored: 3" at Menomonia 16, the widths the
        // field report this fit answers was written against. The module
        // measures its own labels at runtime (a Label's font is Blish's),
        // so these stand in for a size no Blish-free test can resolve;
        // nothing asserted against them is closer than 100px to a boundary.
        private const int ShippedOverridesLabel = 90;
        private const int ShippedIgnoredLabel = 78;

        /// <summary>
        /// The strip's placement on a client of the given width, and the x
        /// the button cluster starts at, through the same chain the view
        /// uses: the window minimum actually enforced there, the tab panel
        /// inside it, the toolbar row inside that, and
        /// TreeToolbarRowLayout.ChipLimitX - the one owner of the cluster's
        /// width, which CraftingPlanView.PlaceTreeToolbarRow reads too.
        /// </summary>
        private static (TreeChipStripLayout.Placement Placement, int LimitX) FitAtClientWidth(
            int clientWidth, int overridesLabelWidth, int ignoredLabelWidth)
        {
            int windowWidth = WindowSizing.EffectiveMinWindowWidth(clientWidth);

            // The row spans the tab panel plus the RightEdgePadding term
            // WindowToTabPanelChrome already took off: the toolbar row is
            // parented to the strip, which is the whole content region, and
            // it is the button walk that steps back in from its edge.
            int rowWidth = WindowSizing.TabPanelWidthFor(windowWidth) + WindowSizing.RightEdgePadding;
            int limitX = TreeToolbarRowLayout.ChipLimitX(rowWidth);

            var placement = TreeChipStripLayout.Fit(
                0, limitX,
                true, overridesLabelWidth, OverridesButton,
                true, ignoredLabelWidth, IgnoredButton);

            return (placement, limitX);
        }
    }
}
