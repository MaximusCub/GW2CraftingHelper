using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// Base for every action a table row draws at its right-hand edge,
    /// built from the very textures Blish paints a window's own close
    /// control with: "button-exit", and "button-exit-active" while the
    /// cursor is on it. One base rather than a control per action, so the
    /// X, the reorder carets and the tree's IGNORE toggle cannot answer
    /// the cursor in different colours or fade to different weights.
    /// <para>
    /// Blish blits the whole 32x32 into a title bar with room for it; a
    /// table row has none, so a key samples the ink rectangle alone, 1:1
    /// and unscaled (Services/GlyphButtonMetrics).
    /// </para>
    /// <para>
    /// A Control rather than a <see cref="FeedbackButton"/>: every layer
    /// that button paints - plate atlas, four border strips, icon, text -
    /// would be suppressed here, and the hover sweep it inherits is a
    /// tween across an atlas this texture pair has no frames in.
    /// </para>
    /// </summary>
    internal abstract class RowActionKey : Control
    {
        private static Texture2D _resting;
        private static Texture2D _active;

        /// <summary>
        /// How far a disabled key fades. Chosen to equal
        /// <see cref="PillColors.DimmedPillFactor"/>, and read from it so
        /// the two cannot drift: a tree row's IGNORE toggle is disabled
        /// exactly when that row is dimmed, so this fade IS the toggle's
        /// share of the row wash and lands it at the weight of the pills
        /// beside it. The tree therefore must not wash the key a second
        /// time - the product would put it well under its own neighbours.
        /// </summary>
        private const float DisabledDim = PillColors.DimmedPillFactor;

        protected RowActionKey()
        {
            Size = new Point(
                GlyphButtonMetrics.RowActionWidth, GlyphButtonMetrics.RowActionHeight);
            PressFeedback.Wire(this);
        }

        /// <summary>
        /// The face for the cursor's current state: the lit key while the
        /// cursor is over an enabled one, the resting key otherwise. Null
        /// when ContentService produced no texture at all, which every
        /// caller has to answer before it draws.
        /// </summary>
        protected Texture2D Face => MouseOver && Enabled ? Active : Resting;

        private static Texture2D Resting =>
            _resting ?? (_resting = GameService.Content.GetTexture("button-exit"));

        private static Texture2D Active =>
            _active ?? (_active = GameService.Content.GetTexture("button-exit-active"));

        /// <summary>
        /// <paramref name="color"/> faded if the key is switched off. Every
        /// layer a key paints goes through here, so a disabled key loses
        /// its plate and its mark together rather than one of the two.
        /// </summary>
        protected Color Dimmed(Color color)
        {
            return Enabled ? color : color * DisabledDim;
        }

        /// <summary>
        /// Blits one rectangle of the key texture. Clamped rather than
        /// trusted: ContentService answers a name it cannot find with its
        /// own error texture, whose size is not this one's, and a source
        /// rectangle reaching past a texture samples whatever the atlas
        /// page holds next.
        /// </summary>
        protected void DrawSlice(
            SpriteBatch spriteBatch,
            Texture2D texture,
            Rectangle destination,
            Rectangle source,
            Color color)
        {
            if (texture == null || destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            var clamped = Rectangle.Intersect(source, texture.Bounds);
            if (clamped.IsEmpty)
            {
                return;
            }

            spriteBatch.DrawOnCtrl(this, texture, destination, clamped, color);
        }
    }
}
