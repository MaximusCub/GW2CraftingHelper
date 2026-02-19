using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GW2CraftingHelper.Views
{
    public class LogTabContent
    {
        private FlowPanel _contentPanel;
        private readonly Func<IReadOnlyList<string>> _getLogLines;

        public LogTabContent(Func<IReadOnlyList<string>> getLogLines)
        {
            _getLogLines = getLogLines;
        }

        public void Build(Container container)
        {
            _contentPanel = new FlowPanel()
            {
                Size = new Point(container.ContentRegion.Width, container.ContentRegion.Height),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = container
            };

            container.Resized += (_, __) =>
            {
                _contentPanel.Size = new Point(
                    container.ContentRegion.Width,
                    container.ContentRegion.Height);
            };

            Refresh();
        }

        public void Refresh()
        {
            if (_contentPanel == null) return;

            foreach (var child in _contentPanel.Children.ToArray())
            {
                child.Dispose();
            }

            var lines = _getLogLines?.Invoke();
            if (lines == null || lines.Count == 0)
            {
                new Label()
                {
                    Text = "No log data. Generate a crafting plan first.",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(8, 8),
                    Parent = _contentPanel
                };
                return;
            }

            foreach (var line in lines)
            {
                new Label()
                {
                    Text = line ?? "",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Parent = _contentPanel
                };
            }
        }
    }
}
