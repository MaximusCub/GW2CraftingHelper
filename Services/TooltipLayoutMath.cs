using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The rich tooltip surface's arithmetic: how a
    /// <see cref="TooltipContent"/> breaks into rendered rows at a real
    /// pixel width, and where the finished box goes so it stays on screen.
    /// Blish-free and expressed against caller-supplied measurement
    /// functions, the same seam <see cref="TextWrapMath"/> uses, so the
    /// surface's Blish-coupled shell stays thin enough to be uninteresting.
    ///
    /// The placement half is the fix Blish itself does not have. Measured
    /// against BlishHUD 1.3.0 (see KNOWN-ISSUES #41):
    /// <c>Tooltip.UpdateTooltipPosition</c> flips above/below the cursor to
    /// protect the TOP edge and shifts left to protect the RIGHT edge, and
    /// clamps neither result - a tall tooltip placed below the cursor runs
    /// off the BOTTOM of the screen, and the left shift can produce a
    /// negative X. <see cref="Place"/> keeps Blish's above-when-it-fits
    /// preference and its 36px cursor gap, then clamps all four edges.
    /// </summary>
    public static class TooltipLayoutMath
    {
        /// <summary>
        /// Blish's own <c>Tooltip.MOUSE_VERTICAL_MARGIN</c> (measured).
        /// Reproduced rather than reduced: the gap is what keeps a tooltip
        /// from covering the cursor.
        /// </summary>
        public const int CursorGap = 36;

        /// <summary>Breathing room kept between the box and every screen edge.</summary>
        public const int ScreenEdgeMargin = 4;

        /// <summary>
        /// Blish's <c>BasicTooltipView.MAX_WIDTH</c> (measured, a hard
        /// 500 that knows nothing about the screen). Kept as the PREFERRED
        /// width so a rich tooltip reads the same as every plain one, but
        /// <see cref="MaxContentWidth"/> narrows it on a screen that cannot
        /// afford it - which is the part Blish's constant cannot do.
        /// </summary>
        public const int PreferredMaxContentWidth = 500;

        /// <summary>
        /// Floor for <see cref="MaxContentWidth"/>. Below this the wrap
        /// degenerates into a column of hard-split fragments; a screen that
        /// narrow is broken in ways a tooltip cannot fix, so the box is
        /// allowed to exceed the margin instead of shredding its text.
        /// </summary>
        public const int MinContentWidth = 120;

        /// <summary>
        /// The width a tooltip may wrap at on this screen.
        /// <paramref name="preferredWidth"/> defaults to Blish's own 500 so
        /// every existing caller reads the same as every plain tooltip; a
        /// caller with a measured cap of its own - the item tooltip, whose
        /// in-game boxes are 300-332px wide (gap G24) - passes it and does
        /// not move the shared constant out from under the rest.
        /// </summary>
        public static int MaxContentWidth(int screenWidth, int chromeWidth, int preferredWidth = 0)
        {
            int preferred = preferredWidth > 0 ? preferredWidth : PreferredMaxContentWidth;
            int usable = screenWidth - (2 * ScreenEdgeMargin) - Math.Max(0, chromeWidth);
            if (usable >= preferred)
            {
                return preferred;
            }
            return Math.Max(MinContentWidth, usable);
        }

        /// <summary>A span placed at an x offset within its rendered row.</summary>
        public readonly struct PlacedSpan
        {
            public PlacedSpan(TooltipSpan span, int x, int width)
            {
                Span = span;
                X = x;
                Width = width;
            }

            public TooltipSpan Span { get; }
            public int X { get; }
            public int Width { get; }
        }

        public sealed class LaidOutRow
        {
            internal LaidOutRow(
                IReadOnlyList<PlacedSpan> spans, int width, int y, int height, string iconUrl)
            {
                Spans = spans;
                Width = width;
                Y = y;
                Height = height;
                IconUrl = iconUrl;
            }

            public IReadOnlyList<PlacedSpan> Spans { get; }
            public int Width { get; }

            /// <summary>Top of this row inside the content, in pixels.</summary>
            public int Y { get; }

            /// <summary>
            /// This row's own height. Prose rows are one line pitch; only
            /// a coin row needs icon clearance and only a header row is
            /// icon-tall (gap G21) - a uniform height taken from the
            /// tallest kind pads every prose row in the box.
            /// </summary>
            public int Height { get; }

            /// <summary>
            /// The header icon, on the FIRST row of a header line only - a
            /// name that wraps must not draw its icon again.
            /// </summary>
            public string IconUrl { get; }
        }

        public sealed class Layout
        {
            internal Layout(IReadOnlyList<LaidOutRow> rows, int width, int height)
            {
                Rows = rows;
                Width = width;
                Height = height;
            }

            public IReadOnlyList<LaidOutRow> Rows { get; }
            public int Width { get; }
            public int Height { get; }
        }

        /// <summary>
        /// Breaks content into rendered rows no wider than
        /// <paramref name="maxWidth"/>.
        ///
        /// Prose is wrapped by <see cref="TextWrapMath.Wrap"/> - the same
        /// tested wrapper the character-budget seam
        /// (<see cref="TooltipTextFormat"/>) uses, called here with a real
        /// font measurement and with the current row's remaining width as
        /// the first-line budget, so a prose span that follows a coin span
        /// wraps against what is actually left of the row. A coin span is
        /// ATOMIC: it moves to the next row whole rather than being split,
        /// because half a coin run is not a number.
        ///
        /// A line with no spans is a deliberate blank separator and still
        /// produces a row, so vertical rhythm survives the layout.
        /// </summary>
        public static Layout LayoutContent(
            TooltipContent content,
            int maxWidth,
            int rowHeight,
            Func<string, int> measureText,
            Func<long, int> measureCoin,
            int coinRowHeight = 0,
            int headerRowHeight = 0,
            int headerIndent = 0)
        {
            if (measureText == null) throw new ArgumentNullException(nameof(measureText));
            if (measureCoin == null) throw new ArgumentNullException(nameof(measureCoin));

            var rows = new List<LaidOutRow>();
            if (content == null || content.IsEmpty)
            {
                return new Layout(rows, 0, 0);
            }

            // Both default to the prose pitch, so a caller that has only
            // one row kind - every test, and every non-item tooltip -
            // still gets the uniform box it always got.
            int coinHeight = coinRowHeight > 0 ? coinRowHeight : rowHeight;
            int headerHeight = headerRowHeight > 0 ? headerRowHeight : rowHeight;

            int effectiveMax = Math.Max(1, maxWidth);
            int y = 0;
            foreach (var line in content.Lines)
            {
                bool isHeader = line.Kind == TooltipLineKind.Header;
                // The name column of a header row starts past the icon,
                // and a wrapped continuation of it stays in that column.
                int indent = isHeader ? Math.Max(0, headerIndent) : 0;
                int lineHeight = isHeader ? headerHeight : rowHeight;
                string iconUrl = isHeader ? line.IconUrl : null;

                var current = new List<PlacedSpan>();
                int x = indent;

                // Commits the row being built and starts the next one -
                // the icon rides the first row of its line only.
                void BreakRow()
                {
                    rows.Add(new LaidOutRow(current, x, y, lineHeight, iconUrl));
                    y += lineHeight;
                    // Continuations are ordinary text rows: only the FIRST
                    // row of a header line carries the icon and its height.
                    lineHeight = rowHeight;
                    iconUrl = null;
                    current = new List<PlacedSpan>();
                    x = indent;
                }

                foreach (var span in line.Spans)
                {
                    if (span.IsCoin)
                    {
                        int coinWidth = Math.Max(0, measureCoin(span.CoinCopper));
                        if (x > indent && x + coinWidth > effectiveMax)
                        {
                            BreakRow();
                        }
                        // A coin run makes the row it actually lands on -
                        // never the one it was pushed off - the taller
                        // coin kind.
                        lineHeight = Math.Max(lineHeight, coinHeight);
                        current.Add(new PlacedSpan(span, x, coinWidth));
                        x += coinWidth;
                        continue;
                    }

                    if (span.Text.Length == 0)
                    {
                        continue;
                    }

                    // TextWrapMath drops the space run a line ends on -
                    // right for a standalone line, wrong for a span whose
                    // trailing space is the separator before the coin run
                    // that follows it ("Cost: " + 1g 23s 45c). Held out of
                    // the wrap and restored below, but only when it still
                    // fits, so the wrap's own width guarantee stands.
                    string core = span.Text.TrimEnd(' ');
                    string trailingSpaces = core.Length == span.Text.Length ? null : span.Text.Substring(core.Length);

                    // Continuation rows start at the indent too, so their
                    // budget is the box minus it - otherwise a wrapped
                    // header name runs past the right edge by one icon.
                    var wrapped = TextWrapMath.Wrap(
                        core, Math.Max(1, effectiveMax - x), Math.Max(1, effectiveMax - indent),
                        measureText).Lines;
                    for (int i = 0; i < wrapped.Count; i++)
                    {
                        if (i > 0)
                        {
                            BreakRow();
                        }
                        string piece = wrapped[i];
                        if (piece.Length == 0)
                        {
                            continue;
                        }
                        int pieceWidth = Math.Max(0, measureText(piece));
                        // WithText, not FromText: a wrapped piece keeps the
                        // original span's role, so a long rarity-coloured
                        // item name stays coloured past its first line.
                        current.Add(new PlacedSpan(span.WithText(piece), x, pieceWidth));
                        x += pieceWidth;
                    }

                    if (trailingSpaces != null)
                    {
                        int trailingWidth = Math.Max(0, measureText(trailingSpaces));
                        if (x + trailingWidth <= effectiveMax)
                        {
                            current.Add(new PlacedSpan(span.WithText(trailingSpaces), x, trailingWidth));
                            x += trailingWidth;
                        }
                    }
                }

                BreakRow();
            }

            int width = 0;
            foreach (var row in rows)
            {
                if (row.Width > width)
                {
                    width = row.Width;
                }
            }
            return new Layout(rows, width, Math.Max(0, y));
        }

        /// <summary>
        /// Where the finished box goes, given the cursor and the screen.
        ///
        /// Horizontal: at the cursor, flipped to the cursor's left when the
        /// box would cross the right edge (Blish's rule), then clamped so
        /// the result cannot be negative (Blish's is not).
        ///
        /// Vertical: above the cursor when it fits, else below (Blish's
        /// rule), then clamped to the bottom edge (Blish never clamps it).
        /// When neither side can hold the box with its cursor gap the box
        /// takes the roomier side and is clamped into the screen - the only
        /// case where it may reach across the cursor, and it needs a
        /// tooltip taller than the screen minus the gap to happen at all.
        /// </summary>
        public static void Place(
            int mouseX, int mouseY,
            int width, int height,
            int screenWidth, int screenHeight,
            out int x, out int y)
        {
            x = mouseX;
            if (x + width > screenWidth - ScreenEdgeMargin)
            {
                x = mouseX - width;
            }
            x = ClampAxis(x, width, screenWidth);

            int above = mouseY - CursorGap - height;
            int below = mouseY + CursorGap;
            if (above >= ScreenEdgeMargin)
            {
                y = above;
                return;
            }
            if (below + height <= screenHeight - ScreenEdgeMargin)
            {
                y = below;
                return;
            }

            int roomAbove = mouseY - CursorGap - ScreenEdgeMargin;
            int roomBelow = screenHeight - ScreenEdgeMargin - CursorGap - mouseY;
            y = ClampAxis(roomAbove >= roomBelow ? ScreenEdgeMargin : screenHeight - height, height, screenHeight);
        }

        /// <summary>
        /// Clamps a box of <paramref name="size"/> into
        /// [<see cref="ScreenEdgeMargin"/>, extent - margin]. A box larger
        /// than the whole extent is pinned to the near edge rather than
        /// pushed off the far one, so its start - where the reader's eye
        /// begins - is always the part that stays visible.
        /// </summary>
        public static int ClampAxis(int desired, int size, int extent)
        {
            int min = ScreenEdgeMargin;
            int max = extent - ScreenEdgeMargin - size;
            if (max <= min)
            {
                return min;
            }
            return desired < min ? min : (desired > max ? max : desired);
        }
    }
}
