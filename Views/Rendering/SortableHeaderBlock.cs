using System;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// One column header as a unit: the word, and - on a sortable column -
    /// the <see cref="SortIndicator"/> that always sits beside it. Every
    /// table's header row moves these two together, so the pair lives here
    /// rather than in each caller's own relayout closure.
    /// <para>
    /// <see cref="Width"/> is what a caller lays out against: it covers the
    /// indicator's reserved slot and is identical in all three sort states,
    /// which is what keeps a header from moving under the cursor at the
    /// moment of a click.
    /// </para>
    /// </summary>
    internal sealed class SortableHeaderBlock
    {
        private readonly Label _title;
        private readonly SortIndicator _indicator;
        private readonly int _titleWidth;

        private SortableHeaderBlock(Label title, SortIndicator indicator, int titleWidth)
        {
            _title = title;
            _indicator = indicator;
            _titleWidth = titleWidth;
        }

        internal Label Title => _title;

        /// <summary>The indicator's own control, or null on an inert column
        /// - what <see cref="HeaderCellPlan.Set"/> takes so the cell tints
        /// the pair together.</summary>
        internal Label IndicatorLabel => _indicator?.Label;

        /// <summary>Width of the whole block, indicator slot included.</summary>
        internal int Width =>
            _indicator == null ? _titleWidth : _indicator.BlockWidth(_titleWidth);

        /// <summary>
        /// Builds one header block. A null <paramref name="direction"/> is an
        /// INERT column: no indicator at all, rather than a dim one
        /// promising a click that does nothing. The "click to sort" note is
        /// the CALLER's - it is the half that knows whether a click is
        /// wired - and goes on both <see cref="Title"/> and
        /// <see cref="IndicatorLabel"/>.
        /// </summary>
        internal static SortableHeaderBlock Create(
            Container parent, BitmapFont font, Color color, int y, string title,
            TableSortDirection? direction)
        {
            var label = LabelHelpers.WithDescenderClearance(new Label()
            {
                Text = title ?? "",
                Font = font,
                TextColor = color,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, y),
                Parent = parent,
            });

            SortIndicator indicator = null;
            if (direction.HasValue)
            {
                indicator = SortIndicator.Create(parent, font, color, y);
                indicator.Apply(direction.Value);
            }

            return new SortableHeaderBlock(
                label, indicator, (int)Math.Ceiling(font.MeasureString(title ?? "").Width));
        }

        /// <summary>Seats the block with its word's left edge at
        /// <paramref name="x"/>. Position only - no measurement, so a
        /// per-drag-frame relayout can call it per column.</summary>
        internal void MoveTo(int x)
        {
            _title.Location = new Point(x, _title.Location.Y);
            _indicator?.PlaceAfter(x, _titleWidth);
        }

        /// <summary>Re-points the indicator after a sort click that did not
        /// rebuild the row - the Snapshot tab's in-place header
        /// refresh.</summary>
        internal void SetDirection(TableSortDirection direction)
        {
            _indicator?.Apply(direction);
        }

        /// <summary>Shows or hides the whole block, for a caller that keeps
        /// surplus headers alive across a resize rather than disposing
        /// them.</summary>
        internal void SetVisible(bool visible)
        {
            _title.Visible = visible;
            if (_indicator != null)
            {
                _indicator.Label.Visible = visible;
            }
        }
    }
}
