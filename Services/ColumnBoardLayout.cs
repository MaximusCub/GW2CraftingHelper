using System.Collections.Generic;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Packs variable-height blocks into as many equal min-width columns as
    /// a board can hold (Blish-free, unit-testable). The shared
    /// <see cref="GridLayout"/> law, with a per-row height instead of a
    /// uniform one: the Settings tab's four short sections are blocks of
    /// different heights that must not overlap each other.
    ///
    /// <para>
    /// Row-major reading order, each board row as tall as its tallest
    /// block - deliberately NOT shortest-column masonry. Masonry balances
    /// the columns better but re-sorts blocks as the width changes, and a
    /// settings section that jumps from column 2 to column 1 mid-resize-drag
    /// is worse than a ragged bottom. A block's position relative to its
    /// neighbours here changes only by WRAPPING.
    /// </para>
    /// </summary>
    internal static class ColumnBoardLayout
    {
        public readonly struct Placement
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Column;
            public readonly int Row;
            public readonly int Width;

            public Placement(int x, int y, int column, int row, int width)
            {
                X = x;
                Y = y;
                Column = column;
                Row = row;
                Width = width;
            }
        }

        public sealed class Board
        {
            public IReadOnlyList<Placement> Blocks { get; }

            public int ColumnCount { get; }

            public int ColumnWidth { get; }

            public int RowCount { get; }

            public int Height { get; }

            internal Board(
                IReadOnlyList<Placement> blocks, int columnCount, int columnWidth, int rowCount, int height)
            {
                Blocks = blocks;
                ColumnCount = columnCount;
                ColumnWidth = columnWidth;
                RowCount = rowCount;
                Height = height;
            }
        }

        /// <summary>The shared grid law at the caller's
        /// <paramref name="minColumnWidth"/> (see <see cref="GridLayout"/>),
        /// capped at <paramref name="blockCount"/>: a column no row ever
        /// puts a block in is stranded space by construction, which is the
        /// defect this class exists to remove.</summary>
        public static int ComputeColumnCount(int boardWidth, int minColumnWidth, int blockCount)
        {
            return GridLayout.ColumnCount(boardWidth, minColumnWidth, blockCount);
        }

        public static int ComputeColumnWidth(int boardWidth, int columnCount)
        {
            return GridLayout.ColumnWidth(boardWidth, columnCount);
        }

        /// <summary>
        /// One placement per block, in input order. Heights are the caller's
        /// own measurement of each block at the resulting column width, so a
        /// caller lays its blocks out first, then places them.
        /// </summary>
        public static Board Compute(
            IReadOnlyList<int> blockHeights, int boardWidth, int minColumnWidth, int rowGap)
        {
            int count = blockHeights == null ? 0 : blockHeights.Count;
            int columnCount = ComputeColumnCount(boardWidth, minColumnWidth, count);
            int columnWidth = ComputeColumnWidth(boardWidth, columnCount);
            int safeRowGap = rowGap > 0 ? rowGap : 0;

            var blocks = new Placement[count];
            int rowCount = GridLayout.RowCount(count, columnCount);
            int y = 0;

            for (int row = 0; row < rowCount; row++)
            {
                int first = row * columnCount;
                int rowHeight = 0;
                for (int i = first; i < count && i < first + columnCount; i++)
                {
                    int height = blockHeights[i] > 0 ? blockHeights[i] : 0;
                    if (height > rowHeight)
                    {
                        rowHeight = height;
                    }
                }

                for (int column = 0; column < columnCount; column++)
                {
                    int i = first + column;
                    if (i >= count)
                    {
                        break;
                    }

                    blocks[i] = new Placement(column * columnWidth, y, column, row, columnWidth);
                }

                y += rowHeight;
                if (row < rowCount - 1)
                {
                    y += safeRowGap;
                }
            }

            return new Board(blocks, columnCount, columnWidth, rowCount, y);
        }
    }
}
