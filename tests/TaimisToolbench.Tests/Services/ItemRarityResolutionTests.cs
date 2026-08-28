using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The one rarity-resolution policy both the Snapshot tab and the
    /// Crafting Ranker read. The drawing is Blish-bound and untestable here;
    /// the decision of WHICH rarity a row renders at is not, and that is the
    /// half that was wrong.
    /// </summary>
    public class ItemRarityResolutionTests
    {
        [Theory]
        [InlineData("Junk")]
        [InlineData("Basic")]
        [InlineData("Fine")]
        [InlineData("Masterwork")]
        [InlineData("Rare")]
        [InlineData("Exotic")]
        [InlineData("Ascended")]
        [InlineData("Legendary")]
        public void Normalize_EveryApiRarity_RoundTripsExactly(string rarity)
        {
            Assert.Equal(rarity, ItemRarityResolution.Normalize(rarity));
        }

        [Theory]
        [InlineData("exotic", "Exotic")]
        [InlineData("ASCENDED", "Ascended")]
        [InlineData("  Fine  ", "Fine")]
        public void Normalize_IsCaseAndWhitespaceInsensitive(string raw, string expected)
        {
            Assert.Equal(expected, ItemRarityResolution.Normalize(raw));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        // Gw2Sharp's own name for a rarity string it did not recognise.
        // It must resolve to unknown, not to a colour.
        [InlineData("Unknown")]
        [InlineData("Mythic")]
        public void Normalize_AbsentOrUnrecognised_IsNull(string raw)
        {
            Assert.Null(ItemRarityResolution.Normalize(raw));
        }

        [Fact]
        public void Resolve_PrefersTheCapturedValue()
        {
            // The captured value came from the same /v2/items response as
            // the name beside it; the session cache is only a fallback.
            Assert.Equal("Ascended", ItemRarityResolution.Resolve("Ascended", "Basic"));
        }

        [Fact]
        public void Resolve_FallsBackToTheSessionCache_WhenNothingWasCaptured()
        {
            // A snapshot.json written before captures carried rarity: the
            // rows are still coloured for whatever the session happens to
            // know, exactly as they were before this field existed.
            Assert.Equal("Rare", ItemRarityResolution.Resolve("", "Rare"));
            Assert.Equal("Rare", ItemRarityResolution.Resolve(null, "Rare"));
        }

        [Fact]
        public void Resolve_NeitherSourceKnows_IsNull()
        {
            // Null is the neutral frame and the neutral name - never a
            // guessed rarity.
            Assert.Null(ItemRarityResolution.Resolve("", null));
        }

        [Fact]
        public void Resolve_UnrecognisedCapturedValue_StillFallsBack()
        {
            // A garbage value in an old file must not shadow a good one.
            Assert.Equal("Exotic", ItemRarityResolution.Resolve("Mythic", "Exotic"));
        }
    }
}
