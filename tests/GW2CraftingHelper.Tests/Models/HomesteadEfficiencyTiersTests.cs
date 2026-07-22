using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;
using Xunit;

namespace GW2CraftingHelper.Tests.Models
{
    public class HomesteadEfficiencyTiersTests
    {
        [Fact]
        public void Constructor_UnknownMaterialId_ThrowsArgumentException()
        {
            var entries = new Dictionary<int, int> { { 12345, 1 } };

            Assert.Throws<ArgumentException>(() => new HomesteadEfficiencyTiers(entries));
        }

        [Fact]
        public void Constructor_TierBelowZero_ThrowsArgumentOutOfRangeException()
        {
            var entries = new Dictionary<int, int> { { Gw2Constants.RefinedHomesteadFiberItemId, -1 } };

            Assert.Throws<ArgumentOutOfRangeException>(() => new HomesteadEfficiencyTiers(entries));
        }

        [Fact]
        public void Constructor_TierAboveTwo_ThrowsArgumentOutOfRangeException()
        {
            var entries = new Dictionary<int, int> { { Gw2Constants.RefinedHomesteadMetalItemId, 3 } };

            Assert.Throws<ArgumentOutOfRangeException>(() => new HomesteadEfficiencyTiers(entries));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Constructor_ValidTier_Accepted(int tier)
        {
            var entries = new Dictionary<int, int> { { Gw2Constants.RefinedHomesteadWoodItemId, tier } };

            var tiers = new HomesteadEfficiencyTiers(entries);

            Assert.Equal(tier, tiers.GetTier(Gw2Constants.RefinedHomesteadWoodItemId));
        }

        [Fact]
        public void Constructor_NullDictionary_ProducesEmptyConfiguration()
        {
            var tiers = new HomesteadEfficiencyTiers(null);

            Assert.Empty(tiers.TierByMaterialId);
        }

        [Fact]
        public void GetTier_UnconfiguredMaterial_ReturnsZero()
        {
            // gw2e's own default AND its no-API-key fallback (see class doc
            // comment) - unconfigured must mean "no upgrade", never invent
            // a higher tier.
            var tiers = new HomesteadEfficiencyTiers(new Dictionary<int, int>());

            Assert.Equal(0, tiers.GetTier(Gw2Constants.RefinedHomesteadFiberItemId));
            Assert.Equal(0, tiers.GetTier(Gw2Constants.RefinedHomesteadMetalItemId));
            Assert.Equal(0, tiers.GetTier(Gw2Constants.RefinedHomesteadWoodItemId));
        }

        [Fact]
        public void GetTier_UnknownMaterialId_ReturnsZeroRatherThanThrowing()
        {
            var tiers = HomesteadEfficiencyTiers.Default;

            Assert.Equal(0, tiers.GetTier(999999));
        }

        [Fact]
        public void Default_IsAllZero()
        {
            var tiers = HomesteadEfficiencyTiers.Default;

            Assert.Equal(0, tiers.GetTier(Gw2Constants.RefinedHomesteadFiberItemId));
            Assert.Equal(0, tiers.GetTier(Gw2Constants.RefinedHomesteadMetalItemId));
            Assert.Equal(0, tiers.GetTier(Gw2Constants.RefinedHomesteadWoodItemId));
        }

        [Fact]
        public void MutatingSourceDictionaryAfterConstruction_DoesNotAffectInstance()
        {
            var source = new Dictionary<int, int> { { Gw2Constants.RefinedHomesteadFiberItemId, 1 } };
            var tiers = new HomesteadEfficiencyTiers(source);

            source[Gw2Constants.RefinedHomesteadFiberItemId] = 2;

            Assert.Equal(1, tiers.GetTier(Gw2Constants.RefinedHomesteadFiberItemId));
        }
    }
}
