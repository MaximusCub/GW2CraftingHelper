using System;
using System.Linq;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// Bridges plain Build(Container) classes to the IView interface
    /// required by TabbedWindow2.Tab. Wraps any Action&lt;Container&gt;
    /// as a View so existing MainView, CraftingPlanView, etc. work
    /// with TabbedWindow2 without conversion.
    ///
    /// Renders a bordered Panel with a title header matching the
    /// BlishHUD native style (Panel.ShowBorder), inset from the
    /// tab content region by OUTER_PADDING. An inner content panel
    /// provides additional left/right/bottom padding so view content
    /// does not press against the border chrome.
    /// </summary>
    internal class ViewAdapter : View
    {
        // Match WindowBase2.STANDARD_MARGIN (internal const = 16)
        private const int OUTER_PADDING = 16;

        // Inner padding between border chrome and view content
        private const int INNER_PADDING = 10;

        private readonly Action<Container> _buildAction;
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

        public ViewAdapter(string title, Action<Container> buildAction)
        {
            _title = title ?? "";
            _buildAction = buildAction ?? throw new ArgumentNullException(nameof(buildAction));
            _borderedInsets = PanelChromeMath.PanelInsets(
                showBorder: true,
                hasTitle: !string.IsNullOrEmpty(_title),
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

            // Bordered inner panel with title header, matching BlishHUD
            // Settings-style visual language (Panel.ShowBorder uses
            // assets 1032325/1002144/605025 for border chrome).
            var borderedPanel = new Panel()
            {
                Parent = buildPanel,
                Location = new Point(OUTER_PADDING, OUTER_PADDING),
                Size = BorderedSize(buildPanel),
                ShowBorder = true,
                Title = _title,
            };

            // Inner content panel with additional padding so view content
            // does not sit flush against the border chrome. The bordered
            // panel's own internal padding (4px L/R, 7px T/B, 36px header)
            // is not enough visual breathing room.
            // Deliberately NOT scrollable: every hosted view provides its
            // own CanScroll panel. Nesting two CanScroll panels parents an
            // invisible outer Scrollbar over the same strip as the visible
            // inner one, and it swallows every click-drag on the thumb.
            var contentPanel = new Panel()
            {
                Parent = borderedPanel,
                Location = new Point(INNER_PADDING, INNER_PADDING),
                Size = ContentSize(borderedPanel.Size),
            };

            _resizedHandler = (s, e) =>
            {
                var bordered = BorderedSize(buildPanel);
                borderedPanel.Size = bordered;

                // Derived from the size just assigned, never read back off
                // borderedPanel.ContentRegion: a resize the window performs
                // from inside its own layout pass reaches here with that
                // region still describing the PREVIOUS size, and nothing
                // re-reads it afterwards. See PanelChromeMath.
                contentPanel.Size = ContentSize(bordered);
            };
            _resizedOwner = buildPanel;
            buildPanel.Resized += _resizedHandler;

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
        /// The inner content panel's size for a bordered panel of
        /// <paramref name="borderedSize"/>: that panel's content region, less
        /// <see cref="INNER_PADDING"/> on all four edges.
        /// </summary>
        private Point ContentSize(Point borderedSize)
        {
            return new Point(
                PanelChromeMath.PaddedContentWidth(borderedSize.X, _borderedInsets, INNER_PADDING),
                PanelChromeMath.PaddedContentHeight(borderedSize.Y, _borderedInsets, INNER_PADDING));
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
