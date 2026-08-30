using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The viewport's hard top cutoff, executable: that re-asserting one
    /// absolute line at every container bounds the whole subtree's reach at a
    /// constant, and that the constant is <see cref="ClipCutoffMath.SlipBudget"/>.
    /// <para>
    /// The propagation model is the same transcription of the decompiled
    /// Blish HUD 1.3.0 paint pipeline that ClipTopSlipSimulationTests runs -
    /// and it is the production one, so a divergence between what ships and
    /// what is proved here is not expressible.
    /// </para>
    /// </summary>
    public class ClipCutoffMathTests
    {
        /// <summary>The four GW2 UI Size scale factors Blish applies as its
        /// UIScaleMultiplier (Small / Normal / Large / Larger).</summary>
        private static readonly float[] Scales = { 0.81f, 0.897f, 1.0f, 1.103f };

        /// <summary>
        /// Absolute logical y values the viewport's top edge is swept over.
        /// The module window is draggable, so its screen y is not one number,
        /// and a cutoff that only held at some window positions would be no
        /// cutoff at all.
        /// </summary>
        private const int PhaseSweep = 4000;

        /// <summary>
        /// Deeper than any container chain the module can build, and
        /// deliberately far past it: the point of the clamp is that this
        /// number does not appear in the guarantee.
        /// </summary>
        private const int AbsurdDepth = 64;

        [Fact]
        public void OneContainerNeverLosesMoreThanTheBudget()
        {
            foreach (float scale in Scales)
            {
                for (int top = 0; top <= PhaseSweep; top++)
                {
                    int propagated = ClipCutoffMath.PropagateClipTop(top, scale);
                    Assert.True(
                        propagated <= top,
                        $"clip top fell at y={top}, scale {scale}");
                    Assert.True(
                        top - propagated <= ClipCutoffMath.SlipBudget,
                        $"one container lost {top - propagated}px at y={top}, scale {scale}");
                }
            }
        }

        [Fact]
        public void TheBudgetIsTightAtTheTwoSubUnityUiSizes()
        {
            // Falsifiable in both directions: a budget of 1 would be too
            // small at Small and Normal, and this is what fails if a future
            // edit trims it.
            Assert.Equal(2, WorstSingleContainerSlip(0.81f));
            Assert.Equal(2, WorstSingleContainerSlip(0.897f));
            Assert.Equal(1, WorstSingleContainerSlip(1.103f));
            Assert.Equal(0, WorstSingleContainerSlip(1.0f));
        }

        [Fact]
        public void ReAssertingTheCutoffBoundsTheReachRegardlessOfDepth()
        {
            // The whole claim. Every container clamps the edge it inherited
            // back to the cutoff before drawing, so the deepest a subtree can
            // reach is one container's loss below the line - at 64 levels
            // exactly as at 1.
            foreach (float scale in Scales)
            {
                for (int viewportTop = 0; viewportTop <= PhaseSweep; viewportTop++)
                {
                    int cutoff = ClipCutoffMath.CutoffTopFor(viewportTop);
                    int worst = cutoff;
                    int edge = cutoff;
                    for (int level = 0; level < AbsurdDepth; level++)
                    {
                        edge = ClipCutoffMath.PropagateClipTop(edge, scale);
                        if (edge < worst)
                        {
                            worst = edge;
                        }

                        edge = ClipCutoffMath.ClampTop(edge, cutoff);
                    }

                    Assert.True(
                        worst >= viewportTop,
                        $"a clamped chain reached {worst}, above viewport top {viewportTop}, scale {scale}");
                }
            }
        }

        [Fact]
        public void WithoutTheReAssertionTheReachKeepsGrowing()
        {
            // The counterfactual that makes the test above mean something:
            // the same model, the same scales, no clamp - and the error is
            // unbounded in depth, which is why a fixed gap sized against a
            // "deepest realistic tree" was never a guarantee.
            int shallow = WorstUnclampedSlip(4, 0.81f);
            int deep = WorstUnclampedSlip(AbsurdDepth, 0.81f);

            Assert.True(shallow > ClipCutoffMath.SlipBudget);
            Assert.True(deep > shallow * 4);
        }

        [Fact]
        public void TheLargeUiSizeIsTheFalsifiableHalf()
        {
            // An integer scale makes both round trips exact, so a report that
            // content still overdraws the strip at GW2 UI Size "Large" would
            // mean the cause is something other than this propagation.
            Assert.Equal(0, WorstUnclampedSlip(AbsurdDepth, 1.0f));
        }

        private static int WorstSingleContainerSlip(float scale)
        {
            int worst = 0;
            for (int top = 0; top <= PhaseSweep; top++)
            {
                int slip = top - ClipCutoffMath.PropagateClipTop(top, scale);
                if (slip > worst)
                {
                    worst = slip;
                }
            }

            return worst;
        }

        private static int WorstUnclampedSlip(int containerDepth, float scale)
        {
            int worst = 0;
            for (int top = 0; top <= PhaseSweep; top++)
            {
                int edge = top;
                for (int level = 0; level < containerDepth; level++)
                {
                    edge = ClipCutoffMath.PropagateClipTop(edge, scale);
                }

                int slip = top - edge;
                if (slip > worst)
                {
                    worst = slip;
                }
            }

            return worst;
        }
    }
}
