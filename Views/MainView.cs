using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Views.Rendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Views
{

    /// <summary>
    /// The Snapshot tab: a search-as-you-type account-inventory browser
    /// over
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
        private readonly ApiAccessDialog _apiAccessDialog;
        private readonly ModuleSettings _settings;
        private readonly Action _clearCache;
        private readonly Action<string> _saveStatus;
        private readonly Action<string> _saveStatusThreadSafe;

        // Session-sticky search/filter state (d1-snapshot-about-settings.md
        // Feature 1's "Tab views are rebuilt from scratch" cross-cutting
        // finding: Build() tears down and recreates every control on each
        // tab visit, so anything that should feel "sticky" across tab
        // switches must live in these instance fields, not the controls
        // themselves, and be read back in when Build() reruns). Every source
        // toggle defaults to true (show everything) and the content-type
        // dropdown defaults to "All", matching the tab's pre-search implicit
        // no-filter behavior.
        // <para>
        // Characters are held as the same exclusion set SnapshotSourceFilter
        // takes, keyed by character name: a name absent from it is checked,
        // so a character new in a fresh snapshot defaults to visible, and a
        // deliberately-unchecked one stays unchecked across tab bounces and
        // snapshot refreshes. Stale names (a deleted character) are left in
        // the set rather than pruned per snapshot - they match nothing, and
        // pruning would silently forget the user's choice whenever a
        // degraded snapshot happened to omit a character.
        // </para>
        private string _lastSearchText = "";
        private string _lastFilterSelection = "All";
        private bool _bankEnabled = true;
        private bool _materialStorageEnabled = true;
        private bool _sharedInventoryEnabled = true;
        private readonly HashSet<string> _uncheckedCharacters = new HashSet<string>(StringComparer.Ordinal);

        // Roster driving the per-character checkboxes, rebuilt once per
        // snapshot alongside _accountItemIndex/_itemsById.
        private List<string> _characterNames = new List<string>();

        // Set while a master-toggle cascade (or a master read-back) is
        // writing Checked on other checkboxes, so their own CheckedChanged
        // handlers do not each trigger their own RebuildContent - one user
        // click stays one rebuild.
        private bool _suppressSourceFilterEvents;

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

        // The status label gets its own full-width row beneath the
        // header rather than sharing _headerPanel with the buttons - a
        // long status string slid under the button row at the window's
        // clamped minimum size. So
        // every row below shifts down by StatusRowHeight + the same 5px
        // gap the header already used before SearchRowY - every other gap
        // (SearchRowY->SourceFilterRowY, etc.) is preserved exactly.
        private const int StatusRowY = HeaderRowY + HeaderHeight + 5;
        private const int StatusRowHeight = 24;
        private const int SearchRowY = StatusRowY + StatusRowHeight + 5;
        private const int SearchRowHeight = 35;
        private const int SourceFilterRowY = SearchRowY + SearchRowHeight + 3;
        private const int CoinHeight = 24;
        private const int SectionGapY = 4;

        // The source-filter row's height is account-driven: it carries one
        // checkbox per character (1 to 15+) and wraps onto extra rows rather
        // than running off the window's right edge, so every row below it
        // shifts down by whatever it ends up needing. _sourceFilterHeight
        // holds the current measured value (see ApplyTopRegionLayout);
        // SourceFilterSingleRowHeight is both the floor and the exact height
        // the row had while it was four fixed checkboxes, so the common
        // single-row case is pixel-identical to before.
        private const int SourceFilterCellHeight = 25;
        private const int SourceFilterCellGapX = 10;
        private const int SourceFilterRowGapY = 4;
        private const int SourceFilterTopPad = 3;
        private const int SourceFilterBottomPad = 2;
        private const int SourceFilterSingleRowHeight = SourceFilterTopPad + SourceFilterCellHeight + SourceFilterBottomPad;

        // The row grows one cell per character, so it must have an upper
        // bound: unbounded, a large roster in a short window pushes the
        // result list to zero height with no way for the user to shrink the
        // row back. Past the bound the row scrolls instead of growing (see
        // ApplyTopRegionLayout), so no checkbox becomes unreachable, and the
        // result list always keeps MinContentHeight.
        private const int SourceFilterMaxRows = 4;
        private const int SourceFilterMaxRowsHeight = SourceFilterTopPad
            + (SourceFilterMaxRows * SourceFilterCellHeight)
            + ((SourceFilterMaxRows - 1) * SourceFilterRowGapY)
            + SourceFilterBottomPad;
        private const int SourceFilterScrollbarAllowance = 20;
        private const int MinContentHeight = 120;

        // Checkbox width beyond its measured label: the box glyph plus its
        // text gap. Reproduces the four widths this row previously hardcoded
        // (e.g. "Bank" 70, "Material Storage" 170) from the measured text.
        private const int CheckboxChromeWidth = 40;

        private int _sourceFilterHeight = SourceFilterSingleRowHeight;
        private int _containerWidth;
        private int _containerHeight;

        private int CoinRowY => SourceFilterRowY + _sourceFilterHeight + SectionGapY;
        private int ContentY => CoinRowY + CoinHeight + SectionGapY;
        private int TopRegionHeight => ContentY;

        // Fixed distance from the filter row's bottom edge to the content
        // region's top: the coin row and the gap on either side of it.
        private const int BelowSourceFilterHeight = SectionGapY + CoinHeight + SectionGapY;

        // Height the filter row may not exceed: never tall enough to drop
        // the result list below MinContentHeight, never more than
        // SourceFilterMaxRows of cells, and never below the single-row
        // height the row had before it became account-sized.
        private int MaxSourceFilterHeight
        {
            get
            {
                int budget = _containerHeight - SourceFilterRowY - BelowSourceFilterHeight - MinContentHeight;
                int cap = budget < SourceFilterMaxRowsHeight ? budget : SourceFilterMaxRowsHeight;
                return cap > SourceFilterSingleRowHeight ? cap : SourceFilterSingleRowHeight;
            }
        }

        private const int SearchBoxWidth = 300;
        private const int FilterDropdownWidth = 140;
        private const int FilterDropdownX = SearchBoxWidth + 10;

        private const int ItemRowHeight = 52;
        private const int WalletRowHeight = 36;

        // UI controls (stored for resize handler)
        private Panel _headerPanel;
        private Panel _statusPanel;
        private Panel _filterPanel;
        private Panel _sourceFilterPanel;
        private FlowPanel _contentPanel;
        private TextBox _searchBox;
        private Dropdown _filterDropdown;
        private Checkbox _charactersMasterCheckbox;

        // Every source checkbox in flow order (the three storage locations,
        // the All Characters master, then one per character) - the single
        // list ApplyTopRegionLayout measures and positions. Not readonly:
        // Build swaps in fresh lists rather than clearing these in place,
        // see there.
        private List<Checkbox> _sourceFilterCells = new List<Checkbox>();

        // Parallel to _characterNames by construction (built in one loop).
        private List<Checkbox> _characterCheckboxes = new List<Checkbox>();

        private StandardButton _clearButton;
        private StandardButton _refreshButton;

        private Panel _coinPanel;
        private Label _statusLabel;
        private Color _defaultStatusColor;

        public MainView(
            AccountSnapshot snapshot,
            string initialStatus,
            Func<Task<AccountSnapshot>> refreshAsync,
            ApiAccessDialog apiAccessDialog,
            ModuleSettings settings,
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
            _characterNames = SnapshotSearchResultBuilder.CollectCharacterNames(_snapshot);
            _initialStatus = initialStatus;
            _refreshAsync = refreshAsync;
            _apiAccessDialog = apiAccessDialog;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _clearCache = clearCache;
            _saveStatus = saveStatus;
            _saveStatusThreadSafe = saveStatusThreadSafe;
        }

        public void SetSnapshot(AccountSnapshot snapshot)
        {
            _snapshot = snapshot;
            _accountItemIndex = new AccountItemIndex(_snapshot?.Items);
            _itemsById = SnapshotSearchResultBuilder.BuildRepresentativeIndex(_snapshot?.Items);

            var characterNames = SnapshotSearchResultBuilder.CollectCharacterNames(_snapshot);
            bool rosterChanged = !RosterEquals(_characterNames, characterNames);
            _characterNames = characterNames;

            // A refresh can add or drop a character, and that has to rebuild
            // the checkbox row itself, not just the result list below. Only
            // when the roster actually changed, though: this path is driven
            // by the periodic background refresh, and rebuilding disposes the
            // very checkbox a click may be mid-press on, losing the click.
            if (rosterChanged)
            {
                RebuildSourceFilterRow();
            }
            else
            {
                ApplyTopRegionLayout();
            }

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
            // convention (StopLiveTickers). The actual cancel-and-clear is
            // deferred to the marshaled tail below, NOT done here - see
            // that block's own comment for why: this method's own body
            // runs on a ThreadPool thread (same as every Build()), and
            // RebuildContent()/ScheduleSearchRebuild() touch this same
            // _searchDebounceCts field on the main thread, so cancelling it
            // here would race them.

            int w = buildPanel.ContentRegion.Width;
            _containerWidth = w;
            _containerHeight = buildPanel.ContentRegion.Height;

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
                var status = $"Cache Cleared \u2014 {DateTime.Now.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)}";
                SetStatus(status);
                _saveStatus(status);
            };

            _refreshButton.Click += async (_, __) => await RefreshNowAsync();

            // Full-width status row beneath the header buttons - see
            // StatusRowY.
            _statusPanel = new Panel()
            {
                Size = new Point(w, StatusRowHeight),
                Location = new Point(0, StatusRowY),
                Parent = buildPanel
            };

            _statusLabel = new Label()
            {
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                // Y=2 (not 4) inside this 24px _statusPanel -
                // matches the coin row's own precedent
                // (LayoutCoinSegments(_coinPanel, segments, 0, 2, font), y=2
                // in the same 24px height), leaving DefaultFont14 the same
                // clearance the coin row already relies on.
                Location = new Point(0, 2),
                Parent = _statusPanel
            };
            // Capture Blish's own real default rather than guessing/
            // hardcoding one, so the non-stale case is byte-identical to
            // today's unset-TextColor appearance once ApplyStatusDisplay
            // below starts writing to this property.
            _defaultStatusColor = _statusLabel.TextColor;

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
                PlaceholderText = "Search items, currencies, characters...",
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
            // Restored before the ValueChanged subscription (matching the
            // search box's Text-then-TextChanged order above) so the
            // read-back itself never triggers a redundant rebuild.
            _filterDropdown.SelectedItem = _lastFilterSelection;
            _filterDropdown.ValueChanged += (_, __) =>
            {
                _lastFilterSelection = _filterDropdown.SelectedItem ?? "All";
                RebuildContent();
            };

            // Source-filter row: one checkbox per storage location plus one
            // per character, all checked by default. Only meaningful when
            // the content-type dropdown includes Items (All/Items) - left
            // visible-but-inert when Wallet is selected rather than adding
            // show/hide logic that itself needs testing (d1 Feature 1's
            // deliberate simplicity choice). The checkboxes themselves are
            // created by RebuildSourceFilterRow from the marshaled tail
            // below, not here: they are account-driven (so they must be
            // rebuilt on every SetSnapshot too, from the main thread) and
            // keeping the single creation path means the two entry points
            // cannot drift. The three fields holding the OUTGOING panel's
            // checkboxes are dropped here rather than in that tail: until
            // it lands, a resize on the main thread would otherwise flow
            // controls belonging to a panel this method has already
            // replaced. Fresh lists rather than Clear() - the main thread
            // may be walking the old ones at this instant, and a reference
            // swap leaves it a consistent list either way. Every reader
            // already tolerates the empty state (it is also the state
            // before the first Build): ApplyTopRegionLayout flows zero
            // cells to the single-row height, SetAllCharactersChecked
            // bounds-checks the parallel list, and OnCharacterToggled
            // null-guards the master.
            _sourceFilterCells = new List<Checkbox>();
            _characterCheckboxes = new List<Checkbox>();
            _charactersMasterCheckbox = null;

            _sourceFilterPanel = new Panel()
            {
                Size = new Point(w, _sourceFilterHeight),
                Location = new Point(0, SourceFilterRowY),
                Parent = buildPanel
            };

            // Coin display panel - see UpdateCoinDisplay's doc comment for
            // the repoint to the shared CoinCurrencyRenderer. The
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

            // Same hazard family as the LogTabContent field crash
            // (docs/KNOWN-ISSUES.md): Blish HUD runs a tab's Build() via
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
            // Children/_contentPanel.Children.
            // <para>
            // NOT the hazard, despite resembling one: Blish's own
            // Container.Children (ControlCollection&lt;T&gt;) is itself
            // ReaderWriterLockSlim-guarded on every operation - Add, Remove,
            // AddRange, and the indexer all EnterWriteLock; Count and the
            // indexer getter EnterReadLock; GetEnumerator EnterReadLocks and
            // releases it from its ControlEnumerator's Dispose() -
            // independently confirmed by decompiling
            // packages/BlishHUD.1.3.0's Blish HUD.exe with ilspycmd. Unlike
            // LogTabContent's plain unsynchronized Queue&lt;(long,Label)&gt;,
            // concurrent Children access cannot corrupt the collection's own
            // internals.
            // </para>
            // <para>
            // The two hazards marshaling this tail actually closes: (a)
            // dispose-then-add is a non-atomic COMPOUND sequence - Children's
            // own lock protects each individual Add/Remove call, but nothing
            // holds a lock across the whole "dispose every old child, then
            // add every new one" sequence, so two interleaved
            // UpdateCoinDisplay/RebuildContent calls can each finish
            // disposing the OLD children before either adds the NEW ones,
            // and both survive - duplicated content, the same shape as the
            // doubled "No log entries yet." placeholders LogTabContent
            // hit live (see LogTabContent.cs's _buildComplete doc
            // comment); and (b) the top-of-Build
            // _searchDebounceCts?.Cancel();?.Dispose(); sequence this branch
            // moved into this tail used to run directly in Build()'s
            // ThreadPool-thread body, racing ScheduleSearchRebuild()/
            // RebuildContent(), which write the same field on the main
            // thread - CancellationTokenSource.Cancel() calls
            // ThrowIfDisposed(), so whichever call landed second on the
            // shared reference could throw ObjectDisposedException.
            // Marshaling this tail onto the main thread serializes it
            // against every Update()-driven SetSnapshot/SetStatus call and
            // every main-thread handler touching the same fields, so
            // neither hazard can occur - impossible BY CONSTRUCTION, matching
            // LogTabContent's fix (which closes the DIFFERENT hazard of an
            // actually-unsynchronized Queue&lt;T&gt;, not a Children race).
            // </para>
            // UpdateCoinDisplay is called here (rather than earlier, right
            // after _coinPanel is created) so all three calls land in the
            // same queued callback as the _searchDebounceCts cleanup above.
            // <para>
            // This does NOT make every MainView mutation path main-thread-
            // only - unlike the state above, the panel/control fields
            // themselves (_headerPanel, _contentPanel, _coinPanel,
            // _searchBox, the checkboxes, etc.) are still first published
            // by the REST of Build()'s body on this same ThreadPool thread,
            // same as LogTabContent's eight control fields. This file's own
            // Clear Cache/Refresh Now/checkbox/dropdown click handlers are
            // wired up mid-body and become clickable the instant each
            // control is parented, so they can run on the main thread while
            // Build() is still constructing later controls; that is only
            // survivable because every one of those handlers' downstream
            // calls (UpdateCoinDisplay, ApplyStatusDisplay, RebuildContent)
            // already null-guards the field it touches. Pre-existing
            // pattern, not changed by this fix.
            // </para>
            MainThreadMarshal.Run(() =>
            {
                // Supersedes any debounce armed by a previous visit to this
                // tab - see the top-of-Build comment for why this cannot run
                // there instead. Deliberately BEFORE the liveness guard
                // below, so a stale debounce is still cancelled even if the
                // module was unloaded (or this tab revisited and rebuilt)
                // before this queued callback ran - otherwise it would sit
                // un-cancelled until some future Build() cycle's own tail
                // happens to run.
                CancelSearchDebounce();

                // The module may have been unloaded by the time this queued
                // callback runs - a disposed control's Parent is nulled on
                // disposal, mirroring this file's own Refresh Now guard.
                // NOTE: a plain tab switch-away does NOT null Parent - see
                // docs/ARCHITECTURE.md Section 1 ("a tab switch detaches, it
                // does not dispose") - so if the user switched away from
                // this tab before this tail lands, this guard does not trip
                // and UpdateCoinDisplay/ApplyStatusDisplay/RebuildContent
                // below still run, just into a real header panel the user
                // can no longer see. Wasted work, not a hazard.
                if (_headerPanel == null || _headerPanel.Parent == null) return;

                RebuildSourceFilterRow();
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

            _containerWidth = w;
            _containerHeight = h;

            _headerPanel.Size = new Point(w, HeaderHeight);
            _clearButton.Location = new Point(w - 220, 5);
            _refreshButton.Location = new Point(w - 110, 5);
            _statusPanel.Size = new Point(w, StatusRowHeight);
            _filterPanel.Size = new Point(w, SearchRowHeight);

            // Re-flows the source-filter checkboxes at the new width (a
            // narrower window can push them onto more rows) and re-anchors
            // the coin and content panels beneath whatever height that
            // needs - the reason those two are not sized here directly.
            ApplyTopRegionLayout();
        }

        /// <summary>
        /// Disposes and recreates every source-filter checkbox from the
        /// current roster, restoring each one's session-sticky checked state
        /// (see <see cref="_uncheckedCharacters"/>), then re-flows the row.
        /// Main-thread only, like every other control mutation here: both
        /// call sites are <c>Build</c>'s marshaled tail and
        /// <see cref="SetSnapshot"/> (itself only reached from
        /// Module.Update's tick or a marshaled refresh tail).
        /// </summary>
        private void RebuildSourceFilterRow()
        {
            if (_sourceFilterPanel == null) return;

            foreach (var child in _sourceFilterPanel.Children.ToArray())
            {
                child.Dispose();
            }

            _sourceFilterCells.Clear();
            _characterCheckboxes.Clear();
            _charactersMasterCheckbox = null;

            AddSourceCheckbox("Bank", _bankEnabled, isChecked => _bankEnabled = isChecked);
            AddSourceCheckbox("Material Storage", _materialStorageEnabled, isChecked => _materialStorageEnabled = isChecked);
            AddSourceCheckbox("Shared Inventory", _sharedInventoryEnabled, isChecked => _sharedInventoryEnabled = isChecked);

            // A master toggle earns its place only once there is more than
            // one character to cascade to.
            if (_characterNames.Count > 1)
            {
                _charactersMasterCheckbox = AddSourceCheckbox("All Characters", AllCharactersChecked(), SetAllCharactersChecked);
            }

            foreach (string name in _characterNames)
            {
                _characterCheckboxes.Add(AddSourceCheckbox(
                    name,
                    !_uncheckedCharacters.Contains(name),
                    isChecked => OnCharacterToggled(name, isChecked)));
            }

            ApplyTopRegionLayout();
        }

        /// <summary>
        /// Ordinal element-wise comparison of two rosters. Order is
        /// meaningful and stable - CollectCharacterNames sorts - so two
        /// snapshots of the same account compare equal.
        /// </summary>
        private static bool RosterEquals(List<string> left, List<string> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            if (left.Count != right.Count) return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal)) return false;
            }

            return true;
        }

        /// <summary>
        /// Creates one source-filter checkbox, appends it to the flow order,
        /// and wires its click to <paramref name="onChanged"/> plus a single
        /// content rebuild. Location is a placeholder until
        /// <see cref="ApplyTopRegionLayout"/> flows the row.
        /// </summary>
        private Checkbox AddSourceCheckbox(string text, bool isChecked, Action<bool> onChanged)
        {
            var checkbox = new Checkbox()
            {
                Text = text,
                Checked = isChecked,
                Size = new Point(MeasureCheckboxWidth(text), SourceFilterCellHeight),
                Location = new Point(0, SourceFilterTopPad),
                Parent = _sourceFilterPanel
            };

            checkbox.CheckedChanged += (_, __) =>
            {
                if (_suppressSourceFilterEvents) return;
                onChanged(checkbox.Checked);
                RebuildContent();
            };

            _sourceFilterCells.Add(checkbox);
            return checkbox;
        }

        private static int MeasureCheckboxWidth(string text)
        {
            var font = GameService.Content.DefaultFont14;
            int textWidth = (int)Math.Ceiling(font.MeasureString(text ?? "").Width);
            return textWidth + CheckboxChromeWidth;
        }

        private bool AllCharactersChecked()
        {
            foreach (string name in _characterNames)
            {
                if (_uncheckedCharacters.Contains(name)) return false;
            }

            return true;
        }

        private void SetAllCharactersChecked(bool isChecked)
        {
            _suppressSourceFilterEvents = true;
            try
            {
                for (int i = 0; i < _characterNames.Count; i++)
                {
                    string name = _characterNames[i];
                    if (isChecked)
                    {
                        _uncheckedCharacters.Remove(name);
                    }
                    else
                    {
                        _uncheckedCharacters.Add(name);
                    }

                    if (i < _characterCheckboxes.Count)
                    {
                        _characterCheckboxes[i].Checked = isChecked;
                    }
                }
            }
            finally
            {
                _suppressSourceFilterEvents = false;
            }
        }

        private void OnCharacterToggled(string characterName, bool isChecked)
        {
            if (isChecked)
            {
                _uncheckedCharacters.Remove(characterName);
            }
            else
            {
                _uncheckedCharacters.Add(characterName);
            }

            if (_charactersMasterCheckbox != null)
            {
                _suppressSourceFilterEvents = true;
                try
                {
                    _charactersMasterCheckbox.Checked = AllCharactersChecked();
                }
                finally
                {
                    _suppressSourceFilterEvents = false;
                }
            }
        }

        /// <summary>
        /// Flows the source-filter checkboxes at the current width and
        /// re-anchors the coin and content rows beneath the height that
        /// needs - the one place <see cref="_sourceFilterHeight"/> (and
        /// therefore CoinRowY/ContentY/TopRegionHeight) is written.
        /// </summary>
        private void ApplyTopRegionLayout()
        {
            int w = _containerWidth;

            if (_sourceFilterPanel != null)
            {
                var widths = new List<int>(_sourceFilterCells.Count);
                foreach (var checkbox in _sourceFilterCells)
                {
                    widths.Add(checkbox.Width);
                }

                var flow = SourceFilterFlowLayout.Layout(
                    widths, w, SourceFilterCellHeight, SourceFilterCellGapX, SourceFilterRowGapY);
                int height = SourceFilterTopPad + flow.TotalHeight + SourceFilterBottomPad;

                // Past the cap the row scrolls rather than growing, so the
                // cells have to be re-flowed clear of the scrollbar strip -
                // which can itself wrap one more cell, hence the second pass.
                int cap = MaxSourceFilterHeight;
                bool scroll = height > cap;
                if (scroll)
                {
                    flow = SourceFilterFlowLayout.Layout(
                        widths,
                        w - SourceFilterScrollbarAllowance,
                        SourceFilterCellHeight,
                        SourceFilterCellGapX,
                        SourceFilterRowGapY);
                    height = SourceFilterTopPad + flow.TotalHeight + SourceFilterBottomPad;
                }

                for (int i = 0; i < _sourceFilterCells.Count; i++)
                {
                    _sourceFilterCells[i].Location = new Point(flow.Cells[i].X, SourceFilterTopPad + flow.Cells[i].Y);
                }

                if (height < SourceFilterSingleRowHeight)
                {
                    height = SourceFilterSingleRowHeight;
                }

                _sourceFilterHeight = height < cap ? height : cap;
                _sourceFilterPanel.CanScroll = scroll;
                _sourceFilterPanel.Size = new Point(w, _sourceFilterHeight);
            }

            if (_coinPanel != null)
            {
                _coinPanel.Location = new Point(0, CoinRowY);
                _coinPanel.Size = new Point(w, CoinHeight);
            }

            if (_contentPanel != null)
            {
                // MaxSourceFilterHeight already reserves MinContentHeight
                // here; a window shorter than the fixed rows above the
                // filter row can still drive this negative, and clamping at
                // zero keeps the panel degenerate-but-valid.
                int contentHeight = _containerHeight - TopRegionHeight;
                _contentPanel.Location = new Point(0, ContentY);
                _contentPanel.Size = new Point(w, contentHeight > 0 ? contentHeight : 0);
            }
        }

        /// <summary>
        /// The Refresh Now button's full click flow - also invoked by the
        /// ApiAccessDialog's Retry button,
        /// so this is a method rather than an inline lambda: both entry
        /// points are Blish UI event handlers (Click, or the dialog's own
        /// Click-driven Retry callback), so both always start on the main
        /// thread, matching CraftingPlanView.TriggerGenerate's own doc
        /// comment on why its own confirm-modal callback needs no extra
        /// synchronization here.
        /// <para>
        /// On failure, classifies the exception via
        /// SnapshotFailureClassifier (Blish-free, real-unit-tested) and:
        /// ApiAccessNotReady pops the ApiAccessDialog walkthrough (the
        /// character-select incident this exists for); every other kind
        /// just gets a more specific status label than the old bare
        /// "Refresh failed" - see StatusText.ForRefreshFailure.
        /// </para>
        /// </summary>
        private async Task RefreshNowAsync()
        {
            if (_refreshAsync == null) return;

            _refreshButton.Enabled = false;
            _clearButton.Enabled = false;
            SetStatus("Refreshing...");

            try
            {
                var snapshot = await _refreshAsync();
                string status = snapshot != null
                    ? $"Updated \u2014 {snapshot.CapturedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)}"
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
                    // The module may have been disabled/unloaded while
                    // the refresh was in flight - a disposed control's
                    // Parent is nulled on disposal, mirroring
                    // CraftingPlanView's ResizeDebounceStep check.
                    // Persistence above already happened regardless, so
                    // bailing here cannot strand any state. NOTE: a
                    // plain tab switch-away does NOT null Parent - see
                    // docs/ARCHITECTURE.md Section 1 ("a tab switch
                    // detaches, it does not dispose") - so this guard
                    // covers module teardown only; a tab-switched-away
                    // user still gets SetSnapshot/SetStatus run into a
                    // real, just no-longer-visible, header panel.
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

                var classification = SnapshotFailureClassifier.Classify(ex);
                string cause = StatusText.ForRefreshFailure(classification.Kind, classification.FailedSourceCount, classification.TotalSourceCount);
                var status = $"{cause} \u2014 {DateTime.Now.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)}";
                _saveStatusThreadSafe(status);
                MainThreadMarshal.Run(() =>
                {
                    if (_headerPanel == null || _headerPanel.Parent == null) return;
                    SetStatus(status);

                    // Same guard as every other UI mutation in this tail -
                    // "safe if the tab was switched away before the failure
                    // lands" means the module-unload check above, not a
                    // stricter "only show if still on this tab" rule: the
                    // dialog is a top-level SpriteScreen-parented window
                    // (like ModalDialog), independent of tab selection, and
                    // a user who clicked Refresh Now and tabbed away while
                    // waiting still wants to know why it failed.
                    if (classification.Kind == SnapshotFailureKind.ApiAccessNotReady)
                    {
                        _apiAccessDialog?.Show(() => { _ = RefreshNowAsync(); });
                    }
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
        }

        /// <summary>
        /// Composes the header status label's text (base status text plus
        /// a staleness-age suffix, e.g. "Updated - Aug 15, 2026 3:41 PM
        /// (2m ago)") and recolors it once the snapshot is older than the
        /// SnapshotRefreshIntervalMinutes setting - the same threshold
        /// Module.Update()'s auto-refresh gate reads, re-read (clamped)
        /// on every call here just like that gate does, so a Settings tab
        /// save changes both together. Called from every place the
        /// status text or the snapshot itself changes (Build's initial
        /// render, SetSnapshot, SetStatus) so the two can never drift out
        /// of sync with each other.
        /// <para>
        /// _statusLabel lives in its own full-width _statusPanel row
        /// beneath the header, so a long status string has no button run
        /// to collide with. This
        /// method still intentionally does not truncate the composed text;
        /// the full-width row is simply far less likely to run out of
        /// space than the header's old shared, button-crowded run was.
        /// </para>
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
                var staleThreshold = TimeSpan.FromMinutes(_settings.GetClampedSnapshotRefreshIntervalMinutes());
                _statusLabel.TextColor = StatusText.IsStale(age, staleThreshold) ? WarningTextColor : _defaultStatusColor;
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
            CancelSearchDebounce();
            var cts = new CancellationTokenSource();
            _searchDebounceCts = cts;

            RunSearchDebounceAsync(cts.Token);
        }

        /// <summary>
        /// Cancels and disposes any in-flight <see cref="_searchDebounceCts"/>
        /// and clears the field - the single source of truth for that
        /// three-step sequence, shared by <see cref="ScheduleSearchRebuild"/>
        /// (which immediately arms a fresh one afterward), <see
        /// cref="RebuildContent"/>, and <c>Build</c>'s marshaled tail. All
        /// three call sites are main-thread-only - see <see
        /// cref="_searchDebounceCts"/>'s own field doc comment and
        /// <c>Build</c>'s comment for why this must never be called from
        /// <c>Build</c>'s own ThreadPool-thread body.
        /// </summary>
        private void CancelSearchDebounce()
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = null;
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
                // A newer keystroke may have canceled this token (the
                // common case). Cancellation is NOT synchronous with a
                // same-tab revisit though: CancelSearchDebounce() runs
                // inside every fresh Build()'s own MainThreadMarshal tail,
                // not at the top of Build itself - Build's body executes on
                // a ThreadPool thread (see Build's top-of-method comment for
                // why the cancel cannot live there), so a same-tab revisit
                // cancels this one queued main-thread callback later, not
                // synchronously with the revisit. A debounce armed on a
                // previous visit is therefore still live for the window
                // between Build's ThreadPool-thread body finishing and its
                // own MainThreadMarshal tail draining: if this callback is
                // the one that lands in that window, token.
                // IsCancellationRequested is still false and the guard below
                // passes, so RebuildContent() runs here using whatever
                // _contentPanel/_searchBox/_filterDropdown/checkbox values
                // this instance's fields currently hold - Build assigns
                // _contentPanel LAST among those (see the field-assignment
                // order in Build's body), and nothing synchronizes
                // cross-thread visibility of the individual writes in
                // between, so in the narrowest case this could even read the
                // NEW _searchBox/filters against the OLD _contentPanel.
                // Either way the result is superseded moments later by
                // Build's own tail calling RebuildContent() again correctly
                // - a wasted rebuild, not a wrong final state.
                //
                // Separately: the module may have been unloaded while this
                // was pending, which is what the Parent-null half of the
                // guard below actually catches - NOT a plain tab
                // switch-away (see docs/ARCHITECTURE.md Section 1, "a tab
                // switch detaches, it does not dispose"): _contentPanel
                // keeps a non-null Parent in that case, so an uncancelled
                // debounce would still render, just into a panel the user
                // can no longer see.
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
            CancelSearchDebounce();

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
                // Read from the sticky fields, not the checkboxes: the
                // fields are the source of truth the controls are built
                // from, and the controls do not exist until the row is
                // rebuilt on the main thread. The exclusion set is copied
                // (roster-sized, so tens of entries) rather than handed over
                // by reference: SnapshotSourceFilter is a mutable public
                // carrier, and nothing across that boundary promises to
                // leave it alone - a normalizing or pruning pass on the far
                // side would otherwise silently re-check the user's boxes.
                var sourceFilter = new SnapshotSourceFilter
                {
                    Bank = _bankEnabled,
                    MaterialStorage = _materialStorageEnabled,
                    SharedInventory = _sharedInventoryEnabled,
                    UncheckedCharacters = new HashSet<string>(_uncheckedCharacters, StringComparer.Ordinal)
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
                    // Wallet has no per-source breakdown at all - the
                    // storage-location and per-character checkboxes are
                    // documented and implemented as having
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

            // A missing IconUrl is a data gap (e.g. the API dropped it),
            // never a genuine load failure - IconControls.CreateItemIcon
            // degrades it to a neutral empty-slot square instead of
            // Blish's alarming magenta missing-texture placeholder (audit
            // row 56 PART B #1).
            IconControls.CreateItemIcon(rowPanel, row.IconUrl, 2, 2);

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

            // See CreateItemRow's matching comment -
            // same data-gap-vs-failure distinction applies to a wallet
            // currency's icon (e.g. the reported Spirit Shards row).
            IconControls.CreateItemIcon(rowPanel, entry.IconUrl, 2, 2);

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

        // This used to carry its own
        // GetCoinColor/AddCoinSegment copies, byte-identical to the ones
        // CraftingPlanView carried before its own coin/currency rendering
        // was extracted into Views/Rendering/CoinCurrencyRenderer -
        // the second independent encoding of the coin invariant. Both are
        // deleted; this now builds its own CoinSegmentSpec list (still
        // always exactly 3 segments - gold, silver, copper - via plain
        // ToString(), no leading-zero-unit omission or zero-padding: that
        // formatting choice is unchanged,
        // deliberately) via the shared CoinCurrencyRenderer.AddSegmentSpec
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

            var (gold, silver, cop) = CoinSegmentMath.Split(copper);

            var font = GameService.Content.DefaultFont14;
            var segments = new List<CoinSegmentMath.CoinSegmentSpec>(3);
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, 156904, gold.ToString());
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, 156907, silver.ToString());
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, 156902, cop.ToString());

            CoinCurrencyRenderer.LayoutCoinSegments(_coinPanel, segments, 0, 2, font);
        }
    }
}
