using System.IO;
using System.Text;
using GW2CraftingHelper.Services.Recipes;
using Xunit;

namespace GW2CraftingHelper.Tests.Services.Recipes
{
    public class ItemNameSeedDataTests
    {
        [Fact]
        public void Load_ValidJson_ReturnsEntries()
        {
            string json = @"[
                {""id"": 100, ""name"": ""Iron Ore"", ""icon"": ""iron.png""},
                {""id"": 200, ""name"": ""Copper Ore"", ""icon"": ""copper.png""}
            ]";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var data = ItemNameSeedData.Load(stream);

                Assert.Equal(2, data.Items.Count);
                Assert.Equal(100, data.Items[0].Id);
                Assert.Equal("Iron Ore", data.Items[0].Name);
                Assert.Equal("iron.png", data.Items[0].Icon);
                Assert.Equal(200, data.Items[1].Id);
            }
        }

        [Fact]
        public void Load_EmptyArray_ReturnsEmpty()
        {
            string json = "[]";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var data = ItemNameSeedData.Load(stream);

                Assert.Empty(data.Items);
            }
        }

        [Fact]
        public void Load_NullStream_ReturnsEmpty()
        {
            var data = ItemNameSeedData.Load(null);

            Assert.Empty(data.Items);
        }

        [Fact]
        public void Constructor_NullItems_DefaultsToEmpty()
        {
            var data = new ItemNameSeedData(null);

            Assert.NotNull(data.Items);
            Assert.Empty(data.Items);
        }
    }
}
