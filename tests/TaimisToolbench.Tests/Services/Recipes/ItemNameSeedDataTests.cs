using System.IO;
using System.Linq;
using System.Text;
using TaimisToolbench.Services.Recipes;
using TaimisToolbench.Tests.Helpers;
using Xunit;
using static TaimisToolbench.Tests.Helpers.RepoFileLocator;

namespace TaimisToolbench.Tests.Services.Recipes
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

        // --- ref/item_name_seed.json (2.1MB) was
        // never exercised through the production ItemNameSeedData.Load by
        // a committed test - mirrors the FindRepoFile shipped-file pattern
        // established in AcquisitionHintServiceTests /
        // RecipeCacheSerializerTests. Pins the real loader's parsed shape
        // (exact count + a known row) through the actual shipped file.
        // This file also happens to ship with a leading UTF-8 BOM, so it
        // incidentally exercises that path too, though the
        // ReadToEnd->DeserializeAsync switch was never at risk of a BOM
        // regression (both paths handle this file's BOM correctly).
        [Fact]
        public void Load_ShippedSeedFile_ParsesAllItemsIncludingLeadingBom()
        {
            string path = FindRepoFile(Path.Combine("ref", "item_name_seed.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/item_name_seed.json by walking up from the test assembly's directory.");

            using (var stream = File.OpenRead(path))
            {
                var data = ItemNameSeedData.Load(stream);

                // Row count and bytes against the seeder's own manifest -
                // see ShippedSeedManifest for why the literal that used to
                // sit here, under a changelog of every past reseed, was a
                // tripwire a contributor could only silence by editing it.
                ShippedSeedManifest.AssertRecipeSeedMatches(
                    "item_name_seed.json", data.Items.Count);
                Assert.InRange(data.Items.Count, 12000, 30000);
                Assert.All(data.Items, item =>
                {
                    Assert.True(item.Id > 0);
                    Assert.False(string.IsNullOrEmpty(item.Name));
                });

                var agonyInfusion = data.Items.Single(i => i.Id == 49433);
                Assert.Equal("+10 Agony Infusion", agonyInfusion.Name);
                Assert.False(string.IsNullOrEmpty(agonyInfusion.Icon));
            }
        }
    }
}
