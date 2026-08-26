using System;
using System.Collections.Generic;
using System.Text;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure text layout arithmetic (Blish-free, unit-testable): greedy
    /// width-wrapping and single-line ellipsis truncation, both expressed
    /// against a caller-supplied width measurement.
    ///
    /// The measurement seam is a <c>Func&lt;string, int&gt;</c> rather than a
    /// BitmapFont, for the same reason SummarySectionLayoutMath takes an
    /// already-measured <c>widestNumberWidth</c> int: the arithmetic belongs
    /// in Services (testable, Blish-free) while the only thing that needs a
    /// real font - measuring a string - stays at the Views/Rendering call
    /// site. Callers pass
    /// <c>s =&gt; (int)Math.Ceiling(font.MeasureString(s).Width)</c>.
    /// LabelHelpers.EllipsizeToWidth is now a thin adapter over
    /// <see cref="Ellipsize"/> so the two paths cannot drift.
    /// </summary>
    internal static class TextWrapMath
    {
        public const string Ellipsis = "...";

        /// <summary>
        /// Default line cap for Wrap - the cap every caller that does not
        /// state its own gets. A note wrapped against
        /// the 12px width floor NotesSectionLayoutMath clamps to could
        /// otherwise turn a few hundred characters into a hundred rows;
        /// past this cap the tail is ellipsized into the last line and the
        /// caller's tooltip carries the full text.
        /// </summary>
        public const int MaxWrappedLines = 24;

        /// <summary>
        /// A wrap result: the physical lines to render, plus whether any
        /// text was dropped (the line cap was hit) and therefore needs a
        /// full-text tooltip.
        /// </summary>
        public readonly struct WrappedText
        {
            public readonly IReadOnlyList<string> Lines;
            public readonly bool Truncated;

            public WrappedText(IReadOnlyList<string> lines, bool truncated)
            {
                Lines = lines;
                Truncated = truncated;
            }
        }

        /// <summary>
        /// Truncates text to fit maxWidth, appending "..." when it does not
        /// fit whole. Binary-searches the longest prefix (rather than
        /// trimming one character at a time) since measurement is not free
        /// and item names can run long.
        /// </summary>
        public static string Ellipsize(string text, int maxWidth, Func<string, int> measure)
        {
            if (measure == null)
            {
                throw new ArgumentNullException(nameof(measure));
            }

            if (string.IsNullOrEmpty(text))
            {
                return text ?? "";
            }

            if (maxWidth <= 0)
            {
                return "";
            }

            if (measure(text) <= maxWidth)
            {
                return text;
            }

            int ellipsisWidth = measure(Ellipsis);
            if (ellipsisWidth >= maxWidth)
            {
                // Degenerate (extremely narrow column): still show the
                // ellipsis rather than nothing, so the row reads as
                // "truncated" instead of "blank/broken".
                return Ellipsis;
            }

            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                int width = measure(text.Substring(0, mid)) + ellipsisWidth;
                if (width <= maxWidth)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return lo <= 0 ? Ellipsis : text.Substring(0, lo) + Ellipsis;
        }

        /// <summary>
        /// Greedy word wrap. The first physical line is budgeted against
        /// firstLineMaxWidth (which a row with a right-aligned coin cell
        /// makes narrower than the rest), every later line against
        /// maxWidth. Always returns at least one line - an empty/null text
        /// wraps to a single empty line, so a caller that emits one fixed-
        /// height row per line still emits the row it would have emitted
        /// before.
        /// </summary>
        public static WrappedText Wrap(
            string text, int firstLineMaxWidth, int maxWidth, Func<string, int> measure)
        {
            return Wrap(text, firstLineMaxWidth, maxWidth, measure, MaxWrappedLines);
        }

        /// <summary>
        /// As <see cref="Wrap(string, int, int, Func{string, int})"/>, with
        /// the line cap supplied by the caller instead of taken from
        /// <see cref="MaxWrappedLines"/> - for a surface whose height is
        /// fixed by something other than a note column, e.g. a dialog that
        /// cannot grow and must keep its buttons inside its own content
        /// region. maxLines below 1 is treated as 1: a caller with no room
        /// at all still gets the ellipsized head rather than nothing.
        /// </summary>
        public static WrappedText Wrap(
            string text, int firstLineMaxWidth, int maxWidth, Func<string, int> measure, int maxLines)
        {
            if (measure == null)
            {
                throw new ArgumentNullException(nameof(measure));
            }

            if (maxLines < 1)
            {
                maxLines = 1;
            }

            var lines = new List<string>();
            foreach (string segment in SplitHardBreaks(text))
            {
                FillSegment(segment, lines, firstLineMaxWidth, maxWidth, measure);
            }

            bool truncated = false;
            if (lines.Count > maxLines)
            {
                // The tail is rebuilt from the lines that do not fit rather
                // than from original offsets: wrapping drops the space runs
                // it breaks on, so there is no exact substring to slice.
                // Only the ellipsized head of this string is ever shown -
                // the caller's tooltip carries the true text.
                var tail = new StringBuilder(lines[maxLines - 1]);
                for (int i = maxLines; i < lines.Count; i++)
                {
                    tail.Append(' ').Append(lines[i]);
                }

                lines.RemoveRange(maxLines - 1, lines.Count - (maxLines - 1));
                lines.Add(Ellipsize(tail.ToString(), BudgetFor(lines.Count, firstLineMaxWidth, maxWidth), measure));
                truncated = true;
            }

            return new WrappedText(lines, truncated);
        }

        private static void FillSegment(
            string segment, List<string> lines, int firstLineMaxWidth, int maxWidth, Func<string, int> measure)
        {
            string current = "";
            string pending = "";
            int i = 0;
            while (i < segment.Length)
            {
                bool isSpace = segment[i] == ' ';
                int j = i;
                while (j < segment.Length && (segment[j] == ' ') == isSpace)
                {
                    j++;
                }

                string token = segment.Substring(i, j - i);
                i = j;

                if (isSpace)
                {
                    // Held back so a line never ends in trailing spaces and
                    // the run is dropped when it lands on a wrap point.
                    // Runs alternate, so this can never overwrite an
                    // unconsumed run.
                    pending = token;
                    continue;
                }

                int budget = BudgetFor(lines.Count, firstLineMaxWidth, maxWidth);
                string candidate = current + pending + token;
                if (measure(candidate) <= budget)
                {
                    current = candidate;
                    pending = "";
                }
                else if (current.Length > 0)
                {
                    lines.Add(current);
                    pending = "";
                    budget = BudgetFor(lines.Count, firstLineMaxWidth, maxWidth);
                    current = measure(token) <= budget
                        ? token
                        : HardSplit(token, lines, firstLineMaxWidth, maxWidth, measure);
                }
                else
                {
                    // Nothing committed on this line yet, so there is no
                    // wrap point to break at - the leading indent plus the
                    // first word already overflows. Split it.
                    current = HardSplit(candidate, lines, firstLineMaxWidth, maxWidth, measure);
                    pending = "";
                }
            }

            // Flushed unconditionally: an empty segment is a deliberate
            // blank line in the source text and keeps its own row. A
            // trailing space run is never flushed with it.
            lines.Add(current);
        }

        /// <summary>
        /// A single token wider than a whole line: split it across lines at
        /// the largest prefix that fits, rather than ellipsizing it. Nothing
        /// is dropped, so a long unbroken run (a URL, an ID-like string, a
        /// very long item name at a very narrow width) stays fully readable
        /// - the alternative, ellipsis, is exactly the text loss this
        /// wrapping exists to remove. Returns the remainder that fits on
        /// the line being built; every full line it produced is already in
        /// lines.
        /// </summary>
        private static string HardSplit(
            string word, List<string> lines, int firstLineMaxWidth, int maxWidth, Func<string, int> measure)
        {
            string rest = word;
            while (true)
            {
                int budget = BudgetFor(lines.Count, firstLineMaxWidth, maxWidth);
                if (budget <= 0 || measure(rest) <= budget)
                {
                    return rest;
                }

                int fit = LongestPrefixWithin(rest, budget, measure);
                if (fit < 1)
                {
                    fit = 1;
                }

                if (fit >= rest.Length)
                {
                    return rest;
                }

                lines.Add(rest.Substring(0, fit));
                rest = rest.Substring(fit);
            }
        }

        private static int LongestPrefixWithin(string text, int maxWidth, Func<string, int> measure)
        {
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (measure(text.Substring(0, mid)) <= maxWidth)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return lo;
        }

        private static int BudgetFor(int lineIndex, int firstLineMaxWidth, int maxWidth)
        {
            return lineIndex == 0 ? firstLineMaxWidth : maxWidth;
        }

        private static IEnumerable<string> SplitHardBreaks(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield return "";
                yield break;
            }

            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\n' && c != '\r')
                {
                    continue;
                }

                yield return text.Substring(start, i - start);
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                start = i + 1;
            }

            yield return text.Substring(start);
        }
    }
}
