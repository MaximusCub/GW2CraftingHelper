using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;
using static TaimisToolbench.Tests.Helpers.RecipeNodeBuilders;
using static TaimisToolbench.Tests.Helpers.VendorOfferBuilders;

namespace TaimisToolbench.Tests.Services
{
    public class PlanSolverVendorCapTests
    {
        // --- Vendor purchase-cap tests ---
        // V2 semantics: a DailyCap/WeeklyCap
        // NEVER excludes an offer or re-routes the solver to a different
        // source - gw2efficiency itself only ever surfaces a cap as a
        // post-solve "this is timegated" notice, never a tree change. A
        // cap-exceeding offer is still used exactly like an uncapped one;
        // the only observable effect is a CraftingPlan.TimegatedItems entry.
        [Fact]
        public void CappedOffer_NeededExceedsCap_StillUsedAsVendor_SurfacesTimegatedNotice()
        {
            // Vendor sells for 1 coin each but only 25/day; node needs 50,
            // exceeding one day's cap. The far cheaper vendor offer (50
            // coin) is still used over the expensive TP price (500 coin) -
            // caps never re-route the solver - and the plan surfaces a
            // timegated notice instead of silently falling back.
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 10 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, dailyCap: 25) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(1, notice.ItemId);
            Assert.Equal(TimegatedCapType.Daily, notice.CapType);
            Assert.Equal(25, notice.CapValue);
            Assert.Equal(50, notice.NeededCount);
        }

        [Fact]
        public void CappedOffer_NeededWithinCap_StillUsedAsVendor()
        {
            // Needed (20) is within the cap (25); the far cheaper vendor
            // offer must still be picked over the expensive TP price, and
            // no timegated notice is raised since the cap is not exceeded.
            var tree = Leaf(1, 20);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 5, dailyCap: 25) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(100, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void CappedBatchOffer_CapTimesOutputCountArithmetic()
        {
            // Offer sells batches of 10 with a cap of 3 purchases/day (max
            // 30 units/day). Needing 25 units requires only 3 purchases
            // (ceil(25/10)), which fits the cap even though 25 itself is far
            // greater than the raw DailyCap of 3 - proving OutputCount is
            // correctly folded into the cap check (no timegated notice)
            // rather than comparing the node's raw quantity against the cap.
            var tree = Leaf(1, 25);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 5, outputCount: 10, dailyCap: 3) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(15, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void CappedBatchOffer_OneMoreUnitPushesPastCap_StillUsedAsVendor_SurfacesTimegatedNotice()
        {
            // Same batch/cap shape as above (10/batch, cap 3 => 30/day), but
            // needing 31 units requires 4 purchases (ceil(31/10)), exceeding
            // the cap. With no TP price and no recipe, the offer is still
            // the only (and therefore chosen) source - caps never exclude -
            // and the plan surfaces a timegated notice for it.
            var tree = Leaf(1, 31);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 5, outputCount: 10, dailyCap: 3) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(20, plan.TotalCoinCost);

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(1, notice.ItemId);
            Assert.Equal(TimegatedCapType.Daily, notice.CapType);
            Assert.Equal(3, notice.CapValue);
            Assert.Equal(4, notice.NeededCount);
        }

        [Fact]
        public void ZeroCap_TreatedAsUncapped()
        {
            // An explicit DailyCap of 0 (not merely absent) must still mean
            // uncapped, not "zero purchases allowed" - no timegated notice.
            var tree = Leaf(1, 500);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, dailyCap: 0) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(500, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void WeeklyCapUsed_WhenDailyCapAbsent()
        {
            // No DailyCap set; WeeklyCap of 25 cannot cover the 50 needed.
            // The offer is still used (far cheaper than TP) and surfaces a
            // Weekly-typed timegated notice.
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 10 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, weeklyCap: 25) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(TimegatedCapType.Weekly, notice.CapType);
            Assert.Equal(25, notice.CapValue);
            Assert.Equal(50, notice.NeededCount);
        }

        [Fact]
        public void DailyCapTakesPrecedenceOverWeeklyCap()
        {
            // DailyCap (100) alone covers the 50 needed, so no notice is
            // raised even though the WeeklyCap (1) alone would have been
            // exceeded - DailyCap wins whenever it is positive.
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, dailyCap: 100, weeklyCap: 1) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void CappedMixedCurrencyOffer_NeededExceedsCap_StillUsedAsFallback_SurfacesTimegatedNotice()
        {
            // A mixed-currency offer only ever competes in the fallback
            // tier (its non-coin currency line is unvalued). With no TP
            // price and no recipe, it remains the only source even though
            // needing 50 exceeds the cap of 10 - caps never exclude - and a
            // timegated notice is raised for it.
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50, dailyCap: 10) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(2500, plan.CurrencyCosts[0].Amount);

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(1, notice.ItemId);
            Assert.Equal(TimegatedCapType.Daily, notice.CapType);
            Assert.Equal(10, notice.CapValue);
            Assert.Equal(50, notice.NeededCount);
        }

