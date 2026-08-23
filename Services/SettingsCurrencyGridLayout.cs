using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure placement arithmetic (Blish-free, unit-testable) for the
    /// Settings tab's currency list: the horizontal extent of one cell,
    /// which rows a filter query keeps, and where each surviving row's cell
    /// sits in the one- or two-column grid. The view
    /// (SettingsTabContent.AddCurrencyRow / ApplyCurrencyFilter) only copies
    /// the results onto controls.
    /// </summary>
    public static class SettingsCurrencyGridLayout
    {
        // Horizontal layout of one currency cell. These live here rather
        // than in the view so MinColumnWidth below is derived from the same
        // numbers the controls are placed with and cannot drift from them,
        // and so the arithmetic is covered by Blish-free tests.
        public const int CellNameX = 8;
        public const int CellNameWidth = 170;
        public const int CellInputX = CellNameX + CellNameWidth;
        public const int CellInputWidth = 70;
        public const int CellClearX = CellInputX + CellInputWidth + 6;

        /// <summary>
        /// Room for the "Clear" checkbox (box plus its label at
        /// DefaultFont14, ~7.7px per character).
        /// </summary>
        public const int CellClearWidth = 70;

        public const int CellTagX = CellClearX + CellClearWidth;

        /// <summary>
        /// Room for the widest string the cell's one tag slot shows:
        /// "default 3600" (12 characters; 3600 is the largest value in
        /// CurrencyDecisionDefaults), which the red "Invalid" tag and
        /// "cleared" both fit inside.
        /// </summary>
        public const int CellTagWidth = 100;

        /// <summary>
        /// Narrowest column a two-up cell fits in - the full extent of the
        /// cell above, tag included. Below twice this the grid falls back to
        /// a single column rather than clipping the right-hand cells.
        /// </summary>
        public const int MinColumnWidth = CellTagX + CellTagWidth;

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

        public static int ComputeColumnWidth(int panelWidth)
        {
            return panelWidth > 0 ? panelWidth / ComputeColumnCount(panelWidth) : 0;
        }

        /// <summary>
        /// Height of the UNFILTERED grid. The view keeps the grid panel at
        /// this height whatever the filter shows: Blish's Scrollbar resets
        /// the scroll position to top on any content-height change (its
        /// RecalculateLayout compares the previous scrollbar percent against
        /// the recomputed one), so a per-keystroke height would yank the tab
        /// back to the top on every filter character.
        /// </summary>
        public static int ComputeHeight(int count, int panelWidth, int rowHeight)
        {
            int columnCount = ComputeColumnCount(panelWidth);
            int safeCount = count > 0 ? count : 0;
            int rowCount = (safeCount + columnCount - 1) / columnCount;
            return rowCount * (rowHeight > 0 ? rowHeight : 0);
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
        /// <param name="alwaysShow">
        /// Optional per-name override, in the same order as names: an entry
        /// that is true is placed even when the filter rejects it. The view
        /// passes the rows carrying an unsaved invalid amount, so Save's
        /// "N invalid entries not saved" warning can never point at a tag
        /// the filter is hiding.
        /// </param>
        public static Grid Compute(
            IReadOnlyList<string> names, string filter, int panelWidth, int rowHeight,
            IReadOnlyList<bool> alwaysShow = null)
        {
            int columnCount = ComputeColumnCount(panelWidth);
            int columnWidth = ComputeColumnWidth(panelWidth);
            int safeRowHeight = rowHeight > 0 ? rowHeight : 0;

            int count = names == null ? 0 : names.Count;
            var cells = new CellPlacement[count];
            int visible = 0;

            for (int i = 0; i < count; i++)
            {
                bool forced = alwaysShow != null && i < alwaysShow.Count && alwaysShow[i];
                if (!forced && !Matches(names[i], filter))
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
