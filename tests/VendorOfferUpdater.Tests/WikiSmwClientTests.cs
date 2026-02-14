using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VendorOfferUpdater;
using VendorOfferUpdater.Tests.Helpers;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    public class WikiSmwClientTests
    {
        private static QueryOptions FastOptions(
            int maxRequests = 2000,
            int maxPrefixDepth = 2)
        {
            return new QueryOptions
            {
                DelayBetweenRequestsMs = 0,
                MaxTotalRequests = maxRequests,
                MaxPrefixDepth = maxPrefixDepth,
                MaxRuntime = TimeSpan.FromMinutes(5)
            };
        }

        private static (WikiSmwClient client, FakeHttpHandler handler) CreateClient()
        {
            var handler = new FakeHttpHandler();
            var httpClient = new HttpClient(handler);
            var client = new WikiSmwClient(httpClient);
            return (client, handler);
        }

        // ── Parsing tests ──────────────────────────────────────────

        [Fact]
        public async Task SingleResult_ParsesAllFields()
        {
            var (client, handler) = CreateClient();
            string json = new WikiJsonBuilder()
                .AddResult("NPC#vendor1",
                    gameId: 19685,
                    itemName: "Iron Ore",
                    quantity: 10,
                    costs: new List<(int, string)> { (100, "Karma") },
                    vendor: "Merchant",
                    locations: new List<string> { "Lion's Arch" })
                .Build();

            handler.Enqueue(json);

            var (results, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Single(results);
            var r = results[0];
            Assert.Equal(19685, r.GameId);
            Assert.Equal("Iron Ore", r.ItemName);
            Assert.Equal(10, r.OutputQuantity);
            Assert.Single(r.CostEntries);
            Assert.Equal(100, r.CostEntries[0].Value);
            Assert.Equal("Karma", r.CostEntries[0].Currency);
            Assert.Equal("Merchant", r.MerchantName);
            Assert.Single(r.Locations);
            Assert.Equal("Lion's Arch", r.Locations[0]);
        }

        [Fact]
        public async Task MultipleCostEntries_AllParsed()
        {
            var (client, handler) = CreateClient();
            string json = new WikiJsonBuilder()
                .AddResult("NPC#v1",
                    gameId: 100,
                    costs: new List<(int, string)> { (50, "Karma"), (10, "Coin") },
                    vendor: "Vendor")
                .Build();

            handler.Enqueue(json);

            var (results, _) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Single(results);
            Assert.Equal(2, results[0].CostEntries.Count);
        }

        [Fact]
        public async Task ZeroValueCost_Excluded()
        {
            var (client, handler) = CreateClient();
            string json = new WikiJsonBuilder()
                .AddResult("NPC#v1",
                    gameId: 100,
                    costs: new List<(int, string)> { (0, "Karma"), (50, "Coin") },
                    vendor: "Vendor")
                .Build();

            handler.Enqueue(json);

            var (results, _) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Single(results);
            Assert.Single(results[0].CostEntries);
            Assert.Equal(50, results[0].CostEntries[0].Value);
        }

        [Fact]
        public async Task EmptyResults_ReturnsEmptyList()
        {
            var (client, handler) = CreateClient();
            handler.Enqueue(WikiJsonBuilder.BuildEmpty());

            var (results, _) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Empty(results);
        }

        // ── Pagination tests ───────────────────────────────────────

        [Fact]
        public async Task Pagination_FollowsContinueOffset()
        {
            var (client, handler) = CreateClient();

            // Page 1: one result, continue at offset 500
            string page1 = new WikiJsonBuilder()
                .AddResult("NPC#v1", gameId: 100, vendor: "VendorA")
                .WithContinueOffset(500)
                .Build();

            // Page 2: different result, no continue
            string page2 = new WikiJsonBuilder()
                .AddResult("NPC#v2", gameId: 200, vendor: "VendorB")
                .Build();

            handler.Enqueue(page1);
            handler.Enqueue(page2);

            var (results, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Equal(2, results.Count);
            Assert.Equal(2, stats.TotalHttpRequests);
            Assert.Equal(2, stats.TotalRowsFetched);
        }

        [Fact]
        public async Task Pagination_DeduplicatesAcrossPages()
        {
            var (client, handler) = CreateClient();

            // Same result on both pages (same GameId, vendor, quantity, costs)
            string page1 = new WikiJsonBuilder()
                .AddResult("NPC#v1", gameId: 100, quantity: 1, vendor: "Vendor")
                .WithContinueOffset(500)
                .Build();

            string page2 = new WikiJsonBuilder()
                .AddResult("NPC#v1_dup", gameId: 100, quantity: 1, vendor: "Vendor")
                .Build();

            handler.Enqueue(page1);
            handler.Enqueue(page2);

            var (results, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Single(results);
            Assert.Equal(1, stats.DuplicatesDiscarded);
        }

        // ── Partitioning tests ─────────────────────────────────────

        [Fact]
        public async Task MaxDepthZero_TruncatesPartition()
        {
            var (client, handler) = CreateClient();

            // Return result + stalled continue-offset (triggers overflow)
            string json = new WikiJsonBuilder()
                .AddResult("NPC#v1", gameId: 100, vendor: "Vendor")
                .WithContinueOffset(0)
                .Build();

            handler.Enqueue(json);

            var (results, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions(maxPrefixDepth: 0));

            Assert.Single(results);
            Assert.Equal(1, stats.TruncatedPartitions);
            Assert.True(stats.Partitions[0].WasTruncated);
        }

        [Fact]
        public async Task Overflow_ProbesSubPartitions()
        {
            var (client, handler) = CreateClient();

            // Root: result + stalled offset → overflow
            string rootJson = new WikiJsonBuilder()
                .AddResult("NPC#v1", gameId: 100, vendor: "Alpha")
                .WithContinueOffset(0)
                .Build();
            handler.Enqueue(rootJson);

            // Probes happen sequentially (A-Z, 0-9). When a probe is non-empty,
            // the full pagination for that prefix runs IMMEDIATELY before the next probe.
            // So the queue order is: A-probe → A-pagination → B-probe → C-probe → ...
            string prefixAJson = new WikiJsonBuilder()
                .AddResult("NPC#v2", gameId: 200, vendor: "Alpha Vendor")
                .Build();

            for (int i = 0; i < 36; i++)
            {
                char c = i < 26 ? (char)('A' + i) : (char)('0' + i - 26);
                if (c == 'A')
                {
                    // Non-empty probe, then immediately the full pagination response
                    handler.Enqueue("{\"query\":{\"results\":{\"SomeResult\":{}}}}");
                    handler.Enqueue(prefixAJson);
                }
                else
                {
                    handler.Enqueue(WikiJsonBuilder.BuildEmpty());
                }
            }

            var (results, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions(maxPrefixDepth: 1));

            // Root result + prefix A result (dedup may merge if same composite key)
            Assert.True(results.Count >= 1);
            // 1 root + 36 probes + 1 full pagination = 38
            Assert.Equal(38, stats.TotalHttpRequests);
        }

        // ── Safety & cancellation tests ────────────────────────────

        [Fact]
        public async Task MaxTotalRequests_ReturnsPartialWithInterrupted()
        {
            var (client, handler) = CreateClient();

            // First request succeeds with continue-offset
            string page1 = new WikiJsonBuilder()
                .AddResult("NPC#v1", gameId: 100, vendor: "Vendor")
                .WithContinueOffset(500)
                .Build();
            handler.Enqueue(page1);

            // Second request would be needed but limit=1 exhausted after first
            var (results, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions(maxRequests: 1));

            Assert.Single(results);
            Assert.True(stats.WasInterrupted);
        }

        [Fact]
        public async Task CancelledToken_ThrowsOperationCanceled()
        {
            var (client, handler) = CreateClient();
            handler.Enqueue(WikiJsonBuilder.BuildEmpty());

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                client.QueryVendorItemsAsync("[[Sells item::+]]", FastOptions(), cts.Token));
        }

        // ── Retry test ─────────────────────────────────────────────

        [Fact]
        public async Task Http429_RetriesAndSucceeds()
        {
            var (client, handler) = CreateClient();

            // First response: 429
            handler.Enqueue("{}", HttpStatusCode.TooManyRequests);

            // Second response: success
            string json = new WikiJsonBuilder()
                .AddResult("NPC#v1", gameId: 100, vendor: "Vendor")
                .Build();
            handler.Enqueue(json);

            var (results, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Single(results);
            Assert.Equal(1, stats.TotalHttpRequests);
            // 2 actual HTTP calls (429 + 200), but stats only count logical requests
            Assert.Equal(2, handler.RequestedUrls.Count);
        }

        // ── DryRun test ────────────────────────────────────────────

        [Fact]
        public async Task DryRun_MakesNoHttpRequests()
        {
            var (client, handler) = CreateClient();

            var options = new QueryOptions
            {
                DryRun = true,
                DelayBetweenRequestsMs = 0
            };

            var (results, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", options);

            Assert.Empty(results);
            Assert.Empty(handler.RequestedUrls);
        }

        // ── Stats tests ────────────────────────────────────────────

        [Fact]
        public async Task Stats_TracksRequestsAndRows()
        {
            var (client, handler) = CreateClient();

            string json = new WikiJsonBuilder()
                .AddResult("NPC#v1", gameId: 100, vendor: "VendorA")
                .AddResult("NPC#v2", gameId: 200, vendor: "VendorB")
                .Build();
            handler.Enqueue(json);

            var (_, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Equal(1, stats.TotalHttpRequests);
            Assert.Equal(2, stats.TotalRowsFetched);
            Assert.Equal(2, stats.DistinctResults);
            Assert.Equal(0, stats.DuplicatesDiscarded);
        }

        [Fact]
        public async Task NonAlphaVendorNames_Detected()
        {
            var (client, handler) = CreateClient();

            string json = new WikiJsonBuilder()
                .AddResult("NPC#v1", gameId: 100, vendor: "#Special Vendor")
                .AddResult("NPC#v2", gameId: 200, vendor: "Normal Vendor")
                .Build();
            handler.Enqueue(json);

            var (_, stats) = await client.QueryVendorItemsAsync(
                "[[Sells item::+]]", FastOptions());

            Assert.Single(stats.NonAlphaVendors);
            Assert.Equal("#Special Vendor", stats.NonAlphaVendors[0]);
        }

        // ── ResolveItemGameIdsAsync tests ──────────────────────────

        [Fact]
        public async Task ResolveItemGameIds_SingleBatch()
        {
            var (client, handler) = CreateClient();

            // Need to call QueryVendorItemsAsync first to initialize _effectiveDelay
            handler.Enqueue(WikiJsonBuilder.BuildEmpty());
            await client.QueryVendorItemsAsync("[[Sells item::+]]", FastOptions());

            // Resolution response
            string resolveJson =
                "{\"query\":{\"results\":{" +
                "\"Iron Ore\":{\"printouts\":{\"Has game id\":[19699]}}," +
                "\"Copper Ore\":{\"printouts\":{\"Has game id\":[19697]}}" +
                "}}}";
            handler.Enqueue(resolveJson);

            var resolved = await client.ResolveItemGameIdsAsync(
                new[] { "Iron Ore", "Copper Ore" });

            Assert.Equal(2, resolved.Count);
            Assert.Equal(19699, resolved["Iron Ore"]);
            Assert.Equal(19697, resolved["Copper Ore"]);
        }

        [Fact]
        public async Task ResolveItemGameIds_BatchesOver10()
        {
            var (client, handler) = CreateClient();

            // Initialize delay
            handler.Enqueue(WikiJsonBuilder.BuildEmpty());
            await client.QueryVendorItemsAsync("[[Sells item::+]]", FastOptions());

            int initialUrlCount = handler.RequestedUrls.Count;

            // 15 items → 2 batches (10 + 5)
            var items = Enumerable.Range(1, 15)
                .Select(i => $"Item{i}")
                .ToList();

            // Batch 1 response
            var batch1Results = Enumerable.Range(1, 10)
                .Select(i => $"\"Item{i}\":{{\"printouts\":{{\"Has game id\":[{i}]}}}}");
            handler.Enqueue(
                "{\"query\":{\"results\":{" + string.Join(",", batch1Results) + "}}}");

            // Batch 2 response
            var batch2Results = Enumerable.Range(11, 5)
                .Select(i => $"\"Item{i}\":{{\"printouts\":{{\"Has game id\":[{i}]}}}}");
            handler.Enqueue(
                "{\"query\":{\"results\":{" + string.Join(",", batch2Results) + "}}}");

            var resolved = await client.ResolveItemGameIdsAsync(items);

            Assert.Equal(15, resolved.Count);
            // 2 batch requests made
            Assert.Equal(initialUrlCount + 2, handler.RequestedUrls.Count);
        }

        [Fact]
        public async Task ResolveItemGameIds_UnresolvedItem_NotInResult()
        {
            var (client, handler) = CreateClient();

            handler.Enqueue(WikiJsonBuilder.BuildEmpty());
            await client.QueryVendorItemsAsync("[[Sells item::+]]", FastOptions());

            // Response has Iron Ore but not Fake Item
            string resolveJson =
                "{\"query\":{\"results\":{" +
                "\"Iron Ore\":{\"printouts\":{\"Has game id\":[19699]}}" +
                "}}}";
            handler.Enqueue(resolveJson);

            var resolved = await client.ResolveItemGameIdsAsync(
                new[] { "Iron Ore", "Fake Item" });

            Assert.Single(resolved);
            Assert.True(resolved.ContainsKey("Iron Ore"));
            Assert.False(resolved.ContainsKey("Fake Item"));
        }
    }
}
