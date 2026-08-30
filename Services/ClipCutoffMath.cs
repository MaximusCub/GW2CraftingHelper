using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The arithmetic behind the scrolling viewport's hard top cutoff
    /// (Blish-free, unit-testable).
    /// <para>
    /// Blish HUD 1.3.0 rebuilds every child's clip rectangle from the
    /// PHYSICAL scissor: <c>Control.Draw</c> writes
    /// <c>Intersect(scissor, AbsoluteBounds).ScaleBy(uiScale)</c> into
    /// <c>GraphicsDevice.ScissorRectangle</c>, and <c>Container.Paint</c> -
    /// which is <c>sealed</c> - reads it back and unscales it with
    /// <c>ScaleBy(1f / uiScale)</c> before handing it to
    /// <c>PaintChildren</c>. <c>ScaleBy</c> floors the origin after a
    /// float32 multiply, so that round trip is
    /// <c>floor(floor(y*s)/s) &lt;= y</c>: the clip's top edge can only ever
    /// RISE, never fall back. <c>PaintChildren</c> re-intersects with the
    /// container's own content region, which re-clamps the edge only when
    /// that container's own top is BELOW the drifted clip - false for every
    /// ancestor of a row scrolled out of view. The error is therefore in the
    /// PROPAGATION, and it accumulates once per nested container.
    /// </para>
    /// <para>
    /// <see cref="SlipBudget"/> is what that propagation can lose across ONE
    /// container, at any screen position and at every GW2 UI Size. A viewport
    /// that re-asserts an absolute cutoff line at every container it owns
    /// therefore bounds the whole subtree's reach at
    /// <c>cutoff - SlipBudget</c> no matter how deep the content nests - the
    /// property <c>ClipCutoffMathTests</c> proves, and the reason the fix is
    /// a re-asserted line rather than a gap sized against nesting depth.
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
