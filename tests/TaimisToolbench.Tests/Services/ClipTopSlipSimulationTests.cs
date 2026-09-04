using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// What the pinned top strip's paint order was covering for, executable:
    /// the clip's top edge rising once per nested container, measured by
    /// depth and by GW2 UI Size.
    /// <para>
    /// The model is the same transcription of the decompiled Blish HUD 1.3.0
    /// paint pipeline that RowDividerScissorSimulationTests runs, applied to
    /// the other edge: that proof needed the reconstructed clip's BOTTOM,
    /// this one needs its TOP. docs/ARCHITECTURE.md section V.26 records
    /// both the transcription and the rule that a model must reproduce
    /// measured behaviour before it is trusted.
    /// </para>
    /// <para>
    /// These numbers are the DEFECT, not the fix. What bounds it is
    /// ClipCutoffMath's re-asserted line, proved depth-independently in
    /// ClipCutoffMathTests.
    /// </para>
    /// </summary>
    public class ClipTopSlipSimulationTests
    {
        /// <summary>The four GW2 UI Size scale factors Blish applies as its
        /// UIScaleMultiplier (Small / Normal / Large / Larger).</summary>
        private const float SmallScale = 0.81f;
        private const float NormalScale = 0.897f;
        private const float LargeScale = 1.0f;
        private const float LargerScale = 1.103f;

        /// <summary>
        /// Absolute logical Y values the content panel's top edge is swept
        /// over - the module window is draggable, so the strip's screen Y is
        /// not one number, and the float32 phase pattern repeats well inside
        /// this range at every scale.
        /// </summary>
        private const int PhaseSweep = 1200;

        /// <summary>
        /// One container's contribution, from the production model
        /// (Services/ClipCutoffMath.cs) rather than a copy of it: Control.Draw
        /// scales the clip to physical space, Container.Paint unscales it back
        /// to logical for the children. The Intersect with the container's own
        /// bounds is absent on purpose - it re-clamps only when the container's
        /// top is BELOW the clip, and a row scrolled above the viewport is
        /// exactly the case where every ancestor's top is above it.
        /// </summary>
        private static int PropagateClipTop(int clipTop, float scale)
        {
            return ClipCutoffMath.PropagateClipTop(clipTop, scale);
        }

        /// <summary>
        /// Worst-case pixels the clip's top edge rises over
        /// <paramref name="containerDepth"/> nested containers, over every
        /// swept screen position.
        /// </summary>
        private static int MaxSlip(int containerDepth, float scale)
        {
            int worst = 0;
            for (int top = 0; top < PhaseSweep; top++)
            {
                int clipTop = top;
                for (int level = 0; level < containerDepth; level++)
                {
                    clipTop = PropagateClipTop(clipTop, scale);
                }

                int slip = top - clipTop;
                if (slip > worst)
                {
                    worst = slip;
                }
            }

            return worst;
        }

        [Fact]
        public void TheClipTopOnlyEverRises()
        {
            // floor(floor(y*s)/s) <= y, the inequality section V.26 states.
            // It is the whole defect: nothing in the pipeline can push the
            // clip's top back down, so the error is one-directional and
            // cumulative.
            foreach (float scale in new[] { SmallScale, NormalScale, LargeScale, LargerScale })
            {
                for (int top = 0; top < PhaseSweep; top++)
                {
                    Assert.True(
                        PropagateClipTop(top, scale) <= top,
                        $"clip top fell at y={top}, scale {scale}");
                }
            }
        }

        [Theory]
        [InlineData(SmallScale, new[] { 2, 3, 4, 5, 7, 8, 9, 10 })]
        [InlineData(NormalScale, new[] { 2, 3, 4, 5, 6, 7, 8, 9 })]
        [InlineData(LargerScale, new[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
        public void SlipGrowsWithEveryLevelOfNesting(float scale, int[] expectedByDepth)
        {
            for (int depth = 1; depth <= expectedByDepth.Length; depth++)
            {
                Assert.Equal(expectedByDepth[depth - 1], MaxSlip(depth, scale));
            }
        }

        [Fact]
        public void AtTheLargeUiSizeThereIsNoSlipAtAll()
        {
            // The falsifiable half of the diagnosis: an integer scale makes
            // both round trips exact. A report that the leak persists at GW2
            // UI Size "Large" would mean the cause is something else - and
            // the paint-order fix would still hold, which is why it was
            // chosen over anything scale-specific.
            for (int depth = 1; depth <= 20; depth++)
            {
                Assert.Equal(0, MaxSlip(depth, LargeScale));
            }
        }

        [Fact]
        public void NoFixedGapInTheStripCanBeWideEnough()
        {
            // Why the fix is a re-asserted cutoff line and not a taller
            // strip. A tree row sits inside one container per depth level
            // plus the section, the content panel and the tab's own panels,
            // so an UNCLAMPED slip is bounded only by how deep the recipe
            // tree goes - and the strip's whole separator-to-content gap is
            // already outrun by a shallow one. Sizing a gap against a
            // "deepest realistic tree" would be a guess, not a guarantee.
            int gap = TopRegionLayoutMath.SeparatorToContentGap;

            Assert.True(
                MaxSlip(4, SmallScale) >= gap,
                $"a four-deep nest already slips {MaxSlip(4, SmallScale)}px past a {gap}px gap");
            Assert.True(
                MaxSlip(20, SmallScale) > MaxSlip(10, SmallScale),
                "slip must keep growing with depth, or a fixed gap would be a fix");
        }

        [Fact]
        public void ADeepPlanReachesFarEnoughToDrawWholeRowsIntoTheStrip()
        {
            // Sanity on the scale of the reported defect rather than a
            // threshold to tune: a field capture showed whole tree rows
            // over the header, and a depth-14 chain sits about eighteen
            // containers down.
            Assert.True(MaxSlip(18, SmallScale) >= 16);
        }
    }
}
