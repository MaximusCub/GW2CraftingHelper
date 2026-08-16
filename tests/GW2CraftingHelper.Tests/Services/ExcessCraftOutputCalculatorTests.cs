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
    ///
    /// Review fix note: every fixture below that wants to assert a real
    /// excess figure nests the excess-producing Craft node one level
    /// UNDER a non-craft wrapper root (see WrapAsRoot). This is required
    /// by the finding-6 fix - the calculator now excludes the display-
    /// tree's own root item id(s) from the output list entirely (that
    /// surplus is already advertised by SellSideEconomics' Sell Value/
    /// Profit tiles) - so a fixture that made its excess-producing Craft
    /// node the literal tree root would have its own excess silently
    /// dropped, proving nothing about the aggregation math the test
    /// actually intends to cover. Root-exclusion itself is covered by its
    /// own dedicated tests near the bottom of this file.
    /// </summary>
    public class ExcessCraftOutputCalculatorTests
    {
        private static CraftingTreeNode CraftNode(
            int itemId, int quantity, int craftsNeeded, int outputCount,
            double? expectedOutputCount = null, params CraftingTreeNode[] children)
        {
            return new CraftingTreeNode
            {
                ItemId = itemId,
                Quantity = quantity,
                Decision = CraftingDecision.Craft,
                CraftsNeeded = craftsNeeded,
                RecipeOutputCount = outputCount,
                RecipeExpectedOutputCount = expectedOutputCount,
                Children = children
            };
        }

        private static CraftingTreeNode BuyNode(
            int itemId, int quantity, bool isReferenceBranch = false, params CraftingTreeNode[] children)
        {
            return new CraftingTreeNode
            {
                ItemId = itemId,
                Quantity = quantity,
                Decision = CraftingDecision.BuyFromTp,
                IsReferenceBranch = isReferenceBranch,
                Children = children
            };
        }

        // Non-craft wrapper so the node under test is never itself the
        // tree root - see this class's own doc comment.
        private static CraftingTreeNode WrapAsRoot(int wrapperItemId, params CraftingTreeNode[] children)
        {
            return BuyNode(wrapperItemId, quantity: 1, isReferenceBranch: false, children: children);
        }

        [Fact]
        public void CraftNodeWithOverproduction_AggregatesPositiveExcess()
        {
            // Crafts 4 times at 3 output each = 12 produced, but only 10 needed -> 2 excess.
            var child = CraftNode(itemId: 2, quantity: 10, craftsNeeded: 4, outputCount: 3);
            var root = WrapAsRoot(wrapperItemId: 1, children: new[] { child });
            var prices = new Dictionary<int, ItemPrice> { { 2, new ItemPrice { SellInstant = 100 } } };
            var metadata = new Dictionary<int, ItemMetadata>();
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(result, prices, metadata);

            Assert.Single(result.ExcessCraftOutputs);
            var excess = result.ExcessCraftOutputs[0];
            Assert.Equal(2, excess.ItemId);
            Assert.Equal(2, excess.ExcessQuantity);
            Assert.True(excess.ReclaimValue.HasValue && excess.ReclaimValue.Value > 0);
            Assert.False(excess.IsAccountBound);
        }

        [Fact]
        public void CraftNodeExactlyMeetingDemand_NoExcessEmitted()
        {
            var child = CraftNode(itemId: 2, quantity: 12, craftsNeeded: 4, outputCount: 3);
            var root = WrapAsRoot(wrapperItemId: 1, children: new[] { child });
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
            var root = WrapAsRoot(wrapperItemId: 1, children: new[] { leftChild, rightChild });
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
            var childA = CraftNode(itemId: 3, quantity: 10, craftsNeeded: 4, outputCount: 3); // 2 excess
            var childB = CraftNode(itemId: 4, quantity: 1, craftsNeeded: 1, outputCount: 3); // 2 excess
            var rootA = WrapAsRoot(wrapperItemId: 1, children: new[] { childA });
            var rootB = WrapAsRoot(wrapperItemId: 2, children: new[] { childB });
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
            var child = CraftNode(itemId: 2, quantity: 10, craftsNeeded: 4, outputCount: 3);
            var root = WrapAsRoot(wrapperItemId: 1, children: new[] { child });
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Single(result.ExcessCraftOutputs);
            Assert.Null(result.ExcessCraftOutputs[0].ReclaimValue);
        }

        [Fact]
        public void AccountBoundItem_ReclaimValueNullEvenWhenPriced()
        {
            var child = CraftNode(itemId: 2, quantity: 10, craftsNeeded: 4, outputCount: 3);
            var root = WrapAsRoot(wrapperItemId: 1, children: new[] { child });
            var prices = new Dictionary<int, ItemPrice> { { 2, new ItemPrice { SellInstant = 100 } } };
            var metadata = new Dictionary<int, ItemMetadata>
            {
                { 2, new ItemMetadata { ItemId = 2, IsAccountBound = true } }
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

        // --- Review fix (finding 1, MEASURED): fractional-EV (Mystic
        // Clover-style) Mystic Forge yield must not fabricate an excess
        // claim - CraftsNeeded is derived from ExpectedOutputCount, not
        // RecipeOutputCount, so "produced" must be recovered on that same
        // basis. Real repro numbers: item 19675 (Mystic Clover),
        // outputItemCount=1, expectedOutputCount=0.31, quantity=77 ->
        // craftsNeeded=249 (ceil(77/0.31)). 249 * 0.31 = 77.19, which
        // floors to 0 excess against a demand of 77 - the plan produces
        // exactly 77 clovers in expectation, not the 172 the pre-fix
        // RecipeOutputCount-basis math fabricated. ---

        [Fact]
        public void FractionalYieldRecipe_UsesExpectedOutputBasis_NoFakeExcess()
        {
            var child = CraftNode(
                itemId: 19675, quantity: 77, craftsNeeded: 249, outputCount: 1, expectedOutputCount: 0.31);
            var root = WrapAsRoot(wrapperItemId: 1, children: new[] { child });
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Empty(result.ExcessCraftOutputs);
        }

        [Fact]
        public void FractionalYieldRecipe_GenuineWholeUnitExcessStillCounted()
        {
            // A fractional ExpectedOutputCount ABOVE 1 (e.g. a recipe whose
            // EV yield is 2.5 against a nominal integer OutputCount of 3)
            // can still clear a whole extra unit of real demand once
            // rounded up to a whole number of attempts - that case must
            // still report real excess, on the EV basis: craftsNeeded =
            // ceil(3 / 2.5) = 2, producedEv = 2 * 2.5 = 5.0 -> 2 excess
            // against a demand of 3.
            var child = CraftNode(
                itemId: 19675, quantity: 3, craftsNeeded: 2, outputCount: 3, expectedOutputCount: 2.5);
            var root = WrapAsRoot(wrapperItemId: 1, children: new[] { child });
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Single(result.ExcessCraftOutputs);
            Assert.Equal(19675, result.ExcessCraftOutputs[0].ItemId);
            Assert.Equal(2, result.ExcessCraftOutputs[0].ExcessQuantity);
        }

        // --- Review fix (finding 2, MEASURED): a reference-branch subtree
        // (gw2e's "what it would cost to craft instead", built under a
        // Buy node that also has a recipe) carries real solver decisions
        // for hypothetical children - none of it was actually crafted, so
        // none of it may contribute excess. ---

        [Fact]
        public void ReferenceBranchChild_NeverContributes()
        {
            // 2 crafts * 2 output = 4 produced against 1 needed -> would be
            // 3 excess if this were real, but it is purely hypothetical.
            var refChild = CraftNode(itemId: 2001, quantity: 1, craftsNeeded: 2, outputCount: 2);
            var root = BuyNode(itemId: 2000, quantity: 1, isReferenceBranch: true, children: new[] { refChild });
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Empty(result.ExcessCraftOutputs);
        }

        [Fact]
        public void ReferenceBranchDoesNotSuppressRealCraftExcessElsewhere()
        {
            var refChild = CraftNode(itemId: 2001, quantity: 1, craftsNeeded: 2, outputCount: 2); // hypothetical only
            var referenceBranchRoot = BuyNode(
                itemId: 2000, quantity: 1, isReferenceBranch: true, children: new[] { refChild });
            var realCraft = CraftNode(itemId: 5, quantity: 10, craftsNeeded: 4, outputCount: 3); // real, 2 excess
            var root = WrapAsRoot(wrapperItemId: 1, children: new[] { referenceBranchRoot, realCraft });
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Single(result.ExcessCraftOutputs);
            Assert.Equal(5, result.ExcessCraftOutputs[0].ItemId);
            Assert.Equal(2, result.ExcessCraftOutputs[0].ExcessQuantity);
        }

        // --- Review fix (finding 6, MEASURED): the requested root item's
        // own over-production is already folded into SellSideEconomics'
        // Sell Value/Profit tiles (ComputePerItemEconomics raising
        // sellableQuantity) - advertising it again here double-counts the
        // same coins under a different label, so root item ids are
        // excluded from the Notes list entirely. ---

        [Fact]
        public void SingleItemPlan_RootsOwnExcess_ExcludedFromOutputs()
        {
            var root = CraftNode(itemId: 1, quantity: 10, craftsNeeded: 4, outputCount: 3); // would be 2 excess
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Empty(result.ExcessCraftOutputs);
        }

        [Fact]
        public void SingleItemPlan_RootExcessExcluded_ButNestedDifferentItemExcessKept()
        {
            var nestedChild = CraftNode(itemId: 2, quantity: 5, craftsNeeded: 3, outputCount: 2); // 1 excess
            var root = CraftNode(
                itemId: 1, quantity: 10, craftsNeeded: 4, outputCount: 3, children: new[] { nestedChild }); // root's own would be 2 excess
            var result = new CraftingPlanResult { CraftingTree = root };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Single(result.ExcessCraftOutputs);
            Assert.Equal(2, result.ExcessCraftOutputs[0].ItemId);
            Assert.Equal(1, result.ExcessCraftOutputs[0].ExcessQuantity);
        }

        [Fact]
        public void MultiItemPlan_EachRootsOwnExcess_ExcludedFromOutputs()
        {
            var rootA = CraftNode(itemId: 1, quantity: 10, craftsNeeded: 4, outputCount: 3); // would be 2 excess
            var rootB = CraftNode(itemId: 2, quantity: 1, craftsNeeded: 1, outputCount: 3); // would be 2 excess
            var result = new CraftingPlanResult
            {
                MultiItemRoots = new List<CraftingTreeNode> { rootA, rootB }
            };

            ExcessCraftOutputCalculator.Apply(
                result, new Dictionary<int, ItemPrice>(), new Dictionary<int, ItemMetadata>());

            Assert.Empty(result.ExcessCraftOutputs);
        }
    }
}
