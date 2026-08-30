using System.Collections.Generic;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Whether a point inside a recipe-tree row lands on one of that row's
    /// decision pills (Blish-free, unit-testable). The row's expand/collapse
    /// handler and its press feedback both defer to this: a container
    /// receives every mouse event its children do, so the row must not
    /// answer a click the pill under the cursor is about to answer.
    /// <para>
    /// GEOMETRY, not hover state, and that is the whole point. Blish
    /// recomputes <c>Control.MouseOver</c> only when the mouse POSITION
    /// changes between frames (Views/Rendering/HoverChainResync), so a
    /// control built under a stationary cursor reads as un-hovered, and a
    /// control rebuilt by the click itself has no <c>Location</c> yet when
    /// the resync runs. A hover-based guard therefore answered "no pill
    /// here" and expanded the node instead of toggling the ignore.
    /// Coordinates read at click time cannot go stale that way.
    /// </para>
    /// </summary>
    internal static class TreeRowPillHitTest
    {
        /// <summary>One pill's rectangle in its row's own coordinates.</summary>
        public readonly struct PillBox
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Width;
            public readonly int Height;

            public PillBox(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        /// <summary>
        /// Whether (<paramref name="x"/>, <paramref name="y"/>) is inside
        /// the pill. Half-open on both axes, the same convention
        /// SortableHeaderCells splits a header band with, so two abutting
        /// pills can never both claim one pixel. A zero- or
        /// negative-extent box covers nothing.
        /// </summary>
        public static bool Covers(PillBox box, int x, int y)
        {
            return x >= box.X && x < box.X + box.Width
                && y >= box.Y && y < box.Y + box.Height;
        }

        /// <summary>
        /// Whether any of <paramref name="boxes"/> covers the point. A null
        /// or empty list is a row with no pills at all, which covers
        /// nothing.
        /// </summary>
        public static bool AnyCovers(IReadOnlyList<PillBox> boxes, int x, int y)
        {
            if (boxes == null)
            {
                return false;
            }

            for (int i = 0; i < boxes.Count; i++)
            {
                if (Covers(boxes[i], x, y))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
