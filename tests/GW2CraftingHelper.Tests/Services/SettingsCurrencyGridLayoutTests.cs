using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class SettingsCurrencyGridLayoutTests
    {
        private static readonly List<string> Names = new List<string>
        {
            "Karma",
            "Laurels",
            "Spirit Shards",
            "Fractal Relics",
            "Pristine Fractal Relics"
        };

        [Theory]
        [InlineData(679, 1)]
        [InlineData(680, 2)]
        [InlineData(740, 2)]
        [InlineData(0, 1)]
        [InlineData(-40, 1)]
        public void ComputeColumnCount_SwitchesAtTwiceMinColumnWidth(int panelWidth, int expected)
        {
            Assert.Equal(expected, SettingsCurrencyGridLayout.ComputeColumnCount(panelWidth));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Matches_BlankFilter_MatchesEverything(string filter)
        {
            Assert.True(SettingsCurrencyGridLayout.Matches("Karma", filter));
            Assert.True(SettingsCurrencyGridLayout.Matches("", filter));
            Assert.True(SettingsCurrencyGridLayout.Matches(null, filter));
        }

        [Theory]
        [InlineData("Spirit Shards", "shard", true)]
        [InlineData("Spirit Shards", "SPIRIT", true)]
        [InlineData("Spirit Shards", "  shards  ", true)]
        [InlineData("Spirit Shards", "relic", false)]
        [InlineData(null, "karma", false)]
        [InlineData("", "karma", false)]
        public void Matches_NonBlankFilter_IsCaseInsensitiveSubstring(string name, string filter, bool expected)
        {
            Assert.Equal(expected, SettingsCurrencyGridLayout.Matches(name, filter));
        }

        [Fact]
        public void Compute_NoFilter_PacksTwoUpInInputOrder()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, 700, 30);

            Assert.Equal(2, grid.ColumnCount);
            Assert.Equal(350, grid.ColumnWidth);
            Assert.Equal(5, grid.VisibleCount);
            Assert.Equal(3, grid.RowCount);
            Assert.Equal(90, grid.Height);

            Assert.Equal(0, grid.Cells[0].X);
            Assert.Equal(0, grid.Cells[0].Y);
            Assert.Equal(350, grid.Cells[1].X);
            Assert.Equal(0, grid.Cells[1].Y);
            Assert.Equal(0, grid.Cells[2].X);
            Assert.Equal(30, grid.Cells[2].Y);
            Assert.Equal(350, grid.Cells[3].X);
            Assert.Equal(30, grid.Cells[3].Y);
            Assert.Equal(0, grid.Cells[4].X);
            Assert.Equal(60, grid.Cells[4].Y);
            Assert.Equal(2, grid.Cells[4].Row);
        }

        [Fact]
        public void Compute_NarrowPanel_FallsBackToOneColumnPerRow()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, 600, 30);

            Assert.Equal(1, grid.ColumnCount);
            Assert.Equal(600, grid.ColumnWidth);
            Assert.Equal(5, grid.RowCount);
            Assert.Equal(150, grid.Height);
            for (int i = 0; i < Names.Count; i++)
            {
                Assert.Equal(0, grid.Cells[i].X);
                Assert.Equal(i * 30, grid.Cells[i].Y);
            }
        }

        [Fact]
        public void Compute_FilterSkippingMiddleEntries_RepacksWithoutGaps()
        {
            // "Fractal Relics" and "Pristine Fractal Relics" only - they sit
            // at input indexes 3 and 4 but must land in the first grid row.
            var grid = SettingsCurrencyGridLayout.Compute(Names, "fractal", 700, 30);

            Assert.Equal(2, grid.VisibleCount);
            Assert.Equal(1, grid.RowCount);
            Assert.Equal(30, grid.Height);

            Assert.False(grid.Cells[0].Visible);
            Assert.False(grid.Cells[1].Visible);
            Assert.False(grid.Cells[2].Visible);

            Assert.True(grid.Cells[3].Visible);
            Assert.Equal(0, grid.Cells[3].X);
            Assert.Equal(0, grid.Cells[3].Y);
            Assert.Equal(0, grid.Cells[3].Row);

            Assert.True(grid.Cells[4].Visible);
            Assert.Equal(350, grid.Cells[4].X);
            Assert.Equal(0, grid.Cells[4].Y);
        }

        [Fact]
        public void Compute_HiddenCells_ReportRowMinusOneSoNoDividerClaimsALastRow()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, "karma", 700, 30);

            Assert.Equal(1, grid.VisibleCount);
            Assert.Equal(0, grid.Cells[0].Row);
            Assert.Equal(-1, grid.Cells[1].Row);
            Assert.Equal(-1, grid.Cells[4].Row);
        }

        [Fact]
        public void Compute_FilterMatchingNothing_CollapsesToZeroHeight()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, "no such currency", 700, 30);

            Assert.Equal(0, grid.VisibleCount);
            Assert.Equal(0, grid.RowCount);
            Assert.Equal(0, grid.Height);
            foreach (var cell in grid.Cells)
            {
                Assert.False(cell.Visible);
            }
        }

        [Fact]
        public void Compute_NullNames_ReturnsEmptyGrid()
        {
            var grid = SettingsCurrencyGridLayout.Compute(null, "karma", 700, 30);

            Assert.Empty(grid.Cells);
            Assert.Equal(0, grid.RowCount);
            Assert.Equal(0, grid.Height);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-30)]
        public void Compute_NonPositiveRowHeight_ProducesZeroHeightGrid(int rowHeight)
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, 700, rowHeight);

            Assert.Equal(3, grid.RowCount);
            Assert.Equal(0, grid.Height);
            foreach (var cell in grid.Cells)
            {
                Assert.Equal(0, cell.Y);
            }
        }

        [Fact]
        public void Compute_NonPositivePanelWidth_ProducesZeroWidthColumns()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, 0, 30);

            Assert.Equal(1, grid.ColumnCount);
            Assert.Equal(0, grid.ColumnWidth);
            Assert.Equal(5, grid.RowCount);
        }

        [Fact]
        public void Compute_OddColumnWidth_KeepsColumnsInsideThePanel()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, 741, 30);

            Assert.Equal(370, grid.ColumnWidth);
            Assert.Equal(370, grid.Cells[1].X);
            Assert.True(grid.Cells[1].X + grid.ColumnWidth <= 741);
        }
    }
}
