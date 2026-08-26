using System.Linq;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// A span's role is metadata the PLAIN path must be blind to: adding
    /// roles must not change one byte of what ToPlainText produces, since
    /// that string is what every BasicTooltipText caller and every existing
    /// composer test still reads.
    /// </summary>
    public class TooltipSpanRoleTests
    {
        [Fact]
        public void PlainSpansStillDefaultToTheDefaultRole()
        {
            Assert.Equal(TooltipSpanRole.Default, TooltipSpan.FromText("x").Role);
            Assert.Equal(TooltipSpanRole.Default, TooltipSpan.FromCoin(1234, "0g 12s 34c").Role);
            Assert.Null(TooltipSpan.FromText("x").RarityKey);
        }

        [Fact]
        public void RarityAndStyledSpansCarryTheirRoleAndKey()
        {
            var name = TooltipSpan.RarityText("Bolt", "Legendary");
            Assert.Equal(TooltipSpanRole.Rarity, name.Role);
            Assert.Equal("Legendary", name.RarityKey);

            var bonus = TooltipSpan.Styled("+25 Power", TooltipSpanRole.Bonus);
            Assert.Equal(TooltipSpanRole.Bonus, bonus.Role);
            Assert.Null(bonus.RarityKey);
        }

        [Fact]
        public void RolesDoNotLeakIntoThePlainTextRendering()
        {
            var content = new TooltipContentBuilder()
                .RarityText("Zojja's Warfists", "Ascended").EndLine()
                .Styled("+141 Power", TooltipSpanRole.Bonus).EndLine()
                .Text("Vendor value: ").Coin(240, "0g 2s 40c")
                .Build();

            Assert.Equal(
                "Zojja's Warfists\n+141 Power\nVendor value: 0g 2s 40c",
                content.ToPlainText());
        }

        [Fact]
        public void HardBreaksInsideStyledTextKeepTheRoleOnEveryResultingLine()
        {
            var content = new TooltipContentBuilder()
                .Styled("(1): +25 Power\n(2): +35 Ferocity", TooltipSpanRole.Bonus)
                .Build();

            Assert.Equal(2, content.Lines.Count);
            Assert.All(content.Lines,
                line => Assert.Equal(TooltipSpanRole.Bonus, line.Spans.Single().Role));
        }

        [Fact]
        public void WrappingASpanPreservesItsRoleOnEveryWrappedRow()
        {
            // One "character" is 10px wide, so a 25px row fits two.
            var content = TooltipContent.FromLines(new[]
            {
                TooltipContent.Line(TooltipSpan.RarityText("aa bb cc", "Exotic"))
            });

            var layout = TooltipLayoutMath.LayoutContent(
                content, maxWidth: 25, rowHeight: 10,
                measureText: s => s.Length * 10,
                measureCoin: c => 40);

            Assert.True(layout.Rows.Count > 1);
            foreach (var row in layout.Rows)
            {
                foreach (var placed in row.Spans)
                {
                    Assert.Equal(TooltipSpanRole.Rarity, placed.Span.Role);
                    Assert.Equal("Exotic", placed.Span.RarityKey);
                }
            }
        }

        [Fact]
        public void CoinSpansSurviveLayoutAsCoinSpans()
        {
            var content = TooltipContent.FromLines(new[]
            {
                TooltipContent.Line(
                    TooltipSpan.Styled("Cost: ", TooltipSpanRole.Muted),
                    TooltipSpan.FromCoin(9999, "0g 99s 99c"))
            });

            var layout = TooltipLayoutMath.LayoutContent(
                content, maxWidth: 500, rowHeight: 10,
                measureText: s => s.Length * 10,
                measureCoin: c => 40);

            var spans = layout.Rows.Single().Spans;
            Assert.Equal(TooltipSpanRole.Muted, spans[0].Span.Role);
            Assert.True(spans[spans.Count - 1].Span.IsCoin);
            Assert.Equal(9999, spans[spans.Count - 1].Span.CoinCopper);
        }
    }
}
