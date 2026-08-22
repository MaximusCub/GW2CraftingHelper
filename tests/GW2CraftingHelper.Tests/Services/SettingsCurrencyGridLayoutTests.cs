using System.Collections.Generic;
using System.Globalization;
using GW2CraftingHelper.Models;
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
        [InlineData(847, 1)]
        [InlineData(848, 2)]
        [InlineData(1000, 2)]
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
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, 900, 30);

            Assert.Equal(2, grid.ColumnCount);
            Assert.Equal(450, grid.ColumnWidth);
            Assert.Equal(5, grid.VisibleCount);
            Assert.Equal(3, grid.RowCount);
            Assert.Equal(90, grid.Height);

            Assert.Equal(0, grid.Cells[0].X);
            Assert.Equal(0, grid.Cells[0].Y);
            Assert.Equal(450, grid.Cells[1].X);
            Assert.Equal(0, grid.Cells[1].Y);
            Assert.Equal(0, grid.Cells[2].X);
            Assert.Equal(30, grid.Cells[2].Y);
            Assert.Equal(450, grid.Cells[3].X);
            Assert.Equal(30, grid.Cells[3].Y);
            Assert.Equal(0, grid.Cells[4].X);
            Assert.Equal(60, grid.Cells[4].Y);
            Assert.Equal(2, grid.Cells[4].Row);
        }

        [Fact]
        public void Compute_NarrowPanel_FallsBackToOneColumnPerRow()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, 800, 30);

            Assert.Equal(1, grid.ColumnCount);
            Assert.Equal(800, grid.ColumnWidth);
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
            var grid = SettingsCurrencyGridLayout.Compute(Names, "fractal", 900, 30);

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
            Assert.Equal(450, grid.Cells[4].X);
            Assert.Equal(0, grid.Cells[4].Y);
        }

        [Fact]
        public void Compute_HiddenCells_ReportRowMinusOneSoNoDividerClaimsALastRow()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, "karma", 900, 30);

            Assert.Equal(1, grid.VisibleCount);
            Assert.Equal(0, grid.Cells[0].Row);
            Assert.Equal(-1, grid.Cells[1].Row);
            Assert.Equal(-1, grid.Cells[4].Row);
        }

        [Fact]
        public void Compute_FilterMatchingNothing_CollapsesToZeroHeight()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, "no such currency", 900, 30);

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
            var grid = SettingsCurrencyGridLayout.Compute(null, "karma", 900, 30);

            Assert.Empty(grid.Cells);
            Assert.Equal(0, grid.RowCount);
            Assert.Equal(0, grid.Height);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-30)]
        public void Compute_NonPositiveRowHeight_ProducesZeroHeightGrid(int rowHeight)
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, 900, rowHeight);

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
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, 901, 30);

            Assert.Equal(450, grid.ColumnWidth);
            Assert.Equal(450, grid.Cells[1].X);
            Assert.True(grid.Cells[1].X + grid.ColumnWidth <= 901);
        }

        [Theory]
        [InlineData(900, 450)]
        [InlineData(901, 450)]
        [InlineData(800, 800)]
        [InlineData(0, 0)]
        [InlineData(-40, 0)]
        public void ComputeColumnWidth_MatchesWhatComputeUses(int panelWidth, int expected)
        {
            Assert.Equal(expected, SettingsCurrencyGridLayout.ComputeColumnWidth(panelWidth));
            Assert.Equal(
                SettingsCurrencyGridLayout.ComputeColumnWidth(panelWidth),
                SettingsCurrencyGridLayout.Compute(Names, null, panelWidth, 30).ColumnWidth);
        }

        [Theory]
        [InlineData(47, 900, 720)]
        [InlineData(47, 800, 1410)]
        [InlineData(0, 900, 0)]
        [InlineData(-3, 900, 0)]
        public void ComputeHeight_IsTheUnfilteredGridHeight(int count, int panelWidth, int expected)
        {
            Assert.Equal(expected, SettingsCurrencyGridLayout.ComputeHeight(count, panelWidth, 30));
        }

        [Fact]
        public void ComputeHeight_DoesNotMoveWithTheFilter()
        {
            // The whole point of the fixed height: Blish's Scrollbar snaps to
            // the top on any content-height change, so the grid panel must
            // measure the same whatever the filter leaves showing.
            int unfiltered = SettingsCurrencyGridLayout.ComputeHeight(Names.Count, 900, 30);

            Assert.Equal(90, unfiltered);
            Assert.Equal(unfiltered, SettingsCurrencyGridLayout.ComputeHeight(Names.Count, 900, 30));
            Assert.NotEqual(
                unfiltered,
                SettingsCurrencyGridLayout.Compute(Names, "karma", 900, 30).Height);
        }

        [Fact]
        public void Compute_AlwaysShow_KeepsANonMatchingRowPlaced()
        {
            // Index 1 ("Laurels") does not match "fractal" but carries an
            // unsaved invalid amount, so it must still be placed - and it
            // must be packed in input order alongside the real matches, not
            // appended after them.
            var alwaysShow = new[] { false, true, false, false, false };

            var grid = SettingsCurrencyGridLayout.Compute(Names, "fractal", 900, 30, alwaysShow);

            Assert.Equal(3, grid.VisibleCount);
            Assert.True(grid.Cells[1].Visible);
            Assert.Equal(0, grid.Cells[1].Row);
            Assert.Equal(0, grid.Cells[1].X);
            Assert.Equal(450, grid.Cells[3].X);
            Assert.Equal(0, grid.Cells[4].X);
            Assert.Equal(30, grid.Cells[4].Y);
            Assert.False(grid.Cells[0].Visible);
        }

        [Fact]
        public void Compute_AlwaysShow_ShorterThanNames_DoesNotThrow()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, "karma", 900, 30, new[] { false });

            Assert.Equal(1, grid.VisibleCount);
        }

        // Upper bound on one character of DefaultFont14, calibrated from the
        // 170px name column the view sizes to hold "Pristine Fractal Relic"
        // (22 characters, ~7.7px each).
        private const int MaxCharWidthPx = 8;

        [Fact]
        public void MinColumnWidth_CoversTheWholeCellItSizes()
        {
            Assert.Equal(
                SettingsCurrencyGridLayout.CellTagX + SettingsCurrencyGridLayout.CellTagWidth,
                SettingsCurrencyGridLayout.MinColumnWidth);

            Assert.True(SettingsCurrencyGridLayout.CellInputX
                >= SettingsCurrencyGridLayout.CellNameX + SettingsCurrencyGridLayout.CellNameWidth);
            Assert.True(SettingsCurrencyGridLayout.CellClearX
                >= SettingsCurrencyGridLayout.CellInputX + SettingsCurrencyGridLayout.CellInputWidth);
            Assert.True(SettingsCurrencyGridLayout.CellTagX
                >= SettingsCurrencyGridLayout.CellClearX + SettingsCurrencyGridLayout.CellClearWidth);
        }

        [Theory]
        [InlineData("Invalid")]
        [InlineData("cleared")]
        public void CellTagWidth_FitsEveryFixedTagString(string tag)
        {
            Assert.True(tag.Length * MaxCharWidthPx <= SettingsCurrencyGridLayout.CellTagWidth);
        }

        [Fact]
        public void CellClearWidth_FitsTheCheckboxAndItsLabel()
        {
            // Blish's Checkbox draws a ~16px box plus a gap ahead of its
            // text; the tag column starts immediately after this budget.
            const int CheckboxGlyphAndGapPx = 24;

            Assert.True(
                CheckboxGlyphAndGapPx + "Clear".Length * MaxCharWidthPx
                    <= SettingsCurrencyGridLayout.CellClearWidth);
        }

        [Fact]
        public void CellTagWidth_FitsEveryRealDefaultEstimate()
        {
            // Real production table, not a sample: a future six-figure
            // default would clip the tag the same way "default: N" clipped
            // the input's placeholder.
            foreach (var kvp in CurrencyDecisionDefaults.DefaultCopperPerUnit)
            {
                string tag = "default " + kvp.Value.ToString(CultureInfo.InvariantCulture);
                Assert.True(
                    tag.Length * MaxCharWidthPx <= SettingsCurrencyGridLayout.CellTagWidth,
                    $"Tag \"{tag}\" does not fit CellTagWidth {SettingsCurrencyGridLayout.CellTagWidth}");
            }
        }

        [Fact]
        public void CellInputWidth_FitsItsPlaceholder()
        {
            // Blish's TextBox insets the placeholder 10px a side and draws it
            // untruncated, so only the inset region is legible.
            int textRegion = SettingsCurrencyGridLayout.CellInputWidth - 20;

            Assert.True("copper".Length * MaxCharWidthPx <= textRegion);
        }
    }
}
