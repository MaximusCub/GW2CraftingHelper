using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The ONE shape every item-hover surface shows (tree row, Used
    /// Materials, Shopping List, Snapshot results, Plan History). The rules
    /// that matter are what happens at the seams: the tooltip always opens
    /// on the icon+name header the game's own does, the name never appears
    /// twice, and a coin amount in a surface's own extra lines stays a coin
    /// span.
    /// </summary>
    public class ItemRowTooltipComposerTests
    {
        private const string IconUrl = "https://render.guildwars2.com/file/AAA/1.png";

        private static ItemStatBlock Block()
        {
            return new ItemStatBlock
            {
                ItemId = 19700,
                Name = "Mithril Ore",
                Rarity = "Basic",
                IconUrl = IconUrl,
                ItemType = "CraftingMaterial",
                VendorValue = 7,
            };
        }

        private static ItemTooltipIdentity Identity(string name = "Mithril Ore")
        {
            return ItemTooltipIdentity.ForItem(name, IconUrl, "Basic");
        }

        [Fact]
        public void StatBlockOpensTheTooltipAndSuppressesTheDuplicateNameLine()
        {
            var lines = ItemRowTooltipComposer.BuildRowContent(
                Block(), Identity(), extraLines: null).ToPlainLines();

            Assert.Equal("Mithril Ore", lines[0]);
            Assert.Equal(1, lines.Count(l => l == "Mithril Ore"));
        }

        /// <summary>
        /// The reported defect: an item nothing had looked up this session
        /// composed to a bare text line, so two adjacent rows of the same
        /// rarity and type showed one tooltip with an icon and one without.
        /// The header now comes from what the ROW knows, which is the same
        /// thing it drew its own icon from.
        /// </summary>
        [Fact]
        public void WithNoStatBlockTheHeaderStillCarriesTheRowsOwnIcon()
        {
            var content = ItemRowTooltipComposer.BuildRowContent(
                (ItemStatBlock)null, Identity("A Very Long Item Name"), extraLines: null);

            Assert.Equal(TooltipLineKind.Header, content.Lines[0].Kind);
            Assert.Equal(IconUrl, content.Lines[0].IconUrl);
            Assert.Equal(new[] { "A Very Long Item Name" }, content.ToPlainLines());
        }

        [Fact]
        public void WithAStatBlockTheHeaderCarriesTheStatBlocksIcon()
        {
            var content = ItemRowTooltipComposer.BuildRowContent(
                Block(), Identity(), extraLines: null);

            Assert.Equal(TooltipLineKind.Header, content.Lines[0].Kind);
            Assert.Equal(IconUrl, content.Lines[0].IconUrl);
        }

        [Fact]
        public void AnIdentityWithNoNameHeadsNothing()
        {
            Assert.True(ItemRowTooltipComposer.BuildRowContent(
                (ItemStatBlock)null, ItemTooltipIdentity.Unnamed(), extraLines: null).IsEmpty);
        }

        [Fact]
        public void ExtraLinesFollowTheStatBlockAfterExactlyOneBlank()
        {
            var lines = ItemRowTooltipComposer.BuildRowContent(
                Block(), Identity(), new[] { "Right-click: Open wiki page" }).ToPlainLines();

            Assert.Equal("Right-click: Open wiki page", lines[lines.Count - 1]);
            Assert.Equal("", lines[lines.Count - 2]);
            Assert.NotEqual("", lines[lines.Count - 3]);
        }

        [Fact]
        public void ExtraLinesAloneNeverOpenOnABlankRow()
        {
            var lines = ItemRowTooltipComposer.BuildRowContent(
                (ItemStatBlock)null, ItemTooltipIdentity.Unnamed(),
                new[] { "Right-click: Open wiki page" }).ToPlainLines();

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
                ItemStatTooltipComposer.BuildContent(Block()), Identity(), extra);

            var coins = content.Lines.SelectMany(l => l.Spans).Where(s => s.IsCoin).ToArray();
            Assert.Equal(2, coins.Length);
            Assert.Contains(coins, c => c.CoinCopper == 1234);
            Assert.Contains(coins, c => c.CoinCopper == 7);
        }
    }
}
