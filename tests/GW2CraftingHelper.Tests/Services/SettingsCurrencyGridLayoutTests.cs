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

        // Two columns of the re-derived 490px cell. The old pair (907/908)
        // moved with MinColumnWidth, which grew when the cell stopped
        // holding a fixed 190px name and started flexing - see
        // MinColumnWidth_CoversTheWholeCellItSizes for the derivation.
        private const int TwoColumnPanelWidth = 2 * SettingsCurrencyGridLayout.MinColumnWidth;

        [Theory]
        [InlineData(979, 1)]
        [InlineData(980, 2)]
        [InlineData(1000, 2)]
        [InlineData(0, 1)]
        [InlineData(-40, 1)]
        public void ComputeColumnCount_SwitchesAtTwiceMinColumnWidth(int panelWidth, int expected)
        {
            Assert.Equal(expected, SettingsCurrencyGridLayout.ComputeColumnCount(panelWidth));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void ComputeColumnCount_IsUncapped(int columns)
        {
            // The sibling grid's law, applied here: a wide window gets as
            // many whole columns as it holds. This one used to stop at two,
            // so a wide window left hundreds of pixels dead inside every one
            // of 47 cells.
            int panelWidth = columns * SettingsCurrencyGridLayout.MinColumnWidth;

            Assert.Equal(columns, SettingsCurrencyGridLayout.ComputeColumnCount(panelWidth));
            Assert.Equal(
                columns, SettingsCurrencyGridLayout.ComputeColumnCount(panelWidth
                    + SettingsCurrencyGridLayout.MinColumnWidth - 1));
        }

        [Fact]
        public void ComputeColumnCount_IsTheSameLawItsSiblingGridUses()
        {
            // One grid law in the module, not two that disagree. Both are
            // "as many whole min-width columns as fit, never fewer than one",
            // so feeding each its OWN minimum must give each the same count.
            foreach (int columns in new[] { 1, 2, 3, 6 })
            {
                Assert.Equal(
                    SnapshotItemGridLayout.ComputeColumnCount(
                        columns * SnapshotItemGridLayout.MinColumnWidth),
                    SettingsCurrencyGridLayout.ComputeColumnCount(
                        columns * SettingsCurrencyGridLayout.MinColumnWidth));
            }
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
            int columnWidth = SettingsCurrencyGridLayout.MinColumnWidth;
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, TwoColumnPanelWidth, 30);

            Assert.Equal(2, grid.ColumnCount);
            Assert.Equal(columnWidth, grid.ColumnWidth);
            Assert.Equal(5, grid.VisibleCount);
            Assert.Equal(3, grid.RowCount);
            Assert.Equal(90, grid.Height);

            Assert.Equal(0, grid.Cells[0].X);
            Assert.Equal(0, grid.Cells[0].Y);
            Assert.Equal(columnWidth, grid.Cells[1].X);
            Assert.Equal(0, grid.Cells[1].Y);
            Assert.Equal(0, grid.Cells[2].X);
            Assert.Equal(30, grid.Cells[2].Y);
            Assert.Equal(columnWidth, grid.Cells[3].X);
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
            var grid = SettingsCurrencyGridLayout.Compute(Names, "fractal", TwoColumnPanelWidth, 30);

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
            Assert.Equal(SettingsCurrencyGridLayout.MinColumnWidth, grid.Cells[4].X);
            Assert.Equal(0, grid.Cells[4].Y);
        }

        [Fact]
        public void Compute_HiddenCells_ReportRowMinusOneSoNoDividerClaimsALastRow()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, "karma", TwoColumnPanelWidth, 30);

            Assert.Equal(1, grid.VisibleCount);
            Assert.Equal(0, grid.Cells[0].Row);
            Assert.Equal(-1, grid.Cells[1].Row);
            Assert.Equal(-1, grid.Cells[4].Row);
        }

        [Fact]
        public void Compute_FilterMatchingNothing_CollapsesToZeroHeight()
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, "no such currency", TwoColumnPanelWidth, 30);

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
            var grid = SettingsCurrencyGridLayout.Compute(null, "karma", TwoColumnPanelWidth, 30);

            Assert.Empty(grid.Cells);
            Assert.Equal(0, grid.RowCount);
            Assert.Equal(0, grid.Height);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-30)]
        public void Compute_NonPositiveRowHeight_ProducesZeroHeightGrid(int rowHeight)
        {
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, TwoColumnPanelWidth, rowHeight);

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
            var grid = SettingsCurrencyGridLayout.Compute(Names, null, TwoColumnPanelWidth + 1, 30);

            Assert.Equal(SettingsCurrencyGridLayout.MinColumnWidth, grid.ColumnWidth);
            Assert.Equal(SettingsCurrencyGridLayout.MinColumnWidth, grid.Cells[1].X);
            Assert.True(grid.Cells[1].X + grid.ColumnWidth <= TwoColumnPanelWidth + 1);
        }

        [Theory]
        [InlineData(980, 490)]
        [InlineData(981, 490)]
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
        [InlineData(47, 980, 720)]
        [InlineData(47, 800, 1410)]
        [InlineData(0, 980, 0)]
        [InlineData(-3, 980, 0)]
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
            int unfiltered = SettingsCurrencyGridLayout.ComputeHeight(
                Names.Count, TwoColumnPanelWidth, 30);

            Assert.Equal(90, unfiltered);
            Assert.Equal(
                unfiltered,
                SettingsCurrencyGridLayout.ComputeHeight(Names.Count, TwoColumnPanelWidth, 30));
            Assert.NotEqual(
                unfiltered,
                SettingsCurrencyGridLayout.Compute(Names, "karma", TwoColumnPanelWidth, 30).Height);
        }

        [Fact]
        public void Compute_AlwaysShow_KeepsANonMatchingRowPlaced()
        {
            // Index 1 ("Laurels") does not match "fractal" but carries an
            // unsaved invalid amount, so it must still be placed - and it
            // must be packed in input order alongside the real matches, not
            // appended after them.
            var alwaysShow = new[] { false, true, false, false, false };

            var grid = SettingsCurrencyGridLayout.Compute(
                Names, "fractal", TwoColumnPanelWidth, 30, alwaysShow);

            Assert.Equal(3, grid.VisibleCount);
            Assert.True(grid.Cells[1].Visible);
            Assert.Equal(0, grid.Cells[1].Row);
            Assert.Equal(0, grid.Cells[1].X);
            Assert.Equal(SettingsCurrencyGridLayout.MinColumnWidth, grid.Cells[3].X);
            Assert.Equal(0, grid.Cells[4].X);
            Assert.Equal(30, grid.Cells[4].Y);
            Assert.False(grid.Cells[0].Visible);
        }

        [Fact]
        public void Compute_AlwaysShow_ShorterThanNames_DoesNotThrow()
        {
            var grid = SettingsCurrencyGridLayout.Compute(
                Names, "karma", TwoColumnPanelWidth, 30, new[] { false });

            Assert.Equal(1, grid.VisibleCount);
        }

        // Upper bound on one character of the BODY font, calibrated from the
        // 190px name column the view sizes to hold "Pristine Fractal Relic"
        // (22 characters, ~8.5px each at Font16).
        private const int MaxCharWidthPx = 9;

        // The same bound for the two controls in this cell that Blish draws
        // in its OWN DefaultFont14 whatever the module's body font is: the
        // Ignore Checkbox (no Font property at all) and the amount TextBox.
        // See Views/Rendering/UiFonts on the exclusions - measuring those
        // two at the body width would reserve pixels they never paint.
        private const int BlishDefaultMaxCharWidthPx = 8;

        // The derivation IS the test: MinColumnWidth is the name inset, a
        // 22-character name floor, the name-to-control gap, the pinned
        // control block, and the table right margin - nothing chosen.
        [Fact]
        public void MinColumnWidth_CoversTheWholeCellItSizes()
        {
            Assert.Equal(
                SettingsCurrencyGridLayout.CellNameX
                    + (SettingsCurrencyGridLayout.NameRunChars * SnapshotItemGridLayout.MaxCharWidthPx)
                    + SettingsCurrencyGridLayout.NameToControlGap
                    + SettingsCurrencyGridLayout.CellInputWidth
                    + SettingsCurrencyGridLayout.CellInputToClearGap
                    + SettingsCurrencyGridLayout.CellClearWidth
                    + SettingsCurrencyGridLayout.CellTagWidth
                    + PlanRelayoutMath.TableRightMargin,
                SettingsCurrencyGridLayout.MinColumnWidth);
        }

        [Theory]
        [InlineData(490)]
        [InlineData(616)]
        [InlineData(1210)]
        public void Cell_ControlBlockPinsToTheCellsOwnRightEdge(int columnWidth)
        {
            int rightEdge = PlanRelayoutMath.PinnedRightEdge(columnWidth);

            Assert.Equal(
                rightEdge - SettingsCurrencyGridLayout.CellTagWidth,
                SettingsCurrencyGridLayout.CellTagX(columnWidth));
            Assert.Equal(
                SettingsCurrencyGridLayout.CellTagX(columnWidth)
                    - SettingsCurrencyGridLayout.CellClearWidth,
                SettingsCurrencyGridLayout.CellClearX(columnWidth));
            Assert.Equal(
                SettingsCurrencyGridLayout.CellClearX(columnWidth)
                    - SettingsCurrencyGridLayout.CellInputToClearGap
                    - SettingsCurrencyGridLayout.CellInputWidth,
                SettingsCurrencyGridLayout.CellInputX(columnWidth));

            // Nothing in the block overlaps the name inset, at any width the
            // grid can produce.
            Assert.True(SettingsCurrencyGridLayout.CellInputX(columnWidth)
                > SettingsCurrencyGridLayout.CellNameX);
        }

        [Fact]
        public void CellNameMaxWidth_IsTheOnlyPartOfTheCellThatFlexes()
        {
            int narrow = SettingsCurrencyGridLayout.CellNameMaxWidth(
                SettingsCurrencyGridLayout.MinColumnWidth);
            int wide = SettingsCurrencyGridLayout.CellNameMaxWidth(1210);

            // Every recovered pixel lands in the name column, not to the
            // right of the block.
            Assert.Equal(
                wide - narrow, 1210 - SettingsCurrencyGridLayout.MinColumnWidth);
            Assert.True(narrow
                >= SettingsCurrencyGridLayout.NameRunChars * SnapshotItemGridLayout.MaxCharWidthPx);
        }

        [Fact]
        public void CellNameMaxWidth_FloorsRatherThanGoingNegative()
        {
            Assert.True(SettingsCurrencyGridLayout.CellNameMaxWidth(0) >= 20);
            Assert.True(SettingsCurrencyGridLayout.CellNameMaxWidth(-100) >= 20);
        }

        [Theory]
        [InlineData("Invalid")]
        [InlineData("ignored")]
        public void CellTagWidth_FitsEveryFixedTagString(string tag)
        {
            Assert.True(tag.Length * MaxCharWidthPx <= SettingsCurrencyGridLayout.CellTagWidth);
        }

        // The cell's total extent is what decides whether the grid gets two
        // columns at the window's minimum, so a label rename that needs
        // more room must take it from inside the cell - never from
        // MinColumnWidth.
        //
        // The panel width is derived through the WHOLE chain, not just the
        // window's own content region. The previous constant here (864 =
        // "884px content region - 20px right padding") skipped the
        // ViewAdapter, so it overstated the panel by 60px and asserted a
        // two-column grid at the old 930px minimum where the real 804px
        // panel gave ONE column - see docs/research/minimum-window-width.md
        // and the "Minimum width raise" section of docs/KNOWN-ISSUES.md.
        //
        // The chain is WindowSizing's, read from the shipped constants
        // rather than copied here, so a change to the enforced minimum or
        // to the chrome moves these cases with it.
        private static readonly int SettingsPanelWidthAtWindowMinimum =
            WindowSizing.TabPanelWidthFor(WindowSizing.MinWindowWidth);

        // Historical literal, deliberately not a production constant.
        private const int OldWindowMinimumWidth = 930;

        [Fact]
        public void MinColumnWidth_FitsTwoColumnsAtTheWindowMinimum()
        {
            Assert.Equal(2, SettingsCurrencyGridLayout.ComputeColumnCount(SettingsPanelWidthAtWindowMinimum));
            Assert.True(2 * SettingsCurrencyGridLayout.MinColumnWidth <= SettingsPanelWidthAtWindowMinimum);
        }

        [Fact]
        public void TwoColumnGrid_NeedsAWindowFarNarrowerThanTheMinimum()
        {
            // How much of the raise the grid actually needed. The cell's
            // re-derivation moved this threshold, and it still clears the
            // 1378px minimum by a wide margin - nothing regresses at the
            // floor. The old 930px minimum was short of it either way,
            // which is why the grid really did fall back to one column
            // there.
            int windowWidthForTwoColumns =
                (2 * SettingsCurrencyGridLayout.MinColumnWidth) + WindowSizing.WindowToTabPanelChrome;

            Assert.True(windowWidthForTwoColumns < WindowSizing.MinWindowWidth);
            Assert.Equal(
                2,
                SettingsCurrencyGridLayout.ComputeColumnCount(
                    WindowSizing.TabPanelWidthFor(windowWidthForTwoColumns)));
            Assert.Equal(
                1,
                SettingsCurrencyGridLayout.ComputeColumnCount(
                    WindowSizing.TabPanelWidthFor(windowWidthForTwoColumns - 1)));
            Assert.Equal(
                1,
                SettingsCurrencyGridLayout.ComputeColumnCount(
                    WindowSizing.TabPanelWidthFor(OldWindowMinimumWidth)));
        }

        [Fact]
        public void CellClearWidth_FitsTheCheckboxAndItsLabel()
        {
            // Blish's Checkbox draws a ~16px box plus a gap ahead of its
            // text; the tag column starts immediately after this budget.
            const int CheckboxGlyphAndGapPx = 24;

            Assert.True(
                CheckboxGlyphAndGapPx + "Ignore".Length * BlishDefaultMaxCharWidthPx
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
                string value = kvp.Value.ToString(CultureInfo.InvariantCulture);
                foreach (string tag in new[] { "default " + value, "was " + value })
                {
                    Assert.True(
                        tag.Length * MaxCharWidthPx <= SettingsCurrencyGridLayout.CellTagWidth,
                        $"Tag \"{tag}\" does not fit CellTagWidth {SettingsCurrencyGridLayout.CellTagWidth}");
                }
            }
        }

        [Fact]
        public void CellInputWidth_FitsEveryDefaultEstimatePlaceholder()
        {
            // The box now hints with the currency's own default value
            // instead of the unit word "copper", which read as a label on a
            // read-only field. Blish's TextBox insets the placeholder 10px a
            // side and draws it untruncated, so only the inset region is
            // legible - and the real defaults table, not a sample, is what
            // has to fit it.
            int textRegion = SettingsCurrencyGridLayout.CellInputWidth - 20;

            foreach (var kvp in CurrencyDecisionDefaults.DefaultCopperPerUnit)
            {
                string placeholder = kvp.Value.ToString(CultureInfo.InvariantCulture);
                Assert.True(
                    placeholder.Length * BlishDefaultMaxCharWidthPx <= textRegion,
                    $"Placeholder \"{placeholder}\" does not fit the {textRegion}px text region");
            }
        }

        [Fact]
        public void CurrencyColumnHeader_FitsBetweenTheInputAndTagColumns()
        {
            // "Copper per unit" sits on the input column's own X and is what
            // now carries the unit for the whole column. It may run over the
            // Ignore checkbox beside it (that column has no header of its
            // own), but not into the tag slot, whose three states are the
            // rightmost thing in the cell.
            // Width-invariant: the block is pinned as a unit, so the gap
            // between the input's x and the tag's x is the same at every
            // column width.
            foreach (int columnWidth in new[] { 490, 616, 1210 })
            {
                int headerRegion = SettingsCurrencyGridLayout.CellTagX(columnWidth)
                    - SettingsCurrencyGridLayout.CellInputX(columnWidth);

                Assert.True("Copper per unit".Length * MaxCharWidthPx <= headerRegion);
            }
        }
    }
}
