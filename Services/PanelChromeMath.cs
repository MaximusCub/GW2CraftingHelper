namespace TaimisToolbench.Services
{
    /// <summary>
    /// Blish-free mirror of the arithmetic Blish_HUD.Controls.Panel uses to
    /// derive its ContentRegion from its own size, so a caller that has just
    /// RESIZED a panel can size that panel's children without reading the
    /// panel's ContentRegion back.
    /// <para>
    /// Reading it back is not safe, which is the whole reason this exists: a
    /// window that resizes itself from inside its own layout pass reaches
    /// its Resized subscribers with the child panel's ContentRegion still
    /// describing the PREVIOUS size, and nothing re-reads it afterwards, so
    /// sizes computed from it stay wrong (KNOWN-ISSUES #65). Derivation:
    /// docs/ARCHITECTURE.md section 4.1.
    /// </para>
    /// <para>
    /// The vendor constants are not duplicated here: the one caller
    /// (Views/ViewAdapter) passes Blish's own public Panel constants in, so
    /// this stays arithmetic and the numbers stay the vendor's.
    /// </para>
    /// </summary>
    internal static class PanelChromeMath
    {
        /// <summary>
        /// The four edge insets a Panel's ContentRegion is inset by, in the
        /// order Panel.RecalculateLayout derives them.
        /// </summary>
        public readonly struct Insets
        {
            public readonly int Top;
            public readonly int Right;
            public readonly int Bottom;
            public readonly int Left;

            public Insets(int top, int right, int bottom, int left)
            {
                Top = top;
                Right = right;
                Bottom = bottom;
                Left = left;
            }
        }

        /// <summary>
        /// The insets Blish gives a panel with the supplied chrome. Mirrors
        /// Panel.RecalculateLayout exactly: a title reserves a header band,
        /// a border adds the four paddings and raises the top inset to at
        /// least the top padding, and a panel with neither is inset by
        /// nothing at all - which is why the ContentRegion of a bare panel
        /// equals its size and the ContentRegion of a titled, bordered one
        /// does not.
        /// </summary>
        public static Insets PanelInsets(
            bool showBorder,
            bool hasTitle,
            int headerHeight,
            int topPadding,
            int rightPadding,
            int bottomPadding,
            int leftPadding)
        {
            int top = hasTitle ? headerHeight : 0;
            if (!showBorder)
            {
                return new Insets(top, 0, 0, 0);
            }

            return new Insets(
                top > topPadding ? top : topPadding,
                rightPadding,
                bottomPadding,
                leftPadding);
        }

        /// <summary>
        /// Width of the content region of a panel that measures
        /// <paramref name="outerWidth"/>, floored at 0.
        /// <para>
        /// The floor is not cosmetic: Control.Size silently IGNORES a
        /// negative component, so a caller that passed one through would
        /// leave the child at whatever size it already had - the same
        /// stale-size failure this class exists to prevent, just reached
        /// from the other end.
        /// </para>
        /// </summary>
        public static int ContentWidth(int outerWidth, Insets insets)
        {
            return AtLeastZero(outerWidth - insets.Left - insets.Right);
        }

        /// <summary>
        /// Height of the content region of a panel that measures
        /// <paramref name="outerHeight"/>, floored at 0 for the reason
        /// <see cref="ContentWidth"/> gives.
        /// </summary>
        public static int ContentHeight(int outerHeight, Insets insets)
        {
            return AtLeastZero(outerHeight - insets.Top - insets.Bottom);
        }

        /// <summary>
        /// The content region of a panel that measures
        /// <paramref name="outer"/>, less <paramref name="pad"/> on all four
        /// edges - the size of a child inset by <paramref name="pad"/> inside
        /// that content region. Floored at 0 on both axes.
        /// </summary>
        public static int PaddedContentWidth(int outer, Insets insets, int pad)
        {
            return AtLeastZero(ContentWidth(outer, insets) - (2 * pad));
        }

        /// <summary>
        /// The vertical counterpart of <see cref="PaddedContentWidth"/>.
        /// </summary>
        public static int PaddedContentHeight(int outer, Insets insets, int pad)
        {
            return AtLeastZero(ContentHeight(outer, insets) - (2 * pad));
        }

        private static int AtLeastZero(int value)
        {
            return value > 0 ? value : 0;
        }
    }
}
