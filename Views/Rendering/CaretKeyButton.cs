using System;
using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// A row's reorder key: the key its X is cut from, carrying a caret
    /// instead of a cross. The cross is painted into the texture and
    /// cannot be sampled around, so the plate is rebuilt from three
    /// slices of the same art - the top frame with the bare plate rows
    /// under it, one of those rows repeated down the middle, and the
    /// bottom frame with its shadow - and the caret is drawn over the
    /// result. Frame, plate and the gold hover therefore come from
    /// Blish's own textures rather than from an imitation of them, and a
    /// row's three actions light up together.
    /// <para>
    /// The caret is one glyph from the module's own atlas
    /// (Services/UiGlyphs): Menomonia carries no triangles.
    /// </para>
    /// </summary>
    internal sealed class CaretKeyButton : RowActionKey
    {
        /// <summary>
        /// The cross's own ink, sampled from "button-exit". The caret has
        /// to carry the weight the X does, and the X is this same
        /// near-black on the resting plate and on the gold one.
        /// </summary>
        private static readonly Color Ink = new Color(8, 0, 0);

        private static readonly Rectangle TopCap = new Rectangle(
            GlyphButtonMetrics.CloseKeySourceX,
            GlyphButtonMetrics.CloseKeySourceY,
            GlyphButtonMetrics.RowActionWidth,
            GlyphButtonMetrics.KeyCapHeight);

        private static readonly Rectangle BottomCap = new Rectangle(
            GlyphButtonMetrics.CloseKeySourceX,
            GlyphButtonMetrics.CloseKeySourceY
                + GlyphButtonMetrics.RowActionHeight - GlyphButtonMetrics.KeyCapHeight,
            GlyphButtonMetrics.RowActionWidth,
            GlyphButtonMetrics.KeyCapHeight);

        private static readonly Rectangle PlateRow = new Rectangle(
            GlyphButtonMetrics.CloseKeySourceX,
            GlyphButtonMetrics.KeyPlateRowY,
            GlyphButtonMetrics.RowActionWidth,
            1);

        private readonly string _glyph;

        internal CaretKeyButton(string glyph)
        {
            // Normalised here rather than guarded in Paint: an empty string
            // measures and draws as nothing, where a null one throws inside
            // MeasureString once a frame.
            _glyph = glyph ?? string.Empty;
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var texture = Face;
            if (texture == null)
            {
                return;
            }

            var color = Dimmed(Color.White);
            int cap = GlyphButtonMetrics.KeyCapHeight;
            int fill = Math.Max(0, bounds.Height - (2 * cap));

            DrawSlice(
                spriteBatch, texture,
                new Rectangle(bounds.X, bounds.Y, bounds.Width, cap), TopCap, color);
            DrawSlice(
                spriteBatch, texture,
                new Rectangle(bounds.X, bounds.Y + cap, bounds.Width, fill), PlateRow, color);
            DrawSlice(
                spriteBatch, texture,
                new Rectangle(bounds.X, bounds.Y + cap + fill, bounds.Width, cap), BottomCap, color);

            PaintCaret(spriteBatch, bounds);
        }

        /// <summary>
        /// The caret, centred in the key. The standalone glyph face centres
        /// a glyph's ink in its line box rather than seating it on a
        /// baseline (Views/Rendering/GlyphFont), so centring the line box
        /// in the control centres the ink itself.
        /// </summary>
        private void PaintCaret(SpriteBatch spriteBatch, Rectangle bounds)
        {
            bool available = UiFonts.GlyphsAvailable;

            spriteBatch.DrawStringOnCtrl(
                this,
                available ? _glyph : UiGlyphs.AsciiFallback(_glyph),
                available ? UiFonts.Glyphs : UiFonts.Caption,
                bounds,
                Dimmed(Ink),
                false,
                HorizontalAlignment.Center,
                VerticalAlignment.Middle);
        }
    }
}
