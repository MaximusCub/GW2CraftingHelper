using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class SnapshotItemGridLayoutTests
    {
        // MainView renders straight into its tab's content region and adds
        // no right-edge padding of its own, so the last (scrollbar) term of
        // WindowSizing's chain is the one SnapshotItemGridLayout applies
        // itself - i.e. the grid width at the window minimum is exactly the
        // panel width the rest of the module's layout math is derived
        // against.
        private static readonly int ContentWidthAtWindowMinimum =
            WindowSizing.TabPanelWidthFor(WindowSizing.MinWindowWidth)
                + SnapshotItemGridLayout.ScrollbarAllowance;

        private static readonly int GridWidthAtWindowMinimum =
            SnapshotItemGridLayout.ComputeGridWidth(ContentWidthAtWindowMinimum);

        [Fact]
        public void GridWidth_AtWindowMinimum_MatchesTheSharedChromeChain()
        {
            Assert.Equal(
                WindowSizing.TabPanelWidthFor(WindowSizing.MinWindowWidth),
                GridWidthAtWindowMinimum);
        }

        [Fact]
        public void MinColumnWidth_FitsTwoColumnsAtTheWindowMinimum()
        {
            Assert.Equal(2, SnapshotItemGridLayout.ComputeColumnCount(GridWidthAtWindowMinimum));
            Assert.True(2 * SnapshotItemGridLayout.MinColumnWidth <= GridWidthAtWindowMinimum);
            Assert.True(3 * SnapshotItemGridLayout.MinColumnWidth > GridWidthAtWindowMinimum);
        }

        [Fact]
        public void ColumnWidth_AtWindowMinimum_LeavesTheNameRunRoomToSpare()
        {
            int columnWidth = SnapshotItemGridLayout.ComputeColumnWidth(GridWidthAtWindowMinimum);

            int nameRunBudget =
                columnWidth - SnapshotItemGridLayout.CellTextX - SnapshotItemGridLayout.CellTextRightPad;

            Assert.True(
                SnapshotItemGridLayout.NameRunChars * SnapshotItemGridLayout.MaxCharWidthPx <= nameRunBudget,
                "a 52-character name line must not ellipsize in a column at the window minimum");
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-100, 1)]
        [InlineData(1, 1)]
        [InlineData(463, 1)]
        [InlineData(927, 1)]
        [InlineData(928, 2)]
        [InlineData(1391, 2)]
        [InlineData(1392, 3)]
        [InlineData(2320, 5)]
        public void ComputeColumnCount_AddsAColumnPerWholeMinColumnWidth(int gridWidth, int expected)
        {
            Assert.Equal(expected, SnapshotItemGridLayout.ComputeColumnCount(gridWidth));
        }

        [Fact]
        public void ComputeColumnCount_IsNotCappedAtTwo()
        {
            // The count is derived from the width the player gave the
            // window, not written down: an ultrawide window gets more
            // columns, and every one of them still clears MinColumnWidth.
            int columns = SnapshotItemGridLayout.ComputeColumnCount(3800);

            Assert.True(columns >= 4);
            Assert.True(SnapshotItemGridLayout.ComputeColumnWidth(3800) >= SnapshotItemGridLayout.MinColumnWidth);
        }

        [Fact]
        public void ComputeGridWidth_NeverGoesNegative()
        {
            Assert.Equal(0, SnapshotItemGridLayout.ComputeGridWidth(0));
            Assert.Equal(0, SnapshotItemGridLayout.ComputeGridWidth(SnapshotItemGridLayout.ScrollbarAllowance));
            Assert.Equal(0, SnapshotItemGridLayout.ComputeGridWidth(-500));
            Assert.Equal(80, SnapshotItemGridLayout.ComputeGridWidth(100));
        }

        [Fact]
        public void ComputeColumnWidth_ZeroOrNegativeWidth_IsZero()
        {
            Assert.Equal(0, SnapshotItemGridLayout.ComputeColumnWidth(0));
            Assert.Equal(0, SnapshotItemGridLayout.ComputeColumnWidth(-40));
        }

        [Fact]
        public void Compute_PacksInReadingOrder()
        {
            var grid = SnapshotItemGridLayout.Compute(5, 1000, 52);

            Assert.Equal(2, grid.ColumnCount);
            Assert.Equal(500, grid.ColumnWidth);
            Assert.Equal(3, grid.RowCount);
            Assert.Equal(156, grid.Height);

            // Left-to-right first, then down - NOT column-major, so the
            // wallet run still reads after the item run above it.
            Assert.Equal((0, 0, 0, 0), Cell(grid, 0));
            Assert.Equal((500, 0, 1, 0), Cell(grid, 1));
            Assert.Equal((0, 52, 0, 1), Cell(grid, 2));
            Assert.Equal((500, 52, 1, 1), Cell(grid, 3));
            Assert.Equal((0, 104, 0, 2), Cell(grid, 4));
        }

        [Fact]
        public void Compute_NarrowPanel_IsOneCellPerRow()
        {
            var grid = SnapshotItemGridLayout.Compute(3, 600, 36);

            Assert.Equal(1, grid.ColumnCount);
            Assert.Equal(600, grid.ColumnWidth);
            Assert.Equal(3, grid.RowCount);
            Assert.Equal(108, grid.Height);

            Assert.Equal((0, 0, 0, 0), Cell(grid, 0));
            Assert.Equal((0, 36, 0, 1), Cell(grid, 1));
            Assert.Equal((0, 72, 0, 2), Cell(grid, 2));
        }

        [Fact]
        public void Compute_ThreeColumns_FillsTheLastRowPartially()
        {
            var grid = SnapshotItemGridLayout.Compute(4, 1500, 52);

            Assert.Equal(3, grid.ColumnCount);
            Assert.Equal(500, grid.ColumnWidth);
            Assert.Equal(2, grid.RowCount);
            Assert.Equal(104, grid.Height);
            Assert.Equal((0, 52, 0, 1), Cell(grid, 3));
        }

        [Fact]
        public void Compute_EmptyOrNegativeCount_IsAnEmptyZeroHeightGrid()
        {
            foreach (int count in new[] { 0, -3 })
            {
                var grid = SnapshotItemGridLayout.Compute(count, 1310, 52);

                Assert.Empty(grid.Cells);
                Assert.Equal(0, grid.RowCount);
                Assert.Equal(0, grid.Height);
            }
        }

        [Fact]
        public void Compute_ZeroRowHeight_PlacesEveryRowAtZero()
        {
            var grid = SnapshotItemGridLayout.Compute(4, 1000, 0);

            Assert.Equal(0, grid.Height);
            Assert.Equal(0, grid.Cells[3].Y);
            Assert.Equal(1, grid.Cells[3].Row);
        }

        [Fact]
        public void Compute_DegenerateWidth_StillPlacesOneCellPerRow()
        {
            // A window dragged to nothing must not divide by zero or stack
            // every cell on top of the first.
            var grid = SnapshotItemGridLayout.Compute(2, 0, 52);

            Assert.Equal(1, grid.ColumnCount);
            Assert.Equal(0, grid.ColumnWidth);
            Assert.Equal(2, grid.RowCount);
            Assert.Equal((0, 52, 0, 1), Cell(grid, 1));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 52)]
        [InlineData(2, 52)]
        [InlineData(3, 104)]
        [InlineData(4, 104)]
        [InlineData(5, 156)]
        public void ComputeHeight_MatchesTheGridItPlaces(int count, int expected)
        {
            Assert.Equal(expected, SnapshotItemGridLayout.ComputeHeight(count, 1000, 52));
            Assert.Equal(expected, SnapshotItemGridLayout.Compute(count, 1000, 52).Height);
        }

        private static (int, int, int, int) Cell(SnapshotItemGridLayout.Grid grid, int index)
        {
            var cell = grid.Cells[index];
            return (cell.X, cell.Y, cell.Column, cell.Row);
        }
    }
}
