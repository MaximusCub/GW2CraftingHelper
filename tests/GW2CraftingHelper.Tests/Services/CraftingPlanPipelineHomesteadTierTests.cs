using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingPlanPipelineHomesteadTierTests
    {
        // --- Homestead Refinement efficiency tiers
        // are snapshotted on PlanSolveContext at generation time and reused
        // as-is by a local override re-solve, matching every other
        // settings-snapshot field on that class (CurrencyValuation,
        // OwnMaterialsMode, ...). ---

        [Fact]
        public async Task GenerateStructuredAsync_NoHomesteadTiersArgument_ContextDefaultsToTierZero()
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

            Assert.NotNull(result.SolveContext.HomesteadTiers);
            Assert.Equal(0, result.SolveContext.HomesteadTiers.GetTier(Gw2Constants.RefinedHomesteadFiberItemId));
            Assert.Equal(0, result.SolveContext.HomesteadTiers.GetTier(Gw2Constants.RefinedHomesteadMetalItemId));
            Assert.Equal(0, result.SolveContext.HomesteadTiers.GetTier(Gw2Constants.RefinedHomesteadWoodItemId));
        }

        [Fact]
        public async Task GenerateStructuredAsync_HomesteadTiersArgument_GatesVendorOfferAndSnapshotsOnContext()
        {
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var recipeApi = new InMemoryRecipeApiClient();
                var priceApi = new InMemoryPriceApiClient();
                priceApi.AddPrice(102205, buyUnitPrice: 1000, sellUnitPrice: 1000);
                var itemApi = new InMemoryItemApiClient();
                itemApi.AddItem(102205, "Refined Homestead Metal", "icon.png");

                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-homestead-tier0",
                        OutputItemId = 102205,
                        OutputCount = 2,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 400 }
                        },
                        MerchantName = "Homestead Refinement\u2014Metal Forge",
                        Locations = new List<string>(),
                        HomesteadTier = 0
                    },
                    new VendorOffer
                    {
                        OfferId = "test-homestead-tier2",
                        OutputItemId = 102205,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 1 }
                        },
                        MerchantName = "Homestead Refinement\u2014Metal Forge",
                        Locations = new List<string>(),
                        HomesteadTier = 2
                    }
                });

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store,
                    reducer: new InventoryReducer());

                // Default (no homesteadTiers argument): tier-2 offer excluded,
                // the far more expensive tier-0 offer is the only candidate.
                var defaultResult = await pipeline.GenerateStructuredAsync(
                    102205, 2, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
                Assert.Single(defaultResult.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, defaultResult.Plan.Steps[0].Source);
                Assert.Equal(400, defaultResult.Plan.Steps[0].TotalCost);

                // Tier 2 configured: the far cheaper tier-2 offer is admitted.
                var tier2 = new HomesteadEfficiencyTiers(new Dictionary<int, int>
                {
                    { Gw2Constants.RefinedHomesteadMetalItemId, 2 }
                });
                var tieredResult = await pipeline.GenerateStructuredAsync(
                    102205, 2, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                    homesteadTiers: tier2);
                Assert.Single(tieredResult.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, tieredResult.Plan.Steps[0].Source);
                Assert.Equal(2, tieredResult.Plan.Steps[0].TotalCost);

                // Snapshotted on the context...
                Assert.NotNull(tieredResult.SolveContext.HomesteadTiers);
                Assert.Equal(2, tieredResult.SolveContext.HomesteadTiers.GetTier(Gw2Constants.RefinedHomesteadMetalItemId));

                // ...and reused as-is by a local override re-solve (no
                // network calls, no overrides) - matching CurrencyValuation's
                // own reuse contract.
                var resolved = pipeline.ResolveWithOverrides(tieredResult.SolveContext, null);
                Assert.Equal(AcquisitionSource.BuyFromVendor, resolved.Plan.Steps[0].Source);
                Assert.Equal(2, resolved.Plan.Steps[0].TotalCost);
            }
        }
    }
}
