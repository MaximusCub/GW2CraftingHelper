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
            var valuation = new CurrencyValuation(new Dictionary<int, long>
            {
                { 2, 5 },
                { 23, 1200 }
            });

            string json = CurrencyValuationSerializer.Serialize(valuation);
            var roundTripped = CurrencyValuationSerializer.Deserialize(json);

            Assert.True(roundTripped.TryGetCopperValue(2, out long karmaValue));
            Assert.Equal(5, karmaValue);
            Assert.True(roundTripped.TryGetCopperValue(23, out long spiritShardValue));
            Assert.Equal(1200, spiritShardValue);
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
    }
}
