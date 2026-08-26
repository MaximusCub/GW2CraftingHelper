using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Pure text-building coverage. Names
    /// are resolved via the SAME resolvers the rest of the tree renderer
    /// uses - never a raw id (repo invariant: IDs are internal-only).
    /// </summary>
    public class PillSubduingTooltipBuilderTests
    {
        [Fact]
        public void NoneRule_ReturnsNull()
        {
            Assert.Null(PillSubduingTooltipBuilder.BuildContent(PillSubduingResult.None, null, null));
            Assert.Null(PillSubduingTooltipBuilder.BuildContent(null, null, null));
        }

        [Fact]
        public void Weighted_WithNonCoinCost_MentionsCurrencyValues()
        {
            var result = new PillSubduingResult(PillSubduingRule.Weighted, 12345, null, hasNonCoinCost: true);

            var content = PillSubduingTooltipBuilder.BuildContent(result, null, null);

            Assert.Contains("More expensive at your current currency values", content.ToPlainText());
            Assert.Contains("1g 23s 45c", content.ToPlainText());
            // The margin is a coin span, not prose - the surface draws icons.
            Assert.Equal(new long[] { 12345 }, content.CoinValues());
        }

        [Fact]
        public void Weighted_PureCoinDifference_NoCurrencyMentioned()
        {
            // TP selected at 500c, CRAFT
            // losing with DecisionValue 800c and no Currency ingredient
            // anywhere - StrictDomination cannot fire (craft's RawCoin is
            // LOWER than TP's, so coinDelta is negative), so Weighted
            // fires on a pure-gold difference. The wording must not claim
            // "your current currency values" when no currency valuation
            // was ever involved.
            var result = new PillSubduingResult(PillSubduingRule.Weighted, 30000, null, hasNonCoinCost: false);

            var content = PillSubduingTooltipBuilder.BuildContent(result, null, null);

            // Coin spelling changed with the CoinSegmentMath.GameStyleText
            // consolidation: every composer now spells a coin amount the
            // way the icons beside it do (leading all-zero units omitted,
            // trailing units zero-padded).
            Assert.Equal("More expensive (3g 00s 00c more)", content.ToPlainText());
            Assert.DoesNotContain("currency values", content.ToPlainText());
        }

        [Fact]
        public void StrictDomination_ItemDelta_ResolvesNameFromItemMetadata()
        {
            var itemMetadata = new Dictionary<int, ItemMetadata>
            {
                { 100, new ItemMetadata { ItemId = 100, Name = "Glob of Ectoplasm" } }
            };
            var deltas = new List<PillCostDelta> { new PillCostDelta("Item", 100, 10) };
            var result = new PillSubduingResult(PillSubduingRule.StrictDomination, null, deltas);

            var content = PillSubduingTooltipBuilder.BuildContent(result, itemMetadata, null);

            Assert.Equal("Always more expensive - needs everything the selected option needs, plus 10 more Glob of Ectoplasm", content.ToPlainText());
            Assert.DoesNotContain("100", content.ToPlainText());
        }

        [Fact]
        public void StrictDomination_ItemDelta_MissingMetadata_FallsBackToUnknownItem_NeverRawId()
        {
            var deltas = new List<PillCostDelta> { new PillCostDelta("Item", 100, 10) };
            var result = new PillSubduingResult(PillSubduingRule.StrictDomination, null, deltas);

            var content = PillSubduingTooltipBuilder.BuildContent(result, null, null);

            Assert.Equal("Always more expensive - needs everything the selected option needs, plus 10 more Unknown Item", content.ToPlainText());
        }

        [Fact]
        public void StrictDomination_CurrencyDelta_ResolvesNameFromCurrencyMetadata()
        {
            var currencyMetadata = new Dictionary<int, CurrencyMetadata>
            {
                { 23, new CurrencyMetadata { CurrencyId = 23, Name = "Karma" } }
            };
            var deltas = new List<PillCostDelta> { new PillCostDelta("Currency", 23, 500) };
            var result = new PillSubduingResult(PillSubduingRule.StrictDomination, null, deltas);

            var content = PillSubduingTooltipBuilder.BuildContent(result, null, currencyMetadata);

            Assert.Equal("Always more expensive - needs everything the selected option needs, plus 500 more Karma", content.ToPlainText());
        }

        [Fact]
        public void StrictDomination_CoinDelta_FormatsAsCoin()
        {
            var deltas = new List<PillCostDelta> { new PillCostDelta("Coin", 0, 150) };
            var result = new PillSubduingResult(PillSubduingRule.StrictDomination, null, deltas);

            var content = PillSubduingTooltipBuilder.BuildContent(result, null, null);

            Assert.Equal("Always more expensive - needs everything the selected option needs, plus 1s 50c more", content.ToPlainText());
        }

        [Fact]
        public void StrictDomination_MultipleDeltas_JoinedWithComma()
        {
            var itemMetadata = new Dictionary<int, ItemMetadata>
            {
                { 100, new ItemMetadata { ItemId = 100, Name = "Ecto" } }
            };
            var deltas = new List<PillCostDelta>
            {
                new PillCostDelta("Coin", 0, 50),
                new PillCostDelta("Item", 100, 3)
            };
            var result = new PillSubduingResult(PillSubduingRule.StrictDomination, null, deltas);

            var content = PillSubduingTooltipBuilder.BuildContent(result, itemMetadata, null);

            Assert.Equal("Always more expensive - needs everything the selected option needs, plus 50c more, 3 more Ecto", content.ToPlainText());
        }
    }
}
