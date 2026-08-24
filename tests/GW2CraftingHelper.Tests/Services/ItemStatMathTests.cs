using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Ground truth throughout: every expected number below is what
    /// /v2/items itself publishes in that item's
    /// details.infix_upgrade.attributes, and every input is that same
    /// item's details.attribute_adjustment paired with the Berserker's
    /// (/v2/itemstats id 161) multipliers .35 / .25 / .25. The test
    /// therefore proves the formula reproduces the API, not that the
    /// method agrees with itself.
    /// </summary>
    public class ItemStatMathTests
    {
        private const double PowerMultiplier = 0.35d;
        private const double PrecisionMultiplier = 0.25d;
        private const double FerocityMultiplier = 0.25d;

        [Theory]
        // Zojja's Warfists / Pauldrons - API says 47 / 34 / 34
        [InlineData(134.442d, 47, 34, 34)]
        // Zojja's Visor - API says 63 / 45 / 45
        [InlineData(179.256d, 63, 45, 45)]
        // Zojja's Tassets - API says 94 / 67 / 67
        [InlineData(268.884d, 94, 67, 67)]
        // Zojja's Breastplate / Doublet - API says 141 / 101 / 101
        [InlineData(403.326d, 141, 101, 101)]
        public void AttributeValue_ReproducesTheApisOwnPublishedModifiers(
            double adjustment, int expectedPower, int expectedPrecision, int expectedFerocity)
        {
            Assert.Equal(expectedPower, ItemStatMath.AttributeValue(PowerMultiplier, adjustment));
            Assert.Equal(expectedPrecision, ItemStatMath.AttributeValue(PrecisionMultiplier, adjustment));
            Assert.Equal(expectedFerocity, ItemStatMath.AttributeValue(FerocityMultiplier, adjustment));
        }

        [Fact]
        public void AttributeValue_RoundsHalvesAwayFromZeroRatherThanToEven()
        {
            Assert.Equal(3, ItemStatMath.AttributeValue(0.5d, 5d));
            Assert.Equal(2, ItemStatMath.AttributeValue(0.5d, 3d));
        }

        [Fact]
        public void AttributeValue_ZeroAdjustmentYieldsZero()
        {
            Assert.Equal(0, ItemStatMath.AttributeValue(PowerMultiplier, 0d));
        }

        [Theory]
        [InlineData("CritDamage", "Ferocity")]
        [InlineData("Healing", "Healing Power")]
        [InlineData("BoonDuration", "Concentration")]
        [InlineData("ConditionDuration", "Expertise")]
        [InlineData("ConditionDamage", "Condition Damage")]
        [InlineData("AgonyResistance", "Agony Resistance")]
        [InlineData("Power", "Power")]
        [InlineData("Precision", "Precision")]
        [InlineData("Toughness", "Toughness")]
        [InlineData("Vitality", "Vitality")]
        public void AttributeDisplayName_MapsEveryAttributeTheApiActuallyEmits(string apiName, string expected)
        {
            Assert.Equal(expected, ItemStatMath.AttributeDisplayName(apiName));
        }

        [Fact]
        public void AttributeDisplayName_PassesAnUnknownTokenThroughRatherThanDroppingIt()
        {
            Assert.Equal("SomeFutureAttribute", ItemStatMath.AttributeDisplayName("SomeFutureAttribute"));
            Assert.Equal("", ItemStatMath.AttributeDisplayName(null));
        }
    }
}
