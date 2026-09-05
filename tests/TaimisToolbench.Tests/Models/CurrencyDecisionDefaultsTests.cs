using System.Linq;
using TaimisToolbench.Models;
using Xunit;

namespace TaimisToolbench.Tests.Models
{
    // CurrencyDecisionDefaults is a static
    // curated table, not a service - these tests pin its structural
    // invariants (no coin-keyed entry, no entry for the currencies
    // deliberately left blank, and the values a
    // second table would otherwise contradict) rather than mirroring every
    // one of its entries.
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

        // Unlike the ids above, gw2efficiency DOES value id 39 (at 3600):
        // this is the one upstream row the module drops on purpose, so the
        // gap reads as a divergence and not as drift. The currency was
        // retired in-game 2022-07-19 and force-converted to Magnetite
        // Shards, so no account can hold one and no offer charges one.
        // docs/ARCHITECTURE.md section 8.3.
        [Fact]
        public void TryGetDefault_RetiredGaetingCrystal_ReturnsFalse()
        {
            Assert.False(CurrencyDecisionDefaults.TryGetDefault(39, out long copperPerUnit));
            Assert.Equal(0, copperPerUnit);
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
        [InlineData(77, 3600)] // Gaeting Crystal, the live id
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

        // Gaeting Crystal is derived FROM Magnetite Shard: the only live
        // exchange priced in 77 sells 1 Magnetite Shard for 1 crystal,
        // uncapped, so 77 is wrong the moment 28 moves. Pins the equality,
        // not the number. Deliberately NOT a shared constant, and the value
        // pin above stays: 28 sits in the vendored gw2efficiency block and
        // 77 in the derived-here block, which the source file keeps apart,
        // and docs/ARCHITECTURE.md section 8.3 makes 77 a snapshot of one
        // expansion that is due re-derivation when the next ships. A hard
        // peg would carry a future change to 28 into 77 with nobody
        // re-deriving it; the value pin forces that re-derivation to be a
        // deliberate edit, and this assertion catches silent drift.
        [Fact]
        public void GaetingCrystal_MatchesTheMagnetiteShardItBuys()
        {
            Assert.True(CurrencyDecisionDefaults.TryGetDefault(28, out long magnetiteShard));
            Assert.True(CurrencyDecisionDefaults.TryGetDefault(77, out long gaetingCrystal));

            Assert.Equal(magnetiteShard, gaetingCrystal);
        }

        // Two branches each adding a { 77, ... } row is how a duplicate key
        // reaches a Dictionary collection initialiser: it compiles, then
        // throws ArgumentException from the static constructor, taking the
        // module down at load. Enumerating the table here forces that
        // constructor, so a duplicate fails this test as a
        // TypeInitializationException before any assertion runs. The
        // retired id 39 is pinned absent alongside it because the merge
        // that could duplicate 77 is the same one that could resurrect 39.
        [Fact]
        public void DefaultCopperPerUnit_CarriesGaetingCrystalOnceAndOnlyTheLiveId()
        {
            Assert.Equal(1, CurrencyDecisionDefaults.DefaultCopperPerUnit.Count(entry => entry.Key == 77));
            Assert.Equal(0, CurrencyDecisionDefaults.DefaultCopperPerUnit.Count(entry => entry.Key == 39));
            Assert.Equal(3600, CurrencyDecisionDefaults.DefaultCopperPerUnit[77]);
        }
    }
}
