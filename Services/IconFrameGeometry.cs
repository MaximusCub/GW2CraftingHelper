namespace TaimisToolbench.Services
{
    /// <summary>
    /// The rectangles an OUTLINE icon frame paints: the border ring, and
    /// nothing inside it. Blish-free so the property the outline exists for -
    /// that no rectangle covers an interior pixel - is asserted without a
    /// graphics device.
    /// <para>
    /// A currency icon's art is mostly transparent (a coin, a shard, a
    /// sliver of crystal), so a filled frame plate behind it reads as a grey
    /// BACKGROUND rather than as a border. An item icon's art is a full-bleed
    /// bag-slot square and hides the same plate completely, which is why one
    /// shape cannot serve both.
    /// </para>
    /// </summary>
    internal static class IconFrameGeometry
    {
        /// <summary>One painted rectangle, in coordinates local to the frame.</summary>
        internal readonly struct Edge
        {
            internal readonly int X;
            internal readonly int Y;
            internal readonly int Width;
            internal readonly int Height;

            internal Edge(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        private static readonly Edge[] None = new Edge[0];

        /// <summary>
        /// A frame's border ring. Degenerate inputs answer with something
        /// drawable rather than throwing: a box with no room for an interior
        /// is all border, and a box with no size at all paints nothing.
        /// Width and height are taken separately so a frame that is not
        /// square draws its ring on its own edges rather than 2px inside
        /// one of them.
        /// </summary>
        public static Edge[] OutlineEdges(int width, int height, int thickness)
        {
            if (width <= 0 || height <= 0 || thickness <= 0)
            {
                return None;
            }

            if (2 * thickness >= width || 2 * thickness >= height)
            {
                return new[] { new Edge(0, 0, width, height) };
            }

            int inner = height - (2 * thickness);
            return new[]
            {
                new Edge(0, 0, width, thickness),
                new Edge(0, height - thickness, width, thickness),
                new Edge(0, thickness, thickness, inner),
                new Edge(width - thickness, thickness, thickness, inner),
            };
        }
    }
}
