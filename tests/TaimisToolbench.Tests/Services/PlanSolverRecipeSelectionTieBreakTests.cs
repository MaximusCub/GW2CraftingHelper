using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;
using static TaimisToolbench.Tests.Helpers.RecipeNodeBuilders;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Characterization of PlanSolver's per-tier recipe selection: within
    /// each tier (comparable/fallback, raw/competent) an equal-cost tie
    /// breaks to the LOWEST RecipeId regardless of recipe list order, a
    /// later recipe replaces the incumbent only when STRICTLY cheaper, and
    /// the fallback tier ranks on real coin cost - never the
    /// valuation-carrying comparison cost. Each behavior is pinned by a
    /// test that fails under the corresponding mutation (tie-break
    /// inversion, strictness swap, fallback craftCost-for-craftRealCost
    /// desync).
    /// </summary>
    public class PlanSolverRecipeSelectionTieBreakTests
    {
        private const int ValuedCurrencyId = 50;
        private const int UnvaluedCurrencyId = 60;

        private static CurrencyValuation ValuedAt5PerUnit()
        {
            return new CurrencyValuation(new Dictionary<int, long> { { ValuedCurrencyId, 5 } });
        }

        // Two comparable recipes with EQUAL comparison cost (100) but
        // different real cost: recipe 100 is all-item (real 100); recipe
        // 200 is item 60c + currency valued at 40c (real 60). The winner's
        // RecipeId and TotalCost together prove which recipe won and that
        // its cost fields stayed paired.
        private static RecipeOption ComparableAllItemOption()
        {
            return Option(100, 1, 1, Leaf(2, 1));
        }

        private static RecipeOption ComparableValuedCurrencyOption()
        {
            return Option(200, 1, 1, Leaf(3, 1), Leaf(ValuedCurrencyId, 8, "Currency"));
        }

        private static Dictionary<int, ItemPrice> ComparableTiePrices(int rootBuy)
        {
            return new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = rootBuy } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 60 } },
            };
        }

        [Fact]
        public void ComparableTie_AutoPick_LowestRecipeIdWins_LowerListedFirst()
        {
            var tree = Craftable(1, 1, ComparableAllItemOption(), ComparableValuedCurrencyOption());
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, ComparableTiePrices(500), null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: ValuedAt5PerUnit());

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].RecipeId);
            Assert.Equal(100, result.Decisions[0].TotalCost);
            Assert.Equal(100, result.Decisions[0].ComparisonValue);
        }

        [Fact]
        public void ComparableTie_AutoPick_LowestRecipeIdWins_LowerListedSecond()
        {
            var tree = Craftable(1, 1, ComparableValuedCurrencyOption(), ComparableAllItemOption());
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, ComparableTiePrices(500), null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: ValuedAt5PerUnit());

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].RecipeId);
            Assert.Equal(100, result.Decisions[0].TotalCost);
        }

        // Buying at 10 wins the automatic pick, so a forced-Craft override
        // is what commits - exercising the raw (non-competent) comparable
        // bests, which the override path reads directly.
        [Fact]
        public void ComparableTie_CraftOverride_LowestRecipeIdWins_LowerListedFirst()
        {
            var tree = Craftable(1, 1, ComparableAllItemOption(), ComparableValuedCurrencyOption());
            var overrides = new Dictionary<int, AcquisitionSource> { { 0, AcquisitionSource.Craft } };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, ComparableTiePrices(10), null, PriceBasis.InstantBuy,
                overrides: overrides, currencyValuation: ValuedAt5PerUnit());

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].RecipeId);
            Assert.Equal(100, result.Decisions[0].TotalCost);
            Assert.Equal(100, result.Decisions[0].ComparisonValue);
        }

        [Fact]
        public void ComparableTie_CraftOverride_LowestRecipeIdWins_LowerListedSecond()
        {
            var tree = Craftable(1, 1, ComparableValuedCurrencyOption(), ComparableAllItemOption());
            var overrides = new Dictionary<int, AcquisitionSource> { { 0, AcquisitionSource.Craft } };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, ComparableTiePrices(10), null, PriceBasis.InstantBuy,
                overrides: overrides, currencyValuation: ValuedAt5PerUnit());

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].RecipeId);
            Assert.Equal(100, result.Decisions[0].TotalCost);
        }

        // Two fallback-tier recipes (each carries an unvalued currency)
        // with EQUAL real cost 100, distinguished only by RecipeId.
        private static RecipeOption FallbackOptionA()
        {
            return Option(100, 1, 1, Leaf(2, 1), Leaf(UnvaluedCurrencyId, 3, "Currency"));
        }

        private static RecipeOption FallbackOptionB()
        {
            return Option(200, 1, 1, Leaf(3, 1), Leaf(UnvaluedCurrencyId, 3, "Currency"));
        }

        private static Dictionary<int, ItemPrice> FallbackTiePrices(int? rootBuy)
        {
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 100 } },
            };
            if (rootBuy.HasValue)
            {
                prices[1] = new ItemPrice { ItemId = 1, BuyInstant = rootBuy.Value };
            }

            return prices;
        }

        [Fact]
        public void FallbackTie_AutoPick_LowestRecipeIdWins_LowerListedFirst()
        {
            var tree = Craftable(1, 1, FallbackOptionA(), FallbackOptionB());
            var solver = new PlanSolver();

            var result = solver.Solve(tree, FallbackTiePrices(null));

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].RecipeId);
            Assert.Equal(100, result.Decisions[0].TotalCost);
        }

        [Fact]
        public void FallbackTie_AutoPick_LowestRecipeIdWins_LowerListedSecond()
        {
            var tree = Craftable(1, 1, FallbackOptionB(), FallbackOptionA());
            var solver = new PlanSolver();

            var result = solver.Solve(tree, FallbackTiePrices(null));

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].RecipeId);
            Assert.Equal(100, result.Decisions[0].TotalCost);
        }

        [Fact]
        public void FallbackTie_CraftOverride_LowestRecipeIdWins_LowerListedFirst()
        {
            var tree = Craftable(1, 1, FallbackOptionA(), FallbackOptionB());
            var overrides = new Dictionary<int, AcquisitionSource> { { 0, AcquisitionSource.Craft } };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, FallbackTiePrices(10), null, PriceBasis.InstantBuy, overrides);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].RecipeId);
            Assert.Equal(100, result.Decisions[0].TotalCost);
        }

        [Fact]
        public void FallbackTie_CraftOverride_LowestRecipeIdWins_LowerListedSecond()
        {
            var tree = Craftable(1, 1, FallbackOptionB(), FallbackOptionA());
            var overrides = new Dictionary<int, AcquisitionSource> { { 0, AcquisitionSource.Craft } };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, FallbackTiePrices(10), null, PriceBasis.InstantBuy, overrides);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].RecipeId);
            Assert.Equal(100, result.Decisions[0].TotalCost);
        }

        // Ingredient item 3 force-crafts from item 4 (50c) plus a VALUED
        // currency (50c valuation), committing TotalCost 50 but
        // ComparisonValue 100. A root fallback recipe containing it
        // therefore has craftRealCost 50 but craftCost 100 - the two
        // diverge, so ranking the fallback tier on craftCost instead of
        // craftRealCost flips the winner against a plain real-60 sibling.
        private static RecipeNode DivergentCostFallbackIngredient()
        {
            return Craftable(3, 1,
                Option(300, 1, 1, Leaf(4, 1), Leaf(ValuedCurrencyId, 10, "Currency")));
        }

        [Fact]
        public void FallbackTier_CraftOverride_RanksOnRealCostNotComparisonCost()
        {
            var tree = Craftable(1, 1,
                Option(100, 1, 1,
                    DivergentCostFallbackIngredient(),
                    Leaf(UnvaluedCurrencyId, 3, "Currency")),
                Option(200, 1, 1, Leaf(5, 1), Leaf(UnvaluedCurrencyId, 3, "Currency")));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 10 } },
                { 4, new ItemPrice { ItemId = 4, BuyInstant = 50 } },
                { 5, new ItemPrice { ItemId = 5, BuyInstant = 60 } },
            };
            var overrides = new Dictionary<int, AcquisitionSource> { { 0, AcquisitionSource.Craft } };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy,
                overrides: overrides, currencyValuation: ValuedAt5PerUnit());

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].RecipeId);
            Assert.Equal(50, result.Decisions[0].TotalCost);
        }

        [Fact]
        public void FallbackTier_AutoPick_RanksOnRealCostNotComparisonCost()
        {
            var tree = Craftable(1, 1,
                Option(100, 1, 1,
                    DivergentCostFallbackIngredient(),
                    Leaf(UnvaluedCurrencyId, 3, "Currency")),
                Option(200, 1, 1, Leaf(5, 1), Leaf(UnvaluedCurrencyId, 3, "Currency")));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 4, new ItemPrice { ItemId = 4, BuyInstant = 50 } },
                { 5, new ItemPrice { ItemId = 5, BuyInstant = 60 } },
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy,
                overrides: null, currencyValuation: ValuedAt5PerUnit());

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].RecipeId);
            Assert.Equal(50, result.Decisions[0].TotalCost);
        }
    }
}
