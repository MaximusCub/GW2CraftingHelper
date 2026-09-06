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
    /// What a plan does with the item it was asked for when the account
    /// already owns some of it.
    /// </summary>
    public class PlanRootOwnedStockTests
    {
        private static PipelineBuilder RootAndIngredient()
        {
            return PipelineBuilder.Create()
                .WithSearchResult(1, 10)
                .WithRecipe(new RawRecipe
                {
                    Id = 10,
                    OutputItemId = 1,
                    OutputItemCount = 1,
                    Ingredients = new List<RawIngredient>
                    {
                        new RawIngredient { Type = "Item", Id = 2, Count = 5 },
                    },
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    Flags = new List<string> { "AutoLearned" },
                })
                .WithPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000)
                .WithPrice(2, buyUnitPrice: 10, sellUnitPrice: 100)
                .WithItem(1, "Target", "t.png")
                .WithItem(2, "Ingredient", "i.png")
                .WithInventoryReducer();
        }

        private static SnapshotItemEntry Owned(int itemId, int count)
        {
            return new SnapshotItemEntry
            {
                ItemId = itemId,
                Count = count,
                Source = AccountItemIndex.SourceMaterialStorage,
            };
        }

        [Fact]
        public async Task OwningTheRequestedItem_CollapsesThePlan()
        {
            var pipeline = RootAndIngredient().Build();
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry> { Owned(1, 1) },
            };

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // The one owned copy is spent against the request, so nothing
            // is left to make and the recipe below it is dropped.
            Assert.Equal(0, result.CraftingTree.Quantity);
            Assert.Equal(CraftingDecision.Have, result.CraftingTree.Decision);
            Assert.Equal(1, result.CraftingTree.OwnedQuantityUsed);
            Assert.Empty(result.CraftingTree.Children);
            Assert.Empty(result.Plan.Steps);
            Assert.Equal(0, result.Plan.TotalCoinCost);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(1, result.UsedMaterials[0].ItemId);
        }

        [Fact]
        public async Task OwningPartOfTheRequestedQuantity_PlansOnlyTheShortfall()
        {
            var pipeline = RootAndIngredient().Build();
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry> { Owned(1, 2) },
            };

            var result = await pipeline.GenerateStructuredAsync(
                1, 3, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(1, result.CraftingTree.Quantity);
            // One craft's worth of item 2 at 100 each, not three.
            Assert.Equal(500, result.Plan.TotalCoinCost);
        }

        [Fact]
        public async Task OwnedIngredientBelowTheRoot_IsSubtracted()
        {
            var pipeline = RootAndIngredient().Build();
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry> { Owned(2, 3) },
            };

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            var ingredient = result.CraftingTree.Children.Single(c => c.ItemId == 2);
            Assert.Equal(3, ingredient.OwnedQuantityUsed);
            // 2 of the 5 still bought at 100 each.
            Assert.Equal(200, result.Plan.TotalCoinCost);
        }

        [Fact]
        public async Task EveryRootOfABatch_CollapsesTheSameWay()
        {
            var pipeline = TwoRootBatch();
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry> { Owned(1, 5), Owned(3, 5) },
            };

            var result = await pipeline.GenerateStructuredAsync(
                BatchRequest(), snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(2, result.MultiItemRoots.Count);
            Assert.All(result.MultiItemRoots, r => Assert.Equal(0, r.Quantity));
            Assert.All(result.MultiItemRoots, r => Assert.Equal(CraftingDecision.Have, r.Decision));
            Assert.Equal(0, result.Plan.TotalCoinCost);
        }

        private static CraftingPlanPipeline TwoRootBatch()
        {
            return PipelineBuilder.Create()
                .WithSearchResult(1, 10)
                .WithRecipe(new RawRecipe
                {
                    Id = 10,
                    OutputItemId = 1,
                    OutputItemCount = 1,
                    Ingredients = new List<RawIngredient>
                    {
                        new RawIngredient { Type = "Item", Id = 2, Count = 5 },
                    },
                })
                .WithSearchResult(3, 12)
                .WithRecipe(new RawRecipe
                {
                    Id = 12,
                    OutputItemId = 3,
                    OutputItemCount = 1,
                    Ingredients = new List<RawIngredient>
                    {
                        new RawIngredient { Type = "Item", Id = 2, Count = 2 },
                    },
                })
                .WithPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000)
                .WithPrice(2, buyUnitPrice: 10, sellUnitPrice: 100)
                .WithPrice(3, buyUnitPrice: 400, sellUnitPrice: 1000)
                .WithItem(1, "Target A", "a.png")
                .WithItem(2, "Ingredient", "i.png")
                .WithItem(3, "Target B", "b.png")
                .WithInventoryReducer()
                .Build();
        }

        private static IReadOnlyList<PlanRequestItem> BatchRequest()
        {
            return new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 3, Quantity = 1 },
            };
        }
    }
}
