using System;
using System.Linq;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The icon-tier vocabulary is what stops a call site inventing a pixel
    /// size, so the vocabulary itself has to be total: every member answers,
    /// and the layout math that reserves room for an icon has to agree with
    /// the table the view draws from. The two used to be separate numbers.
    /// <para>
    /// Swept in a loop rather than as a [Theory]: ItemIconTier is internal
    /// (the module's default), and an xunit theory would have to put it on a
    /// public signature.
    /// </para>
    /// </summary>
    public class ItemIconTiersTests
    {
        [Fact]
        public void EveryTierHasAPositiveArtSizeAndBorder()
        {
            // A member added without a size would otherwise reach the throw
            // only when someone drew that icon at runtime.
            foreach (var tier in Enum.GetValues(typeof(ItemIconTier)).Cast<ItemIconTier>())
            {
                Assert.True(ItemIconTiers.ArtSize(tier) > 0, tier.ToString());
                Assert.True(ItemIconTiers.BorderThickness(tier) > 0, tier.ToString());
            }
        }

        [Fact]
        public void FrameSizeIsArtPlusBothBorders()
        {
            foreach (var tier in Enum.GetValues(typeof(ItemIconTier)).Cast<ItemIconTier>())
            {
                Assert.Equal(
                    ItemIconTiers.ArtSize(tier) + (2 * ItemIconTiers.BorderThickness(tier)),
                    ItemIconTiers.FrameSize(tier));
            }
        }

        [Fact]
        public void AnUnnamedTierThrowsRatherThanGuessingASize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ItemIconTiers.ArtSize((ItemIconTier)999));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ItemIconTiers.BorderThickness((ItemIconTier)999));
        }

        [Fact]
        public void TheTwoGovernedTiersAreTheMeasuredInGameSizes()
        {
            Assert.Equal(ItemIconTiers.BagSlotIconSize, ItemIconTiers.ArtSize(ItemIconTier.BagSlot));
            Assert.Equal(ItemIconTiers.BagSidebarIconSize, ItemIconTiers.ArtSize(ItemIconTier.BagSidebar));
        }

        [Fact]
        public void PlanTabRowMathAgreesWithTheBagSidebarTier()
        {
            // PlanContentHeightMath derives every plan-tab icon row height
            // from these two numbers. If the tier table and the row math
            // ever disagree, the icon overflows or floats inside its row.
            Assert.Equal(
                PlanContentHeightMath.RowIconBorder,
                ItemIconTiers.BorderThickness(ItemIconTier.BagSidebar));
            Assert.Equal(
                PlanContentHeightMath.RowIconFrameSize,
                ItemIconTiers.FrameSize(ItemIconTier.BagSidebar));
        }

        [Fact]
        public void TreeRowShapeAgreesWithTheBagSidebarTier()
        {
            // The tree's name column is offset by the icon FRAME, so a
            // divergence here misaligns every tree row against every table
            // row beneath it.
            Assert.Equal(
                TreeRowShapePlanner.IconFrameSize,
                ItemIconTiers.FrameSize(ItemIconTier.BagSidebar));
        }

        [Fact]
        public void SnapshotAndRankerRowsAgreeWithTheBagSlotTier()
        {
            Assert.Equal(RankerRowLayout.IconSize, ItemIconTiers.ArtSize(ItemIconTier.BagSlot));

            // The Snapshot grid's text column starts 2px in, past the art,
            // then 2px of frame and 6px of gap - the icon is the term that
            // has to track the tier.
            Assert.Equal(
                SnapshotItemGridLayout.CellTextX,
                2 + ItemIconTiers.ArtSize(ItemIconTier.BagSlot) + 2 + 6);
        }

        [Fact]
        public void TheSearchSuggestionTiersFrameFillsItsRowBox()
        {
            // SuggestionPanel insets the art inside the 24px box the row
            // already reserved, rather than growing the box. 22 + 2 = 24.
            Assert.Equal(24, ItemIconTiers.FrameSize(ItemIconTier.SearchSuggestion));
        }
    }
}
