using System;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// The module window's size floor and the chrome between that window and
    /// a tab's content panel. Lives here rather than in Module.cs so the
    /// layout tests assert against the SAME constants the window is built
    /// from instead of re-typing them - a change to the minimum has to move
    /// the tests with it.
    /// </summary>
    internal static class WindowSizing
    {
        /// <summary>
        /// Narrowest window the recipe tree stays readable in. Measured for
        /// the deepest REALISTIC chain rather than the deepest chain that
        /// exists: the legendary trinkets Transcendence and Conflux, both
        /// exactly depth 14, whose widest row at every font size is the
        /// dust-promotion blow-up "429750x Pile of Glittering Dust".
        /// <para>
        /// Chain, all terms measured at Menomonia 16 against the installed
        /// XNBs (docs/research/minimum-window-width.md section 9, which
        /// reproduces the method and every anchor figure of that report's
        /// own 1478-era derivation):
        /// <code>
        ///  629  widestNameEnd  = nameX(14) 394 + "429750x " 69 + name 166
        ///  +24  the designed name-to-pill gutter at the deepest row
        /// +256  TreePillColumnWidth
        /// +335  cost column: 181 worst-digit six-digit-gold coin run
        ///                    + 154 widest two-currency vendor run
        ///   +8  TableRightMargin
        /// ---- 1252  tab panel
        /// +126  WindowToTabPanelChrome
        /// ==== 1378
        /// </code>
        /// </para>
        /// <para>
        /// 1378, not the 1232 the like-for-like depth-14 arithmetic gives
        /// on its own: 1232 accepts that a row combining a forced-craft
        /// dust chain with a vendor currency run ellipsizes, and the
        /// maintainer declined that trade - "we are designing for a minimum
        /// resolution of 1920x1080, so cramming down to a smaller min-size
        /// that will result in cramped renders seems bad". The +154 rider
        /// is what buys "a two-currency vendor run always fits at the
        /// floor". Pinned by DeepestRealisticRowAtTheWindowMinimum in
        /// PlanRelayoutMathTests.
        /// </para>
        /// <para>
        /// Down from 1478, which fitted the depth-23 "+24 Agony Infusion"
        /// chain untruncated. That chain now ellipsizes from depth 20 -
        /// six levels past the deepest realistic plan, and exactly the
        /// idiom of record everywhere else in the view (ellipsis, full name
        /// on the tooltip).
        /// </para>
        /// <para>
        /// The other contributor to this floor is the controls row, which
        /// is subsumed: its widest arrangement is the "Value Own Materials"
        /// checkbox at x=350 (its label measures 145px at Blish's own
        /// Font14, plus the box) clearing the right-anchored 120px Generate
        /// Plan button and <see cref="WindowToTabPanelChrome"/>'s trailing
        /// padding - under 700px all told, half of what the tree needs.
        /// </para>
        /// </summary>
        public const int MinWindowWidth = 1378;

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
        ///  32  TabPanelOuterPadding x2
        ///   8  Blish Panel border chrome, ~4 a side
        ///  20  TabPanelInnerPadding x2
        ///  20  RightEdgePadding, clear of the scrollbar      (tab content)
        /// </code>
        /// The 8px border term is worth +/-2px; nothing derived from it is
        /// within 400px of a layout boundary.
        /// </summary>
        public const int WindowToTabPanelChrome =
            46 + (2 * TabPanelOuterPadding) + 8 + (2 * TabPanelInnerPadding) + RightEdgePadding;

        /// <summary>
        /// Width the vertical scrollbar of a scrolling content panel
        /// occupies, and therefore the width every tab subtracts from its
        /// container before placing anything: content laid out inside the
        /// full container runs under the scrollbar strip.
        /// <para>
        /// Named ONCE, here, beside
        /// <see cref="WindowToTabPanelChrome"/>'s own accounting of the same
        /// 20px. Three private copies of this number existed - in
        /// LogTabContent, SnapshotItemGridLayout and MainView's
        /// source-filter flow - and a fourth would have arrived with every
        /// tab that gained a right edge.
        /// </para>
        /// </summary>
        public const int ScrollbarAllowance = 20;

        /// <summary>
        /// The same number seen from the padding side, kept because
        /// <see cref="WindowToTabPanelChrome"/>'s derivation and the plan
        /// tab's own chrome are written in these terms. One definition, two
        /// names for the two things it is doing.
        /// </summary>
        public const int RightEdgePadding = ScrollbarAllowance;

        /// <summary>
        /// Inset between the window's content region and the bordered panel
        /// a tab's view is built into, on all four edges (Views/ViewAdapter.cs,
        /// which matches Blish's own WindowBase2.STANDARD_MARGIN here).
        /// </summary>
        public const int TabPanelOuterPadding = 16;

        /// <summary>
        /// Inset between that bordered panel's content region and the
        /// container a tab actually renders into, on all four edges.
        /// </summary>
        public const int TabPanelInnerPadding = 10;

        /// <summary>Panel width a tab's content gets inside a window of this width.</summary>
        public static int TabPanelWidthFor(int windowWidth)
        {
            return windowWidth - WindowToTabPanelChrome;
        }

        /// <summary>
        /// Texture-space rows of background 502049 that Module.cs hands
        /// Blish as the window region and the content region. Blish reads
        /// BOTH as absolute texture coordinates: its bottom margin is
        /// <c>windowRegion.Bottom - contentRegion.Bottom</c>, and its top
        /// inset cancels <see cref="WindowContentRegionTop"/> against the
        /// title bar's own vertical offset. A content region authored
        /// window-region-relative therefore pays
        /// <see cref="WindowRegionTop"/> twice at the bottom and nothing at
        /// the top, which is the asymmetry KNOWN-ISSUES #66 records.
        /// </summary>
        public const int WindowRegionTop = 26;

        /// <summary>Height of that window region.</summary>
        public const int WindowRegionHeight = 710;

        /// <summary>Texture-space top of the content region.</summary>
        public const int WindowContentRegionTop = 11;

        /// <summary>
        /// Height of the content region, set so
        /// <see cref="WindowContentBottomMargin"/> comes out at 15 rows.
        /// The clearance is for the texture's soft bottom edge, measured off
        /// asset 502049 rather than guessed: alpha there is 223-241 of 255
        /// at row 736 - the window region's own bottom - and stays above 200
        /// until row 744, so the window region already ends inside the
        /// opaque area and 15 rows of margin is generous.
        /// </summary>
        public const int WindowContentRegionHeight = 710;

        /// <summary>
        /// Blish's WindowBase2 <c>_contentMargin.Y</c>: the control-space
        /// gap left below the window's content region, constant at every
        /// window height.
        /// </summary>
        public const int WindowContentBottomMargin =
            (WindowRegionTop + WindowRegionHeight)
            - (WindowContentRegionTop + WindowContentRegionHeight);

        // WindowBase2.STANDARD_TITLEBAR_HEIGHT / _VERTICAL_OFFSET and
        // Panel.HEADER_HEIGHT / BOTTOM_PADDING, restated as literals to keep
        // this class arithmetic. Views/ViewAdapter.cs feeds PanelChromeMath
        // the vendor's own values at runtime, so a Blish upgrade that moves
        // either one moves the real layout and leaves these behind: they are
        // to be re-read on that upgrade and nothing else checks them.
        private const int TitleBarHeight = 40;
        private const int TitleBarVerticalOffset = 11;
        private const int PanelHeaderHeight = 36;
        private const int PanelBottomPadding = 7;

        /// <summary>
        /// Control-space top of the window's content region, mirroring
        /// WindowBase2.ConstructWindow: the content region's texture-space
        /// top, plus the title bar, less the window's own top padding
        /// (floored at the title bar's vertical offset). It lands on the
        /// title bar's height exactly - the content region begins flush
        /// under the title bar with no top margin at all, which is the half
        /// of the pair that makes a bottom margin visible.
        /// </summary>
        public const int WindowContentTop =
            WindowContentRegionTop + TitleBarHeight
            - (WindowRegionTop - TitleBarHeight > TitleBarVerticalOffset
                ? WindowRegionTop - TitleBarHeight
                : TitleBarVerticalOffset);

        /// <summary>
        /// Content-region height Blish gives a window of this height,
        /// mirroring WindowBase2.OnResized.
        /// </summary>
        public static int WindowContentHeightFor(int windowHeight)
        {
            return Math.Max(0, windowHeight - WindowContentTop - WindowContentBottomMargin);
        }

        /// <summary>
        /// Vertical chrome between the window's own height and the panel a
        /// tab's content is rendered into - the twin of
        /// <see cref="WindowToTabPanelChrome"/>:
        /// <code>
        /// above  40 title bar + 0 window top margin + 16 outer
        ///        + 36 Panel header + 10 inner              = 102
        /// below  15 window bottom margin + 16 outer
        ///        + 7 Panel bottom padding + 10 inner       =  48
        /// </code>
        /// Both are constants, so the panel grows one-for-one with the
        /// window and a gap at the bottom is the same gap at every size.
        /// </summary>
        public const int WindowToTabPanelTopChrome =
            WindowContentTop + TabPanelOuterPadding + PanelHeaderHeight + TabPanelInnerPadding;

        /// <summary>The bottom half of <see cref="WindowToTabPanelTopChrome"/>'s table.</summary>
        public const int WindowToTabPanelBottomChrome =
            WindowContentBottomMargin + TabPanelOuterPadding + PanelBottomPadding + TabPanelInnerPadding;

        /// <summary>Panel height a tab's content gets inside a window of this height.</summary>
        public static int TabPanelHeightFor(int windowHeight)
        {
            return Math.Max(
                0,
                windowHeight - WindowToTabPanelTopChrome - WindowToTabPanelBottomChrome);
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
