using System;
using System.IO;
using System.Text;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RepoFileLocator;

namespace GW2CraftingHelper.Tests.Services
{
    public class DailyCooldownItemServiceTests
    {
        private const string ValidEnvelopeJson = @"{
            ""schemaVersion"": 1,
            ""generatedAt"": ""2026-08-16T00:00:00Z"",
            ""source"": ""wiki.guildwars2.com (manual research)"",
            ""items"": [
                { ""itemId"": 46742, ""perDayCap"": 1, ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Lump_of_Mithrillium"", ""lastVerified"": ""2026-08-16"" },
                { ""itemId"": 43772, ""perDayCap"": 1, ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Charged_Quartz_Crystal"", ""lastVerified"": ""2026-08-16"" }
            ]
        }";

        [Fact]
        public void Load_ValidPayload_ParsesBothEntries()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(ValidEnvelopeJson)))
            {
                var items = DailyCooldownItemService.Load(stream);

                Assert.Equal(2, items.Count);
                Assert.True(items.ContainsKey(46742));
                Assert.True(items.ContainsKey(43772));

                var mithrillium = items[46742];
                Assert.Equal(46742, mithrillium.ItemId);
                Assert.Equal(1, mithrillium.PerDayCap);
                Assert.Equal(
                    "https://wiki.guildwars2.com/wiki/Lump_of_Mithrillium",
                    mithrillium.SourceUrl);
                Assert.Equal("2026-08-16", mithrillium.LastVerified);
            }
        }

        [Fact]
        public void Load_NullStream_ReturnsEmpty()
        {
            var items = DailyCooldownItemService.Load(null);

            Assert.NotNull(items);
            Assert.Empty(items);
        }

        [Fact]
        public void Load_EmptyStream_ReturnsEmpty()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("")))
            {
                var items = DailyCooldownItemService.Load(stream);

                Assert.NotNull(items);
                Assert.Empty(items);
            }
        }

        [Fact]
        public void Load_MalformedJson_ReturnsEmpty_NoThrow()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("{ not valid json ")))
            {
                var items = DailyCooldownItemService.Load(stream);

                Assert.NotNull(items);
                Assert.Empty(items);
            }
        }

        [Fact]
        public void Load_DuplicateItemId_LastWriteWins_NoThrow()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""generatedAt"": ""2026-08-16T00:00:00Z"",
                ""source"": ""test"",
                ""items"": [
                    { ""itemId"": 100, ""perDayCap"": 1, ""sourceUrl"": ""https://example.com/a"", ""lastVerified"": ""2026-01-01"" },
                    { ""itemId"": 100, ""perDayCap"": 5, ""sourceUrl"": ""https://example.com/b"", ""lastVerified"": ""2026-02-02"" }
                ]
            }";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var items = DailyCooldownItemService.Load(stream);

                Assert.Single(items);
                Assert.Equal(5, items[100].PerDayCap);
            }
        }

        [Fact]
        public void Load_ZeroOrNegativeCap_EntrySkipped_NoThrow()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""generatedAt"": ""2026-08-16T00:00:00Z"",
                ""source"": ""test"",
                ""items"": [
                    { ""itemId"": 100, ""perDayCap"": 0, ""sourceUrl"": ""https://example.com/a"", ""lastVerified"": ""2026-01-01"" },
                    { ""itemId"": 101, ""perDayCap"": -1, ""sourceUrl"": ""https://example.com/b"", ""lastVerified"": ""2026-01-01"" },
                    { ""itemId"": 102, ""perDayCap"": 1, ""sourceUrl"": ""https://example.com/c"", ""lastVerified"": ""2026-01-01"" }
                ]
            }";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var items = DailyCooldownItemService.Load(stream);

                Assert.Single(items);
                Assert.True(items.ContainsKey(102));
                Assert.False(items.ContainsKey(100));
                Assert.False(items.ContainsKey(101));
            }
        }

        // --- Shipped seed file (pins the real file against silent drift) ---

        [Fact]
        public void Load_ShippedSeedFile_ParsesAllEntriesWithCitation()
        {
            string path = FindRepoFile(Path.Combine("ref", "daily_cooldown_items.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/daily_cooldown_items.json by walking up from the test assembly's directory.");

            using (var stream = File.OpenRead(path))
            {
                var items = DailyCooldownItemService.Load(stream);

                Assert.InRange(items.Count, 10, 20);
                foreach (var item in items.Values)
                {
                    Assert.True(item.PerDayCap > 0);
                    Assert.False(string.IsNullOrEmpty(item.SourceUrl));
                    Assert.False(string.IsNullOrEmpty(item.LastVerified));
                    Assert.StartsWith("https://wiki.guildwars2.com/", item.SourceUrl);
                }

                // Spot-check the ascended-refinement precursor named in the
                // audit (Lump of Mithrillium, id 46742 - see this file's
                // own header/ref/daily_cooldown_items.json entry).
                Assert.True(items.ContainsKey(46742));
                Assert.Equal(1, items[46742].PerDayCap);
            }
        }

        // FindRepoFile comes from Helpers/RepoFileLocator.cs.
    }
}
