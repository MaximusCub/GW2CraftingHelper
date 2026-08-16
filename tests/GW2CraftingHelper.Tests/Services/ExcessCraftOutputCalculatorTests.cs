using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// design-plan-notes.md (Notes section, excess/reclaim) - direct unit
    /// tests on ExcessCraftOutputCalculator's pure tree-walk aggregation,
    /// using plain CraftingTreeNode fixtures (no Blish reference, no
    /// solver/pipeline round-trip needed).
    /// </summary>
    public class ExcessCraftOutputCalculatorTests
    {
        private static CraftingTreeNode CraftNode(
            int itemId, int quantity, int craftsNeeded, int outputCount, params CraftingTreeNode[] children)
        {
            return new CraftingTreeNode
            {
                ItemId = itemId,
                Quantity = quantity,
                Decision = CraftingDecision.Craft,
                CraftsNeeded = craftsNeeded,
                RecipeOutputCount = outputCount,
                Children = children
            };
        }

        private static CraftingTreeNode BuyNode(int itemId, int quantity, params CraftingTreeNode[] children)
        {
            return new CraftingTreeNode
            {
                ItemId = itemId,
                Quantity = quantity,
                Decision = CraftingDecision.BuyFromTp,
                Children = children
            };
        }

        [Fact]
        public void CraftNodeWithOverproduction_AggregatesPositiveExcess()
        {
            // Crafts 4 times at 3 output each = 12 produced, but only 10 needed -> 2 excess.
            var root = CraftNode(itemId: 1, quantity: 10, craftsNeeded: 4, outputCount: 3);
            var prices = new Dictionary<int, ItemPrice> { { 1, new ItemPrice { SellInstant = 100 } } };
            var metadata = new Dictionary<int, ItemMetadata>();
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(result, prices, metadata);

            Assert.Single(result.ExcessCraftOutputs);
            var excess = result.ExcessCraftOutputs[0];
            Assert.Equal(1, excess.ItemId);
            Assert.Equal(2, excess.ExcessQuantity);
            Assert.True(excess.ReclaimValue.HasValue && excess.ReclaimValue.Value > 0);
            Assert.False(excess.IsAccountBound);
        }

        [Fact]
        public void CraftNodeExactlyMeetingDemand_NoExcessEmitted()
        {
            var root = CraftNode(itemId: 1, quantity: 12, craftsNeeded: 4, outputCount: 3);
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Empty(result.ExcessCraftOutputs);
        }

        [Fact]
        public void NonCraftDecisionNode_NeverContributesEvenWithBatchFieldsSet()
        {
            // Defensive: CraftsNeeded/RecipeOutputCount should never be set
            // on a non-Craft node in production (CraftingTreeBuilder only
            // sets them inside its Decision == Craft branch), but the
            // calculator must gate on Decision == Craft rather than
            // trusting the fields' presence alone.
            var root = new CraftingTreeNode
            {
                ItemId = 1,
                Quantity = 10,
                Decision = CraftingDecision.BuyFromTp,
                CraftsNeeded = 4,
                RecipeOutputCount = 3
            };
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Empty(result.ExcessCraftOutputs);
        }

        [Fact]
        public void SameItemCraftedInTwoBranches_SumsAcrossOccurrences()
        {
            var leftChild = CraftNode(itemId: 2, quantity: 5, craftsNeeded: 3, outputCount: 2); // 6 - 5 = 1 excess
            var rightChild = CraftNode(itemId: 2, quantity: 5, craftsNeeded: 3, outputCount: 2); // 1 excess
            var root = BuyNode(itemId: 1, quantity: 1, leftChild, rightChild);
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Single(result.ExcessCraftOutputs);
            Assert.Equal(2, result.ExcessCraftOutputs[0].ItemId);
            Assert.Equal(2, result.ExcessCraftOutputs[0].ExcessQuantity);
        }

        [Fact]
        public void MultiItemRoots_WalksEveryRoot()
        {
            var rootA = CraftNode(itemId: 1, quantity: 10, craftsNeeded: 4, outputCount: 3); // 2 excess
            var rootB = CraftNode(itemId: 2, quantity: 1, craftsNeeded: 1, outputCount: 3); // 2 excess
            var result = new CraftingPlanResult
            {
                MultiItemRoots = new List<CraftingTreeNode> { rootA, rootB }
            };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Equal(2, result.ExcessCraftOutputs.Count);
        }

        [Fact]
        public void UnpricedItem_ReclaimValueNull()
        {
            var root = CraftNode(itemId: 1, quantity: 10, craftsNeeded: 4, outputCount: 3);
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Single(result.ExcessCraftOutputs);
            Assert.Null(result.ExcessCraftOutputs[0].ReclaimValue);
        }

        [Fact]
        public void AccountBoundItem_ReclaimValueNullEvenWhenPriced()
        {
            var root = CraftNode(itemId: 1, quantity: 10, craftsNeeded: 4, outputCount: 3);
            var prices = new Dictionary<int, ItemPrice> { { 1, new ItemPrice { SellInstant = 100 } } };
            var metadata = new Dictionary<int, ItemMetadata>
            {
                { 1, new ItemMetadata { ItemId = 1, IsAccountBound = true } }
            };
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(result, prices, metadata);

            Assert.Single(result.ExcessCraftOutputs);
            var excess = result.ExcessCraftOutputs[0];
            Assert.True(excess.IsAccountBound);
            Assert.Null(excess.ReclaimValue);
        }

        [Fact]
        public void NullResult_NoOp()
        {
            // Must not throw.
            ExcessCraftOutputCalculator.Apply(null, null, null);
        }

        [Fact]
        public void NoTreeAtAll_EmptyOutputsListNotNull()
        {
            var result = new CraftingPlanResult();

            ExcessCraftOutputCalculator.Apply(result, null, null);

            Assert.NotNull(result.ExcessCraftOutputs);
            Assert.Empty(result.ExcessCraftOutputs);
        }
    }
}
