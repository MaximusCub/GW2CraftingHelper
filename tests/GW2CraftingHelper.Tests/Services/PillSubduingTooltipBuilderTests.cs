using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// source-selection-simplification: pure text-building coverage. Names
    /// are resolved via the SAME resolvers the rest of the tree renderer
    /// uses - never a raw id (repo invariant: IDs are internal-only).
    /// </summary>
    public class PillSubduingTooltipBuilderTests
    {
        [Fact]
        public void NoneRule_ReturnsNull()
        {
            Assert.Null(PillSubduingTooltipBuilder.Build(PillSubduingResult.None, null, null));
            Assert.Null(PillSubduingTooltipBuilder.Build(null, null, null));
        }

        [Fact]
        public void Weighted_FormatsMarginAsCoin()
        {
            var result = new PillSubduingResult(PillSubduingRule.Weighted, 12345, null);

            string text = PillSubduingTooltipBuilder.Build(result, null, null);

            Assert.Contains("More expensive at your current currency values", text);
            Assert.Contains("1g 23s 45c", text);
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

            string text = PillSubduingTooltipBuilder.Build(result, itemMetadata, null);

            Assert.Equal("Always more expensive - same currencies, 10 more Glob of Ectoplasm", text);
            Assert.DoesNotContain("100", text);
        }

        [Fact]
        public void StrictDomination_ItemDelta_MissingMetadata_FallsBackToUnknownItem_NeverRawId()
        {
            var deltas = new List<PillCostDelta> { new PillCostDelta("Item", 100, 10) };
            var result = new PillSubduingResult(PillSubduingRule.StrictDomination, null, deltas);

            string text = PillSubduingTooltipBuilder.Build(result, null, null);

            Assert.Equal("Always more expensive - same currencies, 10 more Unknown Item", text);
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

            string text = PillSubduingTooltipBuilder.Build(result, null, currencyMetadata);

            Assert.Equal("Always more expensive - same currencies, 500 more Karma", text);
        }

        [Fact]
        public void StrictDomination_CoinDelta_FormatsAsCoin()
        {
            var deltas = new List<PillCostDelta> { new PillCostDelta("Coin", 0, 150) };
            var result = new PillSubduingResult(PillSubduingRule.StrictDomination, null, deltas);

            string text = PillSubduingTooltipBuilder.Build(result, null, null);

            Assert.Equal("Always more expensive - same currencies, 1s 50c more", text);
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

            string text = PillSubduingTooltipBuilder.Build(result, itemMetadata, null);

            Assert.Equal("Always more expensive - same currencies, 50c more, 3 more Ecto", text);
        }
    }
}
