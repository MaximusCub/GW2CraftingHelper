using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure placement arithmetic (Blish-free, unit-testable) for the
    /// Settings tab's currency list: the horizontal extent of one cell,
    /// which rows a filter query keeps, and where each surviving row's cell
    /// sits in the grid. The view
    /// (SettingsTabContent.AddCurrencyRow / ApplyCurrencyFilter) only copies
    /// the results onto controls.
    /// </summary>
    public static class SettingsCurrencyGridLayout
    {
        // Horizontal layout of one currency cell. These live here rather
        // than in the view so SettingsCurrencyMinColumnWidth below is derived from the same
        // numbers the controls are placed with and cannot drift from them,
        // and so the arithmetic is covered by Blish-free tests.
        //
        // The cell justifies like a plan table row: the
        // [amount][Ignore][tag] block pins to the cell's own right edge (see
        // PlanRelayoutMath.PinnedRightEdge) and the NAME is the only part
        // that flexes. 16, not the 8 the name used to sit at, so the first
        // column's names line up with the section titles above them.
        public const int CellNameX = SettingsFormLayout.CellLeftPad;
        public const int CellInputWidth = 70;

        /// <summary>
        /// Gap between the amount box and the "Ignore" checkbox. Was 6, with
        /// the checkbox on 70; the label rename needed four more pixels and
        /// they came from here rather than from the cell's total extent.
        /// </summary>
        public const int CellInputToClearGap = 2;

        /// <summary>
        /// Room for the per-currency "Ignore" checkbox: its 32px of box and
        /// gap chrome plus the measured 42px of the word "Ignore".
        /// <para>
        /// Unchanged by the +2pt bump, and that is deliberate rather than an
        /// oversight: Blish_HUD.Controls.Checkbox has no Font property, so
        /// its label stays at DefaultFont14 whatever the module's body font
        /// is. Widening this slot for a Font16 "Ignore" would reserve 6px
        /// the control never paints.
        /// </para>
        /// </summary>
        public const int CellClearWidth = 74;

        /// <summary>
        /// Room for the widest string the cell's one tag slot shows:
        /// "default 3600" (12 characters; 3600 is the largest value in
        /// CurrencyDecisionDefaults), which the red "Invalid" tag and
        /// "ignored" both fit inside. 110, holding that string's measured
        /// 98px at Font16 with the same ~11% of slack the 100px slot gave
        /// its 89px at Font14.
        /// </summary>
        public const int CellTagWidth = 110;

        /// <summary>Gap between the flexing name and the pinned control
        /// block - the module's one name-to-column gap.</summary>
        public const int NameToControlGap = SettingsFormLayout.NameToControlGap;

        /// <summary>
        /// The run the narrowest column holds without ellipsizing a currency
        /// name: 22 characters at
        /// <see cref="SnapshotItemGridLayout.MaxCharWidthPx"/> - what the
        /// old fixed 190px name column was itself sized from ("Pristine
        /// Fractal Relic").
        /// </summary>
        public const int SettingsCurrencyNameRunChars = 22;

        public const int CellNameFloor = SettingsCurrencyNameRunChars * SnapshotItemGridLayout.MaxCharWidthPx;

        /// <summary>The pinned block: amount box, gap, Ignore checkbox, tag
        /// slot.</summary>
        public const int CellControlBlockWidth =
            CellInputWidth + CellInputToClearGap + CellClearWidth + CellTagWidth;

        /// <summary>
        /// Narrowest column a cell fits in, term by term: the name inset, a
        /// 22-character name floor, the name-to-control gap, the pinned
        /// control block, and the table right margin. Below this the grid
        /// falls back to a single column rather than clipping.
        /// </summary>
        public const int SettingsCurrencyMinColumnWidth =
            CellNameX + CellNameFloor + NameToControlGap + CellControlBlockWidth
            + PlanRelayoutMath.TableRightMargin;

        /// <summary>Right edge the cell's control block pins to.</summary>
        public static int CellRightEdge(int columnWidth)
        {
            return PlanRelayoutMath.PinnedRightEdge(columnWidth);
        }

        public static int CellTagX(int columnWidth)
        {
            return CellRightEdge(columnWidth) - CellTagWidth;
        }

        public static int CellClearX(int columnWidth)
        {
            return CellTagX(columnWidth) - CellClearWidth;
        }

        public static int CellInputX(int columnWidth)
        {
            return CellClearX(columnWidth) - CellInputToClearGap - CellInputWidth;
        }

        /// <summary>Width a currency name may occupy before the control
        /// block - the plan tables' rule, applied to one cell.</summary>
        public static int CellNameMaxWidth(int columnWidth)
        {
            return PlanRelayoutMath.NameMaxWidthBeforeColumn(
                CellRightEdge(columnWidth), CellControlBlockWidth, NameToControlGap, CellNameX);
        }

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

        /// <summary>The shared grid law at this grid's own
        /// <see cref="SettingsCurrencyMinColumnWidth"/> - see <see cref="GridLayout"/>.
        /// This grid used to state the law itself and disagreed with its
        /// sibling by holding 454px of content in a 1210px column at a wide
        /// window; delegating is what makes that unrepeatable.</summary>
        public static int ComputeColumnCount(int panelWidth)
        {
            return GridLayout.ColumnCount(panelWidth, SettingsCurrencyMinColumnWidth);
        }

        public static int ComputeColumnWidth(int panelWidth)
        {
            return GridLayout.ColumnWidth(panelWidth, ComputeColumnCount(panelWidth));
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
            int rowCount = GridLayout.RowCount(count, ComputeColumnCount(panelWidth));
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
            if (string.IsNullOrEmpty(trimmed))
            {
                return true;
            }

            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

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

            int rowCount = GridLayout.RowCount(visible, columnCount);
            return new Grid(cells, columnCount, columnWidth, rowCount, rowCount * safeRowHeight, visible);
        }
    }
}
