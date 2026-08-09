using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Newtonsoft.Json;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // W3D (plan persistence across module restarts). Mirrors
    // SnapshotStoreTests' shape (a real store against a real temp
    // directory - no fake file I/O). The round-trip fidelity tests build a
    // real CraftingPlanResult via CraftingPlanPipeline + the fake API
    // clients (InMemoryRecipeApiClient/InMemoryPriceApiClient/
    // InMemoryItemApiClient), matching CraftingPlanPipelineTests' own
    // fixture shape, rather than hand-constructing a CraftingPlanResult -
    // the whole risk this package investigated (PlanSolveContext's
    // interface-typed dictionaries/ISet, CurrencyValuation/
    // HomesteadEfficiencyTiers' non-default constructors, the RecipeNode/
    // CraftingTreeNode trees) only shows up on a REAL pipeline-produced
    // result.
    public class PlanStoreTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly PlanStore _store;

        public PlanStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GW2CraftingHelper_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _store = new PlanStore(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        private static CraftingPlanPipeline BuildPipeline(out InMemoryPriceApiClient priceApi)
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

            priceApi = new InMemoryPriceApiClient();

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));
        }

        private static PersistedPlan Wrap(CraftingPlanResult result, DateTime generatedAt, int quantity = 1, bool useOwn = false, PriceBasis priceBasis = PriceBasis.InstantBuy)
        {
            return new PersistedPlan
            {
                GeneratedAt = generatedAt,
                RequestItems = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = quantity } },
                UseOwnMaterials = useOwn,
                PriceBasis = priceBasis,
                Result = result
            };
        }

        private static string ToJson(object value) => JsonConvert.SerializeObject(value, Formatting.Indented);

        [Fact]
        public async Task Save_Load_RoundTripsResultAsSameViewModel()
        {
            var pipeline = BuildPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            _store.Save(Wrap(result, new DateTime(2026, 8, 9, 10, 30, 0, DateTimeKind.Local)));

            var loaded = _store.LoadLatest();
            Assert.NotNull(loaded);
            Assert.NotNull(loaded.Result);
            Assert.NotSame(result, loaded.Result);

            var vmBuilder = new PlanViewModelBuilder();
            var originalVm = vmBuilder.Build(result);
            var reloadedVm = vmBuilder.Build(loaded.Result);

            Assert.Equal(ToJson(originalVm), ToJson(reloadedVm));
        }

        [Fact]
        public async Task Save_Load_ResolveWithOverrides_MatchesOriginalContext()
        {
            var pipeline = BuildPipeline(out var priceApi);
            // Craft (30) beats buy (1000); gives the override something real to flip.
            priceApi.AddPrice(1, buyUnitPrice: 1000, sellUnitPrice: 2000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
            Assert.NotNull(result.SolveContext);
            Assert.Equal(CraftingDecision.Craft, result.CraftingTree.Decision);

            _store.Save(Wrap(result, DateTime.Now));

            var loaded = _store.LoadLatest();
            Assert.NotNull(loaded?.Result?.SolveContext);

            // Same override, applied to both the original in-memory context
            // and the reloaded-from-disk one - the W3D correctness bar
            // (spec item 3): both must produce identical decisions.
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { result.CraftingTree.NodeId, AcquisitionSource.BuyFromTp }
            };

            var resolvedOriginal = pipeline.ResolveWithOverrides(result.SolveContext, overrides);
            var resolvedReloaded = pipeline.ResolveWithOverrides(loaded.Result.SolveContext, overrides);

            Assert.Equal(AcquisitionSource.BuyFromTp, resolvedOriginal.Plan.Steps[0].Source);
            Assert.Equal(resolvedOriginal.Plan.TotalCoinCost, resolvedReloaded.Plan.TotalCoinCost);
            Assert.Equal(resolvedOriginal.CraftingProfit, resolvedReloaded.CraftingProfit);
            Assert.Equal(resolvedOriginal.CraftingTree.Decision, resolvedReloaded.CraftingTree.Decision);

            var vmBuilder = new PlanViewModelBuilder();
            Assert.Equal(ToJson(vmBuilder.Build(resolvedOriginal)), ToJson(vmBuilder.Build(resolvedReloaded)));
        }

        [Fact]
        public async Task Save_Load_RequestAndTimestampRoundTrip()
        {
            var pipeline = BuildPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(1, 3, null, CancellationToken.None,
                priceBasis: PriceBasis.BuyOrder);

            var timestamp = new DateTime(2026, 8, 9, 14, 22, 0, DateTimeKind.Local);
            _store.Save(Wrap(result, timestamp, quantity: 3, useOwn: true, priceBasis: PriceBasis.BuyOrder));

            var loaded = _store.LoadLatest();
            Assert.NotNull(loaded);
            Assert.Equal(timestamp, loaded.GeneratedAt);
            Assert.True(loaded.UseOwnMaterials);
            Assert.Equal(PriceBasis.BuyOrder, loaded.PriceBasis);
            Assert.NotNull(loaded.RequestItems);
            Assert.Single(loaded.RequestItems);
            Assert.Equal(1, loaded.RequestItems[0].ItemId);
            Assert.Equal(3, loaded.RequestItems[0].Quantity);
        }

        [Fact]
        public async Task Save_AfterOverride_RoundTripsOverriddenResultInPlace()
        {
            var pipeline = BuildPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 1000, sellUnitPrice: 2000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { result.CraftingTree.NodeId, AcquisitionSource.BuyFromTp }
            };
            var overridden = pipeline.ResolveWithOverrides(result.SolveContext, overrides);
            Assert.Equal(AcquisitionSource.BuyFromTp, overridden.Plan.Steps[0].Source);

            // "In place": same GeneratedAt/request as the original Generate,
            // only Result swapped for the override-updated one - mirrors
            // Module.PersistResolvedPlanInBackground's own shape.
            var generatedAt = new DateTime(2026, 8, 9, 9, 0, 0, DateTimeKind.Local);
            _store.Save(Wrap(result, generatedAt));
            _store.Save(Wrap(overridden, generatedAt));

            var loaded = _store.LoadLatest();
            Assert.NotNull(loaded);
            Assert.Equal(generatedAt, loaded.GeneratedAt);
            Assert.NotNull(loaded.Result);
            Assert.Equal(AcquisitionSource.BuyFromTp, loaded.Result.Plan.Steps[0].Source);

            var vmBuilder = new PlanViewModelBuilder();
            Assert.Equal(ToJson(vmBuilder.Build(overridden)), ToJson(vmBuilder.Build(loaded.Result)));
        }

        [Fact]
        public void LoadLatest_MissingFile_ReturnsNull()
        {
            Assert.Null(_store.LoadLatest());
        }

        [Fact]
        public void LoadLatest_CorruptTruncatedJson_ReturnsNullAndLogsWarnNoThrow()
        {
            string filePath = Path.Combine(_tempDir, "plan.json");
            File.WriteAllText(filePath, "{ \"Result\": { \"Plan\": { \"Target");

            string capturedMessage = null;
            Exception capturedException = null;
            var store = new PlanStore(_tempDir, (message, ex) =>
            {
                capturedMessage = message;
                capturedException = ex;
            });

            var loaded = store.LoadLatest();

            Assert.Null(loaded);
            Assert.NotNull(capturedMessage);
            Assert.NotNull(capturedException);
        }

        [Fact]
        public void LoadLatest_WrongSchema_MissingResult_ReturnsNullAndLogsWarn()
        {
            string filePath = Path.Combine(_tempDir, "plan.json");
            File.WriteAllText(filePath, "{ \"GeneratedAt\": \"2026-08-09T00:00:00\", \"UseOwnMaterials\": true }");

            string capturedMessage = null;
            var store = new PlanStore(_tempDir, (message, ex) => capturedMessage = message);

            var loaded = store.LoadLatest();

            Assert.Null(loaded);
            Assert.NotNull(capturedMessage);
        }

        [Fact]
        public void LoadLatest_EmptyFile_ReturnsNull()
        {
            string filePath = Path.Combine(_tempDir, "plan.json");
            File.WriteAllText(filePath, "");

            Assert.Null(_store.LoadLatest());
        }

        [Fact]
        public void Save_Load_ProducesNewInstance()
        {
            var plan = new PersistedPlan
            {
                GeneratedAt = DateTime.Now,
                RequestItems = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 5, Quantity = 2 } },
                UseOwnMaterials = false,
                PriceBasis = PriceBasis.InstantBuy,
                Result = new CraftingPlanResult
                {
                    Plan = new CraftingPlan { TargetItemId = 5, TargetQuantity = 2 }
                }
            };
            _store.Save(plan);

            var loaded = _store.LoadLatest();
            Assert.NotNull(loaded);
            Assert.NotSame(plan, loaded);
            Assert.Equal(5, loaded.Result.Plan.TargetItemId);
        }

        [Fact]
        public void Save_LeavesNoTmpFileBehind()
        {
            var plan = new PersistedPlan
            {
                GeneratedAt = DateTime.Now,
                RequestItems = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = 1 } },
                UseOwnMaterials = false,
                PriceBasis = PriceBasis.InstantBuy,
                Result = new CraftingPlanResult { Plan = new CraftingPlan { TargetItemId = 1, TargetQuantity = 1 } }
            };
            _store.Save(plan);

            string tmpPath = Path.Combine(_tempDir, "plan.json.tmp");
            Assert.False(File.Exists(tmpPath));
        }

        [Fact]
        public void Save_DirectoryCreationFails_InvokesOnErrorInsteadOfThrowing()
        {
            string blockingPath = Path.Combine(_tempDir, "blocked-data-dir");
            File.WriteAllText(blockingPath, "not a directory");

            string capturedMessage = null;
            Exception capturedException = null;
            var store = new PlanStore(blockingPath, (message, ex) =>
            {
                capturedMessage = message;
                capturedException = ex;
            });

            store.Save(new PersistedPlan
            {
                GeneratedAt = DateTime.Now,
                Result = new CraftingPlanResult { Plan = new CraftingPlan() }
            });

            Assert.NotNull(capturedMessage);
            Assert.NotNull(capturedException);
        }
    }
}
