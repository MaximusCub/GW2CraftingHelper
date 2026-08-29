using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class MultiItemHeaderLayoutTests
    {
        // The shipped run: tier-2 framed icons (40px art + 1px frame a
        // side) at the layout's own gap, with the module's "..." marker
        // measuring about 14px at the header's annotation font.
        private const int IconSize = 42;
        private const int Gap = MultiItemHeaderLayout.IconGap;
        private const int EllipsisWidth = 14;

        [Fact]
        public void RunWidth_CountsGapsBetweenIconsOnly()
        {
            Assert.Equal(0, MultiItemHeaderLayout.RunWidth(0, IconSize, Gap));
            Assert.Equal(IconSize, MultiItemHeaderLayout.RunWidth(1, IconSize, Gap));
            Assert.Equal((3 * IconSize) + (2 * Gap), MultiItemHeaderLayout.RunWidth(3, IconSize, Gap));
        }

        [Fact]
        public void IconX_IsTheSamePitchRunWidthIsBuiltFrom()
        {
            Assert.Equal(0, MultiItemHeaderLayout.IconX(0, IconSize, Gap));
            Assert.Equal(IconSize + Gap, MultiItemHeaderLayout.IconX(1, IconSize, Gap));

            // The last icon's right edge is the whole run's width - the
            // property the renderer relies on to stop at availableWidth.
            Assert.Equal(
                MultiItemHeaderLayout.RunWidth(4, IconSize, Gap),
                MultiItemHeaderLayout.IconX(3, IconSize, Gap) + IconSize);
        }

        [Fact]
        public void EverythingFits_DrawsNoEllipsis()
        {
            var run = MultiItemHeaderLayout.Plan(
                itemCount: 3, availableWidth: 400, iconSize: IconSize, iconGap: Gap,
                ellipsisWidth: EllipsisWidth);

            Assert.Equal(3, run.VisibleCount);
            Assert.Equal(0, run.HiddenCount);
            Assert.False(run.ShowsEllipsis);
            Assert.Equal(MultiItemHeaderLayout.RunWidth(3, IconSize, Gap), run.Width);
        }

        [Fact]
        public void ExactFit_IsNotOverflow()
        {
            int exact = MultiItemHeaderLayout.RunWidth(3, IconSize, Gap);

            var run = MultiItemHeaderLayout.Plan(3, exact, IconSize, Gap, EllipsisWidth);

            Assert.Equal(3, run.VisibleCount);
            Assert.False(run.ShowsEllipsis);
        }

        [Fact]
        public void OnePixelShortOfExact_HidesAnIconAndShowsTheEllipsis()
        {
            int exact = MultiItemHeaderLayout.RunWidth(3, IconSize, Gap);

            var run = MultiItemHeaderLayout.Plan(3, exact - 1, IconSize, Gap, EllipsisWidth);

            // Two icons plus their gaps plus the marker (2 * 48 + 14 =
            // 110) fits in 137; a third would need another 48.
            Assert.Equal(2, run.VisibleCount);
            Assert.Equal(1, run.HiddenCount);
            Assert.True(run.ShowsEllipsis);
            Assert.Equal(2 * (IconSize + Gap), run.EllipsisOffset);
            Assert.Equal(run.EllipsisOffset + EllipsisWidth, run.Width);
        }

        [Fact]
        public void TheEllipsisIsReservedBeforeIconsAreCounted()
        {
            // Room for exactly two icons and nothing else. Because
            // something is hidden, the marker has to fit as well, so the
            // run seats ONE icon - the case a bare availableWidth / pitch
            // division gets wrong.
            int twoIcons = MultiItemHeaderLayout.RunWidth(2, IconSize, Gap);

            var run = MultiItemHeaderLayout.Plan(5, twoIcons, IconSize, Gap, EllipsisWidth);

            Assert.Equal(1, run.VisibleCount);
            Assert.Equal(4, run.HiddenCount);
            Assert.True(run.ShowsEllipsis);
            Assert.Equal(IconSize + Gap, run.EllipsisOffset);
            Assert.True(run.Width <= twoIcons);
        }

        [Fact]
        public void TwentyItems_SeatWhatFitsAndCountTheRestAsHidden()
        {
            var run = MultiItemHeaderLayout.Plan(20, 300, IconSize, Gap, EllipsisWidth);

            Assert.Equal((300 - EllipsisWidth) / (IconSize + Gap), run.VisibleCount);
            Assert.Equal(20 - run.VisibleCount, run.HiddenCount);
            Assert.True(run.ShowsEllipsis);
            Assert.True(run.Width <= 300);
        }

        [Fact]
        public void NarrowWindow_ShowsTheEllipsisAlone()
        {
            var run = MultiItemHeaderLayout.Plan(6, EllipsisWidth, IconSize, Gap, EllipsisWidth);

            Assert.Equal(0, run.VisibleCount);
            Assert.Equal(6, run.HiddenCount);
            Assert.True(run.ShowsEllipsis);
            Assert.Equal(0, run.EllipsisOffset);
            Assert.Equal(EllipsisWidth, run.Width);
        }

        [Fact]
        public void NoRoomEvenForTheEllipsis_DrawsNothing()
        {
            var run = MultiItemHeaderLayout.Plan(6, EllipsisWidth - 1, IconSize, Gap, EllipsisWidth);

            Assert.Equal(0, run.VisibleCount);
            Assert.Equal(6, run.HiddenCount);
            Assert.False(run.ShowsEllipsis);
            Assert.Equal(0, run.Width);
        }

        [Fact]
        public void TitleWiderThanTheWindow_LeavesNegativeRoomAndDrawsNothing()
        {
            var run = MultiItemHeaderLayout.Plan(4, -80, IconSize, Gap, EllipsisWidth);

            Assert.Equal(0, run.VisibleCount);
            Assert.Equal(4, run.HiddenCount);
            Assert.False(run.ShowsEllipsis);
            Assert.Equal(0, run.Width);
        }

        [Fact]
        public void ExactlyTwoItemsInTheBatch_LeavesOneIconToStack()
        {
            var wide = MultiItemHeaderLayout.Plan(1, 400, IconSize, Gap, EllipsisWidth);
            Assert.Equal(1, wide.VisibleCount);
            Assert.False(wide.ShowsEllipsis);

            var narrow = MultiItemHeaderLayout.Plan(1, IconSize - 1, IconSize, Gap, EllipsisWidth);
            Assert.Equal(0, narrow.VisibleCount);
            Assert.Equal(1, narrow.HiddenCount);
            Assert.True(narrow.ShowsEllipsis);
        }

        [Fact]
        public void NothingToStack_IsAnEmptyRun()
        {
            var run = MultiItemHeaderLayout.Plan(0, 400, IconSize, Gap, EllipsisWidth);

            Assert.Equal(0, run.VisibleCount);
            Assert.Equal(0, run.HiddenCount);
            Assert.False(run.ShowsEllipsis);
            Assert.Equal(0, run.Width);
        }

        [Fact]
        public void AMarkerNarrowerThanAnIcon_CanKeepEveryIconButTheLast()
        {
            // The marker is 2px; the run is one pixel short of seating all
            // four icons. Three icons plus the marker still fit, so the
            // last item is the only hidden one.
            int oneShort = MultiItemHeaderLayout.RunWidth(4, IconSize, Gap) - 1;

            var run = MultiItemHeaderLayout.Plan(4, oneShort, IconSize, Gap, ellipsisWidth: 2);

            Assert.Equal(3, run.VisibleCount);
            Assert.Equal(1, run.HiddenCount);
            Assert.True(run.ShowsEllipsis);
            Assert.True(run.Width <= oneShort);
        }
    }
}
