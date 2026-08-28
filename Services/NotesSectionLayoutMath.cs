using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The Plan Notes section's own layout arithmetic (Blish-free,
    /// unit-testable): the per-line text budget a note has to work with,
    /// the wrap of one note into physical lines, and the resulting body
    /// height.
    ///
    /// Kept out of PlanContentHeightMath for the same reason
    /// SummarySectionLayoutMath is (see that class's doc comment): Notes is
    /// now the one section whose height is not a function of its row list
    /// alone - it needs the panel width and a font measurement - and
    /// PlanContentHeightMath.SectionBodyHeight's signature deliberately has
    /// neither. Views/CraftingPlanView.CreateCollapsibleSection special-
    /// cases PlanSectionType.Notes to the height its renderer actually
    /// built, exactly as it special-cases Summary.
    ///
    /// The row-height constant is not redefined here: BodyHeight reads
    /// PlanContentHeightMath.FallbackTextRowHeight, so one wrapped line is
    /// still exactly one fixed-height row and the DEBUG per-row assert in
    /// NotesSectionRenderer stays the real check.
    /// </summary>
    internal static class NotesSectionLayoutMath
    {
        /// <summary>Left x of a note line's label.</summary>
        public const int LabelX = 8;

        /// <summary>Right-edge padding shared with every other section.</summary>
        public const int RightPadding = UiSpacing.SectionRightPad;

        /// <summary>Gap reserved between the text and a coin cell.</summary>
        public const int CoinGap = 12;

        /// <summary>
        /// Leading indent every note line carries, continuation lines
        /// included, so a wrapped note reads as one block rather than as
        /// separate notes.
        /// </summary>
        public const string LineIndent = "  ";

        /// <summary>
        /// Floor for the per-line text budget once the indent is charged
        /// against it - mirrors NameMaxWidthBeforeColumn's own 20px clamp,
        /// and keeps a pathologically narrow panel from producing a
        /// zero/negative budget the wrapper would have to degenerate on.
        /// </summary>
        public const int MinTextBudget = 12;

        /// <summary>
        /// Width available to a note's text on a line that reserves
        /// coinCellWidth px for a right-aligned coin cell (0 on lines that
        /// have none). Same NameMaxWidthBeforeColumn shape every other
        /// label-before-a-trailing-column row in this codebase uses.
        /// </summary>
        public static int TextBudget(int panelWidth, int coinCellWidth)
        {
            return PlanRelayoutMath.NameMaxWidthBeforeColumn(
                panelWidth - RightPadding, coinCellWidth, coinCellWidth > 0 ? CoinGap : 0, LabelX);
        }

        /// <summary>
        /// Wraps one note into indented physical lines. The coin cell only
        /// ever sits on the FIRST line, so only that line's budget is
        /// reduced by it; every later line gets the full width.
        /// </summary>
        public static TextWrapMath.WrappedText WrapNote(
            string label, int panelWidth, int coinCellWidth, Func<string, int> measure)
        {
            if (measure == null)
            {
                throw new ArgumentNullException(nameof(measure));
            }

            int indentWidth = measure(LineIndent);
            int firstBudget = Clamp(TextBudget(panelWidth, coinCellWidth) - indentWidth);
            int restBudget = Clamp(TextBudget(panelWidth, 0) - indentWidth);

            var wrapped = TextWrapMath.Wrap(label ?? "", firstBudget, restBudget, measure);

            var indented = new string[wrapped.Lines.Count];
            for (int i = 0; i < wrapped.Lines.Count; i++)
            {
                // An empty line is a deliberate blank line in the source
                // text, not content - indenting it would put stray
                // whitespace in an otherwise blank row.
                indented[i] = wrapped.Lines[i].Length == 0 ? "" : LineIndent + wrapped.Lines[i];
            }

            return new TextWrapMath.WrappedText(indented, wrapped.Truncated);
        }

        /// <summary>
        /// Body height for a Notes section that rendered totalLineCount
        /// wrapped lines - one fixed-height row per LINE, not per note row.
        /// This is the arm that changed: before wrapping, note rows and
        /// physical lines were the same thing and the section fell through
        /// to PlanContentHeightMath.SectionBodyHeight's per-row default.
        /// </summary>
        public static int BodyHeight(int totalLineCount)
        {
            return (totalLineCount > 0 ? totalLineCount : 0) * PlanContentHeightMath.FallbackTextRowHeight;
        }

        private static int Clamp(int budget)
        {
            return budget > MinTextBudget ? budget : MinTextBudget;
        }
    }
}
