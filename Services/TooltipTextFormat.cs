using System;
using System.Collections.Generic;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The single wrap seam for composed tooltip text (Blish-free, so it is
    /// unit-testable alongside the composers that call it).
    ///
    /// Blish's basic tooltip already caps its own content width, at a fixed
    /// 500px - measured against BlishHUD 1.3.0, recorded in
    /// KNOWN-ISSUES #43 - so this
    /// seam is NOT what keeps a tooltip inside the module window (1378px
    /// clamped minimum, and 930px before that); 500px already does. What it adds is control over
    /// where the break lands and what happens to a token that cannot break:
    /// Blish's wrapper (DrawUtil.WrapText) splits on spaces only and never
    /// splits an over-long single token, so an unbroken run wider than 500px
    /// overflows the cap outright, while TextWrapMath hard-splits it. The
    /// budget is therefore set at, not under, Blish's own effective width
    /// (see <see cref="LineBudgetChars"/>) - narrowing it further would only
    /// add lines to a tooltip that Blish positions with no clamp on the
    /// bottom screen edge.
    ///
    /// Its live callers are the two places a finished plain string is
    /// handed to Blish: <c>TooltipFacility.ApplyPlain</c> and
    /// <c>LogTabContent</c>. The tree tooltip composers do NOT route through
    /// here - their output goes to the rich surface, which wraps against a
    /// real font at real pixel widths.
    ///
    /// The budget is a CHARACTER count, not pixels: a tooltip string is
    /// composed in Services, far from any font, and the alternative -
    /// threading a measured <c>Func&lt;string, int&gt;</c> down from
    /// Views/Rendering - would put a Blish dependency on the seam this class
    /// exists to keep Blish-free.
    /// </summary>
    internal static class TooltipTextFormat
    {
        /// <summary>
        /// Characters per wrapped line, derived from the one width Blish
        /// itself enforces: BasicTooltipView.MAX_WIDTH is 500px (measured).
        /// The budget sits just inside that, which makes this wrap a no-op
        /// on width - it reproduces the break Blish would have made anyway,
        /// at a point the module controls - rather than a narrowing that
        /// adds lines. Height matters here: Blish places a tooltip that
        /// does not fit above the cursor 36px BELOW it and never clamps to
        /// the bottom screen edge, so every extra wrapped line is a line
        /// that can fall off the screen.
        /// <para>
        /// The font this is measured against is Blish's DefaultFont14, NOT
        /// the module's body font. Every production consumer of this
        /// constant ends at Control.BasicTooltipText, which Blish renders
        /// in its own BasicTooltipView with no Font seam the module can
        /// reach - the same exclusion Views/Rendering/UiFonts records for
        /// Checkbox and StandardButton. The module's +2pt body bump
        /// therefore does not move this number in either direction, and the
        /// rich tooltip path (Views/Rendering/RichTooltipSurface) wraps
        /// against real pixel widths and never reads it.
        /// </para>
        /// <para>
        /// 71, not the shipped 75: every prose string of 55 characters or
        /// more that this module builds (73 of them, swept out of Services
        /// and Views) measured against the installed Menomonia 14 XNB with
        /// MonoGame.Extended's own advance / XOffset+Width rule - the same
        /// parse behind docs/research/minimum-window-width.md - averages
        /// 7.03px per character, so 500px is 71 characters, not the 76 the
        /// original 6.5px/char estimate assumed. Per-string the spread is
        /// 6.7 to 7.5px/char, so prose at the wide end still crosses 500px
        /// inside a 71-character line; Blish's own space wrap takes those,
        /// which costs a break it would have made anyway and never loses
        /// text. The case only this seam handles - a single token wider
        /// than the cap, which Blish's wrapper will not split - is hard-cut
        /// by TextWrapMath before the budget matters.
        /// </para>
        /// </summary>
        public const int LineBudgetChars = 71;

        // Character-count stand-in for TextWrapMath's font measurement -
        // the whole reason the wrapper takes a measure function rather than
        // a BitmapFont (see TextWrapMath's own doc comment). Cached rather
        // than allocated per call: wrapping runs once per rendered tree row.
        private static readonly Func<string, int> CharacterCount = s => s == null ? 0 : s.Length;

        /// <summary>
        /// Wraps every line of a composed tooltip string to
        /// <see cref="LineBudgetChars"/> at word boundaries. Existing hard
        /// breaks and blank separator lines are preserved exactly; a line
        /// already within budget is returned untouched.
        /// </summary>
        public static string Wrap(string tooltipText)
        {
            if (string.IsNullOrEmpty(tooltipText))
            {
                return tooltipText;
            }

            var wrapped = new List<string>();
            AppendWrapped(tooltipText, wrapped);
            return string.Join("\n", wrapped);
        }

        // Wrapped one source line at a time rather than by handing the whole
        // composed string to TextWrapMath.Wrap in one call: that method caps
        // a single wrap at MaxWrappedLines (24) and ellipsizes the tail past
        // it, a cap sized for one note in a fixed-height panel. A tooltip is
        // many independent lines, and silently dropping its tail is the very
        // text loss this wrap exists to remove - per-line, the cap is
        // unreachable for anything short of a 1400-character sentence.
        private static void AppendWrapped(string line, List<string> into)
        {
            if (line == null)
            {
                into.Add("");
                return;
            }

            foreach (string sourceLine in SplitLines(line))
            {
                into.AddRange(
                    TextWrapMath.Wrap(sourceLine, LineBudgetChars, LineBudgetChars, CharacterCount).Lines);
            }
        }

        private static string[] SplitLines(string text)
        {
            // The overwhelmingly common case - one composer list entry, no
            // embedded break - skips the Replace/Split allocations; this
            // runs once per rendered tree row.
            if (text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0)
            {
                return new[] { text };
            }

            return text.Replace("\r\n", "\n").Split('\n', '\r');
        }
    }
}
