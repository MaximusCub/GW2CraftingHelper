using System;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// The mark beside a sortable column's header word: dim on every
    /// sortable column, solid and directional on the one the table is sorted
    /// by. Its own Label, NOT part of the header's text, because the two
    /// states differ only in <c>Control.Opacity</c> and a Label has one
    /// opacity for the whole string it draws.
    /// <para>
    /// That is also what keeps the header's width fixed across all three
    /// states: the slot is <see cref="SortIndicatorLayout.SlotWidth"/> wide
    /// whatever is in it. Callers lay out against
    /// <see cref="SortIndicatorLayout.BlockWidth"/>, never against the
    /// title's own measured width.
    /// </para>
    /// <para>
    /// On a corrupt install (no ref/glyphs.fnt) the glyphs draw nothing and
    /// advance zero pixels, so the text degrades to the mismatched ASCII
    /// pair <see cref="UiGlyphs.AsciiFallback"/> carries: worse typography,
    /// and the only mark saying which column is sorted.
    /// </para>
    /// </summary>
    internal sealed class SortIndicator
    {
        private readonly Label _label;
        private readonly int _slotWidth;
        private readonly int _ascendingWidth;
        private readonly int _descendingWidth;

        private int _slotX;
        private TableSortDirection _direction = TableSortDirection.None;

        private SortIndicator(
            Label label, int slotWidth, int ascendingWidth, int descendingWidth)
        {
            _label = label;
            _slotWidth = slotWidth;
            _ascendingWidth = ascendingWidth;
            _descendingWidth = descendingWidth;
        }

        /// <summary>The control, for the header cell that tints it alongside
        /// its word on hover.</summary>
        internal Label Label => _label;

        /// <summary>Width of a sortable header carrying this indicator.</summary>
        internal int BlockWidth(int titleWidth)
        {
            return SortIndicatorLayout.BlockWidth(titleWidth, _slotWidth);
        }

        /// <summary>
        /// Width a sortable header would occupy in <paramref name="font"/>,
        /// for a caller measuring before any control exists - the pre-scan
        /// every plan table floors its column bands with.
        /// </summary>
        internal static int BlockWidthFor(BitmapFont font, string title)
        {
            return SortIndicatorLayout.BlockWidth(Measure(font, title), SlotWidthFor(font));
        }

        internal static int SlotWidthFor(BitmapFont font)
        {
            return SortIndicatorLayout.SlotWidth(
                Measure(font, TextFor(TableSortDirection.Ascending)),
                Measure(font, TextFor(TableSortDirection.Descending)));
        }

        internal static SortIndicator Create(
            Container parent, BitmapFont font, Color color, int y)
        {
            int ascending = Measure(font, TextFor(TableSortDirection.Ascending));
            int descending = Measure(font, TextFor(TableSortDirection.Descending));

            var label = LabelHelpers.WithDescenderClearance(new Label()
            {
                Text = TextFor(TableSortDirection.None),
                Font = font,
                TextColor = color,
                Opacity = SortIndicatorLayout.OpacityFor(TableSortDirection.None),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, y),
                Parent = parent,
            });

            return new SortIndicator(
                label, SortIndicatorLayout.SlotWidth(ascending, descending), ascending, descending);
        }

        /// <summary>
        /// Points the indicator at <paramref name="direction"/>. Writes
        /// nothing when the direction has not moved: the two per-column
        /// callers that re-place a live band (the Snapshot's
        /// <c>RefreshHeaders</c>, the plan tables' relayout closures) run
        /// this far more often than a sort actually changes.
        /// </summary>
        internal void Apply(TableSortDirection direction)
        {
            if (direction == _direction)
            {
                return;
            }

            _direction = direction;
            _label.Text = TextFor(direction);
            _label.Opacity = SortIndicatorLayout.OpacityFor(direction);
            PlaceGlyph();
        }

        /// <summary>Seats the slot after a header word starting at
        /// <paramref name="blockX"/>.</summary>
        internal void PlaceAfter(int blockX, int titleWidth)
        {
            _slotX = SortIndicatorLayout.SlotX(blockX, titleWidth);
            PlaceGlyph();
        }

        private void PlaceGlyph()
        {
            int glyphWidth = _direction == TableSortDirection.Descending
                ? _descendingWidth
                : _ascendingWidth;
            _label.Location = new Point(
                SortIndicatorLayout.GlyphX(_slotX, _slotWidth, glyphWidth), _label.Location.Y);
        }

        private static string TextFor(TableSortDirection direction)
        {
            string glyph = SortIndicatorLayout.GlyphFor(direction);
            return UiFonts.GlyphsAvailable ? glyph : UiGlyphs.AsciiFallback(glyph);
        }

        private static int Measure(BitmapFont font, string text)
        {
            return (int)Math.Ceiling(font.MeasureString(text ?? "").Width);
        }
    }
}
