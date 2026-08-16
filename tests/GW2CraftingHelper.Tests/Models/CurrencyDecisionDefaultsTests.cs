using GW2CraftingHelper.Models;
using Xunit;

namespace GW2CraftingHelper.Tests.Models
{
    // currency-ux-package (Feature 1): CurrencyDecisionDefaults is a static
    // curated table, not a service - these tests pin its structural
    // invariants (no coin-keyed entry, no entry for the currencies the
    // maintainer explicitly decided must stay blank) rather than mirroring
    // every one of its 41 entries.
    public class CurrencyDecisionDefaultsTests
    {
        [Fact]
        public void DefaultCopperPerUnit_NeverKeyedOnCoinCurrencyId()
        {
            Assert.False(CurrencyDecisionDefaults.DefaultCopperPerUnit.ContainsKey(Gw2Constants.CoinCurrencyId));
        }

        [Fact]
        public void DefaultCopperPerUnit_EveryValuePositive()
        {
            foreach (var kvp in CurrencyDecisionDefaults.DefaultCopperPerUnit)
            {
                Assert.True(kvp.Value > 0, $"Currency {kvp.Key} has a non-positive default value {kvp.Value}.");
            }
        }

        [Theory]
        [InlineData(2, 1)]     // Karma
        [InlineData(3, 3500)]  // Laurel
        [InlineData(23, 3600)] // Spirit Shard
        public void TryGetDefault_KnownCurrency_ReturnsExpectedValue(int currencyId, long expected)
        {
            Assert.True(CurrencyDecisionDefaults.TryGetDefault(currencyId, out long copperPerUnit));
            Assert.Equal(expected, copperPerUnit);
        }

        // Maintainer decision (currency-ux-package): Astral Acclaim (63)
        // and the three Rift Essence tiers (78/79/80) must stay blank -
        // gw2efficiency's own table has no row for any of them (it does
        // not reach those ids), so this module must not invent one either.
        [Theory]
        [InlineData(63)]
        [InlineData(78)]
        [InlineData(79)]
        [InlineData(80)]
        public void TryGetDefault_CurrenciesWithNoUpstreamEntry_ReturnsFalse(int currencyId)
        {
            Assert.False(CurrencyDecisionDefaults.TryGetDefault(currencyId, out _));
        }

        // Ids gw2efficiency's own table marks `undefined` (it assigns them
        // no decision value at all) among the currencies this module
        // currently surfaces in Settings.
        [Theory]
        [InlineData(18)] // Transmutation Charges
        [InlineData(30)] // PvP League Tickets
        [InlineData(47)] // Racing Medallions
        public void TryGetDefault_Gw2eUndefinedEntries_ReturnsFalse(int currencyId)
        {
            Assert.False(CurrencyDecisionDefaults.TryGetDefault(currencyId, out _));
        }
    }
}
