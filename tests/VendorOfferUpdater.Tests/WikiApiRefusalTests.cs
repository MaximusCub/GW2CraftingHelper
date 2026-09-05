using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using VendorOfferUpdater;
using VendorOfferUpdater.Tests.Helpers;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// The wiki refuses queries with HTTP 200 and an "error" body. Both ask
    /// call sites used to test only for a results object, so a refusal read as
    /// "this branch has no vendors" and the run reported a count of skipped
    /// prefixes. These tests pin the three response shapes apart, and pin what
    /// a refusal now costs: retries, then a named unresolved section, and a
    /// run that carries on.
    /// </summary>
    public class WikiApiRefusalTests
    {
        private static QueryOptions FastOptions(
            int maxAttempts = QueryOptions.DefaultMaxAttempts,
            int maxPrefixDepth = 2)
        {
            return new QueryOptions
            {
                DelayBetweenRequestsMs = 0,
                RetryBackoffBaseMs = 0,
                MaxAttempts = maxAttempts,
                MaxTotalRequests = 2000,
                MaxPrefixDepth = maxPrefixDepth,
                MaxRuntime = TimeSpan.FromMinutes(5),
            };
        }

        private static (WikiSmwClient Client, FakeHttpHandler Handler, HttpClient Http) CreateClient()
        {
            var handler = new FakeHttpHandler();
            var httpClient = new HttpClient(handler);
            return (new WikiSmwClient(httpClient), handler, httpClient);
        }

        private static string OneRow(string vendor, int gameId)
        {
            return new WikiJsonBuilder()
                .AddResult("NPC#v" + gameId, gameId: gameId, vendor: vendor)
                .Build();
        }

        // -- The reader ---------------------------------------------
        [Fact]
        public void Reader_TellsRowsFromEmptyFromRefusal()
        {
            using var rows = JsonDocument.Parse(OneRow("Astral Ward Quartermaster", 100));
            Assert.Equal(WikiAskShape.Rows, WikiAskResponse.Read(rows.RootElement).Shape);

            using var empty = JsonDocument.Parse(WikiJsonBuilder.BuildEmpty());
            Assert.Equal(WikiAskShape.NoRows, WikiAskResponse.Read(empty.RootElement).Shape);

            using var refused = JsonDocument.Parse(WikiJsonBuilder.BuildMaxLagError());
            var reading = WikiAskResponse.Read(refused.RootElement);
            Assert.Equal(WikiAskShape.ApiError, reading.Shape);
            Assert.NotNull(reading.Error);
            Assert.Equal("maxlag", reading.Error!.Code);
            Assert.Contains("lagged", reading.Error.Info, StringComparison.Ordinal);
        }

        [Fact]
        public void Reader_ReportsAnHtmlBodyAsAnError()
        {
            // The wiki serves its rate-limit block page as HTML with a 200.
            var error = WikiAskResponse.ReadApiError(
                "<!DOCTYPE html><html><body>Our servers are currently under maintenance.</body></html>");

            Assert.NotNull(error);
            Assert.Equal("unparseable-response", error!.Code);
        }

        [Fact]
        public void Reader_ReportsNoErrorForAResultSet()
        {
            Assert.Null(WikiAskResponse.ReadApiError(OneRow("Miyani", 100)));
            Assert.Null(WikiAskResponse.ReadApiError(WikiJsonBuilder.BuildEmpty()));
        }

        // -- Retry --------------------------------------------------
        [Fact]
        public async Task Refusal_IsRetriedAndThenSucceeds()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            handler.Enqueue(WikiJsonBuilder.BuildMaxLagError());
            handler.Enqueue(OneRow("Astral Ward Quartermaster", 12345));

            var (results, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Single(results);
            Assert.Equal(12345, results[0].GameId);
            Assert.Equal(2, handler.RequestedUrls.Count);
            Assert.Equal(1, stats.TotalHttpRequests);
            Assert.Empty(client.UnresolvedSections);
        }

        [Fact]
        public async Task Refusal_UsesEveryConfiguredAttempt()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            for (int i = 0; i < 5; i++)
            {
                handler.Enqueue(WikiJsonBuilder.BuildMaxLagError());
            }

            await client.QueryVendorItemsAsync("[[Sells item::+]]", FastOptions());

            // Five by default: the first try and four retries.
            Assert.Equal(5, handler.RequestedUrls.Count);
        }

        [Fact]
        public async Task Refusal_HonoursALowerAttemptCount()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            handler.Enqueue(WikiJsonBuilder.BuildMaxLagError());
            handler.Enqueue(WikiJsonBuilder.BuildMaxLagError());

            await client.QueryVendorItemsAsync("[[Sells item::+]]", FastOptions(maxAttempts: 2));

            Assert.Equal(2, handler.RequestedUrls.Count);
            Assert.Equal(2, client.UnresolvedSections[0].Attempts);
        }

        [Fact]
        public async Task ServerLagRefusalWithA503_IsRetried()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            handler.Enqueue(WikiJsonBuilder.BuildMaxLagError(), HttpStatusCode.ServiceUnavailable);
            handler.Enqueue(OneRow("Astral Ward Quartermaster", 777));

            var (results, _) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Single(results);
            Assert.Empty(client.UnresolvedSections);
        }

        // -- Unresolved, not empty ----------------------------------
        [Fact]
        public async Task ExhaustedSection_IsRecordedUnresolvedRatherThanEmpty()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            for (int i = 0; i < QueryOptions.DefaultMaxAttempts; i++)
            {
                handler.Enqueue(WikiJsonBuilder.BuildMaxLagError());
            }

            var (results, _) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Empty(results);

            var section = Assert.Single(client.UnresolvedSections);
            Assert.Equal("partition", section.Kind);
            Assert.Equal("maxlag", section.ErrorCode);
            Assert.Equal(QueryOptions.DefaultMaxAttempts, section.Attempts);
            Assert.Contains("[[Sells item::+]]", section.Condition, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GenuinelyEmptyResults_AreNotRecordedUnresolved()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            handler.Enqueue(WikiJsonBuilder.BuildEmpty());

            var (results, _) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Empty(results);
            Assert.Empty(client.UnresolvedSections);
            Assert.Single(handler.RequestedUrls);
        }

        [Fact]
        public async Task AnHtmlBlockPage_IsRecordedUnresolved()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            for (int i = 0; i < QueryOptions.DefaultMaxAttempts; i++)
            {
                handler.Enqueue("<!DOCTYPE html><html><body>Access denied</body></html>");
            }

            var (results, _) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Empty(results);
            Assert.Equal("unparseable-response", Assert.Single(client.UnresolvedSections).ErrorCode);
        }

        // -- The run continues --------------------------------------
        [Fact]
        public async Task RefusedProbe_RecordsThePrefixAndKeepsScrapingTheRest()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            // Root overflows (a continue-offset that does not advance), which
            // is what sends the scrape down the per-prefix probe path.
            handler.Enqueue(new WikiJsonBuilder()
                .AddResult("NPC#root", gameId: 1, vendor: "Alpha")
                .WithContinueOffset(0)
                .Build());

            // A: refused on every attempt. B: one row. Everything else empty.
            for (int i = 0; i < QueryOptions.DefaultMaxAttempts; i++)
            {
                handler.Enqueue(WikiJsonBuilder.BuildMaxLagError());
            }

            handler.Enqueue(OneRow("Astral Ward Quartermaster", 200));
            handler.Enqueue(OneRow("Astral Ward Quartermaster", 200));

            for (int i = 0; i < 34; i++)
            {
                handler.Enqueue(WikiJsonBuilder.BuildEmpty());
            }

            var (results, _) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions(maxPrefixDepth: 1));

            // The B rows were fetched after A was refused: the run continued.
            Assert.Contains(results, r => r.GameId == 200);

            var section = Assert.Single(client.UnresolvedSections);
            Assert.Equal("probe", section.Kind);
            Assert.Equal("A", section.Prefix);
            Assert.Contains("~A*", section.Condition, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RefusedItemBatch_DoesNotStopTheFollowingBatches()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            for (int i = 0; i < QueryOptions.DefaultMaxAttempts; i++)
            {
                handler.Enqueue(WikiJsonBuilder.BuildMaxLagError());
            }

            handler.Enqueue(
                "{\"query\":{\"results\":{\"Mystic Coin\":{\"printouts\":{\"Has game id\":[19976]}}}}}");

            var names = Enumerable.Range(1, 10).Select(n => "Item " + n).ToList();
            names.Add("Mystic Coin");

            var resolved = await client.ResolveItemGameIdsAsync(
                names, default, FastOptions());

            Assert.Equal(19976, resolved["Mystic Coin"]);

            var section = Assert.Single(client.UnresolvedSections);
            Assert.Equal("item-batch", section.Kind);
            Assert.Equal("item batch 1", section.Label);
            Assert.Contains("Item 1", section.Condition, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AWikiThatStopsAnsweringAltogether_StopsTheRun()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            handler.Enqueue(new WikiJsonBuilder()
                .AddResult("NPC#root", gameId: 1, vendor: "Alpha")
                .WithContinueOffset(0)
                .Build());

            // Three prefixes refused end to end. The fourth is never asked:
            // the run stops rather than putting every remaining prefix
            // through its own attempt ladder against a wiki that has shut.
            for (int i = 0; i < 3 * QueryOptions.DefaultMaxAttempts; i++)
            {
                handler.Enqueue(WikiJsonBuilder.BuildMaxLagError());
            }

            var (results, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions(maxPrefixDepth: 1));

            Assert.Equal(1 + (3 * QueryOptions.DefaultMaxAttempts), handler.RequestedUrls.Count);
            Assert.Equal(3, client.UnresolvedSections.Count);
            Assert.True(stats.WasInterrupted);

            // The root's own row survives the stop.
            Assert.Single(results);
        }

        // -- Request shape ------------------------------------------
        [Fact]
        public async Task EveryAskCarriesMaxLag()
        {
            var (client, handler, http) = CreateClient();
            using var _ = http;

            handler.Enqueue(OneRow("Miyani", 19976));
            handler.Enqueue(
                "{\"query\":{\"results\":{\"Mystic Coin\":{\"printouts\":{\"Has game id\":[19976]}}}}}");

            await client.QueryVendorItemsAsync("[[Sells item::+]]", FastOptions());
            await client.ResolveItemGameIdsAsync(
                new List<string> { "Mystic Coin" }, default, FastOptions());

            Assert.Equal(2, handler.RequestedUrls.Count);
            Assert.All(
                handler.RequestedUrls,
                url => Assert.Contains("maxlag=5", url, StringComparison.Ordinal));
        }
    }
}
