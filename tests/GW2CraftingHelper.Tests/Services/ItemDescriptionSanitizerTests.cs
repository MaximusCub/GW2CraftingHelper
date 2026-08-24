using System.Linq;
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

        // --- Role-carrying spans (gap G7) ---

        [Fact]
        public void PlainAndFlavorRunsInOneDescriptionKeepSeparateRoles()
        {
            // The xyaren capture's shape: an unmarked sentence the game
            // renders WHITE, then a quoted flavour run it renders teal.
            var spans = ItemDescriptionSanitizer.SanitizeToSpans(
                "A gift bag!<c=@flavor>\"Care is taken with every toy.\"</c>");

            Assert.Equal(2, spans.Count);
            Assert.Equal("A gift bag!", spans[0].Text);
            Assert.Equal(TooltipSpanRole.Default, spans[0].Role);
            Assert.Equal("\"Care is taken with every toy.\"", spans[1].Text);
            Assert.Equal(TooltipSpanRole.Flavor, spans[1].Role);
        }

        [Fact]
        public void AbilityTypeLeadInKeepsItsOwnRoleAndTheRestStaysDefault()
        {
            var spans = ItemDescriptionSanitizer.SanitizeToSpans(
                "<c=@abilitytype>Element: </c>Brilliance<br>Double-click to apply to a piece of armor.");

            Assert.Equal(TooltipSpanRole.AbilityType, spans[0].Role);
            Assert.Equal("Element: ", spans[0].Text);
            Assert.All(spans.Skip(1), s => Assert.Equal(TooltipSpanRole.Default, s.Role));
        }

        [Fact]
        public void WarningAndReminderMapToTheirOwnRoles()
        {
            Assert.Equal(
                TooltipSpanRole.Warning,
                ItemDescriptionSanitizer.SanitizeToSpans("<c=@warning>Do not eat.</c>").Single().Role);

            // Its own role, not Muted: reminder is gw2efficiency's #afafaf
            // (175) and the annotation grey is the measured #939496 (150).
            Assert.Equal(
                TooltipSpanRole.Reminder,
                ItemDescriptionSanitizer.SanitizeToSpans("<c=@reminder>(Rounded down.)</c>").Single().Role);
        }

        [Fact]
        public void AnUnknownColourNameKeepsItsTextAtTheDefaultRole()
        {
            var span = ItemDescriptionSanitizer.SanitizeToSpans("<c=@nosuchcolour>text</c>").Single();

            Assert.Equal("text", span.Text);
            Assert.Equal(TooltipSpanRole.Default, span.Role);
        }

        [Fact]
        public void ANestedRunRestoresTheOuterRoleWhenItCloses()
        {
            var spans = ItemDescriptionSanitizer.SanitizeToSpans(
                "<c=@flavor>outer <c=@warning>inner</c> outer again</c>");

            Assert.Equal(
                new[] { TooltipSpanRole.Flavor, TooltipSpanRole.Warning, TooltipSpanRole.Flavor },
                spans.Select(s => s.Role).ToArray());
        }

        [Fact]
        public void SanitizeStaysExactlyTheConcatenationOfTheSpans()
        {
            const string description =
                "<c=@abilitytype>Element: </c>Brilliance<br><c=@flavor>  Quoted.  </c>";

            Assert.Equal(
                ItemDescriptionSanitizer.Sanitize(description),
                string.Concat(ItemDescriptionSanitizer.SanitizeToSpans(description).Select(s => s.Text)));
        }
    }
}
