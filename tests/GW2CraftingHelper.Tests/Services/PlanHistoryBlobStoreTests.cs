using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // Real PlanHistoryBlobStore against a real temp directory, round-
    // tripping a REAL pipeline-produced CraftingPlanResult (the
    // interface-typed dictionaries and non-default constructors in
    // PlanSolveContext only fail on a real result) - the same fixture
    // shape as PlanStoreTests.BuildPipeline.
    public class PlanHistoryBlobStoreTests : IDisposable
    {
        private const string EntryId = "0123456789abcdef0123456789abcdef";

        private readonly TempDirectory _temp = new TempDirectory();

        public void Dispose()
        {
            _temp.Dispose();
        }

        private string BlobPath(string entryId) =>
            Path.Combine(_temp.Path, "plan_history", entryId + ".json");

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
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" },
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

        private static async Task<PersistedPlan> BuildRealPlanAsync()
        {
            var pipeline = BuildPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            return new PersistedPlan
            {
                SchemaVersion = PersistedPlan.CurrentSchemaVersion,
                GeneratedAt = new DateTime(2026, 8, 20, 9, 15, 0),
                RequestItems = new List<PlanRequestItem>
                {
                    new PlanRequestItem { ItemId = 1, Quantity = 1 },
                },
                UseOwnMaterials = false,
                PriceBasis = PriceBasis.InstantBuy,
                ValueOwnMaterials = false,
                Result = result,
                NodeOverrides = new Dictionary<int, AcquisitionSource>(),
                IgnoredItemIds = new List<int>(),
            };
        }

        [Fact]
        public async Task SaveThenLoad_RoundTripsARealPipelineResult()
        {
            var plan = await BuildRealPlanAsync();
            var store = new PlanHistoryBlobStore(_temp.Path);

            Assert.True(store.Save(EntryId, plan));
            var loaded = store.Load(EntryId);

            Assert.NotNull(loaded);
            Assert.NotSame(plan, loaded);
            Assert.Equal(plan.GeneratedAt, loaded.GeneratedAt);
            Assert.Equal(PriceBasis.InstantBuy, loaded.PriceBasis);
            Assert.NotNull(loaded.Result?.SolveContext);

            // The restored result renders identically - the same bar
            // PlanStoreTests holds plan.json to.
            var vmBuilder = new PlanViewModelBuilder();
            Assert.Equal(
                Newtonsoft.Json.JsonConvert.SerializeObject(vmBuilder.Build(plan.Result)),
                Newtonsoft.Json.JsonConvert.SerializeObject(vmBuilder.Build(loaded.Result)));
        }

        [Fact]
        public async Task WrittenFile_IsGzip()
        {
            var store = new PlanHistoryBlobStore(_temp.Path);
            Assert.True(store.Save(EntryId, await BuildRealPlanAsync()));

            byte[] bytes = File.ReadAllBytes(BlobPath(EntryId));
            Assert.True(bytes.Length >= 2);
            Assert.Equal(0x1F, bytes[0]);
            Assert.Equal(0x8B, bytes[1]);
        }

        [Fact]
        public void LoadMissingBlob_ReturnsNullWithoutOnError()
        {
            int errors = 0;
            var store = new PlanHistoryBlobStore(_temp.Path, (_, __) => errors++);

            Assert.Null(store.Load(EntryId));
            Assert.Equal(0, errors);
        }

        [Fact]
        public async Task LoadTruncatedBlob_ReturnsNullAndFiresOnErrorOnce()
        {
            var writeStore = new PlanHistoryBlobStore(_temp.Path);
            Assert.True(writeStore.Save(EntryId, await BuildRealPlanAsync()));

            byte[] bytes = File.ReadAllBytes(BlobPath(EntryId));
            byte[] truncated = new byte[bytes.Length / 2];
            Array.Copy(bytes, truncated, truncated.Length);
            File.WriteAllBytes(BlobPath(EntryId), truncated);

            int errors = 0;
            var store = new PlanHistoryBlobStore(_temp.Path, (_, __) => errors++);

            Assert.Null(store.Load(EntryId));
            Assert.Equal(1, errors);
        }

        [Fact]
        public async Task LoadBlobAtWrongSchemaVersion_ReturnsNullAndFiresOnErrorOnce()
        {
            var plan = await BuildRealPlanAsync();
            var writeStore = new PlanHistoryBlobStore(_temp.Path);
            Assert.True(writeStore.Save(EntryId, plan));

            // Rewrite the blob's payload at a version this build never
            // shipped - byte-for-byte what an old blob looks like after a
            // PersistedPlan.CurrentSchemaVersion bump.
            string json = GzipJsonFile.DecompressToJson(File.ReadAllBytes(BlobPath(EntryId)));
            var doc = JObject.Parse(json);
            doc["SchemaVersion"] = 999;
            File.WriteAllBytes(BlobPath(EntryId), GzipJsonFile.Compress(doc.ToString()));

            int errors = 0;
            var store = new PlanHistoryBlobStore(_temp.Path, (_, __) => errors++);

            Assert.Null(store.Load(EntryId));
            Assert.Equal(1, errors);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("..")]
        [InlineData("../evil")]
        [InlineData("0123456789abcdef0123456789abcde")] // 31 chars
        [InlineData("0123456789abcdef0123456789abcdef0")] // 33 chars
        [InlineData("0123456789ABCDEF0123456789ABCDEF")] // uppercase
        [InlineData("0123456789abcdef0123456789abcde/")]
        [InlineData("0123456789abcdef0123456789abcde\\")]
        public async Task InvalidEntryIds_AreRejectedWithoutTouchingTheFilesystem(string entryId)
        {
            int errors = 0;
            var store = new PlanHistoryBlobStore(_temp.Path, (_, __) => errors++);

            Assert.Null(store.Load(entryId));
            Assert.False(store.Save(entryId, await BuildRealPlanAsync()));
            Assert.False(store.Delete(entryId));
            Assert.Equal(0, errors);

            // No directory was even created by the rejected calls.
            Assert.False(Directory.Exists(Path.Combine(_temp.Path, "plan_history")));
        }

        [Fact]
        public async Task DeleteOrphans_RemovesExactlyTheBlobsNotKept_AndReturnsTheCount()
        {
            var plan = await BuildRealPlanAsync();
            var store = new PlanHistoryBlobStore(_temp.Path);
            string keepA = new string('a', 32);
            string keepB = new string('b', 32);
            string orphanC = new string('c', 32);
            string orphanD = new string('d', 32);
            Assert.True(store.Save(keepA, plan));
            Assert.True(store.Save(keepB, plan));
            Assert.True(store.Save(orphanC, plan));
            Assert.True(store.Save(orphanD, plan));

            int deleted = store.DeleteOrphans(new[] { keepA, keepB });

            Assert.Equal(2, deleted);
            Assert.True(File.Exists(BlobPath(keepA)));
            Assert.True(File.Exists(BlobPath(keepB)));
            Assert.False(File.Exists(BlobPath(orphanC)));
            Assert.False(File.Exists(BlobPath(orphanD)));
        }

        [Fact]
        public async Task Delete_RemovesTheBlob_AndAMissingFileIsSuccess()
        {
            var store = new PlanHistoryBlobStore(_temp.Path);
            Assert.True(store.Save(EntryId, await BuildRealPlanAsync()));

            Assert.True(store.Delete(EntryId));
            Assert.False(File.Exists(BlobPath(EntryId)));
            Assert.True(store.Delete(EntryId));
        }

        /// <summary>
        /// THE SPLIT'S LOAD-BEARING PROPERTY, pinned: a
        /// PersistedPlan.CurrentSchemaVersion bump discards only blobs.
        /// The index row survives with its full request identity intact -
        /// it still renders, still maps to request-input seeds, and
        /// degrades from Open to Re-solve - while the stale blob is
        /// rejected with one Warn.
        /// </summary>
        [Fact]
        public async Task SchemaBump_DiscardsOnlyTheBlob_RowSurvivesAndStillSeedsAReSolve()
        {
            // A real capture-shaped pair: one index row, one blob.
            var plan = await BuildRealPlanAsync();
            var blobStore = new PlanHistoryBlobStore(_temp.Path);
            Assert.True(blobStore.Save(EntryId, plan));

            var indexStore = new PlanHistoryStore(_temp.Path);
            indexStore.Save(new PlanHistoryIndex
            {
                Entries = new List<PlanHistoryEntry>
                {
                    new PlanHistoryEntry
                    {
                        EntryId = EntryId,
                        CreatedAtUtc = DateTime.UtcNow,
                        LastGeneratedAtUtc = DateTime.UtcNow,
                        RequestItems = new List<PlanRequestItem>
                        {
                            new PlanRequestItem { ItemId = 1, Quantity = 1 },
                        },
                        UseOwnMaterials = false,
                        PriceBasis = PriceBasis.InstantBuy,
                        ValueOwnMaterials = false,
                        IgnoredItemIds = new List<int>(),
                        ItemSummaries = new List<PlanHistoryItemSummary>
                        {
                            new PlanHistoryItemSummary
                            {
                                ItemId = 1, Name = "Target", IconUrl = "t.png", Quantity = 1,
                            },
                        },
                        TotalCoinCostAtGeneration = plan.Result.Plan.TotalCoinCost,
                        BlobPresent = true,
                        BlobSchemaVersion = PersistedPlan.CurrentSchemaVersion,
                    },
                },
            });

            // The "bump": the blob on disk now records a version this
            // build does not read - exactly what an already-written blob
            // becomes the day CurrentSchemaVersion moves.
            string json = GzipJsonFile.DecompressToJson(File.ReadAllBytes(BlobPath(EntryId)));
            var doc = JObject.Parse(json);
            doc["SchemaVersion"] = PersistedPlan.CurrentSchemaVersion + 1;
            File.WriteAllBytes(BlobPath(EntryId), GzipJsonFile.Compress(doc.ToString()));

            // The index loads untouched: the row survives in full.
            int indexErrors = 0;
            var loadedIndex = new PlanHistoryStore(_temp.Path, (_, __) => indexErrors++).Load();
            Assert.Equal(0, indexErrors);
            var row = Assert.Single(loadedIndex.Entries);
            Assert.Equal(EntryId, row.EntryId);

            // The blob is rejected with exactly one Warn - Open degrades.
            int blobErrors = 0;
            var reloadBlobStore = new PlanHistoryBlobStore(_temp.Path, (_, __) => blobErrors++);
            Assert.Null(reloadBlobStore.Load(EntryId));
            Assert.Equal(1, blobErrors);

            // And the surviving row still carries everything Re-solve
            // needs: the request identity round-tripped intact...
            Assert.Equal(1, Assert.Single(row.RequestItems).ItemId);
            Assert.Equal(1, row.RequestItems[0].Quantity);
            Assert.False(row.UseOwnMaterials);
            Assert.Equal(PriceBasis.InstantBuy, row.PriceBasis);
            Assert.False(row.ValueOwnMaterials);

            // ...and maps back onto input-row seeds through the same
            // production path a restored request uses.
            var seeds = RestoredRequestInputs.BuildRowSeeds(
                row.RequestItems,
                new Dictionary<int, ItemMetadata>
                {
                    [1] = new ItemMetadata { ItemId = 1, Name = "Target" },
                });
            var seed = Assert.Single(seeds);
            Assert.Equal(1, seed.ItemId);
            Assert.Equal("Target", seed.ItemName);
            Assert.Equal("1", seed.QuantityText);
        }
    }
}
