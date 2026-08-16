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
            var row = Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.NoteLine, row.RowType);
            // DisplayName ("Halloween"), not the internal key ("halloween").
            Assert.Contains("During Halloween:", row.Label);
            Assert.Contains("Candy Corn Vendor (Weekly)", row.Label);
            Assert.Contains("1x Candy Corn", row.Label);
            Assert.Contains("5x Glob of Ectoplasm", row.Label);
            Assert.Contains("capped 1/week", row.Label);
            Assert.Equal(100, row.CoinValue);
            Assert.Equal("Notes (1)", section.Title);
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
            Assert.Contains("capped 3/day", section.Rows[0].Label);
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
            Assert.DoesNotContain("capped", section.Rows[0].Label);
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
