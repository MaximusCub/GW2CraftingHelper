using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// A row's X: Blish's own window close control blitted whole, so the
    /// two marks cannot read as two different controls.
    /// <para>
    /// Measured off the shipped textures: both are 32x32 with transparent
    /// padding, and the key's ink sits at (7, 6) and 21x23 across - a dark
    /// border around a 16x16 plate carrying a 13x13 cross.
    /// </para>
    /// </summary>
    internal class CloseKeyButton : RowActionKey
    {
        /// <summary>
        /// The ink rectangle inside the 32x32 texture, and the only region
        /// this control ever samples.
        /// </summary>
        private static readonly Rectangle Source = new Rectangle(
            GlyphButtonMetrics.CloseKeySourceX,
            GlyphButtonMetrics.CloseKeySourceY,
            GlyphButtonMetrics.RowActionWidth,
            GlyphButtonMetrics.RowActionHeight);

        private Color _tint = Color.White;

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

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var texture = Face;
            if (texture == null)
            {
                return;
            }

            DrawSlice(spriteBatch, texture, bounds, Source, Dimmed(_tint));
        }
    }
}
