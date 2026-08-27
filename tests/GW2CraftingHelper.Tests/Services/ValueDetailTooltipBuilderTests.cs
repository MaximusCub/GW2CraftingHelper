using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
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
                DecisionValue = decisionValue,
            };
        }

        [Fact]
        public void TryBuildContent_NoDivergence_ReturnsFalse()
        {
            var node = Node(CraftingDecision.Craft, subtreeCost: 100, decisionValue: 100);

            bool result = ValueDetailTooltipBuilder.TryBuildContent(node, null, out var content);

            Assert.False(result);
            Assert.Null(content);
        }

        [Fact]
        public void TryBuildContent_NullNode_ReturnsFalse()
        {
            bool result = ValueDetailTooltipBuilder.TryBuildContent(null, null, out var content);

            Assert.False(result);
            Assert.Null(content);
        }

        [Theory]
        [InlineData(nameof(CraftingDecision.BuyFromTp))]
        [InlineData(nameof(CraftingDecision.Have))]
        [InlineData(nameof(CraftingDecision.Currency))]
        [InlineData(nameof(CraftingDecision.GuildUpgrade))]
        [InlineData(nameof(CraftingDecision.Unknown))]
        public void TryBuildContent_NonCraftNonVendorDecision_ReturnsFalseEvenWithDivergentValues(string decisionName)
        {
            var decision = EnumArg.Parse<CraftingDecision>(decisionName);
            var node = Node(decision, subtreeCost: 100, decisionValue: 350);

            bool result = ValueDetailTooltipBuilder.TryBuildContent(node, null, out var content);

            Assert.False(result);
            Assert.Null(content);
        }

        [Fact]
        public void TryBuildContent_MissingSubtreeCost_ReturnsFalse()
        {
            var node = Node(CraftingDecision.Craft, subtreeCost: null, decisionValue: 350);

            Assert.False(ValueDetailTooltipBuilder.TryBuildContent(node, null, out _));
        }

        [Fact]
        public void TryBuildContent_MissingDecisionValue_ReturnsFalse()
        {
            var node = Node(CraftingDecision.Craft, subtreeCost: 100, decisionValue: null);

            Assert.False(ValueDetailTooltipBuilder.TryBuildContent(node, null, out _));
        }

        [Fact]
        public void TryBuildContent_CraftDivergence_ProducesExpectedLines()
        {
            // 50 real gold vs 300 decision total (currency contributed 250).
            var node = Node(CraftingDecision.Craft, subtreeCost: 5000, decisionValue: 30000);

            bool result = ValueDetailTooltipBuilder.TryBuildContent(node, null, out var content);

            Assert.True(result);
            // Coin spelling changed with the CoinSegmentMath.GameStyleText
            // consolidation: every composer now spells a coin amount the
            // way the icons beside it do (leading all-zero units omitted,
            // trailing units zero-padded).
            Assert.Contains("Crafting gold price: 50s 0c", content.ToPlainText());
            Assert.Contains("Currencies: 2g 50s 0c", content.ToPlainText());
            Assert.Contains("Optimization price: 3g 0s 0c", content.ToPlainText());

            // Unwrapped, and deliberately so: the rich surface this content
            // reaches wraps against a real font at a real pixel width. The
            // 76-character sentence is therefore one line here, not two.
            // (The character-budget wrap this used to assert belonged to the
            // deleted plain wrapper; TooltipTextFormatTests still covers the
            // seam itself, which LogTabContent and TooltipFacility still use.)
            Assert.Contains(
                "This is an estimated opportunity cost for the used currencies in the recipe.",
                content.ToPlainLines());

            // Each gold figure survives as a coin span - real gold, the
            // currency delta, the optimization total - which is what lets
            // the surface draw coin icons instead of spelling them out.
            Assert.Equal(new long[] { 5000, 25000, 30000 }, content.CoinValues());
        }

        [Fact]
        public void TryBuildContent_VendorDivergence_NoCapMap_OmitsVendorCapLine()
        {
            var node = Node(CraftingDecision.BuyFromVendor, subtreeCost: 0, decisionValue: 250);

            bool result = ValueDetailTooltipBuilder.TryBuildContent(node, null, out var content);

            Assert.True(result);
            Assert.DoesNotContain("Vendor cap", content.ToPlainText());
        }

        [Fact]
        public void TryBuildContent_VendorDivergence_WithMatchingCap_AppendsVendorCapLine()
        {
            var node = Node(CraftingDecision.BuyFromVendor, itemId: 7, subtreeCost: 0, decisionValue: 250);
            var caps = new Dictionary<int, TimegatedItem>
            {
                { 7, new TimegatedItem { ItemId = 7, CapType = TimegatedCapType.Daily, CapValue = 5, NeededCount = 20 } },
            };

            bool result = ValueDetailTooltipBuilder.TryBuildContent(node, caps, out var content);

            Assert.True(result);
            Assert.Contains("Vendor cap: 5 per day", content.ToPlainText());
        }

        [Theory]
        [InlineData(nameof(TimegatedCapType.Daily), "day")]
        [InlineData(nameof(TimegatedCapType.Weekly), "week")]
        [InlineData(nameof(TimegatedCapType.Seasonal), "season")]
        public void TryBuildContent_VendorDivergence_CapPeriodTextMatchesCapType(string capTypeName, string expectedPeriod)
        {
            var capType = EnumArg.Parse<TimegatedCapType>(capTypeName);
            var node = Node(CraftingDecision.BuyFromVendor, itemId: 3, subtreeCost: 0, decisionValue: 100);
            var caps = new Dictionary<int, TimegatedItem>
            {
                { 3, new TimegatedItem { ItemId = 3, CapType = capType, CapValue = 2, NeededCount = 10 } },
            };

            ValueDetailTooltipBuilder.TryBuildContent(node, caps, out var content);

            Assert.Contains($"Vendor cap: 2 per {expectedPeriod}", content.ToPlainText());
        }

        [Fact]
        public void TryBuildContent_VendorDivergence_CapPresentButDifferentItemId_OmitsVendorCapLine()
        {
            var node = Node(CraftingDecision.BuyFromVendor, itemId: 7, subtreeCost: 0, decisionValue: 250);
            var caps = new Dictionary<int, TimegatedItem>
            {
                { 999, new TimegatedItem { ItemId = 999, CapType = TimegatedCapType.Weekly, CapValue = 3, NeededCount = 10 } },
            };

            ValueDetailTooltipBuilder.TryBuildContent(node, caps, out var content);

            Assert.DoesNotContain("Vendor cap", content.ToPlainText());
        }

        [Fact]
        public void TryBuildContent_CraftDivergence_NeverAppendsVendorCapLine_EvenIfCapMapHasEntry()
        {
            // Vendor cap only applies to BuyFromVendor - a Craft node must
            // never show one, even if the item id happens to collide with
            // a cap entry.
            var node = Node(CraftingDecision.Craft, itemId: 7, subtreeCost: 5000, decisionValue: 30000);
            var caps = new Dictionary<int, TimegatedItem>
            {
                { 7, new TimegatedItem { ItemId = 7, CapType = TimegatedCapType.Daily, CapValue = 5, NeededCount = 20 } },
            };

            ValueDetailTooltipBuilder.TryBuildContent(node, caps, out var content);

            Assert.DoesNotContain("Vendor cap", content.ToPlainText());
        }
    }
}
