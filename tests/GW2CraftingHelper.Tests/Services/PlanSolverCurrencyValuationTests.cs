using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;
using static GW2CraftingHelper.Tests.Helpers.VendorOfferBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverCurrencyValuationTests
    {
        // --- Currency valuation tests ---
        // A user-provided CurrencyValuation makes an offer's non-coin
        // currency lines comparable, but ONLY when every line on the offer
        // has a valuation; the valuation affects comparison only, never the
        // currency amounts reported on the plan.

        [Fact]
        public void ValuedCurrencyOffer_BeatsExpensiveTp_AndPlanListsCurrencyCost()
        {
            // Karma-priced offer (0 coin, 50 karma) with a user valuation of
            // 5 copper/karma (= 250 total) beats a 1000-copper TP price. The
            // plan must still report the real karma amount to pay, not a
            // coin-converted figure.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.Steps[0].TotalCost); // coin part only - offer has no coin cost
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(50, plan.CurrencyCosts[0].Amount);
        }

        [Fact]
        public void UnvaluedCurrencyOffer_WithoutValuation_StaysFallbackOnly()
        {
            // Same offer and prices as the valued-wins test above, but with
            // no valuation supplied at all: pins the existing fallback-only
            // behavior (TP wins; the offer never even enters the comparison).
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(1000, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void ValuedCurrencyOffer_LosesWhenValuedCostExceedsTp()
        {
            // Same karma offer, but its valued cost (250) now exceeds the TP
            // price (100): TP must win outright, and the losing offer must
            // not leak into CurrencyCosts.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(100, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void MixedValuedAndUnvaluedCurrencyOffer_StaysFallbackTier()
        {
            // Offer costs both a valued currency (karma, id 2) and an
            // unvalued one (laurels, id 3). Any unvalued line must keep the
            // WHOLE offer in the fallback tier - it must not become
            // partially comparable. No TP price exists, so the fallback
            // offer is the only acquisition; both currency lines (valued and
            // unvalued alike) must appear in full on the plan.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var offer = new VendorOffer
            {
                OfferId = "test-mixed-valued-and-unvalued",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = 2, Count = 10 },
                    new CostLine { Type = "Currency", Id = 3, Count = 1000 }
                },
                MerchantName = "Mixed Vendor",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.Steps[0].TotalCost);
            Assert.Equal(2, plan.CurrencyCosts.Count);
            Assert.Contains(plan.CurrencyCosts, c => c.CurrencyId == 2 && c.Amount == 10);
            Assert.Contains(plan.CurrencyCosts, c => c.CurrencyId == 3 && c.Amount == 1000);
        }

        [Fact]
        public void MixedValuedAndUnvaluedCurrencyOffer_DoesNotBeatTp()
        {
            // Even a trivially cheap valued line must not make the offer
            // comparable while any other line stays unvalued.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var offer = new VendorOffer
            {
                OfferId = "test-mixed-valued-cheap",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = 2, Count = 1 },
                    new CostLine { Type = "Currency", Id = 3, Count = 1 }
                },
                MerchantName = "Mixed Vendor",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 1 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(100, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void ValuedCurrencyOffer_ForcedOverride_CarriesCurrencyCostsIntoPlan()
        {
            // A per-node override forcing BuyFromVendor on a fully-valued
            // offer must commit the same real coin part + currency lines as
            // the automatic comparison path.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { 0, AcquisitionSource.BuyFromVendor }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, overrides, valuation).Plan;

            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.Steps[0].TotalCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(50, plan.CurrencyCosts[0].Amount);
        }

        // --- Comparison-value laundering regression tests ---
        // A valued vendor offer's coin-equivalent (coin + valued currency)
        // must survive being summed into an ANCESTOR's craft cost. Before
        // the fix, the craft loop summed each ingredient's returned REAL
        // coin cost (e.g. 0 for a karma-only vendor offer) instead of its
        // comparison value (coin + valued currency), so the karma cost was
        // laundered away and an ancestor could wrongly choose to craft
        // through a valued vendor offer that was actually more expensive.

        [Fact]
        public void ValuedVendorDescendant_DoesNotLaunderIntoCraftComparison_TpWinsForAncestor()
        {
            // B (item 2): TP buy 1000, or vendor offer 0 coin + 50 karma
            // (currency 3) valued at 5 copper/unit = 250 comparison value.
            // A (item 1): TP buy 200, or craft from 1x B.
            // Craft-A's true comparison cost is B's comparison value (250),
            // not B's real coin part (0), so TP-buy-A (200) must beat craft.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 200 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 2, new List<VendorOffer> { MixedVendorOffer(2, 0, 3, 50) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 3, 5 } });
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation);

            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[0].Source);
            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
            Assert.Equal(1, result.Plan.Steps[0].ItemId);
            Assert.Equal(200, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void ValuedVendorDescendant_CraftStillWinsWhenGenuinelyCheaper_PlanShowsRealCoinAndCurrency()
        {
            // Same B options as above, but A's TP price (2000) is expensive
            // enough that craft (comparison cost 250) genuinely wins. The
            // committed plan must show the REAL coin cost (0, B's vendor
            // coin part) and the real karma amount (50) - the valuation used
            // to pick this path must never leak into the displayed coin.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 2000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 2, new List<VendorOffer> { MixedVendorOffer(2, 0, 3, 50) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 3, 5 } });
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation);
            var plan = result.Plan;

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Contains(plan.Steps, s => s.Source == AcquisitionSource.Craft && s.ItemId == 1);
            Assert.Contains(plan.Steps, s => s.Source == AcquisitionSource.BuyFromVendor && s.ItemId == 2);

            // Real coin cost only (B's vendor coin part is 0) - the 250
            // comparison value used to pick this path must not appear here.
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(3, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(50, plan.CurrencyCosts[0].Amount);
        }
    }
}
