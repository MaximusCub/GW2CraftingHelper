using VendorOfferUpdater.Models;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// Festival-vendor auto-tagging follow-up (2026-08-16):
    /// Gw2Constants.ResolveSeasonalFestivalKey against the six MEASURED
    /// wiki-display-name -> internal-key mappings (see
    /// FestivalKeysByWikiDisplayName's own doc comment for how each side
    /// was measured), plus the "never guess" contract for anything not in
    /// that curated table.
    /// </summary>
    public class SeasonalFestivalMappingTests
    {
        [Theory]
        [InlineData("Halloween", "halloween")]
        [InlineData("Dragon Bash", "dragonbash")]
        [InlineData("Wintersday", "wintersday")]
        [InlineData("Festival of the Four Winds", "festivalofthefourwinds")]
        [InlineData("Lunar New Year", "lunarnewyear")]
        [InlineData("Super Adventure Festival", "superadventurefestival")]
        public void KnownWikiDisplayName_ResolvesToInternalKey(string wikiDisplayName, string expectedKey)
        {
            Assert.Equal(expectedKey, Gw2Constants.ResolveSeasonalFestivalKey(wikiDisplayName));
        }

        [Theory]
        [InlineData("Fractal Rush")] // real, live-confirmed one-off event value
        [InlineData("Fractal Incursion")] // real, live-confirmed one-off event value
        [InlineData("halloween")] // wrong case - exact match required, never fuzzy
        [InlineData("Hallowe'en")] // plausible near-miss - must not be guessed
        [InlineData("Some Unrelated Release")]
        public void UnrecognizedValue_ResolvesToNull_NeverGuessed(string wikiDisplayName)
        {
            Assert.Null(Gw2Constants.ResolveSeasonalFestivalKey(wikiDisplayName));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NullOrWhitespace_ResolvesToNull(string wikiDisplayName)
        {
            Assert.Null(Gw2Constants.ResolveSeasonalFestivalKey(wikiDisplayName));
        }

        [Fact]
        public void WikiDisplayName_WithSurroundingWhitespace_StillResolves()
        {
            // The parser trims parameter values already, but the resolver
            // is defensive on its own too.
            Assert.Equal("halloween", Gw2Constants.ResolveSeasonalFestivalKey("  Halloween  "));
        }

        [Fact]
        public void ExactlySixFestivalsAreKnown()
        {
            // Pins the curated table to exactly the six FestivalContext
            // keys named in the follow-up task - a seventh entry sneaking
            // in unreviewed would fail this and force a deliberate look.
            Assert.Equal(6, Gw2Constants.FestivalKeysByWikiDisplayName.Count);
        }
    }
}
