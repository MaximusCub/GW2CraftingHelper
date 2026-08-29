using System;
using System.Linq;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Microsoft.Xna.Framework;
using TaimisToolbench.Services;
using TaimisToolbench.Views.Rendering;

namespace TaimisToolbench.Views
{
    /// <summary>
    /// Bridges plain Build(Container) classes to the IView interface
    /// required by TabbedWindow2.Tab. Wraps any Action&lt;Container&gt;
    /// as a View so existing MainView, CraftingPlanView, etc. work
    /// with TabbedWindow2 without conversion.
    ///
    /// Renders a bordered Panel (Panel.ShowBorder) inset from the tab
    /// content region by OUTER_PADDING, wearing the module's own tab title
    /// band along the top of its content region and an inner content panel
    /// beneath it so view content does not press against the border chrome.
    ///
    /// The band is the module's rather than Blish's Panel.Title header
    /// because the title is the tab's ONLY name - the tab strip draws icons
    /// only - and Blish's header is fixed at 36px and DefaultFont16 by
    /// literals inside private layout methods. See
    /// Views/Rendering/HeaderBands.
    /// </summary>
    internal class ViewAdapter : View
    {
        // Both named once in Services/WindowSizing.cs, which derives the
        // window-to-panel chrome from them; OUTER_PADDING is there because
        // it matches Blish's WindowBase2.STANDARD_MARGIN (internal const).
        private const int OUTER_PADDING = WindowSizing.TabPanelOuterPadding;

        // Inner padding between border chrome and view content
        private const int INNER_PADDING = WindowSizing.TabPanelInnerPadding;

        // Above the hosted view's content panel, which keeps Blish's
        // default 0 - see the assignment in Build for why.
        private const int ContentChromeZIndex = 1;

        private readonly Action<Container> _buildAction;
        private readonly Action<Container> _decorateBand;
        private readonly string _title;

        // The edge insets Blish will give the bordered panel, computed once
        // from Blish's own public Panel constants. Held rather than read back
        // off the panel because Panel.ContentRegion is only refreshed by
        // RecalculateLayout, which Control.UpdateLayout SKIPS while the
        // panel's parent is layout-suspended - see PanelChromeMath, which
        // owns the arithmetic and the failure it prevents.
        private readonly PanelChromeMath.Insets _borderedInsets;

        // The window container Build subscribed on, and the handler it
        // added - held so Unload can detach them. buildPanel is the
        // module-lifetime window itself (WindowBase2.ShowView passes the
        // window to DoBuild), so a handler left behind outlives this view:
        // one closure per tab visit accumulates on the window's Resized
        // invocation list forever, and each closure pins the whole
        // detached view tree it captured (docs/ARCHITECTURE.md's
        // "a tab switch detaches, it does not dispose").
        private Container _resizedOwner;
        private EventHandler<ResizedEventArgs> _resizedHandler;

        /// <param name="decorateBand">
        /// Optional: fills a strip of the title band that shares the content
        /// panel's x-span, so a tab whose actions belong beside its title
        /// can right-anchor them on the SAME edge its content uses. Runs
        /// before <paramref name="buildAction"/>.
        /// </param>
        public ViewAdapter(
            string title, Action<Container> buildAction, Action<Container> decorateBand = null)
        {
            _title = title ?? "";
            _buildAction = buildAction ?? throw new ArgumentNullException(nameof(buildAction));
            _decorateBand = decorateBand;

            // hasTitle: false unconditionally - the module never sets
            // Panel.Title, so Blish reserves only the border's top padding
            // and the band below sits inside the content region.
            _borderedInsets = PanelChromeMath.PanelInsets(
                showBorder: true,
                hasTitle: false,
                headerHeight: Panel.HEADER_HEIGHT,
                topPadding: Panel.TOP_PADDING,
                rightPadding: Panel.RIGHT_PADDING,
                bottomPadding: Panel.BOTTOM_PADDING,
                leftPadding: Panel.LEFT_PADDING);
        }

