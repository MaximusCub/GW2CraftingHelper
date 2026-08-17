using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;
using static GW2CraftingHelper.Tests.Helpers.VendorOfferBuilders;

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
        public void MultiRecipeNode_OneCompetentOneNot_CompetentSiblingAutoWinsOverExcludedCheaperOne()
        {
            // Adversarial-review Critical #1: a node with SEVERAL sibling
            // RecipeOptions (routine - CompositeRecipeApiClient merges API
            // recipe ids with MysticForgeRecipeData for the same output,
            // and the API itself can return more than one recipe per
            // output) must not have its ENTIRE craft arm excluded just
            // because the single CHEAPEST option happens to be untrained.
            // Recipe 10 (Weaponsmith 500, 600c) is cheaper than recipe 11
            // (MysticForge - inherently available, 800c), but the account
            // has no Weaponsmith - so the auto-pick must fall through to
            // the more expensive but ACTUALLY CRAFTABLE recipe 11 (800c),
            // never default to TP at 1000c.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, new List<string> { "Weaponsmith" }, 500, Leaf(2, 1)),
                Option(11, 1, 1, new List<string> { "MysticForge" }, 0, Leaf(3, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 600 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 800 } }
            };
            var solver = new PlanSolver();
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Weaponsmith", Rating = 100 }
            };

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                characterDisciplines: characterDisciplines);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(800, result.Decisions[0].TotalCost);
            Assert.Equal(11, result.Decisions[0].RecipeId);
            Assert.True(result.Decisions[0].CanCraft);

            // Adversarial-review round-2 fix (finding #5), shape (b): this
            // node's craft did NOT get excluded (recipe 11 auto-won), but
            // the CHEAPEST recipe overall (recipe 10, untrained) is still
            // untrained - CheapestCraftUntrained must be true here even
            // though CraftExcludedByCompetency stays false, so
            // CompetencyOpportunityCalculator can report "you could save
            // 200c by training Weaponsmith 500" instead of staying silent.
            Assert.False(result.Decisions[0].CraftExcludedByCompetency);
            Assert.True(result.Decisions[0].CheapestCraftUntrained);
            Assert.Equal(600, result.Decisions[0].CheapestCraftRealCost);
            Assert.Equal("Weaponsmith", Assert.Single(result.Decisions[0].CheapestCraftDisciplines));
            Assert.Equal(500, result.Decisions[0].CheapestCraftMinRating);
        }

        [Fact]
        public void NonCompetentAccount_OnlyAlternativeIsFallbackTierVendor_StillAutoCraftsRatherThanDroppingCost()
        {
            // Adversarial-review Critical #6: the "a genuine next-best
            // source must exist" guard must NOT count a FALLBACK-tier
            // vendor offer (unvalued non-coin currency, e.g. karma-only) as
            // a real alternative. Node has a fully-priced comparable craft
            // (60c) the account cannot craft (untrained Weaponsmith), no TP
            // price, and only a karma-only vendor offer. Before this fix,
            // competency excluded craft, PickCheapest returned
            // UnknownSource, and the terminal fallback branch committed
            // BuyFromVendor at fallbackVendorCoinCost (0c coin) - silently
            // dropping the node's real 60c priced cost and defaulting onto
            // an unvalued karma purchase. Craft must still auto-win here:
            // nothing genuinely comparable exists to default to instead.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, new List<string> { "Weaponsmith" }, 500, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
                // No price for item 1 itself - no TP alternative.
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, coinCost: 0, currencyId: 23, currencyCount: 500000) } }
            };
            var solver = new PlanSolver();
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Weaponsmith", Rating = 0 }
            };

            var result = solver.Solve(
                tree, prices, vendorOffers, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                characterDisciplines: characterDisciplines);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(60, result.Decisions[0].TotalCost);
            Assert.Equal(10, result.Decisions[0].RecipeId);
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

        // --- Adversarial-review round-2 fix (finding #5): real
        // Solve() + CraftingTreeBuilder + CompetencyOpportunityCalculator
        // round trips for the two shapes CraftExcludedByCompetency alone
        // left unreported. Same production pipeline
        // PlanSolverPillSubduingTests already exercises for the subduing
        // feature - proves the whole CheapestCraftUntrained threading, not
        // just the isolated calculator/Decision-field coverage above.

        private static CraftingTreeNode SolveAndBuildRootNode(
            RecipeNode tree, Dictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines)
        {
            var solver = new PlanSolver();
            var solveResult = solver.Solve(
                tree, prices, vendorOffers, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                characterDisciplines: characterDisciplines);
            var builder = new CraftingTreeBuilder();
            return builder.BuildTree(tree, solveResult.Decisions, new Dictionary<int, ItemMetadata>());
        }

        [Fact]
        public void FallbackTierCompetentRecipe_CheaperComparableUntrained_ReportsOpportunity()
        {
            // Shape (a): recipe 10 (Weaponsmith 500, comparable-tier, 30c)
            // is the numerically cheapest craft option but untrained;
            // recipe 20 (Armorsmith 400, FALLBACK-tier - an unvalued
            // Currency ingredient) is competent but never competes on coin
            // cost at all (fallback-tier craft never enters the
            // comparable-tier PickCheapest race). TP buy (1000c) wins by
            // default - CraftExcludedByCompetency stays false (a competent
            // option DOES exist, just in the wrong tier), so only the
            // generalized CheapestCraftUntrained fix reports the missed
            // 970c saving.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, new List<string> { "Weaponsmith" }, 500, Leaf(2, 1)),
                Option(20, 1, 1, new List<string> { "Armorsmith" }, 400, Leaf(3, 1), Leaf(999, 5, "Currency")));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 20 } }
            };
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Armorsmith", Rating = 400 }
            };

            var root = SolveAndBuildRootNode(tree, prices, null, characterDisciplines);

            Assert.Equal(CraftingDecision.BuyFromTp, root.Decision);
            Assert.Equal(1000, root.SubtreeCost);
            Assert.False(root.CraftExcludedByCompetency);
            Assert.True(root.CheapestCraftUntrained);
            Assert.Equal(30, root.CheapestCraftRealCost);

            var result = new CraftingPlanResult { CraftingTree = root };
            CompetencyOpportunityCalculator.Apply(result);

            var opp = Assert.Single(result.CompetencyOpportunities);
            Assert.Equal(1, opp.ItemId);
            Assert.Equal(30, opp.CraftCost);
            Assert.Equal(970, opp.DeltaCost);
            Assert.Equal("Weaponsmith", Assert.Single(opp.Disciplines));
            Assert.Equal(500, opp.MinRating);
        }

        [Fact]
        public void CostlierCompetentSiblingWinsCraft_CheaperUntrainedSibling_ReportsOpportunity()
        {
            // Shape (b), full round trip: same tree as
            // MultiRecipeNode_OneCompetentOneNot_CompetentSiblingAutoWinsOverExcludedCheaperOne
            // above - recipe 11 (MysticForge, 800c) auto-wins Craft over
            // the cheaper but untrained recipe 10 (Weaponsmith 500, 600c).
            // The plan DOES craft (Decision == Craft), so the pre-fix
            // "Decision != Craft" guard would have suppressed this - the
            // generalized delta check reports it anyway.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, new List<string> { "Weaponsmith" }, 500, Leaf(2, 1)),
                Option(11, 1, 1, new List<string> { "MysticForge" }, 0, Leaf(3, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 600 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 800 } }
            };
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Weaponsmith", Rating = 100 }
            };

            var root = SolveAndBuildRootNode(tree, prices, null, characterDisciplines);

            Assert.Equal(CraftingDecision.Craft, root.Decision);
            Assert.Equal(800, root.SubtreeCost);

            var result = new CraftingPlanResult { CraftingTree = root };
            CompetencyOpportunityCalculator.Apply(result);

            var opp = Assert.Single(result.CompetencyOpportunities);
            Assert.Equal(1, opp.ItemId);
            Assert.Equal(600, opp.CraftCost);
            Assert.Equal(200, opp.DeltaCost);
            Assert.Equal("Weaponsmith", Assert.Single(opp.Disciplines));
            Assert.Equal(500, opp.MinRating);
        }
    }
}
