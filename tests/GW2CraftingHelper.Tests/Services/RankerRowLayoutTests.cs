using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class RankerRowLayoutTests
    {
        // The module's supported window widths, minus the tab-panel chrome
        // and the scrollbar allowance the view subtracts before calling in.
        public static readonly object[][] RealWidths =
        {
            new object[] { WindowSizing.TabPanelWidthFor(1378) - WindowSizing.ScrollbarAllowance },
            new object[] { WindowSizing.TabPanelWidthFor(1638) - WindowSizing.ScrollbarAllowance },
            new object[] { WindowSizing.TabPanelWidthFor(1836) - WindowSizing.ScrollbarAllowance },
            new object[] { WindowSizing.TabPanelWidthFor(2406) - WindowSizing.ScrollbarAllowance },
            new object[] { WindowSizing.TabPanelWidthFor(2560) - WindowSizing.ScrollbarAllowance }
        };

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_TheNameBandIsPositiveAndClearsTheReadyCell(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, remainingCellWidth: 120, chipWidth: 96);

            Assert.True(bands.NameWidth > 0);
            Assert.True(bands.NameX + bands.NameWidth <= bands.ReadyRightEdge);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_TheLastButtonEndsExactlyOnTheRowsOneRightEdge(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, remainingCellWidth: 120, chipWidth: 96);

            Assert.Equal(rowWidth - RankerRowLayout.Inset,
                bands.RemoveX + RankerRowLayout.ButtonWidth);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_ThePinnedBlockNeverOverlapsLeftToRight(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, remainingCellWidth: 120, chipWidth: 96);

            Assert.True(bands.ReadyRightEdge <= bands.ChipX);
            Assert.True(bands.ChipX + bands.ChipWidth <= bands.DaysRightEdge);
            Assert.True(bands.DaysRightEdge <= bands.RemainingRightEdge - 120);
            Assert.True(bands.RemainingRightEdge <= bands.UpX);
            Assert.True(bands.UpX + RankerRowLayout.ButtonWidth <= bands.DownX);
            Assert.True(bands.DownX + RankerRowLayout.ButtonWidth <= bands.RemoveX);
        }

        [Fact]
        public void AChiplessRow_ReclaimsTheChipsGapForTheName()
        {
            const int rowWidth = 1200;
            var withChip = RankerRowLayout.Compute(rowWidth, 120, chipWidth: 96);
            var withoutChip = RankerRowLayout.Compute(rowWidth, 120, chipWidth: 0);

            Assert.Equal(0, withoutChip.ChipWidth);
            Assert.True(withoutChip.NameWidth > withChip.NameWidth);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-5000)]
        [InlineData(120)]
        [InlineData(300)]
        public void DegenerateWidths_ClampRatherThanEmittingNegativeWidths(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, remainingCellWidth: 120, chipWidth: 96);

            Assert.True(bands.NameWidth >= 0);
            Assert.True(bands.SubLineWidth >= 0);
            Assert.True(bands.ChipWidth >= 0);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void TheGateStripFillsTheSubLineBandExactly(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, 120, 96);

            int previousRight = bands.SubLineX;
            for (int i = 0; i < RankerRowLayout.GateCellCount; i++)
            {
                RankerRowLayout.GateCell(bands, i, out int x, out int width);
                Assert.Equal(previousRight, x);
                Assert.True(width > 0);
                previousRight = x + width;
            }

            // Justified to the panel: no rounding gap stranded on the right.
            Assert.Equal(bands.SubLineX + bands.SubLineWidth, previousRight);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(RankerRowLayout.GateCellCount)]
        [InlineData(99)]
        public void GateCell_OutOfRange_ReturnsZeroWidth(int index)
        {
            var bands = RankerRowLayout.Compute(1200, 120, 96);

            RankerRowLayout.GateCell(bands, index, out _, out int width);

            Assert.Equal(0, width);
        }

        [Fact]
        public void TotalRowHeight_IsTheBaseRowPlusOneLinePerSubLine()
        {
            Assert.Equal(RankerRowLayout.RowHeight, RankerRowLayout.TotalRowHeight(0));
            Assert.Equal(RankerRowLayout.RowHeight + RankerRowLayout.SubLineHeight,
                RankerRowLayout.TotalRowHeight(1));
            Assert.Equal(RankerRowLayout.RowHeight + 3 * RankerRowLayout.SubLineHeight,
                RankerRowLayout.TotalRowHeight(3));
            Assert.Equal(RankerRowLayout.RowHeight, RankerRowLayout.TotalRowHeight(-4));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(4, 1)]
        [InlineData(5, 2)]
        [InlineData(8, 2)]
        [InlineData(9, 2)]
        public void CurrencyLineCount_IsMonotonicAndCappedAtTwoLines(int currencies, int expected)
        {
            Assert.Equal(expected, RankerRowLayout.CurrencyLineCount(currencies));
        }

        [Fact]
        public void CurrenciesShareTheGateStripsGrid()
        {
            // One set of vertical rails for every sub-line value in a row -
            // the live desktop gate showed that a second grid under the gate
            // strip reads as each value finding its own x.
            Assert.Equal(RankerRowLayout.GateCellCount, RankerRowLayout.CurrenciesPerLine);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_ACurrencyNamePlusValueFitsOneGateCell(int rowWidth)
        {
            // The reason CurrencyLineCount is deliberately width-independent:
            // a width-dependent count would change a row's HEIGHT mid-drag.
            var bands = RankerRowLayout.Compute(rowWidth, 120, 96);
            RankerRowLayout.GateCell(bands, 0, out _, out int cellWidth);

            Assert.True(cellWidth >= 150);
        }

        // ---------------------------------------------------------------
        // The header minimum-width floor - the live desktop gate's Fix 1.
        // The header labels right-align at the same edges the cells do, so
        // every band must stay at least as wide as its own header text even
        // when the table is empty and no cell has been measured.
        // ---------------------------------------------------------------

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(8)]   // a dash-only remaining cell, the empty-table case
        [InlineData(99)]
        public void TheRemainingBandNeverDropsBelowItsHeaderFloor(int measuredCellWidth)
        {
            var bands = RankerRowLayout.Compute(1200, measuredCellWidth, 0);

            Assert.True(bands.RemainingRightEdge - bands.DaysRightEdge
                >= RankerRowLayout.MinRemainingCellWidth + RankerRowLayout.CellGap);
        }

        [Fact]
        public void AWiderMeasuredCellStillBeatsTheFloor()
        {
            var floored = RankerRowLayout.Compute(1200, 8, 0);
            var wide = RankerRowLayout.Compute(1200, 180, 0);

            Assert.Equal(RankerRowLayout.MinRemainingCellWidth + RankerRowLayout.CellGap,
                floored.RemainingRightEdge - floored.DaysRightEdge);
            Assert.Equal(180 + RankerRowLayout.CellGap,
                wide.RemainingRightEdge - wide.DaysRightEdge);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AnEmptyTablesHeaderCellsDoNotOverlap(int rowWidth)
        {
            // Exactly the shape the desktop gate photographed as
            // "ReadhyDaining": zero rows, so a dash-width coin band and no
            // chip. Each right-aligned header must clear the cell to its
            // left even then.
            var bands = RankerRowLayout.Compute(rowWidth, 8, 0);

            int readyCellLeft = bands.ReadyRightEdge - RankerRowLayout.ReadyCellWidth;
            int daysCellLeft = bands.DaysRightEdge - RankerRowLayout.DaysCellWidth;
            int remainingCellLeft = bands.RemainingRightEdge - RankerRowLayout.MinRemainingCellWidth;

            Assert.True(bands.NameX + bands.NameWidth <= readyCellLeft);
            Assert.True(bands.ReadyRightEdge <= daysCellLeft);
            Assert.True(bands.DaysRightEdge <= remainingCellLeft);
            Assert.True(bands.RemainingRightEdge < bands.UpX);
        }

        [Fact]
        public void TheReadyCellIsActuallyReserved_NotJustDeclared()
        {
            // The gate's other header collision: ReadyCellWidth existed but
            // nothing subtracted it, so the name band ran under the
            // right-aligned "100%".
            var bands = RankerRowLayout.Compute(1200, 120, 96);

            Assert.Equal(bands.ReadyRightEdge - RankerRowLayout.ReadyCellWidth - RankerRowLayout.CellGap,
                bands.NameX + bands.NameWidth);
        }
    }
}
