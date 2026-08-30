namespace TaimisToolbench.Services
{
    /// <summary>
    /// Where a table's column-header band is drawn while its rows are being
    /// scrolled past: pinned to the top of the viewport for as long as any
    /// of that table's rows are still in view, then pushed out by the end of
    /// the table itself.
    /// <para>
    /// Every coordinate here is VIEWPORT-RELATIVE - y=0 is the first visible
    /// scanline of the scrolling region - so this class never has to know
    /// how the scroll offset is applied. The caller derives the two inputs
    /// from the live position of the panel the rows live in.
    /// </para>
    /// <para>
    /// The band never rides HIGHER than the row it belongs to would have put
    /// it, which is what keeps it off the section above the table: while the
    /// real header is still on screen nothing is pinned at all, and the
    /// moment it leaves, the pinned copy takes over exactly where it was.
    /// </para>
    /// </summary>
    internal static class StickyHeaderLayout
    {
        /// <summary>
        /// One band's placement for one frame. Not pinned is the common
        /// case - one table in a scroll can be pinned at a time, and often
        /// none is.
        /// </summary>
        internal readonly struct Placement
        {
            /// <summary>Whether the band should be drawn pinned rather than
            /// at its own place in the scrolling content.</summary>
            public readonly bool Pinned;

            /// <summary>Viewport-relative top of the visible slice. Always
            /// 0 while pinned: a band that would sit lower is not pinned at
            /// all.</summary>
            public readonly int ClipY;

            /// <summary>Height of that slice - less than the band's own
            /// height while it is being pushed out by the end of its table,
            /// or on a viewport too short to hold a whole band.</summary>
            public readonly int VisibleHeight;

            /// <summary>How much of the band's TOP is cut away, so the
            /// caller can offset it inside a clip of
            /// <see cref="VisibleHeight"/>.</summary>
            public readonly int OffsetInBand;

            internal Placement(bool pinned, int clipY, int visibleHeight, int offsetInBand)
            {
                Pinned = pinned;
                ClipY = clipY;
                VisibleHeight = visibleHeight;
                OffsetInBand = offsetInBand;
            }
        }

        /// <summary>
        /// <paramref name="headerY"/> is where the band's own row sits;
        /// <paramref name="tableBottomY"/> is one pixel past the table's
        /// last row, which for a table with no rows at all is the band's own
        /// bottom - and such a table never pins, because there is nothing
        /// left for the band to label.
        /// </summary>
        public static Placement Compute(
            int headerY, int headerHeight, int tableBottomY, int viewportHeight)
        {
            if (headerHeight <= 0 || viewportHeight <= 0)
            {
                return default(Placement);
            }

            // Still on screen where it belongs. Pinning here would draw a
            // second copy over the first and, at the top of the scroll, over
            // the section heading above it.
            if (headerY >= 0)
            {
                return default(Placement);
            }

            // A table with no rows is its own header and nothing else. It
            // would pin to exactly where the real band already is, which
            // draws the same pixels - but it would also re-parent a band
            // every time a reader scrolled past an empty section, for no
            // visible difference at all.
            if (tableBottomY <= headerY + headerHeight)
            {
                return default(Placement);
            }

            // Pinned at the viewport top, EXCEPT while the end of the table
            // is pushing it back out - the band leaves with its last row
            // rather than surviving it.
            int top = tableBottomY - headerHeight;
            if (top > 0)
            {
                top = 0;
            }

            int offsetInBand = -top;
            if (offsetInBand >= headerHeight)
            {
                return default(Placement);
            }

            int visible = headerHeight - offsetInBand;
            if (visible > viewportHeight)
            {
                visible = viewportHeight;
            }

            return visible > 0
                ? new Placement(true, 0, visible, offsetInBand)
                : default(Placement);
        }
    }
}
