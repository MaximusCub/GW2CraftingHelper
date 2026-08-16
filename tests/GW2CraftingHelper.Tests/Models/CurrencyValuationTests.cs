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

        // --- currency-ux-package review fix (finding 5, MEASURED):
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
            // review-fix test exists to guard, not merely re-asserting the
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
    }
}
