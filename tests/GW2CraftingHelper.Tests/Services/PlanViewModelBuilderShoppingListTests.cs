using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanViewModelBuilderShoppingListTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

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

        // --- Non-coin currency rows / dash rows ---

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

        // --- M34-B2b: owned/needed split on shopping-row currency Total cells ---

        [Fact]
        public void ShoppingList_VendorRow_OwnedCurrencyAmountsPresent_SetsOwnedQuantityOnCurrencyCosts()
        {
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep
                    {
                        ItemId = 1, Quantity = 2, Source = AcquisitionSource.BuyFromVendor,
                        TotalCost = 0, UnitCost = 0,
                        VendorCurrencyCosts = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 100 } }
                    }
                });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 23, 40 } };
            var vm = _builder.Build(result);

            var row = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows[0];
            Assert.Equal(40, row.CurrencyCosts[0].OwnedQuantity);
            Assert.Equal(40, row.CurrencyCosts[0].RawOwnedQuantity);
        }

        [Fact]
        public void ShoppingList_VendorRow_OwnedCurrencyAmountsExceedRequirement_RawOwnedQuantityKeepsUnclampedHolding()
        {
            // shoplist-have-format: OwnedQuantity clamps to the row's Total
            // (100) so the HAVE/Amount pair the tooltip renders always
            // reads as coverage; RawOwnedQuantity must still carry the real
            // 250 the wallet holds so the tooltip can spell that out too.
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep
                    {
                        ItemId = 1, Quantity = 2, Source = AcquisitionSource.BuyFromVendor,
                        TotalCost = 0, UnitCost = 0,
                        VendorCurrencyCosts = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 100 } }
                    }
                });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 23, 250 } };
            var vm = _builder.Build(result);

            var row = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows[0];
            Assert.Equal(100, row.CurrencyCosts[0].OwnedQuantity);
            Assert.Equal(250, row.CurrencyCosts[0].RawOwnedQuantity);
        }

        [Fact]
        public void ShoppingList_VendorRow_NoOwnedCurrencyAmounts_OwnedQuantityStaysNull()
        {
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep
                    {
                        ItemId = 1, Quantity = 2, Source = AcquisitionSource.BuyFromVendor,
                        TotalCost = 0, UnitCost = 0,
                        VendorCurrencyCosts = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 100 } }
                    }
                });
            var vm = _builder.Build(result);

            var row = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows[0];
            Assert.Null(row.CurrencyCosts[0].OwnedQuantity);
            Assert.Null(row.CurrencyCosts[0].RawOwnedQuantity);
        }

        [Fact]
        public void ShoppingList_VendorRow_UnitCurrencyCosts_NeverGetsOwnedQuantity()
        {
            // Ownership is a total-quantity concept - the Each column's
            // per-unit rate must never carry an owned/needed split even
            // when wallet data is present.
            var result = MakeResult(
                steps: new List<PlanStep>
                {
                    new PlanStep
                    {
                        ItemId = 1, Quantity = 100, Source = AcquisitionSource.BuyFromVendor,
                        TotalCost = 0, UnitCost = 0,
                        VendorCurrencyCosts = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 400 } },
                        VendorOfferOutputCount = 1,
                        VendorOfferCurrencyCostLinesPerBatch = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 4 } }
                    }
                });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 23, 40 } };
            var vm = _builder.Build(result);

            var row = vm.Sections.First(s => s.SectionType == PlanSectionType.ShoppingList).Rows[0];
            Assert.Null(row.UnitCurrencyCosts[0].OwnedQuantity);
            Assert.Null(row.UnitCurrencyCosts[0].RawOwnedQuantity);
            Assert.Equal(40, row.CurrencyCosts[0].OwnedQuantity);
        }

        [Fact]
        public void ShoppingList_VendorRow_UnitCurrencyCosts_UsesWinningOfferRate()
        {
            // Each is the winning offer's own per-batch rate
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
            // blank cell. This test locks in the
            // view-model side of that condition; the dash glyph itself is
            // rendered by CoinCurrencyRenderer.RenderValueCellRightAligned
            // (Views/Rendering, M38 WP-21), which is Blish-only and not
            // covered here.
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
    }
}
