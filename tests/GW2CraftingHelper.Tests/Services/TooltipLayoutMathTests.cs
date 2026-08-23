using System;
using System.Linq;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // The rich tooltip surface's arithmetic. The placement half is the
    // interesting one: it exists because BlishHUD 1.3.0's own
    // Tooltip.UpdateTooltipPosition clamps neither the bottom edge nor a
    // negative X (measured - see docs/KNOWN-ISSUES.md, "Tooltip facility"),
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

        // --- Placement (the four-edge clamp) ---

        [Fact]
        public void Place_RoomAboveTheCursor_PrefersAbove()
        {
            // Blish's own preference, kept.
            TooltipLayoutMath.Place(600, 500, 200, 100, 1920, 1080, out int x, out int y);

            Assert.Equal(600, x);
            Assert.Equal(500 - TooltipLayoutMath.CursorGap - 100, y);
        }

        [Fact]
        public void Place_NoRoomAbove_GoesBelowWithTheCursorGap()
        {
            TooltipLayoutMath.Place(600, 40, 200, 100, 1920, 1080, out _, out int y);

            Assert.Equal(40 + TooltipLayoutMath.CursorGap, y);
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
            Assert.NotEqual(mouseY + TooltipLayoutMath.CursorGap, y);
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

        [Fact]
        public void ClampAxis_KeepsBothEdgesInsideTheMargin()
        {
            Assert.Equal(TooltipLayoutMath.ScreenEdgeMargin, TooltipLayoutMath.ClampAxis(-50, 100, 1000));
            Assert.Equal(1000 - TooltipLayoutMath.ScreenEdgeMargin - 100, TooltipLayoutMath.ClampAxis(5000, 100, 1000));
            Assert.Equal(300, TooltipLayoutMath.ClampAxis(300, 100, 1000));
        }
    }
}
