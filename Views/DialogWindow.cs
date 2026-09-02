using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views
{
    /// <summary>
    /// A <see cref="StandardWindow"/> that can be re-sized around new
    /// content, which the base class cannot: WindowBase2 derives its content
    /// region from the regions handed to its PROTECTED ConstructWindow, and
    /// Container.ContentRegion has no public setter, so a Height written from
    /// outside would leave the region where it was and walk the buttons out
    /// of it. Subclassing is the seam that reaches ConstructWindow, and
    /// re-calling it recomputes the padding, the content margin, the
    /// background ratios and the title-bar bounds together, exactly as a
    /// freshly constructed window would have them.
    /// <para>
    /// Callers speak in CONTENT box pixels; the window-relative arithmetic
    /// below is the only place that knows what Blish adds around one. The
    /// derivation of both offsets: docs/ARCHITECTURE.md, "Views: relocated
    /// design narrative".
    /// </para>
    /// </summary>
    internal class DialogWindow : StandardWindow
    {
        /// <summary>Content inset from each side of the window.</summary>
        internal const int ContentInsetX = 10;

        /// <summary>Content inset from the top of the window region.</summary>
        internal const int ContentInsetY = 35;

        /// <summary>What the window costs horizontally outside its content box.</summary>
        internal const int ChromeWidth = 2 * ContentInsetX;

        // What the window costs vertically outside its content box, measured
        // against the decompiled 1.3.0 ConstructWindow: it puts the content
        // region 64px down (the 40px title bar, plus the 35px inset less the
        // 11px floor it clamps the window's top padding to) and Size ends up
        // 40px taller than the window region it was handed. 24 + 40 + the
        // 10px kept below the content is 74.
        internal const int ChromeHeight = 74;

        private const int WindowRegionSlack = 34;

        private readonly AsyncTexture2D _background;

        internal DialogWindow(AsyncTexture2D background, int contentWidth, int contentHeight)
            : base(background, WindowRegionFor(contentWidth, contentHeight), ContentRegionFor(contentWidth, contentHeight))
        {
            _background = background;
        }

        /// <summary>
        /// Re-seats the window around a content box of this size. Call it
        /// BEFORE parenting the children that will sit in it: a child placed
        /// against the old region and then re-measured is a frame of the
        /// dialog drawn at the wrong shape.
        /// </summary>
        internal void Resize(int contentWidth, int contentHeight)
        {
            ConstructWindow(
                _background,
                WindowRegionFor(contentWidth, contentHeight),
                ContentRegionFor(contentWidth, contentHeight));
        }

        private static Rectangle WindowRegionFor(int contentWidth, int contentHeight)
        {
            return new Rectangle(
                0, 0, contentWidth + ChromeWidth, contentHeight + WindowRegionSlack);
        }

        // Height is passed at the requested value rather than the window
        // region's remainder. ConstructWindow's own Size assignment fires
        // OnResized, which recomputes the region from Size and the content
        // margin and lands 11px TALLER; a Resize to a size the window
        // already has fires nothing and leaves exactly this. Both outcomes
        // hold the content box, which passing the remainder would not.
        private static Rectangle ContentRegionFor(int contentWidth, int contentHeight)
        {
            return new Rectangle(ContentInsetX, ContentInsetY, contentWidth, contentHeight);
        }

        /// <summary>
        /// Window y of the content region's top edge. ConstructWindow lands
        /// it at the requested contentRegion.Y (ContentInsetY) plus the
        /// title bar's height, less the vertical-offset floor it clamps the
        /// window's top padding to - the same pair
        /// <see cref="ChromeHeight"/>'s arithmetic already counts, named
        /// once in Services/WindowSizing rather than re-typed here.
        /// </summary>
        internal const int ContentTopY =
            ContentInsetY + WindowSizing.TitleBarHeight - WindowSizing.TitleBarVerticalOffset;

        /// <summary>
        /// The content-relative Location a child of this window needs for
        /// its top-left to land at a window-relative point: Blish positions
        /// every child off the parent's ContentRegion origin, which this
        /// window parks at (ContentInsetX, <see cref="ContentTopY"/>).
        /// Nothing outside this class may assume that origin.
        /// </summary>
        internal Point ContentLocationFor(Point windowPoint)
        {
            return new Point(windowPoint.X - ContentInsetX, windowPoint.Y - ContentTopY);
        }
    }
}
