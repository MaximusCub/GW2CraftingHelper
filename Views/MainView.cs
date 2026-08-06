using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Views.Rendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    /// fully loaded. The actual scan/rebuild itself needs no cancellation -
    /// each call is a plain, side-effect-free pass over already-loaded
    /// data - but a short cancel-and-replace delay (see
    /// <see cref="ScheduleSearchRebuild"/>/<see cref="SearchDebounceMs"/>)
    /// bounds how often it runs while the search box is being typed into,
    /// which is where the cancellation/marshal ceremony below comes from.
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

        // itemId -> representative-entry map, built once per snapshot
        // (constructor and SetSnapshot, alongside _accountItemIndex) and
        // reused by every RebuildContent/BuildItemRows call for that
        // snapshot - i.e. once per search-box keystroke - instead of
        // BuildItemRows re-scanning the full raw _snapshot.Items list (which
        // can run into the thousands across a large account's characters,
        // bank, material storage, and shared inventory) from scratch on
        // every call. See SnapshotSearchResultBuilder.BuildRepresentativeIndex.
        private Dictionary<int, SnapshotItemEntry> _itemsById;

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

        // Trailing debounce for the search box's per-keystroke rebuild -
        // mirrors CraftingPlanView's ResizeDebounceMs/FrameTicker trailing-
        // settle pattern in spirit, but uses a plain cancel-and-replace
        // CancellationTokenSource + Task.Delay (the same shape
        // SuggestionPanel.OnTextChanged already uses for its own per-
        // keystroke search) rather than a second FrameTicker subclass,
        // since there is no per-frame work to drive here - only a single
        // one-shot delay before the next RebuildContent call. Without this,
        // RebuildContent (which disposes and recreates every visible row's
        // Panel/Label/AsyncTexture2D) ran once per character typed, not
        // once per pause in typing.
        private const int SearchDebounceMs = 150;
        private CancellationTokenSource _searchDebounceCts;

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
            // a null items list, and so does BuildRepresentativeIndex.
            _accountItemIndex = new AccountItemIndex(_snapshot?.Items);
            _itemsById = SnapshotSearchResultBuilder.BuildRepresentativeIndex(_snapshot?.Items);
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
            _itemsById = SnapshotSearchResultBuilder.BuildRepresentativeIndex(_snapshot?.Items);
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
            // A fresh build cycle supersedes any debounced rebuild still
            // pending from a previous visit to this tab (the old
            // _contentPanel/_searchBox this timer was armed against are
            // about to be replaced) - mirrors CraftingPlanView's own "top
            // of Build() cancels leftover tickers from the previous cycle"
            // convention (StopLiveTickers).
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = null;

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
                ScheduleSearchRebuild();
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

            // Coin display panel - see UpdateCoinDisplay's doc comment for
            // the M38 WP-22 repoint to the shared CoinCurrencyRenderer. The
            // actual UpdateCoinDisplay call is deferred to the marshaled
            // tail below (with ApplyStatusDisplay/RebuildContent) - see that
            // block's own comment for why.
            _coinPanel = new Panel()
            {
                Size = new Point(w, CoinHeight),
                Location = new Point(0, CoinRowY),
                Parent = buildPanel
            };

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

            // Same hazard class as the LogTabContent field crash (2026-08-06,
            // docs/KNOWN-ISSUES.md): Blish HUD runs a tab's Build() via
            // View.DoLoad().ContinueWith(...) on a ThreadPool thread, not the
            // main/game thread (docs/ARCHITECTURE.md Section 1). Unlike
            // LogTabContent, this instance is never recreated per tab visit
            // (Module.cs creates ONE MainView in Initialize() and keeps
            // reusing it), and Module.Update() calls SetSnapshot()/
            // SetStatus() on it every tick a background refresh completes -
            // regardless of which tab is currently selected, so it is not
            // even limited to "user is on the Snapshot tab right now". Both
            // paths end up calling UpdateCoinDisplay/ApplyStatusDisplay/
            // RebuildContent, which dispose-then-add into _coinPanel.
            // Children/_contentPanel.Children - if Update() ever landed
            // while Build() was still executing this tail on the ThreadPool
            // thread, two threads would mutate the same Children collections
            // concurrently, the same shape that corrupted LogTabContent's
            // _renderedRows Queue<T>. Marshaling this tail onto the main
            // thread makes every mutation path (this call, and every
            // Update()-driven SetSnapshot/SetStatus call) main-thread-only,
            // so they can never execute concurrently with each other - the
            // race is impossible BY CONSTRUCTION, matching LogTabContent's
            // fix. UpdateCoinDisplay is called here (rather than earlier,
            // right after _coinPanel is created) so all three calls that
            // race against SetSnapshot/SetStatus's own tail land in the same
            // queued callback.
            MainThreadMarshal.Run(() =>
            {
                // The view may already have been torn down by the time this
                // queued callback runs (tab switched away again, module
                // unloaded) - a disposed control's Parent is nulled on
                // disposal, mirroring this file's own Refresh Now guard.
                if (_headerPanel == null || _headerPanel.Parent == null) return;

                UpdateCoinDisplay(_snapshot?.CoinCopper ?? 0);
                ApplyStatusDisplay();
                RebuildContent();
            });
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

        /// <summary>
        /// Cancels-and-replaces the in-flight search debounce, then arms a
        /// new <see cref="SearchDebounceMs"/> delay before the next
        /// RebuildContent call - see the field doc comment on
        /// <see cref="_searchDebounceCts"/>. Called once per search-box
        /// keystroke; a fast typist therefore triggers exactly one rebuild
        /// after the last keystroke, not one per character.
        /// </summary>
        private void ScheduleSearchRebuild()
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            var cts = new CancellationTokenSource();
            _searchDebounceCts = cts;

            RunSearchDebounceAsync(cts.Token);
        }

        /// <summary>
        /// Waits <see cref="SearchDebounceMs"/>, then marshals the actual
        /// rebuild back onto the main thread - Blish HUD's XNA host installs
        /// no SynchronizationContext, so the continuation after
        /// <see cref="Task.Delay"/> may resume on a ThreadPool thread. The
        /// cancel-and-replace CancellationTokenSource shape (see
        /// ScheduleSearchRebuild) mirrors SuggestionPanel.OnTextChanged's
        /// own per-keystroke cancellation, though that method has no added
        /// delay of its own (it cancels a stale in-flight search, not a
        /// timer) - here the awaited step IS the delay itself.
        /// <paramref name="token"/> is a thread-safe struct to read from any
        /// thread; only the eventual RebuildContent call (a control
        /// mutation) needs marshaling.
        /// </summary>
        private async void RunSearchDebounceAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(SearchDebounceMs, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            MainThreadMarshal.Run(() =>
            {
                // A newer keystroke may have canceled this token, or the
                // tab/module may have been torn down while this was
                // pending (Build() tears down and recreates every control
                // on each tab visit - see the class doc comment) - either
                // way there is nothing to render into.
                if (token.IsCancellationRequested) return;
                if (_contentPanel == null || _contentPanel.Parent == null) return;
                RebuildContent();
            });
        }

        private void RebuildContent()
        {
            if (_contentPanel == null) return;

            // Whatever triggered this rebuild (an explicit checkbox/
            // dropdown click, or the debounced search callback itself)
            // supersedes any older still-pending debounced rebuild - avoids
            // a redundant extra rebuild landing a moment after this one.
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = null;

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
                    _itemsById, _accountItemIndex, searchText, sourceFilter, GetActiveCharacterName());
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
                string message;
                if (filter == "Wallet")
                {
                    // Wallet has no per-source breakdown at all - the four
                    // Bank/Material Storage/Shared Inventory/Characters
                    // checkboxes are documented and implemented as having
                    // zero effect here (FilterWallet takes no
                    // SnapshotSourceFilter), so the items-oriented "in the
                    // selected sources" wording below would be factually
                    // false for this filter and would send a user chasing
                    // checkbox toggles that cannot change the result.
                    message = trimmedSearch.Length == 0
                        ? "No currencies available."
                        : $"No currencies match \"{trimmedSearch}\".";
                }
                else
                {
                    message = trimmedSearch.Length == 0
                        ? "No items match the selected sources."
                        : $"No items match \"{trimmedSearch}\" in the selected sources.";
                }

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

        // M38 WP-22 (architecture report S6): this used to carry its own
        // GetCoinColor/AddCoinSegment copies, byte-identical to the ones
        // CraftingPlanView carried before its own coin/currency rendering
        // was extracted into Views/Rendering/CoinCurrencyRenderer (WP-21) -
        // the second independent encoding of the coin invariant. Both are
        // deleted; this now builds its own CoinSegmentSpec list (still
        // always exactly 3 segments - gold, silver, copper - via plain
        // ToString(), no leading-zero-unit omission or zero-padding: that
        // formatting choice is unchanged from before this package,
        // deliberately, per the M38 plan's behavior-preservation-by-
        // default rule) via the shared CoinCurrencyRenderer.AddSegmentSpec
        // (bumped private -> internal for this reuse - a normal forward
        // MainView -> Views/Rendering consumer dependency; see the note at
        // CoinCurrencyRenderer.AddSegmentSpec for why this is not the same
        // precedent as the reverted GetPillColors bump) and hands it to
        // LayoutCoinSegments with
        // startX = 0 (left-anchored) instead of the right-anchored
        // RenderValueCellRightAligned/MeasureValueWidth entry points
        // CraftingPlanView's mixed coin+currency value cells use - those
        // two are for a different rendering shape entirely (right-edge-
        // relative, with an unpriced-dash fallback this coin-only wallet
        // total never needs) and are untouched by this package.
        // ANCHORING: no new parameter was needed on CoinCurrencyRenderer -
        // LayoutCoinSegments already takes a caller-supplied startX and
        // lays out left-to-right from it, i.e. it is anchor-neutral by
        // construction; only the higher-level right-aligned convenience
        // wrappers are direction-locked, and this call site does not use
        // them. GetCoinColor is now applied internally by
        // LayoutCoinSegments, so this call site does not need it at all -
        // the per-segment geometry (icon size 20, label-to-icon gap 2,
        // inter-segment gap 6; label then icon to its right) is now the
        // exact same CoinSegmentMath-driven code CraftingPlanView's coin
        // cells use, not just a matching copy of it.
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

            var font = GameService.Content.DefaultFont14;
            var segments = new List<CoinSegmentMath.CoinSegmentSpec>(3);
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, 156904, gold.ToString());
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, 156907, silver.ToString());
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, 156902, cop.ToString());

            CoinCurrencyRenderer.LayoutCoinSegments(_coinPanel, segments, 0, 2, font);
        }
    }
}
