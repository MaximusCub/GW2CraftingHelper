using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Every plain coin string the module can build, pinned across the same
    /// spread of amounts, through the real composer that builds it.
    ///
    /// <para>
    /// The spread is chosen to separate the formats: 1005 copper is 0 gold,
    /// 10 silver, 5 copper, which is where an always-three-units formatter
    /// ("0g 10s 5c") and the omit-leading-units bare-digit one the game
    /// itself renders ("10s 5c") disagree. 100 and 10000 pin the BARE
    /// trailing zeros - the game shows "2g 0s 0c", never "2g 00s 00c"
    /// (live3 counterfeit-ticket / relic-livingcity, 2026-08-26, which
    /// retired the old zero-padded icon spelling) - and -1 pins the clamp.
    /// </para>
    ///
    /// <para>
    /// All four composers now answer CoinSegmentMath.GameStyleText, so all
    /// four columns below are the icon spelling. The expectations that
    /// changed when they were collapsed are marked at their case.
    /// </para>
    /// </summary>
    public class CoinFormattingTests
    {
        /// <summary>The one plain coin format, and the strings the coin
        /// ICONS are drawn beside - the format a user actually reads.
        /// Every composer below must agree with this table.</summary>
        [Theory]
        [InlineData(0, "0c")]
        [InlineData(1, "1c")]
        [InlineData(99, "99c")]
        [InlineData(100, "1s 0c")]
        [InlineData(1005, "10s 5c")]
        [InlineData(10000, "1g 0s 0c")]
        [InlineData(1234567, "123g 45s 67c")]
        [InlineData(9999, "99s 99c")]
        [InlineData(10101, "1g 1s 1c")]
        [InlineData(-1, "0c")]
        [InlineData(-99999, "0c")]
        public void GameStyleText(long copper, string expected)
        {
            Assert.Equal(expected, CoinSegmentMath.GameStyleText(copper));
        }

        /// <summary>The per-denomination strings GameStyleText and the icon
        /// path are both built from.</summary>
        [Theory]
        [InlineData(0, null, null, "0")]
        [InlineData(1, null, null, "1")]
        [InlineData(99, null, null, "99")]
        [InlineData(100, null, "1", "0")]
        [InlineData(1005, null, "10", "5")]
        [InlineData(10000, "1", "0", "0")]
        [InlineData(1234567, "123", "45", "67")]
        [InlineData(-1, null, null, "0")]
        public void IconSegmentTexts(long copper, string gold, string silver, string cop)
        {
            Assert.Equal((gold, silver, cop), CoinSegmentMath.FormatSegmentTexts(copper));
        }

        // Changed with the consolidation: this composer used to spell
        // every amount with all three units ("0g 10s 5c"). It now answers
        // the shared spelling. The string is never drawn
        // (ACoinSpansTextChangesNoGeometry below), so the change is to
        // what the composer MEANS, not to a pixel.
        [Theory]
        [InlineData(0, "0c")]
        [InlineData(1, "1c")]
        [InlineData(99, "99c")]
        [InlineData(100, "1s 0c")]
        [InlineData(1005, "10s 5c")]
        [InlineData(10000, "1g 0s 0c")]
        [InlineData(1234567, "123g 45s 67c")]
        [InlineData(-1, "0c")]
        public void TreeRowUnitPrice(long copper, string expected)
        {
            var node = new CraftingTreeNode
            {
                ItemId = 1,
                NodeId = 1,
                Decision = CraftingDecision.BuyFromTp,
                Quantity = 2,
                UnitCost = copper,
            };

            var content = TreeRowTooltipComposer.BuildExtraTooltipContent(node, null, null);

            Assert.Contains("Unit price: " + expected, content.ToPlainText());
        }

        // Changed with the consolidation: this composer used to spell
        // every amount with all three units ("0g 10s 5c"). It now answers
        // the shared spelling. The string is never drawn
        // (ACoinSpansTextChangesNoGeometry below), so the change is to
        // what the composer MEANS, not to a pixel.
        [Theory]
        [InlineData(0, "0c")]
        [InlineData(1, "1c")]
        [InlineData(99, "99c")]
        [InlineData(100, "1s 0c")]
        [InlineData(1005, "10s 5c")]
        [InlineData(10000, "1g 0s 0c")]
        [InlineData(1234567, "123g 45s 67c")]
        [InlineData(-1, "0c")]
        public void ValueDetailCraftingGoldPrice(long copper, string expected)
        {
            var node = new CraftingTreeNode
            {
                ItemId = 1,
                NodeId = 1,
                Decision = CraftingDecision.Craft,
                SubtreeCost = copper,

                // Any positive divergence; the line under test is the
                // SubtreeCost one.
                DecisionValue = copper + 1,
            };

            Assert.True(ValueDetailTooltipBuilder.TryBuildContent(node, null, out var content));
            Assert.Contains("Crafting gold price: " + expected, content.ToPlainText());
        }

        // This composer always omitted leading all-zero units and never
        // padded a trailing one - the shape the live3 captures later
        // measured as the game's own.
        [Theory]
        [InlineData(0, "0c")]
        [InlineData(1, "1c")]
        [InlineData(99, "99c")]
        [InlineData(100, "1s 0c")]
        [InlineData(1005, "10s 5c")]
        [InlineData(10000, "1g 0s 0c")]
        [InlineData(1234567, "123g 45s 67c")]
        [InlineData(-1, "0c")]
        public void PillSubduingMargin(long copper, string expected)
        {
            var result = new PillSubduingResult(PillSubduingRule.Weighted, copper, null, hasNonCoinCost: false);

            var content = PillSubduingTooltipBuilder.BuildContent(result, null, null);

            Assert.Equal("More expensive (" + expected + " more)", content.ToPlainText());
        }

        // Unchanged by the consolidation: this was the only composer
        // already spelling coins the way the icons do, and its own comment
        // argued that every tooltip must. It is the format the other three
        // were collapsed onto.
        [Theory]
        [InlineData(0, "0c")]
        [InlineData(1, "1c")]
        [InlineData(99, "99c")]
        [InlineData(100, "1s 0c")]
        [InlineData(1005, "10s 5c")]
        [InlineData(10000, "1g 0s 0c")]
        [InlineData(1234567, "123g 45s 67c")]
        [InlineData(-1, "0c")]
        public void ItemStatVendorValue(long copper, string expected)
        {
            var stats = new ItemStatBlock { ItemId = 1, Name = "Test Item", VendorValue = copper };

            var content = ItemStatTooltipComposer.BuildContent(stats);

            Assert.Contains(expected, content.ToPlainText());
        }

        /// <summary>
        /// What a coin span's plain text costs on screen: nothing. The
        /// layout pass measures a coin span from its COPPER value
        /// (measureCoin) and never from its text, and the rich surface
        /// draws it as icons - so two spans carrying the same amount lay out
        /// identically however their text is spelled. This is the reason
        /// the four composers above could disagree for as long as they did,
        /// and the reason collapsing them onto one format moves no pixel.
        /// </summary>
        [Fact]
        public void ACoinSpansTextChangesNoGeometry()
        {
            System.Func<string, int> tenPxPerChar = s => (s?.Length ?? 0) * 10;
            System.Func<long, int> coinWidthFromCopper = c => (int)(c % 97) + 40;

            TooltipLayoutMath.Layout LayoutOf(string coinText)
            {
                var content = TooltipContent.FromLines(new[]
                {
                    TooltipContent.Line(
                        TooltipSpan.FromText("Unit price: "),
                        TooltipSpan.FromCoin(1005, coinText)),
                });

                return TooltipLayoutMath.LayoutContent(content, 400, 20, tenPxPerChar, coinWidthFromCopper);
            }

            var threeUnit = LayoutOf("0g 10s 5c");
            var gameStyle = LayoutOf("10s 5c");
            var absurd = LayoutOf("this is not a coin string at all, and it is very long indeed");

            Assert.Equal(threeUnit.Width, gameStyle.Width);
            Assert.Equal(threeUnit.Width, absurd.Width);
            Assert.Equal(threeUnit.Height, gameStyle.Height);
            Assert.Equal(threeUnit.Height, absurd.Height);
            Assert.Equal(threeUnit.Rows.Count, absurd.Rows.Count);
            Assert.Equal(threeUnit.Rows[0].Spans[1].X, absurd.Rows[0].Spans[1].X);
            Assert.Equal(threeUnit.Rows[0].Spans[1].Width, absurd.Rows[0].Spans[1].Width);
        }
    }
}
