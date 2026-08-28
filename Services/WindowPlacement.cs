namespace TaimisToolbench.Services
{
    /// <summary>
    /// Where the module window may sit on the game client it is being shown
    /// on. Split out of Views/ResizableTabbedWindow on the same terms as
    /// <see cref="WindowSizing"/> and <see cref="PanelChromeMath"/>: the
    /// arithmetic is the part that has to be pinned, and a Blish control
    /// cannot be constructed in a Blish-free test.
    /// <para>
    /// The guarantee is REACHABILITY, not tidiness: the user must be able to
    /// reach the title bar (the window's full top edge) to move the window
    /// and the resize grip (its bottom-right corner) to resize it. Those two
    /// sit at opposite ends of both axes, so on an axis the window fits on,
    /// guaranteeing both is the same thing as putting the whole of that axis
    /// on screen. On an axis it does NOT fit on - a game client narrower
    /// than <see cref="WindowSizing.NarrowScreenFloorWidth"/>, which is a
    /// supported state, not a broken one - the two cannot both be met and
    /// the leading edge wins: the tab strip and the content begin there, and
    /// a title bar that is visible can always be dragged to bring the grip
    /// into view, whereas a grip cannot be dragged to bring back a title bar.
    /// </para>
    /// <para>
    /// Blish clamps a restored position itself, but only the top-left
    /// CORNER: WindowBase2.Show reads the persisted point and then applies
    /// Clamp(x, 0, SpriteScreen.Width - 64) per axis (BlishHUD 1.3.0,
    /// decompiled). Nothing on that path consults the window's size, so a
    /// position saved against a wide client leaves an arbitrary amount of
    /// the window's right-hand side - cost column, Generate button, resize
    /// grip - past the edge of a narrower one, with no way to drag it back.
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
    }
}
