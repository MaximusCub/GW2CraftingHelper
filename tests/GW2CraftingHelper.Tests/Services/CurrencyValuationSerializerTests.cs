using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // CurrencyValuationSerializer is the Blish-free half of ModuleSettings'
    // currency-valuation persistence (see Services/ModuleSettings.cs): the
    // SettingEntry<string> plumbing itself references Blish_HUD.Settings and
    // cannot be constructed in a Blish-free test, but the actual JSON
    // conversion ModuleSettings.GetCurrencyValuation/SetCurrencyValuation
    // delegate to lives here and is fully exercised below.
    public class CurrencyValuationSerializerTests
    {
        [Fact]
        public void SerializeThenDeserialize_RoundTripsEntries()
        {
            // Currency 63 (Astral Acclaim) - addendum-astral-acclaim.md P1:
            // added to SettingsTabContent.CuratedCurrencyIds alongside
            // Karma/Spirit Shards, so its valuation must round-trip through
            // this same serializer exactly like any other curated currency.
            var valuation = new CurrencyValuation(new Dictionary<int, long>
            {
                { 2, 5 },
                { 23, 1200 },
                { 63, 800 }
            });

            string json = CurrencyValuationSerializer.Serialize(valuation);
            var roundTripped = CurrencyValuationSerializer.Deserialize(json);

            Assert.True(roundTripped.TryGetCopperValue(2, out long karmaValue));
            Assert.Equal(5, karmaValue);
            Assert.True(roundTripped.TryGetCopperValue(23, out long spiritShardValue));
            Assert.Equal(1200, spiritShardValue);
            Assert.True(roundTripped.TryGetCopperValue(63, out long astralAcclaimValue));
            Assert.Equal(800, astralAcclaimValue);
        }

        [Fact]
        public void Serialize_EmptyValuation_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, CurrencyValuationSerializer.Serialize(CurrencyValuation.None));
        }

        [Fact]
        public void Serialize_NullValuation_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, CurrencyValuationSerializer.Serialize(null));
        }

        [Fact]
        public void Deserialize_NullOrWhitespace_ReturnsNone()
        {
            Assert.Same(CurrencyValuation.None, CurrencyValuationSerializer.Deserialize(null));
            Assert.Same(CurrencyValuation.None, CurrencyValuationSerializer.Deserialize(string.Empty));
            Assert.Same(CurrencyValuation.None, CurrencyValuationSerializer.Deserialize("   "));
        }

        [Fact]
        public void Deserialize_MalformedJson_ReturnsNoneInsteadOfThrowing()
        {
            var result = CurrencyValuationSerializer.Deserialize("{not valid json");

            Assert.Same(CurrencyValuation.None, result);
        }

        [Fact]
        public void Deserialize_EmptyObject_ReturnsNone()
        {
            var result = CurrencyValuationSerializer.Deserialize("{}");

            Assert.Same(CurrencyValuation.None, result);
        }

        [Fact]
        public void Deserialize_SkipsNonPositiveEntries_KeepsValidOnes()
        {
            // Entry 2 is valid; 5 (zero) and 6 (negative) are not and must be
            // skipped rather than raising - CurrencyValuation's constructor
            // now rejects non-positive rates outright, so the serializer
            // must filter before constructing.
            var json = "{\"2\":5,\"5\":0,\"6\":-1}";

            var result = CurrencyValuationSerializer.Deserialize(json);

            Assert.True(result.TryGetCopperValue(2, out long karmaValue));
            Assert.Equal(5, karmaValue);
            Assert.False(result.TryGetCopperValue(5, out _));
            Assert.False(result.TryGetCopperValue(6, out _));
        }

        [Fact]
        public void Deserialize_SkipsCoinKeyedEntry_KeepsValidOnes()
        {
            // Entry keyed on the coin currency id (1) must be skipped -
            // coin priced in terms of itself is nonsensical and the
            // constructor now rejects it outright.
            var json = $"{{\"{Gw2Constants.CoinCurrencyId}\":5,\"2\":10}}";

            var result = CurrencyValuationSerializer.Deserialize(json);

            Assert.False(result.TryGetCopperValue(Gw2Constants.CoinCurrencyId, out _));
            Assert.True(result.TryGetCopperValue(2, out long karmaValue));
            Assert.Equal(10, karmaValue);
        }

        [Fact]
        public void Deserialize_AllEntriesInvalid_ReturnsNone()
        {
            var json = $"{{\"{Gw2Constants.CoinCurrencyId}\":5,\"2\":0}}";

            var result = CurrencyValuationSerializer.Deserialize(json);

            Assert.Same(CurrencyValuation.None, result);
        }
    }
}
