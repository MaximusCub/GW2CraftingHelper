using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// guildupgrade-ingredients fix (docs/KNOWN-ISSUES.md): the versioned
    /// GW2 API returns ingredient {type:"GuildUpgrade", id:&lt;upgradeId&gt;,
    /// count:N} on Guild Decoration recipes (e.g. recipe 12002 -> item
    /// 80471, guild upgrade id 829, ref/recipes_seed.json). Before this fix,
    /// PlanSolver.Evaluate's ingredient loop only special-cased
    /// IngredientType == "Currency", so a GuildUpgrade ingredient fell
    /// through to the item-pricing path and was priced as whatever TP ITEM
    /// happened to share that numeric id - wrong domain, wrong cost. These
    /// tests exercise the real PlanSolver.Solve() entry point (not a
    /// contract mirror) and prove a GuildUpgrade ingredient is NEVER priced
    /// as an item or a currency, even when a coincidentally-matching TP
    /// price or CurrencyValuation entry exists for the exact same numeric
    /// id - the domain-collision case Gw2Constants.KnownCurrencyNames' own
    /// doc comment documents as real in the current seed.
    /// </summary>
    public class PlanSolverGuildUpgradeTests
    {
        [Fact]
        public void GuildUpgradeIngredient_NeverPricedAsItem_EvenWhenTpPriceExistsForSameId()
        {
            // Root (item 1) crafts from 1x item 2 (TP price 100) + 5x
            // GuildUpgrade id 829. A TP price is deliberately seeded for id
            // 829 too - the exact "shares that numeric id" collision the
            // pre-fix bug exploited. If the fix regressed, the root's craft
            // cost would include 829's bogus 99999-copper "price".
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 1, "Item"),
                    Leaf(829, 5, "GuildUpgrade")));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
                { 829, new ItemPrice { ItemId = 829, BuyInstant = 99999 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, null);

            // Only source available for the root (no root TP price, no
            // vendor offer) is the fallback-tier craft - same machinery an
            // unvalued real Currency ingredient already uses (M33's
            // "recipe stays feasible, just never wins the automatic
            // coin-comparable comparison" guarantee).
            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.True(result.Decisions[0].CanCraft);
            // Real cost is item 2's price ALONE - the GuildUpgrade
            // ingredient must contribute exactly zero, never 829's TP price.
            Assert.Equal(100, result.Decisions[0].TotalCost);

            Assert.Equal(2, result.Plan.Steps.Count);
            Assert.Contains(result.Plan.Steps, s =>
                s.Source == AcquisitionSource.Craft && s.ItemId == 1 && s.TotalCost == 100);
            Assert.Contains(result.Plan.Steps, s =>
                s.Source == AcquisitionSource.BuyFromTp && s.ItemId == 2 && s.TotalCost == 100);
            // The GuildUpgrade ingredient must never generate its own
            // shopping-list row (it is not a purchasable item).
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 829);
        }

        [Fact]
        public void GuildUpgradeIngredient_NeverPricedAsCurrency_EvenWhenValuationExistsForSameId()
        {
            // Same tree as above, but this time a CurrencyValuation entry
            // exists for id 829 (5 copper/unit - would add 5 * 5 = 25
            // copper if wrongly consulted). The fix must never even look at
            // currencyValuation for a GuildUpgrade ingredient - the id
            // spaces are unrelated domains that merely happen to collide
            // numerically here.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 1, "Item"),
                    Leaf(829, 5, "GuildUpgrade")));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 829, 5 } });
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, null, PriceBasis.InstantBuy, null, valuation);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(100, result.Decisions[0].TotalCost); // not 125
        }

        [Fact]
        public void GuildUpgradeIngredient_NeverAppearsInPlanCurrencyCosts()
        {
            // W4A sweep: a GuildUpgrade ingredient must never surface in
            // plan.CurrencyCosts (the Summary currency table's data source)
            // or any wallet-lookup-keyed display, unlike a real Currency
            // ingredient (which correctly does appear there).
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 1, "Item"),
                    Leaf(829, 5, "GuildUpgrade")));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, null);

            Assert.Empty(result.Plan.CurrencyCosts);
        }

        [Fact]
        public void GuildUpgradeIngredient_TransitivelyDemotesAncestorCraft_TpWinsDespiteCheaperRealCraftCost()
        {
            // Mirrors PlanSolverCurrencyValuationTests.
            // ValuedVendorDescendant_DoesNotLaunderIntoCraftComparison_TpWinsForAncestor,
            // proving the SAME transitive-fallback-tier propagation
            // machinery (Decision.HasUnvaluedCurrency, PlanSolver.Evaluate's
            // recipe loop) already handles a GuildUpgrade-tainted
            // descendant with no extra code: item 3 (TP 50) is a
            // GuildUpgrade-only recipe level down from item 2, which is one
            // level down from root item 1. Root's own TP price (200) is
            // MORE than item 2's real craft cost (50) - if the nested
            // GuildUpgrade taint were NOT propagating correctly, craft would
            // wrongly look "comparable" and win outright (50 < 200); the
            // fix must keep the whole chain fallback-tier, so TP (the only
            // COMPARABLE option) wins instead.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Craftable(2, 1,
                        Option(20, 1, 1,
                            Leaf(3, 1, "Item"),
                            Leaf(829, 1, "GuildUpgrade")))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 200 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 50 } }
                // Deliberately no price for item 2 - its only route is craft.
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, null);

            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[0].Source);
            Assert.Equal(200, result.Decisions[0].TotalCost);
            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
            Assert.Equal(1, result.Plan.Steps[0].ItemId);
            Assert.Equal(200, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void GuildUpgradeOnlyRecipe_NoOtherIngredients_StillCraftsAtZeroCost()
        {
            // Degenerate but real shape: a recipe whose only ingredient is
            // a GuildUpgrade requirement (no priceable component at all)
            // must still resolve to a feasible, zero-cost Craft - never
            // throw, never fall to UnknownSource just because its sole
            // ingredient is unpriceable (mirrors the M33 "hasComponents"
            // guarantee for an all-unvalued-currency recipe).
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(829, 1, "GuildUpgrade")));
            var prices = new Dictionary<int, ItemPrice>();
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, null);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.True(result.Decisions[0].CanCraft);
            Assert.Equal(0, result.Decisions[0].TotalCost);
            Assert.Empty(result.Plan.CurrencyCosts);
        }
    }
}
