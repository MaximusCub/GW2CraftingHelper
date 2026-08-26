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
        // --- Cost diagnostics + force-buy-only exclusion ---
        [Fact]
        public void CostDiagnostics_PopulatedForEveryItemNode_RegardlessOfDecision()
        {
            // Item 1: buy 1000, craft from item 2 (2x30=60) - craft wins.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } },
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
        public void CostDiagnostics_CompetencyResolved_UsesCompetentRecipeNotCheapestOverall()
        {
            // Regression: two recipes for item
            // 1 - RecipeId 10 (Weaponsmith 400, ingredient item 2 x1 @
            // 30c = the numerically cheapest overall) and RecipeId 20
            // (Armorsmith 400, ingredient item 3 x1 @ 100c = costlier, but
            // the ONLY one this account is actually trained for).
            // costDiagnostics' CraftCost figure must track the SAME
            // competency-resolved recipe the real solve commits to (100c,
            // RecipeId 20) - never the cheapest-overall untrained recipe
            // (30c, RecipeId 10) the real solve would never actually pick.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, new List<string> { "Weaponsmith" }, 400, Leaf(2, 1)),
                Option(20, 1, 1, new List<string> { "Armorsmith" }, 400, Leaf(3, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 5000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 100 } },
            };
            var solver = new PlanSolver();
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Armorsmith", Rating = 400 },
            };

            var diagnostics = new Dictionary<int, (long? BuyCost, long? CraftCost)>();
            solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, overrides: null, currencyValuation: null,
                forceBuyOnlyNodeIds: null, costDiagnostics: diagnostics,
                characterDisciplines: characterDisciplines);

            Assert.True(diagnostics.TryGetValue(0, out var rootDiag));
            Assert.Equal(5000, rootDiag.BuyCost);
            Assert.Equal(100, rootDiag.CraftCost);

            // Confirms the diagnostic figure actually matches what the real
            // solve commits to, not merely a value in isolation.
            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, overrides: null, currencyValuation: null,
                characterDisciplines: characterDisciplines);
            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(20, result.Decisions[0].RecipeId);
            Assert.Equal(100, result.Decisions[0].TotalCost);
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
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } },
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
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } },
            };
            var solver = new PlanSolver();

            var forceBuyOnly = new HashSet<int> { 0 };
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { 0, AcquisitionSource.Craft },
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
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } },
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
