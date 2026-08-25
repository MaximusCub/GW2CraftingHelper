using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class SettingsFormLayoutTests
    {
        // Panel widths the board actually resolves to at the window sizes
        // the gate checks - a column at the floor, a column at three-up, and
        // a very wide one.
        private const int FloorColumnWidth = 616;
        private const int WideColumnWidth = 1232;

        // Resolved pixels, not the const sums that produce them. Restating a
        // const expression is a compile-time tautology the compiler already
        // checks; pinning the resolved number is what makes a moved term
        // visible, and the behavioural boundaries below are written in
        // literal pixels so their oracle is independent of the constants.
        [Fact]
        public void ClusterAndColumnFloors_ResolveToTheirShippedPixels()
        {
            Assert.Equal(198, SettingsFormLayout.NameFloor);
            Assert.Equal(336, SettingsFormLayout.WidestClusterWidth);
            Assert.Equal(570, SettingsFormLayout.MinColumnWidth);
            Assert.Equal(546, SettingsFormLayout.ProseMeasure);
        }

        [Fact]
        public void SettingsBoard_TurnsOverToTwoColumnsBetween1139And1140Pixels()
        {
            // The boundary MinColumnWidth exists to place, in literal
            // pixels: a board one pixel short of two floors stays single.
            Assert.Equal(
                1,
                ColumnBoardLayout.ComputeColumnCount(1139, SettingsFormLayout.MinColumnWidth, 8));
            Assert.Equal(
                2,
                ColumnBoardLayout.ComputeColumnCount(1140, SettingsFormLayout.MinColumnWidth, 8));
        }

        [Fact]
        public void NameFloor_HoldsTheWidestLabelTheTabShips()
        {
            Assert.True("Metal (Metal Forge)".Length <= SettingsFormLayout.NameRunChars);
        }

        [Theory]
        [InlineData(FloorColumnWidth)]
        [InlineData(WideColumnWidth)]
        [InlineData(SettingsFormLayout.MinColumnWidth)]
        public void ClusterRightEdge_IsThePinnedRightEdgeAndNothingElse(int columnWidth)
        {
            Assert.Equal(
                PlanRelayoutMath.PinnedRightEdge(columnWidth),
                SettingsFormLayout.ClusterRightEdge(columnWidth));
        }

        [Theory]
        [InlineData(FloorColumnWidth, 60)]
        [InlineData(WideColumnWidth, 120)]
        public void InputSitsLeftOfTheTagSlotByExactlyTheBoxAndItsGap(int columnWidth, int tagBand)
        {
            Assert.Equal(
                SettingsFormLayout.TagX(columnWidth, tagBand)
                    - SettingsFormLayout.InputToTagGap
                    - SettingsFormLayout.InputWidth,
                SettingsFormLayout.InputX(columnWidth, tagBand));
        }

        [Fact]
        public void TagSlotEndsOnTheColumnsRightEdgeWhateverTheBandHolds()
        {
            foreach (int band in new[] { 0, 60, 120, 200 })
            {
                Assert.Equal(
                    SettingsFormLayout.ClusterRightEdge(FloorColumnWidth),
                    SettingsFormLayout.TagX(FloorColumnWidth, band) + band);
            }
        }

        [Fact]
        public void TagSlotDoesNotMoveWhenARowSwitchesFromItsUnitToItsError()
        {
            // The band is max(widest unit, widest error) across the section.
            // These stand in for the widths the view measures; what matters
            // is that ONE band serves both states, so a row failing
            // validation cannot shift the column. The unit-only band below
            // is what a per-string slot would have given, and it is a
            // DIFFERENT x - that difference is the defect this rule removes.
            const int WidestUnit = 88;    // "minutes (1-120)"
            const int WidestError = 110;  // "Must be 0, 1, or 2"
            int band = WidestUnit > WidestError ? WidestUnit : WidestError;

            Assert.Equal(WidestError, band);
            Assert.NotEqual(
                SettingsFormLayout.TagX(FloorColumnWidth, WidestUnit),
                SettingsFormLayout.TagX(FloorColumnWidth, band));

            // Both occupants sit at the banded x, and the box before them
            // does not move either.
            Assert.Equal(
                SettingsFormLayout.ClusterRightEdge(FloorColumnWidth) - band,
                SettingsFormLayout.TagX(FloorColumnWidth, band));
            Assert.Equal(
                SettingsFormLayout.TagX(FloorColumnWidth, band)
                    - SettingsFormLayout.InputToTagGap - SettingsFormLayout.InputWidth,
                SettingsFormLayout.InputX(FloorColumnWidth, band));
        }

        [Fact]
        public void VolumeClusterPinsRightWithTheSliderFixed()
        {
            foreach (int columnWidth in new[] { SettingsFormLayout.MinColumnWidth, FloorColumnWidth, WideColumnWidth })
            {
                int rightEdge = SettingsFormLayout.ClusterRightEdge(columnWidth);

                Assert.Equal(
                    rightEdge - SettingsFormLayout.TestButtonWidth,
                    SettingsFormLayout.TestButtonX(columnWidth));
                Assert.Equal(
                    SettingsFormLayout.TestButtonX(columnWidth)
                        - SettingsFormLayout.ReadoutToTestGap - SettingsFormLayout.ReadoutWidth,
                    SettingsFormLayout.VolumeReadoutX(columnWidth));
                Assert.Equal(
                    SettingsFormLayout.VolumeReadoutX(columnWidth)
                        - SettingsFormLayout.SliderToReadoutGap - SettingsFormLayout.SliderWidth,
                    SettingsFormLayout.VolumeSliderX(columnWidth));

                // The whole cluster is exactly WidestClusterWidth wide at
                // every column width: only the NAME flexes.
                Assert.Equal(
                    SettingsFormLayout.WidestClusterWidth,
                    rightEdge - SettingsFormLayout.VolumeSliderX(columnWidth));
            }
        }

        [Fact]
        public void NameMaxWidth_TakesEveryRecoveredPixel()
        {
            int narrow = SettingsFormLayout.NameMaxWidth(
                SettingsFormLayout.MinColumnWidth, SettingsFormLayout.WidestClusterWidth);
            int wide = SettingsFormLayout.NameMaxWidth(
                WideColumnWidth, SettingsFormLayout.WidestClusterWidth);

            Assert.Equal(WideColumnWidth - SettingsFormLayout.MinColumnWidth, wide - narrow);
            Assert.True(narrow >= SettingsFormLayout.NameFloor);
        }

        [Fact]
        public void NameMaxWidth_IsThePlanTablesOwnRule()
        {
            Assert.Equal(
                PlanRelayoutMath.NameMaxWidthBeforeColumn(
                    SettingsFormLayout.ClusterRightEdge(FloorColumnWidth),
                    SettingsFormLayout.WidestClusterWidth,
                    SettingsFormLayout.NameToControlGap,
                    SettingsFormLayout.CellLeftPad),
                SettingsFormLayout.NameMaxWidth(FloorColumnWidth, SettingsFormLayout.WidestClusterWidth));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-500)]
        public void NameMaxWidth_FloorsRatherThanGoingNegative(int columnWidth)
        {
            Assert.True(
                SettingsFormLayout.NameMaxWidth(columnWidth, SettingsFormLayout.WidestClusterWidth) >= 20);
        }

        [Fact]
        public void DescriptionBudgetIsTheNameColumn_NotTheWholeRow()
        {
            // Prose under a control must not run beneath the control...
            Assert.Equal(
                SettingsFormLayout.NameMaxWidth(
                    FloorColumnWidth, SettingsFormLayout.WidestClusterWidth),
                SettingsFormLayout.DescriptionMaxWidth(
                    FloorColumnWidth, SettingsFormLayout.WidestClusterWidth));

            foreach (int columnWidth in new[] { FloorColumnWidth, WideColumnWidth })
            {
                Assert.True(
                    SettingsFormLayout.DescriptionMaxWidth(
                        columnWidth, SettingsFormLayout.WidestClusterWidth)
                        < columnWidth);
            }
        }

        [Fact]
        public void DescriptionBudgetAlsoCapsAtTheReadingMeasure()
        {
            // ...and a wide column widens the name budget past what a line
            // of prose should be, so the cap holds there too.
            Assert.True(
                SettingsFormLayout.NameMaxWidth(WideColumnWidth, SettingsFormLayout.WidestClusterWidth)
                    > SettingsFormLayout.ProseMeasure);
            Assert.Equal(
                SettingsFormLayout.ProseMeasure,
                SettingsFormLayout.DescriptionMaxWidth(
                    WideColumnWidth, SettingsFormLayout.WidestClusterWidth));

            // A Checkbox row reserves no cluster at all and is capped the
            // same way.
            Assert.Equal(
                SettingsFormLayout.ProseMeasure,
                SettingsFormLayout.DescriptionMaxWidth(WideColumnWidth, 0));
        }

        [Fact]
        public void SectionProseMaxWidth_CapsAtTheMeasureHoweverWideTheColumn()
        {
            Assert.Equal(SettingsFormLayout.ProseMeasure,
                SettingsFormLayout.SectionProseMaxWidth(WideColumnWidth));
            Assert.Equal(SettingsFormLayout.ProseMeasure,
                SettingsFormLayout.SectionProseMaxWidth(4000));

            // Below the measure it takes what the column has.
            Assert.Equal(
                PlanRelayoutMath.PinnedRightEdge(400) - SettingsFormLayout.CellLeftPad,
                SettingsFormLayout.SectionProseMaxWidth(400));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public void SectionProseMaxWidth_FloorsRatherThanGoingNegative(int columnWidth)
        {
            Assert.True(SettingsFormLayout.SectionProseMaxWidth(columnWidth) >= 20);
        }

        [Fact]
        public void InputClusterWidth_IsTheBoxTheGapAndTheBand()
        {
            Assert.Equal(
                SettingsFormLayout.InputWidth + SettingsFormLayout.InputToTagGap + 120,
                SettingsFormLayout.InputClusterWidth(120));

            // A section with no tag text still reserves the box alone.
            Assert.Equal(
                SettingsFormLayout.InputWidth + SettingsFormLayout.InputToTagGap,
                SettingsFormLayout.InputClusterWidth(0));
            Assert.Equal(
                SettingsFormLayout.InputClusterWidth(0),
                SettingsFormLayout.InputClusterWidth(-40));
        }

        [Fact]
        public void FourSectionsFitTwoUpAtTheWindowMinimumAndFourUpWide()
        {
            int panelAtFloor = WindowSizing.TabPanelWidthFor(WindowSizing.MinWindowWidth);

            Assert.Equal(
                2, ColumnBoardLayout.ComputeColumnCount(
                    panelAtFloor, SettingsFormLayout.MinColumnWidth, 4));
            Assert.Equal(
                4, ColumnBoardLayout.ComputeColumnCount(
                    4 * SettingsFormLayout.MinColumnWidth, SettingsFormLayout.MinColumnWidth, 4));

            // And one column still exceeds the min even on the narrow-screen
            // floor, so nothing clips there.
            int narrowPanel = WindowSizing.TabPanelWidthFor(WindowSizing.NarrowScreenFloorWidth);
            Assert.Equal(
                1, ColumnBoardLayout.ComputeColumnCount(
                    narrowPanel, SettingsFormLayout.MinColumnWidth, 4));
            Assert.True(narrowPanel > SettingsFormLayout.MinColumnWidth);
        }
    }
}
