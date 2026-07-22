using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanViewModelBuilderSummaryTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        // --- Empty plan ---

        [Fact]
        public void EmptyPlan_ReturnsSummarySectionOnly()
        {
            var result = MakeResult();
            var vm = _builder.Build(result);

            Assert.Single(vm.Sections);
            Assert.Equal(PlanSectionType.Summary, vm.Sections[0].SectionType);
            Assert.Single(vm.Sections[0].Rows); // CoinTotal row
            Assert.Equal(PlanRowType.CoinTotal, vm.Sections[0].Rows[0].RowType);
            Assert.Equal(0L, vm.Sections[0].Rows[0].CoinValue);
        }

        // --- Target item resolution ---

        [Fact]
        public void TargetItem_ResolvesNameAndIcon()
        {
            var meta = MetaFor((1, "Zojja's Claymore", "claymore.png"));
            var result = MakeResult(targetItemId: 1, metadata: meta);
            var vm = _builder.Build(result);

            Assert.Equal("Zojja's Claymore", vm.TargetItemName);
            Assert.Equal("claymore.png", vm.TargetIconUrl);
        }

        [Fact]
        public void TargetItem_MissingMetadata_FallsBack()
        {
            var result = MakeResult(targetItemId: 999);
            var vm = _builder.Build(result);

            Assert.Equal("Unknown Item", vm.TargetItemName);
            Assert.Null(vm.TargetIconUrl);
            Assert.Null(vm.TargetRarity);
        }

        [Fact]
        public void TargetItem_ResolvesRarity()
        {
            var meta = new Dictionary<int, ItemMetadata>
            {
                [1] = new ItemMetadata { ItemId = 1, Name = "Zojja's Claymore", IconUrl = "c.png", Rarity = "Exotic" }
            };
            var result = MakeResult(targetItemId: 1, metadata: meta);
            var vm = _builder.Build(result);

            Assert.Equal("Exotic", vm.TargetRarity);
        }

        // --- Summary section ---

        [Fact]
        public void SummarySection_CoinTotalRow()
        {
            var result = MakeResult(totalCoinCost: 123456);
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var coinRow = summary.Rows.First(r => r.RowType == PlanRowType.CoinTotal);
            Assert.Equal(123456L, coinRow.CoinValue);
            Assert.Equal("Total", coinRow.Label);
        }

        [Fact]
        public void SummarySection_CurrencyCosts()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 50 },
                new CurrencyCost { CurrencyId = 45, Amount = 100 }
            });
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRows = summary.Rows.Where(r => r.RowType == PlanRowType.CurrencyCost).ToList();
            Assert.Equal(2, ccRows.Count);
            Assert.Equal("50x Spirit Shards", ccRows[0].Label);
            Assert.Equal("100x Volatile Magic", ccRows[1].Label);
        }

        // --- M34-B2b (view-model wiring dates to M34-B2a #4): owned/needed
        // split on Total Cost currency rows ---

        [Fact]
        public void SummarySection_CurrencyCost_OwnedAmountPresent_SetsCurrencyOwnedQuantity()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 500 }
            });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 23, 200 } };

            var vm = _builder.Build(result);

            var ccRow = vm.Sections
                .First(s => s.SectionType == PlanSectionType.Summary)
                .Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal(200, ccRow.CurrencyOwnedQuantity);
        }

        [Fact]
        public void SummarySection_CurrencyCost_OwnedExceedsNeeded_ClampedToAmount()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 500 }
            });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 23, 999999 } };

            var vm = _builder.Build(result);

            var ccRow = vm.Sections
                .First(s => s.SectionType == PlanSectionType.Summary)
                .Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal(500, ccRow.CurrencyOwnedQuantity);
        }

        [Fact]
        public void SummarySection_CurrencyCost_NoOwnedCurrencyAmounts_CurrencyOwnedQuantityNull()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 500 }
            });

            var vm = _builder.Build(result);

            var ccRow = vm.Sections
                .First(s => s.SectionType == PlanSectionType.Summary)
                .Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Null(ccRow.CurrencyOwnedQuantity);
        }

        [Fact]
        public void SummarySection_CurrencyCost_OwnedAmountsMissingThisId_CurrencyOwnedQuantityNull()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 500 }
            });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 2, 100 } }; // different currency id

            var vm = _builder.Build(result);

            var ccRow = vm.Sections
                .First(s => s.SectionType == PlanSectionType.Summary)
                .Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Null(ccRow.CurrencyOwnedQuantity);
        }

        [Fact]
        public void SummarySection_AstralAcclaim_CorrectName()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 63, Amount = 375 }
            });
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRow = summary.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("375x Astral Acclaim", ccRow.Label);
        }

        [Fact]
        public void SummarySection_RiftEssenceCurrencies_CorrectNames()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 78, Amount = 250 },
                new CurrencyCost { CurrencyId = 79, Amount = 50 },
                new CurrencyCost { CurrencyId = 80, Amount = 100 }
            });
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRows = summary.Rows.Where(r => r.RowType == PlanRowType.CurrencyCost).ToList();
            Assert.Equal(3, ccRows.Count);
            Assert.Equal("250x Fine Rift Essence", ccRows[0].Label);
            Assert.Equal("50x Rare Rift Essence", ccRows[1].Label);
            Assert.Equal("100x Masterwork Rift Essence", ccRows[2].Label);
        }

        [Fact]
        public void SummarySection_UnknownCurrency_NoIdDisplayed()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 99999, Amount = 10 }
            });
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRow = summary.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("10x Currency", ccRow.Label);
            Assert.DoesNotContain("99999", ccRow.Label);
        }

        // --- Currency icons (M30 #3) ---

        [Fact]
        public void SummarySection_CurrencyCost_IconUrlFromMetadata_WhenPresent()
        {
            var currencyMeta = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shards", IconUrl = "spirit_shard.png" }
            };
            var result = MakeResult(
                currencyCosts: new List<CurrencyCost> { new CurrencyCost { CurrencyId = 23, Amount = 50 } },
                currencyMetadata: currencyMeta);
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRow = summary.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("spirit_shard.png", ccRow.IconUrl);
            Assert.Equal("50x Spirit Shards", ccRow.Label);
        }

        [Fact]
        public void SummarySection_CurrencyCost_IconUrlNull_WhenMetadataAbsent()
        {
            // No CurrencyMetadata supplied at all (e.g. the pipeline was not
            // wired with a CurrencyMetadataService) - row must render
            // exactly as it did before icons existed: text-only, no guess.
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 50 }
            });
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRow = summary.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Null(ccRow.IconUrl);
            Assert.Equal("50x Spirit Shards", ccRow.Label);
        }

        [Fact]
        public void SummarySection_CurrencyCost_IconUrlNull_WhenIdMissingFromMetadata()
        {
            // Metadata dictionary is present (fetch succeeded) but does not
            // contain this particular currency id - still no placeholder.
            var currencyMeta = new Dictionary<int, CurrencyMetadata>
            {
                [2] = new CurrencyMetadata { CurrencyId = 2, Name = "Karma", IconUrl = "karma.png" }
            };
            var result = MakeResult(
                currencyCosts: new List<CurrencyCost> { new CurrencyCost { CurrencyId = 23, Amount = 50 } },
                currencyMetadata: currencyMeta);
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRow = summary.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Null(ccRow.IconUrl);
            Assert.Equal("50x Spirit Shards", ccRow.Label);
        }

        [Fact]
        public void SummarySection_CurrencyCost_NamePrefersMetadataOverConstantsFallback()
        {
            // Metadata name deliberately differs from the Gw2Constants
            // offline table to prove the live-fetched name wins.
            var currencyMeta = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shard (Live)", IconUrl = "s.png" }
            };
            var result = MakeResult(
                currencyCosts: new List<CurrencyCost> { new CurrencyCost { CurrencyId = 23, Amount = 7 } },
                currencyMetadata: currencyMeta);
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRow = summary.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("7x Spirit Shard (Live)", ccRow.Label);
        }

        [Fact]
        public void SummarySection_CurrencyCost_IdPresentButEmptyNameAndIcon_FallsBackToConstantsAndNullIcon()
        {
            // Id IS in the metadata dictionary (fetch succeeded and covers
            // this currency), but the entry's Name/IconUrl are both empty
            // strings (e.g. an API payload with a blank name) - the
            // !string.IsNullOrEmpty guards in ResolveCurrencyName/
            // ResolveCurrencyIconUrl must treat that the same as "absent"
            // rather than rendering a blank label or a bogus icon.
            var currencyMeta = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "", IconUrl = "" }
            };
            var result = MakeResult(
                currencyCosts: new List<CurrencyCost> { new CurrencyCost { CurrencyId = 23, Amount = 50 } },
                currencyMetadata: currencyMeta);
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRow = summary.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("50x Spirit Shards", ccRow.Label);
            Assert.Null(ccRow.IconUrl);
        }

        [Fact]
        public void SummarySection_CurrencyCost_UnknownId_FallsBackToGeneric_EvenWithMetadataPresent()
        {
            var currencyMeta = new Dictionary<int, CurrencyMetadata>
            {
                [2] = new CurrencyMetadata { CurrencyId = 2, Name = "Karma", IconUrl = "karma.png" }
            };
            var result = MakeResult(
                currencyCosts: new List<CurrencyCost> { new CurrencyCost { CurrencyId = 99999, Amount = 10 } },
                currencyMetadata: currencyMeta);
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var ccRow = summary.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("10x Currency", ccRow.Label);
            Assert.Null(ccRow.IconUrl);
        }
    }
}
