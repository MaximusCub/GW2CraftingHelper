using System.IO;
using System.Text;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class ItemSearchProviderFactoryTests
    {
        [Fact]
        public void ValidSeed_ReturnsCraftableProvider()
        {
            string json = @"[
                {""id"": 100, ""name"": ""Iron Ore"", ""icon"": ""iron.png""},
                {""id"": 200, ""name"": ""Copper Ore"", ""icon"": ""copper.png""}
            ]";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var provider = ItemSearchProviderFactory.Create(stream, out string reason, out _);

                Assert.IsType<CraftableItemSearchProvider>(provider);
                Assert.Null(reason);
            }
        }

        [Fact]
        public void EmptyArray_ReturnsFallbackWithReason()
        {
            string json = "[]";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var provider = ItemSearchProviderFactory.Create(stream, out string reason, out _);

                Assert.IsType<StaticItemSearchProvider>(provider);
                Assert.NotNull(reason);
                Assert.Contains("no items", reason);
            }
        }

        [Fact]
        public void NullStream_ReturnsFallbackWithReason()
        {
            var provider = ItemSearchProviderFactory.Create(null, out string reason, out _);

            Assert.IsType<StaticItemSearchProvider>(provider);
            Assert.NotNull(reason);
            Assert.Contains("null", reason);
        }

        [Fact]
        public void InvalidJson_ReturnsFallbackWithoutThrowing()
        {
            string json = "not valid json at all";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var provider = ItemSearchProviderFactory.Create(stream, out string reason, out _);

                Assert.IsType<StaticItemSearchProvider>(provider);
                Assert.NotNull(reason);
            }
        }

        [Fact]
        public async Task ValidSeed_ProviderActuallySearches()
        {
            string json = @"[
                {""id"": 46762, ""name"": ""Zojja's Claymore"", ""icon"": ""z.png""},
                {""id"": 19684, ""name"": ""Mithril Ingot"", ""icon"": ""m.png""}
            ]";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var provider = ItemSearchProviderFactory.Create(stream, out _, out _);

                var results = await provider.SearchAsync(
                    "Zojja", 10, System.Threading.CancellationToken.None);

                Assert.Single(results);
                Assert.Equal(46762, results[0].ItemId);
                Assert.Equal("Zojja's Claymore", results[0].Name);
            }
        }
    }
}
