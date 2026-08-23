using System;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The module window's size floor and the chrome between that window and
    /// a tab's content panel. Lives here rather than in Module.cs so the
    /// layout tests assert against the SAME constants the window is built
    /// from instead of re-typing them - a change to the minimum has to move
    /// the tests with it.
    /// </summary>
    public static class WindowSizing
    {
        /// <summary>
        /// Narrowest window the recipe tree stays readable in, measured in
        /// docs/research/minimum-window-width.md: the deepest chain in the
        /// game is "+24 Agony Infusion" at depth 23, whose deepest row is
        /// "4194304x Thermocatalytic Reagent". At this width that row keeps
        /// the tree's designed 24px name-to-column gutter and one further
        /// indent level still renders untruncated.
        /// <para>
        /// 1472, not the 1436 the module shipped with: the research's +2pt
        /// row-text variant landed (row text Font14 -> Font16, see
        /// Views/Rendering/UiFonts), and the deepest row's name run grows
        /// with it. The research measured that variant directly at
        /// Menomonia 16 rather than scaling the 14 figures, so this is a
        /// measured number, not a 14.3% inflation of the old one.
        /// </para>
        /// </summary>
        public const int MinWindowWidth = 1472;

        /// <summary>
        /// Unchanged by the width raise: no layout math in the module
        /// derives a height from the window width.
        /// </summary>
        public const int MinWindowHeight = 710;

        /// <summary>
        /// Floor the enforced minimum falls back to on a game client
        /// narrower than <see cref="MinWindowWidth"/> - the width the module
        /// shipped with before the raise, and the width the window texture's
        /// region is authored at. On such a client deep tree rows ellipsize
        /// again, which is strictly better than a window whose right edge
        /// (cost column, Generate button, resize grip) is off-screen.
        /// </summary>
        public const int NarrowScreenFloorWidth = 930;

        /// <summary>
        /// Horizontal chrome between the window's own width and the panel
        /// width a tab's content is rendered into. All of it read from this
        /// repo's source except the border term:
        /// <code>
        ///  46  window region 930 - content region 884        (Module.cs)
        ///  32  ViewAdapter OUTER_PADDING x2                  (ViewAdapter.cs)
        ///   8  Blish Panel border chrome, ~4 a side          (ViewAdapter.cs)
        ///  20  ViewAdapter INNER_PADDING x2                  (ViewAdapter.cs)
        ///  20  RightEdgePadding, clear of the scrollbar      (tab content)
        /// </code>
        /// The 8px border term is worth +/-2px; nothing derived from it is
        /// within 400px of a layout boundary.
        /// </summary>
        public const int WindowToTabPanelChrome = 46 + 32 + 8 + 20 + 20;

        /// <summary>Panel width a tab's content gets inside a window of this width.</summary>
        public static int TabPanelWidthFor(int windowWidth)
        {
            return windowWidth - WindowToTabPanelChrome;
        }

        /// <summary>
        /// Minimum actually enforced on a client of the given width.
        /// <see cref="MinWindowWidth"/> is wider than an ordinary windowed
        /// GW2 client (1280x720, 1366x768), and Blish's SpriteScreen is that
        /// client, not the monitor. Enforcing the full minimum there would
        /// push the window's right edge - and with it the bottom-right
        /// resize grip - off-screen, leaving no way to shrink the window
        /// back. Screen width 0 or less (unknown) keeps the full minimum.
        /// </summary>
        public static int EffectiveMinWindowWidth(int screenWidth)
        {
            if (screenWidth <= 0)
            {
                return MinWindowWidth;
            }

            return Math.Max(NarrowScreenFloorWidth, Math.Min(MinWindowWidth, screenWidth));
        }
    }
}
