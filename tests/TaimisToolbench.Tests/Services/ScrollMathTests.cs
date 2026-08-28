using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
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

        // ApplyPixelDelta
        // coverage.
        [Fact]
        public void ApplyPixelDelta_ContentFitsViewport_ReturnsZero()
        {
            Assert.Equal(0f, ScrollMath.ApplyPixelDelta(0.5f, -90, 500, 700));
            Assert.Equal(0f, ScrollMath.ApplyPixelDelta(0.5f, -90, 700, 700));
        }

        [Fact]
        public void ApplyPixelDelta_NegativeDelta_MovesTowardTop()
        {
            // 1300 scrollable px, starting at ratio 0.5 (650px), moving up
            // 90px -> 560/1300.
            float result = ScrollMath.ApplyPixelDelta(0.5f, -90, 2000, 700);
            Assert.Equal(560f / 1300f, result, 3);
        }

        [Fact]
        public void ApplyPixelDelta_PositiveDelta_MovesTowardBottom()
        {
            float result = ScrollMath.ApplyPixelDelta(0.5f, 90, 2000, 700);
            Assert.Equal(740f / 1300f, result, 3);
        }

        [Fact]
        public void ApplyPixelDelta_MultiNotchDelta_ComposesLinearlyWithSingleNotches()
        {
            // Applying one -270px delta must land identically to three
            // successive -90px single-notch corrections (Blish's own
            // per-notch step is a simple linear accumulator).
            float viaSingleSteps = 0.5f;
            viaSingleSteps = ScrollMath.ApplyPixelDelta(viaSingleSteps, -90, 2000, 700);
            viaSingleSteps = ScrollMath.ApplyPixelDelta(viaSingleSteps, -90, 2000, 700);
            viaSingleSteps = ScrollMath.ApplyPixelDelta(viaSingleSteps, -90, 2000, 700);

            float viaOneMultiNotchWrite = ScrollMath.ApplyPixelDelta(0.5f, -270, 2000, 700);

            Assert.Equal(viaSingleSteps, viaOneMultiNotchWrite, 4);
        }

        [Fact]
        public void ApplyPixelDelta_NegativeOvershoot_ClampsToZero()
        {
            Assert.Equal(0f, ScrollMath.ApplyPixelDelta(0.05f, -5000, 2000, 700));
        }

        [Fact]
        public void ApplyPixelDelta_PositiveOvershoot_ClampsToOne()
        {
            Assert.Equal(1f, ScrollMath.ApplyPixelDelta(0.95f, 5000, 2000, 700));
        }

        [Fact]
        public void ApplyPixelDelta_OutOfRangeCurrentRatio_ClampedBeforeUse()
        {
            // Defensive: a caller-supplied ratio outside 0..1 must not
            // produce a nonsensical negative offset internally.
            Assert.Equal(0f, ScrollMath.ApplyPixelDelta(-0.5f, 0, 2000, 700));
            Assert.Equal(1f, ScrollMath.ApplyPixelDelta(1.5f, 0, 2000, 700));
        }

        [Fact]
        public void ApplyPixelDelta_ZeroDelta_ReturnsSameRatio()
        {
            Assert.Equal(0.5f, ScrollMath.ApplyPixelDelta(0.5f, 0, 2000, 700), 4);
        }
    }
}
