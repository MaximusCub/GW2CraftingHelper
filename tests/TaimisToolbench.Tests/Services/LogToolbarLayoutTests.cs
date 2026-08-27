using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class LogToolbarLayoutTests
    {
        private const int DropdownWidth = 90;
        private const int FollowWidth = 90;
        private const int DeleteWidth = 120;
        private const int ButtonWidth = 100;

        private static LogToolbarLayout.Slots At(int barWidth)
        {
            return LogToolbarLayout.Compute(
                barWidth, DropdownWidth, FollowWidth, DeleteWidth, ButtonWidth, ButtonWidth);
        }

        [Fact]
        public void RightClusterIsPinnedToThePinnedRightEdge()
        {
            var slots = At(1212);

            Assert.Equal(
                PlanRelayoutMath.PinnedRightEdge(1212), slots.ClearViewX + ButtonWidth);
        }

        [Fact]
        public void ButtonsKeepTheirOrderWithExactlyOneGapBetweenThem()
        {
            var slots = At(1212);

            // Delete Log File leftmost of the three: the destructive one is
            // not the easiest to reach.
            Assert.Equal(slots.CopyX - LogToolbarLayout.Gap - DeleteWidth, slots.DeleteX);
            Assert.Equal(slots.ClearViewX - LogToolbarLayout.Gap - ButtonWidth, slots.CopyX);
        }

        [Fact]
        public void LeftClusterStartsAtTheTabsInset()
        {
            var slots = At(1212);

            Assert.Equal(LogToolbarLayout.Inset, slots.SearchX);
            Assert.Equal(slots.SearchX + slots.SearchWidth + LogToolbarLayout.Gap, slots.DropdownX);
            Assert.Equal(slots.DropdownX + DropdownWidth + LogToolbarLayout.Gap, slots.FollowX);
        }

        [Fact]
        public void SearchBoxIsTheOneControlThatFlexes()
        {
            var narrow = At(900);
            var wide = At(2540);

            Assert.True(wide.SearchWidth > narrow.SearchWidth);
            Assert.Equal(LogToolbarLayout.Inset, narrow.SearchX);
            Assert.Equal(LogToolbarLayout.Inset, wide.SearchX);
        }

        [Fact]
        public void SearchBoxStopsGrowingAtItsCap()
        {
            foreach (int barWidth in new[] { 2000, 2540, 4000 })
            {
                Assert.Equal(LogToolbarLayout.SearchMaxWidth, At(barWidth).SearchWidth);
            }
        }

        // Narrowest bar the module can actually present: the narrow-screen
        // client's own panel, less the scrollbar strip. Derived, not picked,
        // so a change to either floor moves this with it.
        private static readonly int NarrowestSupportedBarWidth =
            WindowSizing.TabPanelWidthFor(WindowSizing.NarrowScreenFloorWidth)
                - WindowSizing.ScrollbarAllowance;

        [Fact]
        public void SearchBoxHoldsItsFloorAtEveryWidthTheModuleSupports()
        {
            for (int barWidth = NarrowestSupportedBarWidth; barWidth <= 2600; barWidth += 13)
            {
                Assert.True(
                    At(barWidth).SearchWidth >= LogToolbarLayout.SearchMinWidth,
                    $"search box fell below its floor at barWidth {barWidth}");
            }
        }

        // The narrowest bar that can hold the row at all: every fixed
        // control, every gap, the right margin, and a zero-width search box.
        // Derived from the shipped constants so a widened button moves it.
        private const int NarrowestBarThatHoldsTheRow =
            LogToolbarLayout.Inset
            + LogToolbarLayout.Gap + DropdownWidth
            + LogToolbarLayout.Gap + FollowWidth
            + LogToolbarLayout.Gap + DeleteWidth
            + LogToolbarLayout.Gap + ButtonWidth
            + LogToolbarLayout.Gap + ButtonWidth
            + PlanRelayoutMath.TableRightMargin;

        [Fact]
        public void ClustersDoNotOverlapDownToTheNarrowestBarThatHoldsTheRow()
        {
            // Well below the module's own floor: past its floor the search
            // box keeps shrinking rather than letting the two clusters run
            // into each other.
            Assert.True(NarrowestBarThatHoldsTheRow < NarrowestSupportedBarWidth);

            for (int barWidth = NarrowestBarThatHoldsTheRow; barWidth <= 2600; barWidth += 13)
            {
                var slots = At(barWidth);

                Assert.True(
                    slots.FollowX + FollowWidth <= slots.DeleteX,
                    $"clusters collide at barWidth {barWidth}");
                Assert.True(slots.SearchWidth >= 0);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-500)]
        [InlineData(200)]
        public void DegenerateWidths_CollapseTheSearchBoxRatherThanThrow(int barWidth)
        {
            // Narrower than the row can hold at all: the buttons run off the
            // bar (Blish clips them), but nothing here goes negative-width
            // or throws, and the flexing control is the one that gives.
            var slots = At(barWidth);

            Assert.Equal(0, slots.SearchWidth);
            Assert.Equal(LogToolbarLayout.Inset, slots.SearchX);
        }

        [Theory]
        [InlineData(25, 7)] // Checkbox
        [InlineData(26, 7)] // TextBox
        [InlineData(28, 6)] // StandardButton
        [InlineData(30, 5)] // Dropdown
        public void CenteredY_GivesFourControlHeightsOneOpticalCentre(int height, int expected)
        {
            Assert.Equal(expected, LogToolbarLayout.CenteredY(height));

            // Same distance above and below, to the pixel the integer
            // division allows.
            int below = LogToolbarLayout.BarHeight - height - LogToolbarLayout.CenteredY(height);
            Assert.True(below - LogToolbarLayout.CenteredY(height) <= 1);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        [InlineData(200)]
        public void CenteredY_NeverGoesNegative(int height)
        {
            Assert.True(LogToolbarLayout.CenteredY(height) >= 0);
        }

        [Fact]
        public void InsetMatchesTheGuttersEveryOtherColumnOnTheTabUses()
        {
            // LogToolbarLayout.Inset is DEFINED as LogGutterLayout.GutterX,
            // so comparing the two is a compile-time tautology. The number
            // is what carries information: a moved gutter has to be looked
            // at rather than silently agreeing with itself.
            Assert.Equal(16, LogToolbarLayout.Inset);
            Assert.Equal(16, LogGutterLayout.GutterX);
        }
    }
}
