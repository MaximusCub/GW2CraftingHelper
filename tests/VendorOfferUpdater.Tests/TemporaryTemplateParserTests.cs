using VendorOfferUpdater;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// Festival-vendor auto-tagging follow-up (2026-08-16): exercises
    /// TemporaryTemplateParser against REAL wikitext captured live from
    /// the GW2 Wiki (api.guildwars2.com mirror,
    /// api.php?action=parse&amp;prop=wikitext) - not hand-invented
    /// fixtures - for each of the six known festival vendor NPC pages,
    /// plus the "event=" parameter variant and the "known-but-unmapped
    /// event value" case, both also confirmed live on real vendor NPC
    /// pages. See Gw2Constants.FestivalKeysByWikiDisplayName's own doc
    /// comment for the full citation of each fetch.
    /// </summary>
    public class TemporaryTemplateParserTests
    {
        [Fact]
        public void CandyCornVendorWeekly_SeasonalHalloween_Extracted()
        {
            // Captured verbatim (2026-08-16) from
            // "Candy Corn Vendor (Weekly)".
            const string wikitext =
                "{{Temporary|release=Shadow of the Mad King 2019|seasonal=Halloween}}\n" +
                "{{NPC infobox\n" +
                "| name = Candy Corn Vendor\n" +
                "| icon = Candy Corn (overhead icon).png\n" +
                "| random = yes\n" +
                "| location = Fort Marriner; Grand Piazza; Inner Harbor; Trader's Forum\n" +
                "| race = Halloween creature\n" +
                "| organization = Lunatic Court\n" +
                "| service = Festival Merchant\n" +
                "| coordinates = [48963,30802]\n" +
                "}}\n" +
                "[[Candy Corn Vendor (Weekly)|Candy Corn Vendor]] is a [[Festival Merchant]]...";

            Assert.Equal("Halloween", TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void DragonBashMerchantWeekly_SeasonalDragonBash_Extracted()
        {
            // Captured verbatim (2026-08-16) from
            // "Dragon Bash Merchant (Weekly)".
            const string wikitext = "{{Temporary|release=Dragon Bash 2019|seasonal=Dragon Bash}}\n" +
                "{{NPC infobox\n| name = Dragon Bash Merchant\n}}";

            Assert.Equal("Dragon Bash", TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void WintersdayTraderWeekly_SeasonalWintersday_Extracted()
        {
            // Captured verbatim (2026-08-16) from
            // "Wintersday Trader (Weekly)".
            const string wikitext =
                "{{Temporary|release=A Very Merry Wintersday 2019|seasonal=Wintersday}}\n" +
                "{{NPC infobox\n| name = Wintersday Trader\n}}";

            Assert.Equal("Wintersday", TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void FestivalRewardsVendorWeekly_SeasonalFestivalOfTheFourWinds_Extracted()
        {
            // Captured verbatim (2026-08-16) from
            // "Festival Rewards Vendor (Weekly)".
            const string wikitext =
                "{{Temporary|release=Festival of the Four Winds 2019|seasonal=Festival of the Four Winds}}\n" +
                "{{NPC infobox\n| name = Festival Rewards Vendor\n}}";

            Assert.Equal(
                "Festival of the Four Winds",
                TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void NewYearVendor_SeasonalLunarNewYear_Extracted()
        {
            // Captured verbatim (2026-08-16) from "New Year Vendor".
            const string wikitext = "{{Temporary|release=Lunar New Year 2020|seasonal=Lunar New Year}}\n" +
                "{{NPC infobox\n| name = New Year Vendor\n}}";

            Assert.Equal("Lunar New Year", TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void SuperAdventureBoxWeeklyTrader_SeasonalSuperAdventureFestival_Extracted()
        {
            // Captured verbatim (2026-08-16) from
            // "Super Adventure Box Weekly Trader".
            const string wikitext =
                "{{Temporary|release=Super Adventure Festival 2019|seasonal=Super Adventure Festival}}\n" +
                "{{NPC infobox\n| name = Super Adventure Box Trader\n}}";

            Assert.Equal(
                "Super Adventure Festival",
                TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void TraderVendorPage_EventParameterVariant_Extracted()
        {
            // Captured verbatim (2026-08-16) from "Trader" (the Bazaar of
            // the Four Winds karma merchant) - a vendor NPC page using
            // "event=" instead of "seasonal=" for the identical purpose.
            const string wikitext =
                "{{Temporary|release=Bazaar of the Four Winds|event=Festival of the Four Winds}}\n" +
                "{{NPC infobox\n| name = Trader\n}}";

            Assert.Equal(
                "Festival of the Four Winds",
                TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void LowercaseTemplateName_StillMatches()
        {
            // Captured verbatim (2026-08-16) from "Mad King's Realm" -
            // confirms the lowercase "{{temporary|...}}" spelling used in
            // the wild resolves identically to "{{Temporary|...}}".
            const string wikitext = "{{temporary|release=Shadow of the Mad King 2012|seasonal=Halloween}}";

            Assert.Equal("Halloween", TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void KnownButUnmappedEventValue_StillExtracted_ResolverLeavesUntagged()
        {
            // Captured verbatim (2026-08-16) from
            // "Consortium Trader (Fractal Rush)" - a real vendor NPC page
            // whose {{temporary|event=...}} value is a one-off in-game
            // event, not one of the six recognized festivals. The parser
            // still extracts it (parsing is not where the festival
            // allow-list is enforced) - Gw2Constants.ResolveSeasonalFestivalKey
            // is what leaves this untagged, see Gw2ConstantsTests.
            const string wikitext = "{{temporary|event=Fractal Rush}}\n{{NPC infobox\n| name = Consortium Trader\n}}";

            Assert.Equal("Fractal Rush", TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void NoTemporaryTemplate_ReturnsNull()
        {
            const string wikitext = "{{NPC infobox\n| name = Miyani\n}}\n[[Miyani]] is a merchant.";

            Assert.Null(TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void TemporaryTemplate_NeitherSeasonalNorEventParameter_ReturnsNull()
        {
            // Synthesized minimal case (not a live capture): every real
            // {{Temporary}} usage this pass found on the wiki carried
            // either "seasonal=" or "event=", but the parser must still
            // degrade cleanly for a bare release-only usage rather than
            // throwing or matching garbage.
            const string wikitext = "{{Temporary|release=Some One-Off Release}}";

            Assert.Null(TemporaryTemplateParser.ExtractSeasonalOrEventParameter(wikitext));
        }

        [Fact]
        public void NullWikitext_ReturnsNull()
        {
            Assert.Null(TemporaryTemplateParser.ExtractSeasonalOrEventParameter(null));
        }

        [Fact]
        public void EmptyWikitext_ReturnsNull()
        {
            Assert.Null(TemporaryTemplateParser.ExtractSeasonalOrEventParameter(""));
        }
    }
}
