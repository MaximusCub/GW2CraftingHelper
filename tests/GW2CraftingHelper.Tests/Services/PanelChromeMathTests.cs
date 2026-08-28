using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The arithmetic every tab's hosted container is sized by. What these
    /// pin is one property: the size of the content a titled, bordered panel
    /// can hold comes from that panel's real chrome, never from the
    /// panel-sized default a not-yet-laid-out Blish panel reports. The
    /// difference between the two IS the first-paint truncation - a viewport
    /// sized from the wrong one keeps a stale height, and the bottom of the
    /// plan is not drawn until a resize corrects it (KNOWN-ISSUES #65).
    ///
    /// The vendor constants below are Blish_HUD.Controls.Panel's own public
    /// values, restated here as literals because the test project is
    /// deliberately Blish-free: a drift between them and the vendor's is a
    /// Blish upgrade, which is exactly when these numbers should be re-read.
    /// </summary>
    public class PanelChromeMathTests
    {
        private const int HeaderHeight = 36;
        private const int TopPadding = 7;
        private const int RightPadding = 4;
        private const int BottomPadding = 7;
        private const int LeftPadding = 4;

        private static PanelChromeMath.Insets Insets(bool showBorder, bool hasTitle)
        {
            return PanelChromeMath.PanelInsets(
                showBorder,
                hasTitle,
                HeaderHeight,
                TopPadding,
                RightPadding,
                BottomPadding,
                LeftPadding);
        }

        [Fact]
        public void TitledBorderedPanel_ContentHeight_ExcludesHeaderAndBottomPadding()
        {
            var insets = Insets(showBorder: true, hasTitle: true);

            // 600 less the 36px title header and the 7px bottom padding.
            Assert.Equal(600 - HeaderHeight - BottomPadding, PanelChromeMath.ContentHeight(600, insets));
        }

        [Fact]
        public void TitledBorderedPanel_ContentHeight_IsNotThePanelSizedDefault()
        {
            var insets = Insets(showBorder: true, hasTitle: true);

            // The regression this file exists for: Blish's ContentRegion
            // getter falls back to the panel's full size, and its cached
            // rectangle goes stale whenever a layout pass is skipped, so a
            // caller that reads the region back gets 600 where the truth is
            // 557. Sizing a scroll viewport from the larger number is what
            // left the bottom of the plan undrawn.
            Assert.NotEqual(600, PanelChromeMath.ContentHeight(600, insets));
            Assert.Equal(43, 600 - PanelChromeMath.ContentHeight(600, insets));
        }

        [Fact]
        public void TitledBorderedPanel_ContentWidth_ExcludesBothSidePaddings()
        {
            var insets = Insets(showBorder: true, hasTitle: true);

            Assert.Equal(1000 - LeftPadding - RightPadding, PanelChromeMath.ContentWidth(1000, insets));
        }

        [Fact]
        public void BorderWithoutTitle_TopInsetFallsBackToTopPadding()
        {
            var insets = Insets(showBorder: true, hasTitle: false);

            Assert.Equal(TopPadding, insets.Top);
            Assert.Equal(600 - TopPadding - BottomPadding, PanelChromeMath.ContentHeight(600, insets));
        }

        [Fact]
        public void TitleWithoutBorder_ReservesTheHeaderAndNothingElse()
        {
            var insets = Insets(showBorder: false, hasTitle: true);

            Assert.Equal(HeaderHeight, insets.Top);
            Assert.Equal(0, insets.Left);
            Assert.Equal(0, insets.Right);
            Assert.Equal(0, insets.Bottom);
            Assert.Equal(600 - HeaderHeight, PanelChromeMath.ContentHeight(600, insets));
        }

        [Fact]
        public void BarePanel_ContentIsItsOwnSize()
        {
            var insets = Insets(showBorder: false, hasTitle: false);

            // The one shape where the panel-sized default IS the truth -
            // which is why the hosted view's own container is built this way
            // and every tab may read its ContentRegion.
            Assert.Equal(600, PanelChromeMath.ContentHeight(600, insets));
            Assert.Equal(1000, PanelChromeMath.ContentWidth(1000, insets));
        }

        [Fact]
        public void PaddedContent_RemovesThePadOnBothEdges()
        {
            var insets = Insets(showBorder: true, hasTitle: true);

            Assert.Equal(
                600 - HeaderHeight - BottomPadding - 20,
                PanelChromeMath.PaddedContentHeight(600, insets, 10));
            Assert.Equal(
                1000 - LeftPadding - RightPadding - 20,
                PanelChromeMath.PaddedContentWidth(1000, insets, 10));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(20)]
        [InlineData(43)]
        public void ContentHeight_NeverGoesNegative(int outerHeight)
        {
            var insets = Insets(showBorder: true, hasTitle: true);

            // Control.Size ignores a negative component outright, leaving the
            // child at whatever size it already had - a stale size reached
            // from the other end. 0 shrinks it honestly.
            Assert.Equal(0, PanelChromeMath.ContentHeight(outerHeight, insets));
            Assert.Equal(0, PanelChromeMath.PaddedContentHeight(outerHeight, insets, 10));
        }

        [Fact]
        public void PaddedContent_NeverGoesNegative_WhenThePadExceedsTheRegion()
        {
            var insets = Insets(showBorder: true, hasTitle: true);

            Assert.Equal(0, PanelChromeMath.PaddedContentWidth(20, insets, 10));
        }
    }
}