        protected override void Build(Container buildPanel)
        {
            // Defensive: a rebuilt view must not leave its previous
            // handler behind (same reasoning as Unload).
            if (_resizedOwner != null && _resizedHandler != null)
            {
                _resizedOwner.Resized -= _resizedHandler;
            }

            // Defensive: clear any existing children before rebuilding.
            foreach (var child in buildPanel.Children.ToArray())
            {
                child.Dispose();
            }

            // Bordered inner panel, matching BlishHUD Settings-style
            // visual language (Panel.ShowBorder uses assets
            // 1032325/1002144/605025 for border chrome).
            var borderedSize = BorderedSize(buildPanel);
            var borderedPanel = new Panel()
            {
                Parent = buildPanel,
                Location = new Point(OUTER_PADDING, OUTER_PADDING),
                Size = borderedSize,
                ShowBorder = true,
            };

            // Top of the bordered panel's content region, spanning exactly
            // the x-range Blish's own title header would have used.
            var titleBand = HeaderBands.CreateTabTitleBand(
                borderedPanel, BandWidth(borderedSize), _title, INNER_PADDING + UiSpacing.Inset);

            // Painted after the view's content panel, not before it. A
            // scrolling panel inside that view paints a few pixels above
            // its own top edge - see CraftingPlanView.TopStripZIndex for
            // the clip arithmetic and where it is transcribed from - and
            // the band is the one opaque surface in the chain, so it is
            // also the one that can be relied on to cover a leak that
            // reaches this far up. Costs nothing: the band and the content
            // panel do not overlap, so no hit test changes.
            titleBand.ZIndex = ContentChromeZIndex;

            // The decorator's strip starts where the content panel starts
            // and is as wide, so a caller right-anchoring against its width
            // lands on the same vertical line the content does.
            Panel bandActions = null;
            if (_decorateBand != null)
            {
                bandActions = new Panel()
                {
                    Parent = titleBand,
                    Location = new Point(INNER_PADDING, 0),
                    Size = new Point(ContentWidth(borderedSize), HeaderBands.TabTitleHeight),
                };
            }

            // Inner content panel with additional padding so view content
            // does not sit flush against the band above it or the border
            // chrome around it. The bordered panel's own internal padding
            // (4px L/R, 7px T/B with no title set) is not enough visual
            // breathing room.
            // Deliberately NOT scrollable: every hosted view provides its
            // own CanScroll panel. Nesting two CanScroll panels parents an
            // invisible outer Scrollbar over the same strip as the visible
            // inner one, and it swallows every click-drag on the thumb.
            var contentPanel = new Panel()
            {
                Parent = borderedPanel,
                Location = new Point(INNER_PADDING, HeaderBands.TabTitleHeight + INNER_PADDING),
                Size = ContentSize(borderedSize),
            };

            _resizedHandler = (s, e) =>
            {
                var bordered = BorderedSize(buildPanel);
                borderedPanel.Size = bordered;
                titleBand.Size = new Point(BandWidth(bordered), HeaderBands.TabTitleHeight);

                // Derived from the size just assigned, never read back off
                // borderedPanel.ContentRegion: a resize the window performs
                // from inside its own layout pass reaches here with that
                // region still describing the PREVIOUS size, and nothing
                // re-reads it afterwards. See PanelChromeMath.
                var content = ContentSize(bordered);
                if (bandActions != null)
                {
                    bandActions.Size = new Point(content.X, HeaderBands.TabTitleHeight);
                }

                contentPanel.Size = content;
            };
            _resizedOwner = buildPanel;
            buildPanel.Resized += _resizedHandler;

            if (bandActions != null)
            {
                _decorateBand(bandActions);
            }

            _buildAction(contentPanel);
        }

        /// <summary>
        /// The bordered panel's size, inset from the window's content region
        /// by <see cref="OUTER_PADDING"/>. The WINDOW's ContentRegion is the
        /// one region in this chain that is safe to read at any time:
        /// WindowBase2.OnResized assigns it directly from the new size,
        /// synchronously, before it raises Resized - it is not derived by a
        /// layout pass, so it cannot lag one behind the way a Panel's does.
        /// </summary>
        private static Point BorderedSize(Container buildPanel)
        {
            return new Point(
                Math.Max(0, buildPanel.ContentRegion.Width - (2 * OUTER_PADDING)),
                Math.Max(0, buildPanel.ContentRegion.Height - (2 * OUTER_PADDING)));
        }

        /// <summary>
        /// The title band's width: the bordered panel's full content-region
        /// width, which is the span Blish's own header occupied.
        /// </summary>
        private int BandWidth(Point borderedSize)
        {
            return PanelChromeMath.ContentWidth(borderedSize.X, _borderedInsets);
        }

        /// <summary>
        /// The inner content panel's size for a bordered panel of
        /// <paramref name="borderedSize"/>: that panel's content region, less
        /// <see cref="INNER_PADDING"/> on all four edges and less the title
        /// band that now sits above it. Floored at 0 - Control.Size ignores a
        /// negative component, which would strand the panel at a stale size.
        /// </summary>
        private Point ContentSize(Point borderedSize)
        {
            int height = PanelChromeMath.PaddedContentHeight(
                borderedSize.Y, _borderedInsets, INNER_PADDING) - HeaderBands.TabTitleHeight;

            return new Point(ContentWidth(borderedSize), height > 0 ? height : 0);
        }

        private int ContentWidth(Point borderedSize)
        {
            return PanelChromeMath.PaddedContentWidth(
                borderedSize.X, _borderedInsets, INNER_PADDING);
        }

        /// <summary>
        /// Detaches Build's Resized handler from the window.
        /// WindowBase2.ClearView calls DoUnload on every tab switch, so
        /// this runs exactly when the view tree is detached. Only the
        /// subscription is removed here - the detached controls are left
        /// to the GC rather than disposed, because the module-lifetime tab
        /// content singletons keep field references into the outgoing tree
        /// and their marshaled background tails may still touch it (see
        /// docs/ARCHITECTURE.md on post-switch render tails); an explicit
        /// dispose would turn that documented wasted-work case into
        /// use-after-dispose.
        /// </summary>
        protected override void Unload()
        {
            if (_resizedOwner != null && _resizedHandler != null)
            {
                _resizedOwner.Resized -= _resizedHandler;
            }

            _resizedOwner = null;
            _resizedHandler = null;
        }
    }
}
