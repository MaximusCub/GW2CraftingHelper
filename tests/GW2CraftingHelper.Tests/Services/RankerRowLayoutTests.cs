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
            new object[] { WindowSizing.TabPanelWidthFor(2560) - WindowSizing.ScrollbarAllowance },
        };

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_TheNameBandIsPositiveAndClearsTheReadyCell(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, remainingCellWidth: 120);

            Assert.True(bands.NameWidth > 0);
            Assert.True(bands.NameX + bands.NameWidth <= bands.ReadyRightEdge);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_TheLastButtonEndsExactlyOnTheRowsOneRightEdge(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, remainingCellWidth: 120);

            Assert.Equal(rowWidth - RankerRowLayout.Inset,
                bands.RemoveX + RankerRowLayout.ButtonWidth);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_ThePinnedBlockNeverOverlapsLeftToRight(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, remainingCellWidth: 120);

            Assert.True(bands.ReadyRightEdge <= bands.DaysRightEdge - RankerRowLayout.DaysCellWidth);
            Assert.True(bands.DaysRightEdge <= bands.RemainingRightEdge - 120);
            Assert.True(bands.RemainingRightEdge <= bands.UpX);
            Assert.True(bands.UpX + RankerRowLayout.ButtonWidth <= bands.DownX);
            Assert.True(bands.DownX + RankerRowLayout.ButtonWidth <= bands.RemoveX);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-5000)]
        [InlineData(120)]
        [InlineData(300)]
        public void DegenerateWidths_ClampRatherThanEmittingNegativeWidths(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, remainingCellWidth: 120);

            Assert.True(bands.NameWidth >= 0);
            Assert.True(bands.SubLineWidth >= 0);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void TheGateStripFillsTheSubLineBandExactly(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, 120);

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

        [Fact]
        public void TheGateStripCarriesAllFiveGates()
        {
            // Field issue 7: the strip gained a Recipes cell; the cell count
            // is what the view's render loop truncates against.
            Assert.Equal(5, RankerRowLayout.GateCellCount);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(RankerRowLayout.GateCellCount)]
        [InlineData(99)]
        public void GateCell_OutOfRange_ReturnsZeroWidth(int index)
        {
            var bands = RankerRowLayout.Compute(1200, 120);

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
        [InlineData(3, 1)]
        [InlineData(4, 2)]
        [InlineData(6, 2)]
        [InlineData(7, 3)]
        [InlineData(9, 3)]
        [InlineData(20, 3)]
        public void CurrencyLineCount_IsMonotonicAndCappedAtThreeLines(int currencies, int expected)
        {
            Assert.Equal(expected, RankerRowLayout.CurrencyLineCount(currencies));
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void TheCurrencyGridIsIndentedAndFillsItsOwnBandExactly(int rowWidth)
        {
            // Field issue 6: currency entries no longer sit on the gate
            // rails - their grid starts CurrencyIndent inside the sub-line
            // band so they read as one owned list, not gate children.
            var bands = RankerRowLayout.Compute(rowWidth, 120);

            int previousRight = bands.SubLineX + RankerRowLayout.CurrencyIndent;
            for (int i = 0; i < RankerRowLayout.CurrenciesPerLine; i++)
            {
                RankerRowLayout.CurrencyCell(bands, i, out int x, out int width);
                Assert.Equal(previousRight, x);
                Assert.True(width > 0);
                previousRight = x + width;
            }

            Assert.Equal(bands.SubLineX + bands.SubLineWidth, previousRight);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_ACurrencyCellFitsIconNameAndValue(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, 120);
            RankerRowLayout.CurrencyCell(bands, 0, out _, out int cellWidth);

            // Icon frame + gap + a usable name run + a right-aligned value.
            Assert.True(cellWidth >=
                RankerRowLayout.CurrencyIconSize + 2 + RankerRowLayout.CurrencyIconGap + 150);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(RankerRowLayout.CurrenciesPerLine)]
        [InlineData(99)]
        public void CurrencyCell_OutOfRange_ReturnsZeroWidth(int index)
        {
            var bands = RankerRowLayout.Compute(1200, 120);

            RankerRowLayout.CurrencyCell(bands, index, out _, out int width);

            Assert.Equal(0, width);
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
        [InlineData(8)] // a dash-only remaining cell, the empty-table case
        [InlineData(99)]
        public void TheRemainingBandNeverDropsBelowItsHeaderFloor(int measuredCellWidth)
        {
            var bands = RankerRowLayout.Compute(1200, measuredCellWidth);

            Assert.True(bands.RemainingRightEdge - bands.DaysRightEdge
                >= RankerRowLayout.MinRemainingCellWidth + RankerRowLayout.CellGap);
        }

        [Fact]
        public void AWiderMeasuredCellStillBeatsTheFloor()
        {
            var floored = RankerRowLayout.Compute(1200, 8);
            var wide = RankerRowLayout.Compute(1200, 180);

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
            // "ReadhyDaining": zero rows, so a dash-width coin band. Each
            // right-aligned header must clear the cell to its left even then.
            var bands = RankerRowLayout.Compute(rowWidth, 8);

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
            var bands = RankerRowLayout.Compute(1200, 120);

            Assert.Equal(bands.ReadyRightEdge - RankerRowLayout.ReadyCellWidth - RankerRowLayout.CellGap,
                bands.NameX + bands.NameWidth);
        }

        // ---------------------------------------------------------------
        // The toolbar row - field bug: refresh-progress text stamped onto
        // the fixed-width button spilled past its edges. The status band
        // and the button band must never overlap, at any width, with any
        // progress string.
        // ---------------------------------------------------------------
        private const int SpinnerSize = 20;
        private const int SpinnerGap = 6;

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_TheStatusBandAndButtonNeverOverlap(int barWidth)
        {
            var slots = RankerRowLayout.Toolbar(barWidth, SpinnerSize, SpinnerGap);

            // Status text, its trailing spinner and the gap after it all end
            // before the button starts.
            Assert.True(slots.StatusX + slots.StatusWidth + SpinnerGap + SpinnerSize + SpinnerGap
                <= slots.RefreshX);
            Assert.Equal(barWidth, slots.RefreshX + RankerRowLayout.RefreshButtonWidth);
            Assert.Equal(RankerRowLayout.Inset, slots.StatusX);
            Assert.True(slots.StatusWidth > 0);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void ALongProgressString_EllipsizesInsideTheStatusBand(int barWidth)
        {
            // The view feeds status text through TextWrapMath.Ellipsize
            // against ToolbarSlots.StatusWidth; proven here with the same
            // Blish-free arithmetic and a synthetic 8px-per-char measure.
            var slots = RankerRowLayout.Toolbar(barWidth, SpinnerSize, SpinnerGap);
            string progress = "Refreshing 17 of 25 - The Legendary Item With An Extremely " +
                "Long Name That Keeps Going. The first refresh of a session downloads " +
                "recipe data and can take a while, and this string is longer than any band.";
            System.Func<string, int> measure = s => 8 * (s ?? "").Length;

            string shown = TextWrapMath.Ellipsize(progress, slots.StatusWidth, measure);

            Assert.True(measure(shown) <= slots.StatusWidth);
            Assert.True(slots.StatusX + measure(shown) + SpinnerGap + SpinnerSize
                <= slots.RefreshX);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(-10)]
        public void ToolbarDegenerateWidths_ClampRatherThanGoingNegative(int barWidth)
        {
            var slots = RankerRowLayout.Toolbar(barWidth, SpinnerSize, SpinnerGap);

            Assert.True(slots.RefreshX >= 0);
            Assert.True(slots.StatusWidth >= 0);
        }

        // The comparison-mode radio strip. Measured footprints: the dot,
        // its gap and the widest of the two option labels at UiFonts.Body,
        // which the view measures for real; these stand in for them.
        private const int RadioLabelWidth = 44;
        private const int FirstOptionWidth = 130;
        private const int SecondOptionWidth = 120;
        private const int OptionGap = 16;
        private const int AddButtonRight = 380;

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_BothModeOptionsFitInsideTheRowAndNeverOverlap(int rowWidth)
        {
            var slots = RankerRowLayout.ModeStrip(
                rowWidth, RadioLabelWidth, FirstOptionWidth, SecondOptionWidth,
                OptionGap, AddButtonRight);

            Assert.True(slots.FirstX >= AddButtonRight);
            Assert.True(slots.FirstX + FirstOptionWidth <= slots.SecondX);
            Assert.True(slots.SecondX + SecondOptionWidth <= rowWidth - RankerRowLayout.Inset);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AtEveryRealWidth_TheCaptionClearsTheControlToItsLeft(int rowWidth)
        {
            var slots = RankerRowLayout.ModeStrip(
                rowWidth, RadioLabelWidth, FirstOptionWidth, SecondOptionWidth,
                OptionGap, AddButtonRight);

            // Shown or dropped, never overlapped: -1 is the view's cue to
            // hide it.
            Assert.True(slots.LabelX == -1 || slots.LabelX >= AddButtonRight);
            if (slots.LabelX >= 0)
            {
                Assert.True(slots.LabelX + RadioLabelWidth <= slots.FirstX);
            }
        }

        [Fact]
        public void WhenTheRowIsTooNarrowForTheCaption_ItIsDroppedRatherThanOverlapped()
        {
            var slots = RankerRowLayout.ModeStrip(
                AddButtonRight + FirstOptionWidth + OptionGap + SecondOptionWidth + RankerRowLayout.Inset,
                RadioLabelWidth, FirstOptionWidth, SecondOptionWidth, OptionGap, AddButtonRight);

            Assert.Equal(-1, slots.LabelX);
            Assert.Equal(AddButtonRight, slots.FirstX);
        }

        [Fact]
        public void AtAnAbsurdlyNarrowWidth_NothingIsPlacedLeftOfTheControlBeforeIt()
        {
            var slots = RankerRowLayout.ModeStrip(
                120, RadioLabelWidth, FirstOptionWidth, SecondOptionWidth, OptionGap, AddButtonRight);

            Assert.Equal(-1, slots.LabelX);
            Assert.Equal(AddButtonRight, slots.FirstX);
            Assert.Equal(AddButtonRight, slots.SecondX);
        }

        // WHY THE COLUMN HEADER HAS TO BE RE-SEATED WHENEVER THE COIN BAND
        // CHANGES. Ready and Days are derived by walking LEFT from the coin
        // band, so a table that has not been refreshed yet (coin band at its
        // MinRemainingCellWidth floor) puts those two rails in a different
        // place than the same table does once a real coin cell has been
        // measured. The header labels are right-aligned on the very same
        // rails, so a view that re-renders its rows without re-seating its
        // header leaves the two disagreeing by exactly this difference -
        // the reported "Ready/Days/Remaining are poorly aligned with the
        // content below" (measured at 37px in the 2026-08-27 capture).
        [Theory]
        [MemberData(nameof(RealWidths))]
        public void AWiderCoinBand_MovesTheReadyAndDaysRailsButNotTheCoinRail(int rowWidth)
        {
            var narrow = RankerRowLayout.Compute(rowWidth, RankerRowLayout.MinRemainingCellWidth);
            var wide = RankerRowLayout.Compute(rowWidth, RankerRowLayout.MinRemainingCellWidth + 37);

            Assert.Equal(narrow.RemainingRightEdge, wide.RemainingRightEdge);
            Assert.Equal(narrow.DaysRightEdge - 37, wide.DaysRightEdge);
            Assert.Equal(narrow.ReadyRightEdge - 37, wide.ReadyRightEdge);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void TheSameBandWidth_GivesTheHeaderAndTheCellsOneSetOfRails(int rowWidth)
        {
            // The view derives both from this one call; anything that
            // recomputes the band has to recompute both sides of it.
            var first = RankerRowLayout.Compute(rowWidth, 137);
            var second = RankerRowLayout.Compute(rowWidth, 137);

            Assert.Equal(first.ReadyRightEdge, second.ReadyRightEdge);
            Assert.Equal(first.DaysRightEdge, second.DaysRightEdge);
            Assert.Equal(first.RemainingRightEdge, second.RemainingRightEdge);
        }

        // Independent mode shows no reorder arrows: its order IS its answer.
        // The two rails they leave behind are reclaimed rather than stranded,
        // which is the module's standing rule about dead space.
        [Theory]
        [MemberData(nameof(RealWidths))]
        public void WithNoReorderButtons_TheRowStillEndsOnItsOneRightEdge(int rowWidth)
        {
            var bands = RankerRowLayout.Compute(rowWidth, 137, showReorder: false);

            Assert.Equal(rowWidth - RankerRowLayout.Inset,
                bands.RemoveX + RankerRowLayout.ButtonWidth);
            Assert.Equal(-1, bands.UpX);
            Assert.Equal(-1, bands.DownX);
        }

        [Theory]
        [MemberData(nameof(RealWidths))]
        public void WithNoReorderButtons_EveryBandToTheirLeftGainsTheirWidth(int rowWidth)
        {
            var withArrows = RankerRowLayout.Compute(rowWidth, 137, showReorder: true);
            var without = RankerRowLayout.Compute(rowWidth, 137, showReorder: false);

            int reclaimed = 2 * (RankerRowLayout.ButtonWidth + RankerRowLayout.ButtonGap);
            Assert.Equal(withArrows.RemainingRightEdge + reclaimed, without.RemainingRightEdge);
            Assert.Equal(withArrows.DaysRightEdge + reclaimed, without.DaysRightEdge);
            Assert.Equal(withArrows.ReadyRightEdge + reclaimed, without.ReadyRightEdge);
            Assert.Equal(withArrows.NameWidth + reclaimed, without.NameWidth);
            Assert.Equal(withArrows.RemoveX, without.RemoveX);
        }
    }
}
