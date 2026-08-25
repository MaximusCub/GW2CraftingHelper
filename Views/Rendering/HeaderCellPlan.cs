using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// One header row's cells, described once and re-split on demand: the
    /// labels, their widths and their sort actions are fixed for the life
    /// of the row, and only the x's move.
    /// <para>
    /// CANONICAL NOTE on how often, because the two callers differ and it
    /// is easy to get backwards. The PLAN re-splits every frame of a drag
    /// (CraftingPlanView.ReplayRelayout replays its ISectionRelayoutSink
    /// closures off Blish's Resized event); the SNAPSHOT re-splits once per
    /// drag, trailing-debounced through MainView's ResizeSettleDebounce.
    /// <see cref="Sync"/> is written to the stricter: buffers this instance
    /// owns, no MeasureString, no allocation.
    /// </para>
    /// </summary>
    internal sealed class HeaderCellPlan
    {
        private readonly SortableHeaderCells _cells;
        private readonly Label[] _labels;
        private readonly int[] _widths;
        private readonly int[] _boundaries;
        private readonly Action[] _onClick;
        private readonly HeaderCellMath.LabelExtent[] _extents;
        private readonly HeaderCellMath.CellRange[] _ranges;
        private readonly SortableHeaderCells.Column[] _columns;

        internal HeaderCellPlan(int count, SortableHeaderCells cells)
        {
            _cells = cells;
            _labels = new Label[count];
            _widths = new int[count];
            _boundaries = new int[count];
            _onClick = new Action[count];
            _extents = new HeaderCellMath.LabelExtent[count];
            _ranges = new HeaderCellMath.CellRange[count];
            _columns = new SortableHeaderCells.Column[count];

            for (int i = 0; i < count; i++)
            {
                _boundaries[i] = HeaderCellMath.LabelExtent.NoBoundary;
            }
        }

        internal int Count => _labels.Length;

        /// <summary>One column, left to right. A null
        /// <paramref name="onClick"/> is an inert header (the Recipe Tree's
        /// "Source"): it still divides the band, so its neighbours cannot
        /// claim its pixels, but it washes and answers nothing.</summary>
        internal void Set(int index, Label label, int width, Action onClick)
        {
            _labels[index] = label;
            _widths[index] = width;
            _onClick[index] = onClick;
        }

        /// <summary>Where this column really ends, for a caller that
        /// knows: a column edge rather than a midpoint between two header
        /// words. Written on every re-layout - an int, not a
        /// measurement.</summary>
        internal void SetBoundary(int index, int cellEnd)
        {
            _boundaries[index] = cellEnd;
        }

        internal void Sync(int bandWidth)
        {
            for (int i = 0; i < _labels.Length; i++)
            {
                _extents[i] = new HeaderCellMath.LabelExtent(
                    _labels[i].Location.X, _widths[i], _boundaries[i]);
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
