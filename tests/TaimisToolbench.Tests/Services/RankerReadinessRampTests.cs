using System;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The readiness bars' colour ramp. White text is drawn ON this fill, so
    /// the contrast floor is not a nicety here - it is the constraint that
    /// decides how deep the ramp is allowed to be, and the reason a naive
    /// red-yellow-green would not do.
    /// </summary>
    public class RankerReadinessRampTests
    {
        [Fact]
        public void TheThreeAnchorsAreExactlyWhereTheyWereAuthored()
        {
            AssertRgb(166, 40, 34, RankerReadinessRamp.Fill(0.0));
            AssertRgb(142, 104, 14, RankerReadinessRamp.Fill(0.5));
            AssertRgb(42, 124, 48, RankerReadinessRamp.Fill(1.0));
        }

        [Theory]
        [InlineData(0.00, 7.12)]
        [InlineData(0.25, 5.99)]
        [InlineData(0.50, 5.08)]
        [InlineData(0.75, 5.12)]
        [InlineData(1.00, 5.22)]
        public void WhiteTextClearsTheContrastFloorAtEveryQuarter(double fraction, double expected)
        {
            double actual = RankerReadinessRamp.ContrastWithWhite(RankerReadinessRamp.Fill(fraction));

            Assert.True(actual >= RankerReadinessRamp.WhiteTextContrastFloor,
                "white over " + fraction + " measures " + actual.ToString("0.00"));

            // Pinned, not merely floored: a brighter ramp that still scraped
            // 4.5 would pass a floor-only assertion and lose the headroom the
            // anchors were chosen for.
            Assert.Equal(expected, Math.Round(actual, 2));
        }

        [Fact]
        public void NoPointOnTheWholeSweepDropsBelowTheFloor()
        {
            // The worst point is not at an anchor - it is at 54%, between
            // the amber anchor and green - which is exactly why sampling
            // only the anchors would not be evidence.
            double worst = double.MaxValue;
            double worstAt = 0;
            for (int i = 0; i <= 1000; i++)
            {
                double t = i / 1000.0;
                double contrast = RankerReadinessRamp.ContrastWithWhite(RankerReadinessRamp.Fill(t));
                if (contrast < worst)
                {
                    worst = contrast;
                    worstAt = t;
                }
            }

            Assert.True(worst >= RankerReadinessRamp.WhiteTextContrastFloor,
                "worst " + worst.ToString("0.00") + " at " + worstAt);
            Assert.True(worst > 5.0);
        }

        [Fact]
        public void TheTrackCarriesWhiteToo()
        {
            // A low fill leaves most of the centred percentage over the
            // TRACK rather than over the ramp, so the plate is under the
            // same obligation the fill is.
            Assert.True(RankerReadinessRamp.ContrastWithWhite(RankerReadinessRamp.Track)
                >= RankerReadinessRamp.WhiteTextContrastFloor);
            Assert.True(RankerReadinessRamp.RelativeLuminance(RankerReadinessRamp.Track)
                < RankerReadinessRamp.RelativeLuminance(RankerReadinessRamp.Fill(0.0)));
        }

        [Fact]
        public void TheTrackIsVisibleAgainstThePanelItSitsOn()
        {
            // The obligation the track was first chosen without. A track
            // that matches its background is not a bar: the fill reads as a
            // floating block with no scale, and the centred percentage
            // reads as detached from it. Measured in game before the fix,
            // the track scored 1.05:1 against the panel while the panel's
            // own texture varies by 1.076:1 - less distinguishable from the
            // surface than the surface is from itself, which is the floor
            // this asserts against.
            double vsPanel = RankerReadinessRamp.ContrastRatio(
                RankerReadinessRamp.Track, RankerReadinessRamp.PanelReference);

            // Compared as EXCESS OVER UNITY, not as the ratios themselves:
            // a contrast ratio starts at 1.0, so 1.076 and 1.351 are 0.076
            // and 0.351 of separation, not numbers to multiply directly.
            // Going darker cannot exceed 1.42:1 against this panel at all,
            // so a threshold stated the naive way is unreachable.
            Assert.True(
                (vsPanel - 1.0) > (PanelTextureNoise - 1.0) * 3.0,
                "track vs panel " + vsPanel.ToString("0.000")
                    + " must clear the panel's own texture noise "
                    + PanelTextureNoise.ToString("0.000") + " by 3x its margin");

            // Darker, not lighter: lighter enough to separate would breach
            // the white-text floor the centred percentage depends on.
            Assert.True(
                RankerReadinessRamp.RelativeLuminance(RankerReadinessRamp.Track)
                    < RankerReadinessRamp.RelativeLuminance(RankerReadinessRamp.PanelReference));
        }

        /// <summary>
        /// Contrast between the two panel samples taken in game,
        /// Rgb(38, 36, 34) and Rgb(42, 42, 41) - how much the surface
        /// varies on its own, and so the floor any marking on it must beat.
        /// </summary>
        private const double PanelTextureNoise = 1.076;

        [Fact]
        public void TheMidRangeIsOrangeAndOlive_NotBrown()
        {
            // The measurement that says OKLCh is earning its keep. An sRGB
            // lerp from this red to this green passes through a desaturated
            // brown at the quarter points; the perceptual path does not.
            var quarter = RankerReadinessRamp.Fill(0.25);
            Assert.True(quarter.R > quarter.G);
            Assert.True(quarter.G > quarter.B);

            // Chroma held, not collapsed toward grey.
            Assert.True(quarter.R - quarter.B > 120);

            var threeQuarter = RankerReadinessRamp.Fill(0.75);
            Assert.True(threeQuarter.G >= threeQuarter.R);
            Assert.True(threeQuarter.G - threeQuarter.B > 100);
        }

        [Fact]
        public void TheRampWalksFromRedToGreenWithoutDoublingBack()
        {
            // Green rises and red falls across the whole sweep, so no two
            // readiness figures can read as the same colour by accident.
            var previous = RankerReadinessRamp.Fill(0.0);
            for (int i = 1; i <= 100; i++)
            {
                var current = RankerReadinessRamp.Fill(i / 100.0);
                Assert.True(current.R <= previous.R + 1, "red rose at " + i);
                Assert.True(current.G >= previous.G - 1, "green fell at " + i);
                previous = current;
            }

            Assert.True(RankerReadinessRamp.Fill(0.0).R > RankerReadinessRamp.Fill(1.0).R);
            Assert.True(RankerReadinessRamp.Fill(1.0).G > RankerReadinessRamp.Fill(0.0).G);
        }

        [Theory]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.NegativeInfinity)]
        public void BelowZeroIsTheEmptyAnchor(double fraction)
        {
            AssertRgb(166, 40, 34, RankerReadinessRamp.Fill(fraction));
        }

        [Theory]
        [InlineData(2.0)]
        [InlineData(double.PositiveInfinity)]
        public void AboveOneIsTheFullAnchor(double fraction)
        {
            AssertRgb(42, 124, 48, RankerReadinessRamp.Fill(fraction));
        }

        // ---------------------------------------------------------------
        // How much of a bar is painted. Held off both ends on purpose.
        // ---------------------------------------------------------------
        [Fact]
        public void AMeasuredNonZeroReadinessAlwaysPaintsAtLeastOnePixel()
        {
            // Otherwise a 1% row and a 0% row are the same picture, and the
            // one that has started reads as one that has not.
            Assert.Equal(1, RankerReadinessRamp.FillWidth(100, 0.001));
            Assert.Equal(1, RankerReadinessRamp.FillWidth(60, 0.004));
        }

        [Fact]
        public void AnythingUnderOneHundredPercentLeavesAPixelUnpainted()
        {
            // 99.6% floors to "99%" in the text (FormatReadiness); a bar
            // that painted full beside it would contradict its own label.
            Assert.Equal(99, RankerReadinessRamp.FillWidth(100, 0.996));
            Assert.Equal(100, RankerReadinessRamp.FillWidth(100, 1.0));
        }

        [Fact]
        public void AZeroReadinessPaintsNothing()
        {
            Assert.Equal(0, RankerReadinessRamp.FillWidth(100, 0.0));
            Assert.Equal(0, RankerReadinessRamp.FillWidth(100, -1));
            Assert.Equal(0, RankerReadinessRamp.FillWidth(100, double.NaN));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-40)]
        public void ADegenerateBarPaintsNothingRatherThanGoingNegative(int barWidth)
        {
            Assert.Equal(0, RankerReadinessRamp.FillWidth(barWidth, 0.5));
        }

        [Fact]
        public void FillWidthIsMonotonic()
        {
            int previous = -1;
            for (int i = 0; i <= 100; i++)
            {
                int width = RankerReadinessRamp.FillWidth(140, i / 100.0);
                Assert.True(width >= previous, "fill shrank at " + i);
                Assert.True(width <= 140);
                previous = width;
            }
        }

        private static void AssertRgb(int r, int g, int b, RankerReadinessRamp.Rgb actual)
        {
            Assert.Equal((r, g, b), ((int)actual.R, (int)actual.G, (int)actual.B));
        }
    }
}
