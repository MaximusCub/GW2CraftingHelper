using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// source-selection-simplification (maintainer-approved redesign,
    /// docs/gw2e-considerations.md): a Craft source only wins the
    /// AUTOMATIC buy-vs-craft-vs-vendor comparison when some character can
    /// actually craft it - see PlanSolver.Evaluate's craftExcludedFromAutoPick
    /// competency branch (folded into the SAME seam the force-buy pre-pass
    /// already uses) and CraftCompetencyEvaluator (the pure detection
    /// logic). Real Solve()-path coverage, same granularity every other
    /// PlanSolver decision-rule test suite in this file set uses (e.g.
    /// PlanSolverForceBuyOnlyTests, PlanSolverCraftVendorComparabilityTests).
    /// </summary>
    public class PlanSolverCraftCompetencyTests
    {
        private static readonly Dictionary<int, ItemPrice> CraftBeatsBuyPrices = new Dictionary<int, ItemPrice>
        {
            { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } }, // buy is expensive
            { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }    // ingredient is cheap -> craft (60) beats buy (1000)
        };

        private static RecipeNode WeaponsmithTree()
        {
            return Craftable(1, 1,
                Option(10, 1, 1, new List<string> { "Weaponsmith" }, 400, Leaf(2, 2)));
        }

        [Fact]
        public void NoCharacterDisciplines_CompetencyUnknown_CraftStillAutoWins()
        {
            // Baseline/regression: omitting characterDisciplines entirely
            // (every pre-existing caller) must reproduce pre-existing
            // behavior byte-for-byte - competency UNKNOWN, never penalizes.
            var solver = new PlanSolver();

            var result = solver.Solve(WeaponsmithTree(), CraftBeatsBuyPrices);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(60, result.Decisions[0].TotalCost);
            Assert.True(result.Decisions[0].CanCraft);
        }

        [Fact]
        public void CompetentCharacter_MeetsMinRating_CraftAutoWins()
        {
            var solver = new PlanSolver();
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Weaponsmith", Rating = 400 }
            };

            var result = solver.Solve(
                WeaponsmithTree(), CraftBeatsBuyPrices, null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                characterDisciplines: characterDisciplines);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(60, result.Decisions[0].TotalCost);
        }

        [Fact]
        public void NonCompetentAccount_CraftCheapestButNotCraftable_DefaultsToNextBestSource()
        {
            // Craft (60) is numerically cheapest, but no character meets
            // the Weaponsmith 400 requirement - TP buy (1000) becomes the
            // committed default instead. CanCraft must stay true (the CRAFT
            // pill stays clickable/available - only the AUTOMATIC default
            // flipped, matching a manual override's own untouched
            // feasibility contract).
            var solver = new PlanSolver();
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Weaponsmith", Rating = 100 }
            };

            var result = solver.Solve(
                WeaponsmithTree(), CraftBeatsBuyPrices, null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                characterDisciplines: characterDisciplines);

            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[0].Source);
            Assert.Equal(1000, result.Decisions[0].TotalCost);
            Assert.True(result.Decisions[0].CanCraft);
        }

        [Fact]
        public void KnownButEmptyCharacterDisciplines_NobodyTrainedAtAll_DefaultsToNextBestSource()
        {
            // A real snapshot with zero characters at all (not "no
            // snapshot") is still known data - competency is false, not
            // unknown - so the default still flips.
            var solver = new PlanSolver();

            var result = solver.Solve(
                WeaponsmithTree(), CraftBeatsBuyPrices, null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                characterDisciplines: new List<SnapshotCharacterDiscipline>());

            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[0].Source);
        }

        [Fact]
        public void NonCompetentAccount_NoAlternativeSourceExists_StillAutoCraftsRatherThanDroppingCost()
        {
            // No TP price and no vendor offer for item 1 at all - Craft is
            // the ONLY feasible source. Excluding it here would produce
            // UnknownSource/null cost for a node that has a real, priced
            // recipe - corrupting the plan's totals rather than merely
            // changing a default - so competency must NOT exclude craft
            // when there is nothing to default to instead.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, new List<string> { "Weaponsmith" }, 400, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Weaponsmith", Rating = 0 }
            };

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                characterDisciplines: characterDisciplines);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(60, result.Decisions[0].TotalCost);
        }

        [Fact]
        public void NonCompetentAccount_ManualOverrideToCraft_StillHonored()
        {
            // A manual override always wins over the automatic pick,
            // competency-driven or not - unaffected by this feature.
            var solver = new PlanSolver();
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Weaponsmith", Rating = 100 }
            };
            var overrides = new Dictionary<int, AcquisitionSource> { { 0, AcquisitionSource.Craft } };

            var result = solver.Solve(
                WeaponsmithTree(), CraftBeatsBuyPrices, null, PriceBasis.InstantBuy,
                overrides: overrides, currencyValuation: null,
                characterDisciplines: characterDisciplines);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(60, result.Decisions[0].TotalCost);
        }

        [Fact]
        public void NonLevelableDisciplineTag_InherentlyAvailable_NeverBlocked()
        {
            // A MysticForge-tagged recipe has no unlock/level concept -
            // never blocked by competency, even with a known-empty
            // characterDisciplines list.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, new List<string> { "MysticForge" }, 0, Leaf(2, 2)));
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, CraftBeatsBuyPrices, null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                characterDisciplines: new List<SnapshotCharacterDiscipline>());

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
        }
    }
}
