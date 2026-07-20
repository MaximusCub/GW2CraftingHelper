using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class ScrollMathTests
    {
        [Fact]
        public void ContentFitsViewport_ReturnsZero()
        {
            Assert.Equal(0f, ScrollMath.RatioForOffset(100, 500, 700));
            Assert.Equal(0f, ScrollMath.RatioForOffset(100, 700, 700));
        }

        [Fact]
        public void ZeroOrNegativeOffset_ReturnsZero()
        {
            Assert.Equal(0f, ScrollMath.RatioForOffset(0, 2000, 700));
            Assert.Equal(0f, ScrollMath.RatioForOffset(-5, 2000, 700));
        }

        [Fact]
        public void MidScroll_ProportionalRatio()
        {
            // 650 of 1300 scrollable pixels -> 0.5
            Assert.Equal(0.5f, ScrollMath.RatioForOffset(650, 2000, 700), 3);
        }

        [Fact]
        public void OffsetBeyondScrollable_ClampsToOne()
        {
            // Content shrank below the saved offset: clamp to bottom
            Assert.Equal(1f, ScrollMath.RatioForOffset(5000, 2000, 700));
        }
    }
}
