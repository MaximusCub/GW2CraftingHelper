using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Views
{

    /// <summary>
    /// The Snapshot tab: a search-as-you-type account-inventory browser
    /// (M39 snapshot search, d1-snapshot-about-settings.md Feature 1) over
    /// the existing AccountItemIndex/GetPrioritizedSources seams. A plain
    /// TextBox (not the Crafting Plan tab's SuggestionPanel/
    /// AutocompleteTextBox - that machinery is shaped for picking exactly
    /// one plan-target item, not general item-name search) drives a
    /// synchronous, in-memory substring scan over data that is already
    /// fully loaded, with no cancellation/marshal ceremony needed.
    /// </summary>
    public class MainView
    {

        private static readonly Logger Logger = Logger.GetLogger<MainView>();

        private static readonly Color InfoTextColor = new Color(170, 170, 170);
        private static readonly Color WarningTextColor = new Color(255, 200, 60);

        // Mirrors Module.cs's own StaleThreshold constant. M39 scope does
        // not add d1's proposed shared SnapshotRefreshIntervalMinutes
        // setting (Feature 3 - out of scope for this milestone); this
        // local constant keeps the staleness label's own threshold
        // reasonable in the meantime without inventing a second setting.
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(10);

        private AccountSnapshot _snapshot;
        private AccountItemIndex _accountItemIndex;
        private string _initialStatus;
        private readonly Func<Task<AccountSnapshot>> _refreshAsync;
        private readonly Action _clearCache;
        private readonly Action<string> _saveStatus;
        private readonly Action<string> _saveStatusThreadSafe;

        // Session-sticky search/filter state (d1-snapshot-about-settings.md
        // Feature 1's "Tab views are rebuilt from scratch" cross-cutting
        // finding: Build() tears down and recreates every control on each
        // tab visit, so anything that should feel "sticky" across tab
        // switches must live in these instance fields, not the controls
        // themselves, and be read back in when Build() reruns). All four
        // source toggles default to true (show everything), matching the
        // tab's pre-search implicit no-filter behavior. The pre-existing
        // content-type dropdown deliberately keeps its own prior (reset-
        // to-default) behavior - only the NEW controls added by this
        // feature get this treatment.
        private string _lastSearchText = "";
        private bool _bankEnabled = true;
        private bool _materialStorageEnabled = true;
        private bool _sharedInventoryEnabled = true;
        private bool _charactersEnabled = true;

        // Layout constants
        private const int HeaderRowY = 5;
        private const int HeaderHeight = 40;
        private const int SearchRowY = 50;
        private const int SearchRowHeight = 35;
        private const int SourceFilterRowY = 88;
        private const int SourceFilterHeight = 30;
        private const int CoinRowY = 122;
        private const int CoinHeight = 24;
        private const int ContentY = 150;
        private const int TopRegionHeight = 150;

        private const int SearchBoxWidth = 300;
        private const int FilterDropdownWidth = 140;
        private const int FilterDropdownX = SearchBoxWidth + 10;

        private const int ItemRowHeight = 52;
        private const int WalletRowHeight = 36;

        // UI controls (stored for resize handler)
        private Panel _headerPanel;
        private Panel _filterPanel;
        private Panel _sourceFilterPanel;
        private FlowPanel _contentPanel;
        private TextBox _searchBox;
        private Dropdown _filterDropdown;
        private Checkbox _bankCheckbox;
        private Checkbox _materialStorageCheckbox;
        private Checkbox _sharedInventoryCheckbox;
        private Checkbox _charactersCheckbox;
        private StandardButton _clearButton;
        private StandardButton _refreshButton;

        private Panel _coinPanel;
        private Label _statusLabel;
        private Color _defaultStatusColor;

        public MainView(
            AccountSnapshot snapshot,
            string initialStatus,
            Func<Task<AccountSnapshot>> refreshAsync,
            Action clearCache,
            Action<string> saveStatus,
            Action<string> saveStatusThreadSafe)
        {
            _snapshot = snapshot;
            // The constructor sets _snapshot directly, bypassing
            // SetSnapshot - the index needs its own build call here too,
            // not just inside SetSnapshot (d1 Feature 1's explicit
            // call-out). AccountItemIndex's constructor already tolerates
            // a null items list.
            _accountItemIndex = new AccountItemIndex(_snapshot?.Items);
            _initialStatus = initialStatus;
            _refreshAsync = refreshAsync;
            _clearCache = clearCache;
            _saveStatus = saveStatus;
            _saveStatusThreadSafe = saveStatusThreadSafe;
        }

        public void SetSnapshot(AccountSnapshot snapshot)
        {
            _snapshot = snapshot;
            _accountItemIndex = new AccountItemIndex(_snapshot?.Items);
            UpdateCoinDisplay(_snapshot?.CoinCopper ?? 0);
            ApplyStatusDisplay();
            RebuildContent();
        }

        public void SetStatus(string status)
        {
            _initialStatus = StatusText.Normalize(status);
            ApplyStatusDisplay();
        }

        public void Build(Container buildPanel)
        {
            int w = buildPanel.ContentRegion.Width;

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
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(140, 12),
                Parent = _headerPanel
            };
            // Capture Blish's own real default rather than guessing/
            // hardcoding one, so the non-stale case is byte-identical to
            // today's unset-TextColor appearance once ApplyStatusDisplay
            // below starts writing to this property.
            _defaultStatusColor = _statusLabel.TextColor;

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
                    string status = snapshot != null
                        ? $"Updated \u2014 {snapshot.CapturedAt.ToLocalTime():t}"
                        : null;

                    // Persist BEFORE marshaling, while still on this
                    // continuation thread - StatusStore.Save is blocking
                    // file I/O and is safe to run off the UI thread.
                    // _saveStatusThreadSafe (Module.SaveStatusThreadSafe)
                    // persists via the dirty-flag path rather than
                    // _saveStatus (Module.SaveStatus), which also calls
                    // _snapshotContent.SetStatus directly - a control
                    // mutation that would itself be unsafe to run off-thread
                    // here.
                    if (status != null)
                    {
                        _saveStatusThreadSafe(status);
                    }

                    // Blish HUD's XNA host has no SynchronizationContext, so
                    // this continuation may resume on a ThreadPool thread;
                    // marshal ONLY the remaining control mutations back to
                    // the main thread.
                    MainThreadMarshal.Run(() =>
                    {
                        // The view may have been torn down (tab switched
                        // away, module disabled) while the refresh was in
                        // flight - a disposed control's Parent is nulled on
                        // disposal, mirroring CraftingPlanView's
                        // ResizeDebounceStep check. Persistence above
                        // already happened regardless, so bailing here
                        // cannot strand any state.
                        if (_headerPanel == null || _headerPanel.Parent == null) return;

                        if (snapshot != null)
                        {
                            SetSnapshot(snapshot);
                            SetStatus(status);
                        }
                        else
                        {
                            SetStatus("Refresh in progress...");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Refresh Now failed");
                    ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot", $"Refresh Now failed: {ex.GetType().Name} - {ex.Message}");
                    var status = $"Refresh failed \u2014 {DateTime.Now:t}";
                    _saveStatusThreadSafe(status);
                    MainThreadMarshal.Run(() =>
                    {
                        if (_headerPanel == null || _headerPanel.Parent == null) return;
                        SetStatus(status);
                    });
                }
                finally
                {
                    // Runs later on the main thread once queued - both
                    // buttons still re-enable on every path (success,
                    // exception, or cancellation) since finally always
                    // executes and Run always queues.
                    MainThreadMarshal.Run(() =>
                    {
                        if (_headerPanel == null || _headerPanel.Parent == null) return;
                        _refreshButton.Enabled = true;
                        _clearButton.Enabled = true;
                    });
                }
            };

            // Search row: plain TextBox (not SuggestionPanel/
            // AutocompleteTextBox - see class doc comment) + the existing
            // content-type dropdown alongside it.
            _filterPanel = new Panel()
            {
                Size = new Point(w, SearchRowHeight),
                Location = new Point(0, SearchRowY),
                Parent = buildPanel
            };

            _searchBox = new TextBox()
            {
                Size = new Point(SearchBoxWidth, 26),
                Location = new Point(0, 5),
                PlaceholderText = "Search items and currencies...",
                Text = _lastSearchText ?? "",
                Parent = _filterPanel
            };
            _searchBox.TextChanged += (_, __) =>
            {
                _lastSearchText = _searchBox.Text ?? "";
                RebuildContent();
            };

            _filterDropdown = new Dropdown()
            {
                Size = new Point(FilterDropdownWidth, 30),
                Location = new Point(FilterDropdownX, 5),
                Parent = _filterPanel
            };
            _filterDropdown.Items.Add("All");
            _filterDropdown.Items.Add("Items");
            _filterDropdown.Items.Add("Wallet");
            _filterDropdown.SelectedItem = "All";
            _filterDropdown.ValueChanged += (_, __) => RebuildContent();

            // Source-filter row: one checkbox per storage location, all
            // checked by default. Only meaningful when the content-type
            // dropdown includes Items (All/Items) - left visible-but-inert
            // when Wallet is selected rather than adding show/hide logic
            // that itself needs testing (d1 Feature 1's deliberate
            // simplicity choice).
            _sourceFilterPanel = new Panel()
            {
                Size = new Point(w, SourceFilterHeight),
                Location = new Point(0, SourceFilterRowY),
                Parent = buildPanel
            };

            _bankCheckbox = new Checkbox()
            {
                Text = "Bank",
                Checked = _bankEnabled,
                Size = new Point(70, 25),
                Location = new Point(0, 3),
                Parent = _sourceFilterPanel
            };
            _bankCheckbox.CheckedChanged += (_, __) =>
            {
                _bankEnabled = _bankCheckbox.Checked;
                RebuildContent();
            };

            _materialStorageCheckbox = new Checkbox()
            {
                Text = "Material Storage",
                Checked = _materialStorageEnabled,
                Size = new Point(170, 25),
                Location = new Point(80, 3),
                Parent = _sourceFilterPanel
            };
            _materialStorageCheckbox.CheckedChanged += (_, __) =>
            {
                _materialStorageEnabled = _materialStorageCheckbox.Checked;
                RebuildContent();
            };

            _sharedInventoryCheckbox = new Checkbox()
            {
                Text = "Shared Inventory",
                Checked = _sharedInventoryEnabled,
                Size = new Point(170, 25),
                Location = new Point(260, 3),
                Parent = _sourceFilterPanel
            };
            _sharedInventoryCheckbox.CheckedChanged += (_, __) =>
            {
                _sharedInventoryEnabled = _sharedInventoryCheckbox.Checked;
                RebuildContent();
            };

            _charactersCheckbox = new Checkbox()
            {
                Text = "Characters",
                Checked = _charactersEnabled,
                Size = new Point(110, 25),
                Location = new Point(440, 3),
                Parent = _sourceFilterPanel
            };
            _charactersCheckbox.CheckedChanged += (_, __) =>
            {
                _charactersEnabled = _charactersCheckbox.Checked;
                RebuildContent();
            };

            // Coin display (unchanged - WP-21/22 will repoint this to the
            // shared CoinCurrencyRenderer once it lands; out of scope here).
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

            ApplyStatusDisplay();
            RebuildContent();
        }

        private void OnPanelResized(object sender, ResizedEventArgs e)
        {
            var container = (Container)sender;
            int w = container.ContentRegion.Width;
            int h = container.ContentRegion.Height;

            _headerPanel.Size = new Point(w, HeaderHeight);
            _clearButton.Location = new Point(w - 220, 5);
            _refreshButton.Location = new Point(w - 110, 5);
            _filterPanel.Size = new Point(w, SearchRowHeight);
            _sourceFilterPanel.Size = new Point(w, SourceFilterHeight);
            _coinPanel.Size = new Point(w, CoinHeight);
            _contentPanel.Size = new Point(w, h - TopRegionHeight);
        }

        /// <summary>
        /// Composes the header status label's text (base status text plus
        /// a staleness-age suffix, e.g. "Updated - 3:41 PM (2m ago)") and
        /// recolors it once the snapshot is older than
        /// <see cref="StaleThreshold"/>. Called from every place the
        /// status text or the snapshot itself changes (Build's initial
        /// render, SetSnapshot, SetStatus) so the two can never drift out
        /// of sync with each other.
        /// </summary>
        private void ApplyStatusDisplay()
        {
            if (_statusLabel == null) return;

            string text = _initialStatus ?? "";

            if (_snapshot != null)
            {
                TimeSpan age = DateTime.UtcNow - _snapshot.CapturedAt;
                string ageText = StatusText.ForSnapshotAge(age);
                text = string.IsNullOrEmpty(text) ? ageText : $"{text} ({ageText})";
                _statusLabel.TextColor = age >= StaleThreshold ? WarningTextColor : _defaultStatusColor;
            }
            else
            {
                _statusLabel.TextColor = _defaultStatusColor;
            }

            _statusLabel.Text = text;
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
                    Location = new Point(8, 8),
                    Parent = _contentPanel
                };
                return;
            }

            string filter = _filterDropdown?.SelectedItem ?? "All";
            string searchText = _searchBox?.Text ?? "";

            List<SnapshotSearchRow> itemRows = null;
            List<SnapshotWalletEntry> walletRows = null;

            if (filter == "All" || filter == "Items")
            {
                var sourceFilter = new SnapshotSourceFilter
                {
                    Bank = _bankCheckbox?.Checked ?? true,
                    MaterialStorage = _materialStorageCheckbox?.Checked ?? true,
                    SharedInventory = _sharedInventoryCheckbox?.Checked ?? true,
                    Characters = _charactersCheckbox?.Checked ?? true
                };

                itemRows = SnapshotSearchResultBuilder.BuildItemRows(
                    _snapshot.Items, _accountItemIndex, searchText, sourceFilter, GetActiveCharacterName());
            }

            if (filter == "All" || filter == "Wallet")
            {
                walletRows = SnapshotSearchResultBuilder.FilterWallet(_snapshot.Wallet, searchText);
            }

            bool anyItemRows = itemRows != null && itemRows.Count > 0;
            bool anyWalletRows = walletRows != null && walletRows.Count > 0;

            if (!anyItemRows && !anyWalletRows)
            {
                // A snapshot exists but the current search text + source
                // filters match nothing - distinct from the "no snapshot
                // at all" empty state above (d1 Feature 1's explicit
                // call-out: today's code silently renders a blank list
                // here instead).
                string trimmedSearch = (searchText ?? "").Trim();
                string message = trimmedSearch.Length == 0
                    ? "No items match the selected sources."
                    : $"No items match \"{trimmedSearch}\" in the selected sources.";

                new Label()
                {
                    Text = message,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(8, 8),
                    TextColor = InfoTextColor,
                    Parent = _contentPanel
                };
                return;
            }

            if (itemRows != null)
            {
                foreach (var row in itemRows)
                {
                    CreateItemRow(row);
                }
            }

            if (walletRows != null)
            {
                foreach (var entry in walletRows)
                {
                    CreateWalletRow(entry);
                }
            }
        }

        /// <summary>
        /// Best-effort active-character lookup, used only to bias
        /// GetPrioritizedSources' breakdown ordering (purely cosmetic -
        /// never affects which sources are included or the total, and
        /// GetPrioritizedSources already treats a null/absent name as "no
        /// active character" - see AccountItemIndexTests'
        /// GetPrioritizedSources_NullActiveChar_SkipsCharPriority). Mirrors
        /// Module.cs's own Gw2Mumble try/catch shape (used for the same
        /// purpose in the Crafting Plan tab's account-bound recipe checks),
        /// but deliberately does NOT also write to ModuleLog on failure the
        /// way that call site does: this method runs on every RebuildContent
        /// call, i.e. every keystroke in the search box, not once per
        /// explicit user click - logging "Mumble unavailable" at that
        /// frequency would turn an expected, common condition (Blish
        /// running without Mumble linked) into per-keystroke ring-buffer
        /// (and, if diagnostics are enabled, file) noise, which is exactly
        /// what ModuleLog's own Debug-level convention exists to avoid, not
        /// produce. A silent fallback to null is safe here precisely
        /// because the caller's own contract treats it as cosmetic.
        /// </summary>
        private static string GetActiveCharacterName()
        {
            try
            {
                var mumble = GameService.Gw2Mumble;
                if (mumble?.PlayerCharacter != null && !string.IsNullOrEmpty(mumble.PlayerCharacter.Name))
                {
                    return mumble.PlayerCharacter.Name;
                }
            }
            catch (Exception)
            {
                // Cosmetic-only lookup on a keystroke-frequency path - see
                // the method doc comment for why this is intentionally
                // silent rather than paired with a ModuleLog write.
            }

            return null;
        }

        private void CreateItemRow(SnapshotSearchRow row)
        {
            int panelWidth = _contentPanel?.Width ?? 400;

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, ItemRowHeight),
                Parent = _contentPanel
            };

            AsyncTexture2D icon;
            if (string.IsNullOrEmpty(row.IconUrl))
            {
                icon = new AsyncTexture2D(ContentService.Textures.Error);
            }
            else
            {
                icon = GameService.Content.GetRenderServiceTexture(row.IconUrl);
            }

            new Panel()
            {
                Size = new Point(32, 32),
                Location = new Point(2, 2),
                BackgroundTexture = icon,
                Parent = rowPanel
            };

            // Never display raw item IDs (repo invariant) - row.Name is
            // already the resolved display name.
            new Label()
            {
                Text = $"{row.Name} x{row.TotalCount}",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(40, 4),
                Parent = rowPanel
            };

            string breakdown = row.Breakdown == null || row.Breakdown.Count == 0
                ? ""
                : string.Join("   ", row.Breakdown.Select(b => $"{b.Label} {b.Count}"));

            new Label()
            {
                Text = breakdown,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(40, 24),
                Parent = rowPanel
            };
        }

        private void CreateWalletRow(SnapshotWalletEntry entry)
        {
            int panelWidth = _contentPanel?.Width ?? 400;

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, WalletRowHeight),
                Parent = _contentPanel
            };

            AsyncTexture2D icon;
            if (string.IsNullOrEmpty(entry.IconUrl))
            {
                icon = new AsyncTexture2D(ContentService.Textures.Error);
            }
            else
            {
                icon = GameService.Content.GetRenderServiceTexture(entry.IconUrl);
            }

            new Panel()
            {
                Size = new Point(32, 32),
                Location = new Point(2, 2),
                BackgroundTexture = icon,
                Parent = rowPanel
            };

            // Never display raw currency IDs (repo invariant).
            string name = string.IsNullOrEmpty(entry.CurrencyName) ? "Unknown Currency" : entry.CurrencyName;
            new Label()
            {
                Text = $"{name}: {entry.Value:N0}",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(40, 6),
                Parent = rowPanel
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
