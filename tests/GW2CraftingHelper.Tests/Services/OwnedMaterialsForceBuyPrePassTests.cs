using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class OwnedMaterialsForceBuyPrePassTests
    {
        private static RecipeNode Leaf(int id, int quantity, string type = "Item")
        {
            return new RecipeNode { Id = id, IngredientType = type, Quantity = quantity };
        }

        private static RecipeNode Craftable(int id, int quantity, params RecipeOption[] recipes)
        {
            var node = new RecipeNode { Id = id, IngredientType = "Item", Quantity = quantity };
            node.Recipes.AddRange(recipes);
            return node;
        }

        private static RecipeOption Option(int recipeId, int outputCount, int craftsNeeded, params RecipeNode[] ingredients)
        {
            var opt = new RecipeOption { RecipeId = recipeId, OutputCount = outputCount, CraftsNeeded = craftsNeeded };
            opt.Ingredients.AddRange(ingredients);
            return opt;
        }

        [Fact]
        public void BuyBeatsCraftByMoreThan15Percent_ForcesRootIntoForceBuySet()
        {
            // Root buy = 100; components (2x30=60) cost less than 85 (0.85 x 100).
            // 100 < 60*... wait - the rule is buy < craft*0.85: buy=60, craft=100
            // would need buy cheaper - construct so BUY is the cheap side:
            // buy=100, craft(components fresh)=200 -> 100 < 200*0.85=170 -> forced.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var forced = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);

            Assert.Contains(0, forced); // root NodeId
        }

        [Fact]
        public void BuyBeatsCraftByLessThan15Percent_NotForced()
        {
            // buy=95, craft=100 -> 95 < 100*0.85=85? No (95 > 85) - not forced,
            // even though buy is still cheaper than craft outright.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 95 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var forced = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);

            Assert.DoesNotContain(0, forced);
        }

        [Fact]
        public void CraftCheaperThanBuy_NotForced()
        {
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();

            var forced = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);

            Assert.Empty(forced);
        }

        [Fact]
        public void NoRecipe_NeverForced()
        {
            // A leaf with no recipe has no craftCost at all - never forced,
            // regardless of its buy price.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 10 } }
            };
            var solver = new PlanSolver();

            var forced = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);

            Assert.Empty(forced);
        }

        [Fact]
        public void NoBuyPrice_NeverForced()
        {
            // No TP buy price at all for the root - can't compare against
            // craft cost, so it can never be forced (matches "buy < craft
            // requires buy to actually exist").
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var forced = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null);

            Assert.Empty(forced);
        }
    }
}
