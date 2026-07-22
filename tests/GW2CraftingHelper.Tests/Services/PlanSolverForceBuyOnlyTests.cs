using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverForceBuyOnlyTests
    {
        // --- M34-B2a #3: cost diagnostics + force-buy-only exclusion ---

        [Fact]
        public void CostDiagnostics_PopulatedForEveryItemNode_RegardlessOfDecision()
        {
            // Item 1: buy 1000, craft from item 2 (2x30=60) - craft wins.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();
            var diagnostics = new Dictionary<int, (long? BuyCost, long? CraftCost)>();

            solver.Solve(tree, prices, null, PriceBasis.InstantBuy, null, null,
                forceBuyOnlyNodeIds: null, costDiagnostics: diagnostics);

            // Root (NodeId 0): buy=1000, craft=60 - present even though craft won.
            Assert.True(diagnostics.TryGetValue(0, out var rootDiag));
            Assert.Equal(1000, rootDiag.BuyCost);
            Assert.Equal(60, rootDiag.CraftCost);

            // Leaf ingredient (item 2, NodeId 1): buy=60 (2x30), no recipe -> craft null.
            Assert.True(diagnostics.TryGetValue(1, out var leafDiag));
            Assert.Equal(60, leafDiag.BuyCost);
            Assert.Null(leafDiag.CraftCost);
        }

        [Fact]
        public void ForceBuyOnlyNodeIds_ExcludesCraftFromAutomaticPick()
        {
            // Craft (60) would normally beat buy (100); force-buy-only
            // excludes craft for the root node, so buy wins instead even
            // though nothing else about the tree/prices changed.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();

            var baseline = solver.Solve(tree, prices, null);
            Assert.Equal(AcquisitionSource.Craft, baseline.Decisions[0].Source);

            var forceBuyOnly = new HashSet<int> { 0 };
            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                forceBuyOnlyNodeIds: forceBuyOnly);

            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[0].Source);
            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
            Assert.Equal(100, result.Plan.TotalCoinCost);
            // CanCraft still reflects true feasibility (a recipe exists) -
            // only the AUTOMATIC pick is affected, not the reported flag.
            Assert.True(result.Decisions[0].CanCraft);
        }

        [Fact]
        public void ForceBuyOnlyNodeIds_ManualOverrideStillWinsOverForceBuy()
        {
            // Same setup as above, but the user ALSO manually forces Craft
            // on the root - matching gw2e's own "manual pill always beats
            // the automatic pre-pass" rule (Section 3.2 of the R2 report).
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();

            var forceBuyOnly = new HashSet<int> { 0 };
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { 0, AcquisitionSource.Craft }
            };

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, overrides, null,
                forceBuyOnlyNodeIds: forceBuyOnly);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(60, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void ForceBuyOnlyNodeIds_Null_BehavesExactlyAsBefore()
        {
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                forceBuyOnlyNodeIds: null);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(60, result.Plan.TotalCoinCost);
        }
    }
}
