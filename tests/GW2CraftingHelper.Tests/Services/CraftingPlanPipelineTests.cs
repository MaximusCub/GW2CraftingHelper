using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingPlanPipelineTests
    {
        [Fact]
        public async Task SimpleCraftableItem_ProducesPlanWithStepsAndMetadata()
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
            itemApi.AddItem(1, "Target Item", "target.png");
            itemApi.AddItem(2, "Ingredient", "ingredient.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateAsync(1, 1, CancellationToken.None);

            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);
            Assert.NotNull(result.ItemMetadata);
            Assert.True(result.ItemMetadata.ContainsKey(1));
            Assert.Equal("Target Item", result.ItemMetadata[1].Name);
        }

        [Fact]
        public async Task LeafOnlyItem_ProducesSingleBuyStep()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Copper Ore", "copper.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateAsync(1, 5, CancellationToken.None);

            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
            Assert.Equal(5, result.Plan.Steps[0].Quantity);
            Assert.True(result.ItemMetadata.ContainsKey(1));
        }

        [Fact]
        public async Task AllStepItemIds_HaveMetadataPopulated()
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 },
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 10000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(3, buyUnitPrice: 20, sellUnitPrice: 200);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Final Item", "final.png");
            itemApi.AddItem(2, "Part A", "a.png");
            itemApi.AddItem(3, "Part B", "b.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateAsync(1, 1, CancellationToken.None);

            foreach (var step in result.Plan.Steps)
            {
                Assert.True(result.ItemMetadata.ContainsKey(step.ItemId),
                    $"Missing metadata for item {step.ItemId}");
            }
        }

        [Fact]
        public async Task MissingItemMetadata_StillProducesValidPlan()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            // No metadata for item 1

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateAsync(1, 1, CancellationToken.None);

            Assert.NotNull(result.Plan);
            Assert.Single(result.Plan.Steps);
            Assert.False(result.ItemMetadata.ContainsKey(1));
        }

        [Fact]
        public async Task VendorOfferAvailable_SolverUsesIt()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1

            var priceApi = new InMemoryPriceApiClient();
            // TP price is 500
            priceApi.AddPrice(1, buyUnitPrice: 500, sellUnitPrice: 5000);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Vendor Item", "vendor.png");

            // Vendor offers 1x item for 100 coin - cheaper than TP
            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + System.Guid.NewGuid());
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-vendor",
                        OutputItemId = 1,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 100 }
                        },
                        MerchantName = "Test NPC",
                        Locations = new List<string>()
                    }
                });

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store);

                var result = await pipeline.GenerateAsync(1, 1, CancellationToken.None);

                Assert.Single(result.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, result.Plan.Steps[0].Source);
                Assert.Equal(100, result.Plan.TotalCoinCost);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task NullVendorStore_PipelineStillWorks()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Item", "icon.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                null);

            var result = await pipeline.GenerateAsync(1, 1, CancellationToken.None);

            Assert.NotNull(result.Plan);
            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
        }

        [Fact]
        public async Task GenerateStructuredAsync_NullSnapshot_SameAsOriginal()
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

            var original = await pipeline.GenerateAsync(1, 1, CancellationToken.None);
            var structured = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // Same plan steps
            Assert.Equal(original.Plan.Steps.Count, structured.Plan.Steps.Count);
            for (int i = 0; i < original.Plan.Steps.Count; i++)
            {
                Assert.Equal(original.Plan.Steps[i].ItemId, structured.Plan.Steps[i].ItemId);
                Assert.Equal(original.Plan.Steps[i].Source, structured.Plan.Steps[i].Source);
                Assert.Equal(original.Plan.Steps[i].Quantity, structured.Plan.Steps[i].Quantity);
            }

            // Structured result has extra fields populated
            Assert.NotNull(structured.RequiredDisciplines);
            Assert.NotNull(structured.RequiredRecipes);
            Assert.NotNull(structured.DebugLog);
            Assert.Empty(structured.UsedMaterials);
        }

        [Fact]
        public async Task GenerateStructuredAsync_WithSnapshot_ReducesTree()
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 5 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400
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

            // Snapshot owns 3 of ingredient (item 2)
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 3, Source = AccountItemIndex.SourceMaterialStorage }
                }
            };

            var withoutSnapshot = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
            var withSnapshot = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // With snapshot should buy fewer of item 2
            var buyStepWithout = withoutSnapshot.Plan.Steps
                .FirstOrDefault(s => s.ItemId == 2 && s.Source == AcquisitionSource.BuyFromTp);
            var buyStepWith = withSnapshot.Plan.Steps
                .FirstOrDefault(s => s.ItemId == 2 && s.Source == AcquisitionSource.BuyFromTp);

            Assert.NotNull(buyStepWithout);
            Assert.Equal(5, buyStepWithout.Quantity);
            Assert.NotNull(buyStepWith);
            Assert.Equal(2, buyStepWith.Quantity); // 5 - 3 = 2

            // UsedMaterials should report the 3 consumed
            Assert.Single(withSnapshot.UsedMaterials);
            Assert.Equal(2, withSnapshot.UsedMaterials[0].ItemId);
            Assert.Equal(3, withSnapshot.UsedMaterials[0].QuantityUsed);
        }

        [Fact]
        public async Task GenerateStructuredAsync_OwnedIntermediate_RemovesCraftStep_And_Discipline()
        {
            var recipeApi = new InMemoryRecipeApiClient();

            // Item 1 -> recipe 10 (Weaponsmith 500) -> item 2
            // Item 2 -> recipe 20 (Armorsmith 400) -> item 3
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 500,
                Flags = new List<string> { "AutoLearned" }
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                },
                Disciplines = new List<string> { "Armorsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50000, sellUnitPrice: 100000);
            priceApi.AddPrice(2, buyUnitPrice: 10000, sellUnitPrice: 50000);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Final", "f.png");
            itemApi.AddItem(2, "Intermediate", "m.png");
            itemApi.AddItem(3, "Raw Mat", "r.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            // Own item 2 - the intermediate craftable
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 1, Source = AccountItemIndex.SourceMaterialStorage }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // Item 2's Craft step (recipe 20) should be gone
            Assert.DoesNotContain(result.Plan.Steps,
                s => s.RecipeId == 20 && s.Source == AcquisitionSource.Craft);

            // Item 3's buy step should also be gone (no longer needed)
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 3);

            // Armorsmith discipline should NOT be required (recipe 20 pruned)
            Assert.DoesNotContain(result.RequiredDisciplines,
                d => d.Discipline == "Armorsmith");

            // Recipe 20 should NOT be in required recipes
            Assert.DoesNotContain(result.RequiredRecipes, r => r.RecipeId == 20);

            // Weaponsmith discipline SHOULD still be required (recipe 10 still needed)
            Assert.Contains(result.RequiredDisciplines,
                d => d.Discipline == "Weaponsmith");

            // Recipe 10 SHOULD still be in required recipes
            Assert.Contains(result.RequiredRecipes, r => r.RecipeId == 10);

            // UsedMaterials should report item 2 consumed
            Assert.Contains(result.UsedMaterials,
                u => u.ItemId == 2 && u.QuantityUsed == 1);
        }

        [Fact]
        public async Task GenerateStructuredAsync_UsedMaterialIds_HaveMetadata()
        {
            var recipeApi = new InMemoryRecipeApiClient();

            // Item 1 -> recipe 10 -> item 2 (intermediate) -> recipe 20 -> item 3
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 500
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50000, sellUnitPrice: 100000);
            priceApi.AddPrice(2, buyUnitPrice: 10000, sellUnitPrice: 50000);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Final", "f.png");
            itemApi.AddItem(2, "Intermediate", "m.png");
            itemApi.AddItem(3, "Raw Mat", "r.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            // Own the intermediate item 2 - it gets pruned from steps but
            // should still have metadata for display in UsedMaterials section
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 1, Source = AccountItemIndex.SourceMaterialStorage }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // UsedMaterials includes item 2
            Assert.Contains(result.UsedMaterials, u => u.ItemId == 2);

            // Item 2 should have metadata even though it's not in plan steps
            Assert.True(result.ItemMetadata.ContainsKey(2),
                "UsedMaterial item ID should have metadata populated");
            Assert.Equal("Intermediate", result.ItemMetadata[2].Name);
        }

        [Fact]
        public async Task GenerateAsync_DebugLogContainsTimingEntries()
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

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateAsync(1, 1, CancellationToken.None);

            Assert.NotNull(result.DebugLog);

            // All 7 GenerateAsync phase prefixes must appear with timing
            var expectedPrefixes = new[]
            {
                "Build recipe tree",
                "Collect item IDs",
                "Fetch TP prices",
                "Resolve vendor offers",
                "Query vendor offers",
                "Solve",
                "Fetch item metadata"
            };

            var timingPattern = new Regex(@"\d+ms");

            foreach (var prefix in expectedPrefixes)
            {
                var match = result.DebugLog.FirstOrDefault(
                    line => line.StartsWith(prefix) && timingPattern.IsMatch(line));
                Assert.True(match != null,
                    $"DebugLog missing timing entry for phase '{prefix}'. "
                    + $"Entries: [{string.Join(", ", result.DebugLog)}]");
            }

            // Timing summary block must be present
            Assert.Contains(result.DebugLog,
                line => line == "--- Timing Summary ---");
        }

        [Fact]
        public async Task GenerateStructuredAsync_ReportsProgressForEachPhase()
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

            var progress = new CapturingProgress<PlanStatus>();

            await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, progress,
                priceBasis: PriceBasis.InstantBuy);

            // All 10 expected phase messages in pipeline order
            var expectedSubstrings = new[]
            {
                "recipe tree",
                "Collecting item IDs",
                "Fetching prices",
                "Resolving vendor offers",
                "Looking up vendor offers",
                "Reducing inventory",
                "Solving crafting plan",
                "Fetching item details",
                "Checking learned recipes",
                "Building final result"
            };

            Assert.True(progress.Reports.Count >= expectedSubstrings.Length,
                $"Expected >= {expectedSubstrings.Length} progress reports, "
                + $"got {progress.Reports.Count}: "
                + $"[{string.Join(", ", progress.Reports.Select(r => r.Message))}]");

            // Verify each expected substring appears in order
            int searchFrom = 0;
            foreach (var expected in expectedSubstrings)
            {
                int found = -1;
                for (int i = searchFrom; i < progress.Reports.Count; i++)
                {
                    if (progress.Reports[i].Message != null
                        && progress.Reports[i].Message.Contains(expected))
                    {
                        found = i;
                        break;
                    }
                }
                Assert.True(found >= 0,
                    $"Progress message containing '{expected}' not found at or after index {searchFrom}. "
                    + $"Reports: [{string.Join(", ", progress.Reports.Select(r => r.Message))}]");
                searchFrom = found + 1;
            }
        }

        // --- Sell-side economics ---

        private static CraftingPlanPipeline BuildEconomicsPipeline(
            out InMemoryPriceApiClient priceApi)
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

        [Fact]
        public async Task VendorOfferItemCost_OutsideTree_PriceFetchedAndOfferUsed()
        {
            // Regression (Gift of Glory): a vendor offer charging an ITEM
            // that appears nowhere in the recipe tree was skipped as
            // unpriceable because the cost item's TP price was never
            // fetched, leaving the target as UnknownSource.
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe and no TP price for target item 1
            var priceApi = new InMemoryPriceApiClient();
            // Cost item 999 (not in any tree) has a TP price of 2c
            priceApi.AddPrice(999, buyUnitPrice: 1, sellUnitPrice: 2);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Gifted Item", "g.png");
            itemApi.AddItem(999, "Cost Token", "t.png");

            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + System.Guid.NewGuid());
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-item-cost-outside-tree",
                        OutputItemId = 1,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Item", Id = 999, Count = 250 }
                        },
                        MerchantName = "Token Vendor",
                        Locations = new List<string>()
                    }
                });

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store);

                var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);

                Assert.Single(result.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, result.Plan.Steps[0].Source);
                // 250 x 2c (instant-buy basis) = 500
                Assert.Equal(500, result.Plan.TotalCoinCost);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task Structured_TargetHasBuyOrders_ProfitFieldsComputed()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            // Target: buy orders at 400 (sell revenue), sell listings at 1000.
            // Ingredient: instant buy 100 -> craft cost 3x100=300 < buy 1000.
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(300, result.Plan.TotalCoinCost);
            Assert.Equal(400, result.TargetUnitSellPrice);
            // 400 - 20 (5%) - 40 (10%) = 340 net
            Assert.Equal(340, result.NetSaleValue);
            Assert.Equal(40, result.CraftingProfit);
            Assert.Equal(PriceBasis.InstantBuy, result.PriceBasis);
        }

        [Fact]
        public async Task Structured_NoBuyOrders_ProfitFieldsNull()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 0, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Null(result.TargetUnitSellPrice);
            Assert.Null(result.NetSaleValue);
            Assert.Null(result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_RootRecipeOverproduces_RevenueCoversWholeBatch()
        {
            // Recipe outputs 5 per craft; requesting 1 still costs a full
            // craft, so all 5 produced units count as sellable revenue.
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 5,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 10000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");
            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // Craft cost: 3x100 = 300 for a batch of 5
            Assert.Equal(300, result.Plan.TotalCoinCost);
            Assert.Equal(5, result.SellableQuantity);
            // Revenue: 5 x 400 = 2000 total; -100 listing -200 exchange = 1700
            Assert.Equal(1700, result.NetSaleValue);
            Assert.Equal(1400, result.CraftingProfit);
        }

        [Fact]
        public async Task ResolveWithOverrides_LocalResolveFlipsDecisionAndEconomics()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            // Craft (300) beats buy (1000); target sells to orders at 400.
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var initial = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
            Assert.NotNull(initial.SolveContext);
            Assert.Equal(300, initial.Plan.TotalCoinCost);

            // Force the root to be bought instead
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { initial.CraftingTree.NodeId, AcquisitionSource.BuyFromTp }
            };
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, overrides);

            Assert.Single(resolved.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, resolved.Plan.Steps[0].Source);
            Assert.Equal(1000, resolved.Plan.TotalCoinCost);
            // Economics recomputed: 340 net - 1000 cost = -660
            Assert.Equal(-660, resolved.CraftingProfit);
            // Context is carried forward for subsequent re-solves
            Assert.Same(initial.SolveContext, resolved.SolveContext);
            // Tree reflects the forced decision and availability
            Assert.Equal(Contracts.CraftingDecision.BuyFromTp, resolved.CraftingTree.Decision);
            Assert.True(resolved.CraftingTree.CanCraft);
            Assert.True(resolved.CraftingTree.CanBuyTp);
        }

        [Fact]
        public async Task BuildPresetOverrides_CraftAll_ReachesNodesUnderBoughtIntermediates()
        {
            // Item 1 <- recipe(2) <- recipe(3). Intermediate 2 is cheap to
            // buy, so the best path hides node 3 below a bought node; the
            // Craft All preset must still force-craft both levels.
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });
            var priceApi = new InMemoryPriceApiClient();
            // Buying 2 (50) beats crafting it (2x100=200); buying 1 (500)
            // loses to crafting-from-bought-2 (50)
            priceApi.AddPrice(1, buyUnitPrice: 10, sellUnitPrice: 500);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 50);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Mid", "m.png");
            itemApi.AddItem(3, "Base", "b.png");
            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var initial = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
            // Baseline: craft 1 from bought 2 (50)
            Assert.Equal(50, initial.Plan.TotalCoinCost);

            var craftAll = CraftingPlanPipeline.BuildPresetOverrides(
                initial.SolveContext, AcquisitionSource.Craft);
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, craftAll);

            // Now both levels craft: cost = 2x100 for item 3
            Assert.Equal(200, resolved.Plan.TotalCoinCost);
            Assert.Contains(resolved.Plan.Steps,
                s => s.Source == AcquisitionSource.Craft && s.ItemId == 2);
            // Metadata must cover items surfaced only by the override
            // (regression: items under bought nodes were never fetched and
            // rendered as "Unknown Item")
            Assert.All(resolved.Plan.Steps,
                s => Assert.True(resolved.ItemMetadata.ContainsKey(s.ItemId)));

            // Buy All flips everything buyable to TP purchases
            var buyAll = CraftingPlanPipeline.BuildPresetOverrides(
                initial.SolveContext, AcquisitionSource.BuyFromTp);
            var bought = pipeline.ResolveWithOverrides(initial.SolveContext, buyAll);
            Assert.Single(bought.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, bought.Plan.Steps[0].Source);
        }

        [Fact]
        public async Task Structured_BuyOrderBasis_MaterialsCostedAtBuyOrders()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            // Ingredient: instant 100, buy order 10 -> craft cost 30 at basis.
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.BuyOrder);

            Assert.Equal(30, result.Plan.TotalCoinCost);
            Assert.Equal(PriceBasis.BuyOrder, result.PriceBasis);
            Assert.Equal(310, result.CraftingProfit);
        }

        // --- Currency valuation threading ---

        [Fact]
        public async Task GenerateStructuredAsync_CurrencyValuation_ThreadsIntoSolverAndContext()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 1000, sellUnitPrice: 2000);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Karma Item", "karma.png");

            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + System.Guid.NewGuid());
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-karma-offer",
                        OutputItemId = 1,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = 2, Count = 50 }
                        },
                        MerchantName = "Karma Vendor",
                        Locations = new List<string>()
                    }
                });

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store,
                    reducer: new InventoryReducer());

                var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });

                var result = await pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None,
                    currencyValuation: valuation,
                    priceBasis: PriceBasis.InstantBuy);

                // Vendor wins: 50 karma x 5 copper = 250 < 1000 TP
                Assert.Single(result.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, result.Plan.Steps[0].Source);
                Assert.Equal(0, result.Plan.Steps[0].TotalCost);
                Assert.Single(result.Plan.CurrencyCosts);
                Assert.Equal(2, result.Plan.CurrencyCosts[0].CurrencyId);
                Assert.Equal(50, result.Plan.CurrencyCosts[0].Amount);

                // The valuation is captured on the context for later local re-solves
                Assert.NotNull(result.SolveContext.CurrencyValuation);
                Assert.True(result.SolveContext.CurrencyValuation.TryGetCopperValue(2, out long copperPerUnit));
                Assert.Equal(5, copperPerUnit);

                // A subsequent local re-solve (no network calls, no overrides)
                // must keep using the valuation carried on the context.
                var resolved = pipeline.ResolveWithOverrides(result.SolveContext, null);
                Assert.Equal(AcquisitionSource.BuyFromVendor, resolved.Plan.Steps[0].Source);
                Assert.Single(resolved.Plan.CurrencyCosts);
                Assert.Equal(50, resolved.Plan.CurrencyCosts[0].Amount);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_NoCurrencyValuationArgument_ContextDefaultsToNone()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Item", "icon.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.SolveContext.CurrencyValuation);
            Assert.False(result.SolveContext.CurrencyValuation.TryGetCopperValue(2, out _));
        }

        // --- Own-materials valuation (M28) ---

        private static CraftingPlanPipeline BuildOwnMaterialsPipeline(
            out InMemoryPriceApiClient priceApi, int ingredientCount = 5)
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
                    new RawIngredient { Type = "Item", Id = 2, Count = ingredientCount }
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
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());
        }

        private static AccountSnapshot OwnIngredient(int count)
        {
            return new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry
                    {
                        ItemId = 2,
                        Count = count,
                        Source = AccountItemIndex.SourceMaterialStorage
                    }
                }
            };
        }

        [Fact]
        public async Task Structured_ValuedMode_DeductsMaterialOpportunityCostFromProfit()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            // Ingredient: SellInstant=10 (opportunity-cost basis), BuyInstant=100 (craft-cost basis).
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Own 3 of the 5 needed; the other 2 are bought.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(3), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            // Craft cost: (5 - 3) x 100 = 200
            Assert.Equal(200, result.Plan.TotalCoinCost);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(3, result.UsedMaterials[0].QuantityUsed);

            // Opportunity cost: selling 3 x 10c = 30 total; fees -2 (5%) -3 (10%) = 25 net.
            Assert.Equal(25, result.MaterialOpportunityCost);

            // Sell value (unchanged): 400 - 20 (5%) - 40 (10%) = 340
            Assert.Equal(340, result.NetSaleValue);
            // Profit: 340 - 200 (coin cost) - 25 (opportunity cost) = 115
            Assert.Equal(115, result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_FreeMode_MaterialOpportunityCostNullAndProfitUnchanged()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Default mode (no ownMaterialsMode argument) - Free.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(3), CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(200, result.Plan.TotalCoinCost);
            Assert.Null(result.MaterialOpportunityCost);
            // Profit unaffected by ownership: 340 - 200 = 140
            Assert.Equal(140, result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_ValuedMode_NoSnapshot_MaterialOpportunityCostNull()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Valued mode but nothing was reduced (no snapshot) - no owned
            // materials, so there is nothing to have forgone selling.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Empty(result.UsedMaterials);
            Assert.Null(result.MaterialOpportunityCost);
            // All 5 ingredients bought at 100 each = 500; profit = 340 - 500 = -160
            Assert.Equal(500, result.Plan.TotalCoinCost);
            Assert.Equal(-160, result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_ValuedMode_UnsellableUsedMaterial_ContributesZero()
        {
            // Two ingredients are owned and consumed: item 2 is sellable,
            // item 3 has no buy orders (SellInstant 0) and must contribute
            // 0 to the opportunity cost rather than being skipped/erroring
            // or zeroing the whole sum.
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 5 },
                    new RawIngredient { Type = "Item", Id = 3, Count = 4 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // sellable, SellInstant=10
            priceApi.AddPrice(3, buyUnitPrice: 0, sellUnitPrice: 50);   // unsellable, SellInstant=0

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Sellable Ingredient", "i.png");
            itemApi.AddItem(3, "Unsellable Ingredient", "j.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 5, Source = AccountItemIndex.SourceMaterialStorage },
                    new SnapshotItemEntry { ItemId = 3, Count = 4, Source = AccountItemIndex.SourceMaterialStorage }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, snapshot, CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(2, result.UsedMaterials.Count);

            // Only item 2's 5 units count: 5x10=50 total; fees -3 (5%) -5 (10%) = 42 net.
            // Item 3 contributes 0 despite 4 units being used.
            Assert.Equal(42, result.MaterialOpportunityCost);
        }

        [Fact]
        public async Task ResolveWithOverrides_PreservesOwnMaterialsMode()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(3), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(OwnMaterialsMode.Valued, initial.SolveContext.OwnMaterialsMode);
            Assert.Equal(25, initial.MaterialOpportunityCost);
            Assert.Equal(115, initial.CraftingProfit);

            // A no-op local re-solve must keep valuing owned materials the
            // same way the original Generate did (context-carried, like
            // CurrencyValuation).
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, null);

            Assert.Equal(25, resolved.MaterialOpportunityCost);
            Assert.Equal(115, resolved.CraftingProfit);
        }

        [Fact]
        public async Task GenerateStructuredAsync_NoOwnMaterialsModeArgument_ContextDefaultsToFree()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(3), CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(OwnMaterialsMode.Free, result.SolveContext.OwnMaterialsMode);
            Assert.Null(result.MaterialOpportunityCost);
        }

        [Fact]
        public async Task Structured_ValuedMode_UsedMaterialPrices_AlreadyCoveredByTreeFetch()
        {
            // Design assertion (see M28 spec): prices are fetched for
            // allItemIds, which is collected from the PRE-reduction tree
            // (Step 2 runs before Step 6's reduction), so every used
            // material - being a tree item that reduction happened to
            // remove - already has a price entry by the time
            // ApplySellSideEconomics runs. No separate fetch is needed for
            // MaterialOpportunityCost, and this test pins that: the used
            // material's price came from the ordinary tree price fetch.
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Own ALL of the required ingredient, so nothing is left to buy
            // for item 2 (any remaining step is a zero-quantity/zero-cost
            // placeholder) - its only real trace is UsedMaterials. If its
            // price had to be fetched specially for the opportunity-cost
            // calc rather than coming from the tree-wide fetch, this would
            // be null/0 instead of the expected net value.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(5), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 2 && s.Quantity > 0);
            Assert.Equal(5, result.UsedMaterials[0].QuantityUsed);

            // 5x10=50 total; fees -3 (5%) -5 (10%) = 42 net.
            Assert.Equal(42, result.MaterialOpportunityCost);
        }

        // --- M30 review: currency metadata wired through the pipeline ---

        private class StubCurrencyHandler : HttpMessageHandler
        {
            private readonly string _body;

            public StubCurrencyHandler(string body)
            {
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_body)
                };
                return Task.FromResult(response);
            }
        }

        private const string CurrencySampleJson = @"[
            { ""id"": 2, ""name"": ""Karma"", ""icon"": ""https://render.guildwars2.com/file/karma.png"" }
        ]";

        [Fact]
        public async Task GenerateAsync_WithCurrencyMetadataService_PopulatesCurrencyMetadata()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1 - simplest leaf-buy plan.

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Copper Ore", "copper.png");

            using (var handler = new StubCurrencyHandler(CurrencySampleJson))
            using (var http = new HttpClient(handler))
            {
                var currencyService = new CurrencyMetadataService(http);
                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    currencyMetadataService: currencyService);

                var result = await pipeline.GenerateAsync(1, 1, CancellationToken.None);

                Assert.NotNull(result.CurrencyMetadata);
                Assert.True(result.CurrencyMetadata.ContainsKey(2));
                Assert.Equal("Karma", result.CurrencyMetadata[2].Name);
            }
        }

        [Fact]
        public async Task ResolveWithOverrides_PreservesCurrencyMetadataViaSolveContext()
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
            // Craft (300) beats buy (1000), matching the existing override
            // test's economics so the override below actually flips a
            // real decision.
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            using (var handler = new StubCurrencyHandler(CurrencySampleJson))
            using (var http = new HttpClient(handler))
            {
                var currencyService = new CurrencyMetadataService(http);
                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    currencyMetadataService: currencyService);

                var initial = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);
                Assert.NotNull(initial.CurrencyMetadata);
                Assert.True(initial.CurrencyMetadata.ContainsKey(2));
                Assert.NotNull(initial.SolveContext.CurrencyMetadata);

                var overrides = new Dictionary<int, AcquisitionSource>
                {
                    { initial.CraftingTree.NodeId, AcquisitionSource.BuyFromTp }
                };
                var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, overrides);

                // The local re-solve is purely from the cached SolveContext
                // (no network calls) - CurrencyMetadata must still be there.
                Assert.NotNull(resolved.CurrencyMetadata);
                Assert.True(resolved.CurrencyMetadata.ContainsKey(2));
                Assert.Equal("Karma", resolved.CurrencyMetadata[2].Name);
            }
        }

        // --- M34-B2a #3: force-buy pre-pass (zero-owned baseline) ---

        private static CraftingPlanPipeline BuildForceBuyPipeline(out InMemoryPriceApiClient priceApi)
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 5 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            priceApi = new InMemoryPriceApiClient();
            // NOTE: InMemoryPriceApiClient's (buyUnitPrice, sellUnitPrice)
            // map to raw GW2-API buys/sells.unit_price - TradingPostService
            // then maps BuyInstant (cost to instant-BUY) from the RAW
            // sellUnitPrice param, and SellInstant from the raw
            // buyUnitPrice param (see TradingPostService.cs) - so the
            // SECOND argument here is the one that drives GetUnitPrice at
            // PriceBasis.InstantBuy.
            //
            // Fresh (zero-owned) check: buy(100) < craft(5x30=150)*0.85=127.5
            // -> item 1 is force-buy-flagged on a truly zero-owned baseline.
            priceApi.AddPrice(1, buyUnitPrice: 1000, sellUnitPrice: 100);
            priceApi.AddPrice(2, buyUnitPrice: 300, sellUnitPrice: 30);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());
        }

        private static AccountSnapshot OwnFourOfIngredient()
        {
            return new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry
                    {
                        ItemId = 2,
                        Count = 4,
                        Source = AccountItemIndex.SourceMaterialStorage
                    }
                }
            };
        }

        [Fact]
        public async Task Structured_ValuedMode_ForceBuyPrePass_UsesZeroOwnedBaseline()
        {
            // Own 4 of the 5 needed of item 2: post-reduction, item 1's
            // craft cost collapses to 1x30=30 - misleadingly cheaper than
            // buy(100) if evaluated AFTER reduction. The force-buy flag,
            // computed on the zero-owned (pre-reduction) baseline, must
            // still keep item 1 bought rather than "crafted" from an
            // artificially cheap remainder.
            var pipeline = BuildForceBuyPipeline(out _);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnFourOfIngredient(), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Single(result.Plan.Steps);
            Assert.Equal(1, result.Plan.Steps[0].ItemId);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
            Assert.Equal(100, result.Plan.TotalCoinCost);
        }

        [Fact]
        public async Task Structured_FreeMode_SameOwnershipScenario_CraftsFromReducedRemainder()
        {
            // Control for the test above: Free mode never runs the
            // force-buy pre-pass, so the (misleadingly cheap) post-
            // reduction craft path wins normally, same as before M34.
            var pipeline = BuildForceBuyPipeline(out _);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnFourOfIngredient(), CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy); // default Free

            Assert.Contains(result.Plan.Steps,
                s => s.ItemId == 1 && s.Source == AcquisitionSource.Craft);
            // Only the 1 remaining unit of item 2 is bought.
            Assert.Contains(result.Plan.Steps,
                s => s.ItemId == 2 && s.Source == AcquisitionSource.BuyFromTp && s.Quantity == 1);
        }

        [Fact]
        public async Task Structured_ValuedMode_NoSnapshot_ForceBuyPrePassDoesNotRun()
        {
            // Valued mode alone (no snapshot) must not activate the
            // force-buy pre-pass at all - see CraftingPlanPipeline's own
            // gate comment. The full (unreduced) craft cost (5x30=150)
            // genuinely beats buy(100)? No - buy(100) beats craft(150)
            // outright already, so normal (non-forced) PickCheapest already
            // buys here regardless; this test pins that no snapshot means
            // no special force-buy machinery runs, not just that the
            // outcome happens to match.
            var pipeline = BuildForceBuyPipeline(out _);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
        }

        [Fact]
        public async Task ResolveWithOverrides_ForceBuyPrePass_ManualOverrideStillWins()
        {
            var pipeline = BuildForceBuyPipeline(out _);

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, OwnFourOfIngredient(), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(Contracts.CraftingDecision.BuyFromTp, initial.CraftingTree.Decision);
            Assert.True(initial.CraftingTree.CanCraft); // flag reflects true feasibility

            // Manually force craft on the root - must win over the
            // automatic force-buy pre-pass (gw2e parity: manual pill always
            // beats the automatic pre-pass).
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { initial.CraftingTree.NodeId, AcquisitionSource.Craft }
            };
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, overrides);

            Assert.Equal(Contracts.CraftingDecision.Craft, resolved.CraftingTree.Decision);
            // Real (post-reduction) craft cost: only 1 remaining unit of
            // item 2 needs buying, at 30 each.
            Assert.Equal(30, resolved.Plan.TotalCoinCost);
        }

        [Fact]
        public async Task ResolveWithOverrides_NoOpResolve_ForceBuyDecisionUnchanged()
        {
            // A no-op local re-solve (no overrides at all) must keep
            // applying the force-buy pre-pass exactly as the original
            // generation did - not "forget" it on the first re-solve.
            var pipeline = BuildForceBuyPipeline(out _);

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, OwnFourOfIngredient(), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, null);

            Assert.Equal(AcquisitionSource.BuyFromTp, resolved.Plan.Steps[0].Source);
            Assert.Equal(100, resolved.Plan.TotalCoinCost);
        }

        // --- M34-B2a #4: owned currency (cosmetic only, never affects decisions) ---

        private static CraftingPlanPipeline BuildVendorCurrencyPipeline(
            out VendorOfferStore store, string tempDir)
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1, and (deliberately) no TP price either -
            // a vendor-only purchase. The offer's karma cost line is never
            // valued (no CurrencyValuation passed below), so it can only
            // win via the "fallback" tier (PlanSolver's last-resort branch
            // when nothing coin-priceable/craftable exists at all) - giving
            // it a TP price here would make TP win outright instead.
            var priceApi = new InMemoryPriceApiClient();

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Karma Item", "karma.png");

            var loader = new VendorOfferLoader();
            store = new VendorOfferStore(tempDir, loader);
            store.LoadBaseline(null);
            store.AddOffersToOverlay(new[]
            {
                new VendorOffer
                {
                    OfferId = "test-karma-offer",
                    OutputItemId = 1,
                    OutputCount = 1,
                    CostLines = new List<CostLine>
                    {
                        new CostLine { Type = "Currency", Id = 2, Count = 500 }
                    },
                    MerchantName = "Karma Vendor",
                    Locations = new List<string>()
                }
            });

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                store,
                reducer: new InventoryReducer());
        }

        [Fact]
        public async Task OwnedCurrency_DoesNotAffectDecisionsOrTotals()
        {
            // Regression guard (M34-B2a #4): wallet currency data is
            // cosmetic-only annotation. A plan generated WITH wallet karma
            // must produce the IDENTICAL decisions/costs as one generated
            // with none - only OwnedCurrencyAmounts may differ.
            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + System.Guid.NewGuid());
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var pipeline = BuildVendorCurrencyPipeline(out _, tempDir);

                var withoutWallet = await pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                var snapshotWithWallet = new AccountSnapshot
                {
                    Wallet = new List<SnapshotWalletEntry>
                    {
                        new SnapshotWalletEntry { CurrencyId = 2, Value = 100000 }
                    }
                };
                var withWallet = await pipeline.GenerateStructuredAsync(
                    1, 1, snapshotWithWallet, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                // Decisions/costs identical regardless of wallet content.
                Assert.Equal(withoutWallet.Plan.Steps.Count, withWallet.Plan.Steps.Count);
                Assert.Equal(withoutWallet.Plan.Steps[0].Source, withWallet.Plan.Steps[0].Source);
                Assert.Equal(withoutWallet.Plan.TotalCoinCost, withWallet.Plan.TotalCoinCost);
                Assert.Equal(withoutWallet.Plan.CurrencyCosts.Count, withWallet.Plan.CurrencyCosts.Count);
                Assert.Equal(withoutWallet.Plan.CurrencyCosts[0].Amount, withWallet.Plan.CurrencyCosts[0].Amount);
                Assert.Equal(withoutWallet.CraftingTree.Decision, withWallet.CraftingTree.Decision);

                // Only the annotation differs. CraftingPlanResult.
                // OwnedCurrencyAmounts stores the RAW wallet amount
                // (capping-at-needed is a view-model presentation concern -
                // see PlanViewModelBuilder / the CurrencyCostRow test below).
                Assert.Null(withoutWallet.OwnedCurrencyAmounts);
                Assert.NotNull(withWallet.OwnedCurrencyAmounts);
                Assert.Equal(100000, withWallet.OwnedCurrencyAmounts[2]);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task OwnedCurrency_PartialWalletAmount_CappedAtNeeded()
        {
            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + System.Guid.NewGuid());
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var pipeline = BuildVendorCurrencyPipeline(out _, tempDir);

                var snapshot = new AccountSnapshot
                {
                    Wallet = new List<SnapshotWalletEntry>
                    {
                        new SnapshotWalletEntry { CurrencyId = 2, Value = 200 }
                    }
                };
                var result = await pipeline.GenerateStructuredAsync(
                    1, 1, snapshot, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                // Needs 500, owns only 200 - reported as-is (not capped to
                // itself, since 200 < 500).
                Assert.Equal(200, result.OwnedCurrencyAmounts[2]);
                // The plan itself still needs the full 500 (owned currency
                // never nets against the plan's own currency total).
                Assert.Equal(500, result.Plan.CurrencyCosts[0].Amount);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task OwnedCurrency_NoWalletAtAll_AmountsNull()
        {
            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + System.Guid.NewGuid());
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var pipeline = BuildVendorCurrencyPipeline(out _, tempDir);

                var result = await pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                Assert.Null(result.OwnedCurrencyAmounts);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task OwnedCurrency_ViewModel_CurrencyCostRowGetsOwnedQuantity()
        {
            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + System.Guid.NewGuid());
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var pipeline = BuildVendorCurrencyPipeline(out _, tempDir);

                var snapshot = new AccountSnapshot
                {
                    Wallet = new List<SnapshotWalletEntry>
                    {
                        new SnapshotWalletEntry { CurrencyId = 2, Value = 200 }
                    }
                };
                var result = await pipeline.GenerateStructuredAsync(
                    1, 1, snapshot, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                var vm = new PlanViewModelBuilder().Build(result);
                var summarySection = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
                var currencyRow = summarySection.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);

                Assert.Equal(200, currencyRow.CurrencyOwnedQuantity);
                Assert.Equal(500, currencyRow.Quantity);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task OwnedCurrency_ViewModel_NoWallet_OwnedQuantityNull()
        {
            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + System.Guid.NewGuid());
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var pipeline = BuildVendorCurrencyPipeline(out _, tempDir);

                var result = await pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                var vm = new PlanViewModelBuilder().Build(result);
                var summarySection = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
                var currencyRow = summarySection.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);

                Assert.Null(currencyRow.CurrencyOwnedQuantity);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }
    }
}
