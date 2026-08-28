using System;
using System.Collections.Generic;
using TaimisToolbench.Models;
using Xunit;

namespace TaimisToolbench.Tests.Models
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
                { 23, 1200 },
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

        // --- currency-ux-package (Feature 1): three-state precedence ---
        [Fact]
        public void Constructor_ClearedCoinCurrencyId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new CurrencyValuation(new Dictionary<int, long>(), new[] { Gw2Constants.CoinCurrencyId }));
        }

        [Fact]
        public void Constructor_CurrencyBothValuedAndCleared_ThrowsArgumentException()
        {
            var entries = new Dictionary<int, long> { { 2, 5 } };

            Assert.Throws<ArgumentException>(() => new CurrencyValuation(entries, new[] { 2 }));
        }

        [Fact]
        public void Constructor_NullClearedCollection_ProducesEmptyClearedSet()
        {
            var valuation = new CurrencyValuation(new Dictionary<int, long>(), null);

            Assert.Empty(valuation.ClearedCurrencyIds);
            Assert.False(valuation.IsCleared(2));
        }

        [Fact]
        public void IsCleared_ClearedCurrency_ReturnsTrue()
        {
            var valuation = new CurrencyValuation(new Dictionary<int, long>(), new[] { 2 });

            Assert.True(valuation.IsCleared(2));
            Assert.Contains(2, valuation.ClearedCurrencyIds);
        }

        [Fact]
        public void TryGetEffectiveCopperValue_UserOverride_WinsOverDefault()
        {
            // Currency 2 (Karma) has a CurrencyDecisionDefaults entry (1
            // copper/unit) - an explicit user override must still win.
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 999 } });

            Assert.True(valuation.TryGetEffectiveCopperValue(2, out long copperPerUnit));
            Assert.Equal(999, copperPerUnit);
        }

        [Fact]
        public void TryGetEffectiveCopperValue_NoOverrideNotCleared_FallsThroughToDefault()
        {
            var valuation = CurrencyValuation.None;

            Assert.True(valuation.TryGetEffectiveCopperValue(2, out long copperPerUnit));
            Assert.Equal(CurrencyDecisionDefaults.DefaultCopperPerUnit[2], copperPerUnit);
        }

        [Fact]
        public void TryGetEffectiveCopperValue_Cleared_NeverFallsThroughToDefault()
        {
            var valuation = new CurrencyValuation(new Dictionary<int, long>(), new[] { 2 });

            Assert.False(valuation.TryGetEffectiveCopperValue(2, out long copperPerUnit));
            Assert.Equal(0, copperPerUnit);
        }

        [Fact]
        public void TryGetEffectiveCopperValue_NoOverrideAndNoDefault_ReturnsFalse()
        {
            // Currency 18 (Transmutation Charges) deliberately has no
            // CurrencyDecisionDefaults entry - see that class's doc
            // comment.
            var valuation = CurrencyValuation.None;

            Assert.False(valuation.TryGetEffectiveCopperValue(18, out long copperPerUnit));
            Assert.Equal(0, copperPerUnit);
        }

        // --- CurrencyValuation.WithDefaults:
        // WithDefaults, the merge previously only exercised indirectly via
        // the Blish-coupled (and therefore untestable) ModuleSettings.
        // GetEffectiveCurrencyValuation. ---
        [Fact]
        public void WithDefaults_NullPersisted_ProducesOnlyDefaults()
        {
            var merged = CurrencyValuation.WithDefaults(null);

            Assert.True(merged.TryGetCopperValue(2, out long karmaValue));
            Assert.Equal(CurrencyDecisionDefaults.DefaultCopperPerUnit[2], karmaValue);
            Assert.Empty(merged.ClearedCurrencyIds);
        }

        [Fact]
        public void WithDefaults_UserOverride_WinsOverDefaultInMergedResult()
        {
            var persisted = new CurrencyValuation(new Dictionary<int, long> { { 2, 999 } });

            var merged = CurrencyValuation.WithDefaults(persisted);

            Assert.True(merged.TryGetCopperValue(2, out long karmaValue));
            Assert.Equal(999, karmaValue);
        }

        [Fact]
        public void WithDefaults_ClearedCurrency_HasNoValueInMergedResultAndStaysCleared()
        {
            var persisted = new CurrencyValuation(new Dictionary<int, long>(), new[] { 2 });

            var merged = CurrencyValuation.WithDefaults(persisted);

            // The non-obvious invariant this method depends on: an id that
            // is cleared must never also land in the merged value set
            // (CurrencyValuation's own constructor throws if both are
            // true) - covered here as the concrete regression this
            // this test exists to guard, not merely re-asserting the
            // constructor's own already-tested behavior.
            Assert.False(merged.TryGetCopperValue(2, out _));
            Assert.True(merged.IsCleared(2));
        }

        [Fact]
        public void WithDefaults_CurrencyWithNoDefault_StaysUnvaluedUnlessOverridden()
        {
            // Currency 18 (Transmutation Charges) has no CurrencyDecisionDefaults
            // entry - it must not appear in the merged result at all when
            // the user never set an override for it.
            var merged = CurrencyValuation.WithDefaults(CurrencyValuation.None);

            Assert.False(merged.TryGetCopperValue(18, out _));
        }

        [Fact]
        public void WithDefaults_EveryDefaultId_IsPresentInMergedResult()
        {
            var merged = CurrencyValuation.WithDefaults(CurrencyValuation.None);

            foreach (var kvp in CurrencyDecisionDefaults.DefaultCopperPerUnit)
            {
                Assert.True(merged.TryGetCopperValue(kvp.Key, out long copperPerUnit));
                Assert.Equal(kvp.Value, copperPerUnit);
            }
        }

        // --- barter-item valuations: the item-keyed twin of every
        // currency-keyed rule above (see CurrencyValuation's class doc
        // comment for why they are two tables and not one). ---
        [Fact]
        public void Constructor_ZeroItemCopperPerUnit_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CurrencyValuation(
                null, null, new Dictionary<int, long> { { 43992, 0 } }));
        }

        [Fact]
        public void Constructor_ItemBothValuedAndCleared_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new CurrencyValuation(
                null, null, new Dictionary<int, long> { { 43992, 5 } }, new[] { 43992 }));
        }

        [Fact]
        public void Constructor_ItemIdCollidingWithAValuedCurrencyId_IsIndependent()
        {
            // Currency 39 is Gaeting Crystal in the wallet; item 39 is an
            // unrelated item. The two tables must not answer for each other.
            var valuation = new CurrencyValuation(
                new Dictionary<int, long> { { 39, 100 } },
                null,
                new Dictionary<int, long> { { 39, 7 } });

            Assert.True(valuation.TryGetCopperValue(39, out long currencyValue));
            Assert.Equal(100, currencyValue);
            Assert.True(valuation.TryGetItemCopperValue(39, out long itemValue));
            Assert.Equal(7, itemValue);
        }

        [Fact]
        public void TryGetEffectiveItemCopperValue_UserOverride_WinsOverDefault()
        {
            // Item 19925 (Obsidian Shard) has a BarterItemDecisionDefaults
            // entry - an explicit user override must still win.
            var valuation = new CurrencyValuation(
                null, null, new Dictionary<int, long> { { 19925, 999 } });

            Assert.True(valuation.TryGetEffectiveItemCopperValue(19925, out long copperPerUnit));
            Assert.Equal(999, copperPerUnit);
        }

        [Fact]
        public void TryGetEffectiveItemCopperValue_NoOverrideNotCleared_FallsThroughToDefault()
        {
            Assert.True(CurrencyValuation.None.TryGetEffectiveItemCopperValue(19925, out long copperPerUnit));
            Assert.Equal(
                BarterItemDecisionDefaults.Defaults[19925].CopperPerUnit, copperPerUnit);
        }

        [Fact]
        public void TryGetEffectiveItemCopperValue_Cleared_NeverFallsThroughToDefault()
        {
            var valuation = new CurrencyValuation(null, null, null, new[] { 19925 });

            Assert.False(valuation.TryGetEffectiveItemCopperValue(19925, out long copperPerUnit));
            Assert.Equal(0, copperPerUnit);
            Assert.True(valuation.IsItemCleared(19925));
        }

        [Fact]
        public void TryGetEffectiveItemCopperValue_BlackLionClaimTicket_HasNoDefault()
        {
            // 43992 is the single most-used unpriced barter item in
            // ref/vendor_offers.json (2,365 offers) and is deliberately
            // left unvalued - gem-store RNG-chest currency whose gold worth
            // is personal, the same posture Astral Acclaim already gets.
            Assert.False(CurrencyValuation.None.TryGetEffectiveItemCopperValue(43992, out long copperPerUnit));
            Assert.Equal(0, copperPerUnit);
        }

        [Fact]
        public void WithDefaults_EveryBarterItemDefaultId_IsPresentInMergedResult()
        {
            var merged = CurrencyValuation.WithDefaults(CurrencyValuation.None);

            foreach (var kvp in BarterItemDecisionDefaults.Defaults)
            {
                Assert.True(merged.TryGetItemCopperValue(kvp.Key, out long copperPerUnit));
                Assert.Equal(kvp.Value.CopperPerUnit, copperPerUnit);
            }
        }

        [Fact]
        public void WithDefaults_ClearedItem_HasNoValueInMergedResultAndStaysCleared()
        {
            var persisted = new CurrencyValuation(null, null, null, new[] { 19925 });

            var merged = CurrencyValuation.WithDefaults(persisted);

            Assert.False(merged.TryGetItemCopperValue(19925, out _));
            Assert.True(merged.IsItemCleared(19925));
        }

        [Fact]
        public void WithDefaults_ItemOverride_WinsOverDefaultInMergedResult()
        {
            var persisted = new CurrencyValuation(
                null, null, new Dictionary<int, long> { { 19925, 42 } });

            var merged = CurrencyValuation.WithDefaults(persisted);

            Assert.True(merged.TryGetItemCopperValue(19925, out long copperPerUnit));
            Assert.Equal(42, copperPerUnit);
        }
    }
}
