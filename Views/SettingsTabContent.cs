using System;
using System.Collections.Generic;
using System.Globalization;
using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// Settings tab content: lets the user set the coin value of
    /// non-coin currencies (see Models/CurrencyValuation.cs) used to
    /// compare vendor offers, persisted through ModuleSettings. Plan-level
    /// defaults (price basis, own materials) remain on the Crafting Plan
    /// tab - only informational text about them is shown here.
    /// </summary>
    public class SettingsTabContent
    {
        // Curated list of common plan currencies: Karma, Laurels, Spirit
        // Shards, and the Rift Essence tiers. The coin currency itself
        // (Gw2Constants.CoinCurrencyId) is never listed here - it is
        // already directly comparable and CurrencyValuation rejects
        // coin-keyed entries outright.
        private static readonly int[] CuratedCurrencyIds =
        {
            2,  // Karma
            3,  // Laurels
            23, // Spirit Shards
            78, // Fine Rift Essence
            79, // Rare Rift Essence
            80  // Masterwork Rift Essence
        };

        private static readonly Color InfoTextColor = new Color(170, 170, 170);
        private static readonly Color ErrorTextColor = new Color(255, 100, 100);
        private static readonly Color SuccessTextColor = new Color(150, 200, 150);
        private static readonly Color WarningTextColor = new Color(255, 200, 60);

        private const int RightEdgePadding = 20;
        private const int RowHeight = 30;
        private const int InfoRowHeight = 20;
        private const int NameColumnX = 16;
        private const int NameColumnWidth = 220;
        private const int InputWidth = 80;
        private const int HintX = NameColumnX + NameColumnWidth + InputWidth + 8;
        private const int ErrorX = HintX + 130;

        private static readonly Logger Logger = Logger.GetLogger<SettingsTabContent>();

        private class CurrencyRow
        {
            public int CurrencyId;
            public TextBox Input;
            public Label ErrorLabel;
        }

        private readonly ModuleSettings _settings;
        private readonly List<CurrencyRow> _rows = new List<CurrencyRow>();

        private FlowPanel _rootPanel;
        private Label _statusLabel;

        public SettingsTabContent(ModuleSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Build(Container container)
        {
            _rows.Clear();

            int panelWidth = container.ContentRegion.Width - RightEdgePadding;

            _rootPanel = new FlowPanel()
            {
                Size = new Point(container.ContentRegion.Width, container.ContentRegion.Height),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = container
            };

            container.Resized += (_, __) =>
            {
                _rootPanel.Size = new Point(
                    container.ContentRegion.Width,
                    container.ContentRegion.Height);
            };

            BuildCurrencyValuationsSection(panelWidth);
            BuildPlanDefaultsSection(panelWidth);

            LoadCurrentValuations();
        }

        private void BuildCurrencyValuationsSection(int panelWidth)
        {
            AddSectionHeader("Currency Valuations", panelWidth);
            AddInfoLine("Coin value per unit of each currency, used to compare vendor offers.", panelWidth);
            AddInfoLine("Leave a currency unset to keep it out of price comparisons.", panelWidth);

            foreach (int currencyId in CuratedCurrencyIds)
            {
                AddCurrencyRow(currencyId, panelWidth);
            }

            AddSaveRow(panelWidth);
        }

        private void BuildPlanDefaultsSection(int panelWidth)
        {
            AddSectionHeader("Plan Defaults", panelWidth);
            AddInfoLine("Price basis (Instant Buy / Buy Orders) is chosen per plan in the Crafting Plan tab.", panelWidth);
            AddInfoLine("The \"Use Own Materials\" default is also set per plan in the Crafting Plan tab.", panelWidth);
        }

        private void AddSectionHeader(string title, int panelWidth)
        {
            var headerPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };

            new Label()
            {
                Text = title,
                Font = GameService.Content.DefaultFont18,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(NameColumnX, 4),
                Parent = headerPanel
            };
        }

        private void AddInfoLine(string text, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, InfoRowHeight),
                Parent = _rootPanel
            };

            new Label()
            {
                Text = text,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(NameColumnX, 2),
                Parent = rowPanel
            };
        }

        private void AddCurrencyRow(int currencyId, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };

            new Label()
            {
                Text = Gw2Constants.ResolveCurrencyName(currencyId),
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = NameColumnWidth,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
            };

            var input = new TextBox()
            {
                Size = new Point(InputWidth, 26),
                Location = new Point(NameColumnX + NameColumnWidth, 3),
                Parent = rowPanel
            };

            new Label()
            {
                Text = "copper per unit",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(HintX, 7),
                Parent = rowPanel
            };

            var errorLabel = new Label()
            {
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = ErrorTextColor,
                Location = new Point(ErrorX, 7),
                Parent = rowPanel
            };

            _rows.Add(new CurrencyRow
            {
                CurrencyId = currencyId,
                Input = input,
                ErrorLabel = errorLabel
            });
        }

        private void AddSaveRow(int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, 40),
                Parent = _rootPanel
            };

            var saveButton = new StandardButton()
            {
                Text = "Save",
                Size = new Point(80, 28),
                Location = new Point(NameColumnX, 6),
                Parent = rowPanel
            };
            saveButton.Click += (_, __) => SaveValuations();

            _statusLabel = new Label()
            {
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(NameColumnX + 80 + 12, 12),
                Parent = rowPanel
            };
        }

        private void LoadCurrentValuations()
        {
            var valuation = _settings.GetCurrencyValuation();

            foreach (var row in _rows)
            {
                row.Input.Text = valuation.TryGetCopperValue(row.CurrencyId, out long copperPerUnit)
                    ? copperPerUnit.ToString(CultureInfo.InvariantCulture)
                    : "";
                row.ErrorLabel.Text = "";
            }
        }

        private void SaveValuations()
        {
            var entries = new Dictionary<int, long>();
            int invalidCount = 0;

            foreach (var row in _rows)
            {
                row.ErrorLabel.Text = "";

                string text = row.Input.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    // Blank box = unset; the currency is simply excluded
                    // from the saved valuation, not an error.
                    continue;
                }

                if (SettingsInputParser.TryParseCopperValue(text, out long copperPerUnit))
                {
                    entries[row.CurrencyId] = copperPerUnit;
                }
                else
                {
                    row.ErrorLabel.Text = "Must be a positive whole number";
                    invalidCount++;
                }
            }

            try
            {
                _settings.SetCurrencyValuation(new CurrencyValuation(entries));
            }
            catch (Exception ex)
            {
                // Defensive: entries is built exclusively from
                // SettingsInputParser-validated positive values on
                // non-coin currency ids, so CurrencyValuation's
                // constructor should never actually reject it. Still
                // guarded so a future change to either side degrades to a
                // visible status message instead of an unhandled
                // exception on the UI thread.
                Logger.Warn(ex, "Failed to save currency valuations");
                if (_statusLabel != null)
                {
                    _statusLabel.Text = "Save failed - see log";
                    _statusLabel.TextColor = ErrorTextColor;
                }
                return;
            }

            if (_statusLabel == null) return;

            if (invalidCount == 0)
            {
                _statusLabel.Text = $"Saved - {DateTime.Now:t}";
                _statusLabel.TextColor = SuccessTextColor;
            }
            else
            {
                string entryWord = invalidCount == 1 ? "entry" : "entries";
                _statusLabel.Text = $"Saved - {invalidCount} invalid {entryWord} not saved";
                _statusLabel.TextColor = WarningTextColor;
            }
        }
    }
}
