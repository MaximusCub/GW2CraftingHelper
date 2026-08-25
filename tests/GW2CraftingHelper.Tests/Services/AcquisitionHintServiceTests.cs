using System;
using System.IO;
using System.Text;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RepoFileLocator;

namespace GW2CraftingHelper.Tests.Services
{
    public class AcquisitionHintServiceTests
    {
        // In-file fixture with 6 of the now-10 real ref/acquisition_hints_
        // seed.json entries (the five wiki-verified entries from docs/
        // KNOWN-ISSUES #8, plus the Gift of Battle
        // entry) - exercises AcquisitionHintService.Load's parsing shape
        // in isolation. The separate Load_ShippedSeedFile_* test below
        // reads the actual shipped file and pins its real entry count,
        // so drift between this fixture and the production seed is caught
        // there, not here.
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
        public void Load_ShippedSeedFile_ParsesEveryEntryWithHintAndBadge()
        {
            string path = FindRepoFile(Path.Combine("ref", "acquisition_hints_seed.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/acquisition_hints_seed.json by walking up from the test assembly's directory.");

            using (var stream = File.OpenRead(path))
            {
                var hints = AcquisitionHintService.Load(stream);

                // Deliberately still an exact literal, unlike the four
                // machine-generated corpora that moved to manifest digests:
                // acquisition_hints_seed.json is 3KB of HAND-curated rows
                // with no seeder behind it, so there is no regeneration
                // churn to absorb and no manifest a tool could write. Ten
                // rows means the ten below, and adding an eleventh is
                // exactly the edit that should stop and read this test.
                Assert.Equal(10, hints.Count);
                foreach (var hint in hints.Values)
                {
                    Assert.False(string.IsNullOrEmpty(hint.Hint));
                    Assert.False(string.IsNullOrEmpty(hint.Badge));
                }

                // Charged
                // Quartz Crystal (43772) is made at a Place of Power, not
                // via any recipe this module resolves, so it can never be a
                // DailyCooldownItemService.Load Craft-step entry (see
                // DailyCooldownItemServiceTests' own 43772-absence guard).
                // It still resolves to a ShoppingUnknown leaf with zero
                // timegate signal without this hint - e.g. a plan for one
                // Grow Lamp (66993) needs 10x Charged Quartz Crystal and
                // previously emitted no notice at all. This reuses the
                // existing ShoppingUnknown hint/badge path
                // (PlanViewModelBuilder.ResolveHintText/ResolveBadgeText)
                // with no new code.
                Assert.True(hints.ContainsKey(43772));
                Assert.Equal("DAILY", hints[43772].Badge);
                Assert.Contains("1 per day per account", hints[43772].Hint);

                // The three Endless Summer gifts from the field report.
                // Each has an EMPTY search row in the recipe seed - the
                // API knows no recipe - and each is bought with
                // account-bound tokens that have no TP price, so
                // VendorBatchSolver discards the vendor offer the module
                // does ship and the row reads a bare, useless UNKNOWN.
                // These hints are what turns it into an answer;
                // AcquisitionHintSeedVendorAgreementTests pins them
                // against that shipped offer. The badge is MERCHANT, not
                // VENDOR: a VENDOR badge is byte-identical to the
                // single-source VENDOR pill, which means a priced purchase.
                Assert.Equal("MERCHANT", hints[106712].Badge);
                Assert.Contains("Castaway Agnes", hints[106712].Hint);
                Assert.Equal("MERCHANT", hints[105804].Badge);
                Assert.Contains("Canach", hints[105804].Hint);
                Assert.Equal("ACHIEVEMENT", hints[106986].Badge);
                Assert.Contains("Radiance of the Sun God", hints[106986].Hint);
            }
        }

        // FindRepoFile comes from Helpers/RepoFileLocator.cs.
    }
}
