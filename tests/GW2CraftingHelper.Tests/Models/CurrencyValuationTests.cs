using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;
using Xunit;

namespace GW2CraftingHelper.Tests.Models
{
    public class CurrencyValuationTests
    {
        [Fact]
        public void Constructor_ZeroCopperPerUnit_ThrowsArgumentOutOfRangeException()
        {
            var entries = new Dictionary<int, long> { { 2, 0 } };

            Assert.Throws<ArgumentOutOfRangeException>(() => new CurrencyValuation(entries));
        }

        [Fact]
        public void Constructor_NegativeCopperPerUnit_ThrowsArgumentOutOfRangeException()
        {
            var entries = new Dictionary<int, long> { { 2, -5 } };

            Assert.Throws<ArgumentOutOfRangeException>(() => new CurrencyValuation(entries));
        }

        [Fact]
        public void Constructor_KeyedOnCoinCurrencyId_ThrowsArgumentException()
        {
            var entries = new Dictionary<int, long> { { Gw2Constants.CoinCurrencyId, 5 } };

            Assert.Throws<ArgumentException>(() => new CurrencyValuation(entries));
        }

        [Fact]
        public void Constructor_ValidEntries_Accepted()
        {
            var valuation = new CurrencyValuation(new Dictionary<int, long>
            {
                { 2, 5 },
                { 23, 1200 }
            });

            Assert.True(valuation.TryGetCopperValue(2, out long karmaValue));
            Assert.Equal(5, karmaValue);
        }

        [Fact]
        public void Constructor_NullDictionary_ProducesEmptyValuation()
        {
            var valuation = new CurrencyValuation(null);

            Assert.False(valuation.TryGetCopperValue(2, out _));
            Assert.Empty(valuation.CopperPerUnit);
        }
    }
}
