using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// One header row's cells, described once and re-split on demand. The
    /// labels, their measured widths and their sort actions are fixed for
    /// the life of the row; only the x's move, and they move on every frame
    /// of a resize drag (a right-pinned column's x is a function of the
    /// panel width).
    /// <para>
    /// So <see cref="Sync"/> reads each label's current Location, splits
    /// into buffers this instance owns, and hands the result to
    /// <see cref="SortableHeaderCells"/> - no MeasureString and no
    /// allocation per tick. That is the same rule the plan's relayout
    /// closures already keep: position-and-width work per tick, measuring
    /// at build and settle only.
    /// </para>
    /// </summary>
    internal sealed class HeaderCellPlan
    {
        private readonly SortableHeaderCells _cells;
        private readonly Label[] _labels;
        private readonly int[] _widths;
        private readonly Action[] _onClick;
        private readonly HeaderCellMath.LabelExtent[] _extents;
        private readonly HeaderCellMath.CellRange[] _ranges;
        private readonly SortableHeaderCells.Column[] _columns;

        internal HeaderCellPlan(int count, SortableHeaderCells cells)
        {
            _cells = cells;
            _labels = new Label[count];
            _widths = new int[count];
            _onClick = new Action[count];
            _extents = new HeaderCellMath.LabelExtent[count];
            _ranges = new HeaderCellMath.CellRange[count];
            _columns = new SortableHeaderCells.Column[count];
        }

        internal int Count => _labels.Length;

        /// <summary>
        /// One column, left to right. A null <paramref name="onClick"/> is
        /// an inert header (the Recipe Tree's "Source"): it still divides
        /// the band, so the cells beside it cannot claim its pixels, but it
        /// gets no wash and answers no click.
        /// </summary>
        internal void Set(int index, Label label, int width, Action onClick)
        {
            _labels[index] = label;
            _widths[index] = width;
            _onClick[index] = onClick;
        }

        internal void Sync(int bandWidth)
        {
            for (int i = 0; i < _labels.Length; i++)
            {
                _extents[i] = new HeaderCellMath.LabelExtent(_labels[i].Location.X, _widths[i]);
            }

            HeaderCellMath.Partition(bandWidth, _extents, _ranges);

            for (int i = 0; i < _labels.Length; i++)
            {
                _columns[i] = new SortableHeaderCells.Column(
                    _ranges[i].X, _ranges[i].Width, _labels[i], _onClick[i]);
            }

            _cells.Sync(_columns);
        }
    }
}
