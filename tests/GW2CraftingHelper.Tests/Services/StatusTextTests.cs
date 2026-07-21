using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{

    public class StatusTextTests
    {

        [Fact]
        public void Normalize_NonNull_ReturnsSameString()
        {
            Assert.Equal("Updated \u2014 1:00 PM", StatusText.Normalize("Updated \u2014 1:00 PM"));
        }

        [Fact]
        public void Normalize_Null_ReturnsEmpty()
        {
            Assert.Equal("", StatusText.Normalize(null));
        }

        [Fact]
        public void Normalize_Empty_ReturnsEmpty()
        {
            Assert.Equal("", StatusText.Normalize(""));
        }

        // M37 (KNOWN-ISSUES #22/#27): the ignore toggle (and every other
        // non-Best-Path re-solve trigger) must never produce the Best
        // Path preset's own label, regardless of the current override
        // count - this is exactly the "Best path restored" mislabel bug.
        [Fact]
        public void ForOverrideResolve_NotBestPathPreset_ZeroOverrides_ReturnsDecisionsUpdated()
        {
            Assert.Equal("Decisions updated (0 override(s))", StatusText.ForOverrideResolve(isBestPathPreset: false, overrideCount: 0));
        }

        [Fact]
        public void ForOverrideResolve_NotBestPathPreset_WithOverrides_ReturnsDecisionsUpdatedWithCount()
        {
            Assert.Equal("Decisions updated (3 override(s))", StatusText.ForOverrideResolve(isBestPathPreset: false, overrideCount: 3));
        }

        [Fact]
        public void ForOverrideResolve_BestPathPreset_ReturnsBestPathRestored()
        {
            Assert.Equal("Best path restored", StatusText.ForOverrideResolve(isBestPathPreset: true, overrideCount: 0));
        }
    }

}
