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
