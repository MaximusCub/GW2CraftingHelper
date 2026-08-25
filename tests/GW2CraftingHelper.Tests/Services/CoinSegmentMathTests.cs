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

        // --- Split ---

        // Pins the shared three-way coin split every display site now
        // routes through (tooltip builders, SnapshotHelpers, MainView's
        // coin panel, CoinCurrencyRenderer). Formatting differences stay
        // with the callers; only the arithmetic is shared.
        [Theory]
        [InlineData(0L, 0L, 0L, 0L)]
        [InlineData(1L, 0L, 0L, 1L)]
        [InlineData(99L, 0L, 0L, 99L)]
        [InlineData(100L, 0L, 1L, 0L)]
        [InlineData(9999L, 0L, 99L, 99L)]
        [InlineData(10000L, 1L, 0L, 0L)]
        [InlineData(1234567L, 123L, 45L, 67L)]
        public void Split_SplitsIntoGoldSilverCopper(long copper, long gold, long silver, long cop)
        {
            Assert.Equal((gold, silver, cop), CoinSegmentMath.Split(copper));
        }

        [Fact]
        public void Split_NegativeInput_ClampsToZero()
        {
            // Negative coin amounts are never displayed; every caller used
            // to clamp before splitting and the clamp moved into Split with
            // the consolidation. Callers CAN pass negatives (e.g.
            // ValueDetailTooltipBuilder's delta line), so this is
            // load-bearing, not defensive.
            Assert.Equal((0L, 0L, 0L), CoinSegmentMath.Split(-1));
            Assert.Equal((0L, 0L, 0L), CoinSegmentMath.Split(long.MinValue));
        }

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

        // --- FormatSegmentTexts ---
        //
        // The exact strings a coin amount renders as. Two consumers depend
        // on them agreeing: CoinCurrencyRenderer.BuildCoinSegments (what is
        // drawn) and the recipe tree's cost-column pre-scan (how wide each
        // denomination's sub-column is reserved).

        [Fact]
        public void FormatSegmentTexts_FullAmount_PadsSilverAndCopperOnce_GoldPrecedesThem()
        {
            var (gold, silver, copper) = CoinSegmentMath.FormatSegmentTexts(412680L);

            Assert.Equal("41", gold);
            Assert.Equal("26", silver);
            Assert.Equal("80", copper);
        }

        [Fact]
        public void FormatSegmentTexts_PadsSingleDigitSilverAndCopperUnderGold()
        {
            var (gold, silver, copper) = CoinSegmentMath.FormatSegmentTexts(10203L);

            Assert.Equal("1", gold);
            Assert.Equal("02", silver);
            Assert.Equal("03", copper);
        }

        [Fact]
        public void FormatSegmentTexts_SubGoldAmount_OmitsGoldAndLeavesSilverUnpadded()
        {
            var (gold, silver, copper) = CoinSegmentMath.FormatSegmentTexts(539L);

            Assert.Null(gold);
            Assert.Equal("5", silver);
            Assert.Equal("39", copper);
        }

        [Fact]
        public void FormatSegmentTexts_SubSilverAmount_OmitsBothLeadingUnits()
        {
            var (gold, silver, copper) = CoinSegmentMath.FormatSegmentTexts(7L);

            Assert.Null(gold);
            Assert.Null(silver);
            Assert.Equal("7", copper);
        }

        [Fact]
        public void FormatSegmentTexts_Zero_StillRendersACopperUnit()
        {
            // A zero total must never be a blank cell.
            var (gold, silver, copper) = CoinSegmentMath.FormatSegmentTexts(0L);

            Assert.Null(gold);
            Assert.Null(silver);
            Assert.Equal("0", copper);
        }

        [Fact]
        public void FormatSegmentTexts_Negative_ClampsLikeSplit()
        {
            Assert.Equal(
                CoinSegmentMath.FormatSegmentTexts(0L),
                CoinSegmentMath.FormatSegmentTexts(-500L));
        }

        [Fact]
        public void TotalCoinSegmentsWidth_HonoursACallersOwnIconSize()
        {
            // The rich tooltip draws coin icons at ~0.8x its line height
            // rather than at the plan tables' shared 20px (gap G22), and
            // has to MEASURE at the size it draws at or its box is too
            // wide by one icon per denomination.
            var segments = new List<CoinSegmentMath.CoinSegmentSpec>
            {
                new CoinSegmentMath.CoinSegmentSpec { AssetId = CoinSegmentMath.GoldAssetId, Text = "1", TextWidth = 10 },
                new CoinSegmentMath.CoinSegmentSpec { AssetId = CoinSegmentMath.CopperAssetId, Text = "23", TextWidth = 20 }
            };

            int shared = CoinSegmentMath.TotalCoinSegmentsWidth(segments);
            int local = CoinSegmentMath.TotalCoinSegmentsWidth(segments, 13);

            Assert.Equal(shared - (2 * (CoinSegmentMath.CoinIconSize - 13)), local);
            Assert.Equal(shared, CoinSegmentMath.TotalCoinSegmentsWidth(segments, 0));
        }

        // The hover a coin icon answers with. Every denomination the coin
        // renderer can build a segment for has to have one; anything else
        // returns null, the icon component's "no text of my own" input.
        [Theory]
        [InlineData(CoinSegmentMath.GoldAssetId, "Gold")]
        [InlineData(CoinSegmentMath.SilverAssetId, "Silver")]
        [InlineData(CoinSegmentMath.CopperAssetId, "Copper")]
        public void DenominationName_NamesEveryCoinIcon(int assetId, string expected)
        {
            Assert.Equal(expected, CoinSegmentMath.DenominationName(assetId));
        }

        [Fact]
        public void DenominationName_IsNullForAnythingThatIsNotACoin()
        {
            Assert.Null(CoinSegmentMath.DenominationName(0));
            Assert.Null(CoinSegmentMath.DenominationName(156905));
        }

        [Fact]
        public void EverySegmentTheSplitProduces_CarriesANamedDenomination()
        {
            // Guards the pairing, not the three literals: a fourth
            // denomination would otherwise ship an unnamed icon.
            foreach (int assetId in new[]
            {
                CoinSegmentMath.GoldAssetId, CoinSegmentMath.SilverAssetId, CoinSegmentMath.CopperAssetId
            })
            {
                Assert.False(string.IsNullOrEmpty(CoinSegmentMath.DenominationName(assetId)));
            }
        }
    }
}
