using System;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // NotesSectionLayoutMath is the Plan Notes section's own wrap/height
    // arithmetic - the production seam Views/Rendering/NotesSectionRenderer
    // calls once per note at build and again, slot-pinned, at resize
    // settle. Tests drive it through the same Func<string,int> measurement
    // the renderer passes a BitmapFont through.
    public class NotesSectionLayoutMathTests
    {
        private static readonly Func<string, int> Fixed10 = s => (s ?? "").Length * 10;

        // Roughly the live plan width: the desktop capture the audit was
        // taken from had ~830px usable.
        private const int LivePanelWidth = 830;

        [Fact]
        public void TextBudget_ReservesTheCoinCellAndItsGap()
        {
            int plain = NotesSectionLayoutMath.TextBudget(LivePanelWidth, 0);
            int valued = NotesSectionLayoutMath.TextBudget(LivePanelWidth, 90);

            Assert.Equal(plain - 90 - NotesSectionLayoutMath.CoinGap, valued);
        }

        [Fact]
        public void TextBudget_MatchesTheSharedNameColumnFormula()
        {
            // Same PlanRelayoutMath shape every other label-before-a-
            // trailing-column row uses - not a second copy of the
            // arithmetic.
            Assert.Equal(
                PlanRelayoutMath.NameMaxWidthBeforeColumn(
                    LivePanelWidth - NotesSectionLayoutMath.RightPadding,
                    90,
                    NotesSectionLayoutMath.CoinGap,
                    NotesSectionLayoutMath.LabelX),
                NotesSectionLayoutMath.TextBudget(LivePanelWidth, 90));
        }

        [Fact]
        public void WrapNote_ShortNote_IsOneIndentedLine()
        {
            var wrapped = NotesSectionLayoutMath.WrapNote("Total reclaimable value", LivePanelWidth, 0, Fixed10);

            Assert.Single(wrapped.Lines);
            Assert.Equal("  Total reclaimable value", wrapped.Lines[0]);
            Assert.False(wrapped.Truncated);
        }

        [Fact]
        public void WrapNote_EmptyNote_IsStillOneRow()
        {
            var wrapped = NotesSectionLayoutMath.WrapNote("", LivePanelWidth, 0, Fixed10);

            Assert.Single(wrapped.Lines);
            Assert.Equal("", wrapped.Lines[0]);
        }

        [Fact]
        public void WrapNote_NullNote_IsStillOneRow()
        {
            Assert.Single(NotesSectionLayoutMath.WrapNote(null, LivePanelWidth, 0, Fixed10).Lines);
        }

        [Fact]
        public void WrapNote_LongNote_WrapsInsteadOfEllipsizing()
        {
            // The forge-scope note the live capture showed, as ONE note.
            // Before the fix this was cut to a single ~100-character line
            // plus a hover-only tooltip.
            const string note = "This plan includes a Mystic Clover-style Mystic Forge yield - its " +
                "expected output is already probability-adjusted. True multi-outcome Mystic Forge " +
                "gambles (e.g. precursor forging) are a different mechanic.";

            var wrapped = NotesSectionLayoutMath.WrapNote(note, LivePanelWidth, 0, Fixed10);

            Assert.True(wrapped.Lines.Count > 1);
            Assert.False(wrapped.Truncated);
            Assert.All(wrapped.Lines, line => Assert.DoesNotContain("...", line));
            // Every word survives - the point of the fix.
            Assert.Contains("mechanic.", string.Join(" ", wrapped.Lines));
        }

        [Fact]
        public void WrapNote_EveryLineIsIndentedSoAWrappedNoteReadsAsOneBlock()
        {
            const string note = "Buy the Mystic Clover recipe to craft it instead of buying it - " +
                "recipe costs a great deal more than the audit expected it to";

            var wrapped = NotesSectionLayoutMath.WrapNote(note, LivePanelWidth, 0, Fixed10);

            Assert.True(wrapped.Lines.Count > 1);
            Assert.All(wrapped.Lines, line => Assert.StartsWith(NotesSectionLayoutMath.LineIndent, line));
        }

        [Fact]
        public void WrapNote_ValuedNote_FirstLineIsShorterThanTheRest()
        {
            // The coin cell sits on the FIRST line only, so only that line
            // pays for it.
            const string note = "Excess: 12x Glob of Ectoplasm reclaimable at the trading post today";

            var plain = NotesSectionLayoutMath.WrapNote(note, LivePanelWidth, 0, Fixed10);
            var valued = NotesSectionLayoutMath.WrapNote(note, LivePanelWidth, 200, Fixed10);

            Assert.True(valued.Lines[0].Length < plain.Lines[0].Length);
            Assert.All(valued.Lines, line => Assert.True(Fixed10(line) <= NotesSectionLayoutMath.TextBudget(LivePanelWidth, 0), line));
            Assert.True(Fixed10(valued.Lines[0]) <= NotesSectionLayoutMath.TextBudget(LivePanelWidth, 200));
        }

        [Fact]
        public void WrapNote_ExplicitLineBreaks_ComposeWithWidthWrapping()
        {
            // The section already renders multi-sentence notes the builder
            // pre-split into separate rows; a note whose own text carries
            // breaks must keep them AND still width-wrap each piece.
            const string note = "First sentence that is quite long and will not fit on a single line here.\n" +
                "Second.";

            var wrapped = NotesSectionLayoutMath.WrapNote(note, 300, 0, Fixed10);

            Assert.True(wrapped.Lines.Count > 2);
            Assert.Equal("  Second.", wrapped.Lines[wrapped.Lines.Count - 1]);
        }

        [Fact]
        public void WrapNote_SlotPinned_PadsWithBlankLinesWhenTheNoteGotShorter()
        {
            // The resize path widening: fewer lines needed than the row
            // Panels already built.
            var wrapped = NotesSectionLayoutMath.WrapNote("alpha beta", LivePanelWidth, 0, Fixed10, slotCount: 3);

            Assert.Equal(3, wrapped.Lines.Count);
            Assert.Equal("  alpha beta", wrapped.Lines[0]);
            Assert.Equal("", wrapped.Lines[1]);
            Assert.Equal("", wrapped.Lines[2]);
            Assert.False(wrapped.Truncated);
        }

        [Fact]
        public void WrapNote_SlotPinned_EllipsizesTheTailWhenTheNoteGotLonger()
        {
            // The resize path narrowing: more lines needed than the row
            // Panels already built, so the last one keeps the ellipsis
            // contract and the caller keeps a full-text tooltip.
            const string note = "Excess: 12x Glob of Ectoplasm reclaimable at the trading post today";

            var wrapped = NotesSectionLayoutMath.WrapNote(note, 200, 0, Fixed10, slotCount: 2);

            Assert.Equal(2, wrapped.Lines.Count);
            Assert.True(wrapped.Truncated);
            Assert.EndsWith("...", wrapped.Lines[1]);
        }

        [Fact]
        public void WrapNote_NarrowPanel_StillProducesLinesRatherThanDegenerating()
        {
            var wrapped = NotesSectionLayoutMath.WrapNote("alpha beta gamma", 30, 0, Fixed10);

            Assert.NotEmpty(wrapped.Lines);
            Assert.All(wrapped.Lines, line => Assert.NotNull(line));
        }

        [Fact]
        public void WrapNote_NullMeasure_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => NotesSectionLayoutMath.WrapNote("abc", LivePanelWidth, 0, null));
        }

        // --- BodyHeight: the arm that now counts LINES, not note rows ---

        [Fact]
        public void BodyHeight_CountsWrappedLinesNotNoteRows()
        {
            const string note = "This plan includes a Mystic Clover-style Mystic Forge yield - its " +
                "expected output is already probability-adjusted.";

            int lines = NotesSectionLayoutMath.WrapNote(note, LivePanelWidth, 0, Fixed10).Lines.Count;

            Assert.True(lines > 1);
            Assert.Equal(
                lines * PlanContentHeightMath.FallbackTextRowHeight,
                NotesSectionLayoutMath.BodyHeight(lines));
            // The pre-fix arm (one row per note) would have undercounted.
            Assert.True(
                NotesSectionLayoutMath.BodyHeight(lines) > PlanContentHeightMath.FallbackTextRowHeight);
        }

        [Fact]
        public void BodyHeight_ZeroLines_IsZero()
        {
            Assert.Equal(0, NotesSectionLayoutMath.BodyHeight(0));
            Assert.Equal(0, NotesSectionLayoutMath.BodyHeight(-3));
        }

        [Fact]
        public void BodyHeight_UsesTheSharedFixedRowHeightConstant()
        {
            Assert.Equal(PlanContentHeightMath.FallbackTextRowHeight, NotesSectionLayoutMath.BodyHeight(1));
        }
    }
}
