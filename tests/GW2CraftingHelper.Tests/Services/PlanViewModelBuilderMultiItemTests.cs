using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.CraftingPlanResultBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanViewModelBuilderMultiItemTests
    {
        private readonly PlanViewModelBuilder _builder = new PlanViewModelBuilder();

        // --- Multi-item plans (M35, gw2efficiency parity) ---

        private static CraftingTreeNode RootNode(int nodeId, int itemId, string name)
        {
            return new CraftingTreeNode
            {
                NodeId = nodeId,
                ItemId = itemId,
                Name = name,
                Quantity = 1,
                Decision = CraftingDecision.Craft
            };
        }

        [Fact]
        public void SingleItemRequest_RequestedItemsNull_UsesSingleItemBranchUnchanged()
        {
            // RequestedItems is null even when the caller went through the
            // multi-item entry point with exactly one item (the pipeline's
            // own short-circuit never populates it) - PlanViewModelBuilder
            // must not treat a null/absent list as "multi-item".
            var meta = MetaFor((1, "Zojja's Claymore", "claymore.png"));
            var result = MakeResult(targetItemId: 1, targetQuantity: 5, metadata: meta);

            var vm = _builder.Build(result);

            Assert.Equal(5, vm.TargetQuantity);
            Assert.Equal("Zojja's Claymore", vm.TargetItemName);
            Assert.Null(vm.MultiItemRoots);
        }

        [Fact]
        public void MultiItemRequest_TwoOrMoreItems_PopulatesMultiItemRootsNotTreeRoot()
        {
            var meta = MetaFor((1, "Gift of Exordium", "a.png"), (2, "Second Item", "b.png"));
            var roots = new List<CraftingTreeNode>
            {
                RootNode(10, 1, "Gift of Exordium"),
                RootNode(11, 2, "Second Item")
            };
            var requested = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 3 }
            };
            var result = MakeResult(metadata: meta, requestedItems: requested, multiItemRoots: roots);

            var vm = _builder.Build(result);

            Assert.Null(vm.TreeRoot);
            Assert.NotNull(vm.MultiItemRoots);
            Assert.Equal(2, vm.MultiItemRoots.Count);
            Assert.Same(roots[0], vm.MultiItemRoots[0]);
            Assert.Same(roots[1], vm.MultiItemRoots[1]);
        }

        [Fact]
        public void MultiItemRequest_TargetQuantitySuppressedToZero()
        {
            var meta = MetaFor((1, "A", "a.png"), (2, "B", "b.png"));
            var requested = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };
            var result = MakeResult(targetQuantity: 999, metadata: meta, requestedItems: requested,
                multiItemRoots: new List<CraftingTreeNode> { RootNode(1, 1, "A"), RootNode(2, 2, "B") });

            var vm = _builder.Build(result);

            Assert.Equal(0, vm.TargetQuantity);
        }

        [Fact]
        public void MultiItemRequest_TitleIsFirstItemNamePlusOthersCount()
        {
            var meta = MetaFor((1, "Gift of Exordium", "a.png"), (2, "B", "b.png"), (3, "C", "c.png"));
            var requested = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 },
                new PlanRequestItem { ItemId = 3, Quantity = 1 }
            };
            var result = MakeResult(metadata: meta, requestedItems: requested,
                multiItemRoots: new List<CraftingTreeNode>
                {
                    RootNode(1, 1, "Gift of Exordium"), RootNode(2, 2, "B"), RootNode(3, 3, "C")
                });

            var vm = _builder.Build(result);

            Assert.Equal("Gift of Exordium and 2 others", vm.TargetItemName);
            Assert.Null(vm.TargetIconUrl);
            Assert.Null(vm.TargetRarity);
        }

        [Fact]
        public void MultiItemRequest_TwoItems_TitleUsesSingularOther()
        {
            var meta = MetaFor((1, "Gift of Exordium", "a.png"), (2, "B", "b.png"));
            var requested = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };
            var result = MakeResult(metadata: meta, requestedItems: requested,
                multiItemRoots: new List<CraftingTreeNode> { RootNode(1, 1, "Gift of Exordium"), RootNode(2, 2, "B") });

            var vm = _builder.Build(result);

            Assert.Equal("Gift of Exordium and 1 other", vm.TargetItemName);
        }

        [Fact]
        public void MultiItemRequest_AppendsMultiItemNoteRowToSummarySection()
        {
            var meta = MetaFor((1, "A", "a.png"), (2, "B", "b.png"));
            var requested = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };
            var result = MakeResult(totalCoinCost: 500, metadata: meta, requestedItems: requested,
                multiItemRoots: new List<CraftingTreeNode> { RootNode(1, 1, "A"), RootNode(2, 2, "B") });
            // M37 review fix: the note row is gated on the SAME
            // result.NetSaleValue.HasValue condition as the Sell value/
            // Profit rows above it - it must never be shown next to zero
            // profit numbers, so this test now provides a live rollup.
            result.NetSaleValue = 850;
            result.CraftingProfit = 550;

            var vm = _builder.Build(result);
            var summaryRows = vm.Sections[0].Rows;

            Assert.Equal(PlanRowType.MultiItemNote, summaryRows[summaryRows.Count - 1].RowType);
        }

        [Fact]
        public void MultiItemRequest_NoQualifyingRoots_NoMultiItemNoteRow()
        {
            // M37 review fix: a multi-item batch where every requested root
            // is excluded from the sell/profit rollup (NetSaleValue stays
            // null) must NOT show the note row - there would be no Sell
            // value/Profit rows above it for the note to describe.
            var meta = MetaFor((1, "A", "a.png"), (2, "B", "b.png"));
            var requested = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };
            var result = MakeResult(totalCoinCost: 500, metadata: meta, requestedItems: requested,
                multiItemRoots: new List<CraftingTreeNode> { RootNode(1, 1, "A"), RootNode(2, 2, "B") });

            var vm = _builder.Build(result);
            var summaryRows = vm.Sections[0].Rows;

            Assert.DoesNotContain(summaryRows, r => r.RowType == PlanRowType.MultiItemNote);
        }

        [Fact]
        public void SingleItemRequest_NoMultiItemNoteRow()
        {
            var result = MakeResult(totalCoinCost: 500);
            var vm = _builder.Build(result);
            var summaryRows = vm.Sections[0].Rows;

            Assert.DoesNotContain(summaryRows, r => r.RowType == PlanRowType.MultiItemNote);
        }

        // --- Multi-item batch sell-side economics (M37, KNOWN-ISSUES #25) ---

        private static List<PlanRequestItem> TwoRequestedItems()
        {
            return new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };
        }

        private static List<CraftingTreeNode> TwoRoots()
        {
            return new List<CraftingTreeNode> { RootNode(1, 1, "A"), RootNode(2, 2, "B") };
        }

        [Fact]
        public void MultiItemRequest_SellValuePresent_AddsBatchWordedSellAndProfitRows()
        {
            var result = MakeResult(
                totalCoinCost: 300, requestedItems: TwoRequestedItems(), multiItemRoots: TwoRoots());
            result.SellableQuantity = 2;
            result.NetSaleValue = 850;
            result.CraftingProfit = 550;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal("Sell value (batch total, after 15% TP fees)", rows[1].Label);
            Assert.Equal(850L, rows[1].CoinValue);
            Assert.Equal("Profit if sold (batch total)", rows[2].Label);
            Assert.Equal(550L, rows[2].CoinValue);
        }

        [Fact]
        public void MultiItemRequest_NegativeProfit_RendersAsLossWithBatchQualifier()
        {
            var result = MakeResult(
                totalCoinCost: 900, requestedItems: TwoRequestedItems(), multiItemRoots: TwoRoots());
            result.NetSaleValue = 340;
            result.CraftingProfit = -160;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal("Loss if sold (batch total)", rows[2].Label);
            Assert.Equal(160L, rows[2].CoinValue);
        }

        [Fact]
        public void MultiItemRequest_CurrencyCostsPresent_ProfitRowGetsBatchAndCoinOnlyQualifier()
        {
            var result = MakeResult(
                totalCoinCost: 100,
                currencyCosts: new List<CurrencyCost> { new CurrencyCost { CurrencyId = 2, Amount = 50 } },
                requestedItems: TwoRequestedItems(), multiItemRoots: TwoRoots());
            result.NetSaleValue = 340;
            result.CraftingProfit = 240;

            var vm = _builder.Build(result);
            var rows = vm.Sections[0].Rows;

            Assert.Equal("Profit if sold (batch total, coin costs only)", rows[2].Label);
        }

        [Fact]
        public void MultiItemRequest_SellRowNeverShowsPerItemQuantityQualifier()
        {
            // Single-item mode shows "Sell value (Nx, ...)" when
            // SellableQuantity overproduces the target quantity - that
            // qualifier has no meaning for a batch SUM across N different
            // items' own quantities, so it must never appear here.
            var result = MakeResult(
                targetQuantity: 1, totalCoinCost: 300,
                requestedItems: TwoRequestedItems(), multiItemRoots: TwoRoots());
            result.SellableQuantity = 5;
            result.NetSaleValue = 1700;
            result.CraftingProfit = 1400;

            var vm = _builder.Build(result);

            Assert.Equal("Sell value (batch total, after 15% TP fees)", vm.Sections[0].Rows[1].Label);
        }

        [Fact]
        public void MultiItemRequest_NoteRowText_DescribesTradableOnlyRollupNotGw2eCraftOnlyBanner()
        {
            // M37 review fix: the batch rollup has NO craft-vs-buy filter
            // (a bought-but-tradable root still contributes - see
            // SellSideEconomics.ApplyBatchSellSideEconomics' own doc
            // comment, divergence item 1), so the note text must not claim
            // gw2e's own "sum of all crafted recipes" wording verbatim -
            // that would be inaccurate here.
            var result = MakeResult(
                totalCoinCost: 500, requestedItems: TwoRequestedItems(), multiItemRoots: TwoRoots());
            result.NetSaleValue = 850;
            result.CraftingProfit = 550;

            var vm = _builder.Build(result);
            var summaryRows = vm.Sections[0].Rows;
            var noteRow = summaryRows[summaryRows.Count - 1];

            Assert.Equal(PlanRowType.MultiItemNote, noteRow.RowType);
            Assert.Equal(
                "Sell value and profit are the sum across every requested item that has a live Trading Post sell price.",
                noteRow.Label);
            Assert.DoesNotContain("crafted recipes", noteRow.Label);
        }
    }
}
