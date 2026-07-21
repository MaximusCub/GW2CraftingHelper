using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class WheelDeltaSanitizerTests
    {
        // Exact histogram measured in the live 2026-07-21 instrumented
        // user trace (M36, KNOWN-ISSUES #12 reopened): fast multi-notch
        // wheel-UP flicks arrive as (N*120) - 65536 for N=2..8.
        [Theory]
        [InlineData(-65296, 240)]  // N=2
        [InlineData(-65176, 360)]  // N=3
        [InlineData(-65056, 480)]  // N=4
        [InlineData(-64936, 600)]  // N=5
        [InlineData(-64816, 720)]  // N=6
        [InlineData(-64696, 840)]  // N=7
        [InlineData(-64576, 960)]  // N=8
        public void WrappedUpFlick_ClassifiedAsWrapped_RecoversIntendedPositiveDelta(int raw, int expectedIntended)
        {
            var (isWrapped, intendedDelta) = WheelDeltaSanitizer.Classify(raw);

            Assert.True(isWrapped);
            Assert.Equal(expectedIntended, intendedDelta);
        }

        // Fast multi-notch DOWN flicks coalesce cleanly (no corruption) -
        // must pass through completely unchanged.
        [Theory]
        [InlineData(-240)]
        [InlineData(-360)]
        [InlineData(-480)]
        [InlineData(-600)]
        [InlineData(-720)]
        [InlineData(-840)]
        public void CleanCoalescedDownFlick_NotWrapped_PassesThroughUnchanged(int raw)
        {
            var (isWrapped, intendedDelta) = WheelDeltaSanitizer.Classify(raw);

            Assert.False(isWrapped);
            Assert.Equal(raw, intendedDelta);
        }

        // Single notches both directions are clean.
        [Theory]
        [InlineData(120)]
        [InlineData(-120)]
        public void SingleNotch_NotWrapped_PassesThroughUnchanged(int raw)
        {
            var (isWrapped, intendedDelta) = WheelDeltaSanitizer.Classify(raw);

            Assert.False(isWrapped);
            Assert.Equal(raw, intendedDelta);
        }

        [Fact]
        public void NoScroll_Zero_NotWrapped()
        {
            var (isWrapped, intendedDelta) = WheelDeltaSanitizer.Classify(0);

            Assert.False(isWrapped);
            Assert.Equal(0, intendedDelta);
        }

        // An absurd (implausible) 40-notch clean down-flick must still
        // never be misclassified as wrapped - part of the threshold's
        // safety margin.
        [Fact]
        public void AbsurdFortyNotchDownFlick_StillNotWrapped()
        {
            var (isWrapped, intendedDelta) = WheelDeltaSanitizer.Classify(-4800);

            Assert.False(isWrapped);
            Assert.Equal(-4800, intendedDelta);
        }

        // Boundary: exactly at the threshold is wrapped (inclusive), one
        // above it is not.
        [Fact]
        public void ExactlyAtThreshold_IsWrapped()
        {
            var (isWrapped, intendedDelta) = WheelDeltaSanitizer.Classify(-60000);

            Assert.True(isWrapped);
            Assert.Equal(5536, intendedDelta);
        }

        [Fact]
        public void OneAboveThreshold_NotWrapped()
        {
            var (isWrapped, intendedDelta) = WheelDeltaSanitizer.Classify(-59999);

            Assert.False(isWrapped);
            Assert.Equal(-59999, intendedDelta);
        }

        // Never-actually-observed (N=1 does not mis-fire in practice) but
        // mathematically-in-band edge, plus the theoretical minimum
        // (N=0, raw=-65536) - both should still classify and recover
        // correctly, not throw or overflow.
        [Fact]
        public void TheoreticalWrapBandEdges_RecoverCorrectly()
        {
            var n1 = WheelDeltaSanitizer.Classify(-65416);
            Assert.True(n1.IsWrapped);
            Assert.Equal(120, n1.IntendedDelta);

            var n0 = WheelDeltaSanitizer.Classify(-65536);
            Assert.True(n0.IsWrapped);
            Assert.Equal(0, n0.IntendedDelta);
        }

        [Fact]
        public void PositiveDelta_NeverWrapped()
        {
            var (isWrapped, intendedDelta) = WheelDeltaSanitizer.Classify(5520);

            Assert.False(isWrapped);
            Assert.Equal(5520, intendedDelta);
        }
    }
}
