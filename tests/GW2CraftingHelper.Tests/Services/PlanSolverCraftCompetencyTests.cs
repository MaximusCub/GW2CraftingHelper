using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;
using static GW2CraftingHelper.Tests.Helpers.VendorOfferBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// (redesign,
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
            // Regression: a node with SEVERAL sibling
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

            // Regression: this
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
            // Regression: the "a genuine next-best
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

        // --- Regression: real
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

        [Fact]
        public void ForceBuyOnlyNode_CompetencyIndependentForceBuy_ReportsNoOpportunity()
        {
            // This node has a SINGLE
            // recipe (Weaponsmith 500, untrained) - the force-buy pre-pass's
            // competency-resolved and competency-blind evaluations of the
            // 0.85 rule are therefore identical (both read the same 30c
            // craft cost, since there is no competent sibling recipe for
            // competency to swap in), so this node is genuinely forced
            // REGARDLESS of training: training Weaponsmith 500 here would
            // unlock nothing. Gating solely on raw forceBuyOnlyNodeIds
            // membership (the original fix) happened to give the right
            // answer for this SPECIFIC shape too, but for the wrong reason
            // - see CompetencyCausedForceBuy_UntrainedCheapestRecipe_
            // ReportsOpportunity below for the shape where that gate was
            // actually wrong (a force-buy exclusion that is ITSELF
            // competency-caused). Passing competencyIndependentForceBuyNodeIds
            // explicitly is what PlanSolver.Evaluate now gates on. (The
            // real pre-pass would produce EMPTY sets for this exact fixture
            // - 100 < 30*0.85 is false - but the hand-fed set is still
            // production-realistic: the pre-pass solves the unreduced tree
            // while the real solve can run reduced, so a node can be in
            // the sets while this solve's own numbers would not force it.)
            var tree = Craftable(1, 1,
                Option(10, 1, 1, new List<string> { "Weaponsmith" }, 500, Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Weaponsmith", Rating = 100 }
            };
            var forceBuyOnly = new HashSet<int> { 0 };

            var solver = new PlanSolver();
            var solveResult = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                forceBuyOnlyNodeIds: forceBuyOnly,
                competencyIndependentForceBuyNodeIds: forceBuyOnly,
                characterDisciplines: characterDisciplines);
            var builder = new CraftingTreeBuilder();
            var root = builder.BuildTree(tree, solveResult.Decisions, new Dictionary<int, ItemMetadata>());

            Assert.Equal(CraftingDecision.BuyFromTp, root.Decision);
            Assert.Equal(100, root.SubtreeCost);
            // The recipe genuinely is untrained and genuinely is cheaper -
            // but the reason craft lost here is force-buy, not competency.
            Assert.False(root.CheapestCraftUntrained);

            var result = new CraftingPlanResult { CraftingTree = root };
            CompetencyOpportunityCalculator.Apply(result);

            Assert.Empty(result.CompetencyOpportunities);
        }

        [Fact]
        public void CompetencyCausedForceBuy_UntrainedCheapestRecipe_ReportsOpportunity()
        {
            // Regression - the measured shape
            // ebdf16c's fix wrongly silenced: root item 1 has TWO recipes -
            // RecipeId 10 (Weaponsmith 500, ingredient item 2 @ 30c - the
            // untrained, numerically CHEAPEST recipe overall) and RecipeId
            // 20 (MysticForge - a non-levelable tag, inherently "competent"
            // regardless of training, see
            // CraftCompetencyEvaluator.NonLevelableDisciplineTags -
            // ingredient item 3 @ 1000c). TP buy = 100c. Nobody is trained
            // in Weaponsmith 500.
            //
            // Run through the REAL pre-pass -> solve -> calculator pipeline
            // (unlike the hand-fed forceBuyOnlyNodeIds fixture above):
            // OwnedMaterialsForceBuyPrePass's own throwaway solve is
            // competency-aware, so its craft diagnostic resolves to the
            // COMPETENT MysticForge recipe (1000c) - buy (100c) is less
            // than 1000c*0.85=850c, so root lands in ForceBuyOnlyNodeIds.
            // But the SECOND, competency-BLIND evaluation (the RAW cheapest
            // craft cost, 30c, ignoring training) does NOT force it (100c
            // is not less than 30c*0.85=25.5c) - so root is NOT in
            // CompetencyIndependentForceBuyNodeIds: this force-buy
            // exclusion is ITSELF competency-caused. Training Weaponsmith
            // 500 would empty the force-buy set entirely and let the plan
            // craft at 30c instead of buying at 100c - a genuine 70c
            // opportunity.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, new List<string> { "Weaponsmith" }, 500, Leaf(2, 1)),
                Option(20, 1, 1, new List<string> { "MysticForge" }, 0, Leaf(3, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 1000 } }
            };
            var characterDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Toon", Discipline = "Weaponsmith", Rating = 100 }
            };

            var solver = new PlanSolver();

            // Real pre-pass: computes BOTH sets from one throwaway solve
            // pass - see OwnedMaterialsForceBuyPrePass.ForceBuyPrePassResult's
            // own doc comment.
            var forceBuyPrePassResult = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                solver, tree, prices, null, PriceBasis.InstantBuy, null,
                characterDisciplines: characterDisciplines);

            Assert.Contains(0, forceBuyPrePassResult.ForceBuyOnlyNodeIds);
            Assert.DoesNotContain(0, forceBuyPrePassResult.CompetencyIndependentForceBuyNodeIds);

            // Real solve: reuses the SAME tree (its NodeIds were already
            // stably assigned by the pre-pass's own throwaway solve above,
            // matching CraftingPlanPipeline's own assignNodeIds:false
            // production wiring at Step 7).
            var solveResult = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: null,
                forceBuyOnlyNodeIds: forceBuyPrePassResult.ForceBuyOnlyNodeIds,
                competencyIndependentForceBuyNodeIds: forceBuyPrePassResult.CompetencyIndependentForceBuyNodeIds,
                assignNodeIds: false,
                characterDisciplines: characterDisciplines);

            var builder = new CraftingTreeBuilder();
            var root = builder.BuildTree(tree, solveResult.Decisions, new Dictionary<int, ItemMetadata>());

            // Force-buy still wins the real solve (solver behavior itself
            // is UNCHANGED by this fix) - the plan commits BuyFromTp@100.
            Assert.Equal(CraftingDecision.BuyFromTp, root.Decision);
            Assert.Equal(100, root.SubtreeCost);
            // But CheapestCraftUntrained is no longer suppressed - this
            // force-buy exclusion is competency-caused, not genuine.
            Assert.True(root.CheapestCraftUntrained);
            Assert.Equal(30, root.CheapestCraftRealCost);

            var result = new CraftingPlanResult { CraftingTree = root };
            CompetencyOpportunityCalculator.Apply(result);

            var opportunity = Assert.Single(result.CompetencyOpportunities);
            Assert.Equal(1, opportunity.ItemId);
            Assert.Equal(30, opportunity.CraftCost);
            Assert.Equal(70, opportunity.DeltaCost);
        }

        // --- Characterization: CompetencyOpportunityCalculator is a real
        // downstream consumer of AllocateVendorNodeCosts' merged-ceil
        // remainder shape (quorum verdict C6, merged-ceil-remainder
        // stream). Item 200 has two tree occurrences (qty 1 each) of the
        // SAME "100 for 1000c" bulk vendor offer used by the VendorBatchSolver-
        // level characterization above, each also eligible for the SAME
        // untrained-but-cheaper craft recipe (real cost 100/unit via leaf
        // item 300). CompetencyOpportunityCalculator.Walk records the
        // FIRST tree occurrence whose own SubtreeCost clears the
        // CheapestCraftRealCost gate - NOT simply the first occurrence in
        // the tree - so which occurrence's (possibly skewed) allocated
        // share gets reported depends on AllocateVendorNodeCosts' own
        // per-occurrence math:
        //   PRE-FIX, this used to report an inflated 890 delta: occ1's
        //   floor share (10) did not clear the 100 gate, so the walk fell
        //   through to occ2, whose skewed remainder share (990) did -
        //   reporting a number that only existed because of tree
        //   position, not real economics.
        //   FIXED: occ1's fair proportional share (500) now clears the
        //   gate on first encounter, reporting the true 400 delta; occ2
        //   is never evaluated for recording once item 200 is already in
        //   byItemId.
        [Fact]
        public void MergedVendorLeaf_UnequalOccurrenceShares_CompetencyDeltaUsesFairProportionalShare()
        {
            var occ1 = Craftable(200, 1,
                Option(20, 1, 1, new List<string> { "Weaponsmith" }, 500, Leaf(300, 1)));
            var occ2 = Craftable(200, 1,
                Option(20, 1, 1, new List<string> { "Weaponsmith" }, 500, Leaf(300, 1)));
            var tree = Craftable(1, 1, Option(10, 1, 1, occ1, occ2));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 300, new ItemPrice { ItemId = 300, BuyInstant = 100 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 200, new List<VendorOffer> { CoinVendorOffer(200, 1000, outputCount: 100) } }
            };
            var characterDisciplines = new List<SnapshotCharacterDiscipline>();

            var root = SolveAndBuildRootNode(tree, prices, vendorOffers, characterDisciplines);

            Assert.Equal(CraftingDecision.Craft, root.Decision);
            Assert.Equal(2, root.Children.Count);
            Assert.Equal(CraftingDecision.BuyFromVendor, root.Children[0].Decision);
            Assert.Equal(CraftingDecision.BuyFromVendor, root.Children[1].Decision);
            // Fair proportional split (500/500), matching the
            // VendorBatchSolver-level characterization.
            Assert.Equal(500, root.Children[0].SubtreeCost);
            Assert.Equal(500, root.Children[1].SubtreeCost);

            var result = new CraftingPlanResult { CraftingTree = root };
            CompetencyOpportunityCalculator.Apply(result);

            var opportunity = Assert.Single(result.CompetencyOpportunities);
            Assert.Equal(200, opportunity.ItemId);
            Assert.Equal(100, opportunity.CraftCost);
            Assert.Equal(400, opportunity.DeltaCost);
        }
    }
}
