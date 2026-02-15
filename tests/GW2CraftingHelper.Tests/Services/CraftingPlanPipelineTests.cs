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

            // Vendor offers 1x item for 100 coin — cheaper than TP
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
            var structured = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None);

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
                    new SnapshotItemEntry { ItemId = 2, Count = 3 }
                }
            };

            var withoutSnapshot = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None);
            var withSnapshot = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None);

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

            // Own item 2 — the intermediate craftable
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 1 }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None);

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

            // Own the intermediate item 2 — it gets pruned from steps but
            // should still have metadata for display in UsedMaterials section
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 1 }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None);

            // UsedMaterials includes item 2
            Assert.Contains(result.UsedMaterials, u => u.ItemId == 2);

            // Item 2 should have metadata even though it's not in plan steps
            Assert.True(result.ItemMetadata.ContainsKey(2),
                "UsedMaterial item ID should have metadata populated");
            Assert.Equal("Intermediate", result.ItemMetadata[2].Name);
        }
    }
}
