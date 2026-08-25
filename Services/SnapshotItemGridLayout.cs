using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure placement arithmetic (Blish-free, unit-testable) for the
    /// Snapshot tab's result list: how many columns the content panel can
    /// hold, how wide one of them is, and where each result cell sits in
    /// the grid. The view (MainView.RebuildContent/LayoutResultGrid) only
    /// copies the results onto controls.
    /// <para>
    /// Same shape as <see cref="SettingsCurrencyGridLayout"/> - fixed cell
    /// height, reading order, a one-column fallback below a minimum column
    /// width - with two differences the Snapshot tab's data forces: cells
    /// are never individually hidden here (a search rebuilds the row set
    /// rather than filtering an always-present one, so the input is a
    /// count, not a name list), and the item and wallet runs are laid out
    /// as two grids because their row heights differ.
    /// </para>
    /// </summary>
    public static class SnapshotItemGridLayout
    {
        /// <summary>
        /// Left edge of a cell's text column: the 32px icon at x=2 plus its
        /// right gap. Same number MainView's rows have always used; it lives
        /// here so <see cref="MinColumnWidth"/> is derived from the geometry
        /// the cells are actually built with and cannot drift from it.
        /// </summary>
        public const int CellTextX = 40;

        /// <summary>Gap kept clear of a cell's right edge.</summary>
        public const int CellTextRightPad = 8;

        /// <summary>
        /// Width the vertical scrollbar of the scrolling content panel
        /// occupies. The grid is laid out inside the panel width minus this,
        /// so the right-hand column's text ellipsizes before it runs under
        /// the scrollbar - the same allowance LogTabContent applies to its
        /// own rows, and the same 20px the last term of
        /// <see cref="WindowSizing.WindowToTabPanelChrome"/> accounts for on
        /// tabs that pad their panel instead.
        /// </summary>
        public const int ScrollbarAllowance = 20;

        /// <summary>
        /// Upper bound on one character of the body font, which averages
        /// ~8.4px on item names at Font16 (measured: "Thermocatalytic
        /// Reagent" is 192px over 23 characters). Rounding up is what pays
        /// for the cell's breathing room. Was 8 against Font14's ~7.6px.
        /// </summary>
        public const int MaxCharWidthPx = 9;

        /// <summary>
        /// The run the narrowest column is sized to hold without
        /// ellipsizing: an item's NAME, 45 characters. It was 52 - a
        /// 7-character "9,999x " count prefix plus the name - until the
        /// count became its own right-pinned column, which is budgeted
        /// separately by <see cref="AmountColumnFloor"/>.
        /// <para>
        /// The BREAKDOWN line below it is deliberately NOT part of this
        /// budget. A full source breakdown ("Character: &lt;name&gt; 250
        /// Bank 250   Material Storage 2000") is unbounded in the roster's
        /// name lengths and already ellipsizes with the full text on the
        /// row's tooltip at every width; sizing a column to it would price
        /// the second column out of every window a player actually uses.
        /// Per column it simply ellipsizes earlier.
        /// </para>
        /// </summary>
        public const int NameRunChars = 45;

        /// <summary>
        /// Gap between a cell's flexing name and the Amount column pinned
        /// to its right - the same 12px the plan's own name columns keep
        /// before their trailing column.
        /// </summary>
        public const int CellAmountGap = 12;

        /// <summary>
        /// Width the Amount column is assumed to want when the minimum
        /// column width is derived. MEASURED, not chosen: the band's own
        /// floor is its header label, and "Amount" is 79px at the
        /// ColumnHeader tier (20 bold) - the same figure the plan's own
        /// header-floored bands were derived from. A run whose digits are
        /// wider than that ellipsizes its names a little earlier, which is
        /// what the name column flexing means.
        /// </summary>
        public const int AmountColumnFloor = 79;

        /// <summary>
        /// Narrowest column a cell fits in. Below twice this the grid falls
        /// back to a single column rather than clipping the name line.
        /// <para>
        /// 544px - the cell's whole width, term by term: the icon column,
        /// a 45-character name, the gap before the Amount column, that
        /// column's own floor, and the cell's right pad. Two columns fit
        /// inside the 1252px grid the 1378px window minimum leaves (626px
        /// each) and a third only once the window reaches 1758px.
        /// </para>
        /// </summary>
        public const int MinColumnWidth =
            CellTextX + (NameRunChars * MaxCharWidthPx) + CellAmountGap + AmountColumnFloor + CellTextRightPad;

        /// <summary>
        /// Right edge every cell's Amount column is pinned to. A cell is a
        /// table row one column wide, so it justifies the same way the plan
        /// tables do: the Amount edge is a function of the cell's width
        /// alone, and the name is the only part that flexes.
        /// </summary>
        public static int CellAmountRightEdge(int columnWidth)
        {
            return columnWidth - CellTextRightPad;
        }

        /// <summary>
        /// Width the Amount column reserves: the widest amount the run
        /// renders, floored at its own header label. A header at the
        /// ColumnHeader tier routinely out-measures the digits under it
        /// ("Amount" is 79px at 20-bold against a 32px "12x"), and a name
        /// budgeted against the digits alone would run under the header.
        /// </summary>
        public static int CellAmountBandWidth(int widestAmountWidth, int headerLabelWidth)
        {
            int band = widestAmountWidth > headerLabelWidth ? widestAmountWidth : headerLabelWidth;
            return band > 0 ? band : 0;
        }

        /// <summary>
        /// Where a cell's Name header cell ends and its Amount header cell
        /// begins: the gap between the flexing name and the Amount band,
        /// split down the middle. The name column IS everything left of
        /// that band, so a boundary taken between the two header WORDS
        /// would hand the pixels above the right-hand end of the names to
        /// the Amount header (see HeaderCellMath.LabelExtent).
        /// </summary>
        public static int CellHeaderSplitX(int columnWidth, int amountBandWidth)
        {
            return PlanRelayoutMath.HeaderSplitBeforeColumn(
                CellAmountRightEdge(columnWidth), amountBandWidth, CellAmountGap);
        }

        /// <summary>
        /// Width a cell's name line may occupy before the Amount column -
        /// the plan tables' own rule, applied to one grid cell rather than
        /// to a full-width row.
        /// </summary>
        public static int CellNameMaxWidth(int columnWidth, int amountBandWidth)
        {
            return PlanRelayoutMath.NameMaxWidthBeforeColumn(
                CellAmountRightEdge(columnWidth), amountBandWidth, CellAmountGap, CellTextX);
        }

        /// <summary>
        /// Width the cell's second line (an item's source breakdown) may
        /// occupy. It runs under the Amount column rather than stopping at
        /// it: the amount is one short line at the top of the cell, and the
        /// breakdown is the row's own unbounded text.
        /// </summary>
        public static int CellFullLineMaxWidth(int columnWidth)
        {
            int width = CellAmountRightEdge(columnWidth) - CellTextX;
            return width > 20 ? width : 20;
        }

        public readonly struct CellPlacement
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Column;
            public readonly int Row;

            public CellPlacement(int x, int y, int column, int row)
            {
                X = x;
                Y = y;
                Column = column;
                Row = row;
            }
        }

        public sealed class Grid
        {
            public IReadOnlyList<CellPlacement> Cells { get; }
            public int ColumnCount { get; }
            public int ColumnWidth { get; }
            public int RowCount { get; }
            public int Height { get; }

            internal Grid(
                IReadOnlyList<CellPlacement> cells, int columnCount, int columnWidth, int rowCount, int height)
            {
                Cells = cells;
                ColumnCount = columnCount;
                ColumnWidth = columnWidth;
                RowCount = rowCount;
                Height = height;
            }
        }

        /// <summary>
        /// Width the grid is laid out in, given the scrolling content
        /// panel's own width - see <see cref="ScrollbarAllowance"/>.
        /// </summary>
        public static int ComputeGridWidth(int contentWidth)
        {
            int width = contentWidth - ScrollbarAllowance;
            return width > 0 ? width : 0;
        }

        /// <summary>
        /// As many whole <see cref="MinColumnWidth"/> columns as fit, never
        /// fewer than one. Not capped at two: the count is derived from the
        /// width the player gave the window, so a wide window gets three or
        /// more columns and every one of them is still at least
        /// MinColumnWidth across.
        /// </summary>
        public static int ComputeColumnCount(int gridWidth)
        {
            int columns = gridWidth / MinColumnWidth;
            return columns > 1 ? columns : 1;
        }

        public static int ComputeColumnWidth(int gridWidth)
        {
            return gridWidth > 0 ? gridWidth / ComputeColumnCount(gridWidth) : 0;
        }

        public static int ComputeHeight(int count, int gridWidth, int rowHeight)
        {
            int safeCount = count > 0 ? count : 0;
            int columnCount = ComputeColumnCount(gridWidth);
            int rowCount = (safeCount + columnCount - 1) / columnCount;
            return rowCount * (rowHeight > 0 ? rowHeight : 0);
        }

        /// <summary>
        /// One placement per cell, in input order, packed left-to-right then
        /// top-to-bottom - reading order, so the single-column list the tab
        /// shipped with is exactly the one-column case of this grid.
        /// </summary>
        /// <param name="offsetY">
        /// Y the section starts at. The wallet run is laid out at the item
        /// run's <see cref="Grid.Height"/> so it still reads after the items
        /// above it, in the same grid panel and at the same column count.
        /// <see cref="Grid.Height"/> is the section's OWN height and never
        /// includes this offset.
        /// </param>
        public static Grid Compute(int count, int gridWidth, int rowHeight, int offsetY = 0)
        {
            int columnCount = ComputeColumnCount(gridWidth);
            int columnWidth = ComputeColumnWidth(gridWidth);
            int safeRowHeight = rowHeight > 0 ? rowHeight : 0;
            int safeCount = count > 0 ? count : 0;

            var cells = new CellPlacement[safeCount];
            for (int i = 0; i < safeCount; i++)
            {
                int row = i / columnCount;
                int column = i % columnCount;
                cells[i] = new CellPlacement(
                    column * columnWidth, offsetY + (row * safeRowHeight), column, row);
            }

            int rowCount = (safeCount + columnCount - 1) / columnCount;
            return new Grid(cells, columnCount, columnWidth, rowCount, rowCount * safeRowHeight);
        }
    }
}
