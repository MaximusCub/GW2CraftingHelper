using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure placement arithmetic (Blish-free, unit-testable) for the
    /// Settings tab's currency list: which rows a filter query keeps, and
    /// where each surviving row's cell sits in the one- or two-column grid.
    /// The view (SettingsTabContent.ApplyCurrencyFilter) only copies the
    /// results onto controls.
    /// </summary>
    public static class SettingsCurrencyGridLayout
    {
        /// <summary>
        /// Narrowest column a two-up cell still fits in: name (8 + 170),
        /// input (70), Clear checkbox and a short error tag - see
        /// SettingsTabContent's Cell* constants. Below twice this, the grid
        /// falls back to a single column rather than overlapping columns.
        /// </summary>
        public const int MinColumnWidth = 340;

        public readonly struct CellPlacement
        {
            public readonly bool Visible;
            public readonly int X;
            public readonly int Y;
            public readonly int Row;

            public CellPlacement(bool visible, int x, int y, int row)
            {
                Visible = visible;
                X = x;
                Y = y;
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
            public int VisibleCount { get; }

            internal Grid(
                IReadOnlyList<CellPlacement> cells, int columnCount, int columnWidth,
                int rowCount, int height, int visibleCount)
            {
                Cells = cells;
                ColumnCount = columnCount;
                ColumnWidth = columnWidth;
                RowCount = rowCount;
                Height = height;
                VisibleCount = visibleCount;
            }
        }

        public static int ComputeColumnCount(int panelWidth)
        {
            return panelWidth >= 2 * MinColumnWidth ? 2 : 1;
        }

        /// <summary>
        /// Case-insensitive substring match. A blank query matches
        /// everything (the unfiltered list); a null name never matches a
        /// non-blank query.
        /// </summary>
        public static bool Matches(string name, string filter)
        {
            string trimmed = filter == null ? null : filter.Trim();
            if (string.IsNullOrEmpty(trimmed)) return true;
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// One placement per name, in input order. Matching cells are packed
        /// left-to-right, top-to-bottom with no gap for the hidden ones, so
        /// a filtered list is as short as its match count allows.
        /// </summary>
        public static Grid Compute(IReadOnlyList<string> names, string filter, int panelWidth, int rowHeight)
        {
            int columnCount = ComputeColumnCount(panelWidth);
            int columnWidth = panelWidth > 0 ? panelWidth / columnCount : 0;
            int safeRowHeight = rowHeight > 0 ? rowHeight : 0;

            int count = names == null ? 0 : names.Count;
            var cells = new CellPlacement[count];
            int visible = 0;

            for (int i = 0; i < count; i++)
            {
                if (!Matches(names[i], filter))
                {
                    cells[i] = new CellPlacement(false, 0, 0, -1);
                    continue;
                }

                int row = visible / columnCount;
                int column = visible % columnCount;
                cells[i] = new CellPlacement(true, column * columnWidth, row * safeRowHeight, row);
                visible++;
            }

            int rowCount = (visible + columnCount - 1) / columnCount;
            return new Grid(cells, columnCount, columnWidth, rowCount, rowCount * safeRowHeight, visible);
        }
    }
}
