using System;
using System.IO;
using System.Text;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class AcquisitionHintServiceTests
    {
        // Mirrors the real ref/acquisition_hints_seed.json content (the
        // five wiki-verified entries from docs/KNOWN-ISSUES.md item 8, plus
        // the M33 item-17 Gift of Battle entry) so this test also guards
        // the production seed file's shape.
        private const string ValidEnvelopeJson = @"{
            ""schemaVersion"": 1,
            ""generatedAt"": ""2026-07-20T00:00:00Z"",
            ""source"": ""wiki.guildwars2.com (manual research)"",
            ""hints"": [
                { ""itemId"": 71994, ""hint"": ""Salvaged from ascended weapons and armor with an Ascended Salvage Kit (guaranteed) or ascended trinkets/back items (low chance). Account bound; not tradable."", ""badge"": ""SALVAGE"", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Ball_of_Dark_Energy"", ""lastVerified"": ""2026-07-20"" },
                { ""itemId"": 70698, ""hint"": ""Received for completing map exploration of Dragon's Stand. Account bound; not tradable; no recipe."", ""badge"": ""EXPLORE"", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Gift_of_the_Jungle"", ""lastVerified"": ""2026-07-20"" },
                { ""itemId"": 70797, ""hint"": ""Received for completing map exploration of Verdant Brink. Account bound; not tradable; no recipe."", ""badge"": ""EXPLORE"", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Gift_of_the_Fleet"", ""lastVerified"": ""2026-07-20"" },
                { ""itemId"": 71943, ""hint"": ""Received for completing map exploration of Auric Basin. Account bound; not tradable; no recipe."", ""badge"": ""EXPLORE"", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Gift_of_Tarir"", ""lastVerified"": ""2026-07-20"" },
                { ""itemId"": 74528, ""hint"": ""Received for completing map exploration of Tangled Depths. Account bound; not tradable; no recipe."", ""badge"": ""EXPLORE"", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Gift_of_the_Chak"", ""lastVerified"": ""2026-07-20"" },
                { ""itemId"": 19678, ""hint"": ""Obtained from the Gift of Battle Item Reward Track (WvW). Formerly purchasable from Battle Master for 500 Badges of Honor; that vendor path was removed in the Spring 2016 Quarterly Update. Account bound; not tradable; no recipe."", ""badge"": ""WVW"", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Gift_of_Battle"", ""lastVerified"": ""2026-07-20"" }
            ]
        }";

        [Fact]
        public void Load_ValidPayload_ParsesAllSixEntries()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(ValidEnvelopeJson)))
            {
                var hints = AcquisitionHintService.Load(stream);

                Assert.Equal(6, hints.Count);
                Assert.True(hints.ContainsKey(71994));
                Assert.True(hints.ContainsKey(70698));
                Assert.True(hints.ContainsKey(70797));
                Assert.True(hints.ContainsKey(71943));
                Assert.True(hints.ContainsKey(74528));
                Assert.True(hints.ContainsKey(19678));

                var ballOfDarkEnergy = hints[71994];
                Assert.Equal(71994, ballOfDarkEnergy.ItemId);
                Assert.False(string.IsNullOrEmpty(ballOfDarkEnergy.Hint));
                Assert.Equal("SALVAGE", ballOfDarkEnergy.Badge);
                Assert.Equal(
                    "https://wiki.guildwars2.com/wiki/Ball_of_Dark_Energy",
                    ballOfDarkEnergy.SourceUrl);
                Assert.Equal("2026-07-20", ballOfDarkEnergy.LastVerified);

                Assert.Equal("EXPLORE", hints[70698].Badge);
                Assert.Equal("EXPLORE", hints[70797].Badge);
                Assert.Equal("EXPLORE", hints[71943].Badge);
                Assert.Equal("EXPLORE", hints[74528].Badge);

                var giftOfBattle = hints[19678];
                Assert.Equal("WVW", giftOfBattle.Badge);
                Assert.Equal(
                    "https://wiki.guildwars2.com/wiki/Gift_of_Battle",
                    giftOfBattle.SourceUrl);
            }
        }

        [Fact]
        public void Load_EntryWithoutBadge_BadgeIsNull()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""generatedAt"": ""2026-07-20T00:00:00Z"",
                ""source"": ""test"",
                ""hints"": [
                    { ""itemId"": 100, ""hint"": ""no badge here"", ""sourceUrl"": ""https://example.com/a"", ""lastVerified"": ""2026-01-01"" }
                ]
            }";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var hints = AcquisitionHintService.Load(stream);

                Assert.Single(hints);
                Assert.Equal("no badge here", hints[100].Hint);
                Assert.Null(hints[100].Badge);
            }
        }

        [Fact]
        public void Load_NullStream_ReturnsEmpty()
        {
            var hints = AcquisitionHintService.Load(null);

            Assert.NotNull(hints);
            Assert.Empty(hints);
        }

        [Fact]
        public void Load_EmptyStream_ReturnsEmpty()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("")))
            {
                var hints = AcquisitionHintService.Load(stream);

                Assert.NotNull(hints);
                Assert.Empty(hints);
            }
        }

        [Fact]
        public void Load_MalformedJson_ReturnsEmpty_NoThrow()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("{ not valid json ")))
            {
                var hints = AcquisitionHintService.Load(stream);

                Assert.NotNull(hints);
                Assert.Empty(hints);
            }
        }

        [Fact]
        public void Load_DuplicateItemId_LastWriteWins_NoThrow()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""generatedAt"": ""2026-07-20T00:00:00Z"",
                ""source"": ""test"",
                ""hints"": [
                    { ""itemId"": 100, ""hint"": ""first"", ""sourceUrl"": ""https://example.com/a"", ""lastVerified"": ""2026-01-01"" },
                    { ""itemId"": 100, ""hint"": ""second"", ""sourceUrl"": ""https://example.com/b"", ""lastVerified"": ""2026-02-02"" }
                ]
            }";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var hints = AcquisitionHintService.Load(stream);

                Assert.Single(hints);
                Assert.Equal("second", hints[100].Hint);
            }
        }

        // --- Shipped seed file (pins the real file against silent drift) ---

        [Fact]
        public void Load_ShippedSeedFile_ParsesSixEntriesWithHintAndBadge()
        {
            string path = FindRepoFile(Path.Combine("ref", "acquisition_hints_seed.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/acquisition_hints_seed.json by walking up from the test assembly's directory.");

            using (var stream = File.OpenRead(path))
            {
                var hints = AcquisitionHintService.Load(stream);

                Assert.Equal(6, hints.Count);
                foreach (var hint in hints.Values)
                {
                    Assert.False(string.IsNullOrEmpty(hint.Hint));
                    Assert.False(string.IsNullOrEmpty(hint.Badge));
                }
            }
        }

        /// <summary>
        /// Walks up from the running test assembly's directory looking for
        /// relativePath, so this test finds the repo's ref/ folder
        /// regardless of build configuration (Debug/Release) or platform
        /// subfolder depth. Returns null if not found within a reasonable
        /// number of levels, rather than throwing or scanning unrelated
        /// directories.
        /// </summary>
        private static string FindRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; dir != null && i < 12; i++)
            {
                string candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
