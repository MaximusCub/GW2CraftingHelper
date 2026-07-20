using System.IO;
using System.Text;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class AcquisitionHintServiceTests
    {
        // Mirrors the real ref/acquisition_hints_seed.json content (the
        // five wiki-verified entries from docs/KNOWN-ISSUES.md item 8) so
        // this test also guards the production seed file's shape.
        private const string ValidEnvelopeJson = @"{
            ""schemaVersion"": 1,
            ""generatedAt"": ""2026-07-20T00:00:00Z"",
            ""source"": ""wiki.guildwars2.com (manual research)"",
            ""hints"": [
                { ""itemId"": 71994, ""hint"": ""Salvaged from ascended weapons and armor with an Ascended Salvage Kit (guaranteed) or ascended trinkets/back items (low chance). Account bound; not tradable."", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Ball_of_Dark_Energy"", ""lastVerified"": ""2026-07-20"" },
                { ""itemId"": 70698, ""hint"": ""Received for completing map exploration of Dragon's Stand. Account bound; not tradable; no recipe."", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Gift_of_the_Jungle"", ""lastVerified"": ""2026-07-20"" },
                { ""itemId"": 70797, ""hint"": ""Received for completing map exploration of Verdant Brink. Account bound; not tradable; no recipe."", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Gift_of_the_Fleet"", ""lastVerified"": ""2026-07-20"" },
                { ""itemId"": 71943, ""hint"": ""Received for completing map exploration of Auric Basin. Account bound; not tradable; no recipe."", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Gift_of_Tarir"", ""lastVerified"": ""2026-07-20"" },
                { ""itemId"": 74528, ""hint"": ""Received for completing map exploration of Tangled Depths. Account bound; not tradable; no recipe."", ""sourceUrl"": ""https://wiki.guildwars2.com/wiki/Gift_of_the_Chak"", ""lastVerified"": ""2026-07-20"" }
            ]
        }";

        [Fact]
        public void Load_ValidPayload_ParsesAllFiveEntries()
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(ValidEnvelopeJson)))
            {
                var hints = AcquisitionHintService.Load(stream);

                Assert.Equal(5, hints.Count);
                Assert.True(hints.ContainsKey(71994));
                Assert.True(hints.ContainsKey(70698));
                Assert.True(hints.ContainsKey(70797));
                Assert.True(hints.ContainsKey(71943));
                Assert.True(hints.ContainsKey(74528));

                var ballOfDarkEnergy = hints[71994];
                Assert.Equal(71994, ballOfDarkEnergy.ItemId);
                Assert.False(string.IsNullOrEmpty(ballOfDarkEnergy.Hint));
                Assert.Equal(
                    "https://wiki.guildwars2.com/wiki/Ball_of_Dark_Energy",
                    ballOfDarkEnergy.SourceUrl);
                Assert.Equal("2026-07-20", ballOfDarkEnergy.LastVerified);
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
    }
}
