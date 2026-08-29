using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The reported defect, driven end to end over the SHIPPED corpus:
    /// ref/recipes_seed.json, ref/mystic_forge_recipes.json and
    /// ref/vendor_offers.json, through the production RecipeService,
    /// VendorOfferLoader and PlanSolver.
    /// <para>
    /// Lyhr, in the Wizard's Tower, sells the Obsidian Heavy Breastplate for
    /// the four Gifts its recipe calls for plus 10 Globs of Ectoplasm, and
    /// the module recommended buying at 2g95s10c - the ectoplasm, and nothing
    /// else. docs/KNOWN-ISSUES.md item 44, docs/ARCHITECTURE.md section 7.4.
    /// </para>
    /// <para>
    /// Only Globs of Ectoplasm carry a price here, the same single input the
    /// original report was measured with, so the figures below are directly
    /// comparable with it.
    /// </para>
    /// </summary>
    public class VendorCostLineExpansionRealCorpusTests
    {
        private const int ObsidianHeavyBreastplate = 101521;
        private const int GlobOfEctoplasm = 19721;
        private const int EctoplasmUnitPrice = 2916;

        /// <summary>The fee Lyhr charges on top of the recipe: 10 x 2,916.</summary>
        private const long LyhrFee = 10L * EctoplasmUnitPrice;

        private sealed class Fixture
        {
            public RecipeNode Tree;
            public Dictionary<int, ItemPrice> Prices;
            public Dictionary<int, IReadOnlyList<VendorOffer>> Offers;
            public VendorCostLineSubtrees Subtrees;
        }

        /// <summary>
        /// Builds the plan tree and the cost-line subtrees the same way
        /// CraftingPlanPipeline.ExpandVendorCostLinesAsync does: expand the
        /// Item cost lines nothing can price, then expand whatever the new
        /// subtrees exposed, until nothing new appears.
        /// </summary>
        private static async Task<Fixture> BuildAsync(int itemId, int quantity)
        {
            var corpus = RealCorpusFixture.Load();
            var recipeService = corpus.NewRecipeService();
            var tree = await recipeService.BuildTreeAsync(itemId, quantity, CancellationToken.None);

            var prices = new Dictionary<int, ItemPrice>
            {
                {
                    GlobOfEctoplasm,
                    new ItemPrice
                    {
                        ItemId = GlobOfEctoplasm,
                        BuyInstant = EctoplasmUnitPrice,
                        SellInstant = EctoplasmUnitPrice,
                    }
                },
            };

            var offers = new Dictionary<int, IReadOnlyList<VendorOffer>>();
            AddOffers(corpus, ItemIdsIn(tree), offers);

            var subtreesByItemId = new Dictionary<int, RecipeNode>();
            var built = new HashSet<int>();
            var frontier = new List<IReadOnlyList<VendorOffer>>(offers.Values);

            for (int round = 0; round < 6; round++)
            {
                var wanted = VendorCostLineSubtrees.CollectUnpricedCostItemIds(
                    frontier, prices, PriceBasis.InstantBuy, built);
                if (wanted.Count == 0)
                {
                    break;
                }

                var next = new List<IReadOnlyList<VendorOffer>>();
                foreach (int costItemId in wanted.OrderBy(id => id))
                {
                    built.Add(costItemId);
                    var subtree = await recipeService.BuildTreeAsync(costItemId, 1, CancellationToken.None);
                    subtreesByItemId[costItemId] = subtree;
                    AddOffers(corpus, ItemIdsIn(subtree), offers, next);
                }

                frontier = next;
            }

            return new Fixture
            {
                Tree = tree,
                Prices = prices,
                Offers = offers,
                Subtrees = VendorCostLineSubtrees.Create(subtreesByItemId),
            };
        }

        private static SolveResult Solve(
            Fixture f,
            IReadOnlyDictionary<int, AcquisitionSource> overrides = null,
            IReadOnlyList<SnapshotCharacterDiscipline> disciplines = null,
            bool withSubtrees = true)
        {
            return new PlanSolver().Solve(
                f.Tree, f.Prices, f.Offers, PriceBasis.InstantBuy,
                overrides: overrides,
                currencyValuation: null,
                forceBuyOnlyNodeIds: null,
                competencyIndependentForceBuyNodeIds: null,
                costDiagnostics: null,
                rawCraftCostDiagnostics: null,
                assignNodeIds: true,
                ignoredItemIds: null,
                homesteadTiers: null,
                characterDisciplines: disciplines,
                ownedQuantityUsedByNode: null,
                vendorCostSubtrees: withSubtrees ? f.Subtrees : null);
        }

        private static IReadOnlyList<SnapshotCharacterDiscipline> MasterArmorsmith()
        {
            return new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline
                {
                    CharacterName = "Taimi",
                    Discipline = "Armorsmith",
                    Rating = 500,
                    Active = true,
                },
            };
        }

        [Fact]
        public async Task TheReportedCase_CraftWins_AndTheVendorRouteCostsTheCraftPlusLyhrsFee()
        {
            var f = await BuildAsync(ObsidianHeavyBreastplate, 1);
            Assert.NotNull(f.Subtrees);

            var auto = Solve(f);
            long craftCost = auto.Decisions[0].TotalCost.Value;

            Assert.Equal(AcquisitionSource.Craft, auto.Decisions[0].Source);
            Assert.Equal(craftCost, auto.Plan.TotalCoinCost);
            Assert.DoesNotContain(
                auto.Plan.Steps,
                step => step.ItemId == ObsidianHeavyBreastplate &&
                        step.Source == AcquisitionSource.BuyFromVendor);

            // The whole point. Lyhr's offer is the craft recipe plus 10 Globs
            // of Ectoplasm, so taking it must cost exactly the craft plus that
            // fee - not the fee on its own, which is what the module used to
            // report and what made buying look 634x cheaper than crafting.
            var forced = Solve(f, new Dictionary<int, AcquisitionSource> { { 0, AcquisitionSource.BuyFromVendor } });
            var vendorDecision = forced.Decisions[0];

            Assert.Equal(AcquisitionSource.BuyFromVendor, vendorDecision.Source);
            Assert.Equal(craftCost + LyhrFee, vendorDecision.TotalCost);
            Assert.Equal(craftCost + LyhrFee, forced.Plan.TotalCoinCost);
            Assert.True(
                vendorDecision.TotalCost > craftCost,
                "A convenience vendor's offer is the craft plus a fee, so it can never be the cheaper route.");
        }

        [Fact]
        public async Task TheReportedCase_BeforeExpansion_TheVendorRouteReportedTheEctoplasmAlone()
        {
            // The defect itself, pinned so the fix cannot silently come
            // undone: with no cost-line subtrees the solver still prices the
            // offer at its ectoplasm and nothing else, because the four Gifts
            // fold into no coin at all.
            var f = await BuildAsync(ObsidianHeavyBreastplate, 1);

            var forced = Solve(
                f,
                new Dictionary<int, AcquisitionSource> { { 0, AcquisitionSource.BuyFromVendor } },
                withSubtrees: false);

            Assert.Equal(LyhrFee, forced.Decisions[0].TotalCost);

            // And every Gift line reaches the decision as a quantity with no
            // gold value at all - the "cost line is never solved" asymmetry.
            var unpriced = forced.Decisions[0].VendorItemCosts
                .Where(line => !line.GoldValue.HasValue)
                .ToList();
            Assert.Equal(4, unpriced.Count);
        }

        [Fact]
        public async Task EveryCostLineTheSolverPriced_ReportsItsOwnGoldValue()
        {
            // Costing a line and reporting it are the same act: a line folded
            // into the decision's coin total must also carry the figure that
            // was folded, or the tree would show a total no visible row
            // accounts for.
            var f = await BuildAsync(ObsidianHeavyBreastplate, 1);

            var forced = Solve(f, new Dictionary<int, AcquisitionSource> { { 0, AcquisitionSource.BuyFromVendor } });
            var decision = forced.Decisions[0];

            long reportedLineTotal = decision.VendorItemCosts.Sum(line => line.GoldValue ?? 0L);
            Assert.Equal(decision.TotalCost, reportedLineTotal);

            // The Gifts that could be costed are costed; the ectoplasm was
            // always money and stays so.
            Assert.Contains(decision.VendorItemCosts, line => line.ItemId == GlobOfEctoplasm && line.GoldValue == LyhrFee);
            Assert.True(
                decision.VendorItemCosts.Count(line => line.GoldValue.HasValue) > 1,
                "at least one account-bound Gift must now carry a solved acquisition cost");
        }

        [Fact]
        public async Task ComparisonValueNeverOmitsACostTheUserWouldPay()
        {
            // The two-price invariant, over the whole real tree: no committed
            // decision may be compared at a value below the coin it actually
            // commits. ComparisonValue may EXCEED TotalCost (a valued wallet
            // currency is decision-only), never fall short of it.
            var f = await BuildAsync(ObsidianHeavyBreastplate, 1);
            var result = Solve(f);

            foreach (var kvp in result.Decisions)
            {
                var decision = kvp.Value;
                if (!decision.TotalCost.HasValue || !decision.ComparisonValue.HasValue)
                {
                    continue;
                }

                Assert.True(
                    decision.ComparisonValue.Value >= decision.TotalCost.Value,
                    "node " + kvp.Key + " is compared at " + decision.ComparisonValue.Value
                        + " but commits " + decision.TotalCost.Value);
            }
        }

        [Fact]
        public async Task TheSolveTerminates_OverACostLineGraphThatHasCycles()
        {
            // 86094 and 91232 buy each other, among at least twelve cycles in
            // the shipped offer data. Expansion walks that graph, so "it
            // finishes" is the property under test - and it must finish with
            // a real answer, not by having given up on the whole plan.
            var f = await BuildAsync(86094, 1);
            Assert.NotNull(f.Subtrees);
            Assert.True(f.Subtrees.ByItemId.ContainsKey(91232), "the cycle's other half must have been expanded");

            var result = Solve(f);

            Assert.NotEmpty(result.Decisions);
            Assert.NotNull(result.Plan);
        }

        [Fact]
        public async Task LyhrsOffer_IsDominatedByTheRecipe_OnlyWhenTheAccountCanCraftIt()
        {
            var f = await BuildAsync(ObsidianHeavyBreastplate, 1);
            var lyhr = f.Offers[ObsidianHeavyBreastplate].Single();

            // The recipe is Armorsmith 500. A master armorsmith makes the
            // offer a strictly worse copy of a recipe they can already use.
            Assert.True(VendorOfferDomination.IsDominatedByAnyRecipe(
                lyhr,
                f.Tree,
                CraftCompetencyEvaluator.BuildBestRatingByDiscipline(MasterArmorsmith())));

            // Nobody trained: the vendor is the only route there is, and
            // calling it dominated would hide it.
            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(
                lyhr,
                f.Tree,
                CraftCompetencyEvaluator.BuildBestRatingByDiscipline(
                    new List<SnapshotCharacterDiscipline>())));

            // Competency unknown is not competency proven.
            Assert.False(VendorOfferDomination.IsDominatedByAnyRecipe(lyhr, f.Tree, null));
        }

        [Fact]
        public async Task ADominatedOffer_StaysOfferedAndStaysSelectable()
        {
            // Demoted, never dropped: a player already holding the four Gifts
            // still wants to see that Lyhr sells the piece, and the VENDOR
            // pill has to stay clickable.
            var f = await BuildAsync(ObsidianHeavyBreastplate, 1);

            var result = Solve(f, disciplines: MasterArmorsmith());

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.True(result.Decisions[0].CanBuyVendor);
            Assert.True(result.Decisions[0].BuyFromVendorCostBreakdown.IsAvailable);

            var forced = Solve(
                f,
                new Dictionary<int, AcquisitionSource> { { 0, AcquisitionSource.BuyFromVendor } },
                disciplines: MasterArmorsmith());
            Assert.Equal(AcquisitionSource.BuyFromVendor, forced.Decisions[0].Source);
        }

        [Fact]
        public async Task ARestoredPlansValues_ReproduceTheGeneratingSolveWithoutTheSubtrees()
        {
            // What PlanSolveContext snapshots is the resolved values, not the
            // thousands of nodes behind them, so an override re-solve has to
            // reach the same numbers from those values alone.
            var f = await BuildAsync(ObsidianHeavyBreastplate, 1);
            var generated = Solve(f);
            Assert.NotNull(generated.VendorCostLineValues);

            var resolved = new PlanSolver().Solve(
                f.Tree, f.Prices, f.Offers, PriceBasis.InstantBuy,
                vendorCostLineValues: generated.VendorCostLineValues);

            Assert.Equal(generated.Decisions[0].Source, resolved.Decisions[0].Source);
            Assert.Equal(generated.Decisions[0].TotalCost, resolved.Decisions[0].TotalCost);
            Assert.Equal(generated.Plan.TotalCoinCost, resolved.Plan.TotalCoinCost);
        }

        [Fact]
        public async Task NoSubtreeNodeLeaksIntoThePublicDecisions()
        {
            // Cost-line subtrees are numbered from their own sequence and
            // evaluated into their own memo. A subtree node is not a node of
            // the plan, and a NodeId collision would put one in front of the
            // user as though it were.
            var f = await BuildAsync(ObsidianHeavyBreastplate, 1);

            var withSubtrees = Solve(f);
            var without = Solve(f, withSubtrees: false);

            Assert.Equal(without.Decisions.Count, withSubtrees.Decisions.Count);
            Assert.Equal(
                without.Decisions.Keys.OrderBy(k => k),
                withSubtrees.Decisions.Keys.OrderBy(k => k));
        }

        private static HashSet<int> ItemIdsIn(RecipeNode root)
        {
            var ids = new HashSet<int>();
            Walk(root, ids);
            return ids;
        }

        private static void Walk(RecipeNode node, HashSet<int> ids)
        {
            if (node.IngredientType == "Item")
            {
                ids.Add(node.Id);
            }

            foreach (var recipe in node.Recipes)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    Walk(ingredient, ids);
                }
            }
        }

        private static void AddOffers(
            RealCorpusFixture corpus,
            HashSet<int> itemIds,
            Dictionary<int, IReadOnlyList<VendorOffer>> into,
            List<IReadOnlyList<VendorOffer>> alsoInto = null)
        {
            foreach (int id in itemIds)
            {
                if (!corpus.OffersByOutputItem.TryGetValue(id, out var offers))
                {
                    continue;
                }

                into[id] = offers;
                alsoInto?.Add(offers);
            }
        }
    }
}
