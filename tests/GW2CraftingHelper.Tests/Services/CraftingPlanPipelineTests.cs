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

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

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

            var result = await pipeline.GenerateStructuredAsync(1, 5, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

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

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            foreach (var step in result.Plan.Steps)
            {
                Assert.True(result.ItemMetadata.ContainsKey(step.ItemId),
                    $"Missing metadata for item {step.ItemId}");
            }
        }

        // KNOWN-ISSUES #31/api-degradation F4: a failing learned-recipes fetch
        // must degrade to null (the same supported "unknown known-recipe
        // status" state PlanResultBuilder already handles) rather than
        // aborting an otherwise fully-successful, fully-priced plan.
        [Fact]
        public async Task GenerateStructuredAsync_LearnedRecipeFetchFails_DegradesToNull_DoesNotAbortPlan()
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item", "target.png");
            itemApi.AddItem(2, "Ingredient", "ingredient.png");

            var accountClient = new InMemoryAccountRecipeClient();
            accountClient.ThrowOnGet = true; // has permission by default, but the fetch itself fails

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                accountRecipeClient: accountClient);

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // The plan itself must be fully built despite the failure.
            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);

            // Recipe 10 is not inherently-available (no MysticForge/
            // Achievement/Merchant discipline) and learnedRecipeIds is
            // null, so IsMissing must be null ("unknown"), not crash and
            // not silently claim the recipe is known.
            var recipe = result.RequiredRecipes.FirstOrDefault(r => r.RecipeId == 10);
            Assert.NotNull(recipe);
            Assert.Null(recipe.IsMissing);
        }

        // KNOWN-ISSUES #31/api-degradation F4 (audit follow-up): the same
        // catch-and-degrade-to-null fix above is duplicated verbatim in
        // GenerateStructuredMultiAsync (the 2+ item path reached via the
        // IReadOnlyList<PlanRequestItem> overload below); only the
        // single-item path above had a regression test. Mirrors that test
        // exactly, just with two roots, so a future refactor that
        // reverts/diverges the multi-item copy of this catch block fails a
        // test instead of silently aborting an otherwise fully-priced
        // multi-item plan.
        [Fact]
        public async Task GenerateStructuredMultiAsync_LearnedRecipeFetchFails_DegradesToNull_DoesNotAbortPlan()
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

            var accountClient = new InMemoryAccountRecipeClient();
            accountClient.ThrowOnGet = true; // has permission by default, but the fetch itself fails

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                accountRecipeClient: accountClient);

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };

            var result = await pipeline.GenerateStructuredAsync(items, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // The multi-item plan itself must be fully built despite the
            // learned-recipe fetch failure.
            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);
            Assert.Equal(2, result.MultiItemRoots.Count);

            // Neither recipe is inherently-available and learnedRecipeIds
            // is null, so IsMissing must be null ("unknown") for both
            // roots' recipes, not crash and not silently claim either
            // recipe is known.
            var recipeA = result.RequiredRecipes.FirstOrDefault(r => r.RecipeId == 10);
            var recipeB = result.RequiredRecipes.FirstOrDefault(r => r.RecipeId == 20);
            Assert.NotNull(recipeA);
            Assert.NotNull(recipeB);
            Assert.Null(recipeA.IsMissing);
            Assert.Null(recipeB.IsMissing);
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

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

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
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
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

                var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);

                Assert.Single(result.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, result.Plan.Steps[0].Source);
                Assert.Equal(100, result.Plan.TotalCoinCost);
            }
        }

        // SEASONAL VENDOR TIP:
        // real-path proof that SeasonalOfferFilter.ExcludeSeasonal is
        // actually wired into the SOLVE call site, not just unit-tested in
        // isolation (SeasonalOfferFilterTests). An item whose ONLY vendor
        // offer is seasonal must fall back to the TP, never BuyFromVendor
        // - and, with the festival active, the excluded offer should still
        // surface as a SeasonalVendorTips entry (the informational Notes
        // row this whole exclusion exists to make room for).
        [Fact]
        public async Task SeasonalOnlyVendorOffer_ExcludedFromSolve_FallsBackToTp_SurfacesAsTip()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1

            var priceApi = new InMemoryPriceApiClient();
            // TP price is 500
            priceApi.AddPrice(1, buyUnitPrice: 500, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Festival Item", "festival.png");

            // The ONLY vendor offer for item 1 is a seasonal (Halloween)
            // offer at 100 coin - cheaper than the 500 TP price, so if the
            // exclusion were NOT wired at this solve call site the solver
            // would wrongly pick BuyFromVendor here.
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-seasonal-vendor",
                        OutputItemId = 1,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 100 }
                        },
                        MerchantName = "Candy Corn Vendor (Weekly)",
                        Locations = new List<string>(),
                        SeasonalFestival = Gw2Constants.HalloweenFestivalName
                    }
                });

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store,
                    activeFestivalNames: () => new[] { Gw2Constants.HalloweenFestivalName });

                var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);

                // Never chosen as BuyFromVendor - the seasonal-only offer
                // was excluded from the solver's own candidate set.
                Assert.Single(result.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
                Assert.Equal(500, result.Plan.TotalCoinCost);

                // Surfaces as an informational tip instead - the excluded
                // offer is still cheaper than the plan's own TP price.
                var tip = Assert.Single(result.SeasonalVendorTips);
                Assert.Equal(1, tip.ItemId);
                Assert.Equal(Gw2Constants.HalloweenFestivalName, tip.Festival);
                Assert.Equal(100, tip.OfferUnitCost);
                Assert.Equal(500, tip.PlanUnitPrice);
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

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.Plan);
            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
        }
    }
}
