using TaimisToolbench.Models;
using Xunit;

namespace TaimisToolbench.Tests.Models
{
    // CurrencyDecisionDefaults is a static
    // curated table, not a service - these tests pin its structural
    // invariants (no coin-keyed entry, no entry for the currencies
    // deliberately left blank, and the values a second table would
    // otherwise contradict) rather than mirroring every entry.
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
        [InlineData(2, 1)] // Karma
        [InlineData(3, 3500)] // Laurel
        [InlineData(23, 3600)] // Spirit Shard
        public void TryGetDefault_KnownCurrency_ReturnsExpectedValue(int currencyId, long expected)
        {
            Assert.True(CurrencyDecisionDefaults.TryGetDefault(currencyId, out long copperPerUnit));
            Assert.Equal(expected, copperPerUnit);
        }

        // Currencies charged by real offers in ref/vendor_offers.json that
        // this table deliberately does not value: each has either no
        // trading-post-tradable output to anchor on, or sibling currencies
        // whose own values disagree by more than an order of magnitude, so
        // no single figure is defensible. An unvalued cost line correctly
        // makes its offer incomparable rather than cheap. Reason per id:
        // docs/ARCHITECTURE.md section 8.3.
        [Theory]
        [InlineData(63)] // Astral Acclaim - dev/proposals/research-aa-spending-consensus.md
        [InlineData(58)] // War Supplies
        [InlineData(70)] // Legendary Insight
        [InlineData(72)] // Static Charge
        [InlineData(73)] // Pinch of Stardust
        [InlineData(75)] // Calcified Gasp
        [InlineData(78)] // Fine Rift Essence
        [InlineData(79)] // Rare Rift Essence
        [InlineData(80)] // Masterwork Rift Essence
        [InlineData(81)] // Antiquated Ducat
        [InlineData(83)] // Aether-Rich Sap
        public void TryGetDefault_DeliberatelyUnvaluedCurrencies_ReturnsFalse(int currencyId)
        {
            Assert.False(CurrencyDecisionDefaults.TryGetDefault(currencyId, out _));
        }

        // Ids gw2efficiency's own table marks `undefined` (it assigns them
        // no decision value at all) that this repository has not
        // independently derived a value for either.
        [Theory]
        [InlineData(18)] // Transmutation Charges
        [InlineData(47)] // Racing Medallions
        public void TryGetDefault_Gw2eUndefinedEntries_ReturnsFalse(int currencyId)
        {
            Assert.False(CurrencyDecisionDefaults.TryGetDefault(currencyId, out _));
        }

        // The block of this table derived here rather than adapted from
        // gw2efficiency. Pinned by value because each number is an
        // argument, not a preference: changing one without redoing the
        // derivation in docs/ARCHITECTURE.md section 8.3 should fail.
        [Theory]
        [InlineData(30, 3770)] // PvP League Ticket
        [InlineData(66, 197)] // Ancient Coin
        [InlineData(76, 125)] // Ursus Oblige
        [InlineData(77, 3600)] // Gaeting Crystal (Janthir Wilds raids)
        [InlineData(82, 135)] // Testimony of Castoran Heroics
        public void TryGetDefault_DerivedHereEntries_ReturnExpectedValue(int currencyId, long expected)
        {
            Assert.True(CurrencyDecisionDefaults.TryGetDefault(currencyId, out long copperPerUnit));
            Assert.Equal(expected, copperPerUnit);
        }

        // Testimony of Castoran Heroics is derived FROM its two siblings:
        // the Notary of Heroics charges the same count of any of the three
        // for the same item, so the moment the two upstream values move,
        // the derived one is wrong. Pins the equality, not the number.
        [Fact]
        public void CastoranHeroics_MatchesItsDesertAndJadeSiblings()
        {
            Assert.True(CurrencyDecisionDefaults.TryGetDefault(36, out long desert));
            Assert.True(CurrencyDecisionDefaults.TryGetDefault(65, out long jade));
            Assert.True(CurrencyDecisionDefaults.TryGetDefault(82, out long castoran));

            Assert.Equal(desert, jade);
            Assert.Equal(desert, castoran);
        }

        // The live API has two distinct wallet currencies named "Gaeting
        // Crystal" (39, Path of Fire raids; 77, Janthir Wilds raids) plus
        // an item form, 86094, which BarterItemDecisionDefaults already
        // pins to currency 39. All three are the same in-game good, so a
        // plan must never price one differently from another.
        [Fact]
        public void BothGaetingCrystalCurrencies_CarryTheSameValue()
        {
            Assert.True(CurrencyDecisionDefaults.TryGetDefault(39, out long pathOfFire));
            Assert.True(CurrencyDecisionDefaults.TryGetDefault(77, out long janthirWilds));
            Assert.True(BarterItemDecisionDefaults.TryGetDefault(86094, out long itemForm));

            Assert.Equal(pathOfFire, janthirWilds);
            Assert.Equal(pathOfFire, itemForm);
        }
    }
}
