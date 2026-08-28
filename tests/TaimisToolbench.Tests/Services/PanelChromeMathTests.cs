using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
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
    ///
    /// The second half of the file walks the same chain end to end, from the
    /// window height down to the panel a tab renders into, at window sizes
    /// from the module's floor to a 4K-tall client. What it pins is the
    /// bottom of that chain: the viewport used to stop 74px above the window
    /// while its top sat flush under the title bar, because the content
    /// region Module.cs hands Blish was authored window-region-relative and
    /// Blish reads it as absolute texture coordinates (KNOWN-ISSUES #66).
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

        // Window heights the sweep runs at: the module's own floor, the
        // constructed size, the common 16:9 client heights, a 1440p client
        // (the one this was reported from) and a 4K one. The defect is a
        // constant, so a single size would have caught it - the sweep is
        // what proves it is a constant and not a ratio.
        [Theory]
        [InlineData(710)]
        [InlineData(750)]
        [InlineData(900)]
        [InlineData(1080)]
        [InlineData(1200)]
        [InlineData(1440)]
        [InlineData(2160)]
        public void ViewportBottomGap_IsTheBudgetAtEveryWindowHeight(int windowHeight)
        {
            Assert.Equal(BottomGapBudget, BottomGap(windowHeight));
        }

        [Theory]
        [InlineData(710)]
        [InlineData(750)]
        [InlineData(900)]
        [InlineData(1080)]
        [InlineData(1200)]
        [InlineData(1440)]
        [InlineData(2160)]
        public void TabPanelHeightFor_IsTheChainViewAdapterActuallyWalks(int windowHeight)
        {
            Assert.Equal(WindowSizing.TabPanelHeightFor(windowHeight), TabPanelHeight(windowHeight));
        }

        [Fact]
        public void TabPanelHeight_GrowsOneForOneWithTheWindow()
        {
            // A ratio error - the other shape a short viewport could take -
            // would show up here as a delta that is not 1.
            for (int windowHeight = 710; windowHeight < 2160; windowHeight += 137)
            {
                Assert.Equal(
                    TabPanelHeight(windowHeight) + 1,
                    TabPanelHeight(windowHeight + 1));
            }
        }

        [Fact]
        public void WindowBottomMargin_IsNoLargerThanThePanelInsetBesideIt()
        {
            // The regression in one line. Blish's own bottom margin is the
            // outermost of the four terms below the viewport, and it is the
            // one the module chooses: 41px of it (windowRegion.Bottom 736
            // less contentRegion.Bottom 695) put the viewport 74px above the
            // window while the top margin was 0. Anything at or under one
            // panel inset keeps the two ends of the window comparable.
            Assert.True(
                WindowSizing.WindowContentBottomMargin <= WindowSizing.TabPanelOuterPadding,
                $"window bottom margin {WindowSizing.WindowContentBottomMargin} exceeds "
                    + $"the {WindowSizing.TabPanelOuterPadding}px panel inset beside it");
            Assert.Equal(0, WindowSizing.WindowContentTop - TitleBarHeight);
        }

        [Fact]
        public void WindowToTabPanelChrome_MatchesTheBudgetOnEachEdge()
        {
            Assert.Equal(WindowBottomMarginBudget, WindowSizing.WindowContentBottomMargin);
            Assert.Equal(BottomGapBudget, WindowSizing.WindowToTabPanelBottomChrome);
            Assert.Equal(
                TitleBarHeight + OuterPadding + HeaderHeight + InnerPadding,
                WindowSizing.WindowToTabPanelTopChrome);
        }

        [Fact]
        public void TabPanelHeight_FloorsAtZeroForAWindowShorterThanItsOwnChrome()
        {
            Assert.Equal(0, WindowSizing.TabPanelHeightFor(0));
            Assert.Equal(0, WindowSizing.TabPanelHeightFor(WindowSizing.WindowToTabPanelTopChrome));
            Assert.True(WindowSizing.TabPanelHeightFor(WindowSizing.MinWindowHeight) > 0);
        }

        private const int TitleBarHeight = 40;
        private const int OuterPadding = WindowSizing.TabPanelOuterPadding;
        private const int InnerPadding = WindowSizing.TabPanelInnerPadding;

        // Written as literals, not summed off WindowSizing: a budget that
        // follows the constant it is meant to hold cannot fail. 15 is the
        // texture rows between the content region's bottom and the window
        // region's bottom, and it is generous - background 502049 is still
        // 88% opaque at the window region's own last row. The other three
        // are the panel inset, Blish's Panel.BOTTOM_PADDING and the inner
        // inset. 74 is what the four came to while the content region was
        // authored window-region-relative (KNOWN-ISSUES #66).
        private const int WindowBottomMarginBudget = 15;
        private const int BottomGapBudget =
            WindowBottomMarginBudget + OuterPadding + BottomPadding + InnerPadding;

        /// <summary>
        /// The chain Views/ViewAdapter.cs builds, rebuilt from the shipped
        /// constants: Blish sizes the window's content region, the adapter
        /// insets a titled, bordered panel by OUTER on every edge, and the
        /// container a tab renders into is that panel's content region less
        /// INNER on every edge. Only the window's own arithmetic is restated
        /// here; the panel chrome comes from the production helper.
        /// </summary>
        private static int TabPanelHeight(int windowHeight)
        {
            int bordered = WindowSizing.WindowContentHeightFor(windowHeight) - (2 * OuterPadding);

            return PanelChromeMath.PaddedContentHeight(
                bordered, Insets(showBorder: true, hasTitle: true), InnerPadding);
        }

        /// <summary>
        /// Control-space distance from the bottom of that container to the
        /// bottom of the window - the band the report was about.
        /// </summary>
        private static int BottomGap(int windowHeight)
        {
            int top = WindowSizing.WindowContentTop + OuterPadding + HeaderHeight + InnerPadding;

            return windowHeight - (top + TabPanelHeight(windowHeight));
        }

        [Fact]
        public void PaddedContent_NeverGoesNegative_WhenThePadExceedsTheRegion()
        {
            var insets = Insets(showBorder: true, hasTitle: true);

            Assert.Equal(0, PanelChromeMath.PaddedContentWidth(20, insets, 10));
        }
    }
}
