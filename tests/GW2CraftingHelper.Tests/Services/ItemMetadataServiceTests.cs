using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class ItemMetadataServiceTests
    {
        [Fact]
        public async Task SingleItem_ReturnsNameAndIcon()
        {
            var api = new InMemoryItemApiClient();
            api.AddItem(19685, "Mithril Ingot", "https://example.com/mithril.png");
            var svc = new ItemMetadataService(api);

            var result = await svc.GetMetadataAsync(new[] { 19685 }, CancellationToken.None);

            Assert.True(result.ContainsKey(19685));
            Assert.Equal("Mithril Ingot", result[19685].Name);
            Assert.Equal("https://example.com/mithril.png", result[19685].IconUrl);
        }

        [Fact]
        public async Task TransientPartialResponse_RetriedOnce()
        {
            var api = new InMemoryItemApiClient();
            api.AddItem(1, "A", "a.png");
            api.AddItem(2, "B", "b.png");
            api.DropOnce.Add(2);
            var svc = new ItemMetadataService(api);

            var result = await svc.GetMetadataAsync(new[] { 1, 2 }, CancellationToken.None);

            // Second request healed the dropped id
            Assert.True(result.ContainsKey(2));
            Assert.Equal("B", result[2].Name);
            Assert.Equal(2, api.Calls.Count);
        }

        [Fact]
        public async Task SeedFallback_UsedForMissingIds_AndMissesNegativeCached()
        {
            var api = new InMemoryItemApiClient();
            api.AddItem(1, "Api Name", "api.png", "Exotic");
            var seed = new GW2CraftingHelper.Services.Recipes.ItemNameSeedData(
                new List<GW2CraftingHelper.Services.Recipes.ItemNameEntry>
                {
                    new GW2CraftingHelper.Services.Recipes.ItemNameEntry
                    {
                        Id = 2, Name = "Seed Name", Icon = "seed.png"
                    }
                });
            var svc = new ItemMetadataService(api, seed);

            var first = await svc.GetMetadataAsync(new[] { 1, 2 }, CancellationToken.None);

            Assert.Equal("Api Name", first[1].Name);
            Assert.Equal("Seed Name", first[2].Name);
            Assert.Equal("seed.png", first[2].IconUrl);
            Assert.Null(first[2].Rarity);

            int callsAfterFirst = api.Calls.Count;

            // Item 2 is now negative-cached as genuinely missing: even
            // though the API "recovers", the service must not pay another
            // round trip for it within this session - it keeps serving the
            // seed fallback and issues zero additional API calls.
            api.AddItem(2, "Api Late", "late.png", "Rare");
            var second = await svc.GetMetadataAsync(new[] { 2 }, CancellationToken.None);
            Assert.Equal("Seed Name", second[2].Name);
            Assert.Equal(callsAfterFirst, api.Calls.Count);
        }

        [Fact]
        public async Task KnownMissingId_SecondCall_PerformsZeroAdditionalApiCalls()
        {
            var api = new InMemoryItemApiClient();
            var svc = new ItemMetadataService(api);

            await svc.GetMetadataAsync(new[] { 99999 }, CancellationToken.None);
            int callsAfterFirst = api.Calls.Count;

            await svc.GetMetadataAsync(new[] { 99999 }, CancellationToken.None);

            Assert.Equal(callsAfterFirst, api.Calls.Count);
        }

        [Fact]
        public async Task RetryWaveFailure_DegradesToPartialResult_DoesNotThrow()
        {
            var api = new InMemoryItemApiClient();
            api.AddItem(1, "A", "a.png");
            api.AddItem(2, "B", "b.png");
            api.DropOnce.Add(2);
            // Call 1 is the first wave (drops id 2); call 2 is the retry
            // wave for the straggler - make it fail transiently.
            api.ThrowOnCallNumber = 2;
            var svc = new ItemMetadataService(api);

            var result = await svc.GetMetadataAsync(new[] { 1, 2 }, CancellationToken.None);

            Assert.True(result.ContainsKey(1));
            Assert.Equal("A", result[1].Name);
            Assert.False(result.ContainsKey(2));
        }

        [Fact]
        public async Task Rarity_FlowsThroughFromApi()
        {
            var api = new InMemoryItemApiClient();
            api.AddItem(19685, "Mithril Ingot", "https://example.com/mithril.png", "Basic");
            api.AddItem(30684, "Frostfang", "https://example.com/ff.png");
            var svc = new ItemMetadataService(api);

            var result = await svc.GetMetadataAsync(new[] { 19685, 30684 }, CancellationToken.None);

            Assert.Equal("Basic", result[19685].Rarity);
            Assert.Null(result[30684].Rarity);
        }

        [Fact]
        public async Task ItemAbsentFromApi_NotInDictionary()
        {
            var api = new InMemoryItemApiClient();
            var svc = new ItemMetadataService(api);

            var result = await svc.GetMetadataAsync(new[] { 99999 }, CancellationToken.None);

            Assert.False(result.ContainsKey(99999));
        }

        [Fact]
        public async Task Batching_LargeSetSplitIntoChunks()
        {
            var api = new InMemoryItemApiClient();
            var ids = new List<int>();
            for (int i = 1; i <= 250; i++)
            {
                api.AddItem(i, $"Item {i}", $"https://example.com/{i}.png");
                ids.Add(i);
            }
            var svc = new ItemMetadataService(api);

            var result = await svc.GetMetadataAsync(ids, CancellationToken.None);

            Assert.Equal(250, result.Count);
            Assert.Equal(2, api.Calls.Count);
            Assert.Equal(200, api.Calls[0].Count);
            Assert.Equal(50, api.Calls[1].Count);
        }

        [Fact]
        public async Task Caching_SecondCallSkipsAlreadyFetched()
        {
            var api = new InMemoryItemApiClient();
            api.AddItem(1, "Item A", "a.png");
            api.AddItem(2, "Item B", "b.png");
            api.AddItem(3, "Item C", "c.png");
            var svc = new ItemMetadataService(api);

            await svc.GetMetadataAsync(new[] { 1, 2 }, CancellationToken.None);
            var result = await svc.GetMetadataAsync(new[] { 2, 3 }, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, api.Calls.Count);
            Assert.Single(api.Calls[1]); // only item 3
            Assert.Equal(3, api.Calls[1][0]);
        }
    }
}
