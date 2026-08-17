using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // ValueDetailTooltipBuilder is the
    // Blish-free half of the Recipe Tree's value-detail hover (see
    // TreeSectionController.RenderDecisionPills for the actual
    // BasicTooltipText wiring, which cannot be unit tested per repo
    // invariant).
    public class ValueDetailTooltipBuilderTests
    {
        private static CraftingTreeNode Node(
            CraftingDecision decision, int itemId = 1,
            long? subtreeCost = null, long? decisionValue = null)
        {
            return new CraftingTreeNode
            {
                ItemId = itemId,
                NodeId = 1,
                Decision = decision,
                SubtreeCost = subtreeCost,
                DecisionValue = decisionValue
            };
        }

        [Fact]
        public void TryBuild_NoDivergence_ReturnsFalse()
        {
            var node = Node(CraftingDecision.Craft, subtreeCost: 100, decisionValue: 100);

            bool result = ValueDetailTooltipBuilder.TryBuild(node, null, out string text);

            Assert.False(result);
            Assert.Null(text);
        }

        [Fact]
        public void TryBuild_NullNode_ReturnsFalse()
        {
            bool result = ValueDetailTooltipBuilder.TryBuild(null, null, out string text);

            Assert.False(result);
            Assert.Null(text);
        }

        [Theory]
        [InlineData(CraftingDecision.BuyFromTp)]
        [InlineData(CraftingDecision.Have)]
        [InlineData(CraftingDecision.Currency)]
        [InlineData(CraftingDecision.GuildUpgrade)]
        [InlineData(CraftingDecision.Unknown)]
        public void TryBuild_NonCraftNonVendorDecision_ReturnsFalseEvenWithDivergentValues(CraftingDecision decision)
        {
            var node = Node(decision, subtreeCost: 100, decisionValue: 350);

            bool result = ValueDetailTooltipBuilder.TryBuild(node, null, out string text);

            Assert.False(result);
            Assert.Null(text);
        }

        [Fact]
        public void TryBuild_MissingSubtreeCost_ReturnsFalse()
        {
            var node = Node(CraftingDecision.Craft, subtreeCost: null, decisionValue: 350);

            Assert.False(ValueDetailTooltipBuilder.TryBuild(node, null, out _));
        }

        [Fact]
        public void TryBuild_MissingDecisionValue_ReturnsFalse()
        {
            var node = Node(CraftingDecision.Craft, subtreeCost: 100, decisionValue: null);

            Assert.False(ValueDetailTooltipBuilder.TryBuild(node, null, out _));
        }

        [Fact]
        public void TryBuild_CraftDivergence_ProducesExpectedLines()
        {
            // 50 real gold vs 300 decision total (currency contributed 250).
            var node = Node(CraftingDecision.Craft, subtreeCost: 5000, decisionValue: 30000);

            bool result = ValueDetailTooltipBuilder.TryBuild(node, null, out string text);

            Assert.True(result);
            Assert.Contains("Crafting gold price: 0g 50s 0c", text);
            Assert.Contains("Currencies: 2g 50s 0c", text);
            Assert.Contains("This is an estimated opportunity cost for the used currencies in the recipe.", text);
            Assert.Contains("Optimization price: 3g 0s 0c", text);
        }

        [Fact]
        public void TryBuild_VendorDivergence_NoCapMap_OmitsVendorCapLine()
        {
            var node = Node(CraftingDecision.BuyFromVendor, subtreeCost: 0, decisionValue: 250);

            bool result = ValueDetailTooltipBuilder.TryBuild(node, null, out string text);

            Assert.True(result);
            Assert.DoesNotContain("Vendor cap", text);
        }

        [Fact]
        public void TryBuild_VendorDivergence_WithMatchingCap_AppendsVendorCapLine()
        {
            var node = Node(CraftingDecision.BuyFromVendor, itemId: 7, subtreeCost: 0, decisionValue: 250);
            var caps = new Dictionary<int, TimegatedItem>
            {
                { 7, new TimegatedItem { ItemId = 7, CapType = TimegatedCapType.Daily, CapValue = 5, NeededCount = 20 } }
            };

            bool result = ValueDetailTooltipBuilder.TryBuild(node, caps, out string text);

            Assert.True(result);
            Assert.Contains("Vendor cap: 5 per day", text);
        }

        [Theory]
        [InlineData(TimegatedCapType.Daily, "day")]
        [InlineData(TimegatedCapType.Weekly, "week")]
        [InlineData(TimegatedCapType.Seasonal, "season")]
        public void TryBuild_VendorDivergence_CapPeriodTextMatchesCapType(TimegatedCapType capType, string expectedPeriod)
        {
            var node = Node(CraftingDecision.BuyFromVendor, itemId: 3, subtreeCost: 0, decisionValue: 100);
            var caps = new Dictionary<int, TimegatedItem>
            {
                { 3, new TimegatedItem { ItemId = 3, CapType = capType, CapValue = 2, NeededCount = 10 } }
            };

            ValueDetailTooltipBuilder.TryBuild(node, caps, out string text);

            Assert.Contains($"Vendor cap: 2 per {expectedPeriod}", text);
        }

        [Fact]
        public void TryBuild_VendorDivergence_CapPresentButDifferentItemId_OmitsVendorCapLine()
        {
            var node = Node(CraftingDecision.BuyFromVendor, itemId: 7, subtreeCost: 0, decisionValue: 250);
            var caps = new Dictionary<int, TimegatedItem>
            {
                { 999, new TimegatedItem { ItemId = 999, CapType = TimegatedCapType.Weekly, CapValue = 3, NeededCount = 10 } }
            };

            ValueDetailTooltipBuilder.TryBuild(node, caps, out string text);

            Assert.DoesNotContain("Vendor cap", text);
        }

        [Fact]
        public void TryBuild_CraftDivergence_NeverAppendsVendorCapLine_EvenIfCapMapHasEntry()
        {
            // Vendor cap only applies to BuyFromVendor - a Craft node must
            // never show one, even if the item id happens to collide with
            // a cap entry.
            var node = Node(CraftingDecision.Craft, itemId: 7, subtreeCost: 5000, decisionValue: 30000);
            var caps = new Dictionary<int, TimegatedItem>
            {
                { 7, new TimegatedItem { ItemId = 7, CapType = TimegatedCapType.Daily, CapValue = 5, NeededCount = 20 } }
            };

            ValueDetailTooltipBuilder.TryBuild(node, caps, out string text);

            Assert.DoesNotContain("Vendor cap", text);
        }
    }
}
