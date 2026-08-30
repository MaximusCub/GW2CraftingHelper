using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// The scrolling viewport's hard top cutoff: one absolute logical y that
    /// every container inside the viewport re-asserts on the clip rectangle
    /// it was handed, so no descendant can paint above the viewport's own top
    /// edge whatever its nesting depth.
    /// <para>
    /// Why a re-assertion at every container rather than a taller strip: the
    /// derivation, and the measured single-container budget the line is
    /// offset by, are in <see cref="ClipCutoffMath"/>.
    /// </para>
    /// <para>
    /// The line lives in a static because <c>Container.Paint</c> is
    /// <c>sealed</c> and gives a subclass no seam to pass anything down its
    /// own subtree; painting is a single depth-first walk on the game's
    /// update thread, so <see cref="Enter"/>/<see cref="Exit"/> nest like a
    /// stack of one. Nothing outside a paint walk reads it.
    /// </para>
    /// </summary>
    internal static class ClipCutoff
    {
        private const int Inactive = int.MinValue;

        private static int _cutoffTop = Inactive;

        /// <summary>
        /// Puts <paramref name="cutoffTop"/> in force and returns the value
        /// to hand back to <see cref="Exit"/>. Nested viewports (a scrolling
        /// panel inside another tab's scrolling panel) therefore restore the
        /// outer line rather than clearing it.
        /// </summary>
        internal static int Enter(int cutoffTop)
        {
            int previous = _cutoffTop;
            _cutoffTop = cutoffTop;
            return previous;
        }

        internal static void Exit(int previous)
        {
            _cutoffTop = previous;
        }

        /// <summary>
        /// The clip a container should use in place of the one it inherited.
        /// A no-op when no viewport is painting, which is what makes the
        /// clamping control types below safe to use on any tab.
        /// </summary>
        internal static Rectangle Clamp(Rectangle scissor)
        {
            int cutoffTop = _cutoffTop;
            if (cutoffTop == Inactive)
            {
                return scissor;
            }

            int top = ClipCutoffMath.ClampTop(scissor.Y, cutoffTop);
            if (top == scissor.Y)
            {
                return scissor;
            }

            int height = scissor.Bottom - top;
            return new Rectangle(scissor.X, top, scissor.Width, height > 0 ? height : 0);
        }
    }

    /// <summary>
    /// A <see cref="Panel"/> that re-asserts <see cref="ClipCutoff"/> on the
    /// clip it is drawn with. Use it for every container built inside a
    /// scrolling viewport: a plain <c>Panel</c> in the chain hands its own
    /// children an edge that has drifted one round trip further up, and the
    /// drift is what accumulates with depth.
    /// </summary>
    internal class ClippedPanel : Panel
    {
        public override void Draw(SpriteBatch spriteBatch, Rectangle drawBounds, Rectangle scissor)
        {
            base.Draw(spriteBatch, drawBounds, ClipCutoff.Clamp(scissor));
        }
    }

    /// <summary>
    /// <see cref="ClippedPanel"/>'s flowing twin - see that type.
    /// </summary>
    internal class ClippedFlowPanel : FlowPanel
    {
        public override void Draw(SpriteBatch spriteBatch, Rectangle drawBounds, Rectangle scissor)
        {
            base.Draw(spriteBatch, drawBounds, ClipCutoff.Clamp(scissor));
        }
    }

    /// <summary>
    /// The scrolling viewport itself: it publishes the cutoff for the whole
    /// of its own subtree's paint, then restores whatever was in force.
    /// Its own drawing is unclamped - the line is derived from its bounds, so
    /// clamping itself against it would only shrink the viewport.
    /// </summary>
    internal sealed class ClipAuthorityFlowPanel : FlowPanel
    {
        public override void Draw(SpriteBatch spriteBatch, Rectangle drawBounds, Rectangle scissor)
        {
            int previous = ClipCutoff.Enter(ClipCutoffMath.CutoffTopFor(AbsoluteBounds.Y));
            try
            {
                base.Draw(spriteBatch, drawBounds, scissor);
            }
            finally
            {
                ClipCutoff.Exit(previous);
            }
        }
    }
}
