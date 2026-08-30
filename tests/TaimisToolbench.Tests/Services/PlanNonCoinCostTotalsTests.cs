using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The plan's whole NON-COIN price, driven end to end through the real
    /// pipeline, the real solver and the real PlanViewModelBuilder.
    /// <para>
    /// A vendor paid in an untradeable token folds nothing into
    /// CraftingPlan.TotalCoinCost - the token's units ARE the price - so a
    /// plan that reported only its coin figure reported less than it costs.
    /// These tests pin the aggregate that closes that hole and the Total
    /// Cost section that shows it.
    /// </para>
    /// </summary>
    public class PlanNonCoinCostTotalsTests
    {
        private const int Target = 1;
        private const int BarterToken = 99;
        private const int SpiritShardCurrency = 23;

        /// <summary>
        /// Item 1 has no recipe and no Trading Post price, so the vendor
        /// offer below is its only route; the token it costs has neither
        /// either, which is exactly what makes that cost line a BARTER
        /// line rather than money.
        /// </summary>
        private static async Task<CraftingPlanResult> GenerateAsync(
            IEnumerable<CostLine> costLines,
            int outputCount = 1,
            int requestQuantity = 1)
        {
            var builder = PipelineBuilder.Create()
                .WithItem(Target, "Vendor Only Widget", "widget.png")
                .WithItem(BarterToken, "Blue Prophet Shard", "shard.png")
                .WithInventoryReducer();

            using (var tmp = new TempDirectory())
            {
                var store = new VendorOfferStore(tmp.Path, new VendorOfferLoader());
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-barter-w5",
                        OutputItemId = Target,
                        OutputCount = outputCount,
                        CostLines = new List<CostLine>(costLines),
                        MerchantName = "Test NPC",
                        Locations = new List<string>(),
                    },
                });

                return await builder.WithVendorOfferStore(store).Build()
                    .GenerateStructuredAsync(
                        Target, requestQuantity, null, CancellationToken.None,
                        priceBasis: PriceBasis.InstantBuy);
            }
        }

        private static IEnumerable<CostLine> BarterOnly(int count)
        {
            yield return new CostLine { Type = "Item", Id = BarterToken, Count = count };
        }

        private static List<PlanRowViewModel> NonCoinRows(PlanViewModel vm)
        {
            return vm.Sections
                .Single(s => s.SectionType == PlanSectionType.Summary)
                .Rows
                .Where(r => r.RowType == PlanRowType.CurrencyCost)
                .ToList();
        }

        private static List<PlanRowViewModel> Footnotes(PlanViewModel vm)
        {
            return vm.Sections
                .Single(s => s.SectionType == PlanSectionType.Summary)
                .Rows
                .Where(r => r.RowType == PlanRowType.SummaryFootnote)
                .ToList();
        }

        [Fact]
        public async Task BarterOnlyVendorOffer_ReachesThePlanLevelTotal()
        {
            var result = await GenerateAsync(BarterOnly(3), requestQuantity: 2);

            Assert.Equal(AcquisitionSource.BuyFromVendor, Assert.Single(result.Plan.Steps).Source);

            // The whole cost of this plan, and none of it is coin.
            Assert.Equal(0, result.Plan.TotalCoinCost);
            var barter = Assert.Single(result.Plan.BarterItemCosts);
            Assert.Equal(BarterToken, barter.ItemId);
            Assert.Equal(6, barter.Amount);
        }

        [Fact]
        public async Task BarterOnlyVendorOffer_ShowsInTheTotalCostSection()
        {
            var result = await GenerateAsync(BarterOnly(3), requestQuantity: 2);
            var vm = new PlanViewModelBuilder().Build(result);

            var row = Assert.Single(NonCoinRows(vm));
            Assert.True(row.IsBarterItemCost);
            Assert.Equal("Blue Prophet Shard", row.Label);
            Assert.Equal(6, row.Quantity);

            // A wallet knows nothing about an item, so "Have" is unknown -
            // never a fabricated zero, and never a coverage claim.
            Assert.Null(row.CurrencyOwnedQuantity);
            Assert.Null(row.CurrencyNeededQuantity);
            Assert.False(row.CurrencyFullyCovered);

            var total = Assert.Single(vm.NonCoinCostTotals);
            Assert.Equal("Blue Prophet Shard", total.Name);
            Assert.Equal(6, total.Amount);
            Assert.Null(total.OwnedQuantity);
        }

        /// <summary>
        /// A barter cost is a real cost the coin total counts as zero, so
        /// the floor disclosure has to fire on it.
        /// </summary>
        [Fact]
        public async Task BarterCost_FiresTheFloorDisclosure()
        {
            var result = await GenerateAsync(BarterOnly(3), requestQuantity: 2);
            var vm = new PlanViewModelBuilder().Build(result);

            Assert.Contains(
                Footnotes(vm),
                f => f.Label == PlanViewModelBuilder.UnpricedFootnoteText);
        }

        /// <summary>
        /// The merged-ceil contract, on the barter side: a "1 token buys 2"
        /// offer bought for a demand of 3 is TWO purchases, so two tokens -
        /// not the three a per-unit multiplication would report. Same
        /// derivation as the coin and currency totals beside it
        /// (docs/ARCHITECTURE.md, "Merged-ceil vendor batching").
        /// </summary>
        [Fact]
        public async Task BarterTotal_ComesFromTheOffersBatchShape()
        {
            var result = await GenerateAsync(BarterOnly(1), outputCount: 2, requestQuantity: 3);

            var barter = Assert.Single(result.Plan.BarterItemCosts);
            Assert.Equal(2, barter.Amount);

            var step = Assert.Single(result.Plan.Steps);
            Assert.Equal(3, step.Quantity);
            var stepLine = Assert.Single(step.VendorBarterItemCosts);
            Assert.Equal(BarterToken, stepLine.Id);
            Assert.Equal(2, stepLine.Count);
        }

        [Fact]
        public async Task CurrencyAndBarterInOneOffer_BothReachTheTable()
        {
            var result = await GenerateAsync(new[]
            {
                new CostLine { Type = "Item", Id = BarterToken, Count = 2 },
                new CostLine { Type = "Currency", Id = SpiritShardCurrency, Count = 5 },
            });

            Assert.Equal(2, Assert.Single(result.Plan.BarterItemCosts).Amount);
            Assert.Equal(5, Assert.Single(result.Plan.CurrencyCosts).Amount);

            var vm = new PlanViewModelBuilder().Build(result);
            var rows = NonCoinRows(vm);
            Assert.Equal(2, rows.Count);
            Assert.Single(rows, r => r.IsBarterItemCost);
            Assert.Single(rows, r => !r.IsBarterItemCost);

            // Sorted by name across BOTH kinds, and the plan-level list is
            // that same order - it is projected from these very rows.
            Assert.Equal(
                rows.Select(r => r.Label).ToList(),
                vm.NonCoinCostTotals.Select(t => t.Name).ToList());
        }

        /// <summary>
        /// A batch solves ONE synthetic wrapper tree, so its barter total
        /// is the sum across every requested item with nothing to merge -
        /// pinned here because a future batch path that stitched separate
        /// plans together would have to sum this list too.
        /// </summary>
        [Fact]
        public async Task MultiItemBatch_SumsTheBarterCostAcrossRequestedItems()
        {
            const int SecondTarget = 2;
            var builder = PipelineBuilder.Create()
                .WithItem(Target, "Vendor Only Widget", "widget.png")
                .WithItem(SecondTarget, "Other Vendor Widget", "other.png")
                .WithItem(BarterToken, "Blue Prophet Shard", "shard.png")
                .WithInventoryReducer();

            CraftingPlanResult result;
            using (var tmp = new TempDirectory())
            {
                var store = new VendorOfferStore(tmp.Path, new VendorOfferLoader());
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    BarterOffer("test-barter-w5-a", Target, 3),
                    BarterOffer("test-barter-w5-b", SecondTarget, 4),
                });

                result = await builder.WithVendorOfferStore(store).Build()
                    .GenerateStructuredAsync(
                        new List<PlanRequestItem>
                        {
                            new PlanRequestItem { ItemId = Target, Quantity = 2 },
                            new PlanRequestItem { ItemId = SecondTarget, Quantity = 1 },
                        },
                        null,
                        CancellationToken.None,
                        priceBasis: PriceBasis.InstantBuy);
            }

            // 2 x 3 + 1 x 4
            var barter = Assert.Single(result.Plan.BarterItemCosts);
            Assert.Equal(BarterToken, barter.ItemId);
            Assert.Equal(10, barter.Amount);

            var vm = new PlanViewModelBuilder().Build(result);
            Assert.Equal(10, Assert.Single(vm.NonCoinCostTotals).Amount);
        }

        private static VendorOffer BarterOffer(string offerId, int outputItemId, int tokenCount)
        {
            return new VendorOffer
            {
                OfferId = offerId,
                OutputItemId = outputItemId,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Item", Id = BarterToken, Count = tokenCount },
                },
                MerchantName = "Test NPC",
                Locations = new List<string>(),
            };
        }

        /// <summary>
        /// The common case: a plan that costs nothing but coin grows no
        /// table, no disclosure line and no plan-level list.
        /// </summary>
        [Fact]
        public async Task CoinOnlyPlan_AddsNoNonCoinChrome()
        {
            var pipeline = PipelineBuilder.Create()
                .WithPrice(Target, buyUnitPrice: 500, sellUnitPrice: 900)
                .WithItem(Target, "Plain Widget", "widget.png")
                .WithInventoryReducer()
                .Build();

            var result = await pipeline.GenerateStructuredAsync(
                Target, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            Assert.Empty(result.Plan.BarterItemCosts);
            Assert.Empty(result.Plan.CurrencyCosts);

            var vm = new PlanViewModelBuilder().Build(result);
            Assert.Empty(NonCoinRows(vm));
            Assert.Null(vm.NonCoinCostTotals);

            var footnote = Assert.Single(Footnotes(vm));
            Assert.Equal(PlanViewModelBuilder.FootnoteText, footnote.Label);
            Assert.All(
                vm.Sections.Single(s => s.SectionType == PlanSectionType.Summary).Rows
                    .Where(r => r.RowType == PlanRowType.CostFormulaTile),
                t => Assert.DoesNotContain(PlanViewModelBuilder.UnpricedTileMarker, t.Label));
        }
    }
}
