using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverMultiItemWrapperTests
    {
        // --- M35-B1: synthetic multi-item wrapper root (gw2e parity) ---
        // WrapperOf lives in Helpers/RecipeNodeBuilders.cs.

        [Fact]
        public void WrapperRoot_NeverAppearsAsItsOwnStep_OnlyItemRootsDo()
        {
            var itemA = Leaf(100, 5);
            var itemB = Leaf(200, 3);
            var wrapper = WrapperOf(itemA, itemB);

            var prices = new Dictionary<int, ItemPrice>
            {
                { 100, new ItemPrice { ItemId = 100, BuyInstant = 10 } },
                { 200, new ItemPrice { ItemId = 200, BuyInstant = 20 } }
            };

            var result = new PlanSolver().Solve(wrapper, prices, null, PriceBasis.InstantBuy);
            var plan = result.Plan;

            Assert.Equal(2, plan.Steps.Count);
            Assert.DoesNotContain(plan.Steps, s => s.ItemId == Gw2Constants.MultiItemWrapperItemId);
            Assert.Contains(plan.Steps, s => s.ItemId == 100 && s.Quantity == 5 && s.TotalCost == 50);
            Assert.Contains(plan.Steps, s => s.ItemId == 200 && s.Quantity == 3 && s.TotalCost == 60);
            Assert.Equal(110, plan.TotalCoinCost);

            // The wrapper's own memo entry exists (Evaluate always visits
            // it) but is never surfaced via a step; it also never appears
            // as a decision a caller would look up (NodeId 0, pre-order
            // DFS root).
            Assert.True(result.Decisions.ContainsKey(wrapper.NodeId));
        }

        [Fact]
        public void WrapperRoot_EachItemRoot_GetsIndependentCraftVsBuyDecision()
        {
            // Item A: crafting (2 x 10 = 20) beats its own buy price (100).
            var ingredientA = Leaf(101, 2);
            var itemA = Craftable(100, 1, Option(110, 1, 1, ingredientA));
            // Item B: no recipe, always bought.
            var itemB = Leaf(200, 4);

            var wrapper = WrapperOf(itemA, itemB);

            var prices = new Dictionary<int, ItemPrice>
            {
                { 100, new ItemPrice { ItemId = 100, BuyInstant = 100 } },
                { 101, new ItemPrice { ItemId = 101, BuyInstant = 10 } },
                { 200, new ItemPrice { ItemId = 200, BuyInstant = 5 } }
            };

            var result = new PlanSolver().Solve(wrapper, prices, null, PriceBasis.InstantBuy);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[itemA.NodeId].Source);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[itemB.NodeId].Source);
        }
    }
}
