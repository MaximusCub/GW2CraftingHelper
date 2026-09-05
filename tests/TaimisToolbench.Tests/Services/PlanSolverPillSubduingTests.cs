using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;
using static TaimisToolbench.Tests.Helpers.RecipeNodeBuilders;
using static TaimisToolbench.Tests.Helpers.VendorOfferBuilders;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// (redesign,
    /// docs/gw2e-considerations.md): real Solve()-path coverage of
    /// PlanSolver.Evaluate's new per-source PillSourceCostBreakdown
    /// computation (CraftCostBreakdown/BuyFromTpCostBreakdown/
    /// BuyFromVendorCostBreakdown), threaded through CraftingTreeBuilder
    /// into a real CraftingTreeNode and consumed by the real
    /// DecisionPillPlanner.BuildPillSpecs - exercises the full production
    /// pipeline for both subduing rules, not just the
    /// pure PillSubduingEvaluator/DecisionPillPlanner unit coverage.
    /// </summary>
    public class PlanSolverPillSubduingTests
    {
        private static CraftingTreeNode SolveAndBuildRootNode(
            RecipeNode tree, Dictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers = null,
            CurrencyValuation valuation = null)
        {
            var solver = new PlanSolver();
            var solveResult = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation);
            var builder = new CraftingTreeBuilder();
            return builder.BuildTree(tree, solveResult.Decisions, new Dictionary<int, ItemMetadata>());
        }

        [Fact]
        public void AmalgamatedRiftEssenceShape_VendorNeedsMoreRawEcto_StrictlyDominated()
        {
            // The canonical example: crafting needs 5
            // Globs of Ectoplasm (item 100); the vendor trade-in needs 15 -
            // same currencies (both 0 coin), 10 more raw Ecto. Craft wins
            // on real cost (500c vs the vendor's 1500c-folded offer); the
            // VENDOR pill must come back Subdued/StrictDomination WITHOUT
            // any CurrencyValuation at all (needs no valuation - the whole
            // point of this rule).
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(100, 5)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 100, new ItemPrice { ItemId = 100, BuyInstant = 100 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { ItemAndCurrencyVendorOffer(1, new[] { (100, 15) }, null) } },
            };

            var root = SolveAndBuildRootNode(tree, prices, vendorOffers);

            Assert.Equal(CraftingDecision.Craft, root.Decision);
            Assert.Equal(500, root.SubtreeCost);

            Assert.True(root.CraftCostBreakdown.IsAvailable);
            Assert.Equal(0, root.CraftCostBreakdown.RawCoin);
            var craftLine = Assert.Single(root.CraftCostBreakdown.CostLines);
            Assert.Equal("Item", craftLine.Type);
            Assert.Equal(100, craftLine.Id);
            Assert.Equal(5, craftLine.Count);

            Assert.True(root.BuyFromVendorCostBreakdown.IsAvailable);
            Assert.Equal(0, root.BuyFromVendorCostBreakdown.RawCoin);
            var vendorLine = Assert.Single(root.BuyFromVendorCostBreakdown.CostLines);
            Assert.Equal("Item", vendorLine.Type);
            Assert.Equal(100, vendorLine.Id);
            Assert.Equal(15, vendorLine.Count);

            var specs = DecisionPillPlanner.BuildPillSpecs(root);
            var vendorPill = specs.Single(s => s.Text == "VENDOR");
            Assert.Equal(PillKind.Subdued, vendorPill.Kind);
            Assert.Equal(PillSubduingRule.StrictDomination, vendorPill.SubduingResult.Rule);
            var delta = Assert.Single(vendorPill.SubduingResult.Deltas);
            Assert.Equal("Item", delta.Kind);
            Assert.Equal(100, delta.Id);
            Assert.Equal(10, delta.Amount);

            var craftPill = specs.Single(s => s.Text == "CRAFT");
            Assert.Equal(PillKind.Selected, craftPill.Kind);
        }

        [Fact]
        public void WeightedValuation_VendorCheaperInRawCoinButPricierWhenValued_Subdued()
        {
            // TP buy (500 coin, selected) vs a karma-only vendor offer
            // (0 coin, so NOT dominated on raw coin - the vendor needs
            // LESS coin) that is nonetheless pricier once the user's own
            // karma valuation (1 copper/karma * 1000 = 1000c) is folded in.
            // This isolates the Weighted path: StrictDomination must NOT
            // fire (vendor's raw coin is strictly better, not worse), so
            // only the fully-valued decision-value comparison can subdue
            // the vendor pill.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 77, 1000) } },
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 77, 1 } });

            var root = SolveAndBuildRootNode(tree, prices, vendorOffers, valuation);

            Assert.Equal(CraftingDecision.BuyFromTp, root.Decision);
            Assert.Equal(500, root.SubtreeCost);

            var specs = DecisionPillPlanner.BuildPillSpecs(root);
            var vendorPill = specs.Single(s => s.Text == "VENDOR");
            Assert.Equal(PillKind.Subdued, vendorPill.Kind);
            Assert.Equal(PillSubduingRule.Weighted, vendorPill.SubduingResult.Rule);
            Assert.Equal(500, vendorPill.SubduingResult.ValueMarginCopper);

            var tpPill = specs.Single(s => s.Text == "TP");
            Assert.Equal(PillKind.Selected, tpPill.Kind);
        }

        [Fact]
        public void WeightedCraftLosing_PureGoldNoValuation_HasNonCoinCostFalse()
        {
            // Regression: TP
            // (400c, selected) vs a craft recipe (losing) that consumes 5x
            // item 100 at 100c each (500c) - pure gold on both sides, NO
            // CurrencyValuation anywhere. BuildCraftCostBreakdown still
            // emits an "Item" CostLine for the ingredient (every craft
            // breakdown has one), so this isolates the round-1 regression:
            // HasNonCoinCost must be false (and the tooltip must NOT say
            // "at your current currency values") because that Item line
            // was never priced by a user valuation - only a Type ==
            // "Currency" line could ever have been.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(100, 5)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 400 } },
                { 100, new ItemPrice { ItemId = 100, BuyInstant = 100 } },
            };

            var root = SolveAndBuildRootNode(tree, prices);

            Assert.Equal(CraftingDecision.BuyFromTp, root.Decision);
            Assert.Equal(400, root.SubtreeCost);

            Assert.True(root.CraftCostBreakdown.IsAvailable);
            Assert.Equal(500, root.CraftCostBreakdown.DecisionValue);
            var craftLine = Assert.Single(root.CraftCostBreakdown.CostLines);
            Assert.Equal("Item", craftLine.Type);

            var specs = DecisionPillPlanner.BuildPillSpecs(root);
            var craftPill = specs.Single(s => s.Text == "CRAFT");
            Assert.Equal(PillKind.Subdued, craftPill.Kind);
            Assert.Equal(PillSubduingRule.Weighted, craftPill.SubduingResult.Rule);
            Assert.Equal(100, craftPill.SubduingResult.ValueMarginCopper);
            Assert.False(craftPill.SubduingResult.HasNonCoinCost);

            var tooltip = PillSubduingTooltipBuilder.BuildContent(
                craftPill.SubduingResult,
                new Dictionary<int, ItemMetadata>(),
                new Dictionary<int, CurrencyMetadata>());
            Assert.DoesNotContain("currency values", tooltip.ToPlainText());

            var tpPill = specs.Single(s => s.Text == "TP");
            Assert.Equal(PillKind.Selected, tpPill.Kind);
        }

        [Fact]
        public void UnvaluedNonDominatedAlternative_StaysAvailable_NotSubdued()
        {
            // A vendor offer priced ONLY in an unvalued currency (no
            // CurrencyValuation supplied) is fallback-tier - unvalued and,
            // since it needs LESS raw coin than TP (0 vs 500), not
            // dominated either. Per the spec, both pills stay normal.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 77, 1000) } },
            };

            var root = SolveAndBuildRootNode(tree, prices, vendorOffers);

            Assert.Equal(CraftingDecision.BuyFromTp, root.Decision);

            var specs = DecisionPillPlanner.BuildPillSpecs(root);
            var vendorPill = specs.Single(s => s.Text == "VENDOR");
            Assert.Equal(PillKind.Available, vendorPill.Kind);
            Assert.Null(vendorPill.SubduingResult);

            var tpPill = specs.Single(s => s.Text == "TP");
            Assert.Equal(PillKind.Selected, tpPill.Kind);
        }
    }
}
