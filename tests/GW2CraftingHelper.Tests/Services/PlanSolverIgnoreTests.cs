using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverIgnoreTests
    {
        // --- "Ignore" pill (ignoredItemIds) ---
        [Fact]
        public void IgnoredItemIds_LeafBuyNode_ZeroCostNoStep()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                ignoredItemIds: new HashSet<int> { 1 });

            Assert.Empty(result.Plan.Steps);
            Assert.Equal(0, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void IgnoredItemIds_CraftIngredient_ParentCostExcludesIgnoredIngredient()
        {
            // Item 1 crafts from 2x item 2 (would normally cost 2*100=200).
            // Ignoring item 2 must make the WHOLE craft's cost 0, not just
            // hide item 2's own row - matching gw2e's "owned materials are
            // free" rule (Section 2.1 of the r2 report) applied via Ignore.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } }, // buying finished item is far pricier
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
            };
            var solver = new PlanSolver();

            var baseline = solver.Solve(tree, prices, null);
            Assert.Equal(AcquisitionSource.Craft, baseline.Decisions[0].Source);
            Assert.Equal(200, baseline.Plan.TotalCoinCost);

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                ignoredItemIds: new HashSet<int> { 2 });

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(0, result.Plan.TotalCoinCost);
            // Item 2's own row is gone entirely - not a "0 cost" leftover row.
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 2);
        }

        [Fact]
        public void IgnoredItemIds_DoesNotAffectUnrelatedItem()
        {
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(2, 2), Leaf(3, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 50 } },
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                ignoredItemIds: new HashSet<int> { 2 });

            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 2);
            var item3Step = result.Plan.Steps.Single(s => s.ItemId == 3);
            Assert.Equal(AcquisitionSource.BuyFromTp, item3Step.Source);
            Assert.Equal(50, item3Step.TotalCost);
            Assert.Equal(50, result.Plan.TotalCoinCost); // only item 3's real cost remains
        }

        [Fact]
        public void IgnoredItemIds_DoesNotRecurseIntoIgnoredNodesOwnIngredients()
        {
            // Item 2 (ignored) itself crafts from item 3 - since item 2 is
            // treated as fully in-hand, its own recipe must never be
            // evaluated/collected (matching gw2e's "an un-crafted branch
            // never asks for its ingredients" rule), so item 3 must not
            // appear anywhere in the plan even though it has a real price.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Craftable(2, 5, Option(20, 1, 1, Leaf(3, 10)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 5 } },
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                ignoredItemIds: new HashSet<int> { 2 });

            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 2);
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 3);
            Assert.Equal(0, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void IgnoredItemIds_Null_BehavesExactlyAsBefore()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                ignoredItemIds: null);

            Assert.Single(result.Plan.Steps);
            Assert.Equal(500, result.Plan.TotalCoinCost);
        }

        // KNOWN-ISSUES #20.4 "Conservative reading":
        // the Ignore x owned-materials interaction is NOT pinned at this
        // layer. RecipeNode (the type Solve consumes) has no ownership
        // field at all - only Id/NodeId/Quantity/achievement fields - so
        // "a node already reduced by partial ownership" cannot be
        // represented here beyond just choosing a smaller Quantity, which
        // collapses to the exact same Evaluate/Collect code path as
        // IgnoredItemIds_LeafBuyNode_ZeroCostNoStep above and proves
        // nothing extra about the interaction. Ownership only exists on the
        // downstream CraftingTreeNode built after Solve returns, so the
        // real pin lives one and two layers up:
        // CraftingPlanPipelineIgnoreTests (GenerateStructuredAsync Ignore x
        // owned-materials coverage) and DecisionPillPlannerTests
        // (Have_IgnoredAndPartiallyOwned_ShowsIgnoredNotOwnedInfo).
    }
}
