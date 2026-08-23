using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The single wrap seam for composed tooltip text (Blish-free, so it is
    /// unit-testable alongside the composers that call it).
    ///
    /// Blish's basic tooltip does cap its own content, but at a fixed 500px
    /// that knows nothing about the module window (930px minimum) or the
    /// screen, and its wrapper splits on spaces only, so an over-long
    /// unbroken token overflows the cap outright - measured against
    /// BlishHUD 1.3.0, recorded in docs/KNOWN-ISSUES.md under "Audit batches
    /// A+B+C tier 1". A tooltip anchored on a tree row therefore still
    /// spills well past the window it belongs to. Constraining content width
    /// to something the module chose is the module's job, and it is done
    /// here once rather than at each call site: every composer routes its
    /// finished text through <see cref="Wrap"/> or <see cref="WrapLines"/>
    /// at its return seam, so future callers inherit the wrap without having
    /// to know it exists.
    ///
    /// The budget is a CHARACTER count, not pixels: a tooltip string is
    /// composed in Services, far from any font, and the alternative -
    /// threading a measured <c>Func&lt;string, int&gt;</c> down from
    /// Views/Rendering - would put a Blish dependency on the seam this class
    /// exists to keep Blish-free. Blish's tooltip font is close enough to
    /// fixed-advance for prose that a character budget bounds the rendered
    /// width well within the window.
    /// </summary>
    public static class TooltipTextFormat
    {
        /// <summary>
        /// Characters per wrapped line. Sized so the widest tooltip line
        /// stays inside the module window at its clamped minimum width
        /// (930px) with room for the tooltip's own padding.
        /// </summary>
        public const int LineBudgetChars = 60;

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

        /// <summary>
        /// The list-shaped counterpart to <see cref="Wrap"/> for composers
        /// that return one string per tooltip line. Returns a fresh,
        /// never-null list; an over-budget input line becomes several output
        /// lines, which is exactly what the caller's newline join renders.
        /// </summary>
        public static List<string> WrapLines(IEnumerable<string> lines)
        {
            var wrapped = new List<string>();
            if (lines == null)
            {
                return wrapped;
            }

            foreach (string line in lines)
            {
                AppendWrapped(line, wrapped);
            }
            return wrapped;
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
