using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class WindowPlacementTests
    {
        // The measured defect: a position saved on a 3440x1440 ultrawide,
        // restored on a 1080x1920 portrait display, left roughly a third of
        // the window - cost column, Generate button, bottom-right resize
        // grip - past the right edge with no way to drag it back.
        private const int UltrawideWidth = 3440;
        private const int UltrawideHeight = 1440;
        private const int PortraitWidth = 1080;
        private const int PortraitHeight = 1920;

        [Fact]
        public void ClampAxis_TheMeasuredDefect_PullsTheWindowBackOntoThePortraitClient()
        {
            // Blish persists a position only on a drag release and a size
            // only on a resize-drag release
            // (WindowBase2.OnGlobalMouseRelease), so a session that moved the
            // window on the ultrawide but never resized it comes back at the
            // width THIS client's own floor gives, with the saved x intact:
            // 340px of the window, resize grip included, past the right edge.
            const int SavedX = 340;
            int windowWidth = WindowSizing.EffectiveMinWindowWidth(PortraitWidth);
            Assert.Equal(PortraitWidth, windowWidth);
            Assert.True(SavedX + windowWidth > PortraitWidth);

            int clamped = WindowPlacement.ClampAxis(SavedX, windowWidth, PortraitWidth);

            Assert.Equal(0, clamped);
            Assert.True(clamped + windowWidth <= PortraitWidth);
        }

        [Fact]
        public void ClampAxis_OnItsOwnScreen_LeavesACenteredWindowWhereItIs()
        {
            int windowWidth = WindowSizing.EffectiveMinWindowWidth(UltrawideWidth);
            int centeredX = (UltrawideWidth - windowWidth) / 2;

            Assert.Equal(centeredX, WindowPlacement.ClampAxis(centeredX, windowWidth, UltrawideWidth));
            Assert.Equal(
                (UltrawideHeight - WindowSizing.MinWindowHeight) / 2,
                WindowPlacement.ClampAxis(
                    (UltrawideHeight - WindowSizing.MinWindowHeight) / 2,
                    WindowSizing.MinWindowHeight,
                    UltrawideHeight));
        }

        [Theory]
        [InlineData(PortraitWidth, PortraitHeight)]
        [InlineData(UltrawideWidth, UltrawideHeight)]
        [InlineData(1920, 1080)]
        [InlineData(1366, 768)]
        public void ClampAxis_OnEveryScreenTheWindowFits_TitleBarAndGripBothLandOnScreen(
            int screenWidth, int screenHeight)
        {
            int windowWidth = WindowSizing.EffectiveMinWindowWidth(screenWidth);
            int windowHeight = WindowSizing.MinWindowHeight;
            Assert.True(windowWidth <= screenWidth);
            Assert.True(windowHeight <= screenHeight);

            foreach (int x in new[] { -5000, -1, 0, screenWidth / 2, screenWidth, 5000 })
            {
                int clampedX = WindowPlacement.ClampAxis(x, windowWidth, screenWidth);

                // The title bar spans the window's full top edge and the
                // resize grip is its bottom-right corner, so "both reachable"
                // is the whole axis on screen.
                Assert.True(clampedX >= 0);
                Assert.True(clampedX + windowWidth <= screenWidth);
            }

            foreach (int y in new[] { -5000, -1, 0, screenHeight / 2, screenHeight, 5000 })
            {
                int clampedY = WindowPlacement.ClampAxis(y, windowHeight, screenHeight);

                Assert.True(clampedY >= 0);
                Assert.True(clampedY + windowHeight <= screenHeight);
            }
        }

        [Theory]
        [InlineData(800)]
        [InlineData(640)]
        public void ClampAxis_ClientNarrowerThanTheWindowFloor_PinsTheLeadingEdge(int screenWidth)
        {
            // A client below WindowSizing's own fallback floor is supported,
            // not broken: the size clamp stops shrinking there, so the window
            // is genuinely wider than the screen and the two guarantees
            // cannot both be met. The leading edge wins - a visible title bar
            // can be dragged to bring the grip into view.
            int windowWidth = WindowSizing.EffectiveMinWindowWidth(screenWidth);
            Assert.Equal(WindowSizing.NarrowScreenFloorWidth, windowWidth);
            Assert.True(windowWidth > screenWidth);

            Assert.Equal(0, WindowPlacement.ClampAxis(340, windowWidth, screenWidth));
            Assert.Equal(0, WindowPlacement.ClampAxis(-340, windowWidth, screenWidth));
            Assert.Equal(0, WindowPlacement.ClampAxis(0, windowWidth, screenWidth));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ClampAxis_UnknownScreenExtent_LeavesThePositionAlone(int screenExtent)
        {
            Assert.Equal(340, WindowPlacement.ClampAxis(340, WindowSizing.MinWindowWidth, screenExtent));
            Assert.Equal(-40, WindowPlacement.ClampAxis(-40, WindowSizing.MinWindowWidth, screenExtent));
        }

        [Fact]
        public void ClampAxis_IsIdempotent()
        {
            int windowWidth = WindowSizing.EffectiveMinWindowWidth(UltrawideWidth);

            foreach (int x in new[] { -5000, -1, 0, 340, PortraitWidth, 5000 })
            {
                int once = WindowPlacement.ClampAxis(x, windowWidth, PortraitWidth);
                Assert.Equal(once, WindowPlacement.ClampAxis(once, windowWidth, PortraitWidth));
            }
        }

        [Fact]
        public void ClampAxis_SizeSavedWiderThanTheClient_PinsTheLeadingEdgeAndStillFitsTheOtherAxis()
        {
            // A session that DID drag the resize grip persists that size, and
            // nothing shrinks a too-large window back down - so the two axes
            // can land on different sides of the rule at once.
            const int SavedWidth = 2000;
            const int SavedHeight = 1200;

            // Wider than the portrait client: leading edge wins, and the grip
            // stays out of reach until the user drags the title bar.
            Assert.Equal(0, WindowPlacement.ClampAxis(900, SavedWidth, PortraitWidth));

            // Shorter than it: fully on screen, grip included.
            int y = WindowPlacement.ClampAxis(900, SavedHeight, PortraitHeight);
            Assert.True(y >= 0);
            Assert.True(y + SavedHeight <= PortraitHeight);
        }
    }
}
