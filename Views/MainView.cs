using Blish_HUD;
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

        private AccountSnapshot _snapshot;
        private readonly Func<Task> _refreshAsync;

        private FlowPanel _contentPanel;
        private Dropdown _filterDropdown;
        private Checkbox _aggregateCheckbox;

        private Label _coinLabel;

        public MainView(AccountSnapshot snapshot, Func<Task> refreshAsync)
        {
            _snapshot = snapshot;
            _refreshAsync = refreshAsync;
        }

        public void SetSnapshot(AccountSnapshot snapshot)
        {
            _snapshot = snapshot;

            if (_coinLabel != null)
            {
                _coinLabel.Text = FormatCoin(_snapshot?.CoinCopper ?? 0);
            }

            RebuildContent();
        }

        protected override void Build(Container buildPanel)
        {
            // Header row
            var headerPanel = new Panel()
            {
                Size = new Point(buildPanel.ContentRegion.Width, 40),
                Parent = buildPanel
            };

            new Label()
            {
                Text = "Account Snapshot",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 8),
                Parent = headerPanel
            };

            var statusLabel = new Label()
            {
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(140, 12),
                Parent = headerPanel
            };

            var refreshButton = new StandardButton()
            {
                Text = "Refresh Now",
                Size = new Point(100, 30),
                Location = new Point(buildPanel.ContentRegion.Width - 110, 5),
                Parent = headerPanel,
                Enabled = _refreshAsync != null
            };

            refreshButton.Click += async (_, __) => {
                if (_refreshAsync == null) return;

                refreshButton.Enabled = false;
                statusLabel.Text = "Refreshing...";

                try
                {
                    await _refreshAsync();
                    statusLabel.Text = $"Updated {DateTime.Now:t}";
                }
                catch (Exception ex)
                {
                    // This writes to the Blish HUD log
                    Blish_HUD.Logger.GetLogger<MainView>().Warn(ex, "Refresh Now failed");
                    statusLabel.Text = "Refresh failed (see log)";
                }
                finally
                {
                    refreshButton.Enabled = true;
                }
            };

            // Filter row
            var filterPanel = new Panel()
            {
                Size = new Point(buildPanel.ContentRegion.Width, 40),
                Location = new Point(0, 45),
                Parent = buildPanel
            };

            _filterDropdown = new Dropdown()
            {
                Size = new Point(150, 30),
                Location = new Point(0, 5),
                Parent = filterPanel
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
                Parent = filterPanel
            };
            _aggregateCheckbox.CheckedChanged += (_, __) => RebuildContent();

            // Coin display
            _coinLabel = new Label()
            {
                Text = FormatCoin(_snapshot?.CoinCopper ?? 0),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 90),
                Parent = buildPanel
            };

            // Scrollable content
            _contentPanel = new FlowPanel()
            {
                Size = new Point(buildPanel.ContentRegion.Width, buildPanel.ContentRegion.Height - 115),
                Location = new Point(0, 115),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = buildPanel
            };

            RebuildContent();
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

            IEnumerable<SnapshotItemEntry> items = _snapshot.Items;

            if (aggregate)
            {
                items = _snapshot.Items
                    .GroupBy(i => i.ItemId)
                    .Select(g => new SnapshotItemEntry
                    {
                        ItemId = g.Key,
                        Name = g.First().Name,
                        Count = g.Sum(i => i.Count),
                        Source = "Total"
                    });
            }

            foreach (var item in items)
            {
                string name = string.IsNullOrEmpty(item.Name) ? item.ItemId.ToString() : item.Name;
                string text = $"{name} x{item.Count}  ({item.Source})";

                new Label()
                {
                    Text = text,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Parent = _contentPanel
                };
            }
        }

        private void RebuildWallet()
        {
            if (_snapshot?.Wallet == null) return;

            foreach (var entry in _snapshot.Wallet)
            {
                string name = string.IsNullOrEmpty(entry.CurrencyName) ? $"Currency #{entry.CurrencyId}" : entry.CurrencyName;
                string text = $"{name}: {entry.Value}";

                new Label()
                {
                    Text = text,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Parent = _contentPanel
                };
            }
        }

        internal static string FormatCoin(int copper)
        {
            return SnapshotHelpers.FormatCoin(copper);
        }
    }
}
