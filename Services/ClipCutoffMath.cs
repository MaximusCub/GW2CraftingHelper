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
        /// Worst-case logical pixels one container's scissor round trip can
        /// lift the clip's top edge, over every GW2 UI Size: 2, at the two
        /// sub-unity ones. For a caller that knows the live scale,
        /// <see cref="SlipBudgetFor"/> is the number that actually applies -
        /// this constant over-clips by up to 2px at UI Size Large, where the
        /// round trip is exact.
        /// </summary>
        public const int SlipBudget = 2;

        /// <summary>
        /// Absolute logical y values <see cref="SlipBudgetFor"/> sweeps. The
        /// loss depends on the edge's phase against the scale, and a window
        /// is draggable, so the answer has to hold at every y a clip top can
        /// take - which a screen bounds well inside this.
        /// </summary>
        private const int PhaseSweep = 4096;

        private static float _budgetScale;
        private static int _budgetForScale = SlipBudget;

        /// <summary>
        /// The worst round-trip loss at <paramref name="scale"/>, measured
        /// over <see cref="PhaseSweep"/> rather than tabulated, so an
        /// unlisted UI Size gets its own true answer instead of the
        /// four-value table's worst. 0 at 1.0, where both floors are exact;
        /// 2 at 0.81 and 0.897; 1 at 1.103.
        /// <para>
        /// Cached in one slot because the scale is a player setting that
        /// changes rarely and this is read once per viewport per paint.
        /// The read and the write are both on the paint thread; a racing
        /// caller would only recompute.
        /// </para>
        /// </summary>
        public static int SlipBudgetFor(float scale)
        {
            // Negated rather than "<= 0f" so a NaN also falls back: casting
            // a NaN product to int is platform-defined, and one that landed
            // here would compute a budget from int.MinValue.
            if (!(scale > 0f))
            {
                return SlipBudget;
            }

            if (scale == _budgetScale)
            {
                return _budgetForScale;
            }

            int worst = 0;
            for (int top = 0; top <= PhaseSweep; top++)
            {
                int slip = top - PropagateClipTop(top, scale);
                if (slip > worst)
                {
                    worst = slip;
                }
            }

            _budgetForScale = worst;
            _budgetScale = scale;
            return worst;
        }

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
        /// <para>
        /// The budget is the live scale's, not the four-size worst case.
        /// The difference is the strip between the protected edge and the
        /// cutoff, which nothing is obliged to paint: at UI Size Large the
        /// constant reserved 2px that the round trip never loses, so
        /// scrolled rows were cut 2px below every viewport's top and 2px
        /// below every pinned sticky band. docs/ARCHITECTURE.md V.26.1.
        /// </para>
        /// </summary>
        public static int CutoffTopFor(int viewportTop, float scale)
        {
            return viewportTop + SlipBudgetFor(scale);
        }
    }
}
