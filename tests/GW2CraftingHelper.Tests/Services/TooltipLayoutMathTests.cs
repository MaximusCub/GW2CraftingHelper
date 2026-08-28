using System;
using System.Linq;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // The rich tooltip surface's arithmetic. The placement half is the
    // interesting one: it exists because BlishHUD 1.3.0's own
    // Tooltip.UpdateTooltipPosition clamps neither the bottom edge nor a
    // negative X (measured - see KNOWN-ISSUES #41),
    // so these tests pin the four-edge guarantee the module now makes.
    public class TooltipLayoutMathTests
    {
        // Fixed-width "font": every character is 10px, so a wrap budget in
        // pixels is a character count times ten and the expected breaks can
        // be written out by hand.
        private static readonly Func<string, int> TenPxPerChar = s => (s?.Length ?? 0) * 10;

        // A coin run is one atomic block - width does not depend on the
        // amount's digits here, which keeps the atomicity assertions about
        // atomicity rather than about arithmetic.
        private static readonly Func<long, int> FixedCoinWidth = _ => 100;

        private static TooltipLayoutMath.Layout Layout(TooltipContent content, int maxWidth)
        {
            return TooltipLayoutMath.LayoutContent(content, maxWidth, 20, TenPxPerChar, FixedCoinWidth);
        }

        private static string RowText(TooltipLayoutMath.LaidOutRow row)
        {
            return string.Concat(row.Spans.Select(s => s.Span.Text));
        }

        // --- Row breaking ---
        [Fact]
        public void LayoutContent_ShortLine_IsOneRowAtOffsetZero()
        {
            var layout = Layout(TooltipContent.FromText("abc"), 500);

            Assert.Single(layout.Rows);
            Assert.Equal(0, layout.Rows[0].Spans[0].X);
            Assert.Equal(30, layout.Rows[0].Width);
            Assert.Equal(30, layout.Width);
        }

        [Fact]
        public void LayoutContent_HeightIsRowCountTimesRowHeight()
        {
            var layout = Layout(TooltipContent.FromText("one\ntwo\nthree"), 500);

            Assert.Equal(3, layout.Rows.Count);
            Assert.Equal(60, layout.Height);
        }

        [Fact]
        public void LayoutContent_BlankSeparatorLine_StillOccupiesARow()
        {
            var layout = Layout(TooltipContent.FromText("head\n\ntail"), 500);

            Assert.Equal(3, layout.Rows.Count);
            Assert.Empty(layout.Rows[1].Spans);
            Assert.Equal(0, layout.Rows[1].Width);
            Assert.Equal(60, layout.Height);
        }

        [Fact]
        public void LayoutContent_OverWideProse_WrapsAtWordBoundaries()
        {
            var layout = Layout(TooltipContent.FromText("aaa bbb ccc ddd"), 70);

            Assert.Equal(2, layout.Rows.Count);
            Assert.Equal("aaa bbb", RowText(layout.Rows[0]));
            Assert.Equal("ccc ddd", RowText(layout.Rows[1]));
            Assert.True(layout.Rows.All(r => r.Width <= 70));
        }

        [Fact]
        public void LayoutContent_UnbreakableToken_IsHardSplitNotOverflowed()
        {
            // The difference from Blish's own wrapper, which splits on
            // spaces only and lets an over-long token run past its cap.
            var layout = Layout(TooltipContent.FromText(new string('x', 12)), 50);

            Assert.True(layout.Rows.Count > 1);
            Assert.True(layout.Width <= 50);
            Assert.Equal(new string('x', 12), string.Concat(layout.Rows.Select(RowText)));
        }

        // --- Coin runs ---
        [Fact]
        public void LayoutContent_CoinSpan_KeepsItsCopperValueAndSitsAfterItsLabel()
        {
            var content = new TooltipContentBuilder()
                .Text("Cost: ").Coin(12345, "1g 23s 45c").Build();

            var layout = Layout(content, 500);

            Assert.Single(layout.Rows);
            Assert.Equal("Cost: 1g 23s 45c", RowText(layout.Rows[0]));
            var coin = layout.Rows[0].Spans.Last();
            Assert.True(coin.Span.IsCoin);
            Assert.Equal(12345, coin.Span.CoinCopper);
            // Placed immediately after the label - including the label's
            // trailing space, which the wrapper drops and the layout
            // restores - and the row is as wide as label + coin run.
            Assert.Equal(60, coin.X);
            Assert.Equal(100, coin.Width);
            Assert.Equal(160, layout.Rows[0].Width);
        }

        [Fact]
        public void LayoutContent_CoinRunThatWouldOverflow_MovesToTheNextRowWhole()
        {
            // Half a coin run is not a number, so a coin span never splits.
            var content = new TooltipContentBuilder()
                .Text("Cost: ").Coin(12345, "1g 23s 45c").Build();

            var layout = Layout(content, 120);

            Assert.Equal(2, layout.Rows.Count);
            Assert.Equal("Cost: ", RowText(layout.Rows[0]));
            Assert.Single(layout.Rows[1].Spans);
            Assert.True(layout.Rows[1].Spans[0].Span.IsCoin);
            Assert.Equal(0, layout.Rows[1].Spans[0].X);
        }

        [Fact]
        public void LayoutContent_CoinWiderThanTheWholeRow_StillRendersOnItsOwnRow()
        {
            var content = new TooltipContentBuilder().Text("C ").Coin(1, "1c").Build();

            var layout = Layout(content, 60);

            Assert.Equal(2, layout.Rows.Count);
            Assert.True(layout.Rows[1].Spans[0].Span.IsCoin);
        }

        [Fact]
        public void LayoutContent_ProseAfterACoin_WrapsAgainstWhatIsLeftOfTheRow()
        {
            // The reason LayoutContent passes a first-line budget rather
            // than wrapping every span from x=0: the suffix has to know a
            // coin run is already sitting on this row.
            var content = new TooltipContentBuilder()
                .Text("More expensive (").Coin(700, "7s 0c").Text(" more)").Build();

            var layout = Layout(content, 200);

            Assert.Equal(2, layout.Rows.Count);
            Assert.Equal("More expensive (", RowText(layout.Rows[0]));
            Assert.True(layout.Rows[1].Spans[0].Span.IsCoin);
            Assert.Equal(" more)", layout.Rows[1].Spans[1].Span.Text);
            Assert.Equal(100, layout.Rows[1].Spans[1].X);
        }

        [Fact]
        public void LayoutContent_EmptyContent_IsZeroSized()
        {
            var layout = Layout(TooltipContent.Empty, 500);

            Assert.Empty(layout.Rows);
            Assert.Equal(0, layout.Width);
            Assert.Equal(0, layout.Height);
        }

        [Fact]
        public void LayoutContent_NullContent_IsZeroSized()
        {
            Assert.Empty(Layout(null, 500).Rows);
        }

        [Fact]
        public void LayoutContent_NullMeasure_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                TooltipLayoutMath.LayoutContent(TooltipContent.FromText("a"), 100, 20, null, FixedCoinWidth));
            Assert.Throws<ArgumentNullException>(() =>
                TooltipLayoutMath.LayoutContent(TooltipContent.FromText("a"), 100, 20, TenPxPerChar, null));
        }

        // --- Max content width ---
        [Fact]
        public void MaxContentWidth_RoomySpriteScreen_UsesBlishsOwnPreferredCap()
        {
            Assert.Equal(
                TooltipLayoutMath.PreferredMaxContentWidth,
                TooltipLayoutMath.MaxContentWidth(1920, 10));
        }

        [Fact]
        public void MaxContentWidth_NarrowSpriteScreen_NarrowsBelowTheFixedCap()
        {
            // The part Blish's hard 500 cannot do: it knows nothing about
            // the screen it is drawn on.
            int width = TooltipLayoutMath.MaxContentWidth(400, 10);

            Assert.Equal(400 - 8 - 10, width);
            Assert.True(width < TooltipLayoutMath.PreferredMaxContentWidth);
        }

        [Fact]
        public void MaxContentWidth_AbsurdlyNarrowScreen_StopsAtTheFloor()
        {
            Assert.Equal(TooltipLayoutMath.MinContentWidth, TooltipLayoutMath.MaxContentWidth(60, 10));
        }

        // --- The item tooltip's live-derived wrap cap ---

        // Menomonia 14 exactly as the tooltip surface gets it: glyph
        // metrics read out of the shipped
        // Content/fonts/menomonia/menomonia-14-regular.xnb for the
        // characters the A/B strings use, plus Blish's global
        // LetterSpacing = -1. Measured the way MonoGame.Extended's
        // BitmapFont.MeasureString does - the pen advances by
        // XAdvance + LetterSpacing and the reported width is the rightmost
        // glyph ink - so a wrap asserted here is the wrap the module
        // renders. Every listed glyph has ink, so no zero-width guard is
        // needed.
        private const string Menomonia14Chars = " .:AFMTabcdefghilmnorstuwy";

        private static readonly int[] Menomonia14Advance = new[]
        {
            6, 3, 3, 9, 8, 13, 10, 8, 8, 8, 8, 8, 6,
            8, 8, 4, 4, 13, 8, 8, 5, 7, 6, 8, 13, 8,
        };

        private static readonly int[] Menomonia14XOffset = new[]
        {
            -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        };

        private static readonly int[] Menomonia14Ink = new[]
        {
            5, 5, 5, 11, 10, 15, 12, 10, 10, 10, 10, 10, 8,
            10, 10, 5, 6, 14, 10, 10, 7, 9, 8, 10, 15, 10,
        };

        private const int BlishLetterSpacing = -1;

        private static int MeasureMenomonia14(string text)
        {
            int pen = 0;
            int width = 0;
            foreach (char c in text ?? string.Empty)
            {
                int i = Menomonia14Chars.IndexOf(c);
                if (i < 0)
                {
                    throw new ArgumentException(
                        "No metric captured for '" + c + "'.", nameof(text));
                }

                int right = pen + Menomonia14XOffset[i] + Menomonia14Ink[i];
                if (right > width)
                {
                    width = right;
                }

                pen += Menomonia14Advance[i] + BlishLetterSpacing;
            }

            return width;
        }

        [Fact]
        public void ItemTooltipWrapCap_SitsInsideTheBracketTheLiveCapturesLeave()
        {
            // Every live capture that wraps a paragraph pins the cap from
            // both sides: at least the width of the line the game KEPT
            // whole, below that line plus the word it PUSHED down. Through
            // this face the corpus reads (kept / pushed): Gift of Twilight
            // 282/338, eyes-of-kormir 313/366 and 315/352,
            // heart-of-destroyer 293/362 and 326/387, plus Gift of
            // Twilight's unwrapped 317. Intersection [326, 338).
            Assert.InRange(TooltipLayoutMath.ItemTooltipMaxContentWidth, 326, 337);

            // The metric arrays are indexed by position in the character
            // string; a length drift would measure silently wrong rather
            // than throw.
            Assert.Equal(Menomonia14Chars.Length, Menomonia14Advance.Length);
            Assert.Equal(Menomonia14Chars.Length, Menomonia14XOffset.Length);
            Assert.Equal(Menomonia14Chars.Length, Menomonia14Ink.Length);

            // The two numbers the A/B turns on, so a font or metric change
            // that moved them could not silently keep the cap valid.
            Assert.Equal(
                282, MeasureMenomonia14("A gift used to create the legendary greatsword"));
            Assert.Equal(
                338, MeasureMenomonia14("A gift used to create the legendary greatsword Twilight."));
            Assert.Equal(
                317, MeasureMenomonia14("Made by combining these items in the Mystic Forge:"));
        }

        [Fact]
        public void ItemTooltipWrapCap_BreaksGiftOfTwilightWhereTheGameDoes()
        {
            // The 2026-08-27 owner A/B: item 19648 hovered in the module
            // and in the live game. The game wrapped its description after
            // "greatsword" and kept the Mystic Forge line whole; at the
            // former 350 the module fitted the whole description on one
            // line, which is the discrepancy the capture pair showed.
            var content = TooltipContent.FromText(
                "A gift used to create the legendary greatsword Twilight.\n" +
                "Made by combining these items in the Mystic Forge:");

            var layout = TooltipLayoutMath.LayoutContent(
                content,
                TooltipLayoutMath.MaxContentWidth(
                    1920, 10, TooltipLayoutMath.ItemTooltipMaxContentWidth),
                18,
                MeasureMenomonia14,
                _ => 0);

            Assert.Equal(
                new[]
                {
                    "A gift used to create the legendary greatsword",
                    "Twilight.",
                    "Made by combining these items in the Mystic Forge:",
                },
                layout.Rows.Select(RowText).ToArray());

            // Shrink-wrap to the widest laid-out line, not to the cap -
            // the sizing rule the live corpus confirms.
            Assert.Equal(317, layout.Width);
        }

        // --- Placement (the four-edge clamp) ---
        [Fact]
        public void Place_RoomAboveTheCursor_PrefersAbove()
        {
            // Blish's own preference, kept.
            TooltipLayoutMath.Place(600, 500, 200, 100, 1920, 1080, out int x, out int y);

            Assert.Equal(600, x);
            Assert.Equal(500 - TooltipLayoutMath.CursorGapAbove - 100, y);
        }

        [Fact]
        public void Place_NoRoomAbove_GoesBelowWithTheCursorGap()
        {
            TooltipLayoutMath.Place(600, 40, 200, 100, 1920, 1080, out _, out int y);

            Assert.Equal(40 + TooltipLayoutMath.CursorGapBelow, y);
        }

        [Fact]
        public void Place_TallTooltipNearTheBottom_IsClampedInsteadOfRunningOffScreen()
        {
            // The measured Blish defect: no room above, so it places the
            // tooltip 36px BELOW the cursor and never clamps the bottom.
            int mouseY = 300;
            int height = 900;
            TooltipLayoutMath.Place(600, mouseY, 200, height, 1920, 1080, out _, out int y);

            Assert.True(y + height <= 1080 - TooltipLayoutMath.ScreenEdgeMargin);
            Assert.True(y >= TooltipLayoutMath.ScreenEdgeMargin);
            // Blish would have produced this, off the bottom edge.
            Assert.NotEqual(mouseY + TooltipLayoutMath.CursorGapBelow, y);
        }

        [Fact]
        public void Place_NearTheRightEdge_FlipsToTheCursorsLeft()
        {
            TooltipLayoutMath.Place(1900, 500, 200, 100, 1920, 1080, out int x, out _);

            Assert.Equal(1700, x);
            Assert.True(x + 200 <= 1920 - TooltipLayoutMath.ScreenEdgeMargin);
        }

        [Fact]
        public void Place_WideTooltipNearBothHorizontalEdges_NeverGoesNegative()
        {
            // The measured Blish defect: the left shift is not clamped to
            // >= 0, so a tooltip wider than the cursor's X lands off the
            // left edge of the screen.
            TooltipLayoutMath.Place(120, 500, 600, 100, 700, 1080, out int x, out _);

            Assert.True(x >= TooltipLayoutMath.ScreenEdgeMargin);
            Assert.True(x + 600 <= 700 - TooltipLayoutMath.ScreenEdgeMargin);
        }

        [Fact]
        public void Place_BoxLargerThanTheScreen_PinsToTheNearEdgeSoItsStartStaysVisible()
        {
            TooltipLayoutMath.Place(400, 400, 2000, 2000, 800, 600, out int x, out int y);

            Assert.Equal(TooltipLayoutMath.ScreenEdgeMargin, x);
            Assert.Equal(TooltipLayoutMath.ScreenEdgeMargin, y);
        }

        [Fact]
        public void Place_NeitherSideFits_TakesTheRoomierSide()
        {
            // Cursor high on the screen, tooltip too tall for either gap:
            // below has more room, so the box goes as low as it can.
            TooltipLayoutMath.Place(600, 100, 200, 1000, 1920, 1080, out _, out int y);

            Assert.Equal(1080 - TooltipLayoutMath.ScreenEdgeMargin - 1000, y);
        }

        // --- Header rows and per-row heights (G11, G21) ---
        [Fact]
        public void HeaderRow_IsIconTall_IndentedPastTheIcon_AndCarriesItsIcon()
        {
            var content = TooltipContent.FromLines(new[]
            {
                TooltipContent.HeaderLine("icon.png", "Bolt", "Legendary"),
                TooltipContent.TextLine("Weapon Strength: 950 - 1,050"),
            });

            var layout = TooltipLayoutMath.LayoutContent(
                content, 500, 20, TenPxPerChar, FixedCoinWidth,
                coinRowHeight: 20, headerRowHeight: 34, headerIndent: 39);

            var header = layout.Rows[0];
            Assert.Equal(34, header.Height);
            Assert.Equal(0, header.Y);
            Assert.Equal("icon.png", header.IconUrl);
            Assert.Equal(39, header.Spans[0].X);

            // The prose row under it is one line pitch, not icon-tall, and
            // starts where the header ends.
            Assert.Equal(20, layout.Rows[1].Height);
            Assert.Equal(34, layout.Rows[1].Y);
            Assert.Null(layout.Rows[1].IconUrl);
            Assert.Equal(0, layout.Rows[1].Spans[0].X);
            Assert.Equal(54, layout.Height);
        }

        [Fact]
        public void EffectRows_AreIndented_OneLinePitchTall_WithTheIconOnTheFirstRowOnly()
        {
            // The consumable effect block (live3 soul-pastries /
            // candy-corn, 2026-08-26): every row of the block is indented
            // past the inline icon, rows stay one line pitch tall, and the
            // icon rides the first row only - a wrapped continuation
            // included.
            var content = new TooltipContentBuilder()
                .EffectBlock("apple.png", "aaaa bbbb\ncc", TooltipSpanRole.Muted)
                .Build();

            var layout = TooltipLayoutMath.LayoutContent(
                content, 500, 20, TenPxPerChar, FixedCoinWidth, effectIndent: 31);

            Assert.Equal(2, layout.Rows.Count);
            Assert.All(layout.Rows, r => Assert.Equal(TooltipLineKind.Effect, r.Kind));
            Assert.All(layout.Rows, r => Assert.Equal(20, r.Height));
            Assert.Equal("apple.png", layout.Rows[0].IconUrl);
            Assert.Null(layout.Rows[1].IconUrl);
            Assert.Equal(31, layout.Rows[0].Spans[0].X);
            Assert.Equal(31, layout.Rows[1].Spans[0].X);
        }

        [Fact]
        public void AWrappedEffectLineKeepsItsIndentKindAndSingleIcon()
        {
            // 31px indent leaves 90px of a 121px budget for text at 10px a
            // character: "aaaa bbbb" (90) fits, "cccc" wraps, and the
            // wrapped row is still an indented Effect row with no second
            // icon.
            var content = new TooltipContentBuilder()
                .EffectBlock("apple.png", "aaaa bbbb cccc", TooltipSpanRole.Muted)
                .Build();

            var layout = TooltipLayoutMath.LayoutContent(
                content, 121, 20, TenPxPerChar, FixedCoinWidth, effectIndent: 31);

            Assert.Equal(2, layout.Rows.Count);
            Assert.Equal("aaaa bbbb", RowText(layout.Rows[0]));
            Assert.Equal("cccc", RowText(layout.Rows[1]));
            Assert.Equal(TooltipLineKind.Effect, layout.Rows[1].Kind);
            Assert.Equal(31, layout.Rows[1].Spans[0].X);
            Assert.Null(layout.Rows[1].IconUrl);
        }

        [Fact]
        public void AWrappedHeaderNameStaysInTheNameColumnAndDrawsOneIcon()
        {
            var content = TooltipContent.FromLines(new[]
            {
                TooltipContent.HeaderLine("icon.png", "aaa bbb ccc", "Exotic"),
            });

            // 39px indent + a 70px budget for the name itself.
            var layout = TooltipLayoutMath.LayoutContent(
                content, 109, 20, TenPxPerChar, FixedCoinWidth,
                headerRowHeight: 34, headerIndent: 39);

            Assert.Equal(2, layout.Rows.Count);
            Assert.All(layout.Rows, r => Assert.Equal(39, r.Spans[0].X));
            Assert.All(layout.Rows, r => Assert.True(r.Width <= 109));
            Assert.Equal("icon.png", layout.Rows[0].IconUrl);
            Assert.Null(layout.Rows[1].IconUrl);

            // Only the icon-bearing first row is icon-tall; a wrapped
            // continuation is an ordinary text row at the line pitch, so
            // the box does not grow icon-height per wrapped name row.
            Assert.Equal(34, layout.Rows[0].Height);
            Assert.Equal(20, layout.Rows[1].Height);
            Assert.Equal(54, layout.Height);
        }

        [Fact]
        public void AHeaderWithNoIconUrlStillHasAnIconToDrawInTheColumnItReserves()
        {
            // An item whose /v2/items response carries no "icon" reaches
            // here with null. The row reserves the name indent either way,
            // so the url has to survive as EMPTY - which the surface draws
            // as the module's neutral empty-slot square - rather than as
            // null, which draws nothing and leaves the name floating over
            // the reserved column.
            var content = TooltipContent.FromLines(new[]
            {
                TooltipContent.HeaderLine(null, "Iconless Thing", "Basic"),
                TooltipContent.TextLine("Basic"),
            });

            Assert.Equal("", content.Lines[0].IconUrl);

            var layout = TooltipLayoutMath.LayoutContent(
                content, 500, 20, TenPxPerChar, FixedCoinWidth,
                headerRowHeight: 34, headerIndent: 39);

            Assert.Equal("", layout.Rows[0].IconUrl);
            Assert.Equal(39, layout.Rows[0].Spans[0].X);
            Assert.Null(layout.Rows[1].IconUrl);
        }

        [Fact]
        public void OnlyTheCoinRowTakesTheCoinHeight()
        {
            var content = TooltipContent.FromLines(new[]
            {
                TooltipContent.TextLine("prose"),
                TooltipContent.Line(TooltipSpan.FromCoin(240, "2s 40c")),
            });

            var layout = TooltipLayoutMath.LayoutContent(
                content, 500, 20, TenPxPerChar, FixedCoinWidth, coinRowHeight: 26);

            Assert.Equal(20, layout.Rows[0].Height);
            Assert.Equal(26, layout.Rows[1].Height);
            Assert.Equal(46, layout.Height);
        }

        [Fact]
        public void ACoinRunPushedOntoTheNextRow_TakesTheHeightWithIt()
        {
            // "aaaaa" is 50px and the coin run 100px, so the run cannot
            // share the 120px row and moves down whole.
            var content = TooltipContent.FromLines(new[]
            {
                TooltipContent.Line(
                    TooltipSpan.FromText("aaaaa"),
                    TooltipSpan.FromCoin(240, "2s 40c")),
            });

            var layout = TooltipLayoutMath.LayoutContent(
                content, 120, 20, TenPxPerChar, FixedCoinWidth, coinRowHeight: 26);

            Assert.Equal(2, layout.Rows.Count);
            Assert.Equal(20, layout.Rows[0].Height);
            Assert.Equal(26, layout.Rows[1].Height);
        }

        [Fact]
        public void HeaderPlainTextIsStillJustTheName()
        {
            var content = TooltipContent.FromLines(new[]
            {
                TooltipContent.HeaderLine("icon.png", "Bolt", "Legendary"),
            });

            Assert.Equal("Bolt", content.ToPlainText());
            Assert.Equal(TooltipSpanRole.Rarity, content.Lines[0].Spans[0].Role);
            Assert.Equal(TooltipLineKind.Header, content.Lines[0].Kind);
        }

        [Fact]
        public void ClampAxis_KeepsBothEdgesInsideTheMargin()
        {
            Assert.Equal(TooltipLayoutMath.ScreenEdgeMargin, TooltipLayoutMath.ClampAxis(-50, 100, 1000));
            Assert.Equal(1000 - TooltipLayoutMath.ScreenEdgeMargin - 100, TooltipLayoutMath.ClampAxis(5000, 100, 1000));
            Assert.Equal(300, TooltipLayoutMath.ClampAxis(300, 100, 1000));
        }

        // The canvas art source is a 1:1 crop: any box the module can
        // actually produce sources exactly its own size. 942 is the real
        // "tooltip" texture's edge (decompiled Blish 1.3.0), and (3,4) is
        // the crop origin the live client provably uses (fidelity-audit
        // 8.4: live2/k-2 interior vs texture r=0.983 at that alignment).
        [Fact]
        public void CanvasArtSource_IsTheBoxSizeForEveryReachableBox()
        {
            Assert.Equal(412, TooltipLayoutMath.CanvasArtSourceLength(
                412, 942, TooltipLayoutMath.CanvasArtSourceX));
            Assert.Equal(600, TooltipLayoutMath.CanvasArtSourceLength(
                600, 942, TooltipLayoutMath.CanvasArtSourceY));
        }

        [Fact]
        public void CanvasArtSource_ClampsToWhatTheTextureHasPastTheOrigin()
        {
            Assert.Equal(939, TooltipLayoutMath.CanvasArtSourceLength(
                1000, 942, TooltipLayoutMath.CanvasArtSourceX));
            Assert.Equal(938, TooltipLayoutMath.CanvasArtSourceLength(
                1000, 942, TooltipLayoutMath.CanvasArtSourceY));
            // exact fit is not a clamp
            Assert.Equal(939, TooltipLayoutMath.CanvasArtSourceLength(
                939, 942, TooltipLayoutMath.CanvasArtSourceX));
        }

        [Fact]
        public void CanvasArtSource_DegenerateBoxesAndTexturesSourceNothing()
        {
            Assert.Equal(0, TooltipLayoutMath.CanvasArtSourceLength(0, 942, 3));
            Assert.Equal(0, TooltipLayoutMath.CanvasArtSourceLength(-5, 942, 3));
            // texture no larger than the crop origin has nothing to give
            Assert.Equal(0, TooltipLayoutMath.CanvasArtSourceLength(100, 3, 3));
            Assert.Equal(0, TooltipLayoutMath.CanvasArtSourceLength(100, 0, 4));
        }
    }
}
