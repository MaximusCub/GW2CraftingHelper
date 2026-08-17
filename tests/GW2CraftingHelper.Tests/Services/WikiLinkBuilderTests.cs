using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class WikiLinkBuilderTests
    {
        // --- BuildItemPageUrl ---

        [Fact]
        public void BuildItemPageUrl_SimpleSpaces_ReplacedWithUnderscores()
        {
            string url = WikiLinkBuilder.BuildItemPageUrl("Bolt of Damask");

            Assert.Equal("https://wiki.guildwars2.com/wiki/Bolt_of_Damask", url);
        }

        [Fact]
        public void BuildItemPageUrl_Apostrophe_PercentEncoded()
        {
            string url = WikiLinkBuilder.BuildItemPageUrl("Zojja's Claymore");

            Assert.Equal("https://wiki.guildwars2.com/wiki/Zojja%27s_Claymore", url);
        }

        [Fact]
        public void BuildItemPageUrl_NullOrEmpty_ReturnsNull()
        {
            Assert.Null(WikiLinkBuilder.BuildItemPageUrl(null));
            Assert.Null(WikiLinkBuilder.BuildItemPageUrl(""));
            Assert.Null(WikiLinkBuilder.BuildItemPageUrl("   "));
        }

        [Fact]
        public void BuildItemPageUrl_LeadingTrailingWhitespace_Trimmed()
        {
            string url = WikiLinkBuilder.BuildItemPageUrl("  Bolt of Damask  ");

            Assert.Equal("https://wiki.guildwars2.com/wiki/Bolt_of_Damask", url);
        }

        [Fact]
        public void BuildItemPageUrl_NonAsciiName_Utf8PercentEncoded()
        {
            // Repo rule: ASCII-only in source, so the non-ASCII character is
            // a \u escape rather than a literal. Exercises the axis
            // EncodeTitle's hand-rolled "Recipe:_" prefix special case
            // invites a future maintainer to break: Uri.EscapeDataString
            // percent-encodes multi-byte UTF-8 characters correctly today.
            string url = WikiLinkBuilder.BuildItemPageUrl("Caf\u00e9");

            Assert.Equal("https://wiki.guildwars2.com/wiki/Caf%C3%A9", url);
        }

        // --- BuildItemAcquisitionUrl ---

        [Fact]
        public void BuildItemAcquisitionUrl_AppendsAnchor()
        {
            string url = WikiLinkBuilder.BuildItemAcquisitionUrl("Bolt of Damask");

            Assert.Equal("https://wiki.guildwars2.com/wiki/Bolt_of_Damask#Acquisition", url);
        }

        [Fact]
        public void BuildItemAcquisitionUrl_ApostropheName_EncodedThenAnchored()
        {
            string url = WikiLinkBuilder.BuildItemAcquisitionUrl("Zojja's Claymore");

            Assert.Equal("https://wiki.guildwars2.com/wiki/Zojja%27s_Claymore#Acquisition", url);
        }

        [Fact]
        public void BuildItemAcquisitionUrl_NullName_ReturnsNull()
        {
            Assert.Null(WikiLinkBuilder.BuildItemAcquisitionUrl(null));
        }

        // --- BuildRecipeSheetUrl ---

        [Fact]
        public void BuildRecipeSheetUrl_UsesLiteralNamespaceColon()
        {
            string url = WikiLinkBuilder.BuildRecipeSheetUrl("Bolt of Damask");

            Assert.Equal("https://wiki.guildwars2.com/wiki/Recipe:_Bolt_of_Damask", url);
        }

        [Fact]
        public void BuildRecipeSheetUrl_ApostropheName_EncodedButColonLiteral()
        {
            string url = WikiLinkBuilder.BuildRecipeSheetUrl("Zojja's Claymore");

            Assert.Equal("https://wiki.guildwars2.com/wiki/Recipe:_Zojja%27s_Claymore", url);
        }

        [Fact]
        public void BuildRecipeSheetUrl_NullName_ReturnsNull()
        {
            Assert.Null(WikiLinkBuilder.BuildRecipeSheetUrl(null));
        }

        // --- BuildRequiredRecipeUrl (flag-based target) ---

        [Fact]
        public void BuildRequiredRecipeUrl_LearnedFromItem_LinksToRecipeSheet()
        {
            string url = WikiLinkBuilder.BuildRequiredRecipeUrl("Bolt of Damask", isLearnedFromItem: true);

            Assert.Equal("https://wiki.guildwars2.com/wiki/Recipe:_Bolt_of_Damask", url);
        }

        [Fact]
        public void BuildRequiredRecipeUrl_NotLearnedFromItem_LinksToItemAcquisitionAnchor()
        {
            string url = WikiLinkBuilder.BuildRequiredRecipeUrl("Bolt of Damask", isLearnedFromItem: false);

            Assert.Equal("https://wiki.guildwars2.com/wiki/Bolt_of_Damask#Acquisition", url);
        }

        [Fact]
        public void BuildRequiredRecipeUrl_LearnedFromItem_ApostropheName()
        {
            string url = WikiLinkBuilder.BuildRequiredRecipeUrl("Zojja's Claymore", isLearnedFromItem: true);

            Assert.Equal("https://wiki.guildwars2.com/wiki/Recipe:_Zojja%27s_Claymore", url);
        }

        // --- Sentinel/placeholder names (fix-pass: dead-link suppression) ---
        // Every one of these is a real, literal name-resolution fallback
        // used elsewhere in the module (see WikiLinkBuilder's SentinelNames
        // doc comment for each source) - none of them names a real wiki
        // page, so every BuildXxxUrl method must return null for all four
        // rather than construct a guaranteed-404 URL.

        [Theory]
        [InlineData("Unknown Item")]
        [InlineData("Guild upgrade (unresolved)")]
        [InlineData("Unrecognized ingredient type")]
        [InlineData("Currency")]
        public void BuildItemPageUrl_SentinelName_ReturnsNull(string sentinelName)
        {
            Assert.Null(WikiLinkBuilder.BuildItemPageUrl(sentinelName));
        }

        [Theory]
        [InlineData("Unknown Item")]
        [InlineData("Guild upgrade (unresolved)")]
        [InlineData("Unrecognized ingredient type")]
        [InlineData("Currency")]
        public void BuildItemAcquisitionUrl_SentinelName_ReturnsNull(string sentinelName)
        {
            Assert.Null(WikiLinkBuilder.BuildItemAcquisitionUrl(sentinelName));
        }

        [Theory]
        [InlineData("Unknown Item")]
        [InlineData("Guild upgrade (unresolved)")]
        [InlineData("Unrecognized ingredient type")]
        [InlineData("Currency")]
        public void BuildRecipeSheetUrl_SentinelName_ReturnsNull(string sentinelName)
        {
            Assert.Null(WikiLinkBuilder.BuildRecipeSheetUrl(sentinelName));
        }

        [Theory]
        [InlineData("Unknown Item")]
        [InlineData("Guild upgrade (unresolved)")]
        [InlineData("Unrecognized ingredient type")]
        [InlineData("Currency")]
        public void BuildRequiredRecipeUrl_SentinelName_ReturnsNull_RegardlessOfLearnedFlag(string sentinelName)
        {
            Assert.Null(WikiLinkBuilder.BuildRequiredRecipeUrl(sentinelName, isLearnedFromItem: true));
            Assert.Null(WikiLinkBuilder.BuildRequiredRecipeUrl(sentinelName, isLearnedFromItem: false));
        }

        [Fact]
        public void BuildItemPageUrl_SentinelNameWithSurroundingWhitespace_StillReturnsNull()
        {
            Assert.Null(WikiLinkBuilder.BuildItemPageUrl("  Unknown Item  "));
        }

        // --- HasWikiPage (cheap render-path pre-check) ---

        [Fact]
        public void HasWikiPage_RealName_ReturnsTrue()
        {
            Assert.True(WikiLinkBuilder.HasWikiPage("Bolt of Damask"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void HasWikiPage_NullOrBlank_ReturnsFalse(string itemName)
        {
            Assert.False(WikiLinkBuilder.HasWikiPage(itemName));
        }

        [Theory]
        [InlineData("Unknown Item")]
        [InlineData("Guild upgrade (unresolved)")]
        [InlineData("Unrecognized ingredient type")]
        [InlineData("Currency")]
        public void HasWikiPage_SentinelName_ReturnsFalse(string sentinelName)
        {
            Assert.False(WikiLinkBuilder.HasWikiPage(sentinelName));
        }

        [Theory]
        [InlineData("Bolt of Damask", true)]
        [InlineData("Unknown Item", false)]
        [InlineData("Currency", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void HasWikiPage_AgreesWithBuildItemPageUrl(string itemName, bool expected)
        {
            // The whole point of the pre-check is that callers can trust it
            // to predict BuildItemPageUrl's null-vs-non-null outcome
            // without paying for the real URL construction - pin both legs
            // to the same expected outcome across inputs from both sides of
            // the divide, so agreement-by-accident (false == false for
            // every input) cannot pass.
            Assert.Equal(expected, WikiLinkBuilder.BuildItemPageUrl(itemName) != null);
            Assert.Equal(expected, WikiLinkBuilder.HasWikiPage(itemName));
        }
    }
}
