using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class ColumnBoardLayoutTests
    {
        private const int MinColumnWidth = 570;

        private static readonly List<int> FourBlocks = new List<int> { 100, 160, 120, 80 };

        [Theory]
        [InlineData(1232, 2)]
        [InlineData(1710, 3)]
        [InlineData(2280, 4)]
        public void ComputeColumnCount_TakesEveryWholeMinWidthColumnTheBoardHolds(
            int boardWidth, int expected)
        {
            Assert.Equal(
                expected,
                ColumnBoardLayout.ComputeColumnCount(boardWidth, MinColumnWidth, FourBlocks.Count));
        }

        [Fact]
        public void ComputeColumnCount_NeverExceedsTheBlockCount()
        {
            // A column no row ever puts a block in is stranded space by
            // construction, which is the defect this class removes.
            Assert.Equal(2, ColumnBoardLayout.ComputeColumnCount(4000, MinColumnWidth, 2));
            Assert.Equal(1, ColumnBoardLayout.ComputeColumnCount(4000, MinColumnWidth, 1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        [InlineData(569)]
        public void ComputeColumnCount_NeverFallsBelowOne(int boardWidth)
        {
            Assert.Equal(
                1, ColumnBoardLayout.ComputeColumnCount(boardWidth, MinColumnWidth, FourBlocks.Count));
        }

        [Fact]
        public void ComputeColumnCount_ZeroBlocksOrZeroMinWidth_StillReturnsOne()
        {
            Assert.Equal(1, ColumnBoardLayout.ComputeColumnCount(1232, MinColumnWidth, 0));
            Assert.Equal(1, ColumnBoardLayout.ComputeColumnCount(1232, 0, 4));
        }

        [Fact]
        public void Compute_PacksRowMajorInReadingOrder()
        {
            var board = ColumnBoardLayout.Compute(FourBlocks, 1232, MinColumnWidth, 20);

            Assert.Equal(2, board.ColumnCount);
            Assert.Equal(616, board.ColumnWidth);
            Assert.Equal(2, board.RowCount);

            Assert.Equal(0, board.Blocks[0].Column);
            Assert.Equal(0, board.Blocks[0].Row);
            Assert.Equal(1, board.Blocks[1].Column);
            Assert.Equal(0, board.Blocks[1].Row);
            Assert.Equal(0, board.Blocks[2].Column);
            Assert.Equal(1, board.Blocks[2].Row);
            Assert.Equal(1, board.Blocks[3].Column);
            Assert.Equal(1, board.Blocks[3].Row);

            Assert.Equal(0, board.Blocks[0].X);
            Assert.Equal(616, board.Blocks[1].X);
            foreach (var block in board.Blocks)
            {
                Assert.Equal(616, block.Width);
            }
        }

        [Fact]
        public void Compute_RowHeightIsTheTallestBlockInThatRow()
        {
            var board = ColumnBoardLayout.Compute(FourBlocks, 1232, MinColumnWidth, 20);

            // Row 0 is max(100, 160) = 160; row 1 starts after it plus the gap.
            Assert.Equal(0, board.Blocks[0].Y);
            Assert.Equal(0, board.Blocks[1].Y);
            Assert.Equal(180, board.Blocks[2].Y);
            Assert.Equal(180, board.Blocks[3].Y);
        }

        [Fact]
        public void Compute_HeightIsTheRowHeightsPlusTheGapsBetweenThem()
        {
            var board = ColumnBoardLayout.Compute(FourBlocks, 1232, MinColumnWidth, 20);

            // max(100,160) + gap + max(120,80): the trailing row adds no gap.
            Assert.Equal(160 + 20 + 120, board.Height);
        }

        [Fact]
        public void Compute_OneBlockTallerThanEveryOther_DoesNotOverlapTheRowBelow()
        {
            var heights = new List<int> { 40, 400, 40, 40 };

            var board = ColumnBoardLayout.Compute(heights, 1232, MinColumnWidth, 20);

            Assert.Equal(420, board.Blocks[2].Y);
            Assert.True(board.Blocks[2].Y >= board.Blocks[1].Y + heights[1]);
            Assert.Equal(400 + 20 + 40, board.Height);
        }

        [Fact]
        public void Compute_OneColumn_StacksEveryBlockWithTheRowGapBetweenThem()
        {
            var board = ColumnBoardLayout.Compute(FourBlocks, 600, MinColumnWidth, 20);

            Assert.Equal(1, board.ColumnCount);
            Assert.Equal(600, board.ColumnWidth);
            Assert.Equal(4, board.RowCount);
            Assert.Equal(0, board.Blocks[0].Y);
            Assert.Equal(120, board.Blocks[1].Y);
            Assert.Equal(300, board.Blocks[2].Y);
            Assert.Equal(440, board.Blocks[3].Y);
            Assert.Equal(520, board.Height);
        }

        [Fact]
        public void Compute_ColumnCountEqualsBlockCount_PutsOneBlockPerColumnInOneRow()
        {
            var board = ColumnBoardLayout.Compute(FourBlocks, 2280, MinColumnWidth, 20);

            Assert.Equal(4, board.ColumnCount);
            Assert.Equal(1, board.RowCount);
            Assert.Equal(160, board.Height);
            for (int i = 0; i < FourBlocks.Count; i++)
            {
                Assert.Equal(0, board.Blocks[i].Y);
                Assert.Equal(i, board.Blocks[i].Column);
            }
        }

        [Fact]
        public void Compute_NoBlocks_IsAnEmptyZeroHeightBoard()
        {
            var board = ColumnBoardLayout.Compute(new List<int>(), 1232, MinColumnWidth, 20);

            Assert.Empty(board.Blocks);
            Assert.Equal(0, board.RowCount);
            Assert.Equal(0, board.Height);
            Assert.Equal(1, board.ColumnCount);
        }

        [Fact]
        public void Compute_NullBlocks_DoesNotThrow()
        {
            var board = ColumnBoardLayout.Compute(null, 1232, MinColumnWidth, 20);

            Assert.Empty(board.Blocks);
            Assert.Equal(0, board.Height);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-40)]
        public void Compute_NonPositiveRowGap_StacksRowsFlush(int rowGap)
        {
            var board = ColumnBoardLayout.Compute(FourBlocks, 1232, MinColumnWidth, rowGap);

            Assert.Equal(160, board.Blocks[2].Y);
            Assert.Equal(280, board.Height);
        }

        [Fact]
        public void Compute_NonPositiveBlockHeight_ContributesNothingToItsRow()
        {
            var board = ColumnBoardLayout.Compute(new List<int> { 0, -50, 90 }, 1232, MinColumnWidth, 20);

            Assert.Equal(2, board.ColumnCount);
            Assert.Equal(0 + 20 + 90, board.Height);
        }

        [Fact]
        public void Compute_ColumnWidthMatchesWhatComputeColumnWidthReports()
        {
            foreach (int boardWidth in new[] { 0, 600, 1232, 1711, 2280 })
            {
                var board = ColumnBoardLayout.Compute(FourBlocks, boardWidth, MinColumnWidth, 20);
                Assert.Equal(
                    ColumnBoardLayout.ComputeColumnWidth(boardWidth, board.ColumnCount),
                    board.ColumnWidth);
                Assert.True(board.ColumnWidth * board.ColumnCount <= (boardWidth > 0 ? boardWidth : 0));
            }
        }
    }
}
