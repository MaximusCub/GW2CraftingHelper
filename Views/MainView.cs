using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Views
{

    public class MainView : View
    {

        private static readonly Logger Logger = Logger.GetLogger<MainView>();

        private AccountSnapshot _snapshot;
        private string _initialStatus;
        private readonly Func<Task<AccountSnapshot>> _refreshAsync;
        private readonly Action _clearCache;
        private readonly Action<string> _saveStatus;
        private readonly Action _switchToCrafting;

        // Layout constants
        private const int TabHeight = 35;
        private const int HeaderRowY = 40;
        private const int HeaderHeight = 40;
        private const int FilterRowY = 85;
        private const int FilterHeight = 40;
        private const int CoinRowY = 130;
        private const int CoinHeight = 24;
        private const int ContentY = 158;
        private const int TopRegionHeight = 160;

        // UI controls (stored for resize handler)
        private Panel _tabPanel;
        private Panel _headerPanel;
        private Panel _filterPanel;
        private FlowPanel _contentPanel;
        private Dropdown _filterDropdown;
        private Checkbox _aggregateCheckbox;
        private StandardButton _clearButton;
        private StandardButton _refreshButton;

        private Panel _coinPanel;
        private Label _statusLabel;

        public MainView(
            AccountSnapshot snapshot,
            string initialStatus,
            Func<Task<AccountSnapshot>> refreshAsync,
            Action clearCache,
            Action<string> saveStatus,
            Action switchToCrafting = null)
        {
            _snapshot = snapshot;
            _initialStatus = initialStatus;
            _refreshAsync = refreshAsync;
            _clearCache = clearCache;
            _saveStatus = saveStatus;
            _switchToCrafting = switchToCrafting;
        }

        public void SetSnapshot(AccountSnapshot snapshot)
        {
            _snapshot = snapshot;
            UpdateCoinDisplay(_snapshot?.CoinCopper ?? 0);
            RebuildContent();
        }

        public void SetStatus(string status)
        {
            _initialStatus = StatusText.Normalize(status);
            if (_statusLabel != null)
            {
                _statusLabel.Text = _initialStatus;
            }
        }

        protected override void Build(Container buildPanel)
        {
            int w = buildPanel.ContentRegion.Width;

            // Tab bar
            _tabPanel = new Panel()
            {
                Size = new Point(w, TabHeight),
                Parent = buildPanel
            };

            new StandardButton()
            {
                Text = "Snapshot",
                Size = new Point(100, 28),
                Location = new Point(0, 3),
                Enabled = false, // current tab
                Parent = _tabPanel
            };

            if (_switchToCrafting != null)
            {
                var craftingTab = new StandardButton()
                {
                    Text = "Crafting Plan",
                    Size = new Point(110, 28),
                    Location = new Point(105, 3),
                    Parent = _tabPanel
                };
                craftingTab.Click += (_, __) => _switchToCrafting();
            }

            // Header row
            _headerPanel = new Panel()
            {
                Size = new Point(w, HeaderHeight),
                Location = new Point(0, HeaderRowY),
                Parent = buildPanel
            };

            new Label()
            {
                Text = "Account Snapshot",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 8),
                Parent = _headerPanel
            };

            _statusLabel = new Label()
            {
                Text = _initialStatus ?? "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(140, 12),
                Parent = _headerPanel
            };

            _clearButton = new StandardButton()
            {
                Text = "Clear Cache",
                Size = new Point(100, 30),
                Location = new Point(w - 220, 5),
                Parent = _headerPanel,
                Enabled = _clearCache != null
            };

            _refreshButton = new StandardButton()
            {
                Text = "Refresh Now",
                Size = new Point(100, 30),
                Location = new Point(w - 110, 5),
                Parent = _headerPanel,
                Enabled = _refreshAsync != null
            };

            _clearButton.Click += (_, __) =>
            {
                _clearCache();
                SetSnapshot(null);
                var status = $"Cache Cleared \u2014 {DateTime.Now:t}";
                SetStatus(status);
                _saveStatus(status);
            };

            _refreshButton.Click += async (_, __) =>
            {
                if (_refreshAsync == null) return;

                _refreshButton.Enabled = false;
                _clearButton.Enabled = false;
                SetStatus("Refreshing...");

                try
                {
                    var snapshot = await _refreshAsync();
                    if (snapshot != null)
                    {
                        SetSnapshot(snapshot);
                        var status = $"Updated \u2014 {snapshot.CapturedAt.ToLocalTime():t}";
                        SetStatus(status);
                        _saveStatus(status);
                    }
                    else
                    {
                        SetStatus("Refresh in progress...");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Refresh Now failed");
                    var status = $"Refresh failed \u2014 {DateTime.Now:t}";
                    SetStatus(status);
                    _saveStatus(status);
                }
                finally
                {
                    _refreshButton.Enabled = true;
                    _clearButton.Enabled = true;
                }
            };

            // Filter row
            _filterPanel = new Panel()
            {
                Size = new Point(w, FilterHeight),
                Location = new Point(0, FilterRowY),
                Parent = buildPanel
            };

            _filterDropdown = new Dropdown()
            {
                Size = new Point(150, 30),
                Location = new Point(0, 5),
                Parent = _filterPanel
            };
            _filterDropdown.Items.Add("All");
            _filterDropdown.Items.Add("Items");
            _filterDropdown.Items.Add("Wallet");
            _filterDropdown.SelectedItem = "All";
            _filterDropdown.ValueChanged += (_, __) => RebuildContent();

            _aggregateCheckbox = new Checkbox()
            {
                Text = "Aggregate",
                Size = new Point(120, 25),
                Location = new Point(160, 8),
                Parent = _filterPanel
            };
            _aggregateCheckbox.CheckedChanged += (_, __) => RebuildContent();

            // Coin display
            _coinPanel = new Panel()
            {
                Size = new Point(w, CoinHeight),
                Location = new Point(0, CoinRowY),
                Parent = buildPanel
            };
            UpdateCoinDisplay(_snapshot?.CoinCopper ?? 0);

            // Scrollable content
            _contentPanel = new FlowPanel()
            {
                Size = new Point(w, buildPanel.ContentRegion.Height - TopRegionHeight),
                Location = new Point(0, ContentY),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = buildPanel
            };

            // Subscribe to resize
            buildPanel.Resized += OnPanelResized;

            RebuildContent();
        }

        private void OnPanelResized(object sender, ResizedEventArgs e)
        {
            var container = (Container)sender;
            int w = container.ContentRegion.Width;
            int h = container.ContentRegion.Height;

            _tabPanel.Size = new Point(w, TabHeight);
            _headerPanel.Size = new Point(w, HeaderHeight);
            _clearButton.Location = new Point(w - 220, 5);
            _refreshButton.Location = new Point(w - 110, 5);
            _filterPanel.Size = new Point(w, FilterHeight);
            _coinPanel.Size = new Point(w, CoinHeight);
            _contentPanel.Size = new Point(w, h - TopRegionHeight);
        }

        private void RebuildContent()
        {
            if (_contentPanel == null) return;

            foreach (var child in _contentPanel.Children.ToArray())
            {
                child.Dispose();
            }

            if (_snapshot == null)
            {
                new Label()
                {
                    Text = "No snapshot available. Click Refresh Now.",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Parent = _contentPanel
                };
                return;
            }

            string filter = _filterDropdown?.SelectedItem ?? "All";
            bool aggregate = _aggregateCheckbox?.Checked ?? false;

            if (filter == "All" || filter == "Items")
            {
                RebuildItems(aggregate);
            }

            if (filter == "All" || filter == "Wallet")
            {
                RebuildWallet();
            }
        }

        private void RebuildItems(bool aggregate)
        {
            if (_snapshot?.Items == null) return;

            IEnumerable<SnapshotItemEntry> items = aggregate
                ? SnapshotHelpers.AggregateItems(_snapshot.Items)
                : _snapshot.Items;

            foreach (var item in items)
            {
                string name = string.IsNullOrEmpty(item.Name) ? item.ItemId.ToString() : item.Name;
                string text = $"{name} x{item.Count}  ({item.Source})";
                CreateRow(item.IconUrl, text);
            }
        }

        private void RebuildWallet()
        {
            if (_snapshot?.Wallet == null) return;

            foreach (var entry in _snapshot.Wallet)
            {
                string name = string.IsNullOrEmpty(entry.CurrencyName) ? $"Currency #{entry.CurrencyId}" : entry.CurrencyName;
                string text = $"{name}: {entry.Value:N0}";
                CreateRow(entry.IconUrl, text);
            }
        }

        private void CreateRow(string iconUrl, string text)
        {
            int panelWidth = _contentPanel?.Width ?? 400;

            var row = new Panel()
            {
                Size = new Point(panelWidth, 36),
                Parent = _contentPanel
            };

            AsyncTexture2D icon;
            if (string.IsNullOrEmpty(iconUrl))
            {
                icon = new AsyncTexture2D(ContentService.Textures.Error);
            }
            else
            {
                icon = GameService.Content.GetRenderServiceTexture(iconUrl);
            }

            new Panel()
            {
                Size = new Point(32, 32),
                Location = new Point(2, 2),
                BackgroundTexture = icon,
                Parent = row
            };

            new Label()
            {
                Text = text,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(40, 6),
                Parent = row
            };
        }

        private void UpdateCoinDisplay(int copper)
        {
            if (_coinPanel == null) return;

            foreach (var child in _coinPanel.Children.ToArray())
            {
                child.Dispose();
            }

            if (copper < 0) copper = 0;

            int gold = copper / 10000;
            int silver = (copper % 10000) / 100;
            int cop = copper % 100;

            int x = 0;
            x = AddCoinSegment(_coinPanel, x, 156904, gold.ToString());
            x = AddCoinSegment(_coinPanel, x, 156907, silver.ToString());
            AddCoinSegment(_coinPanel, x, 156902, cop.ToString());
        }

        private static Color GetCoinColor(int assetId)
        {
            switch (assetId)
            {
                case 156904: return new Color(255, 204, 0);
                case 156907: return new Color(192, 192, 192);
                case 156902: return new Color(205, 127, 50);
                default:     return Color.White;
            }
        }

        private static int AddCoinSegment(Panel parent, int x, int assetId, string value)
        {
            const int iconSize = 20;
            const int gap = 2;
            const int segmentGap = 6;

            var label = new Label()
            {
                Text = value,
                TextColor = GetCoinColor(assetId),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(x, 2),
                Parent = parent
            };

            new Panel()
            {
                Size = new Point(iconSize, iconSize),
                Location = new Point(x + label.Width + gap, 2),
                BackgroundTexture = AsyncTexture2D.FromAssetId(assetId),
                Parent = parent
            };

            return x + label.Width + gap + iconSize + segmentGap;
        }
    }
}
