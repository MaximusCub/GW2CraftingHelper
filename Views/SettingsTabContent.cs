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

        // M37 (KNOWN-ISSUES #24): one row per Homestead Refinement material
        // family. MaterialItemId is internal-only bookkeeping (never
        // displayed - see MaterialLabel) used solely to route the parsed
        // tier back to the right ModuleSettings entry.
        private class HomesteadTierRow
        {
            public int MaterialItemId;
            public string MaterialLabel;
            public TextBox Input;
            public Label ErrorLabel;
        }

        private readonly ModuleSettings _settings;
        private readonly List<CurrencyRow> _rows = new List<CurrencyRow>();
        private readonly List<HomesteadTierRow> _homesteadRows = new List<HomesteadTierRow>();

        private FlowPanel _rootPanel;
        private Label _statusLabel;
        private Label _homesteadStatusLabel;
        private Checkbox _valueOwnMaterialsCheckbox;

        // M39 (log system, d2-log-system.md Section 5 / tab-roadmap-proposal
        // Section 2.1): the ONE "Diagnostics" checkbox + the two log-file
        // policy rows (max size / retention). No separate
        // ScrollDiagnosticsEnabled checkbox is surfaced here - see
        // ModuleSettings' own doc comment on that setting's backward-compat
        // read.
        private Checkbox _logDiagnosticsCheckbox;
        private TextBox _logMaxSizeInput;
        private Label _logMaxSizeErrorLabel;
        private TextBox _logRetentionDaysInput;
        private Label _logRetentionDaysErrorLabel;
        private Label _logStatusLabel;

        public SettingsTabContent(ModuleSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Build(Container container)
        {
            _rows.Clear();

            // Pre-existing gap found during M39 review (this same file's
            // reused-instance-per-reopen shape is exactly why _rows.Clear()
            // above already exists): Module.cs's Settings tab reuses this
            // SAME SettingsTabContent instance across every tab re-open
            // (unlike the Log tab's "new instance per open" factory), so
            // without clearing here, re-opening Settings more than once
            // per session would accumulate stale HomesteadTierRow entries
            // pointing at controls the previous Build() cycle's container
            // already disposed - LoadCurrentHomesteadTiers/
            // SaveHomesteadTiers would then read/write through them
            // alongside the current cycle's real rows.
            _homesteadRows.Clear();

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
            BuildHomesteadRefinementSection(panelWidth);
            BuildLoggingSection(panelWidth);

            LoadCurrentValuations();
            LoadCurrentHomesteadTiers();
            LoadCurrentLoggingSettings();
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
            AddValueOwnMaterialsRow(panelWidth);
        }

        /// <summary>
        /// M34-B2a #3: the "Value own materials" checkbox previously had no
        /// in-window affordance at all (ModuleSettings.ValueOwnMaterials
        /// was only reachable via Blish HUD's own generic settings panel,
        /// or by hand-editing the persisted JSON). Applies immediately -
        /// no Save button, matching a plain Blish SettingEntry&lt;bool&gt;
        /// (unlike the currency valuation rows above, which need text
        /// parsing/validation before they can be persisted).
        /// </summary>
        private void AddValueOwnMaterialsRow(int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };

            _valueOwnMaterialsCheckbox = new Checkbox()
            {
                Text = "Value own materials",
                Checked = _settings.ValueOwnMaterials.Value,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
            };
            _valueOwnMaterialsCheckbox.CheckedChanged += (_, e) =>
            {
                _settings.ValueOwnMaterials.Value = e.Checked;
            };

            new Label()
            {
                Text = "Force-buy where cheaper than crafting fresh; value owned materials at sell price instead of free",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(NameColumnX + 170, 7),
                Parent = rowPanel
            };
        }

        /// <summary>
        /// M37 (KNOWN-ISSUES #24, gw2e parity): three per-material efficiency
        /// tier rows (Fiber/Metal/Wood), each an integer 0/1/2 entered as
        /// text and validated on Save - same TextBox+Save shape as the
        /// Currency Valuations section above (a plain Checkbox's immediate-
        /// apply pattern doesn't fit a 3-valued integer, and no Dropdown/
        /// stepper control is otherwise used in this codebase's Views).
        /// Labels name the material family only - no raw item/vendor ids
        /// are ever displayed (repo invariant).
        /// </summary>
        private void BuildHomesteadRefinementSection(int panelWidth)
        {
            AddSectionHeader("Homestead Refinement", panelWidth);
            AddInfoLine("Efficiency upgrades owned per material (0 = none, 1 = one upgrade, 2 = both).", panelWidth);
            AddInfoLine("Raises how much Refined Homestead material each trade produces.", panelWidth);

            AddHomesteadTierRow(Gw2Constants.RefinedHomesteadFiberItemId, "Fiber (Farm)", panelWidth);
            AddHomesteadTierRow(Gw2Constants.RefinedHomesteadMetalItemId, "Metal (Metal Forge)", panelWidth);
            AddHomesteadTierRow(Gw2Constants.RefinedHomesteadWoodItemId, "Wood (Lumber Mill)", panelWidth);

            AddHomesteadSaveRow(panelWidth);
        }

        private void AddHomesteadTierRow(int materialItemId, string materialLabel, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };

            new Label()
            {
                Text = materialLabel,
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
                Text = "tier (0-2)",
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

            _homesteadRows.Add(new HomesteadTierRow
            {
                MaterialItemId = materialItemId,
                MaterialLabel = materialLabel,
                Input = input,
                ErrorLabel = errorLabel
            });
        }

        private void AddHomesteadSaveRow(int panelWidth)
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
            saveButton.Click += (_, __) => SaveHomesteadTiers();

            _homesteadStatusLabel = new Label()
            {
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(NameColumnX + 80 + 12, 12),
                Parent = rowPanel
            };
        }

        private void LoadCurrentHomesteadTiers()
        {
            var tiers = _settings.GetHomesteadEfficiencyTiers();

            foreach (var row in _homesteadRows)
            {
                row.Input.Text = tiers.GetTier(row.MaterialItemId).ToString(CultureInfo.InvariantCulture);
                row.ErrorLabel.Text = "";
            }
        }

        private void SaveHomesteadTiers()
        {
            int invalidCount = 0;
            var parsedTiers = new Dictionary<int, int>();

            foreach (var row in _homesteadRows)
            {
                row.ErrorLabel.Text = "";

                if (SettingsInputParser.TryParseTier(row.Input.Text, out int tier))
                {
                    parsedTiers[row.MaterialItemId] = tier;
                }
                else
                {
                    // Left out of this save entirely - whatever was
                    // previously persisted for this material is preserved,
                    // matching the currency valuation Save button's
                    // "invalid rows are not saved" contract.
                    row.ErrorLabel.Text = "Must be 0, 1, or 2";
                    invalidCount++;
                }
            }

            if (parsedTiers.TryGetValue(Gw2Constants.RefinedHomesteadFiberItemId, out int fiberTier))
            {
                _settings.HomesteadFiberTier.Value = fiberTier;
            }
            if (parsedTiers.TryGetValue(Gw2Constants.RefinedHomesteadMetalItemId, out int metalTier))
            {
                _settings.HomesteadMetalTier.Value = metalTier;
            }
            if (parsedTiers.TryGetValue(Gw2Constants.RefinedHomesteadWoodItemId, out int woodTier))
            {
                _settings.HomesteadWoodTier.Value = woodTier;
            }

            if (_homesteadStatusLabel == null) return;

            if (invalidCount == 0)
            {
                _homesteadStatusLabel.Text = $"Saved - {DateTime.Now:t}";
                _homesteadStatusLabel.TextColor = SuccessTextColor;
            }
            else
            {
                string entryWord = invalidCount == 1 ? "entry" : "entries";
                _homesteadStatusLabel.Text = $"Saved - {invalidCount} invalid {entryWord} not saved";
                _homesteadStatusLabel.TextColor = WarningTextColor;
            }
        }

        /// <summary>
        /// M39 (log system): one "Diagnostics" checkbox (idiom (a),
        /// immediate-apply - matches ValueOwnMaterials above) plus two
        /// TextBox+Save rows (idiom (b) - matches the Homestead section
        /// above) for the log file's size cap and retention window. This is
        /// the ONE diagnostics toggle for the whole module per the
        /// tab-roadmap-proposal synthesis (Section 2.1) - no separate
        /// ScrollDiagnosticsEnabled checkbox is added alongside it.
        /// </summary>
        private void BuildLoggingSection(int panelWidth)
        {
            AddSectionHeader("Logging", panelWidth);
            AddInfoLine("Controls the module's own log file (data/module_log.jsonl), separate from Blish HUD's own log.", panelWidth);
            AddInfoLine("The Log tab always shows the current session regardless of these settings.", panelWidth);

            AddLogDiagnosticsRow(panelWidth);
            AddLogMaxSizeRow(panelWidth);
            AddLogRetentionDaysRow(panelWidth);
            AddLogSaveRow(panelWidth);
        }

        private void AddLogDiagnosticsRow(int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };

            _logDiagnosticsCheckbox = new Checkbox()
            {
                Text = "Diagnostics logging",
                Checked = _settings.LogDiagnosticsEnabled.Value,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
            };
            _logDiagnosticsCheckbox.CheckedChanged += (_, e) =>
            {
                _settings.LogDiagnosticsEnabled.Value = e.Checked;
                ModuleLog.Shared.DiagnosticsEnabled = e.Checked;
            };

            new Label()
            {
                Text = "Log fine-grained diagnostic events (including scroll machinery) to the Log tab and file",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(NameColumnX + 170, 7),
                Parent = rowPanel
            };
        }

        private void AddLogMaxSizeRow(int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };

            new Label()
            {
                Text = "Log max size",
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = NameColumnWidth,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
            };

            _logMaxSizeInput = new TextBox()
            {
                Size = new Point(InputWidth, 26),
                Location = new Point(NameColumnX + NameColumnWidth, 3),
                Parent = rowPanel
            };

            new Label()
            {
                Text = "MB (1-1000)",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(HintX, 7),
                Parent = rowPanel
            };

            _logMaxSizeErrorLabel = new Label()
            {
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = ErrorTextColor,
                Location = new Point(ErrorX, 7),
                Parent = rowPanel
            };
        }

        private void AddLogRetentionDaysRow(int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };

            new Label()
            {
                Text = "Log retention",
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = NameColumnWidth,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
            };

            _logRetentionDaysInput = new TextBox()
            {
                Size = new Point(InputWidth, 26),
                Location = new Point(NameColumnX + NameColumnWidth, 3),
                Parent = rowPanel
            };

            new Label()
            {
                Text = "days (1-365)",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(HintX, 7),
                Parent = rowPanel
            };

            _logRetentionDaysErrorLabel = new Label()
            {
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = ErrorTextColor,
                Location = new Point(ErrorX, 7),
                Parent = rowPanel
            };
        }

        private void AddLogSaveRow(int panelWidth)
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
            saveButton.Click += (_, __) => SaveLoggingSettings();

            _logStatusLabel = new Label()
            {
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(NameColumnX + 80 + 12, 12),
                Parent = rowPanel
            };
        }

        private void LoadCurrentLoggingSettings()
        {
            if (_logMaxSizeInput != null)
            {
                long mb = _settings.LogMaxSizeBytes.Value / (1024 * 1024);
                _logMaxSizeInput.Text = mb.ToString(CultureInfo.InvariantCulture);
            }
            if (_logMaxSizeErrorLabel != null)
            {
                _logMaxSizeErrorLabel.Text = "";
            }

            if (_logRetentionDaysInput != null)
            {
                _logRetentionDaysInput.Text = _settings.LogRetentionDays.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (_logRetentionDaysErrorLabel != null)
            {
                _logRetentionDaysErrorLabel.Text = "";
            }
        }

        private void SaveLoggingSettings()
        {
            int invalidCount = 0;

            if (_logMaxSizeErrorLabel != null)
            {
                _logMaxSizeErrorLabel.Text = "";
            }
            if (SettingsInputParser.TryParseLogMaxSizeMb(_logMaxSizeInput?.Text, out long maxSizeBytes))
            {
                _settings.LogMaxSizeBytes.Value = (int)maxSizeBytes;

                // Pushed live immediately (not just persisted) - the
                // running ModuleLog instance otherwise would not pick up a
                // smaller/larger cap until the next module reload. Mirrors
                // DiagnosticsEnabled's own immediate-apply behavior above,
                // even though this row uses the TextBox+Save idiom rather
                // than a plain checkbox. Routed through the same clamp as
                // Module.cs's own Configure call (redundant here in
                // practice, since TryParseLogMaxSizeMb already rejected
                // anything outside 1-1000 MB above, but keeps every live
                // consumer of this setting going through one clamped
                // accessor rather than two separately-trusted paths).
                ModuleLog.Shared.MaxFileSizeBytes = _settings.GetClampedLogMaxSizeBytes();
            }
            else if (_logMaxSizeErrorLabel != null)
            {
                _logMaxSizeErrorLabel.Text = "Must be 1-1000";
                invalidCount++;
            }

            if (_logRetentionDaysErrorLabel != null)
            {
                _logRetentionDaysErrorLabel.Text = "";
            }
            if (SettingsInputParser.TryParseRetentionDays(_logRetentionDaysInput?.Text, out int retentionDays))
            {
                // Retention is only enforced once per session at
                // Module.Initialize (age-based pruning does not need
                // per-write cost - d2-log-system.md Section 4.2), so a
                // saved value here intentionally takes effect next session,
                // not immediately - no live push needed, unlike the size
                // cap above.
                _settings.LogRetentionDays.Value = retentionDays;
            }
            else if (_logRetentionDaysErrorLabel != null)
            {
                _logRetentionDaysErrorLabel.Text = "Must be 1-365";
                invalidCount++;
            }

            if (_logStatusLabel == null) return;

            if (invalidCount == 0)
            {
                _logStatusLabel.Text = $"Saved - {DateTime.Now:t}";
                _logStatusLabel.TextColor = SuccessTextColor;
            }
            else
            {
                string entryWord = invalidCount == 1 ? "entry" : "entries";
                _logStatusLabel.Text = $"Saved - {invalidCount} invalid {entryWord} not saved";
                _logStatusLabel.TextColor = WarningTextColor;
            }
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
            // Seeded from the currently-persisted valuation (not empty) so
            // an invalid row is left untouched below rather than silently
            // dropped: the status label tells the user invalid entries are
            // "not saved", which must mean unchanged, not cleared. Only a
            // row the user deliberately blanks is removed.
            var entries = new Dictionary<int, long>();
            foreach (var kvp in _settings.GetCurrencyValuation().CopperPerUnit)
            {
                entries[kvp.Key] = kvp.Value;
            }

            int invalidCount = 0;

            foreach (var row in _rows)
            {
                row.ErrorLabel.Text = "";

                string text = row.Input.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    // Blank box = unset; the currency is simply excluded
                    // from the saved valuation, not an error.
                    entries.Remove(row.CurrencyId);
                    continue;
                }

                if (SettingsInputParser.TryParseCopperValue(text, out long copperPerUnit))
                {
                    entries[row.CurrencyId] = copperPerUnit;
                }
                else
                {
                    // Left out of this row's changes entirely - whatever
                    // was previously persisted for this currency (if
                    // anything) is preserved, matching "not saved" below.
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
                // Defensive: entries is seeded from the already-valid
                // persisted valuation and only ever updated with
                // SettingsInputParser-validated positive values on
                // non-coin currency ids, so CurrencyValuation's
                // constructor should never actually reject it. Still
                // guarded so a future change to either side degrades to a
                // visible status message instead of an unhandled
                // exception on the UI thread.
                Logger.Warn(ex, "Failed to save currency valuations");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "settings", $"Failed to save currency valuations: {ex.GetType().Name} - {ex.Message}");
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
