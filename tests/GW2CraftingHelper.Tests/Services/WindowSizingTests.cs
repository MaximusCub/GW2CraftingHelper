using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class WindowSizingTests
    {
        [Theory]
        [InlineData(2560)]
        [InlineData(1920)]
        [InlineData(WindowSizing.MinWindowWidth)]
        public void EffectiveMinWindowWidth_ClientAtLeastAsWideAsTheMinimum_KeepsTheMinimum(int screenWidth)
        {
            Assert.Equal(WindowSizing.MinWindowWidth, WindowSizing.EffectiveMinWindowWidth(screenWidth));
        }

        [Theory]
        [InlineData(1366)]
        [InlineData(1280)]
        [InlineData(1024)]
        public void EffectiveMinWindowWidth_WindowedClientNarrowerThanTheMinimum_FitsTheClient(int screenWidth)
        {
            // SpriteScreen is the game client, not the monitor: a windowed
            // 1280x720 player must not get a window whose right edge - and
            // with it the resize grip - is off-screen.
            Assert.Equal(screenWidth, WindowSizing.EffectiveMinWindowWidth(screenWidth));
        }

        [Theory]
        [InlineData(800)]
        [InlineData(1)]
        public void EffectiveMinWindowWidth_ClientBelowTheFloor_StopsAtTheFloor(int screenWidth)
        {
            // Below the pre-raise minimum the module has never been usable
            // anyway; shrinking further would break layouts that predate
            // this change.
            Assert.Equal(WindowSizing.NarrowScreenFloorWidth, WindowSizing.EffectiveMinWindowWidth(screenWidth));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void EffectiveMinWindowWidth_UnknownClientWidth_KeepsTheFullMinimum(int screenWidth)
        {
            Assert.Equal(WindowSizing.MinWindowWidth, WindowSizing.EffectiveMinWindowWidth(screenWidth));
        }

        // Historical literal, deliberately not read from production: this is
        // the number the three private copies (LogTabContent's, MainView's
        // source-filter one, SnapshotItemGridLayout's) each carried before
        // the consolidation, and the fence that says the consolidation moved
        // no layout.
        private const int ScrollbarAllowanceBeforeConsolidation = 20;

        [Fact]
        public void ScrollbarAllowance_IsTheNumberTheThreePrivateCopiesUsed()
        {
            Assert.Equal(ScrollbarAllowanceBeforeConsolidation, WindowSizing.ScrollbarAllowance);
            Assert.Equal(WindowSizing.ScrollbarAllowance, SnapshotItemGridLayout.ScrollbarAllowance);
        }

        [Fact]
        public void RightEdgePadding_IsTheSameNumberSeenFromThePaddingSide()
        {
            Assert.Equal(WindowSizing.ScrollbarAllowance, WindowSizing.RightEdgePadding);
        }

        [Fact]
        public void WindowToTabPanelChrome_StillAccountsForOneScrollbarAllowance()
        {
            Assert.Equal(
                46 + 32 + 8 + 20 + WindowSizing.ScrollbarAllowance,
                WindowSizing.WindowToTabPanelChrome);
        }
    }
}
