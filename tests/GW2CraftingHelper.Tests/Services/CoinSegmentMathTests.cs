using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // TotalCoinSegmentsWidth/TotalCurrencySegmentsWidth, their plain data
    // specs (CoinSegmentSpec/CurrencySegmentSpec), and the geometry
    // constants they're built from were extracted verbatim out of
    // CoinCurrencyRenderer (Views/Rendering, Blish-bound) into a
    // Blish-free class so the arithmetic can be tested without referencing
    // UI code (repo invariant: tests must never reference UI code).
    // Expected values are computed against the real
    // CoinLabelIconGap/CoinIconSize/CoinSegmentGap constants rather than
    // re-deriving the formula, so a future constant change updates the
    // assertions' inputs, not just the production code, catching drift -
    // mirrors ShoppingColumnMathTests' SegmentRunWidth cases.
    public class CoinSegmentMathTests
    {
        private const int IconSize = CoinSegmentMath.CoinIconSize;
        private const int LabelIconGap = CoinSegmentMath.CoinLabelIconGap;
        private const int SegmentGap = CoinSegmentMath.CoinSegmentGap;

        // --- TotalCoinSegmentsWidth ---

        [Fact]
        public void TotalCoinSegmentsWidth_Empty_ReturnsZero()
        {
            var segments = new List<CoinSegmentMath.CoinSegmentSpec>();

            Assert.Equal(0, CoinSegmentMath.TotalCoinSegmentsWidth(segments));
        }

        [Fact]
        public void TotalCoinSegmentsWidth_SingleSegment_NoTrailingGap()
        {
            // One segment: textWidth + labelIconGap + iconSize, no trailing
            // segmentGap since there is nothing after it.
            var segments = new List<CoinSegmentMath.CoinSegmentSpec>
            {
                new CoinSegmentMath.CoinSegmentSpec { AssetId = 156902, Text = "56", TextWidth = 18 }
            };

            int expected = 18 + LabelIconGap + IconSize;
            Assert.Equal(expected, CoinSegmentMath.TotalCoinSegmentsWidth(segments));
        }

        [Fact]
        public void TotalCoinSegmentsWidth_MultipleSegments_GapBetweenNotAfter()
        {
            // Gold, silver, copper: three segments, one segmentGap between
            // each pair, none trailing after the last - pins the gap
            // arithmetic exactly (2 gaps for 3 segments, not 3).
            var segments = new List<CoinSegmentMath.CoinSegmentSpec>
            {
                new CoinSegmentMath.CoinSegmentSpec { AssetId = 156904, Text = "12", TextWidth = 16 },
                new CoinSegmentMath.CoinSegmentSpec { AssetId = 156907, Text = "34", TextWidth = 18 },
                new CoinSegmentMath.CoinSegmentSpec { AssetId = 156902, Text = "56", TextWidth = 18 }
            };

            int expected =
                (16 + LabelIconGap + IconSize) +
                (18 + LabelIconGap + IconSize) +
                (18 + LabelIconGap + IconSize) +
                2 * SegmentGap;
            Assert.Equal(expected, CoinSegmentMath.TotalCoinSegmentsWidth(segments));
        }

        [Fact]
        public void TotalCoinSegmentsWidth_ZeroTextWidthSegment_StillIncludesIconAndGap()
        {
            // A degenerate zero-width measured string (e.g. a single glyph
            // BitmapFont.MeasureString rounds down to 0) must not collapse
            // the segment away - the icon and its gap still occupy space.
            var segments = new List<CoinSegmentMath.CoinSegmentSpec>
            {
                new CoinSegmentMath.CoinSegmentSpec { AssetId = 156902, Text = "0", TextWidth = 0 }
            };

            int expected = LabelIconGap + IconSize;
            Assert.Equal(expected, CoinSegmentMath.TotalCoinSegmentsWidth(segments));
        }

        // --- TotalCurrencySegmentsWidth ---

        [Fact]
        public void TotalCurrencySegmentsWidth_Empty_ReturnsZero()
        {
            var segments = new List<CoinSegmentMath.CurrencySegmentSpec>();

            Assert.Equal(0, CoinSegmentMath.TotalCurrencySegmentsWidth(segments));
        }

        [Fact]
        public void TotalCurrencySegmentsWidth_SingleSegment_NoTrailingGap()
        {
            var segments = new List<CoinSegmentMath.CurrencySegmentSpec>
            {
                new CoinSegmentMath.CurrencySegmentSpec { IconUrl = "spirit-shard.png", Text = "5", TextWidth = 12 }
            };

            int expected = 12 + LabelIconGap + IconSize;
            Assert.Equal(expected, CoinSegmentMath.TotalCurrencySegmentsWidth(segments));
        }

        [Fact]
        public void TotalCurrencySegmentsWidth_MultipleSegments_GapBetweenNotAfter()
        {
            // Pins the gap arithmetic exactly (1 gap for 2 segments):
            // 30 + 2 + 20 = 52 for the first, 10 + 2 + 20 = 32 for the
            // second, plus a single 6px segmentGap between them = 90.
            var segments = new List<CoinSegmentMath.CurrencySegmentSpec>
            {
                new CoinSegmentMath.CurrencySegmentSpec { IconUrl = "karma.png", Text = "1200", TextWidth = 30 },
                new CoinSegmentMath.CurrencySegmentSpec { IconUrl = "spirit-shard.png", Text = "3", TextWidth = 10 }
            };

            int expected =
                (30 + LabelIconGap + IconSize) +
                (10 + LabelIconGap + IconSize) +
                SegmentGap;
            Assert.Equal(expected, CoinSegmentMath.TotalCurrencySegmentsWidth(segments));
            Assert.Equal(90, expected);
        }

        [Fact]
        public void TotalCurrencySegmentsWidth_ZeroTextWidthSegment_StillIncludesIconAndGap()
        {
            var segments = new List<CoinSegmentMath.CurrencySegmentSpec>
            {
                new CoinSegmentMath.CurrencySegmentSpec { IconUrl = "spirit-shard.png", Text = "0", TextWidth = 0 }
            };

            int expected = LabelIconGap + IconSize;
            Assert.Equal(expected, CoinSegmentMath.TotalCurrencySegmentsWidth(segments));
        }

        [Fact]
        public void TotalCurrencySegmentsWidth_MatchesTotalCoinSegmentsWidth_ForEquivalentInput()
        {
            // Both methods lay out the identical "label, gap, icon, gap"
            // geometry off the same CoinIconSize/CoinLabelIconGap/
            // CoinSegmentGap constants (the coin invariant, shared between
            // coin and currency rendering) - for the same text widths they
            // must agree exactly, even though TotalCoinSegmentsWidth sums
            // inline while TotalCurrencySegmentsWidth delegates to
            // ShoppingColumnMath.SegmentRunWidth.
            var coinSegments = new List<CoinSegmentMath.CoinSegmentSpec>
            {
                new CoinSegmentMath.CoinSegmentSpec { AssetId = 156904, Text = "12", TextWidth = 16 },
                new CoinSegmentMath.CoinSegmentSpec { AssetId = 156907, Text = "34", TextWidth = 18 }
            };
            var currencySegments = new List<CoinSegmentMath.CurrencySegmentSpec>
            {
                new CoinSegmentMath.CurrencySegmentSpec { IconUrl = "a.png", Text = "12", TextWidth = 16 },
                new CoinSegmentMath.CurrencySegmentSpec { IconUrl = "b.png", Text = "34", TextWidth = 18 }
            };

            int coinWidth = CoinSegmentMath.TotalCoinSegmentsWidth(coinSegments);
            int currencyWidth = CoinSegmentMath.TotalCurrencySegmentsWidth(currencySegments);

            Assert.Equal(coinWidth, currencyWidth);
        }
    }
}
