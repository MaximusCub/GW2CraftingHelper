using System.Collections.Generic;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Placement arithmetic (Blish-free, unit-testable) for the plan tab's
    /// multi-item input strip: how many input cells a panel width holds,
    /// where item i's cell sits, where the controls inside a cell sit, and
    /// where the add button goes.
    /// <para>
    /// The strip used to spend one full-width row per item, so a five-item
    /// plan pinned 175px of header above the content. Cells fill in
    /// reading order - row-major - which is the order with the fewest rows
    /// at every item count: filling a column first spends a second row on
    /// the second item, where row-major spends one on the first
    /// <see cref="ColumnCount"/>.
    /// </para>
    /// <para>
    /// A cell reads [Qty: label] [quantity box] [name box] [remove
    /// button]; why quantity leads is on <see cref="NameToButtonGap"/>.
    /// Column count comes from <see cref="GridLayout"/>, the module's one
    /// column-grid law.
    /// </para>
    /// </summary>
    internal static class ItemInputGridLayout
    {
        /// <summary>Gap between one cell's remove button and the next
        /// cell's "Qty:" label. Wider than any gap inside a cell so the eye
        /// groups a cell's own controls before it groups the cells.</summary>
        public const int ColumnGap = 12;

        /// <summary>Ink width of the "Qty:" label at the body face
        /// (Menomonia 16 regular with Blish's LetterSpacing of -1), read
        /// off the shipped XNB glyph advances.</summary>
        public const int QtyLabelInk = 28;

        /// <summary>Gap between the "Qty:" label and the box it
        /// names.</summary>
        public const int LabelToQtyGap = 6;

        /// <summary>Band the leading "Qty:" label occupies - its ink plus
        /// the gap to the quantity box.</summary>
        public const int QtyLabelBand = QtyLabelInk + LabelToQtyGap;

        /// <summary>
        /// Blish's own <c>TextBox.TEXT_HORIZONTALPADDING</c>, read off the
        /// shipped assembly: the inset between a text box's edge and its
        /// text, budgeted for BOTH sides because that is the reading that
        /// can only leave headroom, never take it.
        /// </summary>
        public const int TextBoxSidePadding = 10;

        /// <summary>
        /// Widest three-digit run at the quantity box's face - a text box
        /// paints in Menomonia 14 regular, whose widest digit advances 9px
        /// and 8px after letter spacing, so "000" measures 26.
        /// </summary>
        public const int QtyDigitRunWidth = 26;

        /// <summary>
        /// Quantity box, sized to the digits it must hold rather than to a
        /// round number: <see cref="QtyDigitRunWidth"/> plus the box's own
        /// padding a side. That is a three-digit quantity in the widest
        /// digits and more in narrower ones - the capacity the 50px box
        /// this replaces actually had, at 4px less width.
        /// </summary>
        public const int QtyBoxWidth = QtyDigitRunWidth + (2 * TextBoxSidePadding);

        /// <summary>Gap between the quantity box and the name box after
        /// it.</summary>
        public const int QtyToNameGap = 8;

        /// <summary>
        /// Gap between the name box and the cell's remove button. The
        /// button pair used to sit beside the QUANTITY box, where "-" and
        /// "+" read as its stepper rather than as row controls; leading
        /// with quantity is what moved them off it, so this gap no longer
        /// has to carry that separation on its own.
        /// </summary>
        public const int NameToButtonGap = 8;

        /// <summary>
        /// Narrowest a cell's name box may get, and therefore what sets the
        /// column count. Derived from the shipped item corpus
        /// (ref/item_name_seed.json, 14,766 names) measured at the box's own
        /// face: 250px shows 97.5% of them whole, against 62% at the 154px
        /// a fourth column would leave. Three cells at
        /// <see cref="MinCellWidth"/> come to 1158 of the 1192 the 1378px
        /// window minimum leaves this strip; a fourth needs a 1730px window.
        /// </summary>
        public const int MinSearchBoxWidth = 250;

        /// <summary>
        /// A cell is one strip row tall, so a grid row is exactly the row
        /// the strip always used - read from the same constant the top
        /// region's Y arithmetic uses rather than re-aliased.
        /// </summary>
        public const int RowHeight = TopRegionLayoutMath.TopRegionRowHeight;

        /// <summary>Floor for the search box on a panel too narrow for even
        /// one whole cell. A control sized at or below zero is dropped by
        /// Blish rather than clipped, which would leave the strip with no
        /// visible input at all.</summary>
        private const int DegenerateControlWidth = 20;

        /// <summary>
        /// Everything in a cell that is not the name box: the leading
        /// "Qty:" band, the quantity box, the two gaps around the name box
        /// and the remove button.
        /// <paramref name="buttonSize"/> is the caller's measurement - the
        /// Views layer owns the module's button height and this layer may
        /// not name it.
        /// </summary>
        public static int CellChromeWidth(int buttonSize)
        {
            return QtyLabelBand + QtyBoxWidth + QtyToNameGap + NameToButtonGap
                + (buttonSize > 0 ? buttonSize : 0);
        }

        /// <summary>Narrowest whole column: a floor-width name box, the
        /// cell chrome beside it, and the gap to the next column.</summary>
        public static int MinCellWidth(int buttonSize)
        {
            return MinSearchBoxWidth + CellChromeWidth(buttonSize) + ColumnGap;
        }

        /// <summary>Width reserved at the strip's right edge for the add
        /// button in the one case where it cannot sit in the column after
        /// its own cell: a last item that fills the last column.</summary>
        public static int AddButtonGutter(int buttonSize)
        {
            return ColumnGap + (buttonSize > 0 ? buttonSize : 0);
        }

        /// <summary>
        /// Width the columns are divided out of, given the strip panel's
        /// own width: the panel less the tab's right-edge padding - the
        /// edge the plan tab already pins its separator and its Generate
        /// button to - and less the add button's gutter.
        /// </summary>
        public static int ColumnStripWidth(int panelWidth, int buttonSize)
        {
            int width = panelWidth - WindowSizing.RightEdgePadding - AddButtonGutter(buttonSize);
            return width > 0 ? width : 0;
        }

        /// <summary>Cells one panel width seats side by side, floored at one
        /// by <see cref="GridLayout.ColumnCount"/>. Uncapped above: a wider
        /// window seats more items per row, which is the whole point.</summary>
        public static int ColumnCount(int panelWidth, int buttonSize)
        {
            return GridLayout.ColumnCount(
                ColumnStripWidth(panelWidth, buttonSize), MinCellWidth(buttonSize));
        }

        /// <summary>
        /// Rows <paramref name="itemCount"/> items fill. Never zero: the
        /// add button lives on the last row, so an empty strip still has to
        /// reserve one for it.
        /// </summary>
        public static int RowCount(int itemCount, int panelWidth, int buttonSize)
        {
            int rows = GridLayout.RowCount(itemCount, ColumnCount(panelWidth, buttonSize));
            return rows > 0 ? rows : 1;
        }

        /// <summary>Height the strip's panel needs for that many
        /// items.</summary>
        public static int BlockHeight(int itemCount, int panelWidth, int buttonSize)
        {
            return RowCount(itemCount, panelWidth, buttonSize) * RowHeight;
        }

        /// <summary>Where one item's cell sits, relative to the strip
        /// panel's own top-left.</summary>
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

        /// <summary>
        /// One laid-out strip. The cell-interior offsets are on the grid
        /// rather than per cell because every cell is the same width: a
        /// caller places the same four controls at the same four offsets in
        /// each one.
        /// </summary>
        public sealed class Grid
        {
            public IReadOnlyList<CellPlacement> Cells { get; }

            public int ColumnCount { get; }

            /// <summary>Pitch between two cells' left edges - the cell plus
            /// <see cref="ItemInputGridLayout.ColumnGap"/>.</summary>
            public int ColumnWidth { get; }

            /// <summary>Width a cell's controls occupy, remove button
            /// included.</summary>
            public int CellWidth { get; }

            /// <summary>
            /// Width of the panel those controls are parented into: the
            /// whole column wherever that is the wider of the two, so the
            /// rightmost control never sits flush against the panel's own
            /// clipping edge.
            /// </summary>
            public int CellPanelWidth { get; }

            /// <summary>Leading control in a cell: the label names the box
            /// beside it, which a bare number box at the cell's head would
            /// not.</summary>
            public int QtyLabelX { get; }

            public int QtyBoxX { get; }

            public int SearchBoxX { get; }

            public int SearchBoxWidth { get; }

            /// <summary>Where the remove button sits in a cell. Reserved
            /// even on the single row that has none, so adding a second
            /// item cannot shift the first one's controls.</summary>
            public int RemoveButtonX { get; }

            public int RowCount { get; }

            public int Height { get; }

            /// <summary>
            /// Left edge of the add button: the column immediately after
            /// the last item's cell, so the button always sits exactly
            /// where the next item's search box will appear. A last item
            /// filling the last column pushes it into the reserved gutter
            /// instead of onto a new row - see
            /// <see cref="AddButtonGutter"/>.
            /// </summary>
            public int AddButtonX { get; }

            /// <summary>Top of the row the add button shares with the last
            /// item. The caller adds its own in-row offset, the same one it
            /// gives a cell's buttons.</summary>
            public int AddButtonY { get; }

            internal Grid(
                IReadOnlyList<CellPlacement> cells, int columnCount, int columnWidth, int cellWidth,
                int searchBoxX, int searchBoxWidth, int qtyLabelX, int qtyBoxX, int removeButtonX,
                int rowCount, int height, int addButtonX, int addButtonY)
            {
                Cells = cells;
                ColumnCount = columnCount;
                ColumnWidth = columnWidth;
                CellWidth = cellWidth;
                CellPanelWidth = columnWidth > cellWidth ? columnWidth : cellWidth;
                SearchBoxX = searchBoxX;
                SearchBoxWidth = searchBoxWidth;
                QtyLabelX = qtyLabelX;
                QtyBoxX = qtyBoxX;
                RemoveButtonX = removeButtonX;
                RowCount = rowCount;
                Height = height;
                AddButtonX = addButtonX;
                AddButtonY = addButtonY;
            }
        }

        /// <summary>
        /// The whole strip for one panel width and item count: one
        /// placement per item in reading order, the offsets every cell
        /// shares, and the add button's seat.
        /// </summary>
        public static Grid Compute(int itemCount, int panelWidth, int buttonSize)
        {
            int safeCount = itemCount > 0 ? itemCount : 0;
            int columnCount = ColumnCount(panelWidth, buttonSize);
            int columnWidth = GridLayout.ColumnWidth(
                ColumnStripWidth(panelWidth, buttonSize), columnCount);

            int chrome = CellChromeWidth(buttonSize);
            int searchBoxWidth = columnWidth - ColumnGap - chrome;
            if (searchBoxWidth < DegenerateControlWidth)
            {
                searchBoxWidth = DegenerateControlWidth;
            }

            int cellWidth = searchBoxWidth + chrome;
            const int qtyLabelX = 0;
            int qtyBoxX = qtyLabelX + QtyLabelBand;
            int searchBoxX = qtyBoxX + QtyBoxWidth + QtyToNameGap;
            int removeButtonX = searchBoxX + searchBoxWidth + NameToButtonGap;

            var cells = new CellPlacement[safeCount];
            for (int i = 0; i < safeCount; i++)
            {
                int row = i / columnCount;
                int column = i % columnCount;
                cells[i] = new CellPlacement(column * columnWidth, row * RowHeight, column, row);
            }

            int rowCount = RowCount(safeCount, panelWidth, buttonSize);
            int lastIndex = safeCount - 1;
            int addButtonX = safeCount == 0 ? 0 : ((lastIndex % columnCount) + 1) * columnWidth;
            int addButtonY = safeCount == 0 ? 0 : (lastIndex / columnCount) * RowHeight;

            return new Grid(
                cells, columnCount, columnWidth, cellWidth, searchBoxX, searchBoxWidth,
                qtyLabelX, qtyBoxX, removeButtonX, rowCount, rowCount * RowHeight,
                addButtonX, addButtonY);
        }
    }
}
