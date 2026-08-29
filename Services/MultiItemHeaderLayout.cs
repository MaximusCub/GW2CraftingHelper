namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure overflow arithmetic (Blish-free, unit-testable) for the plan
    /// header's batch icon run: a multi-item plan names its first item and
    /// then stacks the remaining items' icons left-to-right after the
    /// title, with an ellipsis standing in for whatever does not fit.
    /// <para>
    /// Every measurement is the caller's - the icon's framed edge comes
    /// from <see cref="ItemIconTiers.FrameSize"/> and the ellipsis width
    /// from a Blish-bound MeasureString - so this arithmetic cannot drift
    /// from what the renderer actually lays out. Offsets are relative to
    /// the run's own left edge; the renderer adds its origin.
    /// </para>
    /// </summary>
    internal static class MultiItemHeaderLayout
    {
        /// <summary>
        /// Gap between two stacked icons, and between the last drawn icon
        /// and the ellipsis after it. Tighter than a row gutter on purpose:
        /// the run reads as one group belonging to the title beside it.
        /// </summary>
        public const int IconGap = 6;

        /// <summary>Gap between the end of the title text and the first
        /// stacked icon - wide enough that the run reads as a separate
        /// group rather than as punctuation on the title.</summary>
        public const int TextGap = 14;

        /// <summary>
        /// What the run draws: the first <see cref="VisibleCount"/> icons,
        /// then the ellipsis when <see cref="ShowsEllipsis"/> - which is
        /// exactly when <see cref="HiddenCount"/> is non-zero AND there was
        /// room for the marker. A window too narrow even for the ellipsis
        /// draws nothing rather than overprinting the title; the items are
        /// still named in the plan below.
        /// </summary>
        public readonly struct IconRun
        {
            public readonly int VisibleCount;
            public readonly int HiddenCount;
            public readonly bool ShowsEllipsis;

            /// <summary>Left edge of the ellipsis, relative to the run's
            /// own left edge. Meaningless unless
            /// <see cref="ShowsEllipsis"/>.</summary>
            public readonly int EllipsisOffset;

            /// <summary>Total width the run occupies, ellipsis
            /// included.</summary>
            public readonly int Width;

            public IconRun(int visibleCount, int hiddenCount, bool showsEllipsis, int ellipsisOffset, int width)
            {
                VisibleCount = visibleCount;
                HiddenCount = hiddenCount;
                ShowsEllipsis = showsEllipsis;
                EllipsisOffset = ellipsisOffset;
                Width = width;
            }
        }

        /// <summary>
        /// Left edge of the icon at <paramref name="index"/>, relative to
        /// the run's own left edge. The one place the pitch is written, so
        /// the build pass and the resize closure cannot place the same icon
        /// differently.
        /// </summary>
        public static int IconX(int index, int iconSize, int iconGap)
        {
            return index * (iconSize + iconGap);
        }

        /// <summary>
        /// Width of a run of <paramref name="count"/> icons with no
        /// ellipsis - gaps between them only, never a trailing one.
        /// </summary>
        public static int RunWidth(int count, int iconSize, int iconGap)
        {
            return count <= 0 ? 0 : (count * iconSize) + ((count - 1) * iconGap);
        }

        /// <summary>
        /// How many of <paramref name="itemCount"/> icons fit in
        /// <paramref name="availableWidth"/>, and where the ellipsis begins
        /// when they do not all fit.
        /// <para>
        /// The ellipsis is not free: once anything is hidden, the marker
        /// has to fit too, so the run reserves
        /// <paramref name="ellipsisWidth"/> plus one gap before counting
        /// icons. That is why the answer is not simply
        /// availableWidth / pitch - a run that would seat four icons seats
        /// three plus the marker that says there are more.
        /// </para>
        /// </summary>
        public static IconRun Plan(
            int itemCount, int availableWidth, int iconSize, int iconGap, int ellipsisWidth)
        {
            if (itemCount <= 0 || iconSize <= 0)
            {
                return new IconRun(0, itemCount > 0 ? itemCount : 0, false, 0, 0);
            }

            if (iconGap < 0)
            {
                iconGap = 0;
            }

            int fullWidth = RunWidth(itemCount, iconSize, iconGap);
            if (fullWidth <= availableWidth)
            {
                return new IconRun(itemCount, 0, false, 0, fullWidth);
            }

            // Everything below hides at least one item, so the marker is
            // mandatory. No room for it at all is the degenerate window:
            // nothing is drawn rather than something overprinting the title.
            if (ellipsisWidth > availableWidth)
            {
                return new IconRun(0, itemCount, false, 0, 0);
            }

            // n icons followed by the marker occupy n * (iconSize + iconGap)
            // + ellipsisWidth: each icon carries the gap that separates it
            // from whatever comes next, and something always does.
            int visible = (availableWidth - ellipsisWidth) / (iconSize + iconGap);
            if (visible > itemCount - 1)
            {
                visible = itemCount - 1;
            }

            int ellipsisOffset = visible * (iconSize + iconGap);
            return new IconRun(
                visible, itemCount - visible, true, ellipsisOffset, ellipsisOffset + ellipsisWidth);
        }
    }
}
