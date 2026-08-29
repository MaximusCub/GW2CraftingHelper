using System;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class ItemInputGridLayoutTests
    {
        // 28 is Views/Rendering/UiMetrics.ButtonHeight, which the Views
        // layer owns and this layer may not name - the strip passes it in.
        private const int ButtonSize = 28;

        // The plan tab lays its top strip straight into the tab panel, so
        // the strip's panel width IS the width every other piece of this
        // module's layout math is derived against. Move
        // WindowToTabPanelChrome or MinCellWidth and the column-count cases
        // below fail with the documented window widths.
        private static int PanelWidthFor(int windowWidth)
        {
            return WindowSizing.TabPanelWidthFor(windowWidth);
        }

        private static readonly int PanelAtWindowMinimum =
            PanelWidthFor(WindowSizing.MinWindowWidth);

        private static ItemInputGridLayout.Grid AtWindowMinimum(int itemCount)
        {
            return ItemInputGridLayout.Compute(itemCount, PanelAtWindowMinimum, ButtonSize);
        }

        // ---- The requirement: three cells across at the window minimum ----
        [Fact]
        public void ThreeCellsFitTheStripTheWindowMinimumLeaves()
        {
            Assert.Equal(3, ItemInputGridLayout.ColumnCount(PanelAtWindowMinimum, ButtonSize));
        }

        [Fact]
        public void ThreeCellsAreWhatTheStripWidthActuallyPaysFor()
        {
            int strip = ItemInputGridLayout.ColumnStripWidth(PanelAtWindowMinimum, ButtonSize);
            int cell = ItemInputGridLayout.MinCellWidth(ButtonSize);

            Assert.True(3 * cell <= strip, $"three cells ({3 * cell}) must fit {strip}px");
            Assert.True(4 * cell > strip, $"a fourth cell ({4 * cell}) must not fit {strip}px");
        }

        [Fact]
        public void TheThirdColumnHoldsWellBelowTheWindowMinimum()
        {
            // The count must not be one drag tick from collapsing at the
            // narrowest window the module allows: 34px of the 1192 the
            // minimum leaves is slack over the three cells' 1158.
            int strip = ItemInputGridLayout.ColumnStripWidth(PanelAtWindowMinimum, ButtonSize);
            int slack = strip - (3 * ItemInputGridLayout.MinCellWidth(ButtonSize));

            Assert.True(slack >= 30, $"only {slack}px of slack under the third column");
        }

        [Fact]
        public void EveryColumnIsAtLeastTheMinimumCellWideAtTheWindowMinimum()
        {
            var grid = AtWindowMinimum(8);

            Assert.True(
                grid.ColumnWidth >= ItemInputGridLayout.MinCellWidth(ButtonSize),
                $"column {grid.ColumnWidth} is narrower than its own minimum");
            Assert.True(
                grid.SearchBoxWidth >= ItemInputGridLayout.MinSearchBoxWidth,
                $"name box {grid.SearchBoxWidth} is under the floor the column count assumes");
        }

        // ---- Height, which is the whole point of the change ----
        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 1)]
        [InlineData(3, 1)]
        [InlineData(4, 2)]
        [InlineData(5, 2)]
        [InlineData(8, 3)]
        [InlineData(9, 3)]
        [InlineData(20, 7)]
        public void RowCountAtTheWindowMinimumGrowsOnlyEveryThirdItem(int itemCount, int expectedRows)
        {
            Assert.Equal(
                expectedRows,
                ItemInputGridLayout.RowCount(itemCount, PanelAtWindowMinimum, ButtonSize));
            Assert.Equal(
                expectedRows * ItemInputGridLayout.RowHeight,
                ItemInputGridLayout.BlockHeight(itemCount, PanelAtWindowMinimum, ButtonSize));
            Assert.Equal(expectedRows, AtWindowMinimum(itemCount).RowCount);
        }

        [Fact]
        public void AFiveItemPlanCostsTwoRowsWhereItOnceCostFive()
        {
            // The screenshot that started this: five stacked rows squeezing
            // the plan below into a strip.
            int stacked = 5 * ItemInputGridLayout.RowHeight;
            int gridded = ItemInputGridLayout.BlockHeight(5, PanelAtWindowMinimum, ButtonSize);

            Assert.Equal(2 * ItemInputGridLayout.RowHeight, gridded);
            Assert.True(gridded * 2 < stacked, $"{gridded} is not a real saving on {stacked}");
        }

        [Fact]
        public void AnEmptyStripStillReservesTheRowItsAddButtonSitsOn()
        {
            var grid = ItemInputGridLayout.Compute(0, PanelAtWindowMinimum, ButtonSize);

            Assert.Empty(grid.Cells);
            Assert.Equal(1, grid.RowCount);
            Assert.Equal(ItemInputGridLayout.RowHeight, grid.Height);
            Assert.Equal(0, grid.AddButtonX);
            Assert.Equal(0, grid.AddButtonY);
        }

        // ---- Reading order, which is what makes it the shorter fill ----
        [Fact]
        public void CellsFillLeftToRightThenDown()
        {
            var grid = AtWindowMinimum(7);

            Assert.Equal(3, grid.ColumnCount);
            for (int i = 0; i < 7; i++)
            {
                Assert.Equal(i % 3, grid.Cells[i].Column);
                Assert.Equal(i / 3, grid.Cells[i].Row);
                Assert.Equal((i % 3) * grid.ColumnWidth, grid.Cells[i].X);
                Assert.Equal((i / 3) * ItemInputGridLayout.RowHeight, grid.Cells[i].Y);
            }
        }

        [Fact]
        public void RemovingAMiddleItemRepacksEveryCellAfterIt()
        {
            var before = AtWindowMinimum(6);
            var after = AtWindowMinimum(5);

            // The strip rebuilds from its row list, so the grid for one
            // fewer item is what the surviving rows are laid out against:
            // the item that was in cell 3 has to land in cell 2's seat, and
            // the block has to keep the row that cell 5 was on.
            Assert.Equal(5, after.Cells.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(before.Cells[i].X, after.Cells[i].X);
                Assert.Equal(before.Cells[i].Y, after.Cells[i].Y);
            }

            Assert.Equal(2, after.RowCount);
            Assert.Equal(before.Cells[5].X, after.AddButtonX);
            Assert.Equal(before.Cells[5].Y, after.AddButtonY);
            Assert.Equal(3, after.ColumnCount);
        }

        // ---- The add button ----
        [Fact]
        public void AddButtonSitsWhereTheNextItemsCellWillOpen()
        {
            // Except on a full row, where the next item opens a row the
            // button deliberately does not reserve - it takes the gutter
            // instead, which is the case AddButtonStaysInsideThePanel...
            // covers.
            for (int itemCount = 1; itemCount <= 12; itemCount++)
            {
                var grid = AtWindowMinimum(itemCount);
                if (itemCount % grid.ColumnCount == 0)
                {
                    continue;
                }

                var next = AtWindowMinimum(itemCount + 1);
                Assert.Equal(next.Cells[itemCount].X, grid.AddButtonX);
                Assert.Equal(next.Cells[itemCount].Y, grid.AddButtonY);
            }
        }

        [Fact]
        public void AddButtonNeverPushesTheStripOntoAnExtraRow()
        {
            for (int itemCount = 1; itemCount <= 20; itemCount++)
            {
                var grid = AtWindowMinimum(itemCount);

                Assert.Equal(grid.Cells[itemCount - 1].Y, grid.AddButtonY);
                Assert.True(
                    grid.AddButtonY < grid.Height,
                    $"add button at y={grid.AddButtonY} is below a {grid.Height}px strip");
            }
        }

        [Fact]
        public void AddButtonStaysInsideThePanelWhenTheLastRowIsFull()
        {
            // A full last row is the case the gutter exists for: the button
            // has no column after its own cell to sit in.
            var grid = AtWindowMinimum(3);

            Assert.Equal(grid.ColumnCount * grid.ColumnWidth, grid.AddButtonX);
            Assert.True(
                grid.AddButtonX + ButtonSize <= PanelAtWindowMinimum - WindowSizing.RightEdgePadding,
                "the add button must stay clear of the tab's right edge");
        }

        // ---- Cell interior ----
        [Fact]
        public void CellControlsRunQtyFirstThenNameThenTheRemoveButton()
        {
            var grid = AtWindowMinimum(3);

            Assert.Equal(0, grid.QtyLabelX);
            Assert.Equal(ItemInputGridLayout.QtyLabelBand, grid.QtyBoxX);
            Assert.Equal(
                grid.QtyBoxX + ItemInputGridLayout.QtyBoxWidth + ItemInputGridLayout.QtyToNameGap,
                grid.SearchBoxX);
            Assert.Equal(
                grid.SearchBoxX + grid.SearchBoxWidth + ItemInputGridLayout.NameToButtonGap,
                grid.RemoveButtonX);
            Assert.Equal(grid.RemoveButtonX + ButtonSize, grid.CellWidth);
        }

        [Fact]
        public void TheRemoveButtonIsSeparatedFromBothQuantityBoxesAroundIt()
        {
            // The whole reason quantity leads the cell: a button touching a
            // number box reads as its stepper. Its own cell's box is a name
            // box away, and the NEXT cell's is behind that cell's label.
            var grid = AtWindowMinimum(6);

            int buttonRight = grid.RemoveButtonX + ButtonSize;
            int ownQtyBoxRight = grid.QtyBoxX + ItemInputGridLayout.QtyBoxWidth;
            Assert.True(
                grid.RemoveButtonX - ownQtyBoxRight >= grid.SearchBoxWidth,
                "the remove button must sit a whole name box away from its own quantity field");

            int nextCellQtyBoxX = grid.ColumnWidth + grid.QtyBoxX;
            Assert.True(
                nextCellQtyBoxX - buttonRight >= ItemInputGridLayout.QtyLabelBand,
                "the next cell's label, not its box, is what the button abuts");
        }

        [Fact]
        public void CellsNeverOverlapTheColumnAfterThem()
        {
            foreach (int windowWidth in new[] { 930, WindowSizing.MinWindowWidth, 1632, 2540 })
            {
                var grid = ItemInputGridLayout.Compute(12, PanelWidthFor(windowWidth), ButtonSize);

                Assert.True(
                    grid.CellWidth + ItemInputGridLayout.ColumnGap <= grid.ColumnWidth,
                    $"at {windowWidth} a {grid.CellWidth}px cell overruns a {grid.ColumnWidth}px column");
                Assert.Equal(grid.ColumnWidth, grid.CellPanelWidth);
            }
        }

        [Fact]
        public void ACellsPanelIsNeverNarrowerThanTheControlsInIt()
        {
            // A control whose right edge is flush with its parent's is the
            // one that gets clipped, and the remove button is exactly that
            // by construction - so the panel is the wider of the two.
            foreach (int panelWidth in new[] { 0, 120, 640, PanelAtWindowMinimum, 2414 })
            {
                var grid = ItemInputGridLayout.Compute(4, panelWidth, ButtonSize);

                Assert.True(
                    grid.CellPanelWidth >= grid.CellWidth,
                    $"at {panelWidth} a {grid.CellPanelWidth}px panel cannot hold {grid.CellWidth}px of controls");
            }
        }

        // ---- Degrading on a narrow panel, growing on a wide one ----
        [Fact]
        public void BelowTheWindowMinimumTheGridJustFallsBackToOneWideCell()
        {
            // Not a supported width - the module enforces MinWindowWidth and
            // the narrow-screen floor exists only for a client too small to
            // honour it. All that is asked of it is that it still lays out.
            int panel = PanelWidthFor(WindowSizing.NarrowScreenFloorWidth);
            var grid = ItemInputGridLayout.Compute(3, panel, ButtonSize);

            Assert.Equal(1, ItemInputGridLayout.ColumnCount(panel, ButtonSize));
            Assert.Equal(3, grid.RowCount);
            Assert.True(grid.SearchBoxWidth >= ItemInputGridLayout.MinSearchBoxWidth);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-500)]
        [InlineData(120)]
        public void ADegeneratePanelFallsBackToOneUsableColumn(int panelWidth)
        {
            var grid = ItemInputGridLayout.Compute(3, panelWidth, ButtonSize);

            Assert.Equal(1, grid.ColumnCount);
            Assert.Equal(3, grid.RowCount);
            Assert.True(grid.SearchBoxWidth > 0, "a zero-width search box is dropped, not clipped");
            Assert.True(grid.CellWidth > 0);
        }

        [Fact]
        public void AWideWindowSeatsMoreCellsPerRowRatherThanWiderOnes()
        {
            int wide = PanelWidthFor(2540);

            Assert.Equal(6, ItemInputGridLayout.ColumnCount(wide, ButtonSize));
            Assert.Equal(1, ItemInputGridLayout.RowCount(6, wide, ButtonSize));
            Assert.Equal(2, ItemInputGridLayout.RowCount(7, wide, ButtonSize));
        }

        [Fact]
        public void TheFourthColumnArrivesOnlyOnAWindowWideEnoughToPayForIt()
        {
            // Four cells of the shipped MinCellWidth need a 1730px window.
            // Stated here so a chrome trim that silently re-crosses the
            // 1378px minimum back into four narrow columns fails.
            Assert.Equal(3, ItemInputGridLayout.ColumnCount(PanelWidthFor(1729), ButtonSize));
            Assert.Equal(4, ItemInputGridLayout.ColumnCount(PanelWidthFor(1730), ButtonSize));
        }

        [Fact]
        public void ColumnCountNeverFallsAsThePanelGrows()
        {
            int previous = 0;
            for (int panelWidth = 200; panelWidth <= 3400; panelWidth += 7)
            {
                int columns = ItemInputGridLayout.ColumnCount(panelWidth, ButtonSize);
                Assert.True(columns >= previous, $"column count fell at panel width {panelWidth}");
                previous = columns;
            }
        }

        [Fact]
        public void RowMajorIsNeverTallerThanFillingAColumnFirst()
        {
            // The alternative fill order, measured rather than preferred:
            // a column-first strip that is two rows deep spends both of
            // them on its second item, and stays even with row-major only
            // once a row is full.
            const int columnFirstDepth = 2;
            for (int itemCount = 1; itemCount <= 20; itemCount++)
            {
                int rowMajor = ItemInputGridLayout.RowCount(itemCount, PanelAtWindowMinimum, ButtonSize);
                int columns = ItemInputGridLayout.ColumnCount(PanelAtWindowMinimum, ButtonSize);
                int columnFirst = itemCount <= columns * columnFirstDepth
                    ? Math.Min(itemCount, columnFirstDepth)
                    : columnFirstDepth + ((itemCount - (columns * columnFirstDepth) + columns - 1) / columns);

                Assert.True(
                    rowMajor <= columnFirst,
                    $"{itemCount} items: row-major {rowMajor} rows vs column-first {columnFirst}");
            }
        }
    }
}
