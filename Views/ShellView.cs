using System;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using GW2CraftingHelper.Models;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    public class ShellView : View
    {
        private const int TabPanelWidth = 120;
        private const int TabButtonHeight = 32;
        private const int TabButtonGap = 2;
        private const int IconAreaHeight = 52;

        private readonly MainView _snapshotContent;
        private readonly CraftingPlanView _craftingContent;
        private readonly AsyncTexture2D _moduleIcon;

        private Panel _tabPanel;
        private Panel _contentArea;
        private Panel[] _tabContainers;
        private Panel[] _tabButtons;
        private bool[] _tabBuilt;
        private int _activeTabIndex = -1;

        private LogTabContent _logContent;
        private bool _built;

        public ShellView(
            MainView snapshotContent,
            CraftingPlanView craftingContent,
            AsyncTexture2D moduleIcon)
        {
            _snapshotContent = snapshotContent;
            _craftingContent = craftingContent;
            _moduleIcon = moduleIcon;
        }

        public void SetSnapshot(AccountSnapshot snapshot)
        {
            _snapshotContent?.SetSnapshot(snapshot);
        }

        public void SetStatus(string status)
        {
            _snapshotContent?.SetStatus(status);
        }

        protected override void Build(Container buildPanel)
        {
            if (_built) return;
            _built = true;

            int w = buildPanel.ContentRegion.Width;
            int h = buildPanel.ContentRegion.Height;

            var tabs = TabRegistry.Tabs;
            int tabCount = tabs.Count;

            // Left tab panel (dark background)
            _tabPanel = new Panel()
            {
                Size = new Point(TabPanelWidth, h),
                Location = new Point(0, 0),
                BackgroundColor = new Color(20, 20, 20, 180),
                Parent = buildPanel
            };

            // Module icon centered at top
            if (_moduleIcon != null)
            {
                new Panel()
                {
                    Size = new Point(40, 40),
                    Location = new Point((TabPanelWidth - 40) / 2, 6),
                    BackgroundTexture = _moduleIcon,
                    Parent = _tabPanel
                };
            }

            // Tab buttons
            _tabButtons = new Panel[tabCount];
            for (int i = 0; i < tabCount; i++)
            {
                int tabIndex = i;
                int btnY = IconAreaHeight + i * (TabButtonHeight + TabButtonGap);

                var btn = new Panel()
                {
                    Size = new Point(TabPanelWidth - 4, TabButtonHeight),
                    Location = new Point(2, btnY),
                    BackgroundColor = new Color(40, 40, 40, 150),
                    Parent = _tabPanel
                };

                string label = tabs[i].IsPlaceholder
                    ? tabs[i].Name + " *"
                    : tabs[i].Name;

                new Label()
                {
                    Text = label,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(10, 7),
                    Parent = btn
                };

                btn.Click += (_, __) => SwitchToTab(tabIndex);
                _tabButtons[i] = btn;
            }

            // Right content area
            int contentW = Math.Max(0, w - TabPanelWidth);
            _contentArea = new Panel()
            {
                Size = new Point(contentW, h),
                Location = new Point(TabPanelWidth, 0),
                Parent = buildPanel
            };

            // One container panel per tab (all hidden initially)
            _tabContainers = new Panel[tabCount];
            _tabBuilt = new bool[tabCount];
            for (int i = 0; i < tabCount; i++)
            {
                _tabContainers[i] = new Panel()
                {
                    Size = new Point(contentW, h),
                    Location = new Point(0, 0),
                    Visible = false,
                    Parent = _contentArea
                };
            }

            buildPanel.Resized += OnShellResized;

            // Default to Snapshot tab
            SwitchToTab(TabRegistry.TabSnapshot);
        }

        private void SwitchToTab(int index)
        {
            if (_activeTabIndex == index) return;
            if (index < 0 || index >= _tabContainers.Length) return;

            // Hide previous tab
            if (_activeTabIndex >= 0 && _activeTabIndex < _tabContainers.Length)
            {
                _tabContainers[_activeTabIndex].Visible = false;
            }

            _activeTabIndex = index;

            // Lazy-build tab content on first switch
            if (!_tabBuilt[index])
            {
                BuildTabContent(index, _tabContainers[index]);
                _tabBuilt[index] = true;
            }

            // Refresh log tab every time it becomes active
            if (index == TabRegistry.TabLog && _logContent != null)
            {
                _logContent.Refresh();
            }

            _tabContainers[index].Visible = true;
            UpdateTabHighlights();
        }

        private void BuildTabContent(int index, Panel container)
        {
            switch (index)
            {
                case TabRegistry.TabSnapshot:
                    _snapshotContent.Build(container);
                    break;

                case TabRegistry.TabCraftingPlan:
                    _craftingContent.Build(container);
                    break;

                case TabRegistry.TabLog:
                    _logContent = new LogTabContent(() => _craftingContent.LastDebugLog);
                    _logContent.Build(container);
                    break;

                default:
                    new Label()
                    {
                        Text = TabRegistry.Tabs[index].Name + " - Coming Soon",
                        AutoSizeWidth = true,
                        AutoSizeHeight = true,
                        Location = new Point(20, 20),
                        TextColor = new Color(150, 150, 150),
                        Parent = container
                    };
                    break;
            }
        }

        private void UpdateTabHighlights()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                _tabButtons[i].BackgroundColor = i == _activeTabIndex
                    ? new Color(60, 60, 60, 220)
                    : new Color(40, 40, 40, 150);
            }
        }

        private void OnShellResized(object sender, ResizedEventArgs e)
        {
            var container = (Container)sender;
            int w = container.ContentRegion.Width;
            int h = container.ContentRegion.Height;
            int contentW = Math.Max(0, w - TabPanelWidth);

            _tabPanel.Size = new Point(TabPanelWidth, h);
            _contentArea.Size = new Point(contentW, h);
            _contentArea.Location = new Point(TabPanelWidth, 0);

            for (int i = 0; i < _tabContainers.Length; i++)
            {
                _tabContainers[i].Size = new Point(contentW, h);
            }
        }
    }
}