        [Fact]
        public void CappedMixedCurrencyOffer_NeededWithinCap_StillUsedAsFallback()
        {
            // Needed (5) is within the cap (10); the mixed-currency offer
            // remains the fallback acquisition (no TP price, no recipe), and
            // no timegated notice is raised.
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50, dailyCap: 10) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(250, plan.CurrencyCosts[0].Amount);
            Assert.Empty(plan.TimegatedItems);
        }

        // --- Seasonal (Astral Acclaim package, KNOWN-ISSUES #33) vendor
        // purchase-cap tests ---
        // Same warn-only semantics as Daily/Weekly above (a cap never gates
        // offer eligibility or re-routes the solver), but checked
        // INDEPENDENTLY of Daily/Weekly rather than folded into the same
        // "pick one" precedence - see the SeasonalAndWeeklyCap test below.
        [Fact]
        public void SeasonalCap_NeededExceedsCap_StillUsedAsVendor_SurfacesTimegatedNotice()
        {
            // Vendor sells for 1 coin each but only 20/season; node needs
            // 25, exceeding the season's cap. The far cheaper vendor offer
            // (25 coin) is still used over the expensive TP price (10000
            // coin) - caps never re-route the solver - and the plan
            // surfaces a Seasonal-typed timegated notice instead of
            // silently falling back.
            var tree = Leaf(1, 25);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 400 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, seasonalCap: 20) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(25, plan.TotalCoinCost);

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(1, notice.ItemId);
            Assert.Equal(TimegatedCapType.Seasonal, notice.CapType);
            Assert.Equal(20, notice.CapValue);
            Assert.Equal(25, notice.NeededCount);
        }

        [Fact]
        public void SeasonalCap_NeededWithinCap_StillUsedAsVendor_NoNotice()
        {
            // Needed (10) is within the season cap (20); the far cheaper
            // vendor offer must still be picked over the expensive TP
            // price, and no timegated notice is raised since the cap is
            // not exceeded.
            var tree = Leaf(1, 10);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 5, seasonalCap: 20) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void SeasonalCapZero_TreatedAsUncapped()
        {
            // An explicit SeasonalCap of 0 (not merely absent) must still
            // mean uncapped, not "zero purchases allowed" - matching the
            // DailyCap/WeeklyCap zero-cap convention exactly.
            var tree = Leaf(1, 500);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, seasonalCap: 0) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(500, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void SeasonalAndWeeklyCap_BothExceeded_BothNoticesReported()
        {
            // A single offer carrying BOTH a WeeklyCap and a SeasonalCap
            // must surface BOTH notices when both are exceeded - Seasonal
            // is checked independently of Daily/Weekly (a separate,
            // unrelated real-world limit), unlike Daily's precedence over
            // Weekly which suppresses one notice in favor of the other on
            // that SAME axis (see DailyCapTakesPrecedenceOverWeeklyCap).
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, weeklyCap: 10, seasonalCap: 20) } },
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);

            Assert.Equal(2, plan.TimegatedItems.Count);
            var weeklyNotice = Assert.Single(plan.TimegatedItems, t => t.CapType == TimegatedCapType.Weekly);
            Assert.Equal(1, weeklyNotice.ItemId);
            Assert.Equal(10, weeklyNotice.CapValue);
            Assert.Equal(50, weeklyNotice.NeededCount);
            var seasonalNotice = Assert.Single(plan.TimegatedItems, t => t.CapType == TimegatedCapType.Seasonal);
            Assert.Equal(1, seasonalNotice.ItemId);
            Assert.Equal(20, seasonalNotice.CapValue);
            Assert.Equal(50, seasonalNotice.NeededCount);
        }

        [Fact]
        public void SeasonalCap_NeverChangesDecisionOrTotalCost_Regression()
        {
            // Regression guard (mirrors the existing Daily/Weekly cap-
            // never-reroutes tests): an exceeded SeasonalCap must not alter
            // the solver's Source choice, TotalCost, or the per-node
            // Decision - purely an informational notice layered on top of
            // an otherwise-unchanged solve.
            var tree = Leaf(1, 30);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 2, seasonalCap: 5) } },
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);
            var plan = result.Plan;

            // The vendor offer (2 coin each = 60 total) is still far
            // cheaper than TP (1000 each) and remains the chosen source
            // despite the exceeded SeasonalCap (need 30, cap 5).
            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(60, plan.Steps[0].TotalCost);
            Assert.Equal(60, plan.TotalCoinCost);
            Assert.Equal(60, result.Decisions[tree.NodeId].TotalCost);
            Assert.NotEmpty(plan.TimegatedItems);
        }

        [Fact]
        public void SeasonalCappedCurrencyOffer_ValuedCurrency_BeatsExpensiveTp_SurfacesTimegatedNotice()
        {
            // HONESTY NOTE: in live data, Wizard's Vault offers are priced
            // in unvalued Astral Acclaim, so the solver only ever selects
            // one (and therefore only ever fires this notice) when the
            // user has supplied a CurrencyValuation for that currency.
            // This exercises that real path through the actual comparable-
            // tier pipeline: a currency-priced (not coin-priced) offer,
            // chosen over TP because the user values the currency, whose
            // SeasonalCap the merged demand exceeds.
            var tree = Leaf(1, 25);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 9, seasonalCap: 20) } },
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(225, plan.CurrencyCosts[0].Amount); // real currency amount: 9 * 25, unaffected by valuation

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(1, notice.ItemId);
            Assert.Equal(TimegatedCapType.Seasonal, notice.CapType);
            Assert.Equal(20, notice.CapValue);
            Assert.Equal(25, notice.NeededCount);
        }
    }
}
