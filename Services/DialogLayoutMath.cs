using System;
using System.Collections.Generic;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// How wide and how tall a popup dialog has to be for the words and the
    /// buttons it actually carries, and where those go inside it. Blish-free
    /// and expressed against a caller-supplied measurement function, the same
    /// seam <see cref="TextWrapMath"/> and <see cref="TooltipLayoutMath"/>
    /// use, so the dialog shells above it stay thin enough to be
    /// uninteresting.
    /// <para>
    /// The problem it replaces: both dialogs were fixed rectangles sized for
    /// their worst case, so a one-line acknowledgement and a five-line
    /// warning drew the same box. Width, height and every inner offset are
    /// now derived from the measured message and the measured button labels.
    /// </para>
    /// <para>The width bracket and the balanced wrap: docs/ARCHITECTURE.md,
    /// "Services A-P: relocated design narrative".</para>
    /// </summary>
    internal static class DialogLayoutMath
    {
        /// <summary>Breathing room kept between the window and every screen edge.</summary>
        public const int ScreenEdgeMargin = 8;

        /// <summary>
        /// The width a message is allowed to wrap at before the box stops
        /// growing - the content width both dialogs already shipped at
        /// (a 560px window less its 10px side insets). Kept as the ceiling
        /// so no message can wrap to MORE lines than it did before this
        /// arithmetic existed, and so one long sentence cannot produce a
        /// 1200px dialog.
        /// </summary>
        public const int PreferredMaxContentWidth = 540;

        /// <summary>
        /// Floor for the content width. Not a readability number: it
        /// protects the title bar. WindowBase2 stretches its left title-bar
        /// texture into <c>Min(textureWidth, windowWidth - 216)</c>
        /// (decompiled 1.3.0, RecalculateLayout), so a narrow window
        /// squeezes the art. The only recorded streaking is at a 400px
        /// window (~184px of draw) and the only recorded clean render at
        /// 560 (~344px); 500 - this constant plus the 2x10 side insets -
        /// sits at ~284. INFERRED, not measured: raise it if the art
        /// degrades.
        /// </summary>
        public const int MinContentWidth = 480;

        /// <summary>Gap above the first line of the message.</summary>
        public const int MessageTopMargin = 6;

        /// <summary>Gap between two message paragraphs.</summary>
        public const int ParagraphGap = 8;

        /// <summary>Gap between the last message line and the button row.</summary>
        public const int MessageToButtonGap = 16;

        /// <summary>
        /// Footer button height. The two dialogs are deliberately outside
        /// Views/Rendering/UiMetrics.ButtonHeight, which is the height of a
        /// button on a TAB.
        /// </summary>
        public const int ButtonHeight = 25;

        /// <summary>Gap below the button row.</summary>
        public const int ButtonBottomMargin = 10;

        /// <summary>Gap between the two buttons of a confirm.</summary>
        public const int ButtonGap = 16;

        /// <summary>
        /// Total left+right slack around a measured button label, so a label
        /// that only just fits its floor width does not sit edge to edge with
        /// the border. StandardButton centres its text with zero side
        /// padding, so the breathing room has to be added by the caller.
        /// </summary>
        public const int ButtonSidePadding = 24;

        /// <summary>Slack kept outside the button row when it is what sets the width.</summary>
        public const int ButtonRowSideMargin = 8;

        /// <summary>
        /// Floor for a button seat, and the only one. A confirm and a cancel
        /// in one row share a SINGLE width - the larger of what the two
        /// labels need - so the shorter label can never render as the wider
        /// button. Two floors sat here first, 100 for the confirm seat and
        /// 70 for the cancel, which inverted that relationship outright:
        /// "Clear" floored up to 100 beside a longer "Cancel" that reached
        /// only 70, and it was reported on sight once the boxes went tight.
        /// <para>
        /// 80 is the width of the Settings save bar's "Save"
        /// (SettingsTabContent.SaveButtonWidth), the narrowest
        /// free-standing word-labelled button the module ships, so a verb in
        /// a dialog is never narrower than the same verb on a tab. A lone
        /// acknowledgement button is not in a pair: it takes this floor or
        /// its own label, whichever is wider, and is not stretched to the
        /// width a second button would have wanted.
        /// </para>
        /// </summary>
        public const int MinButtonWidth = 80;

        /// <summary>
        /// Where WindowBase2 starts drawing the title inside the window
        /// (decompiled 1.3.0: the left title-bar bounds offset by 80, whose
        /// own X is -2). Fixed, with no alignment control, so window width
        /// is the only lever a dialog has over its title.
        /// </summary>
        public const int TitleTextIndent = 80;

        /// <summary>
        /// The run at the right of the title bar the title must not reach:
        /// the exit button plus the right title-bar section's own inset.
        /// Derived so the module's longest title reproduces its measured
        /// requirement - "GW2 API access not ready" clipped three characters
        /// at a 480px window and renders whole at 560. This and
        /// <see cref="TitleTextIndent"/> are window-coordinate quantities
        /// applied to a CONTENT width, so the shell's side insets are slack
        /// on top: deliberate, because the exit button's texture width is
        /// not knowable from here.
        /// </summary>
        public const int TitleRightReserve = 80;

        /// <summary>
        /// X of ONE physical line of the body, centred in the content box.
        /// The body's lines are placed one control each rather than as a
        /// single multi-line label: a label centres its whole BLOCK, and the
        /// pen returns to that block's own left edge at every newline, so
        /// every line after the first reads as left-aligned under the one
        /// above it - the defect this exists to remove.
        /// <para>
        /// A line wider than the box pins to its left edge rather than
        /// overhanging both sides, which is what
        /// <see cref="Measure"/>'s own width clamp can leave when the screen
        /// refuses the width the message wanted.
        /// </para>
        /// </summary>
        public static int LineX(int contentWidth, int lineWidth)
        {
            int x = (contentWidth - lineWidth) / 2;
            return x > 0 ? x : 0;
        }

        /// <summary>One rendered paragraph: its physical lines and its top edge.</summary>
        public sealed class MessageBlock
        {
            internal MessageBlock(IReadOnlyList<string> lines, int y, bool truncated)
            {
                Lines = lines;
                Y = y;
                Truncated = truncated;
            }

            public IReadOnlyList<string> Lines { get; }

            /// <summary>Top of this paragraph inside the content, in pixels.</summary>
            public int Y { get; }

            /// <summary>
            /// True when the line cap dropped text, so the caller owes the
            /// full string a tooltip.
            /// </summary>
            public bool Truncated { get; }
        }

        /// <summary>A finished dialog: the content box, its message and its button row.</summary>
        public sealed class Layout
        {
            internal Layout(
                int contentWidth, int contentHeight, IReadOnlyList<MessageBlock> blocks,
                int buttonY, int confirmX, int confirmWidth, int cancelX, int cancelWidth)
            {
                ContentWidth = contentWidth;
                ContentHeight = contentHeight;
                Blocks = blocks;
                ButtonY = buttonY;
                ConfirmX = confirmX;
                ConfirmWidth = confirmWidth;
                CancelX = cancelX;
                CancelWidth = cancelWidth;
            }

            public int ContentWidth { get; }

            public int ContentHeight { get; }

            public IReadOnlyList<MessageBlock> Blocks { get; }

            public int ButtonY { get; }

            public int ConfirmX { get; }

            public int ConfirmWidth { get; }

            /// <summary>Left edge of the second button; 0 when there is none.</summary>
            public int CancelX { get; }

            /// <summary>
            /// Width of the second button; 0 when there is none. Equal to
            /// <see cref="ConfirmWidth"/> whenever there is one - see
            /// <see cref="MinButtonWidth"/> for why a pair shares a seat.
            /// </summary>
            public int CancelWidth { get; }
        }

        /// <summary>
        /// The content width this screen can physically hold, with
        /// <paramref name="chromeWidth"/> the part the window spends outside
        /// its content box. This is the HARD ceiling and nothing overrides
        /// it. It is deliberately not clamped to
        /// <see cref="PreferredMaxContentWidth"/>: a button row or a title
        /// too wide for the preferred width must still be allowed to grow
        /// the box, and only the screen can refuse it.
        /// </summary>
        public static int MaxContentWidth(int screenWidth, int chromeWidth)
        {
            return Math.Max(1, screenWidth - (2 * ScreenEdgeMargin) - Math.Max(0, chromeWidth));
        }

        /// <summary>
        /// One line of message plus the button row and the paddings - the
        /// smallest content box a dialog can be answered in.
        /// </summary>
        public static int MinContentHeight(int lineHeight)
        {
            return MessageTopMargin + Math.Max(1, lineHeight)
                + MessageToButtonGap + ButtonHeight + ButtonBottomMargin;
        }

        /// <summary>
        /// The height a dialog's content may occupy on this screen.
        /// <paramref name="chromeHeight"/> is what the window spends outside
        /// the content box - its title bar and its insets. Never returns less
        /// than <see cref="MinContentHeight"/>: a dialog whose buttons have
        /// left the screen cannot be answered.
        /// </summary>
        public static int MaxContentHeight(int screenHeight, int chromeHeight, int lineHeight)
        {
            int floor = MinContentHeight(lineHeight);
            int usable = screenHeight - (2 * ScreenEdgeMargin) - Math.Max(0, chromeHeight);
            return usable < floor ? floor : usable;
        }

        /// <summary>
        /// Sizes a dialog around what it actually carries.
        /// <para>
        /// Width: the smallest width that still wraps the message to the
        /// line count it reaches at <see cref="PreferredMaxContentWidth"/>,
        /// so a two-line message gets two balanced lines rather than one
        /// full one and a stub; then raised to whatever the button row, the
        /// title and <see cref="MinContentWidth"/> need; then clamped to
        /// <paramref name="maxContentWidth"/>, which is the screen's and
        /// wins over all of them. Height: the wrapped line count at the
        /// chosen width plus the button row and the paddings, capped so the
        /// buttons stay on screen.
        /// </para>
        /// <paramref name="cancelLabelWidth"/> below zero means a one-button
        /// dialog. Widths are measured by the caller because only it has a
        /// font: the message in the message face, the buttons in the face
        /// StandardButton paints, the title in the face WindowBase2 paints.
        /// </summary>
        public static Layout Measure(
            IReadOnlyList<string> paragraphs,
            Func<string, int> measureMessage,
            int lineHeight,
            int titleWidth,
            int confirmLabelWidth,
            int cancelLabelWidth,
            int maxContentWidth,
            int maxContentHeight)
        {
            if (measureMessage == null)
            {
                throw new ArgumentNullException(nameof(measureMessage));
            }

            var text = Normalize(paragraphs);
            int pitch = Math.Max(1, lineHeight);
            int cap = Math.Max(1, maxContentWidth);
            int wrapCap = Math.Min(PreferredMaxContentWidth, cap);

            bool hasCancel = cancelLabelWidth >= 0;
            int seat = Math.Max(MinButtonWidth, Math.Max(0, confirmLabelWidth) + ButtonSidePadding);
            if (hasCancel)
            {
                seat = Math.Max(seat, cancelLabelWidth + ButtonSidePadding);
            }

            int rowWidth = hasCancel ? (2 * seat) + ButtonGap : seat;

            int floor = Math.Max(MinContentWidth, rowWidth + (2 * ButtonRowSideMargin));
            floor = Math.Max(floor, TitleTextIndent + Math.Max(0, titleWidth) + TitleRightReserve);

            int width = BalancedWidth(text, measureMessage, Math.Min(floor, wrapCap), wrapCap);
            width = Math.Min(Math.Max(width, floor), cap);

            var blocks = WrapBlocks(text, measureMessage, width, pitch, maxContentHeight);
            int messageBottom = blocks.Count == 0
                ? MessageTopMargin
                : blocks[blocks.Count - 1].Y + (blocks[blocks.Count - 1].Lines.Count * pitch);

            int contentHeight = messageBottom + MessageToButtonGap + ButtonHeight + ButtonBottomMargin;
            int buttonY = contentHeight - ButtonBottomMargin - ButtonHeight;
            int confirmX = Math.Max(0, (width - rowWidth) / 2);

            return new Layout(
                width, contentHeight, blocks, buttonY,
                confirmX, seat,
                hasCancel ? confirmX + seat + ButtonGap : 0, hasCancel ? seat : 0);
        }

        // A null or empty request is still one paragraph: TextWrapMath.Wrap
        // returns a single empty line for empty text, and a dialog that drew
        // no message row before must still draw one.
        private static IReadOnlyList<string> Normalize(IReadOnlyList<string> paragraphs)
        {
            if (paragraphs == null || paragraphs.Count == 0)
            {
                return new[] { string.Empty };
            }

            var kept = new List<string>(paragraphs.Count);
            foreach (string paragraph in paragraphs)
            {
                kept.Add(paragraph ?? string.Empty);
            }

            return kept;
        }

        /// <summary>
        /// The narrowest width in [<paramref name="low"/>,
        /// <paramref name="high"/>] that wraps to no more lines than
        /// <paramref name="high"/> does. Greedy wrapping never produces
        /// fewer lines as the width shrinks, so the predicate is monotone
        /// and a binary search is exact. Runs once per Show, not per frame:
        /// about ten wraps of one message, each a handful of measurements.
        /// </summary>
        private static int BalancedWidth(
            IReadOnlyList<string> paragraphs, Func<string, int> measure, int low, int high)
        {
            if (low >= high)
            {
                return high;
            }

            int target = CountLines(paragraphs, measure, high);
            while (low < high)
            {
                int mid = low + ((high - low) / 2);
                if (CountLines(paragraphs, measure, mid) <= target)
                {
                    high = mid;
                }
                else
                {
                    low = mid + 1;
                }
            }

            return low;
        }

        private static int CountLines(
            IReadOnlyList<string> paragraphs, Func<string, int> measure, int width)
        {
            int total = 0;
            foreach (string paragraph in paragraphs)
            {
                total += TextWrapMath.Wrap(paragraph, width, width, measure, int.MaxValue).Lines.Count;
            }

            return total;
        }

        /// <summary>
        /// Wraps every paragraph at the chosen width, spending a shared line
        /// budget in order so the box cannot outgrow the screen. Every
        /// paragraph keeps at least one line even when the budget is gone -
        /// dropping one would change what the dialog says, and a dialog too
        /// tall for the screen it is centred on is still readable at the top.
        /// </summary>
        private static List<MessageBlock> WrapBlocks(
            IReadOnlyList<string> paragraphs, Func<string, int> measure,
            int width, int pitch, int maxContentHeight)
        {
            int gaps = ParagraphGap * (paragraphs.Count - 1);
            int available = maxContentHeight - MessageTopMargin - gaps
                - MessageToButtonGap - ButtonHeight - ButtonBottomMargin;
            int budget = Math.Max(paragraphs.Count, available / pitch);

            var blocks = new List<MessageBlock>(paragraphs.Count);
            int y = MessageTopMargin;
            for (int i = 0; i < paragraphs.Count; i++)
            {
                int reserved = paragraphs.Count - 1 - i;
                var wrapped = TextWrapMath.Wrap(
                    paragraphs[i], width, width, measure, Math.Max(1, budget - reserved));
                budget -= wrapped.Lines.Count;

                blocks.Add(new MessageBlock(wrapped.Lines, y, wrapped.Truncated));
                y += (wrapped.Lines.Count * pitch) + ParagraphGap;
            }

            return blocks;
        }
    }
}
