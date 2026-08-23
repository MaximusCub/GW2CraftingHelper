using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Every input string here is a verbatim /v2/items "description",
    /// captured live - the markup vocabulary is the API's, not an invented
    /// sample.
    /// </summary>
    public class ItemDescriptionSanitizerTests
    {
        [Fact]
        public void FlavorSpan_IsUnwrappedNotDeleted()
        {
            // Zojja's Warfists (48074)
            Assert.Equal(
                "Crafted in the style of the renowned asuran genius, Zojja.",
                ItemDescriptionSanitizer.Sanitize(
                    "<c=@flavor>Crafted in the style of the renowned asuran genius, Zojja.</c>"));
        }

        [Fact]
        public void AbilityTypeSpanAndBreakTag_BecomeOneLineBreak()
        {
            // Superior Rune of the Scholar (24836)
            Assert.Equal(
                "Element: Brilliance\nDouble-click to apply to a piece of armor.",
                ItemDescriptionSanitizer.Sanitize(
                    "<c=@abilitytype>Element: </c>Brilliance<br>Double-click to apply to a piece of armor."));
        }

        [Fact]
        public void RealNewlinesAndNonAsciiBullets_SurviveUnchanged()
        {
            // Sunrise (30703) - the bullets are U+2022 in the API's own bytes.
            string sanitized = ItemDescriptionSanitizer.Sanitize(
                "<c=@flavor>This weapon is used to craft the legendary greatsword Eternity " +
                "by combining it in the Mystic Forge with:\n\u2022 Twilight\n\u2022 5 Piles of " +
                "Crystalline Dust\n\u2022 10 Philosopher's Stones</c>");

            Assert.StartsWith("This weapon is used to craft", sanitized);
            Assert.Contains("\n\u2022 Twilight\n", sanitized);
            Assert.EndsWith("10 Philosopher's Stones", sanitized);
            Assert.DoesNotContain("<", sanitized);
        }

        [Fact]
        public void PlainDescription_IsReturnedUntouched()
        {
            // Mithril Ore (19700)
            Assert.Equal("Refine into Ingots.", ItemDescriptionSanitizer.Sanitize("Refine into Ingots."));
        }

        [Fact]
        public void NullOrEmpty_YieldsEmptyRatherThanNull()
        {
            Assert.Equal("", ItemDescriptionSanitizer.Sanitize(null));
            Assert.Equal("", ItemDescriptionSanitizer.Sanitize(""));
        }

        [Fact]
        public void CarriageReturnsCollapseToASingleBreak()
        {
            Assert.Equal("a\nb\nc", ItemDescriptionSanitizer.Sanitize("a\r\nb\rc"));
        }

        [Fact]
        public void SelfClosingBreakVariantsAreAllRecognised()
        {
            Assert.Equal("a\nb\nc", ItemDescriptionSanitizer.Sanitize("a<br>b<br />c"));
        }

        [Fact]
        public void UnknownMarkupIsPreservedRatherThanSilentlyDeleted()
        {
            Assert.Equal("<b>bold</b>", ItemDescriptionSanitizer.Sanitize("<b>bold</b>"));
            Assert.Equal("5 < 6 and 7 > 6", ItemDescriptionSanitizer.Sanitize("5 < 6 and 7 > 6"));
        }
    }
}
