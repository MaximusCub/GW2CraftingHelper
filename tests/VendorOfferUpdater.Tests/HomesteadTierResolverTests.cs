using VendorOfferUpdater;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    public class HomesteadTierResolverTests
    {
        [Theory]
        [InlineData("Homestead Refinement\u2014Farm")]
        [InlineData("Homestead Refinement\u2014Lumber Mill")]
        [InlineData("Homestead Refinement\u2014Metal Forge")]
        public void KnownHomesteadMerchant_NoRequirement_ResolvesTierZero(string merchant)
        {
            int? tier = HomesteadTierResolver.ResolveTier(merchant, null);

            Assert.Equal(0, tier);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void KnownHomesteadMerchant_BlankRequirement_ResolvesTierZero(string requirement)
        {
            int? tier = HomesteadTierResolver.ResolveTier("Homestead Refinement\u2014Metal Forge", requirement);

            Assert.Equal(0, tier);
        }

        [Fact]
        public void KnownHomesteadMerchant_OneRequirement_ResolvesTierOne()
        {
            int? tier = HomesteadTierResolver.ResolveTier(
                "Homestead Refinement\u2014Metal Forge",
                "one [[Homestead Upgrade: Ore Trade Efficiency]]");

            Assert.Equal(1, tier);
        }

        [Fact]
        public void KnownHomesteadMerchant_TwoRequirement_ResolvesTierTwo()
        {
            int? tier = HomesteadTierResolver.ResolveTier(
                "Homestead Refinement\u2014Metal Forge",
                "two [[Homestead Upgrade: Ore Trade Efficiency]]");

            Assert.Equal(2, tier);
        }

        [Fact]
        public void KnownHomesteadMerchant_OneRequirement_CaseInsensitive()
        {
            int? tier = HomesteadTierResolver.ResolveTier(
                "Homestead Refinement\u2014Farm",
                "ONE [[Homestead Upgrade: Fiber Trade Efficiency]]");

            Assert.Equal(1, tier);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Miyani")]
        [InlineData("Battle Master")]
        public void NonHomesteadMerchant_ReturnsNull(string merchant)
        {
            int? tier = HomesteadTierResolver.ResolveTier(merchant, "one [[Something]]");

            Assert.Null(tier);
        }

        [Fact]
        public void KnownHomesteadMerchant_UnrecognizedRequirementText_ReturnsNullRatherThanGuessing()
        {
            // A genuine achievement-gate requirement someone might attach
            // to a Homestead row in the future - must never be
            // misinterpreted as an efficiency tier.
            int? tier = HomesteadTierResolver.ResolveTier(
                "Homestead Refinement\u2014Farm",
                "[[Some Unrelated Achievement]]");

            Assert.Null(tier);
        }

        [Fact]
        public void KnownHomesteadMerchant_WordBoundary_OnerousDoesNotMatchOne()
        {
            int? tier = HomesteadTierResolver.ResolveTier(
                "Homestead Refinement\u2014Farm",
                "onerous requirement text");

            Assert.Null(tier);
        }

        [Fact]
        public void SubstringMatch_MerchantNameWithExtraText_StillResolves()
        {
            // Echoes gw2e's own merchant.name.includes('Homestead
            // Refinement') substring check, not an exact-name match.
            int? tier = HomesteadTierResolver.ResolveTier(
                "Some Homestead Refinement\u2014Farm Location Suffix", null);

            Assert.Equal(0, tier);
        }
    }
}
