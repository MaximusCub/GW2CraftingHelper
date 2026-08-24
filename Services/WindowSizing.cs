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
        /// Narrowest window the recipe tree stays readable in. Measured for
        /// the deepest REALISTIC chain rather than the deepest chain that
        /// exists: the legendary trinkets Transcendence and Conflux, both
        /// exactly depth 14, whose widest row at every font size is the
        /// dust-promotion blow-up "429750x Pile of Glittering Dust".
        /// <para>
        /// Chain, all terms measured at Menomonia 16 against the installed
        /// XNBs (plan-redesign/minwidth.md, which reproduces the method and
        /// every anchor figure of docs/research/minimum-window-width.md):
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
