using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingPlanPipelineCurrencyTests
    {
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

        // --- currency metadata wired through the pipeline ---

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
        public async Task GenerateStructuredAsync_WithCurrencyMetadataService_PopulatesCurrencyMetadata()
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

                var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);

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
    }
}
