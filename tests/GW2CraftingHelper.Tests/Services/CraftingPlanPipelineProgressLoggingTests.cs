using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingPlanPipelineProgressLoggingTests
    {
        // --- Generation progress + rich logging ---

        [Fact]
        public async Task GenerateStructuredAsync_ReportsPhaseEventsInOrderWithSanePayloads()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 5000, sellUnitPrice: 10000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var phaseProgress = new CapturingProgress<PlanPhaseEvent>();

            await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy, phaseProgress: phaseProgress);

            var expectedOrder = new[]
            {
                PlanPhase.BuildingTree,
                PlanPhase.FetchingPrices,
                PlanPhase.SolvingDecisions,
                PlanPhase.FetchingItemDetails,
                PlanPhase.BuildingDisplay
            };

            Assert.Equal(expectedOrder.Length, phaseProgress.Reports.Count);
            for (int i = 0; i < expectedOrder.Length; i++)
            {
                Assert.Equal(expectedOrder[i], phaseProgress.Reports[i].Phase);
                Assert.False(string.IsNullOrEmpty(phaseProgress.Reports[i].DisplayName));
                // Phase-level granularity only in v1 - no per-item Done
                // count on any event (see PlanPhaseEvent.Done's own doc
                // comment).
                Assert.Null(phaseProgress.Reports[i].Done);
            }

            // FetchingPrices/FetchingItemDetails know an up-front item
            // count; the other three phases do not.
            Assert.True(phaseProgress.Reports[1].Total > 0);
            Assert.True(phaseProgress.Reports[3].Total > 0);
            Assert.Null(phaseProgress.Reports[0].Total);
            Assert.Null(phaseProgress.Reports[2].Total);
            Assert.Null(phaseProgress.Reports[4].Total);
        }

        [Fact]
        public async Task GenerateStructuredAsync_NullPhaseProgress_ProducesCompleteResult()
        {
            // Same fixture and absolute expectations as
            // Structured_TargetHasBuyOrders_ProfitFieldsComputed - a null
            // phaseProgress must not degrade the result.
            var pipeline = PipelineBuilder.BuildEconomicsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                phaseProgress: null);

            Assert.Equal(300, result.Plan.TotalCoinCost);
            Assert.Equal(2, result.Plan.Steps.Count);
            Assert.Equal(340, result.NetSaleValue);
            Assert.Equal(40, result.CraftingProfit);
        }

        [Fact]
        public async Task GenerateStructuredMultiAsync_ReportsPhaseEventsInOrder()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 1 }
                }
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 4, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 60, sellUnitPrice: 1200);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(4, buyUnitPrice: 20, sellUnitPrice: 200);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item A", "targeta.png");
            itemApi.AddItem(2, "Target Item B", "targetb.png");
            itemApi.AddItem(3, "Ingredient A", "ingredienta.png");
            itemApi.AddItem(4, "Ingredient B", "ingredientb.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };

            var phaseProgress = new CapturingProgress<PlanPhaseEvent>();

            await pipeline.GenerateStructuredAsync(
                items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                phaseProgress: phaseProgress);

            var expectedOrder = new[]
            {
                PlanPhase.BuildingTree,
                PlanPhase.FetchingPrices,
                PlanPhase.SolvingDecisions,
                PlanPhase.FetchingItemDetails,
                PlanPhase.BuildingDisplay
            };
            Assert.Equal(expectedOrder.Length, phaseProgress.Reports.Count);
            for (int i = 0; i < expectedOrder.Length; i++)
            {
                Assert.Equal(expectedOrder[i], phaseProgress.Reports[i].Phase);
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_List_WritesRichModuleLogEntries_IntoRealTempDirStore()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                // Isolated instance (not ModuleLog.Shared) - see ModuleLog's
                // own class doc comment on why Shared is unsuitable for
                // exact-count/content assertions.
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);
                // Debug entries only reach the file sink when this is true
                // (see ModuleLog.ShouldWriteToFile) - the per-phase Debug
                // lines this test asserts on need it.
                log.DiagnosticsEnabled = true;

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    moduleLog: log);

                var items = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = 1 } };

                await pipeline.GenerateStructuredAsync(
                    items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                    requestLabel: "Orrax Manifested x1");

                // The file-sink append happens on a background flush queue
                // (never on the calling thread) - only guaranteed to have
                // landed once this returns true.
                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(5)));
                var entries = store.ReadAll();

                // Info on start: real item name + quantity, never an
                // internal item id.
                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message == "Generating plan for Orrax Manifested x1");

                // Debug: one bounded entry per phase as it completes
                // (timing + counts where known) - exactly 5, matching
                // PlanPhase's 5 values, no per-item spam.
                var phaseDebugEntries = entries
                    .Where(e => e.Level == ModuleLogLevel.Debug && e.Tag == "plan")
                    .ToList();
                Assert.Equal(5, phaseDebugEntries.Count);
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Building recipe tree:") && e.Message.Contains("ms"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Fetching prices:") && e.Message.Contains("items"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Solving decisions:") && e.Message.Contains("ms"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Fetching item details:") && e.Message.Contains("items"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Building display:") && e.Message.Contains("ms"));

                // Info on finish: one compact per-phase summary line,
                // naming the plan by the same label the start line used.
                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message.StartsWith("Plan for Orrax Manifested x1: tree ")
                    && e.Message.Contains("prices ") && e.Message.Contains("solve ")
                    && e.Message.Contains("item details ") && e.Message.Contains("display ")
                    && e.Message.Contains(" - total "));

                // Every entry this run wrote used the "plan" category, per
                // the milestone's own rich-logging contract.
                Assert.All(entries, e => Assert.Equal("plan", e.Tag));
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_List_MultiItem_WritesRichModuleLogEntries_IntoRealTempDirStore()
        {
            // The 1-item rich-ModuleLog test above only
            // exercises the list overload's single-entry short-circuit (see
            // GenerateStructuredAsync's own doc comment), which delegates
            // straight to the untouched single-item overload - this covers
            // the GENUINE 2+ item multi-item path
            // (GenerateStructuredMultiAsync) end to end against a real
            // ModuleLog + ModuleLogStore in a temp dir, mirroring
            // GenerateStructuredMultiAsync_ReportsPhaseEventsInOrder's own
            // fakes above.
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 1 }
                }
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 4, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 60, sellUnitPrice: 1200);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(4, buyUnitPrice: 20, sellUnitPrice: 200);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item A", "targeta.png");
            itemApi.AddItem(2, "Target Item B", "targetb.png");
            itemApi.AddItem(3, "Ingredient A", "ingredienta.png");
            itemApi.AddItem(4, "Ingredient B", "ingredientb.png");

            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);
                log.DiagnosticsEnabled = true;

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    moduleLog: log);

                var items = new List<PlanRequestItem>
                {
                    new PlanRequestItem { ItemId = 1, Quantity = 1 },
                    new PlanRequestItem { ItemId = 2, Quantity = 1 }
                };

                await pipeline.GenerateStructuredAsync(
                    items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                    requestLabel: "Target Item A x1, Target Item B x1");

                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(5)));
                var entries = store.ReadAll();

                // Info on start: the real multi-item label, never an
                // internal item id or the "(N items)" fallback wording.
                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message == "Generating plan for Target Item A x1, Target Item B x1");

                // Debug: one bounded entry per phase as it completes -
                // exactly 5, same as the single-item path, confirming the
                // multi-item branch drives the SAME PhaseTracker.
                var phaseDebugEntries = entries
                    .Where(e => e.Level == ModuleLogLevel.Debug && e.Tag == "plan")
                    .ToList();
                Assert.Equal(5, phaseDebugEntries.Count);
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Building recipe tree:") && e.Message.Contains("ms"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Fetching prices:") && e.Message.Contains("items"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Solving decisions:") && e.Message.Contains("ms"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Fetching item details:") && e.Message.Contains("items"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Building display:") && e.Message.Contains("ms"));

                // Info on finish: the compact per-phase summary line, named
                // by the same multi-item label the start line used.
                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message.StartsWith("Plan for Target Item A x1, Target Item B x1: tree ")
                    && e.Message.Contains("prices ") && e.Message.Contains("solve ")
                    && e.Message.Contains("item details ") && e.Message.Contains("display ")
                    && e.Message.Contains(" - total "));

                Assert.All(entries, e => Assert.Equal("plan", e.Tag));
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_List_NoRequestLabel_FallsBackToItemCountWording()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Item", "icon.png");

            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    moduleLog: log);

                var items = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = 1 } };

                // No requestLabel supplied - matches every caller
                // (including any future non-UI caller) that bypasses
                // CraftingPlanView's item-name resolution.
                await pipeline.GenerateStructuredAsync(
                    items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(5)));
                var entries = store.ReadAll();

                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message == "Generating plan for 1 item");
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_List_FinishSummary_IncludesWallClockTotalDistinctFromPhaseSum()
        {
            // The finish summary's "total" used to be the
            // SUM of the raw per-step timing lines, which necessarily
            // excludes every un-instrumented gap between them and so
            // silently under-reports the wall-clock duration a field
            // tester actually experiences. It must now show the wrapper's
            // own Stopwatch elapsed time as "total", with the phase sum
            // appended alongside as "(phases Nms)" - see
            // PlanPhaseTimingSummary.FormatCompactSummary's own doc
            // comment.
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    moduleLog: log);

                var items = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = 1 } };

                await pipeline.GenerateStructuredAsync(
                    items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                    requestLabel: "Target x1");

                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(5)));
                var entries = store.ReadAll();

                var finishEntry = entries.Single(e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message.StartsWith("Plan for Target x1:"));

                Assert.Contains(" - total ", finishEntry.Message);
                // The phase sum is now a parenthetical alongside the real
                // wall-clock total, never the total itself.
                Assert.Contains("ms (phases ", finishEntry.Message);
                Assert.EndsWith("ms)", finishEntry.Message);
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_RecipeDiscoveryDiagnostic_ReachesModuleLog_EvenWithNullPlanStatusProgress()
        {
            // CraftingPlanView now passes progress: null
            // (IProgress<PlanStatus>) on every real Generate click - the
            // coarse phase events replace PlanStatus for the live status
            // strip. RecipeService.OnStatusUpdate's "first run" diagnostic
            // must still reach ModuleLog in that case instead of being
            // silently lost. A fresh RecipeService's default
            // InMemoryRecipeCacheStore starts empty, so the very first
            // search deterministically misses (SearchMisses > SearchHits),
            // which is exactly the condition RecipeService.PreWarmCacheAsync
            // uses to report this message.
            var recipeApi = new InMemoryRecipeApiClient();
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Item", "icon.png");

            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    moduleLog: log);

                await pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, progress: null,
                    priceBasis: PriceBasis.InstantBuy);

                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(5)));
                var entries = store.ReadAll();

                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message.Contains("Discovering recipes from API"));
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_BuildingTreePhaseEvent_CarriesFirstRunHintAsDetail()
        {
            // The old "(may take several seconds on
            // first run)" PlanStatus hint is unreachable once the view
            // passes progress: null - it must still surface somewhere live,
            // via PlanPhaseEvent.Detail on the BuildingTree event (see
            // PlanStripTickDecision.FormatPhaseText).
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var phaseProgress = new CapturingProgress<PlanPhaseEvent>();

            await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy, phaseProgress: phaseProgress);

            var treeEvent = phaseProgress.Reports.Single(r => r.Phase == PlanPhase.BuildingTree);
            Assert.False(string.IsNullOrEmpty(treeEvent.Detail));
            Assert.Contains("first run", treeEvent.Detail);

            // Every OTHER phase carries no Detail - reserved for the
            // BuildingTree first-run hint only (v1 scope).
            foreach (var report in phaseProgress.Reports)
            {
                if (report.Phase != PlanPhase.BuildingTree)
                {
                    Assert.Null(report.Detail);
                }
            }
        }
    }
}
