namespace TaimisToolbench.Services
{
    /// <summary>
    /// Where the module window may sit on the game client it is being shown on
    /// (Blish-free arithmetic, on the same terms as <see cref="WindowSizing"/>).
    /// <para>
    /// The guarantee is REACHABILITY, not tidiness: the user must be able to
    /// reach the title bar to move the window and the resize grip to resize it.
    /// On an axis the window fits on, that is the same thing as putting the
    /// whole of that axis on screen. On an axis it does NOT fit on - a client
    /// narrower than <see cref="WindowSizing.NarrowScreenFloorWidth"/>, which is
    /// a supported state, not a broken one - the two cannot both be met and the
    /// leading edge wins: a visible title bar can always be dragged to bring the
    /// grip into view, whereas a grip cannot be dragged to bring back a title
    /// bar.
    /// </para>
    /// <para>
    /// Blish clamps a restored position itself, but only the top-left CORNER
    /// and never against the window's size (BlishHUD 1.3.0, decompiled), which
    /// is what <see cref="ClampExtent"/> is for. See docs/ARCHITECTURE.md, S2.9.
    /// </para>
    /// </summary>
    internal static class WindowPlacement
    {
        /// <summary>
        /// The position one axis of a window may take, given the window's
        /// own extent on that axis and the screen's.
        /// <para>
        /// A screen extent of 0 or less means "not known yet" - the sprite
        /// screen has not settled to the real client size - and leaves the
        /// position exactly as it was, matching
        /// <see cref="WindowSizing.EffectiveMinWindowWidth"/>'s treatment of
        /// the same case.
        /// </para>
        /// </summary>
        public static int ClampAxis(int position, int windowExtent, int screenExtent)
        {
            if (screenExtent <= 0)
            {
                return position;
            }

            if (windowExtent >= screenExtent)
            {
                return 0;
            }

            if (position < 0)
            {
                return 0;
            }

            int max = screenExtent - windowExtent;
            return position > max ? max : position;
        }

        /// <summary>
        /// The extent one axis of a window may take, given the minimum that axis
        /// enforces and the screen's own extent - the ceiling to
        /// <see cref="WindowSizing.EffectiveMinWindowWidth"/>'s floor.
        ///
        /// Blish restores a persisted size verbatim - WindowBase2.Show writes it
        /// from settings with no clamp of any kind (BlishHUD 1.3.0, decompiled)
        /// - so a size dragged out on a 3440-wide client comes back whole on a
        /// 1080-wide one, and the position clamp cannot rescue it: the grip is
        /// off the trailing edge at every position the title bar is reachable
        /// from, and the grip is the only way to shrink the window.
        ///
        /// Where the two converge the FLOOR wins, and it has to: below
        /// <see cref="WindowSizing.NarrowScreenFloorWidth"/> the effective
        /// minimum is already wider than the screen and the window's own layout
        /// pass re-applies it, so a ceiling that undercut it would be grown
        /// straight back and the window would oscillate. A screen extent of 0 or
        /// less (not settled yet) applies the floor and no ceiling.
        /// </summary>
        public static int ClampExtent(int windowExtent, int minExtent, int screenExtent)
        {
            int fitted = screenExtent > 0 && windowExtent > screenExtent
                ? screenExtent
                : windowExtent;

            return fitted < minExtent ? minExtent : fitted;
        }
    }
}
