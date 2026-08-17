using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// opportunity-notes (SEASONAL VENDOR TIP) -
    /// PlanViewModelBuilder.BuildNotesSection's formatting, given an
    /// already-computed CraftingPlanResult.SeasonalVendorTips list (the
    /// calculator's own math is covered separately by
    /// SeasonalVendorTipCalculatorTests).
    /// </summary>
    public class PlanViewModelBuilderNotesSeasonalVendorTipTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        [Fact]
        public void ItemCostTip_RendersFullSentenceWithDisplayFestivalName()
        {
            var meta = MetaFor((19721, "Glob of Ectoplasm", "e.png"), (999, "Candy Corn", "c.png"));
            var result = MakeResult(
                metadata: meta,
                seasonalVendorTips: new List<SeasonalVendorTip>
                {
                    new SeasonalVendorTip
                    {
                        ItemId = 19721,
                        Festival = Gw2Constants.HalloweenFestivalName,
                        MerchantName = "Candy Corn Vendor (Weekly)",
                        CostLines = new List<CostLine> { new CostLine { Type = "Item", Id = 999, Count = 1 } },
                        OutputCount = 5,
                        OfferUnitCost = 10,
                        PlanUnitPrice = 100,
                        WeeklyCap = 1
                    }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            // Review fix (finding 4): split into two rows so the trailing
            // "cheaper than..." clause can never be the part an ellipsized
            // panel width cuts - see BuildNotesSection's own doc comment.
            Assert.Equal(2, section.Rows.Count);
            var tradeRow = section.Rows[0];
            var priceRow = section.Rows[1];
            Assert.Equal(PlanRowType.NoteLine, tradeRow.RowType);
            Assert.Equal(PlanRowType.NoteLine, priceRow.RowType);
            // DisplayName ("Halloween"), not the internal key ("halloween").
            Assert.Contains("During Halloween:", tradeRow.Label);
            Assert.Contains("Candy Corn Vendor (Weekly)", tradeRow.Label);
            Assert.Contains("1x Candy Corn", tradeRow.Label);
            Assert.Contains("5x Glob of Ectoplasm", tradeRow.Label);
            Assert.Contains("limit 1 purchase/week", tradeRow.Label);
            Assert.Equal(0, tradeRow.CoinValue);
            // "per unit" stated explicitly - PlanUnitPrice is a PER-UNIT
            // price, not the price of the "5x Glob of Ectoplasm" bundle
            // named on the row just above it.
            Assert.Contains("per unit", priceRow.Label);
            Assert.Equal(100, priceRow.CoinValue);
            Assert.Equal("Notes (1)", section.Title);
        }

        // Review fix (finding 2, 2026-08-17): guards the exact regression
        // found - FestivalDisplayNames used to contain only Halloween, so
        // any of the five other known festivals rendered the raw internal
        // key verbatim ("During superadventurefestival:") instead of its
        // DisplayName. Picks a non-Halloween key deliberately so a
        // regression to "Halloween only" trips this test even though
        // ItemCostTip_RendersFullSentenceWithDisplayFestivalName above
        // would keep passing.
        [Fact]
        public void ItemCostTip_NonHalloweenFestival_RendersDisplayFestivalNameNotInternalKey()
        {
            var meta = MetaFor((19721, "Glob of Ectoplasm", "e.png"), (999, "Zhaitaffy", "c.png"));
            var result = MakeResult(
                metadata: meta,
                seasonalVendorTips: new List<SeasonalVendorTip>
                {
                    new SeasonalVendorTip
                    {
                        ItemId = 19721,
                        Festival = "superadventurefestival",
                        MerchantName = "Super Adventure Box Weekly Trader",
                        CostLines = new List<CostLine> { new CostLine { Type = "Item", Id = 999, Count = 1 } },
                        OutputCount = 1,
                        OfferUnitCost = 10,
                        PlanUnitPrice = 100,
                        WeeklyCap = 1
                    }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.Contains("During Super Adventure Festival:", section.Rows[0].Label);
            Assert.DoesNotContain("superadventurefestival", section.Rows[0].Label);
        }

        [Fact]
        public void CoinCostLine_SkippedEntirely_CannotRenderInlineWithoutIcon()
        {
            var meta = MetaFor((19721, "Glob of Ectoplasm", "e.png"));
            var result = MakeResult(
                metadata: meta,
                seasonalVendorTips: new List<SeasonalVendorTip>
                {
                    new SeasonalVendorTip
                    {
                        ItemId = 19721,
                        Festival = Gw2Constants.HalloweenFestivalName,
                        MerchantName = "Some Vendor",
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 500 }
                        },
                        OutputCount = 1,
                        OfferUnitCost = 500,
                        PlanUnitPrice = 1000
                    }
                });

            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.Notes);
        }

        [Fact]
        public void DailyCapUsed_WhenNoWeeklyCap()
        {
            var meta = MetaFor((19721, "Glob of Ectoplasm", "e.png"), (999, "Candy Corn", "c.png"));
            var result = MakeResult(
                metadata: meta,
                seasonalVendorTips: new List<SeasonalVendorTip>
                {
                    new SeasonalVendorTip
                    {
                        ItemId = 19721,
                        Festival = Gw2Constants.HalloweenFestivalName,
                        MerchantName = "Some Vendor",
                        CostLines = new List<CostLine> { new CostLine { Type = "Item", Id = 999, Count = 1 } },
                        OutputCount = 5,
                        OfferUnitCost = 10,
                        PlanUnitPrice = 100,
                        DailyCap = 3
                    }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.Contains("limit 3 purchases/day", section.Rows[0].Label);
        }

        [Fact]
        public void NoCap_OmitsCapClause()
        {
            var meta = MetaFor((19721, "Glob of Ectoplasm", "e.png"), (999, "Candy Corn", "c.png"));
            var result = MakeResult(
                metadata: meta,
                seasonalVendorTips: new List<SeasonalVendorTip>
                {
                    new SeasonalVendorTip
                    {
                        ItemId = 19721,
                        Festival = Gw2Constants.HalloweenFestivalName,
                        MerchantName = "Some Vendor",
                        CostLines = new List<CostLine> { new CostLine { Type = "Item", Id = 999, Count = 1 } },
                        OutputCount = 5,
                        OfferUnitCost = 10,
                        PlanUnitPrice = 100
                    }
                });

            var vm = _builder.Build(result);

            var section = vm.Sections.Single(s => s.SectionType == PlanSectionType.Notes);
            Assert.DoesNotContain("limit", section.Rows[0].Label);
        }

        [Fact]
        public void NoTips_NoRows()
        {
            var result = MakeResult(seasonalVendorTips: new List<SeasonalVendorTip>());

            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.Notes);
        }
    }
}
