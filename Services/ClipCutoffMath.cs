using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The arithmetic behind the scrolling viewport's hard top cutoff
    /// (Blish-free, unit-testable).
    /// <para>
    /// Blish 1.3.0 rebuilds a child's clip from the PHYSICAL scissor, and
    /// <c>ScaleBy</c> floors the origin after a float32 multiply, so the
    /// round trip is <c>floor(floor(y*s)/s) &lt;= y</c>: a clip's top edge
    /// can only ever RISE. The error is in the PROPAGATION and accumulates
    /// once per nested container.
    /// </para>
    /// <para>
    /// <see cref="SlipBudget"/> bounds what ONE container can lose, so a
    /// viewport re-asserting an absolute line at every container it owns
    /// bounds the whole subtree's reach at <c>cutoff - SlipBudget</c>
    /// whatever the nesting depth - the property
    /// <c>ClipCutoffMathTests</c> proves. Derivation and the measured
    /// per-scale numbers: docs/ARCHITECTURE.md section V.26.1.
    /// </para>
    /// </summary>
    internal static class ClipCutoffMath
    {
        /// <summary>
        /// Logical pixels one container's scissor round trip can lift the
        /// clip's top edge. 2 at both sub-unity GW2 UI Sizes (0.81 and
        /// 0.897), 1 at 1.103 and 0 at 1.0; the single number is the worst
        /// of them, because the active scale is the player's setting and can
        /// change without a rebuild.
        /// </summary>
        public const int SlipBudget = 2;

        /// <summary>
        /// One edge through <c>RectangleExtension.ScaleBy</c>: a float32
        /// multiply, then floor. The vendor ceils extents and floors
        /// origins; only the origin matters to a top edge.
        /// </summary>
        public static int ScaleEdge(int edge, float scale)
        {
            return (int)Math.Floor((float)edge * scale);
        }

        /// <summary>
        /// One container's contribution to the drift: <c>Control.Draw</c>
        /// scales the clip into physical space, <c>Container.Paint</c>
        /// unscales it back for the children.
        /// </summary>
        public static int PropagateClipTop(int clipTop, float scale)
        {
            return ScaleEdge(ScaleEdge(clipTop, scale), 1f / scale);
        }

        /// <summary>
        /// The scissor top a container hands on after re-asserting
        /// <paramref name="cutoffTop"/>: the cutoff wins whenever the
        /// inherited edge has drifted above it, and an edge already below it
        /// (a container nested further down the page, or a caller with no
        /// cutoff in force) is left alone.
        /// </summary>
        public static int ClampTop(int scissorTop, int cutoffTop)
        {
            return scissorTop < cutoffTop ? cutoffTop : scissorTop;
        }

        /// <summary>
        /// Where a subtree's cutoff line sits for a viewport whose own top
        /// edge is <paramref name="viewportTop"/>: one budget below it, so
        /// that the single round trip between a container re-asserting the
        /// line and its children receiving it lands ON the viewport's edge
        /// rather than above it.
        /// </summary>
        public static int CutoffTopFor(int viewportTop)
        {
            return viewportTop + SlipBudget;
        }
    }
}
