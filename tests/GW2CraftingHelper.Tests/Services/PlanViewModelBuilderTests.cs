using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanViewModelBuilderTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        private static CraftingPlanResult MakeResult(
            int targetItemId = 1,
            int targetQuantity = 1,
            long totalCoinCost = 0,
            List<PlanStep> steps = null,
            List<CurrencyCost> currencyCosts = null,
            Dictionary<int, ItemMetadata> metadata = null,
            List<UsedMaterial> usedMaterials = null,
            List<RequiredDiscipline> requiredDisciplines = null,
            List<RequiredRecipe> requiredRecipes = null,
            Dictionary<int, CurrencyMetadata> currencyMetadata = null,
            Dictionary<int, AcquisitionHint> acquisitionHints = null,
            List<TimegatedItem> timegatedItems = null)
        {
            return new CraftingPlanResult
            {
                Plan = new CraftingPlan
                {
                    TargetItemId = targetItemId,
                    TargetQuantity = targetQuantity,
                    TotalCoinCost = totalCoinCost,
                    Steps = steps ?? new List<PlanStep>(),
                    CurrencyCosts = currencyCosts ?? new List<CurrencyCost>(),
                    TimegatedItems = timegatedItems ?? new List<TimegatedItem>()
                },
                ItemMetadata = metadata != null
                    ? metadata
                    : new Dictionary<int, ItemMetadata>(),
                UsedMaterials = usedMaterials,
                RequiredDisciplines = requiredDisciplines ?? new List<RequiredDiscipline>(),
                RequiredRecipes = requiredRecipes ?? new List<RequiredRecipe>(),
                DebugLog = new List<string>(),
                CurrencyMetadata = currencyMetadata,
                AcquisitionHints = acquisitionHints
            };
        }

        private static Dictionary<int, ItemMetadata> MetaFor(params (int id, string name, string icon)[] items)
        {
            var dict = new Dictionary<int, ItemMetadata>();
            foreach (var (id, name, icon) in items)
            {
                dict[id] = new ItemMetadata { ItemId = id, Name = name, IconUrl = icon };
            }
            return dict;
        }

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

        // --- Used Materials ---

        [Fact]
        public void UsedMaterials_NonEmpty_CreatesSection()
        {
            var meta = MetaFor((10, "Ori Ingot", "ori.png"), (20, "Mithril Ore", "mith.png"));
            var result = MakeResult(
                metadata: meta,
                usedMaterials: new List<UsedMaterial>
                {
                    new UsedMaterial { ItemId = 10, QuantityUsed = 5 },
                    new UsedMaterial { ItemId = 20, QuantityUsed = 3 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.FirstOrDefault(s => s.SectionType == PlanSectionType.UsedMaterials);
            Assert.NotNull(section);
            Assert.Equal("Used Materials (2)", section.Title);
            Assert.Equal(2, section.Rows.Count);
            Assert.Equal("Ori Ingot", section.Rows[0].Label);
            Assert.Equal(5, section.Rows[0].Quantity);
            Assert.Equal("ori.png", section.Rows[0].IconUrl);
            Assert.Equal(PlanRowType.UsedMaterial, section.Rows[0].RowType);
        }

        [Fact]
        public void UsedMaterials_Empty_NoSection()
        {
            var result = MakeResult(usedMaterials: new List<UsedMaterial>());
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.UsedMaterials);
        }

        [Fact]
        public void UsedMaterials_Null_NoSection()
        {
            var result = MakeResult(usedMaterials: null);
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.UsedMaterials);
        }

        // --- Shopping List ---

        [Fact]
        public void ShoppingList_BuyFromTp_CorrectRowType()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.ShoppingBuy, section.Rows[0].RowType);
        }

        [Fact]
        public void ShoppingList_BuyFromVendor_CorrectRowType()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.BuyFromVendor, TotalCost = 100 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.ShoppingVendor, section.Rows[0].RowType);
        }

        [Fact]
        public void ShoppingList_Currency_CorrectRowType()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 2, Source = AcquisitionSource.Currency }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.ShoppingCurrency, section.Rows[0].RowType);
        }

        [Fact]
        public void ShoppingList_UnknownSource_CorrectRowType()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.ShoppingUnknown, section.Rows[0].RowType);
        }

        [Fact]
        public void ShoppingList_CoinValueFromTotalCost()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 5, Source = AcquisitionSource.BuyFromTp, TotalCost = 5000 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Equal(5000L, section.Rows[0].CoinValue);
        }

        [Fact]
        public void ShoppingList_UnitCoinValueFromStepUnitCost()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 5, Source = AcquisitionSource.BuyFromTp, UnitCost = 1000, TotalCost = 5000 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Equal(1000L, section.Rows[0].UnitCoinValue);
            Assert.Equal(5000L, section.Rows[0].CoinValue);
        }

        // --- Non-coin currency rows / dash rows (KNOWN-ISSUES #16) ---

        [Fact]
        public void ShoppingList_VendorRow_ZeroCoinWithCurrencyCost_PopulatesCurrencyCosts()
        {
            // Vision-Crystal-style vendor decision: no coin, priced
            // entirely in a non-coin currency - previously rendered as a
            // blank cell (bug); the row's CoinValue stays 0 but
            // CurrencyCosts must carry the real cost so the view can render
            // it instead of silently dropping it.
            var currencyMeta = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shards", IconUrl = "s.png" }
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep
                    {
                        ItemId = 1, Quantity = 2, Source = AcquisitionSource.BuyFromVendor,
                        TotalCost = 0, UnitCost = 0,
                        VendorCurrencyCosts = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 100 } }
                    }
                },
                currencyMetadata: currencyMeta);
            var vm = _builder.Build(result);

            var row = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows[0];
            Assert.Equal(0L, row.CoinValue);
            Assert.NotNull(row.CurrencyCosts);
            Assert.Single(row.CurrencyCosts);
            Assert.Equal(100, row.CurrencyCosts[0].Amount);
            Assert.Equal("Spirit Shards", row.CurrencyCosts[0].Name);
            Assert.Equal("s.png", row.CurrencyCosts[0].IconUrl);
        }

        [Fact]
        public void ShoppingList_VendorRow_UnitCurrencyCosts_UsesWinningOfferRate()
        {
            // M34-B1 #2: Each is the winning offer's own per-batch rate
            // (VendorOfferCurrencyCostLinesPerBatch / VendorOfferOutputCount
            // - here a 4-for-4 batch bought 100 times = 400 total), not a
            // total/Quantity average over the aggregated row.
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep
                {
                    ItemId = 1, Quantity = 400, Source = AcquisitionSource.BuyFromVendor,
                    TotalCost = 0, UnitCost = 0,
                    VendorCurrencyCosts = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 400 } },
                    VendorOfferOutputCount = 4,
                    VendorOfferCurrencyCostLinesPerBatch = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 4 } }
                }
            });
            var vm = _builder.Build(result);

            var row = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows[0];
            Assert.Equal(1, row.UnitCurrencyCosts[0].Amount);
            Assert.Equal(400, row.CurrencyCosts[0].Amount);
        }

        [Fact]
        public void ShoppingList_VendorRow_MixedOfferConflict_NoBatchInfo_UnitCurrencyCostsNull()
        {
            // A step whose tree occurrences resolved to more than one
            // distinct offer (PlanSolver's Conflict case) carries no batch
            // info - Each must be omitted, never an invented/guessed rate.
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep
                {
                    ItemId = 1, Quantity = 101, Source = AcquisitionSource.BuyFromVendor,
                    TotalCost = 0, UnitCost = 0,
                    VendorCurrencyCosts = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 152 } }
                }
            });
            var vm = _builder.Build(result);

            var row = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows[0];
            Assert.Null(row.UnitCurrencyCosts);
            Assert.Equal(152, row.CurrencyCosts[0].Amount);
        }

        [Fact]
        public void ShoppingList_VendorRow_MixedCoinAndCurrency_BothPopulated()
        {
            // A vendor offer partly priced in coin and partly in a non-coin
            // currency - the row must carry both, not just one.
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep
                {
                    ItemId = 1, Quantity = 1, Source = AcquisitionSource.BuyFromVendor,
                    TotalCost = 500, UnitCost = 500,
                    VendorCurrencyCosts = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 50 } }
                }
            });
            var vm = _builder.Build(result);

            var row = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows[0];
            Assert.Equal(500L, row.CoinValue);
            Assert.NotNull(row.CurrencyCosts);
            Assert.Equal(50, row.CurrencyCosts[0].Amount);
        }

        [Fact]
        public void ShoppingList_TpRow_NeverHasCurrencyCosts()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 }
            });
            var vm = _builder.Build(result);

            var row = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows[0];
            Assert.Null(row.CurrencyCosts);
            Assert.Null(row.UnitCurrencyCosts);
        }

        [Fact]
        public void ShoppingList_UnknownSource_ZeroCoinAndNoCurrencyCosts_DashRowCondition()
        {
            // Genuinely unpriceable: PlanSolver never populates TotalCost/
            // VendorCurrencyCosts for an UnknownSource step, so the row
            // ends up with CoinValue == 0 and CurrencyCosts == null - the
            // exact combination the view renders as a dash instead of a
            // blank cell (KNOWN-ISSUES #16b). This test locks in the
            // view-model side of that condition; the dash glyph itself is
            // rendered by CraftingPlanView.RenderValueCellRightAligned,
            // which is Blish-only and not covered here.
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource }
            });
            var vm = _builder.Build(result);

            var row = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows[0];
            Assert.Equal(0L, row.CoinValue);
            Assert.Equal(0L, row.UnitCoinValue);
            Assert.Null(row.CurrencyCosts);
            Assert.Null(row.UnitCurrencyCosts);
        }

        [Fact]
        public void Build_CurrencyMetadata_PassedThroughToViewModel()
        {
            var currencyMeta = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shards" }
            };
            var result = MakeResult(currencyMetadata: currencyMeta);

            var vm = _builder.Build(result);

            Assert.Same(currencyMeta, vm.CurrencyMetadata);
        }

        [Fact]
        public void Build_CurrencyMetadataNull_ViewModelCurrencyMetadataNull()
        {
            var result = MakeResult();

            var vm = _builder.Build(result);

            Assert.Null(vm.CurrencyMetadata);
        }

        // --- Acquisition hints (M32) ---

        [Fact]
        public void ShoppingList_UnknownSource_WithHint_PopulatesHintText()
        {
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Salvaged from ascended gear." }
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource }
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Equal("Salvaged from ascended gear.", section.Rows[0].HintText);
        }

        [Fact]
        public void ShoppingList_UnknownSource_NoHintsDict_HintTextNull()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Null(section.Rows[0].HintText);
        }

        [Fact]
        public void ShoppingList_NonUnknownSource_HintsPresent_HintTextStaysNull()
        {
            // A hint entry exists for the item, but the row is a normal TP
            // purchase, not an unknown-source row - the hint must not bleed
            // onto a priced row's tooltip.
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Should never appear on a priced row." }
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 }
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Null(section.Rows[0].HintText);
        }

        [Fact]
        public void ShoppingList_UnknownSource_EmptyHintString_HintTextStaysNull()
        {
            // Empty-string Hint (as opposed to a missing dict entry) must
            // resolve to null, same guard as CraftingTreeBuilder's
            // ApplyAcquisitionHint uses for AcquisitionHint.
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "" }
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource }
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Null(section.Rows[0].HintText);
        }

        [Fact]
        public void ShoppingList_UnknownSource_WithBadge_PopulatesBadgeText()
        {
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Salvaged from ascended gear.", Badge = "SALVAGE" }
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource }
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Equal("SALVAGE", section.Rows[0].BadgeText);
        }

        [Fact]
        public void ShoppingList_UnknownSource_NoBadge_BadgeTextNull()
        {
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Salvaged from ascended gear." }
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 1, Source = AcquisitionSource.UnknownSource }
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Null(section.Rows[0].BadgeText);
        }

        [Fact]
        public void ShoppingList_NonUnknownSource_BadgePresent_BadgeTextStaysNull()
        {
            // Same non-bleed guarantee as HintText: a badge entry existing
            // for the item must not appear on a priced row's tag.
            var hints = new Dictionary<int, AcquisitionHint>
            {
                [1] = new AcquisitionHint { ItemId = 1, Hint = "Should never appear.", Badge = "SALVAGE" }
            };
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 }
                },
                acquisitionHints: hints);
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Null(section.Rows[0].BadgeText);
        }

        // --- Crafting Steps ---

        [Fact]
        public void CraftingSteps_OnlyCraftSource()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp },
                new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                new PlanStep { ItemId = 3, Quantity = 2, Source = AcquisitionSource.BuyFromVendor }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.CraftStep, section.Rows[0].RowType);
        }

        [Fact]
        public void CraftingSteps_PreservesOrder()
        {
            var meta = MetaFor((2, "Blade", "blade.png"), (3, "Hilt", "hilt.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                    new PlanStep { ItemId = 3, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 20 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Equal(2, section.Rows.Count);
            Assert.Equal("Blade", section.Rows[0].Label);
            Assert.Equal("Hilt", section.Rows[1].Label);
        }

        [Fact]
        public void NoCraftSteps_NoCraftingSection()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 5, Source = AcquisitionSource.BuyFromTp }
            });
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.CraftingSteps);
        }

        [Fact]
        public void TimegatedItems_AppendedAsNoticeRowsInCraftingSteps()
        {
            // M34-B1 #3: a timegated (vendor purchase cap) notice renders as
            // a plain informational row alongside real craft steps, never
            // altering the numbered CraftStep rows themselves.
            var meta = MetaFor((2, "Blade", "blade.png"), (9, "Obsidian Shard", "shard.png"));
            var result = MakeResult(
                metadata: meta,
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 }
                },
                timegatedItems: new List<TimegatedItem>
                {
                    new TimegatedItem { ItemId = 9, CapType = TimegatedCapType.Daily, CapValue = 3, NeededCount = 4 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Equal(2, section.Rows.Count);
            Assert.Equal(PlanRowType.CraftStep, section.Rows[0].RowType);
            Assert.Equal(PlanRowType.TimegatedNotice, section.Rows[1].RowType);
            Assert.Contains("Obsidian Shard", section.Rows[1].Label);
            Assert.Contains("Daily", section.Rows[1].Label);
            Assert.Contains("3", section.Rows[1].Label);
            Assert.Contains("4", section.Rows[1].Label);
        }

        [Fact]
        public void TimegatedItems_NoCraftSteps_StillCreatesCraftingSection()
        {
            // A plan with zero real craft steps but a timegated vendor buy
            // must still surface the notice - the section is no longer
            // gated purely on craftSteps.Count.
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 9, Quantity = 4, Source = AcquisitionSource.BuyFromVendor }
                },
                timegatedItems: new List<TimegatedItem>
                {
                    new TimegatedItem { ItemId = 9, CapType = TimegatedCapType.Weekly, CapValue = 3, NeededCount = 4 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.TimegatedNotice, section.Rows[0].RowType);
        }

        // --- Required Disciplines ---

        [Fact]
        public void RequiredDisciplines_MapsCorrectly()
        {
            var result = MakeResult(requiredDisciplines: new List<RequiredDiscipline>
            {
                new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredDisciplines);
            Assert.Single(section.Rows);
            Assert.Equal(PlanRowType.DisciplineRow, section.Rows[0].RowType);
            Assert.Equal("Weaponsmith", section.Rows[0].Label);
            Assert.Equal("Level 500", section.Rows[0].Sublabel);
        }

        [Fact]
        public void RequiredDisciplines_Empty_NoSection()
        {
            var result = MakeResult(requiredDisciplines: new List<RequiredDiscipline>());
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections, s => s.SectionType == PlanSectionType.RequiredDisciplines);
        }

        // --- Required Recipes ---

        [Fact]
        public void RequiredRecipes_AutoLearned_StatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = true,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = null
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Auto-learned", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_Missing_StatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = true
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Missing!", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_Learned_StatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = false
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Learned", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_NullMissing_EmptyStatusTag()
        {
            var result = MakeResult(requiredRecipes: new List<RequiredRecipe>
            {
                new RequiredRecipe
                {
                    RecipeId = 10,
                    OutputItemId = 1,
                    IsAutoLearned = false,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    IsMissing = null
                }
            });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("", section.Rows[0].StatusTag);
        }

        [Fact]
        public void RequiredRecipes_OutputName_FromMetadata()
        {
            var meta = MetaFor((5, "Cool Blade", "blade.png"));
            var result = MakeResult(
                metadata: meta,
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 10,
                        OutputItemId = 5,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "Weaponsmith" },
                        MinRating = 400
                    }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Cool Blade", section.Rows[0].Label);
            Assert.Equal("blade.png", section.Rows[0].IconUrl);
        }

        // --- Section order ---

        [Fact]
        public void SectionOrder_MatchesSpec()
        {
            var meta = MetaFor(
                (1, "Target", "t.png"),
                (2, "Blade", "b.png"),
                (3, "Ore", "o.png"),
                (10, "Used", "u.png"));
            var result = MakeResult(
                targetItemId: 1,
                metadata: meta,
                usedMaterials: new List<UsedMaterial>
                {
                    new UsedMaterial { ItemId = 10, QuantityUsed = 1 }
                },
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 3, Quantity = 5, Source = AcquisitionSource.BuyFromTp, TotalCost = 500 },
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 20 }
                },
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 }
                },
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 20,
                        OutputItemId = 2,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "Weaponsmith" },
                        MinRating = 500
                    }
                });
            var vm = _builder.Build(result);

            var types = vm.Sections.Select(s => s.SectionType).ToList();
            Assert.Equal(new[]
            {
                PlanSectionType.Summary,
                PlanSectionType.UsedMaterials,
                PlanSectionType.ShoppingList,
                PlanSectionType.RequiredDisciplines,
                PlanSectionType.RequiredRecipes,
                PlanSectionType.CraftingSteps
            }, types);
        }

        // --- Mixed steps ---

        [Fact]
        public void MixedSteps_CorrectSectionAssignment()
        {
            var result = MakeResult(steps: new List<PlanStep>
            {
                new PlanStep { ItemId = 1, Quantity = 3, Source = AcquisitionSource.BuyFromTp, TotalCost = 300 },
                new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 },
                new PlanStep { ItemId = 3, Quantity = 2, Source = AcquisitionSource.BuyFromVendor, TotalCost = 200 }
            });
            var vm = _builder.Build(result);

            var shopping = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList);
            Assert.Equal(2, shopping.Rows.Count);
            Assert.Contains(shopping.Rows, r => r.RowType == PlanRowType.ShoppingBuy);
            Assert.Contains(shopping.Rows, r => r.RowType == PlanRowType.ShoppingVendor);

            var crafting = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Single(crafting.Rows);
            Assert.Equal(PlanRowType.CraftStep, crafting.Rows[0].RowType);
        }

        // --- Target quantity ---

        [Fact]
        public void TargetQuantity_PassedThrough()
        {
            var result = MakeResult(targetQuantity: 5);
            var vm = _builder.Build(result);

            Assert.Equal(5, vm.TargetQuantity);
        }

        // --- FormatDisciplineSublabel ---

        [Fact]
        public void FormatDisciplineSublabel_SingleDiscipline()
        {
            var planDiscNames = new HashSet<string> { "Weaponsmith" };
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string> { "Weaponsmith" }, 400, planDiscNames);

            Assert.Equal("Weaponsmith 400", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_MultiDiscipline_FiltersToRelevant()
        {
            var planDiscNames = new HashSet<string> { "Weaponsmith" };
            // Recipe has 4 disciplines, but plan only uses Weaponsmith
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string> { "Weaponsmith", "Armorsmith", "Huntsman", "Artificer" },
                400, planDiscNames);

            Assert.Equal("Weaponsmith 400", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_MultiDiscipline_MultiRelevant()
        {
            var planDiscNames = new HashSet<string> { "Armorsmith", "Weaponsmith" };
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string> { "Weaponsmith", "Armorsmith", "Huntsman" },
                400, planDiscNames);

            Assert.Equal("Armorsmith / Weaponsmith 400", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_NoDisciplines_EmptyString()
        {
            var planDiscNames = new HashSet<string> { "Weaponsmith" };
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string>(), 0, planDiscNames);

            Assert.Equal("", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_NullDisciplines_EmptyString()
        {
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                null, 0, new HashSet<string>());

            Assert.Equal("", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_NoIntersection_FallbackToAll()
        {
            // Plan disciplines don't overlap with recipe disciplines
            var planDiscNames = new HashSet<string> { "Leatherworker" };
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string> { "Weaponsmith", "Armorsmith" },
                300, planDiscNames);

            Assert.Equal("Armorsmith / Weaponsmith 300", result);
        }

        [Fact]
        public void FormatDisciplineSublabel_NullPlanDiscNames_ShowsAll()
        {
            var result = PlanViewModelBuilder.FormatDisciplineSublabel(
                new List<string> { "Weaponsmith", "Armorsmith" }, 400, null);

            Assert.Equal("Armorsmith / Weaponsmith 400", result);
        }

        // --- Recipe sublabel integration ---

        [Fact]
        public void RequiredRecipes_Sublabel_ShowsRelevantDisciplines()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 }
                },
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 10,
                        OutputItemId = 1,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "Weaponsmith", "Armorsmith", "Huntsman" },
                        MinRating = 400
                    }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.RequiredRecipes);
            Assert.Equal("Weaponsmith 400", section.Rows[0].Sublabel);
        }

        [Fact]
        public void CraftingSteps_Sublabel_ShowsRelevantDisciplines()
        {
            var result = MakeResult(
                requiredDisciplines: new List<RequiredDiscipline>
                {
                    new RequiredDiscipline { Discipline = "Weaponsmith", MinRating = 500 }
                },
                requiredRecipes: new List<RequiredRecipe>
                {
                    new RequiredRecipe
                    {
                        RecipeId = 10,
                        OutputItemId = 2,
                        IsAutoLearned = true,
                        Disciplines = new List<string> { "Weaponsmith", "Armorsmith", "Huntsman", "Artificer" },
                        MinRating = 400
                    }
                },
                steps: new List<PlanStep>
                {
                    new PlanStep { ItemId = 2, Quantity = 1, Source = AcquisitionSource.Craft, RecipeId = 10 }
                });
            var vm = _builder.Build(result);

            var section = vm.Sections.First(s => s.SectionType == PlanSectionType.CraftingSteps);
            Assert.Equal("Weaponsmith 400", section.Rows[0].Sublabel);
        }

        // --- Sell-side economics rows ---

        [Fact]
        public void NoSellPrice_NoSellRows()
        {
            var result = MakeResult(totalCoinCost: 500);
            var vm = _builder.Build(result);

            var rows = vm.Sections[0].Rows;
            Assert.Single(rows);
            Assert.Equal("Total", rows[0].Label);
        }

        [Fact]
        public void SellValuePresent_AddsSellAndProfitRows()
        {
            var result = MakeResult(totalCoinCost: 300);
            result.TargetUnitSellPrice = 400;
            result.NetSaleValue = 340;
            result.CraftingProfit = 40;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal(3, rows.Count);
            Assert.Equal("Sell value (after 15% TP fees)", rows[1].Label);
            Assert.Equal(340L, rows[1].CoinValue);
            Assert.Equal("Profit if sold", rows[2].Label);
            Assert.Equal(40L, rows[2].CoinValue);
        }

        [Fact]
        public void NegativeProfit_RendersAsLossWithAbsoluteValue()
        {
            var result = MakeResult(totalCoinCost: 500);
            result.NetSaleValue = 340;
            result.CraftingProfit = -160;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal("Loss if sold", rows[2].Label);
            Assert.Equal(160L, rows[2].CoinValue);
        }

        [Fact]
        public void CurrencyCostsPresent_ProfitRowGetsCoinOnlyQualifier()
        {
            var result = MakeResult(
                totalCoinCost: 100,
                currencyCosts: new List<CurrencyCost>
                {
                    new CurrencyCost { CurrencyId = 2, Amount = 50 }
                });
            result.NetSaleValue = 340;
            result.CraftingProfit = 240;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal("Profit if sold (coin costs only)", rows[2].Label);
        }

        [Fact]
        public void OverproducedBatch_SellRowShowsActualQuantity()
        {
            var result = MakeResult(targetQuantity: 1, totalCoinCost: 300);
            result.SellableQuantity = 5;
            result.NetSaleValue = 1700;
            result.CraftingProfit = 1400;

            var vm = _builder.Build(result);

            Assert.Equal("Sell value (5x, after 15% TP fees)", vm.Sections[0].Rows[1].Label);
        }

        [Fact]
        public void BuyOrderBasis_TotalRowLabeled()
        {
            var result = MakeResult(totalCoinCost: 100);
            result.PriceBasis = PriceBasis.BuyOrder;

            var vm = _builder.Build(result);

            Assert.Equal("Total (buy-order prices)", vm.Sections[0].Rows[0].Label);
        }

        // --- Own-materials opportunity cost row (M28) ---

        [Fact]
        public void MaterialOpportunityCostPositive_AddsRowRightAfterTotal()
        {
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 25;
            result.NetSaleValue = 340;
            result.CraftingProfit = 115;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal(4, rows.Count);
            Assert.Equal("Total", rows[0].Label);
            Assert.Equal("Own materials (sell value forgone)", rows[1].Label);
            Assert.Equal(25L, rows[1].CoinValue);
            Assert.Equal(PlanRowType.CoinTotal, rows[1].RowType);
            Assert.Equal("Sell value (after 15% TP fees)", rows[2].Label);
            Assert.Equal("Profit if sold", rows[3].Label);
            Assert.Equal(115L, rows[3].CoinValue);
        }

        [Fact]
        public void MaterialOpportunityCostPositive_NoSellPrice_StillAddsRow()
        {
            // MaterialOpportunityCost can be populated even when the target
            // has no live sell price (NetSaleValue/CraftingProfit stay
            // null) - the row is not gated on target sellability.
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 25;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal(2, rows.Count);
            Assert.Equal("Own materials (sell value forgone)", rows[1].Label);
            Assert.Equal(25L, rows[1].CoinValue);
        }

        [Fact]
        public void MaterialOpportunityCostZero_NoRow()
        {
            // All used materials were unsellable - the sum is 0, not null,
            // but a 0-value row is not worth surfacing.
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 0;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Single(rows);
            Assert.Equal("Total", rows[0].Label);
        }

        [Fact]
        public void MaterialOpportunityCostNull_NoRow()
        {
            // Free mode (default) - MaterialOpportunityCost is never set.
            var result = MakeResult(totalCoinCost: 200);

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Single(rows);
            Assert.Equal("Total", rows[0].Label);
        }
    }
}
