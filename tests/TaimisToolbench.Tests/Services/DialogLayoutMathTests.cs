using System;
using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // DialogLayoutMath is the Blish-free half of both popup dialogs' sizing.
    // Every test drives the real production entry points through the same
    // Func<string,int> measurement seam the views pass a BitmapFont through,
    // so the numbers below are arithmetic rather than font-dependent.
    public class DialogLayoutMathTests
    {
        // 10px per character, and a 20px line pitch - close enough to the
        // module's own Font16 that the shapes the tests assert are the
        // shapes the module gets.
        private static readonly Func<string, int> Fixed10 = s => (s ?? "").Length * 10;

        private const int Pitch = 20;

        // A 1920x1080 screen with the dialog shell's own insets already
        // removed - what the views hand Measure.
        private const int RoomyWidth = 1884;

        private const int RoomyHeight = 990;

        private static DialogLayoutMath.Layout Measure(
            string message, int confirmLabelWidth = 60, int cancelLabelWidth = 60,
            int titleWidth = 0, int maxWidth = RoomyWidth, int maxHeight = RoomyHeight)
        {
            return DialogLayoutMath.Measure(
                new[] { message }, Fixed10, Pitch, titleWidth,
                confirmLabelWidth, cancelLabelWidth, maxWidth, maxHeight);
        }

        // Ten nine-character words: 490px for five of them, 590 for six, so
        // the wrap points below are exact rather than approximate.
        private static string TenWords()
        {
            return string.Join(" ", Enumerable.Repeat(new string('a', 9), 10));
        }

        // --- Width ---
        [Fact]
        public void Measure_OneWordMessage_TakesTheMinimumWidthNotThePreferredOne()
        {
            var layout = Measure("Saved");

            Assert.Equal(DialogLayoutMath.MinContentWidth, layout.ContentWidth);
        }

        [Fact]
        public void Measure_LongSentence_StopsAtThePreferredMaximum()
        {
            // 400 characters is 4,000px of natural measure. The box must not
            // follow it: the ceiling is what keeps one sentence from
            // producing a dialog wider than the screen it sits on. 54
            // characters is exactly the ceiling, so that case pins it from
            // below as well.
            Assert.True(
                Measure(new string('a', 400)).ContentWidth
                    <= DialogLayoutMath.PreferredMaxContentWidth);
            Assert.Equal(
                DialogLayoutMath.PreferredMaxContentWidth,
                Measure(new string('a', 54)).ContentWidth);
        }

        [Fact]
        public void Measure_TwoLineMessage_NarrowsToTheBalancedWidth()
        {
            // Five words (490px) per line at the preferred 540 ceiling. The
            // balanced width is the narrowest that still fits five, so the
            // second line is as full as the first instead of being a stub.
            var layout = Measure(TenWords());

            Assert.Equal(490, layout.ContentWidth);
            Assert.Equal(2, layout.Blocks.Single().Lines.Count);
        }

        [Fact]
        public void Measure_NarrowingWouldAddALine_KeepsTheWiderWidth()
        {
            // One word narrower per line and the same text needs three, so
            // the balanced search must stop at 490 rather than at the floor.
            var atFloor = DialogLayoutMath.Measure(
                new[] { TenWords() }, Fixed10, Pitch, 0, 60, 60,
                DialogLayoutMath.MinContentWidth, RoomyHeight);

            Assert.Equal(3, atFloor.Blocks.Single().Lines.Count);
        }

        [Fact]
        public void Measure_LongTitle_WidensPastThePreferredMaximum()
        {
            // The title is drawn at a fixed indent with no alignment control,
            // so window width is the only thing that keeps it out of the
            // exit button.
            var layout = Measure("Short", titleWidth: 400);

            Assert.Equal(
                DialogLayoutMath.TitleTextIndent + 400 + DialogLayoutMath.TitleRightReserve,
                layout.ContentWidth);
        }

        // --- The button row ---
        [Fact]
        public void Measure_TwoButtons_CentresTheRowAtItsMeasuredWidths()
        {
            var layout = Measure("Short", confirmLabelWidth: 130, cancelLabelWidth: 70);

            int confirmWidth = 130 + DialogLayoutMath.ButtonSidePadding;
            int cancelWidth = 70 + DialogLayoutMath.ButtonSidePadding;
            int row = confirmWidth + DialogLayoutMath.ButtonGap + cancelWidth;

            Assert.Equal(confirmWidth, layout.ConfirmWidth);
            Assert.Equal(cancelWidth, layout.CancelWidth);
            Assert.Equal((layout.ContentWidth - row) / 2, layout.ConfirmX);
            Assert.Equal(layout.ConfirmX + confirmWidth + DialogLayoutMath.ButtonGap, layout.CancelX);
        }

        [Fact]
        public void Measure_ShortLabels_KeepTheirFloorWidths()
        {
            var layout = Measure("Short", confirmLabelWidth: 10, cancelLabelWidth: 10);

            Assert.Equal(DialogLayoutMath.MinConfirmButtonWidth, layout.ConfirmWidth);
            Assert.Equal(DialogLayoutMath.MinCancelButtonWidth, layout.CancelWidth);
        }

        [Fact]
        public void Measure_OneButton_LeavesNoSecondSeatAndCentresTheFirst()
        {
            var layout = Measure("Short", confirmLabelWidth: 20, cancelLabelWidth: -1);

            Assert.Equal(0, layout.CancelWidth);
            Assert.Equal(0, layout.CancelX);
            Assert.Equal((layout.ContentWidth - layout.ConfirmWidth) / 2, layout.ConfirmX);
        }

        [Fact]
        public void Measure_ButtonsWiderThanThePreferredWidth_WidenTheDialog()
        {
            var layout = Measure("Short", confirmLabelWidth: 300, cancelLabelWidth: 300);

            int row = layout.ConfirmWidth + DialogLayoutMath.ButtonGap + layout.CancelWidth;
            Assert.True(
                layout.ContentWidth >= row + (2 * DialogLayoutMath.ButtonRowSideMargin),
                "a dialog must never be narrower than its own buttons need");
            Assert.True(layout.ContentWidth > DialogLayoutMath.PreferredMaxContentWidth);
        }

        // --- Height ---
        [Fact]
        public void Measure_OneLine_IsShorterThanATwoLineDialog()
        {
            int one = Measure("Short").ContentHeight;
            int two = Measure(TenWords()).ContentHeight;

            Assert.Equal(Pitch, two - one);
            Assert.Equal(
                DialogLayoutMath.MessageTopMargin + Pitch + DialogLayoutMath.MessageToButtonGap
                    + DialogLayoutMath.ButtonHeight + DialogLayoutMath.ButtonBottomMargin,
                one);
        }

        [Fact]
        public void Measure_ExplicitNewlines_CountAsLinesAtAnyWidth()
        {
            var layout = Measure("one\ntwo\nthree");

            Assert.Equal(3, layout.Blocks.Single().Lines.Count);
            Assert.Equal(new[] { "one", "two", "three" }, layout.Blocks.Single().Lines);
            Assert.Equal(
                DialogLayoutMath.MessageTopMargin + (3 * Pitch) + DialogLayoutMath.MessageToButtonGap
                    + DialogLayoutMath.ButtonHeight + DialogLayoutMath.ButtonBottomMargin,
                layout.ContentHeight);
        }

        [Fact]
        public void Measure_ButtonRowSitsOnTheBottomPaddingOfTheContentBox()
        {
            var layout = Measure("one\ntwo\nthree");

            Assert.Equal(
                layout.ContentHeight - DialogLayoutMath.ButtonBottomMargin
                    - DialogLayoutMath.ButtonHeight,
                layout.ButtonY);
        }

        [Fact]
        public void Measure_SeveralParagraphs_AreSpacedAndStacked()
        {
            var layout = DialogLayoutMath.Measure(
                new[] { "one", "two", "three" }, Fixed10, Pitch, 0, 60, 60,
                RoomyWidth, RoomyHeight);

            Assert.Equal(3, layout.Blocks.Count);
            Assert.Equal(DialogLayoutMath.MessageTopMargin, layout.Blocks[0].Y);
            Assert.Equal(
                DialogLayoutMath.MessageTopMargin + Pitch + DialogLayoutMath.ParagraphGap,
                layout.Blocks[1].Y);
            Assert.Equal(
                layout.Blocks[1].Y + Pitch + DialogLayoutMath.ParagraphGap,
                layout.Blocks[2].Y);
        }

        // --- Clamping to the screen ---
        [Fact]
        public void Measure_ShortScreen_CapsTheLinesAndKeepsTheButtonsInside()
        {
            var layout = Measure("one\ntwo\nthree\nfour\nfive", maxHeight: 110);

            Assert.Equal(2, layout.Blocks.Single().Lines.Count);
            Assert.True(layout.Blocks.Single().Truncated, "dropped text owes the caller a tooltip");
            Assert.True(layout.ContentHeight <= 110);
            Assert.True(layout.ButtonY + DialogLayoutMath.ButtonHeight <= layout.ContentHeight);
        }

        [Fact]
        public void Measure_ScreenNarrowerThanTheButtons_LetsTheScreenWin()
        {
            var layout = Measure(
                "Short", confirmLabelWidth: 300, cancelLabelWidth: 300, maxWidth: 300);

            Assert.Equal(300, layout.ContentWidth);
        }

        [Fact]
        public void Measure_ScreenTooShortForEvenOneLine_StillPlacesTheButtons()
        {
            var layout = Measure("Short", maxHeight: 1);

            Assert.Single(layout.Blocks.Single().Lines);
            Assert.True(layout.ButtonY > 0);
        }

        [Fact]
        public void MaxContentWidth_SubtractsTheChromeAndBothScreenMargins()
        {
            Assert.Equal(
                1920 - (2 * DialogLayoutMath.ScreenEdgeMargin) - 20,
                DialogLayoutMath.MaxContentWidth(1920, 20));
        }

        [Fact]
        public void MaxContentWidth_IsNotCappedToThePreferredWidth()
        {
            // The preferred width is a wrap ceiling, not a screen fact: a
            // button row or a title wider than it must still be able to grow
            // the box.
            Assert.True(
                DialogLayoutMath.MaxContentWidth(1920, 20)
                    > DialogLayoutMath.PreferredMaxContentWidth);
        }

        [Fact]
        public void MaxContentHeight_NeverDropsBelowOneLinePlusTheButtonRow()
        {
            int floor = DialogLayoutMath.MessageTopMargin + Pitch
                + DialogLayoutMath.MessageToButtonGap + DialogLayoutMath.ButtonHeight
                + DialogLayoutMath.ButtonBottomMargin;

            Assert.Equal(floor, DialogLayoutMath.MaxContentHeight(120, 74, Pitch));
            Assert.Equal(1080 - 16 - 74, DialogLayoutMath.MaxContentHeight(1080, 74, Pitch));
        }

        // --- Degenerate inputs ---
        [Fact]
        public void Measure_NullMeasure_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => DialogLayoutMath.Measure(
                new[] { "x" }, null, Pitch, 0, 60, 60, RoomyWidth, RoomyHeight));
        }

        [Fact]
        public void Measure_NoParagraphs_StillDrawsOneEmptyLine()
        {
            var layout = DialogLayoutMath.Measure(
                new string[0], Fixed10, Pitch, 0, 60, 60, RoomyWidth, RoomyHeight);

            Assert.Equal(new[] { "" }, layout.Blocks.Single().Lines);
            Assert.Equal(DialogLayoutMath.MinContentWidth, layout.ContentWidth);
        }

        [Fact]
        public void Measure_NullParagraphEntry_IsTreatedAsEmpty()
        {
            var layout = DialogLayoutMath.Measure(
                new string[] { null }, Fixed10, Pitch, 0, 60, 60, RoomyWidth, RoomyHeight);

            Assert.Equal(new[] { "" }, layout.Blocks.Single().Lines);
        }

        [Fact]
        public void Measure_ZeroLineHeight_DoesNotDivideByZero()
        {
            var layout = DialogLayoutMath.Measure(
                new[] { "x" }, Fixed10, 0, 0, 60, 60, RoomyWidth, RoomyHeight);

            Assert.Single(layout.Blocks.Single().Lines);
        }

        // --- The module's own messages, at the module's own numbers ---
        [Theory]
        [InlineData("Add at least one item, then Generate Plan.", 1)]
        [InlineData("This permanently deletes the log file from disk. Continue?", 1)]
        [InlineData("Discard the cached account snapshot? It can only be rebuilt when the GW2 API is reachable.", 2)]
        [InlineData("You have 3 unsaved changes on the Settings tab. Save now, or discard and keep the last saved values?", 2)]
        public void Measure_RealMessages_StayWithinTwoLinesAndOneScreen(string message, int lines)
        {
            // At roughly 8px per character - the module's Font16 - rather
            // than the 10px the rest of the file uses.
            Func<string, int> font16 = s => (int)Math.Ceiling((s ?? "").Length * 8.1);
            var layout = DialogLayoutMath.Measure(
                new[] { message }, font16, Pitch, 105, 60, 60, RoomyWidth, RoomyHeight);

            Assert.Equal(lines, layout.Blocks.Single().Lines.Count);
            Assert.False(layout.Blocks.Single().Truncated);
            Assert.InRange(
                layout.ContentWidth,
                DialogLayoutMath.MinContentWidth, DialogLayoutMath.PreferredMaxContentWidth);
        }

        [Fact]
        public void Measure_EveryConfirmIsShorterThanTheFixedBoxItReplaces()
        {
            // The old box was a constant 145px of content for every caller.
            var messages = new List<string>
            {
                "Add at least one item, then Generate Plan.",
                "This removes every unpinned entry from Plan History. Continue?",
                "Buy everything with a Trading Post price?",
            };

            foreach (string message in messages)
            {
                Assert.True(Measure(message).ContentHeight < 145, message);
            }
        }
    }
}
