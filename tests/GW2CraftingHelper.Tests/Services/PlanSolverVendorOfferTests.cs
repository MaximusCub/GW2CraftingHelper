using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;
using static GW2CraftingHelper.Tests.Helpers.VendorOfferBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverVendorOfferTests
    {
        // --- VendorCurrencyCosts threading tests (M33 spec item 5) ---

        [Fact]
        public void VendorCurrencyCosts_ThreadedOntoSolverDecisionAndPlanStep()
        {
            var tree = Leaf(1, 2);
            var prices = new Dictionary<int, ItemPrice>();
            var offer = new VendorOffer
            {
                OfferId = "test-currency-thread",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 10 },
                    new CostLine { Type = "Currency", Id = 23, Count = 50 }
                },
                MerchantName = "Miyani",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);

            Assert.Equal(AcquisitionSource.BuyFromVendor, result.Decisions[0].Source);
            Assert.NotNull(result.Decisions[0].VendorCurrencyCosts);
            Assert.Single(result.Decisions[0].VendorCurrencyCosts);
            Assert.Equal(23, result.Decisions[0].VendorCurrencyCosts[0].Id);
            Assert.Equal(100, result.Decisions[0].VendorCurrencyCosts[0].Count); // 50/unit * qty 2

            var step = result.Plan.Steps.Single(s => s.ItemId == 1);
            Assert.NotNull(step.VendorCurrencyCosts);
            Assert.Single(step.VendorCurrencyCosts);
            Assert.Equal(23, step.VendorCurrencyCosts[0].Id);
            Assert.Equal(100, step.VendorCurrencyCosts[0].Count);
        }

        [Fact]
        public void VendorCurrencyCosts_MergedAcrossDeduplicatedOccurrences()
        {
            // Same vendor-sourced item reached via two tree branches must
            // sum its currency cost into the single aggregated PlanStep row,
            // not just the last-seen occurrence's amount.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 1),
                    Craftable(3, 1,
                        Option(20, 1, 1,
                            Leaf(2, 1)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 100000 } }
            };
            var offer = new VendorOffer
            {
                OfferId = "test-dedup-currency",
                OutputItemId = 2,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = 23, Count = 10 }
                },
                MerchantName = "Miyani",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 2, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            var item2Steps = plan.Steps.Where(s => s.ItemId == 2).ToList();
            Assert.Single(item2Steps); // deduplicated into one row
            Assert.NotNull(item2Steps[0].VendorCurrencyCosts);
            Assert.Single(item2Steps[0].VendorCurrencyCosts);
            Assert.Equal(23, item2Steps[0].VendorCurrencyCosts[0].Id);
            Assert.Equal(20, item2Steps[0].VendorCurrencyCosts[0].Count); // 10 + 10 across both occurrences
        }

        [Fact]
        public void VendorCurrencyCosts_MergeOverflow_ClampsRatherThanWraps()
        {
            // Two occurrences of the same vendor-sourced item, each with a
            // currency count near int.MaxValue, sum past int.MaxValue -
            // must clamp, not silently wrap to a negative/garbage count.
            const int nearMax = 1_200_000_000;
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 1),
                    Craftable(3, 1,
                        Option(20, 1, 1,
                            Leaf(2, 1)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 100000 } }
            };
            var offer = new VendorOffer
            {
                OfferId = "test-overflow-currency",
                OutputItemId = 2,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = 23, Count = nearMax }
                },
                MerchantName = "Miyani",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 2, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            var item2Step = plan.Steps.Single(s => s.ItemId == 2);
            Assert.Equal(int.MaxValue, item2Step.VendorCurrencyCosts[0].Count);
        }

        // --- Backward-compat regression tests ---

        [Fact]
        public void ExistingLeafBuyFromTp_WithNullVendorOffers_Unchanged()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, null).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(500, plan.TotalCoinCost);
        }

        [Fact]
        public void ExistingCraftCheaper_WithEmptyVendorOffers_Unchanged()
        {
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>();
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Equal(2, plan.Steps.Count);
            Assert.Contains(plan.Steps, s => s.Source == AcquisitionSource.Craft && s.ItemId == 1);
            Assert.Equal(200, plan.TotalCoinCost);
        }

        // --- Vendor offer tests ---

        [Fact]
        public void VendorCheaperThanTpAndCraft_ChoosesVendor()
        {
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 400 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 200) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(200, plan.Steps[0].TotalCost);
            Assert.Equal(200, plan.TotalCoinCost);
        }

        [Fact]
        public void VendorMoreExpensiveThanTp_ChoosesTp()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 200 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 500) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(200, plan.TotalCoinCost);
        }

        [Fact]
        public void VendorWithCurrencyCost_TracksCurrencyInPlan()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var offer = new VendorOffer
            {
                OfferId = "test-mixed",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 100 },
                    new CostLine { Type = "Currency", Id = 2, Count = 50 }
                },
                MerchantName = "Karma Vendor",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(100, plan.Steps[0].TotalCost);
            Assert.Equal(100, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(50, plan.CurrencyCosts[0].Amount);
        }

        [Fact]
        public void VendorOnlyOption_NoTpNoCraft_ChoosesVendor()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 300) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(300, plan.TotalCoinCost);
        }

        [Fact]
        public void MultipleVendorOffers_PicksCheapest()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1, new List<VendorOffer>
                    {
                        CoinVendorOffer(1, 500),
                        CoinVendorOffer(1, 100)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(100, plan.TotalCoinCost);
        }

        [Fact]
        public void VendorOfferWithItemCosts_PricesViaTP()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 200 } },
                { 42, new ItemPrice { ItemId = 42, BuyInstant = 10 } }
            };
            var offer = new VendorOffer
            {
                OfferId = "test-item-cost",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Item", Id = 42, Count = 5 }
                },
                MerchantName = "Barter Vendor",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            // Vendor cost = 5 * 10 = 50, TP buy = 200 -> vendor wins
            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);
        }

        [Fact]
        public void VendorOfferWithOutputCountGreaterThanOne_ScalesCorrectly()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>();
            // Vendor sells 2 for 100 coin each batch -> need ceil(5/2)=3 batches = 300
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 100, outputCount: 2) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(300, plan.TotalCoinCost);
        }
    }
}
