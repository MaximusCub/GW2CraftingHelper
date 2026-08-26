using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverQuantityZeroGuardTests
    {
        // --- A Quantity == 0 node
        // must never leave a standalone "ghost" step, even when its own
        // resolved Source/stepKey does not match any other occurrence's ---
        [Fact]
        public void QuantityZeroNode_NestedUnderCraftedParent_MismatchedStepKey_NoGhostStep()
        {
            // Item 900 occurs twice under root 999's chosen recipe:
            // - branchA: Quantity == 0 (simulating either genuine full
            //   ownership or an AchievementBitDedupPrePass zeroing - both
            //   collapse the same way: Recipes cleared, forced onto a
            //   Buy-only path).
            // - branchB: Quantity == 1, has its OWN recipe (crafting from
            //   901 at 1 coin beats buying 900 at 100), so it resolves to
            //   Craft - a DIFFERENT stepKey than branchA's forced Buy.
            // Before the guard, branchA (Quantity 0, Source
            // BuyFromTp) would still call AggregateStep and - since nothing
            // else shares its (900, BuyFromTp, 0) stepKey - leave a
            // standalone "buy 0 units of 900, 0 cost" row in Plan.Steps.
            var branchA = Leaf(900, 0);
            var branchB = Craftable(900, 1, Option(50, 1, 1, Leaf(901, 5)));
            var root = Craftable(999, 1, Option(10, 1, 1, branchA, branchB));

            var prices = new Dictionary<int, ItemPrice>
            {
                { 900, new ItemPrice { ItemId = 900, BuyInstant = 100 } },
                { 901, new ItemPrice { ItemId = 901, BuyInstant = 1 } },
            };

            var result = new PlanSolver().Solve(root, prices, null, PriceBasis.InstantBuy);

            // branchB genuinely crafts (5*1=5 beats buying at 100).
            Assert.Contains(result.Plan.Steps, s => s.ItemId == 900 && s.Source == AcquisitionSource.Craft && s.Quantity == 1);
            // branchA contributes NOTHING - no standalone zero-quantity row
            // of any Source for item 900.
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 900 && s.Source == AcquisitionSource.BuyFromTp);
            Assert.DoesNotContain(result.Plan.Steps, s => s.Quantity == 0);
        }

        [Fact]
        public void QuantityZeroNode_MatchingStepKeyElsewhere_MergesWithoutInflatingQuantityOrCost()
        {
            // Same shape, but branchB has NO recipe of its own (a plain
            // buy) - both occurrences now share the SAME stepKey
            // (900, BuyFromTp, 0). Confirms the Quantity == 0 guard does
            // not merely avoid a ghost row but also does not change the
            // ordinary merge-by-stepKey outcome for the real occurrence.
            var branchA = Leaf(900, 0);
            var branchB = Leaf(900, 1);
            var root = Craftable(999, 1, Option(10, 1, 1, branchA, branchB));

            var prices = new Dictionary<int, ItemPrice>
            {
                { 900, new ItemPrice { ItemId = 900, BuyInstant = 100 } },
            };

            var result = new PlanSolver().Solve(root, prices, null, PriceBasis.InstantBuy);

            var step = Assert.Single(result.Plan.Steps.Where(s => s.ItemId == 900));
            Assert.Equal(AcquisitionSource.BuyFromTp, step.Source);
            Assert.Equal(1, step.Quantity);
            Assert.Equal(100, step.TotalCost);
        }
    }
}
