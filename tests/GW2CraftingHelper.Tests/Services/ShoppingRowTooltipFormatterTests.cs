using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Pins the exact HAVE/NEED wording ShoppingListSectionRenderer's
    /// tooltip depends on (shoplist-have-format review finding #3 -
    /// previously nothing observed these strings, so a regression straight
    /// back to the banned "N owned, M needed" phrasing passed the full
    /// suite). Also covers review finding #2: the output must never claim
    /// "plan requires" - cc.Amount is this row's own total, not the whole
    /// plan's requirement for that currency id.
    /// </summary>
    public class ShoppingRowTooltipFormatterTests
    {
        [Fact]
        public void BuildCurrencyLines_NullList_ReturnsEmptyList()
        {
            var lines = ShoppingRowTooltipFormatter.BuildCurrencyLines(null);

            Assert.Empty(lines);
        }

        [Fact]
        public void BuildCurrencyLines_NoWalletData_LineSkipped()
        {
            var costs = new List<CurrencyAmountViewModel>
            {
                new CurrencyAmountViewModel { Amount = 100, Name = "Karma", OwnedQuantity = null }
            };

            var lines = ShoppingRowTooltipFormatter.BuildCurrencyLines(costs);

            Assert.Empty(lines);
        }

        [Fact]
        public void BuildCurrencyLines_ZeroAmount_LineSkipped()
        {
            var costs = new List<CurrencyAmountViewModel>
            {
                new CurrencyAmountViewModel { Amount = 0, Name = "Karma", OwnedQuantity = 0, RawOwnedQuantity = 0 }
            };

            var lines = ShoppingRowTooltipFormatter.BuildCurrencyLines(costs);

            Assert.Empty(lines);
        }

        [Fact]
        public void BuildCurrencyLines_Shortfall_RendersHaveNeedWithRowScopeMarker()
        {
            var costs = new List<CurrencyAmountViewModel>
            {
                new CurrencyAmountViewModel
                {
                    Amount = 500,
                    Name = "Karma",
                    OwnedQuantity = 200,
                    RawOwnedQuantity = 200
                }
            };

            var lines = ShoppingRowTooltipFormatter.BuildCurrencyLines(costs);

            Assert.Equal(new[] { "Karma: HAVE 200/500 THIS ROW, NEED 300" }, lines);
        }

        [Fact]
        public void BuildCurrencyLines_ExactlyCovered_RendersCoverageFractionWithRowScopeMarkerNoAside()
        {
            var costs = new List<CurrencyAmountViewModel>
            {
                new CurrencyAmountViewModel
                {
                    Amount = 500,
                    Name = "Spirit Shards",
                    OwnedQuantity = 500,
                    RawOwnedQuantity = 500
                }
            };

            var lines = ShoppingRowTooltipFormatter.BuildCurrencyLines(costs);

            Assert.Equal(new[] { "Spirit Shards: HAVE 500/500 THIS ROW" }, lines);
        }

        [Fact]
        public void BuildCurrencyLines_CoveredWithSurplus_AppendsWalletAside()
        {
            var costs = new List<CurrencyAmountViewModel>
            {
                new CurrencyAmountViewModel
                {
                    Amount = 500,
                    Name = "Spirit Shards",
                    OwnedQuantity = 500, // clamped by CurrencyDisplayResolver
                    RawOwnedQuantity = 999999
                }
            };

            var lines = ShoppingRowTooltipFormatter.BuildCurrencyLines(costs);

            Assert.Equal(new[] { "Spirit Shards: HAVE 500/500 THIS ROW (wallet 999999)" }, lines);
        }

        [Fact]
        public void BuildCurrencyLines_RawOwnedQuantityNull_FallsBackToOwnedQuantity_NoAside()
        {
            // Guards the ?? fallback for a hypothetical future caller that
            // constructs the view model directly with only OwnedQuantity
            // set (RawOwnedQuantity left null) - must never crash, and must
            // never fabricate a surplus that was not actually reported.
            var costs = new List<CurrencyAmountViewModel>
            {
                new CurrencyAmountViewModel
                {
                    Amount = 500,
                    Name = "Karma",
                    OwnedQuantity = 500,
                    RawOwnedQuantity = null
                }
            };

            var lines = ShoppingRowTooltipFormatter.BuildCurrencyLines(costs);

            Assert.Equal(new[] { "Karma: HAVE 500/500 THIS ROW" }, lines);
        }

        [Fact]
        public void BuildCurrencyLines_MultipleCurrencies_OneLinePerCurrencyInOrder()
        {
            var costs = new List<CurrencyAmountViewModel>
            {
                new CurrencyAmountViewModel { Amount = 500, Name = "Karma", OwnedQuantity = 200, RawOwnedQuantity = 200 },
                new CurrencyAmountViewModel { Amount = 100, Name = "Spirit Shards", OwnedQuantity = 100, RawOwnedQuantity = 250 },
                new CurrencyAmountViewModel { Amount = 50, Name = "Unresolved Currency", OwnedQuantity = null }
            };

            var lines = ShoppingRowTooltipFormatter.BuildCurrencyLines(costs);

            Assert.Equal(new[]
            {
                "Karma: HAVE 200/500 THIS ROW, NEED 300",
                "Spirit Shards: HAVE 100/100 THIS ROW (wallet 250)"
            }, lines);
        }
    }
}
