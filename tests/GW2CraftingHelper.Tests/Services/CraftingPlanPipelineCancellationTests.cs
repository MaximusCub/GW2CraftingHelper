using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class CraftingPlanPipelineCancellationTests
    {
        // --- Pipeline-level cancellation,
        // dependency-throws (degrade vs abort), and Ignore x owned-materials
        // interaction coverage. Every existing test above calls
        // GenerateStructuredAsync with CancellationToken.None and a fully-
        // healthy set of in-memory fixtures - nothing here exercised
        // cancellation or a thrown dependency until now. ---

        // KNOWN-ISSUES #31/31c-audit: TradingPostService's
        // AwaitRespectingOwnCancellationAsync races the caller's own ct
        // against the shared upstream fetch it started, throwing promptly
        // without waiting for the fetch to finish. Gating the fake price
        // API's response lets this test cancel deterministically "between
        // phases" (after the recipe tree is built, while the price fetch is
        // still in flight) with no sleep/timing race - the same idiom
        // TradingPostServiceTests' own ConcurrentCalls_*Cancelled* tests
        // already use one layer down.
        [Fact]
        public async Task GenerateStructuredAsync_List_SingleItem_CancelledWhilePriceFetchInFlight_PropagatesCancellation()
        {
            // No recipe for item 1 - simplest leaf-buy tree, so Step 1
            // (build recipe tree) completes synchronously and the pipeline
            // reaches the price fetch immediately.
            var builder = PipelineBuilder.Create()
                .WithPrice(1, buyUnitPrice: 50, sellUnitPrice: 500)
                .WithItem(1, "Copper Ore", "copper.png");

            var gate = new TaskCompletionSource<bool>();
            builder.PriceApi.Gate = gate.Task;

            var pipeline = builder.Build();

            var cts = new CancellationTokenSource();
            var items = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = 1 } };

            // This is the ONE production entry point Module.cs actually
            // calls (see GenerateStructuredAsync's own doc comment) - a
            // single-entry list short-circuits straight to the single-item
            // core, so this also exercises that method's own
            // catch (OperationCanceledException) { ...; throw; } vs
            // catch (Exception) { ...; throw; } distinction.
            var planTask = pipeline.GenerateStructuredAsync(
                items, null, cts.Token, priceBasis: PriceBasis.InstantBuy);

            cts.Cancel();
            gate.SetResult(true); // release the now-abandoned fetch so nothing is left hanging

            await Assert.ThrowsAsync<OperationCanceledException>(() => planTask);
            Assert.True(planTask.IsCanceled);
        }

        // Same race, through the genuine 2+ item path (GenerateStructuredMultiAsync)
        // instead of the single-item short-circuit - a separate method with
        // its own step sequence, worth proving independently.
        [Fact]
        public async Task GenerateStructuredAsync_List_MultiItem_CancelledWhilePriceFetchInFlight_PropagatesCancellation()
        {
            // No recipes for items 1/2 - both are simplest leaf-buy trees.
            var builder = PipelineBuilder.Create()
                .WithPrice(1, buyUnitPrice: 50, sellUnitPrice: 500)
                .WithPrice(2, buyUnitPrice: 20, sellUnitPrice: 200)
                .WithItem(1, "Copper Ore", "copper.png")
                .WithItem(2, "Iron Ore", "iron.png");

            var gate = new TaskCompletionSource<bool>();
            builder.PriceApi.Gate = gate.Task;

            var pipeline = builder.Build();

            var cts = new CancellationTokenSource();
            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 },
            };

            var planTask = pipeline.GenerateStructuredAsync(
                items, null, cts.Token, priceBasis: PriceBasis.InstantBuy);

            cts.Cancel();
            gate.SetResult(true);

            await Assert.ThrowsAsync<OperationCanceledException>(() => planTask);
            Assert.True(planTask.IsCanceled);
        }

        // Builds a pipeline whose target item (id 1) crafts from
        // `ingredientCount` distinct, individually-priced/metadata'd leaf
        // ingredient items - large enough to exceed TradingPostService's and
        // ItemMetadataService's shared BatchSize (200), so a single bad
        // batch's documented degrade-vs-abort boundary (KNOWN-ISSUES
        // #31/api-degradation F2/F3) is observable end to end through the real
        // pipeline, not just at TradingPostServiceTests'/
        // ItemMetadataServiceTests' own service-level unit tests.
        private static CraftingPlanPipeline BuildManyLeafIngredientsPipeline(
            int ingredientCount,
            out InMemoryPriceApiClient priceApi,
            out InMemoryItemApiClient itemApi)
        {
            var builder = PipelineBuilder.Create()
                .WithSearchResult(1, 10)
                .WithPrice(1, buyUnitPrice: 1, sellUnitPrice: 2)
                .WithItem(1, "Target", "t.png");

            priceApi = builder.PriceApi;
            itemApi = builder.ItemApi;

            var ingredients = new List<RawIngredient>(ingredientCount);
            for (int i = 0; i < ingredientCount; i++)
            {
                int id = 1000 + i;
                ingredients.Add(new RawIngredient { Type = "Item", Id = id, Count = 1 });
                builder.WithPrice(id, buyUnitPrice: 1, sellUnitPrice: 2);
                builder.WithItem(id, "Ingredient " + id, "i.png");
            }

            return builder
                .WithRecipe(new RawRecipe
                {
                    Id = 10,
                    OutputItemId = 1,
                    OutputItemCount = 1,
                    Ingredients = ingredients,
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                })
                .Build();
        }

        // KNOWN-ISSUES #31/api-degradation F2: TradingPostService degrades a
        // single failing batch to missing prices instead of aborting the
        // whole GetPricesAsync call. This proves that degrade behavior
        // survives being called THROUGH the pipeline, not just at
        // TradingPostServiceTests.OneBatchFails_DegradesToHolesInsteadOfAbortingWholeCall's
        // own service-level test.
        [Fact]
        public async Task GenerateStructuredAsync_OneOfManyPriceBatchesFails_DegradesInsteadOfAborting()
        {
            var pipeline = BuildManyLeafIngredientsPipeline(210, out var priceApi, out _);
            priceApi.ThrowOnCallNumber = 2; // second of two sequential batches fails

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            // Proves the multi-batch scenario was genuinely exercised (not a
            // vacuous pass that would also hold if BatchSize ever changed to
            // no longer split 211 ids into two calls) - the second batch's
            // failure must not have short-circuited the fetch into only
            // attempting one batch.
            Assert.Equal(2, priceApi.Calls.Count);
            // The actual "degrades, does not abort" claim: the pipeline
            // still completed and produced a usable plan despite the second
            // batch's failure, rather than propagating it as a thrown
            // exception (see the AllPriceBatchesFail sibling test below for
            // the total-outage case, which DOES throw).
            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);
        }

        // KNOWN-ISSUES #31/api-degradation F2's other half: a genuine total
        // price-API outage (every batch fails) must still surface as a
        // thrown exception through the pipeline, not silently degrade to an
        // all-unpriceable "success".
        [Fact]
        public async Task GenerateStructuredAsync_AllPriceBatchesFail_AbortsInsteadOfSilentlyDegrading()
        {
            // No recipe for item 1 - simplest leaf-buy tree.
            var builder = PipelineBuilder.Create()
                .WithPrice(1, buyUnitPrice: 50, sellUnitPrice: 500)
                .WithItem(1, "Copper Ore", "copper.png");
            builder.PriceApi.ThrowAlways = true;

            var pipeline = builder.Build();

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy));
        }

        // KNOWN-ISSUES #31/api-degradation F3: ItemMetadataService degrades a
        // single failing first-wave batch (retry wave/seed fallback/
        // omission) instead of aborting GetMetadataAsync entirely. Same
        // large-fixture shape as the price-side degrade test above, proven
        // through the real pipeline.
        [Fact]
        public async Task GenerateStructuredAsync_OneOfManyMetadataBatchesFails_DegradesInsteadOfAborting()
        {
            var pipeline = BuildManyLeafIngredientsPipeline(210, out _, out var itemApi);
            itemApi.ThrowOnCallNumber = 2; // second of two sequential first-wave batches fails

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            // Proves the multi-batch scenario was genuinely exercised (at
            // least the 2 first-wave batches; a 3rd retry-wave call is
            // possible per ItemMetadataService's own degrade behavior).
            Assert.True(itemApi.Calls.Count >= 2);
            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);
        }

        // KNOWN-ISSUES #31/api-degradation F3's other half: a genuine total item
        // API outage (the only first-wave batch fails) must still surface
        // as a thrown exception through the pipeline.
        [Fact]
        public async Task GenerateStructuredAsync_AllMetadataBatchesFail_AbortsInsteadOfSilentlyDegrading()
        {
            // No recipe for item 1 - simplest leaf-buy tree, single item
            // metadata batch.
            var builder = PipelineBuilder.Create()
                .WithPrice(1, buyUnitPrice: 50, sellUnitPrice: 500)
                .WithItem(1, "Copper Ore", "copper.png");
            builder.ItemApi.ThrowOnCallNumber = 1; // the sole first-wave batch fails

            var pipeline = builder.Build();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy));
        }

        // KNOWN-ISSUES #20.4's "Conservative reading": Ignore (per-solve,
        // keyed by ItemId, zeroes cost via PlanSolver's ignoredItemIds) and
        // ownership (InventoryReducer, runs BEFORE Solve, zeroes cost by
        // reducing node.Quantity) are two independently-evolved mechanisms.
        // Unlike ResolveWithOverrides_IgnoredItemIds_ZeroesIngredientCost
        // above (which deliberately tests Ignore alone, "no snapshot"), this
        // combines both: 3 of 5 needed units are genuinely owned via a real
        // reduction, and the same ingredient id is then also Ignored on a
        // later local re-solve.
        [Fact]
        public async Task ResolveWithOverrides_IgnoredItemIds_PartiallyOwnedIngredient_ShowsBothOwnedAndIgnored()
        {
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out var priceApi, ingredientCount: 5);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // BuyInstant (craft-cost basis) = 100

            // Own 3 of the 5 needed via a real reduction.
            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, PipelineBuilder.OwnIngredient(3), CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
            Assert.Equal(200, initial.Plan.TotalCoinCost); // (5-3) x 100 = 200, unaffected by Ignore
            Assert.Equal(3, initial.CraftingTree.Children[0].OwnedQuantityUsed);
            Assert.False(initial.CraftingTree.Children[0].IsIgnored);

            var resolved = pipeline.ResolveWithOverrides(
                initial.SolveContext, null, new HashSet<int> { 2 });

            // Ignore zeroes cost outright, same as with no ownership at all -
            // it does not matter that 3 of the 5 were already owned.
            Assert.Equal(0, resolved.Plan.TotalCoinCost);
            Assert.DoesNotContain(resolved.Plan.Steps, s => s.ItemId == 2);

            // Both mechanisms leave their own mark on the same node:
            // CraftingTreeBuilder.BuildNode sets OwnedQuantityUsed
            // unconditionally BEFORE its IsIgnored early return, so both
            // survive on the same CraftingTreeNode simultaneously.
            var ingredientNode = resolved.CraftingTree.Children[0];
            Assert.Equal(CraftingDecision.Have, ingredientNode.Decision);
            Assert.True(ingredientNode.IsIgnored);
            Assert.Equal(3, ingredientNode.OwnedQuantityUsed);

            // The top-level UsedMaterials list (set once at generation/
            // reduction time) is untouched by the later Ignore re-solve.
            Assert.Single(resolved.UsedMaterials);
            Assert.Equal(3, resolved.UsedMaterials[0].QuantityUsed);
        }
    }
}
