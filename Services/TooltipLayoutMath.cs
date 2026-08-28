using System;
using System.Collections.Generic;

namespace TaimisToolbench.Services
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
    /// preference - which is also the game's - hugs the cursor from above
    /// at the measured game gap rather than Blish's uniform 36
    /// (<see cref="CursorGapAbove"/>), and clamps all four edges.
    /// </summary>
    internal static class TooltipLayoutMath
    {
        /// <summary>
        /// The gap between the cursor and a box placed ABOVE it. The game
        /// hugs the cursor here: on every live3 storage hover
        /// (vials/fury/candy-corn/almonds, 2026-08-26) the box bottom sits
        /// 3-8px above the hovered slot's TOP edge, with the cursor inside
        /// the slot below - so the true cursor-to-box gap is small, and
        /// Blish's uniform 36 (its MOUSE_VERTICAL_MARGIN) is what made a
        /// bottom-of-window hover read as a detached box. 8 is the
        /// measured slot-edge bound; the exact cursor pixel is not visible
        /// in any capture, so the value within [3..8] is INFERRED. Nothing
        /// above the cursor needs clearing - the cursor sprite hangs
        /// down-right of its hotspot.
        /// </summary>
        public const int CursorGapAbove = 8;

        /// <summary>
        /// The gap between the cursor and a box placed BELOW it - Blish's
        /// own <c>Tooltip.MOUSE_VERTICAL_MARGIN</c> (measured, decompiled
        /// 1.3.0), kept: it is what clears the cursor sprite, and no
        /// capture measures the game's below-placement gap.
        /// </summary>
        public const int CursorGapBelow = 36;

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
        /// The item tooltip's own wrap maximum, in the width units
        /// <c>BitmapFont.MeasureString</c> reports for the shipped
        /// Menomonia 14 face with Blish's <c>LetterSpacing = -1</c>.
        /// <para>
        /// DERIVED FROM THE GAME'S OWN BREAK DECISIONS rather than from a
        /// game-pixel cap converted by a scale factor. The earlier 350 came
        /// from a measured game cap of [345, 347) game px multiplied by a
        /// MEAN font ratio of 1.014; that mean hides a real per-string
        /// spread of 0.99x to 1.03x, because LetterSpacing = -1 tightens
        /// tracking on a face whose glyph boxes are already ~10% wider than
        /// the game's, so how a given string lands depends on its letter
        /// count as much as its length. The 2026-08-27 owner A/B - Gift of
        /// Twilight 19648 hovered in the module and in the game - caught
        /// the low end of that spread: the game wrapped its description and
        /// the module did not.
        /// </para>
        /// <para>
        /// Each live capture that wraps a paragraph pins the cap twice: it
        /// must be at least the width of the line the game KEPT whole, and
        /// below that line plus the word the game PUSHED down. Measured
        /// through this face for the whole wrapped corpus (widths in this
        /// constant's units):
        /// </para>
        /// <list type="bullet">
        /// <item>Gift of Twilight 19648: 282 kept / 338 with "Twilight."
        /// pushed down; its "Made by combining these items in the Mystic
        /// Forge:" line, 317, stays whole.</item>
        /// <item>eyes-of-kormir 83103: 313 kept / 366 with "because";
        /// 315 kept / 352 with "under".</item>
        /// <item>heart-of-destroyer 67017: 293 kept / 362 with
        /// "Bloodstone"; 326 kept / 387 with "Destroyer".</item>
        /// <item>fury-scorched 86967: 357 kept / 378 with "for" - the ONE
        /// outlier, see below.</item>
        /// </list>
        /// <para>
        /// Every constraint but fury's intersects at [326, 338); 332 is its
        /// midpoint, so no decision sits within 6px of flipping. Fury's
        /// kept line needs a cap of 357+, which would un-wrap Gift of
        /// Twilight AND eyes' second line, so it loses 1 constraint to 5.
        /// Fury's own line is the corpus's widest-measuring string in this
        /// face (1.03x the game) and it will wrap one word early - a
        /// recorded, measured cost of rendering the game's text at a face
        /// the game does not ship.
        /// </para>
        /// </summary>
        public const int ItemTooltipMaxContentWidth = 332;

        /// <summary>
        /// The width a tooltip may wrap at on this screen.
        /// <paramref name="preferredWidth"/> defaults to Blish's own 500 so
        /// every existing caller reads the same as every plain tooltip; a
        /// caller with a measured cap of its own - the item tooltip, at
        /// <see cref="ItemTooltipMaxContentWidth"/> - passes it and does
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
                IReadOnlyList<PlacedSpan> spans, int width, int y, int height, string iconUrl,
                TooltipLineKind kind = TooltipLineKind.Text)
            {
                Spans = spans;
                Width = width;
                Y = y;
                Height = height;
                IconUrl = iconUrl;
                Kind = kind;
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
            /// name that wraps must not draw its icon again. On an
            /// <see cref="TooltipLineKind.Effect"/> row, the effect's own
            /// inline icon instead, same first-row-only rule.
            /// </summary>
            public string IconUrl { get; }

            /// <summary>
            /// The line kind this row renders, continuation rows included -
            /// how the surface tells a framed 32px header icon from the
            /// bare ~26px effect icon that shares <see cref="IconUrl"/>.
            /// </summary>
            public TooltipLineKind Kind { get; }
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
            int headerIndent = 0,
            int effectIndent = 0)
        {
            if (measureText == null)
            {
                throw new ArgumentNullException(nameof(measureText));
            }

            if (measureCoin == null)
            {
                throw new ArgumentNullException(nameof(measureCoin));
            }

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
                bool isEffect = line.Kind == TooltipLineKind.Effect;
                // The name column of a header row starts past the icon,
                // and a wrapped continuation of it stays in that column.
                // An effect row is indented past its inline icon the same
                // way (measured: the game's effect text column starts
                // ~31px in, live3 soul-pastries/candy-corn).
                int indent = isHeader ? Math.Max(0, headerIndent)
                    : isEffect ? Math.Max(0, effectIndent) : 0;
                int lineHeight = isHeader ? headerHeight : rowHeight;
                string iconUrl = isHeader || isEffect ? line.IconUrl : null;

                var current = new List<PlacedSpan>();
                int x = indent;

                // Commits the row being built and starts the next one -
                // the icon rides the first row of its line only.
                void BreakRow()
                {
                    rows.Add(new LaidOutRow(current, x, y, lineHeight, iconUrl, line.Kind));
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
        /// Vertical: above the cursor when it fits - hugging it at the
        /// measured <see cref="CursorGapAbove"/>, which is the game's own
        /// preference (every live3 capture grows up from just above the
        /// cursor) - else below at <see cref="CursorGapBelow"/>, then
        /// clamped to the bottom edge (Blish never clamps it).
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

            int above = mouseY - CursorGapAbove - height;
            int below = mouseY + CursorGapBelow;
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

            int roomAbove = mouseY - CursorGapAbove - ScreenEdgeMargin;
            int roomBelow = screenHeight - ScreenEdgeMargin - CursorGapBelow - mouseY;
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

        /// <summary>
        /// Where the game's tooltip canvas art starts inside Blish's
        /// 942x942 "tooltip" texture. Blish's own draw (decompiled 1.3.0)
        /// crops from (3,4) to skip the art's baked border, and the
        /// 2026-08-26 live captures confirm the game itself composites
        /// exactly this crop 1:1: the interior of live2/k-2 correlates with
        /// the texture at r=0.983 when aligned to this origin
        /// (fidelity-audit, 8.4 closure).
        /// </summary>
        public const int CanvasArtSourceX = 3;

        public const int CanvasArtSourceY = 4;

        /// <summary>
        /// How much of one axis of the canvas art a box of
        /// <paramref name="boxLength"/> can source starting at
        /// <paramref name="offset"/>: the box length, clamped to what the
        /// texture has left past the offset, never negative. The 942px
        /// texture leaves 939x938 - a rich tooltip is
        /// <see cref="ItemTooltipMaxContentWidth"/> plus chrome at its
        /// widest and never approaches it, so the clamp exists for the
        /// pathological box, not the common one.
        /// </summary>
        public static int CanvasArtSourceLength(int boxLength, int textureLength, int offset)
        {
            int available = textureLength - offset;
            if (available <= 0 || boxLength <= 0)
            {
                return 0;
            }

            return boxLength < available ? boxLength : available;
        }
    }
}
