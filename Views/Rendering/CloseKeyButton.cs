using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// A row's X, drawn from the very textures Blish paints a window's own
    /// close control with: "button-exit", and "button-exit-active" while the
    /// cursor is on it. Same asset, same pixels, same size, so the two marks
    /// cannot read as two different controls.
    /// <para>
    /// Measured off the shipped textures: both are 32x32 with transparent
    /// padding, and the key's ink sits at (7, 6) and 21x23 across - a dark
    /// border around a 16x16 plate carrying a 13x13 cross. Blish blits the
    /// whole 32x32 into a title bar with room for it; a table row has none,
    /// so this blits the ink rectangle alone, 1:1 and unscaled
    /// (Services/GlyphButtonMetrics).
    /// </para>
    /// <para>
    /// A Control rather than a <see cref="FeedbackButton"/>: every layer that
    /// button paints - plate atlas, four border strips, icon, text - would be
    /// suppressed here, and the hover sweep it inherits is a tween across an
    /// atlas this texture pair has no frames in.
    /// </para>
    /// </summary>
    internal class CloseKeyButton : Control
    {
        private static Texture2D _resting;
        private static Texture2D _active;

        /// <summary>
        /// The ink rectangle inside the 32x32 texture, and the only region
        /// this control ever samples.
        /// </summary>
        private static readonly Rectangle Source = new Rectangle(
            GlyphButtonMetrics.CloseKeySourceX,
            GlyphButtonMetrics.CloseKeySourceY,
            GlyphButtonMetrics.RowActionWidth,
            GlyphButtonMetrics.RowActionHeight);

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

        private Color _tint = Color.White;

        internal CloseKeyButton()
        {
            Size = new Point(
                GlyphButtonMetrics.RowActionWidth, GlyphButtonMetrics.RowActionHeight);
            PressFeedback.Wire(this);
        }

        /// <summary>
        /// Multiplied into the key. White - the texture untouched - is the
        /// resting state, and the tree's IGNORE toggle sets PillColors'
        /// ignore-active amber while its item is ignored, the same filled-key
        /// signal the pill it replaced carried.
        /// <para>
        /// A MULTIPLY, not a fill: the hover texture is itself gold, so a
        /// state that replaced the plate colour outright would be the hover
        /// look standing still. Multiplied, an ON key is a darker amber than
        /// either resting beige or hover gold, and hovering an ON key still
        /// brightens it.
        /// </para>
        /// <para>
        /// A disabled key fades the tint with the rest of it rather than
        /// repainting over it, so the state survives being switched off.
        /// </para>
        /// </summary>
        internal Color Tint
        {
            get => _tint;
            set => SetProperty(ref _tint, value, false, nameof(Tint));
        }

        private static Texture2D Resting =>
            _resting ?? (_resting = GameService.Content.GetTexture("button-exit"));

        private static Texture2D Active =>
            _active ?? (_active = GameService.Content.GetTexture("button-exit-active"));

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var texture = MouseOver && Enabled ? Active : Resting;
            if (texture == null)
            {
                return;
            }

            // Clamped rather than trusted: ContentService answers a name it
            // cannot find with its own error texture, whose size is not this
            // one's, and a source rectangle reaching past a texture samples
            // whatever the atlas page holds next.
            var source = Rectangle.Intersect(Source, texture.Bounds);
            if (source.IsEmpty)
            {
                return;
            }

            spriteBatch.DrawOnCtrl(
                this,
                texture,
                bounds,
                source,
                Enabled ? _tint : _tint * DisabledDim);
        }
    }
}
