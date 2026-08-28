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
        public void SizeThenPosition_ASizeDraggedOutOnTheUltrawide_PutsTheGripBackOnScreen()
        {
            // The trap the position clamp alone cannot open: a session that
            // DID drag the resize grip on the ultrawide persists that size,
            // Blish restores it whole, and it is wider AND taller than a
            // 1920x1080 client. The window is fitted first and positioned
            // against the fitted size, in that order, because the position
            // rule reads the extent.
            const int SavedWidth = UltrawideWidth;
            const int SavedHeight = UltrawideHeight;
            const int ClientWidth = 1920;
            const int ClientHeight = 1080;
            Assert.True(SavedWidth > ClientWidth);
            Assert.True(SavedHeight > ClientHeight);

            int width = WindowPlacement.ClampExtent(
                SavedWidth, WindowSizing.EffectiveMinWindowWidth(ClientWidth), ClientWidth);
            int height = WindowPlacement.ClampExtent(
                SavedHeight, WindowSizing.MinWindowHeight, ClientHeight);
            int x = WindowPlacement.ClampAxis(900, width, ClientWidth);
            int y = WindowPlacement.ClampAxis(900, height, ClientHeight);

            // The grip is the window's bottom-right corner, and it is what
            // the user needs in order to undo the size themselves.
            Assert.True(x + width <= ClientWidth);
            Assert.True(y + height <= ClientHeight);
            Assert.True(x >= 0);
            Assert.True(y >= 0);
        }

        [Fact]
        public void ClampExtent_TheSameSizeOnThePortraitClient_ShrinksOnlyTheAxisThatOverflows()
        {
            // 3440x1440 saved, restored on 1080x1920: the width overflows and
            // the height does not, so exactly one axis moves. A ceiling that
            // fitted both axes to the smaller screen dimension would lose
            // 480px of a window that fits.
            Assert.Equal(
                PortraitWidth,
                WindowPlacement.ClampExtent(
                    UltrawideWidth,
                    WindowSizing.EffectiveMinWindowWidth(PortraitWidth),
                    PortraitWidth));

            Assert.Equal(
                UltrawideHeight,
                WindowPlacement.ClampExtent(
                    UltrawideHeight, WindowSizing.MinWindowHeight, PortraitHeight));
        }

        [Theory]
        [InlineData(PortraitWidth, PortraitHeight)]
        [InlineData(UltrawideWidth, UltrawideHeight)]
        [InlineData(1920, 1080)]
        [InlineData(1366, 768)]
        public void ClampExtent_OnEveryScreenTheWindowFits_NeverLeavesTheWindowLargerThanTheClient(
            int screenWidth, int screenHeight)
        {
            int minWidth = WindowSizing.EffectiveMinWindowWidth(screenWidth);
            Assert.True(minWidth <= screenWidth);
            Assert.True(screenHeight >= WindowSizing.MinWindowHeight);

            foreach (int width in new[] { 0, minWidth - 1, minWidth, screenWidth, 5000 })
            {
                int fitted = WindowPlacement.ClampExtent(width, minWidth, screenWidth);

                Assert.True(fitted <= screenWidth);
                Assert.True(fitted >= minWidth);
            }

            int minHeight = WindowSizing.MinWindowHeight;
            foreach (int height in new[] { 0, minHeight, screenHeight, 5000 })
            {
                int fitted = WindowPlacement.ClampExtent(height, minHeight, screenHeight);

                Assert.True(fitted <= screenHeight);
                Assert.True(fitted >= minHeight);
            }
        }

        [Theory]
        [InlineData(800)]
        [InlineData(640)]
        public void ClampExtent_ClientNarrowerThanTheWindowFloor_TheFloorWins(int screenWidth)
        {
            // Floor and ceiling converge and cross below
            // NarrowScreenFloorWidth. The floor wins, so this stays the
            // leading-edge case ClampAxis_ClientNarrowerThanTheWindowFloor_
            // PinsTheLeadingEdge covers, and the enforced minimum the window
            // grows back to on its next layout pass is the value returned
            // here - the two clamps agree instead of oscillating.
            int minWidth = WindowSizing.EffectiveMinWindowWidth(screenWidth);
            Assert.Equal(WindowSizing.NarrowScreenFloorWidth, minWidth);
            Assert.True(minWidth > screenWidth);

            Assert.Equal(minWidth, WindowPlacement.ClampExtent(UltrawideWidth, minWidth, screenWidth));
            Assert.Equal(minWidth, WindowPlacement.ClampExtent(minWidth, minWidth, screenWidth));
            Assert.Equal(minWidth, WindowPlacement.ClampExtent(500, minWidth, screenWidth));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ClampExtent_UnknownScreenExtent_AppliesTheFloorAndNoCeiling(int screenExtent)
        {
            // What the size path did before it had a ceiling at all: an
            // unsettled sprite screen is not a reason to shrink anything.
            Assert.Equal(
                UltrawideWidth,
                WindowPlacement.ClampExtent(UltrawideWidth, WindowSizing.MinWindowWidth, screenExtent));
            Assert.Equal(
                WindowSizing.MinWindowWidth,
                WindowPlacement.ClampExtent(930, WindowSizing.MinWindowWidth, screenExtent));
        }

        [Fact]
        public void ClampExtent_DoesNotRegressTheFloor_OnAClientTheMinimumFitsOn()
        {
            // A size persisted below the current minimum still grows: the
            // ceiling is added to the floor, not swapped for it.
            int minWidth = WindowSizing.EffectiveMinWindowWidth(UltrawideWidth);
            Assert.Equal(WindowSizing.MinWindowWidth, minWidth);

            Assert.Equal(minWidth, WindowPlacement.ClampExtent(930, minWidth, UltrawideWidth));
            Assert.Equal(
                WindowSizing.MinWindowHeight,
                WindowPlacement.ClampExtent(400, WindowSizing.MinWindowHeight, UltrawideHeight));
        }

        [Fact]
        public void ClampExtent_IsIdempotent()
        {
            int minWidth = WindowSizing.EffectiveMinWindowWidth(PortraitWidth);

            foreach (int width in new[] { 0, 500, 930, minWidth, UltrawideWidth, 5000 })
            {
                int once = WindowPlacement.ClampExtent(width, minWidth, PortraitWidth);
                Assert.Equal(once, WindowPlacement.ClampExtent(once, minWidth, PortraitWidth));
            }
        }
    }
}
