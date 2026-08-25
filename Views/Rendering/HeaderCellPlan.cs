using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// One header row's cells, described once and re-split on demand. The
    /// labels, their measured widths and their sort actions are fixed for
    /// the life of the row; only the x's move (a right-pinned column's x is
    /// a function of the panel width).
    /// <para>
    /// CANONICAL NOTE on how often that is, because this class has callers
    /// at two different rates and it is an easy one to get backwards. The
    /// PLAN's sections re-split on every frame of a resize drag: their
    /// closures go through ISectionRelayoutSink, and
    /// CraftingPlanView.ReplayRelayout replays them straight off Blish's
    /// Resized event. The SNAPSHOT re-splits once per drag, because
    /// MainView.ScheduleRowRefit trailing-debounces its whole re-layout.
    /// <see cref="Sync"/> is written to the stricter of the two: it reads
    /// each label's current Location, splits into buffers this instance
    /// owns, and hands the result to <see cref="SortableHeaderCells"/> - no
    /// MeasureString, no allocation.
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

        /// <summary>
        /// Where this column really ends, for a caller that knows: a
        /// column edge rather than the midpoint between two header words.
        /// A right-pinned column's edge moves with the panel, so this is
        /// written on every re-layout - an int, not a measurement.
        /// </summary>
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
