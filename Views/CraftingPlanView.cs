using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using GW2CraftingHelper.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Views
{
    public class CraftingPlanView : View
    {
        private static readonly Logger Logger = Logger.GetLogger<CraftingPlanView>();

        private readonly Func<int, int, CancellationToken, Task<CraftingPlanResult>> _generateAsync;
        private readonly Action _switchToSnapshot;

        private CraftingPlanResult _result;

        private FlowPanel _stepPanel;
        private Panel _summaryPanel;
        private Label _statusLabel;
        private StandardButton _generateButton;

        public CraftingPlanView(
            Func<int, int, CancellationToken, Task<CraftingPlanResult>> generateAsync,
            Action switchToSnapshot)
        {
            _generateAsync = generateAsync;
            _switchToSnapshot = switchToSnapshot;
        }

        public void SetResult(CraftingPlanResult result)
        {
            _result = result;
            RebuildSteps();
            RebuildSummary();
        }

        public void SetStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = status ?? "";
            }
        }

        protected override void Build(Container buildPanel)
        {
            int w = buildPanel.ContentRegion.Width;

            // Tab bar
            var tabPanel = new Panel()
            {
                Size = new Point(w, 35),
                Parent = buildPanel
            };

            var snapshotTab = new StandardButton()
            {
                Text = "Snapshot",
                Size = new Point(100, 28),
                Location = new Point(0, 3),
                Parent = tabPanel
            };
            snapshotTab.Click += (_, __) => _switchToSnapshot?.Invoke();

            new StandardButton()
            {
                Text = "Crafting Plan",
                Size = new Point(110, 28),
                Location = new Point(105, 3),
                Enabled = false, // current tab
                Parent = tabPanel
            };

            // Header
            var headerPanel = new Panel()
            {
                Size = new Point(w, 40),
                Location = new Point(0, 40),
                Parent = buildPanel
            };

            _statusLabel = new Label()
            {
                Text = "Ready",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 10),
                Parent = headerPanel
            };

            _generateButton = new StandardButton()
            {
                Text = "Generate Plan",
                Size = new Point(120, 30),
                Location = new Point(w - 130, 5),
                Parent = headerPanel
            };

            _generateButton.Click += async (_, __) =>
            {
                _generateButton.Enabled = false;
                SetStatus("Generating...");

                try
                {
                    // Hardcoded: Mithril Ingot (19685) — known to be in /v2/recipes
                    var result = await _generateAsync(19685, 1, CancellationToken.None);
                    SetResult(result);
                    SetStatus($"Plan generated \u2014 {DateTime.Now:t}");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Plan generation failed");
                    SetStatus($"Error: {ex.Message}");
                }
                finally
                {
                    _generateButton.Enabled = true;
                }
            };

            // Scrollable step list
            _stepPanel = new FlowPanel()
            {
                Size = new Point(w, buildPanel.ContentRegion.Height - 145),
                Location = new Point(0, 85),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = buildPanel
            };

            // Summary footer
            _summaryPanel = new Panel()
            {
                Size = new Point(w, 55),
                Location = new Point(0, buildPanel.ContentRegion.Height - 55),
                Parent = buildPanel
            };

            if (_result != null)
            {
                RebuildSteps();
                RebuildSummary();
            }
        }

        private void RebuildSteps()
        {
            if (_stepPanel == null) return;

            foreach (var child in _stepPanel.Children.ToArray())
            {
                child.Dispose();
            }

            if (_result?.Plan == null) return;

            foreach (var step in _result.Plan.Steps)
            {
                string name = GetItemName(step.ItemId);
                string prefix = GetSourcePrefix(step.Source);
                string costText = step.Source == AcquisitionSource.BuyFromTp
                    ? $" \u2014 {FormatCoin(step.TotalCost)}"
                    : "";
                string text = $"{prefix} {step.Quantity}x {name}{costText}";
                string iconUrl = GetItemIcon(step.ItemId);

                CreateStepRow(iconUrl, text);
            }
        }

        private void RebuildSummary()
        {
            if (_summaryPanel == null) return;

            foreach (var child in _summaryPanel.Children.ToArray())
            {
                child.Dispose();
            }

            if (_result?.Plan == null) return;

            int y = 0;

            // Total coin cost with icons
            var coinPanel = new Panel()
            {
                Size = new Point(_summaryPanel.Width, 24),
                Location = new Point(0, y),
                Parent = _summaryPanel
            };
            BuildCoinDisplay(coinPanel, _result.Plan.TotalCoinCost);
            y += 26;

            // Currency costs
            if (_result.Plan.CurrencyCosts != null && _result.Plan.CurrencyCosts.Count > 0)
            {
                var parts = _result.Plan.CurrencyCosts
                    .Select(c => $"{c.Amount}x Currency#{c.CurrencyId}");
                new Label()
                {
                    Text = "Currencies: " + string.Join(", ", parts),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(0, y),
                    Parent = _summaryPanel
                };
            }
        }

        private void CreateStepRow(string iconUrl, string text)
        {
            int panelWidth = _stepPanel?.Width ?? 400;

            var row = new Panel()
            {
                Size = new Point(panelWidth, 36),
                Parent = _stepPanel
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

        private static void BuildCoinDisplay(Panel parent, long copper)
        {
            if (copper < 0) copper = 0;

            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;

            int x = 0;
            var totalLabel = new Label()
            {
                Text = "Total: ",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 2),
                Parent = parent
            };
            x = totalLabel.Width;

            x = AddCoinSegment(parent, x, 156904, gold.ToString());
            x = AddCoinSegment(parent, x, 156907, silver.ToString());
            AddCoinSegment(parent, x, 156902, cop.ToString());
        }

        private static Color GetCoinColor(int assetId)
        {
            switch (assetId)
            {
                case 156904: return new Color(255, 204, 0);
                case 156907: return new Color(192, 192, 192);
                case 156902: return new Color(205, 127, 50);
                default: return Color.White;
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

        private string GetItemName(int itemId)
        {
            if (_result?.ItemMetadata != null &&
                _result.ItemMetadata.TryGetValue(itemId, out var meta) &&
                !string.IsNullOrEmpty(meta.Name))
            {
                return meta.Name;
            }
            return $"Item #{itemId}";
        }

        private string GetItemIcon(int itemId)
        {
            if (_result?.ItemMetadata != null &&
                _result.ItemMetadata.TryGetValue(itemId, out var meta))
            {
                return meta.IconUrl;
            }
            return null;
        }

        private static string GetSourcePrefix(AcquisitionSource source)
        {
            switch (source)
            {
                case AcquisitionSource.BuyFromTp: return "Buy";
                case AcquisitionSource.Craft: return "Craft";
                case AcquisitionSource.UnknownSource: return "???";
                default: return "???";
            }
        }

        private static string FormatCoin(long copper)
        {
            if (copper < 0) copper = 0;
            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;
            return $"{gold}g {silver}s {cop}c";
        }
    }
}
