using System;
using System.Linq;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // TextWrapMath is the Blish-free half of the Plan Notes wrapping fix
    // (audit finding M14) and of LabelHelpers.EllipsizeToWidth, which is now
    // a font adapter over Ellipsize below. Every test drives the real
    // production entry points through the same Func<string,int>
    // measurement seam the renderer passes a BitmapFont through.
    public class TextWrapMathTests
    {
        // A fixed-pitch measurement: 10px per character. Keeps the expected
        // wrap points arithmetic rather than font-dependent.
        private static readonly Func<string, int> Fixed10 = s => (s ?? "").Length * 10;

        // A proportional measurement: 'i' is narrow, everything else is 10px.
        // Proves the wrapper asks the measurement rather than counting
        // characters itself.
        private static readonly Func<string, int> Proportional =
            s => (s ?? "").Sum(c => c == 'i' ? 2 : 10);

        // --- Ellipsize (moved out of LabelHelpers, behavior unchanged) ---
        [Fact]
        public void Ellipsize_TextThatFits_ReturnedWhole()
        {
            Assert.Equal("abcde", TextWrapMath.Ellipsize("abcde", 50, Fixed10));
        }

        [Fact]
        public void Ellipsize_TooLong_TruncatesAndAppendsEllipsis()
        {
            // 100px budget, "..." costs 30px, so 7 characters survive.
            Assert.Equal("abcdefg...", TextWrapMath.Ellipsize("abcdefghijklmnop", 100, Fixed10));
        }

        [Fact]
        public void Ellipsize_BudgetNarrowerThanEllipsis_ReturnsEllipsisOnly()
        {
            Assert.Equal("...", TextWrapMath.Ellipsize("abcdefgh", 20, Fixed10));
        }

        [Fact]
        public void Ellipsize_NonPositiveBudget_ReturnsEmpty()
        {
            Assert.Equal("", TextWrapMath.Ellipsize("abc", 0, Fixed10));
        }

        [Fact]
        public void Ellipsize_NullMeasure_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => TextWrapMath.Ellipsize("abc", 100, null));
        }

        // --- Wrap ---
        [Fact]
        public void Wrap_ShortText_StaysOneLine()
        {
            var wrapped = TextWrapMath.Wrap("a short note", 200, 200, Fixed10);

            Assert.Equal(new[] { "a short note" }, wrapped.Lines);
            Assert.False(wrapped.Truncated);
        }

        [Fact]
        public void Wrap_EmptyText_IsStillOneLine()
        {
            var wrapped = TextWrapMath.Wrap("", 200, 200, Fixed10);

            Assert.Equal(new[] { "" }, wrapped.Lines);
            Assert.False(wrapped.Truncated);
        }

        [Fact]
        public void Wrap_NullText_IsStillOneLine()
        {
            Assert.Equal(new[] { "" }, TextWrapMath.Wrap(null, 200, 200, Fixed10).Lines);
        }

        [Fact]
        public void Wrap_LongText_BreaksAtWordBoundariesAndKeepsEveryWord()
        {
            // 100px = 10 characters per line.
            var wrapped = TextWrapMath.Wrap("alpha beta gamma delta", 100, 100, Fixed10);

            Assert.Equal(new[] { "alpha beta", "gamma", "delta" }, wrapped.Lines);
            Assert.False(wrapped.Truncated);
            Assert.DoesNotContain(wrapped.Lines, line => line.Contains("..."));
        }

        [Fact]
        public void Wrap_NoLineExceedsItsBudget()
        {
            const string note = "This plan includes a Mystic Clover-style Mystic Forge yield - " +
                "its expected output is already probability-adjusted.";

            var wrapped = TextWrapMath.Wrap(note, 300, 300, Fixed10);

            Assert.True(wrapped.Lines.Count > 1);
            Assert.All(wrapped.Lines, line => Assert.True(Fixed10(line) <= 300, line));
        }

        [Fact]
        public void Wrap_UsesTheMeasurementNotCharacterCount()
        {
            // "iiiiiiiiii" measures 20px under Proportional, so it fits on
            // the same 50px line as "ab" plus a space; under a character
            // count it would not.
            var wrapped = TextWrapMath.Wrap("ab iiiiiiiiii cd", 50, 50, Proportional);

            Assert.Equal(new[] { "ab iiiiiiiiii", "cd" }, wrapped.Lines);
        }

        [Fact]
        public void Wrap_SingleOverlongWord_HardSplitsAndLosesNothing()
        {
            // Documented choice: a token wider than a whole line is split
            // across lines rather than ellipsized, so no text is lost.
            var wrapped = TextWrapMath.Wrap("abcdefghijkl", 50, 50, Fixed10);

            Assert.Equal(new[] { "abcde", "fghij", "kl" }, wrapped.Lines);
            Assert.False(wrapped.Truncated);
            Assert.Equal("abcdefghijkl", string.Concat(wrapped.Lines));
        }

        [Fact]
        public void Wrap_OverlongWordAfterNormalWords_BreaksFirstThenSplits()
        {
            var wrapped = TextWrapMath.Wrap("hi abcdefghijkl", 50, 50, Fixed10);

            Assert.Equal(new[] { "hi", "abcde", "fghij", "kl" }, wrapped.Lines);
        }

        [Fact]
        public void Wrap_ExplicitLineBreaks_ComposeWithWidthWrapping()
        {
            // Each hard line starts a new physical line AND is width-wrapped
            // on its own.
            var wrapped = TextWrapMath.Wrap("alpha beta\ngamma", 100, 100, Fixed10);

            Assert.Equal(new[] { "alpha beta", "gamma" }, wrapped.Lines);
        }

        [Fact]
        public void Wrap_ExplicitLineBreak_ForcesABreakEvenWhenTheTextWouldFit()
        {
            var wrapped = TextWrapMath.Wrap("ab\ncd", 500, 500, Fixed10);

            Assert.Equal(new[] { "ab", "cd" }, wrapped.Lines);
        }

        [Fact]
        public void Wrap_CarriageReturnLineFeed_IsOneBreakNotTwo()
        {
            Assert.Equal(new[] { "ab", "cd" }, TextWrapMath.Wrap("ab\r\ncd", 500, 500, Fixed10).Lines);
            Assert.Equal(new[] { "ab", "cd" }, TextWrapMath.Wrap("ab\rcd", 500, 500, Fixed10).Lines);
        }

        [Fact]
        public void Wrap_BlankLineBetweenParagraphs_KeepsItsOwnLine()
        {
            Assert.Equal(new[] { "ab", "", "cd" }, TextWrapMath.Wrap("ab\n\ncd", 500, 500, Fixed10).Lines);
        }

        [Fact]
        public void Wrap_LeadingIndentIsPreservedOnTheFirstLineOnly()
        {
            // The Notes builder emits continuation rows that already start
            // with two spaces ("  Saves per unit crafted"); that indent is
            // content, not a wrap artifact.
            var wrapped = TextWrapMath.Wrap("  alpha beta gamma", 100, 100, Fixed10);

            Assert.Equal(new[] { "  alpha", "beta gamma" }, wrapped.Lines);
        }

        [Fact]
        public void Wrap_FirstLineBudgetIsNarrowerThanTheRest()
        {
            // The coin-cell case: the first line reserves room for the
            // right-aligned value, later lines get the full width.
            var wrapped = TextWrapMath.Wrap("alpha beta gamma", 60, 200, Fixed10);

            Assert.Equal(new[] { "alpha", "beta gamma" }, wrapped.Lines);
        }

        [Fact]
        public void Wrap_BeyondTheLineCap_EllipsizesTheTailAndReportsTruncation()
        {
            // 10px per character, 10px budget = 1 character per line, so
            // this text needs far more than MaxWrappedLines lines.
            string text = new string('a', TextWrapMath.MaxWrappedLines * 3);

            var wrapped = TextWrapMath.Wrap(text, 10, 10, Fixed10);

            Assert.Equal(TextWrapMath.MaxWrappedLines, wrapped.Lines.Count);
            Assert.True(wrapped.Truncated);
            Assert.EndsWith("...", wrapped.Lines[wrapped.Lines.Count - 1]);
        }

        [Fact]
        public void Wrap_NullMeasure_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => TextWrapMath.Wrap("abc", 100, 100, null));
        }

        // --- Caller-supplied line cap (ModalDialog: the dialog cannot grow,
        // so the message is capped to the lines that fit above its buttons) ---
        [Fact]
        public void Wrap_CallerLineCap_CapsBelowTheDefaultAndEllipsizesTheTail()
        {
            // 10px per character at a 50px budget = 5 characters per line;
            // six words of five characters would otherwise wrap to 6 lines.
            string text = "aaaaa bbbbb ccccc ddddd eeeee fffff";

            var wrapped = TextWrapMath.Wrap(text, 50, 50, Fixed10, 3);

            Assert.Equal(3, wrapped.Lines.Count);
            Assert.True(wrapped.Truncated);
            Assert.Equal("aaaaa", wrapped.Lines[0]);
            Assert.Equal("bbbbb", wrapped.Lines[1]);
            Assert.EndsWith("...", wrapped.Lines[2]);
        }

        [Fact]
        public void Wrap_CallerLineCap_TextThatFitsIsUntouched()
        {
            var wrapped = TextWrapMath.Wrap("aaaaa bbbbb", 50, 50, Fixed10, 3);

            Assert.Equal(new[] { "aaaaa", "bbbbb" }, wrapped.Lines);
            Assert.False(wrapped.Truncated);
        }

        [Fact]
        public void Wrap_CallerLineCapBelowOne_StillYieldsOneLine()
        {
            // A dialog whose message area measured smaller than one line of
            // its own font must still render the ellipsized head rather
            // than an empty label.
            var wrapped = TextWrapMath.Wrap("aaaaa bbbbb ccccc", 50, 50, Fixed10, 0);

            Assert.Single(wrapped.Lines);
            Assert.True(wrapped.Truncated);
            Assert.EndsWith("...", wrapped.Lines[0]);
        }

        [Fact]
        public void Wrap_WithoutACap_UsesTheDefaultLineCap()
        {
            string text = new string('a', TextWrapMath.MaxWrappedLines * 3);

            var withDefault = TextWrapMath.Wrap(text, 10, 10, Fixed10);
            var withExplicitDefault = TextWrapMath.Wrap(text, 10, 10, Fixed10, TextWrapMath.MaxWrappedLines);

            Assert.Equal(withExplicitDefault.Lines, withDefault.Lines);
            Assert.Equal(withExplicitDefault.Truncated, withDefault.Truncated);
        }

        [Fact]
        public void Wrap_DegenerateZeroBudget_TerminatesWithTheTextIntact()
        {
            var wrapped = TextWrapMath.Wrap("alpha beta", 0, 0, Fixed10);

            Assert.Equal(new[] { "alpha", "beta" }, wrapped.Lines);
        }

        [Fact]
        public void Wrap_SameTextAtTheSameWidth_ProducesTheSameLineCount()
        {
            // NotesSectionRenderer compares the settle-time wrap's line
            // count against the one it built with and only rebuilds when
            // they differ, so the wrap has to be deterministic for a width
            // that did not change.
            var first = TextWrapMath.Wrap("alpha beta gamma delta", 100, 100, Fixed10);
            var second = TextWrapMath.Wrap("alpha beta gamma delta", 100, 100, Fixed10);

            Assert.Equal(first.Lines, second.Lines);
        }

        [Fact]
        public void Wrap_WiderBudget_NeedsFewerLines()
        {
            // The widen case behind the deferred rebuild: the same note
            // genuinely needs fewer rows than it was built with.
            var narrow = TextWrapMath.Wrap("alpha beta gamma delta", 100, 100, Fixed10);
            var wide = TextWrapMath.Wrap("alpha beta gamma delta", 300, 300, Fixed10);

            Assert.True(wide.Lines.Count < narrow.Lines.Count);
            Assert.False(wide.Truncated);
            Assert.All(wide.Lines, line => Assert.DoesNotContain("...", line));
        }
    }
}
