using System.Collections.Generic;
using GW2CraftingHelper.Views.Rendering;
using Xunit;

namespace GW2CraftingHelper.Tests.Views.Rendering
{
    // M38 WP-21 findings fix: TotalCoinSegmentsWidth/TotalCurrencySegmentsWidth
    // take only plain data structs (CoinSegmentSpec/CurrencySegmentSpec - no
    // XNA/Blish types) and are pure integer arithmetic, so they are
    // Blish-free and testable exactly like ShoppingColumnMath. Expected
    // values are computed against the real CoinLabelIconGap/CoinIconSize/
    // CoinSegmentGap constants rather than re-deriving the formula, so a
    // future constant change updates the assertions' inputs, not just the
    // production code, catching drift.
    public class CoinCurrencyRendererTests
    {
        private const int IconSize = CoinCurrencyRenderer.CoinIconSize;
        private const int LabelIconGap = CoinCurrencyRenderer.CoinLabelIconGap;
        private const int SegmentGap = CoinCurrencyRenderer.CoinSegmentGap;

        // --- TotalCoinSegmentsWidth ---

        [Fact]
        public void TotalCoinSegmentsWidth_Empty_ReturnsZero()
        {
            var segments = new List<CoinCurrencyRenderer.CoinSegmentSpec>();

            Assert.Equal(0, CoinCurrencyRenderer.TotalCoinSegmentsWidth(segments));
        }

        [Fact]
        public void TotalCoinSegmentsWidth_SingleSegment_NoTrailingGap()
        {
            // One segment: textWidth + labelIconGap + iconSize, no trailing
            // segmentGap since there is nothing after it.
            var segments = new List<CoinCurrencyRenderer.CoinSegmentSpec>
            {
                new CoinCurrencyRenderer.CoinSegmentSpec { AssetId = 156902, Text = "56", TextWidth = 18 }
            };

            int expected = 18 + LabelIconGap + IconSize;
            Assert.Equal(expected, CoinCurrencyRenderer.TotalCoinSegmentsWidth(segments));
        }

        [Fact]
        public void TotalCoinSegmentsWidth_MultipleSegments_GapBetweenNotAfter()
        {
            // Gold, silver, copper: three segments, one segmentGap between
            // each pair, none trailing after the last.
            var segments = new List<CoinCurrencyRenderer.CoinSegmentSpec>
            {
                new CoinCurrencyRenderer.CoinSegmentSpec { AssetId = 156904, Text = "12", TextWidth = 16 },
                new CoinCurrencyRenderer.CoinSegmentSpec { AssetId = 156907, Text = "34", TextWidth = 18 },
                new CoinCurrencyRenderer.CoinSegmentSpec { AssetId = 156902, Text = "56", TextWidth = 18 }
            };

            int expected =
                (16 + LabelIconGap + IconSize) +
                (18 + LabelIconGap + IconSize) +
                (18 + LabelIconGap + IconSize) +
                2 * SegmentGap;
            Assert.Equal(expected, CoinCurrencyRenderer.TotalCoinSegmentsWidth(segments));
        }

        [Fact]
        public void TotalCoinSegmentsWidth_ZeroTextWidthSegment_StillIncludesIconAndGap()
        {
            // A degenerate zero-width measured string (e.g. a single glyph
            // BitmapFont.MeasureString rounds down to 0) must not collapse
            // the segment away - the icon and its gap still occupy space.
            var segments = new List<CoinCurrencyRenderer.CoinSegmentSpec>
            {
                new CoinCurrencyRenderer.CoinSegmentSpec { AssetId = 156902, Text = "0", TextWidth = 0 }
            };

            int expected = LabelIconGap + IconSize;
            Assert.Equal(expected, CoinCurrencyRenderer.TotalCoinSegmentsWidth(segments));
        }

        // --- TotalCurrencySegmentsWidth ---

        [Fact]
        public void TotalCurrencySegmentsWidth_Empty_ReturnsZero()
        {
            var segments = new List<CoinCurrencyRenderer.CurrencySegmentSpec>();

            Assert.Equal(0, CoinCurrencyRenderer.TotalCurrencySegmentsWidth(segments));
        }

        [Fact]
        public void TotalCurrencySegmentsWidth_SingleSegment_NoTrailingGap()
        {
            var segments = new List<CoinCurrencyRenderer.CurrencySegmentSpec>
            {
                new CoinCurrencyRenderer.CurrencySegmentSpec { IconUrl = "spirit-shard.png", Text = "5", TextWidth = 12 }
            };

            int expected = 12 + LabelIconGap + IconSize;
            Assert.Equal(expected, CoinCurrencyRenderer.TotalCurrencySegmentsWidth(segments));
        }

        [Fact]
        public void TotalCurrencySegmentsWidth_MultipleSegments_GapBetweenNotAfter()
        {
            var segments = new List<CoinCurrencyRenderer.CurrencySegmentSpec>
            {
                new CoinCurrencyRenderer.CurrencySegmentSpec { IconUrl = "karma.png", Text = "1200", TextWidth = 30 },
                new CoinCurrencyRenderer.CurrencySegmentSpec { IconUrl = "spirit-shard.png", Text = "3", TextWidth = 10 }
            };

            int expected =
                (30 + LabelIconGap + IconSize) +
                (10 + LabelIconGap + IconSize) +
                SegmentGap;
            Assert.Equal(expected, CoinCurrencyRenderer.TotalCurrencySegmentsWidth(segments));
        }

        [Fact]
        public void TotalCurrencySegmentsWidth_ZeroTextWidthSegment_StillIncludesIconAndGap()
        {
            var segments = new List<CoinCurrencyRenderer.CurrencySegmentSpec>
            {
                new CoinCurrencyRenderer.CurrencySegmentSpec { IconUrl = "spirit-shard.png", Text = "0", TextWidth = 0 }
            };

            int expected = LabelIconGap + IconSize;
            Assert.Equal(expected, CoinCurrencyRenderer.TotalCurrencySegmentsWidth(segments));
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
            var coinSegments = new List<CoinCurrencyRenderer.CoinSegmentSpec>
            {
                new CoinCurrencyRenderer.CoinSegmentSpec { AssetId = 156904, Text = "12", TextWidth = 16 },
                new CoinCurrencyRenderer.CoinSegmentSpec { AssetId = 156907, Text = "34", TextWidth = 18 }
            };
            var currencySegments = new List<CoinCurrencyRenderer.CurrencySegmentSpec>
            {
                new CoinCurrencyRenderer.CurrencySegmentSpec { IconUrl = "a.png", Text = "12", TextWidth = 16 },
                new CoinCurrencyRenderer.CurrencySegmentSpec { IconUrl = "b.png", Text = "34", TextWidth = 18 }
            };

            int coinWidth = CoinCurrencyRenderer.TotalCoinSegmentsWidth(coinSegments);
            int currencyWidth = CoinCurrencyRenderer.TotalCurrencySegmentsWidth(currencySegments);

            Assert.Equal(coinWidth, currencyWidth);
        }
    }
}
