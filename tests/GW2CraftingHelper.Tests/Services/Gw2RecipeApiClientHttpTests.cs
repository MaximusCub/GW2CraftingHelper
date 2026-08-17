using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // KNOWN-ISSUES api-degradation F5: Gw2RecipeApiClient previously called
    // the classic HttpClient.GetStringAsync(url) overload (no
    // CancellationToken parameter exists for it on net472), silently making
    // its own `ct` parameter a no-op, and never special-cased 404 the way
    // its siblings (Gw2PriceApiClient/Gw2ItemApiClient) do. These tests
    // exercise the real HTTP call path (mirroring Gw2ApiClient404Tests'
    // established StubHandler pattern) rather than just ParseRecipe's pure
    // JSON parsing, which Gw2RecipeApiClientParseTests already covers.
    public class Gw2RecipeApiClientHttpTests
    {
        private class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _body;

            public StubHandler(HttpStatusCode statusCode, string body = "")
            {
                _statusCode = statusCode;
                _body = body;
            }

            // Captures the actual
            // request URI so tests can assert the schema-version query
            // parameter is present, mirroring StubHandler's own pattern
            // but adding observability rather than changing behavior.
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

        // --- SearchByOutputAsync ---

        [Fact]
        public async Task SearchByOutputAsync_200_ReturnsParsedIds()
        {
            using (var handler = new StubHandler(HttpStatusCode.OK, "[10, 20, 30]"))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2RecipeApiClient(http);
                var result = await client.SearchByOutputAsync(1, CancellationToken.None);

                Assert.Equal(new[] { 10, 20, 30 }, result);
            }
        }

        [Fact]
        public async Task SearchByOutputAsync_RequestUri_CarriesSchemaVersion()
        {
            // The actual regression this
            // branch exists to fix - Gw2RecipeApiClient.SchemaVersion's "v="
            // query parameter - had zero coverage before this test.
            // ParseRecipe-only tests pass identically whether or not the
            // request URL is versioned, so deleting "&v={SchemaVersion}"
            // from the client would leave a fully green suite without this.
            using (var handler = new StubHandler(HttpStatusCode.OK, "[10, 20, 30]"))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2RecipeApiClient(http);
                await client.SearchByOutputAsync(1, CancellationToken.None);

                Assert.NotNull(handler.LastRequestUri);
                Assert.Matches(@"[?&]v=\d{4}-\d{2}-\d{2}(&|$)", handler.LastRequestUri.Query);
            }
        }

        [Fact]
        public async Task SearchByOutputAsync_404_ReturnsEmptyList()
        {
            // Real GW2 API 404 shape, matching Gw2ApiClient404Tests'
            // sibling coverage for prices/items.
            using (var handler = new StubHandler(HttpStatusCode.NotFound,
                @"{""text"":""no results""}"))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2RecipeApiClient(http);
                var result = await client.SearchByOutputAsync(99999, CancellationToken.None);

                Assert.Empty(result);
            }
        }

        [Fact]
        public async Task SearchByOutputAsync_500_ThrowsWithStatusCode()
        {
            using (var handler = new StubHandler(HttpStatusCode.InternalServerError))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2RecipeApiClient(http);

                var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                    client.SearchByOutputAsync(1, CancellationToken.None));

                Assert.Contains("500", ex.Message);
            }
        }

        // --- GetRecipeAsync ---

        [Fact]
        public async Task GetRecipeAsync_200_ReturnsParsedRecipe()
        {
            var json = @"{
                ""id"": 10,
                ""output_item_id"": 1,
                ""output_item_count"": 1,
                ""disciplines"": [""Weaponsmith""],
                ""min_rating"": 0,
                ""flags"": [],
                ""ingredients"": [{ ""item_id"": 2, ""count"": 3 }]
            }";
            using (var handler = new StubHandler(HttpStatusCode.OK, json))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2RecipeApiClient(http);
                var recipe = await client.GetRecipeAsync(10, CancellationToken.None);

                Assert.NotNull(recipe);
                Assert.Equal(10, recipe.Id);
                Assert.Equal(1, recipe.OutputItemId);
                Assert.Single(recipe.Ingredients);
            }
        }

        [Fact]
        public async Task GetRecipeAsync_RequestUri_CarriesSchemaVersion()
        {
            // Same coverage gap as
            // SearchByOutputAsync_RequestUri_CarriesSchemaVersion, for the
            // /v2/recipes/{id} detail call - see that test's doc comment.
            var json = @"{
                ""id"": 10,
                ""output_item_id"": 1,
                ""output_item_count"": 1,
                ""disciplines"": [""Weaponsmith""],
                ""min_rating"": 0,
                ""flags"": [],
                ""ingredients"": [{ ""item_id"": 2, ""count"": 3 }]
            }";
            using (var handler = new StubHandler(HttpStatusCode.OK, json))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2RecipeApiClient(http);
                await client.GetRecipeAsync(10, CancellationToken.None);

                Assert.NotNull(handler.LastRequestUri);
                Assert.Matches(@"[?&]v=\d{4}-\d{2}-\d{2}(&|$)", handler.LastRequestUri.Query);
            }
        }

        [Fact]
        public async Task GetRecipeAsync_404_ReturnsNull()
        {
            using (var handler = new StubHandler(HttpStatusCode.NotFound,
                @"{""text"":""no such recipe""}"))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2RecipeApiClient(http);
                var recipe = await client.GetRecipeAsync(99999, CancellationToken.None);

                Assert.Null(recipe);
            }
        }

        [Fact]
        public async Task GetRecipeAsync_500_ThrowsWithStatusCode()
        {
            using (var handler = new StubHandler(HttpStatusCode.InternalServerError))
            using (var http = new HttpClient(handler))
            {
                var client = new Gw2RecipeApiClient(http);

                var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
                    client.GetRecipeAsync(10, CancellationToken.None));

                Assert.Contains("500", ex.Message);
            }
        }

        // --- ct threading (KNOWN-ISSUES api-degradation F5's core defect) ---
        //
        // A plain token-equality check against the handler's received token
        // is not reliable here: HttpClient.GetAsync(url, ct) links the
        // caller's token with its own internal machinery before it reaches
        // SendAsync, so the token instance the handler observes is not
        // guaranteed to reference-equal the caller's original token even
        // when it is faithfully honored. The only way to actually prove
        // cancellation propagates end-to-end is to observe a real
        // cancellation take effect: this handler's own Task only completes
        // when the received token fires, so if cancelling the CALLER's
        // token aborts the call, `ct` was genuinely threaded all the way
        // through - exactly what the pre-fix GetStringAsync(url) call
        // (which never received `ct` at all on this project's net472
        // target) could never do.
        private class CancellationAwareHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var tcs = new TaskCompletionSource<HttpResponseMessage>();
                cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
                return tcs.Task; // never completes on its own
            }
        }

        [Fact]
        public async Task SearchByOutputAsync_CancellingCallerToken_CancelsInFlightRequest()
        {
            using (var handler = new CancellationAwareHandler())
            using (var http = new HttpClient(handler))
            using (var cts = new CancellationTokenSource())
            {
                var client = new Gw2RecipeApiClient(http);
                var task = client.SearchByOutputAsync(1, cts.Token);

                cts.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            }
        }

        [Fact]
        public async Task GetRecipeAsync_CancellingCallerToken_CancelsInFlightRequest()
        {
            using (var handler = new CancellationAwareHandler())
            using (var http = new HttpClient(handler))
            using (var cts = new CancellationTokenSource())
            {
                var client = new Gw2RecipeApiClient(http);
                var task = client.GetRecipeAsync(10, cts.Token);

                cts.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            }
        }
    }
}
