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
    }
}
