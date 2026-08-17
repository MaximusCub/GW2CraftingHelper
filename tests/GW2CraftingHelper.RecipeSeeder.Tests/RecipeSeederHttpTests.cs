using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.RecipeSeeder;
using Xunit;

namespace GW2CraftingHelper.RecipeSeeder.Tests
{
    // Review-fix (recipe-ingestion-fix): tools/GW2CraftingHelper.RecipeSeeder
    // previously had no test project at all (tests/VendorOfferUpdater.Tests
    // is this repo's own precedent for testing a tool), so its half of the
    // schema-version fix - identical in spirit to
    // Gw2RecipeApiClientHttpTests' runtime-client coverage - had zero
    // regression coverage. FetchAllRecipeIdsAsync/FetchRecipeBatchAsync are
    // internal (not private) specifically so this project's
    // InternalsVisibleTo (see GW2CraftingHelper.RecipeSeeder.csproj) can
    // reach them with a real HttpClient + stub handler, exercising the
    // actual production code path rather than re-deriving the URL string
    // by hand.
    public class RecipeSeederHttpTests
    {
        private class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _body;

            public StubHandler(HttpStatusCode statusCode, string body)
            {
                _statusCode = statusCode;
                _body = body;
            }

            public Uri LastRequestUri { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequestUri = request.RequestUri;
                var response = new HttpResponseMessage(_statusCode)
                {
                    Content = new StringContent(_body)
                };
                return Task.FromResult(response);
            }
        }

        [Fact]
        public async Task FetchAllRecipeIdsAsync_RequestUri_CarriesSchemaVersion()
        {
            using (var handler = new StubHandler(HttpStatusCode.OK, "[10, 20, 30]"))
            using (var http = new HttpClient(handler))
            {
                var ids = await Program.FetchAllRecipeIdsAsync(http);

                Assert.Equal(new List<int> { 10, 20, 30 }, ids);
                Assert.NotNull(handler.LastRequestUri);
                Assert.Matches(@"[?&]v=\d{4}-\d{2}-\d{2}(&|$)", handler.LastRequestUri.Query);
            }
        }

        [Fact]
        public async Task FetchRecipeBatchAsync_RequestUri_CarriesSchemaVersion()
        {
            string json = @"[{
                ""id"": 10,
                ""output_item_id"": 1,
                ""output_item_count"": 1,
                ""disciplines"": [""Weaponsmith""],
                ""min_rating"": 0,
                ""flags"": [],
                ""ingredients"": [{ ""id"": 2, ""count"": 3 }]
            }]";
            using (var handler = new StubHandler(HttpStatusCode.OK, json))
            using (var http = new HttpClient(handler))
            {
                var recipes = await Program.FetchRecipeBatchAsync(http, new List<int> { 10 });

                Assert.Single(recipes);
                Assert.Equal(10, recipes[0].Id);
                Assert.NotNull(handler.LastRequestUri);
                Assert.Matches(@"[?&]v=\d{4}-\d{2}-\d{2}(&|$)", handler.LastRequestUri.Query);
            }
        }

        [Fact]
        public async Task FetchRecipeBatchAsync_TypedIngredient_ParsesIdKeyWithoutThrowing()
        {
            // KNOWN-ISSUES recipe-ingestion bug class (2026-08-15): the
            // versioned schema keys every ingredient's item id as "id" -
            // the seeder's own ParseRecipeBatch used to unconditionally
            // GetProperty("item_id"), which THROWS a JsonException on this
            // exact shape (every ingredient of a currency-ingredient-era
            // recipe like 14025).
            string json = @"[{
                ""id"": 14025,
                ""output_item_id"": 100930,
                ""output_item_count"": 1,
                ""disciplines"": [],
                ""min_rating"": 400,
                ""flags"": [],
                ""ingredients"": [
                    { ""type"": ""Currency"", ""id"": 78, ""count"": 250 },
                    { ""type"": ""Item"", ""id"": 19721, ""count"": 50 }
                ]
            }]";
            using (var handler = new StubHandler(HttpStatusCode.OK, json))
            using (var http = new HttpClient(handler))
            {
                var recipes = await Program.FetchRecipeBatchAsync(http, new List<int> { 14025 });

                Assert.Single(recipes);
                Assert.Equal(2, recipes[0].Ingredients.Count);
                Assert.Equal("Currency", recipes[0].Ingredients[0].Type);
                Assert.Equal(78, recipes[0].Ingredients[0].Id);
                Assert.Equal(19721, recipes[0].Ingredients[1].Id);
            }
        }
    }
}
