using System;
using System.Linq;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
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
    public class ViewAdapter : View
    {
        // Match WindowBase2.STANDARD_MARGIN (internal const = 16)
        private const int OUTER_PADDING = 16;

        // Inner padding between border chrome and view content
        private const int INNER_PADDING = 10;

        private readonly Action<Container> _buildAction;
        private readonly string _title;

        public ViewAdapter(string title, Action<Container> buildAction)
        {
            _title = title ?? "";
            _buildAction = buildAction ?? throw new ArgumentNullException(nameof(buildAction));
        }

        protected override void Build(Container buildPanel)
        {
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
                Size = new Point(
                    buildPanel.ContentRegion.Width - 2 * OUTER_PADDING,
                    buildPanel.ContentRegion.Height - 2 * OUTER_PADDING),
                ShowBorder = true,
                Title = _title
            };

            // Inner content panel with additional padding so view content
            // does not sit flush against the border chrome. The bordered
            // panel's own internal padding (4px L/R, 7px T/B, 36px header)
            // is not enough visual breathing room.
            var contentPanel = new Panel()
            {
                Parent = borderedPanel,
                Location = new Point(INNER_PADDING, INNER_PADDING),
                Size = new Point(
                    borderedPanel.ContentRegion.Width - 2 * INNER_PADDING,
                    borderedPanel.ContentRegion.Height - 2 * INNER_PADDING),
                CanScroll = true
            };

            buildPanel.Resized += (s, e) =>
            {
                borderedPanel.Size = new Point(
                    buildPanel.ContentRegion.Width - 2 * OUTER_PADDING,
                    buildPanel.ContentRegion.Height - 2 * OUTER_PADDING);
                contentPanel.Size = new Point(
                    borderedPanel.ContentRegion.Width - 2 * INNER_PADDING,
                    borderedPanel.ContentRegion.Height - 2 * INNER_PADDING);
            };

            _buildAction(contentPanel);
        }
    }
}
