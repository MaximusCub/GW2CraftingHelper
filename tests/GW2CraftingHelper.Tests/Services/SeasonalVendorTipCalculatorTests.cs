using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// opportunity-notes (SEASONAL VENDOR TIP) - direct unit tests on
    /// SeasonalVendorTipCalculator's pure step-walk, using plain PlanStep/
    /// VendorOffer fixtures (no Blish reference, no solver/pipeline
    /// round-trip needed).
    /// </summary>
    public class SeasonalVendorTipCalculatorTests
    {
        private static CraftingPlanResult MakeResult(params PlanStep[] steps)
        {
            return new CraftingPlanResult
            {
                Plan = new CraftingPlan { Steps = new List<PlanStep>(steps) }
            };
        }

        private static VendorOffer HalloweenOffer(int outputItemId, int outputCount, int itemCostId, int itemCostCount, string merchant = "Candy Corn Vendor (Weekly)", int? weeklyCap = null)
        {
            return new VendorOffer
            {
                OfferId = "hall-" + outputItemId,
                OutputItemId = outputItemId,
                OutputCount = outputCount,
                CostLines = new List<CostLine> { new CostLine { Type = "Item", Id = itemCostId, Count = itemCostCount } },
                MerchantName = merchant,
                SeasonalFestival = Gw2Constants.HalloweenFestivalName,
                WeeklyCap = weeklyCap
            };
        }

        [Fact]
        public void ActiveFestival_CheaperOffer_EmitsTip()
        {
            var step = new PlanStep { ItemId = 19721, Quantity = 10, UnitCost = 100, Source = AcquisitionSource.BuyFromTp };
            var result = MakeResult(step);
            // Offer: 1x item 999 for 5x ecto -> unit cost basis needs a
            // price for item 999.
            var offer = HalloweenOffer(19721, 5, 999, 1, weeklyCap: 1);
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 19721, new List<VendorOffer> { offer } }
            };
            var prices = new Dictionary<int, ItemPrice> { { 999, new ItemPrice { SellInstant = 50 } } };

            SeasonalVendorTipCalculator.Apply(
                result, vendorOffers, prices, PriceBasis.BuyOrder,
                new List<string> { Gw2Constants.HalloweenFestivalName });

            var tip = Assert.Single(result.SeasonalVendorTips);
            Assert.Equal(19721, tip.ItemId);
            Assert.Equal(Gw2Constants.HalloweenFestivalName, tip.Festival);
            Assert.Equal("Candy Corn Vendor (Weekly)", tip.MerchantName);
            Assert.Equal(10, tip.OfferUnitCost); // 50 / 5
            Assert.Equal(100, tip.PlanUnitPrice);
            Assert.Equal(1, tip.WeeklyCap);
        }

        [Fact]
        public void FestivalNotActive_NoTip()
        {
            var step = new PlanStep { ItemId = 19721, Quantity = 10, UnitCost = 100, Source = AcquisitionSource.BuyFromTp };
            var result = MakeResult(step);
            var offer = HalloweenOffer(19721, 5, 999, 1);
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>> { { 19721, new List<VendorOffer> { offer } } };
            var prices = new Dictionary<int, ItemPrice> { { 999, new ItemPrice { SellInstant = 50 } } };

            SeasonalVendorTipCalculator.Apply(result, vendorOffers, prices, PriceBasis.BuyOrder, new List<string>());

            Assert.Empty(result.SeasonalVendorTips);
        }

        [Fact]
        public void EmptyActiveFestivalList_NoTip()
        {
            var step = new PlanStep { ItemId = 19721, Quantity = 10, UnitCost = 100, Source = AcquisitionSource.BuyFromTp };
            var result = MakeResult(step);
            var offer = HalloweenOffer(19721, 5, 999, 1);
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>> { { 19721, new List<VendorOffer> { offer } } };
            var prices = new Dictionary<int, ItemPrice> { { 999, new ItemPrice { SellInstant = 50 } } };

            SeasonalVendorTipCalculator.Apply(result, vendorOffers, prices, PriceBasis.BuyOrder, null);

            Assert.Empty(result.SeasonalVendorTips);
        }

        [Fact]
        public void OfferNotCheaperThanPlan_NoTip()
        {
            var step = new PlanStep { ItemId = 19721, Quantity = 10, UnitCost = 5, Source = AcquisitionSource.BuyFromTp };
            var result = MakeResult(step);
            var offer = HalloweenOffer(19721, 5, 999, 1); // offer unit cost = 10, plan is already cheaper at 5
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>> { { 19721, new List<VendorOffer> { offer } } };
            var prices = new Dictionary<int, ItemPrice> { { 999, new ItemPrice { SellInstant = 50 } } };

            SeasonalVendorTipCalculator.Apply(
                result, vendorOffers, prices, PriceBasis.BuyOrder,
                new List<string> { Gw2Constants.HalloweenFestivalName });

            Assert.Empty(result.SeasonalVendorTips);
        }

        [Fact]
        public void NonSeasonalOffer_NeverConsidered()
        {
            var step = new PlanStep { ItemId = 19721, Quantity = 10, UnitCost = 1000, Source = AcquisitionSource.BuyFromTp };
            var result = MakeResult(step);
            var regularOffer = new VendorOffer
            {
                OfferId = "regular", OutputItemId = 19721, OutputCount = 5,
                CostLines = new List<CostLine> { new CostLine { Type = "Item", Id = 999, Count = 1 } },
                MerchantName = "Regular Vendor", SeasonalFestival = null
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>> { { 19721, new List<VendorOffer> { regularOffer } } };
            var prices = new Dictionary<int, ItemPrice> { { 999, new ItemPrice { SellInstant = 50 } } };

            SeasonalVendorTipCalculator.Apply(
                result, vendorOffers, prices, PriceBasis.BuyOrder,
                new List<string> { Gw2Constants.HalloweenFestivalName });

            Assert.Empty(result.SeasonalVendorTips);
        }

        [Fact]
        public void UnpricedCostLine_NoTip()
        {
            var step = new PlanStep { ItemId = 19721, Quantity = 10, UnitCost = 100, Source = AcquisitionSource.BuyFromTp };
            var result = MakeResult(step);
            var offer = HalloweenOffer(19721, 5, 999, 1);
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>> { { 19721, new List<VendorOffer> { offer } } };

            SeasonalVendorTipCalculator.Apply(
                result, vendorOffers, new Dictionary<int, ItemPrice>(), PriceBasis.BuyOrder,
                new List<string> { Gw2Constants.HalloweenFestivalName });

            Assert.Empty(result.SeasonalVendorTips);
        }

        [Fact]
        public void StepWithVendorCurrencyCosts_NotComparable_NoTip()
        {
            var step = new PlanStep
            {
                ItemId = 19721, Quantity = 10, UnitCost = 100, Source = AcquisitionSource.BuyFromVendor,
                VendorCurrencyCosts = new List<CostLine> { new CostLine { Type = "Currency", Id = 2, Count = 5 } }
            };
            var result = MakeResult(step);
            var offer = HalloweenOffer(19721, 5, 999, 1);
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>> { { 19721, new List<VendorOffer> { offer } } };
            var prices = new Dictionary<int, ItemPrice> { { 999, new ItemPrice { SellInstant = 50 } } };

            SeasonalVendorTipCalculator.Apply(
                result, vendorOffers, prices, PriceBasis.BuyOrder,
                new List<string> { Gw2Constants.HalloweenFestivalName });

            Assert.Empty(result.SeasonalVendorTips);
        }

        [Fact]
        public void NullResult_NoOp()
        {
            SeasonalVendorTipCalculator.Apply(null, null, null, PriceBasis.BuyOrder, null);
        }

        [Fact]
        public void NoSteps_EmptyTipsListNotNull()
        {
            var result = new CraftingPlanResult { Plan = new CraftingPlan { Steps = new List<PlanStep>() } };

            SeasonalVendorTipCalculator.Apply(
                result, new Dictionary<int, IReadOnlyList<VendorOffer>>(), new Dictionary<int, ItemPrice>(),
                PriceBasis.BuyOrder, new List<string> { Gw2Constants.HalloweenFestivalName });

            Assert.NotNull(result.SeasonalVendorTips);
            Assert.Empty(result.SeasonalVendorTips);
        }
    }
}
