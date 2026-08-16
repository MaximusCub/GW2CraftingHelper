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

            var rows = vm.Sections[0].Rows;
            // W4A: collapsed cost-formula tile ("Actual Cost to Craft") +
            // the always-present footnote row - no profit band (no sell
            // price), no currency rows (no currency costs).
            Assert.Equal(2, rows.Count);
            Assert.Equal(PlanRowType.CostFormulaTile, rows[0].RowType);
            Assert.Equal("Actual Cost to Craft", rows[0].Label);
            Assert.Equal(0L, rows[0].CoinValue);
            Assert.Equal(PlanRowType.SummaryFootnote, rows[1].RowType);
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

        // --- W4A: cost formula band (collapse rule + arithmetic) ---

        [Fact]
        public void CostBand_NoMaterialsUsed_CollapsesToSingleActualCostTile()
        {
            var result = MakeResult(totalCoinCost: 123456);
            var vm = _builder.Build(result);

            var summary = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
            var costTiles = summary.Rows.Where(r => r.RowType == PlanRowType.CostFormulaTile).ToList();

            Assert.Single(costTiles);
            Assert.Equal("Actual Cost to Craft", costTiles[0].Label);
            Assert.Equal(123456L, costTiles[0].CoinValue);
            Assert.False(string.IsNullOrEmpty(costTiles[0].TooltipText));
        }

        [Fact]
        public void CostBand_MaterialOpportunityCostZero_StillCollapses()
        {
            // A material with no instant-sell price contributes 0, not
            // null - the collapse rule treats null AND 0 identically
            // (spec: "when MaterialOpportunityCost is null or 0").
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 0;

            var vm = _builder.Build(result);
            var costTiles = vm.Sections[0].Rows.Where(r => r.RowType == PlanRowType.CostFormulaTile).ToList();

            Assert.Single(costTiles);
            Assert.Equal("Actual Cost to Craft", costTiles[0].Label);
            Assert.Equal(200L, costTiles[0].CoinValue);
        }

        [Fact]
        public void CostBand_MaterialsUsedPositive_ExpandsToThreeTilesWithCorrectArithmetic()
        {
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 25;

            var vm = _builder.Build(result);
            var costTiles = vm.Sections[0].Rows.Where(r => r.RowType == PlanRowType.CostFormulaTile).ToList();

            Assert.Equal(3, costTiles.Count);
            Assert.Equal("Total Materials Value", costTiles[0].Label);
            Assert.Equal(225L, costTiles[0].CoinValue); // 200 + 25
            Assert.Equal("Your Materials Used", costTiles[1].Label);
            Assert.Equal(25L, costTiles[1].CoinValue);
            Assert.Equal("Actual Cost to Craft", costTiles[2].Label);
            Assert.Equal(200L, costTiles[2].CoinValue);

            // M32 lesson (user-mandated tooltips): every tile header has
            // its own non-empty tooltip.
            Assert.All(costTiles, t => Assert.False(string.IsNullOrEmpty(t.TooltipText)));

            // Review fix (round 2): the cost band's three non-negative
            // terms always balance exactly (225 - 25 == 200), so its
            // final-boundary operator is always the true "=" - the
            // FormulaResultIsExact escape hatch exists for the profit
            // band's loss case only.
            Assert.True(costTiles[2].FormulaResultIsExact);
        }

        [Fact]
        public void CostBand_BuyOrderBasis_QualifierMovesToActualCostTooltip()
        {
            var result = MakeResult(totalCoinCost: 100);
            result.PriceBasis = PriceBasis.BuyOrder;

            var vm = _builder.Build(result);
            var costTile = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CostFormulaTile);

            // Caption stays short (no qualifier baked into the Label) - the
            // basis qualifier now lives in the tooltip only.
            Assert.Equal("Actual Cost to Craft", costTile.Label);
            Assert.Contains("buy-order prices", costTile.TooltipText);
        }

        // --- W4A: profit formula band (presence/absence, arithmetic, sign) ---

        [Fact]
        public void ProfitBand_NoSellPrice_Absent()
        {
            var result = MakeResult(totalCoinCost: 500);
            var vm = _builder.Build(result);

            Assert.DoesNotContain(vm.Sections[0].Rows, r => r.RowType == PlanRowType.ProfitFormulaTile);
        }

        [Fact]
        public void ProfitBand_SellPricePresent_ThreeTilesWithIdentityArithmetic()
        {
            var result = MakeResult(totalCoinCost: 300);
            result.TargetUnitSellPrice = 400;
            result.NetSaleValue = 340;
            result.CraftingProfit = 40;

            var vm = _builder.Build(result);
            var profitTiles = vm.Sections[0].Rows.Where(r => r.RowType == PlanRowType.ProfitFormulaTile).ToList();

            Assert.Equal(3, profitTiles.Count);
            Assert.Equal("Sell Value", profitTiles[0].Label);
            Assert.Equal(340L, profitTiles[0].CoinValue);
            Assert.Equal("Total Materials Value", profitTiles[1].Label);
            // Single-item identity: NetSaleValue - CraftingProfit == 340 - 40 == 300 == TotalCoinCost.
            Assert.Equal(300L, profitTiles[1].CoinValue);
            Assert.Equal("Profit if Sold", profitTiles[2].Label);
            Assert.Equal(40L, profitTiles[2].CoinValue);
            Assert.All(profitTiles, t => Assert.False(string.IsNullOrEmpty(t.TooltipText)));

            // Review fix (round 2): a non-negative profit means the drawn
            // "Sell Value - Total Materials Value = Profit if Sold"
            // equation is literally true (340 - 300 == 40), so the
            // renderer's final-boundary operator stays "=".
            Assert.True(profitTiles[2].FormulaResultIsExact);
        }

        [Fact]
        public void ProfitBand_NegativeProfit_LabeledLossWithAbsoluteValue()
        {
            var result = MakeResult(totalCoinCost: 500);
            result.NetSaleValue = 340;
            result.CraftingProfit = -160;

            var vm = _builder.Build(result);
            var profitTile = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.ProfitFormulaTile && r.Label.Contains("Loss"));

            Assert.Equal("Loss if Sold", profitTile.Label);
            Assert.Equal(160L, profitTile.CoinValue);

            // Review fix (round 2): the abs-value "Loss if Sold" display
            // makes "Sell Value - Total Materials Value = Loss if Sold"
            // (340 - 500 = 160) arithmetically false - the true right-hand
            // side is -160, not 160. FormulaResultIsExact false tells
            // SummarySectionRenderer.CreateFormulaBand to draw a neutral
            // separator instead of an asserting "=" for this band's final
            // boundary.
            Assert.False(profitTile.FormulaResultIsExact);
        }

        [Fact]
        public void ProfitBand_ZeroProfit_FormulaResultIsExactTrue()
        {
            // profit == 0 is the boundary of the ">= 0" check in
            // BuildProfitFormulaBand - Label stays "Profit if Sold" (not
            // "Loss") and the equation (340 - 340 = 0) is exactly true, so
            // FormulaResultIsExact must be true here too, not just for a
            // strictly positive profit.
            var result = MakeResult(totalCoinCost: 340);
            result.NetSaleValue = 340;
            result.CraftingProfit = 0;

            var vm = _builder.Build(result);
            var profitTile = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.ProfitFormulaTile && r.Label.Contains("Profit"));

            Assert.Equal("Profit if Sold", profitTile.Label);
            Assert.Equal(0L, profitTile.CoinValue);
            Assert.True(profitTile.FormulaResultIsExact);
        }

        [Fact]
        public void ProfitBand_TotalMaterialsValueMatchesCostBand_ForSingleItemPlan()
        {
            // The identity (SellSideEconomics.ApplySellSideEconomics) makes
            // Band 2's derived Total Materials Value equal Band 1's, for
            // every single-item plan.
            var result = MakeResult(totalCoinCost: 200);
            result.MaterialOpportunityCost = 25;
            result.NetSaleValue = 340;
            result.CraftingProfit = 115; // 340 - 200 - 25

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            long costBandTotalMaterialsValue = rows.First(r => r.RowType == PlanRowType.CostFormulaTile && r.Label == "Total Materials Value").CoinValue;
            long profitBandTotalMaterialsValue = rows.First(r => r.RowType == PlanRowType.ProfitFormulaTile && r.Label == "Total Materials Value").CoinValue;

            Assert.Equal(225L, costBandTotalMaterialsValue);
            Assert.Equal(costBandTotalMaterialsValue, profitBandTotalMaterialsValue);
        }

        [Fact]
        public void ProfitBand_CurrencyCostsPresent_CoinOnlyQualifierInTooltip()
        {
            var result = MakeResult(
                totalCoinCost: 100,
                currencyCosts: new List<CurrencyCost> { new CurrencyCost { CurrencyId = 2, Amount = 50 } });
            result.NetSaleValue = 340;
            result.CraftingProfit = 240;

            var vm = _builder.Build(result);
            var profitTile = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.ProfitFormulaTile && r.Label.Contains("Profit"));

            Assert.Equal("Profit if Sold", profitTile.Label);
            Assert.Contains("coin costs only", profitTile.TooltipText);
        }

        [Fact]
        public void ProfitBand_Overproduced_QualifierInSellTooltipNotLabel()
        {
            var result = MakeResult(targetQuantity: 1, totalCoinCost: 300);
            result.SellableQuantity = 5;
            result.NetSaleValue = 1700;
            result.CraftingProfit = 1400;

            var vm = _builder.Build(result);
            var sellTile = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.ProfitFormulaTile && r.Label == "Sell Value");

            Assert.Equal("Sell Value", sellTile.Label);
            Assert.Contains("5x", sellTile.TooltipText);
            Assert.Contains("overproduction", sellTile.TooltipText);
        }

        // --- W4A: currency table rows (alphabetical, Required/Have/Needed) ---

        [Fact]
        public void CurrencyTable_RowsSortedAlphabeticallyByName()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 78, Amount = 250 }, // Fine Rift Essence
                new CurrencyCost { CurrencyId = 79, Amount = 50 },  // Rare Rift Essence
                new CurrencyCost { CurrencyId = 80, Amount = 100 }  // Masterwork Rift Essence
            });
            var vm = _builder.Build(result);

            var ccRows = vm.Sections[0].Rows.Where(r => r.RowType == PlanRowType.CurrencyCost).ToList();
            Assert.Equal(3, ccRows.Count);
            Assert.Equal("Fine Rift Essence", ccRows[0].Label);
            Assert.Equal("Masterwork Rift Essence", ccRows[1].Label);
            Assert.Equal("Rare Rift Essence", ccRows[2].Label);
        }

        [Fact]
        public void CurrencyTable_LabelIsNameOnly_RequiredMovedToQuantity()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 50 }
            });
            var vm = _builder.Build(result);

            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("Spirit Shards", ccRow.Label);
            Assert.Equal(50, ccRow.Quantity);
        }

        [Fact]
        public void CurrencyTable_HaveIsUnclamped_ExceedsRequired()
        {
            // W4A (user-mandated): the pre-W4A behavior clamped this to
            // 500 (the Required amount) - the redesigned "Have" column
            // must show the REAL holding instead.
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 500 }
            });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 23, 999999 } };

            var vm = _builder.Build(result);
            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);

            Assert.Equal(999999, ccRow.CurrencyOwnedQuantity);
            Assert.Equal(500, ccRow.Quantity);
        }

        [Fact]
        public void CurrencyTable_HaveCoversRequired_NeededZeroAndFullyCoveredTrue()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 500 }
            });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 23, 999999 } };

            var vm = _builder.Build(result);
            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);

            Assert.Equal(0, ccRow.CurrencyNeededQuantity);
            Assert.True(ccRow.CurrencyFullyCovered);
        }

        [Fact]
        public void CurrencyTable_HaveExactlyEqualsRequired_FullyCoveredTrue()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 200 }
            });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 23, 200 } };

            var vm = _builder.Build(result);
            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);

            Assert.Equal(0, ccRow.CurrencyNeededQuantity);
            Assert.True(ccRow.CurrencyFullyCovered);
        }

        [Fact]
        public void CurrencyTable_HaveBelowRequired_NeededIsGapAndNotCovered()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 500 }
            });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 23, 200 } };

            var vm = _builder.Build(result);
            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);

            Assert.Equal(200, ccRow.CurrencyOwnedQuantity);
            Assert.Equal(300, ccRow.CurrencyNeededQuantity);
            Assert.False(ccRow.CurrencyFullyCovered);
        }

        [Fact]
        public void CurrencyTable_NoWalletData_HaveAndNeededNullNotCovered()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 500 }
            });

            var vm = _builder.Build(result);
            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);

            Assert.Null(ccRow.CurrencyOwnedQuantity);
            Assert.Null(ccRow.CurrencyNeededQuantity);
            Assert.False(ccRow.CurrencyFullyCovered);
        }

        [Fact]
        public void CurrencyTable_WalletMissingThisCurrencyId_HaveAndNeededNull()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 500 }
            });
            result.OwnedCurrencyAmounts = new Dictionary<int, int> { { 2, 100 } }; // different currency id

            var vm = _builder.Build(result);
            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);

            Assert.Null(ccRow.CurrencyOwnedQuantity);
            Assert.Null(ccRow.CurrencyNeededQuantity);
            Assert.False(ccRow.CurrencyFullyCovered);
        }

        [Fact]
        public void CurrencyTable_AstralAcclaim_CorrectName()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 63, Amount = 375 }
            });
            var vm = _builder.Build(result);

            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("Astral Acclaim", ccRow.Label);
            Assert.Equal(375, ccRow.Quantity);
        }

        [Fact]
        public void CurrencyTable_UnknownCurrency_NoIdDisplayed()
        {
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 99999, Amount = 10 }
            });
            var vm = _builder.Build(result);

            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("Currency", ccRow.Label);
            Assert.DoesNotContain("99999", ccRow.Label);
            Assert.Equal(10, ccRow.Quantity);
        }

        // --- Currency icons (M30 #3) ---

        [Fact]
        public void CurrencyTable_IconUrlFromMetadata_WhenPresent()
        {
            var currencyMeta = new Dictionary<int, CurrencyMetadata>
            {
                [23] = new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shards", IconUrl = "spirit_shard.png" }
            };
            var result = MakeResult(
                currencyCosts: new List<CurrencyCost> { new CurrencyCost { CurrencyId = 23, Amount = 50 } },
                currencyMetadata: currencyMeta);
            var vm = _builder.Build(result);

            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("spirit_shard.png", ccRow.IconUrl);
            Assert.Equal("Spirit Shards", ccRow.Label);
        }

        [Fact]
        public void CurrencyTable_IconUrlNull_WhenMetadataAbsent()
        {
            // No CurrencyMetadata supplied at all (e.g. the pipeline was not
            // wired with a CurrencyMetadataService) - row must render
            // exactly as it did before icons existed: text-only, no guess.
            var result = MakeResult(currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 50 }
            });
            var vm = _builder.Build(result);

            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Null(ccRow.IconUrl);
            Assert.Equal("Spirit Shards", ccRow.Label);
        }

        [Fact]
        public void CurrencyTable_IconUrlNull_WhenIdMissingFromMetadata()
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

            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Null(ccRow.IconUrl);
            Assert.Equal("Spirit Shards", ccRow.Label);
        }

        [Fact]
        public void CurrencyTable_NamePrefersMetadataOverConstantsFallback()
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

            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("Spirit Shard (Live)", ccRow.Label);
            Assert.Equal(7, ccRow.Quantity);
        }

        [Fact]
        public void CurrencyTable_IdPresentButEmptyNameAndIcon_FallsBackToConstantsAndNullIcon()
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

            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("Spirit Shards", ccRow.Label);
            Assert.Null(ccRow.IconUrl);
        }

        [Fact]
        public void CurrencyTable_UnknownId_FallsBackToGeneric_EvenWithMetadataPresent()
        {
            var currencyMeta = new Dictionary<int, CurrencyMetadata>
            {
                [2] = new CurrencyMetadata { CurrencyId = 2, Name = "Karma", IconUrl = "karma.png" }
            };
            var result = MakeResult(
                currencyCosts: new List<CurrencyCost> { new CurrencyCost { CurrencyId = 99999, Amount = 10 } },
                currencyMetadata: currencyMeta);
            var vm = _builder.Build(result);

            var ccRow = vm.Sections[0].Rows.Single(r => r.RowType == PlanRowType.CurrencyCost);
            Assert.Equal("Currency", ccRow.Label);
            Assert.Null(ccRow.IconUrl);
        }

        // --- W4A: footnote row ---

        [Fact]
        public void Footnote_AlwaysPresentAsLastRow()
        {
            var result = MakeResult(totalCoinCost: 500, currencyCosts: new List<CurrencyCost>
            {
                new CurrencyCost { CurrencyId = 23, Amount = 50 }
            });
            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal(PlanRowType.SummaryFootnote, rows[rows.Count - 1].RowType);
            Assert.Equal(
                "Prices are Trading Post data - actual purchase and sale prices are likely to vary.",
                rows[rows.Count - 1].Label);
        }

        // --- AUDIT ROW 20/38 review-fix (TEST GAP): PlanViewModel.PriceBasis pass-through ---
        //
        // PlanViewModelBuilder.Build's `PriceBasis = result.PriceBasis`
        // assignment is the SOLE feed for TreeSectionController's fell-
        // back-price tooltip caveat, which renders one of two OPPOSITE
        // sentences depending on this value ("Buy-order price unavailable
        // - instant-buy price shown" vs. "Instant-buy price unavailable -
        // buy-order price shown"). Nothing previously asserted the
        // assignment itself, so deleting it (or any future refactor that
        // silently drops it) would leave every BuyOrder-basis plan
        // rendering the InstantBuy-basis sentence - the exact inverse
        // claim - with no test failing. BuyOrder is asserted explicitly
        // (not the enum's own default of InstantBuy = 0) so a dropped
        // assignment, which would leave vm.PriceBasis at its own default
        // of InstantBuy, cannot coincidentally satisfy the assertion.

        [Fact]
        public void Build_PriceBasisBuyOrder_PassedThroughToViewModel()
        {
            var result = MakeResult();
            result.PriceBasis = PriceBasis.BuyOrder;

            var vm = _builder.Build(result);

            Assert.Equal(PriceBasis.BuyOrder, vm.PriceBasis);
        }

        [Fact]
        public void Build_PriceBasisInstantBuy_PassedThroughToViewModel()
        {
            var result = MakeResult();
            result.PriceBasis = PriceBasis.InstantBuy;

            var vm = _builder.Build(result);

            Assert.Equal(PriceBasis.InstantBuy, vm.PriceBasis);
        }
    }
}
