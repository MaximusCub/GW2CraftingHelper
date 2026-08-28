using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The ONE shape every item-hover surface shows (tree row, Used
    /// Materials, Shopping List, Snapshot results). The rules that matter
    /// are what happens at the seams: the name must never appear twice, a
    /// row with no stats must still show what it always showed, and a coin
    /// amount in a surface's own extra lines must stay a coin span.
    /// </summary>
    public class ItemRowTooltipComposerTests
    {
        private static ItemStatBlock Block()
        {
            return new ItemStatBlock
            {
                ItemId = 19700,
                Name = "Mithril Ore",
                Rarity = "Basic",
                ItemType = "CraftingMaterial",
                VendorValue = 7,
            };
        }

        [Fact]
        public void StatBlockOpensTheTooltipAndSuppressesTheDuplicateNameLine()
        {
            var lines = ItemRowTooltipComposer.BuildRowContent(
                Block(), "Mithril Ore", nameTruncated: true, extraLines: null).ToPlainLines();

            Assert.Equal("Mithril Ore", lines[0]);
            Assert.Equal(1, lines.Count(l => l == "Mithril Ore"));
        }

        [Fact]
        public void WithNoStatBlockAnEllipsizedRowStillGetsItsFullName()
        {
            var lines = ItemRowTooltipComposer.BuildRowContent(
                (ItemStatBlock)null, "A Very Long Item Name", nameTruncated: true, extraLines: null)
                .ToPlainLines();

            Assert.Equal(new[] { "A Very Long Item Name" }, lines);
        }

        [Fact]
        public void AnUntruncatedRowWithNothingToAddHasNoTooltipAtAll()
        {
            Assert.True(ItemRowTooltipComposer.BuildRowContent(
                (ItemStatBlock)null, "Short", nameTruncated: false, extraLines: null).IsEmpty);
        }

        [Fact]
        public void ExtraLinesFollowTheStatBlockAfterExactlyOneBlank()
        {
            var lines = ItemRowTooltipComposer.BuildRowContent(
                Block(), "Mithril Ore", false, new[] { "Right-click: Open wiki page" }).ToPlainLines();

            Assert.Equal("Right-click: Open wiki page", lines[lines.Count - 1]);
            Assert.Equal("", lines[lines.Count - 2]);
            Assert.NotEqual("", lines[lines.Count - 3]);
        }

        [Fact]
        public void ExtraLinesAloneNeverOpenOnABlankRow()
        {
            var lines = ItemRowTooltipComposer.BuildRowContent(
                (ItemStatBlock)null, "Short", false, new[] { "Right-click: Open wiki page" }).ToPlainLines();

            Assert.Equal(new[] { "Right-click: Open wiki page" }, lines);
        }

        [Fact]
        public void ACoinAmountInTheExtraContentStaysACoinSpan()
        {
            // The tree's "Unit price:" line: a string tooltip could only
            // spell it out, which is the whole reason the rich path exists.
            var extra = TooltipContent.FromLines(new[]
            {
                TooltipContent.Line(
                    TooltipSpan.FromText("Unit price: "),
                    TooltipSpan.FromCoin(1234, "12s 34c")),
            });

            var content = ItemRowTooltipComposer.BuildRowContent(
                ItemStatTooltipComposer.BuildContent(Block()), "Mithril Ore", false, extra);

            var coins = content.Lines.SelectMany(l => l.Spans).Where(s => s.IsCoin).ToArray();
            Assert.Equal(2, coins.Length);
            Assert.Contains(coins, c => c.CoinCopper == 1234);
            Assert.Contains(coins, c => c.CoinCopper == 7);
        }
    }
}
