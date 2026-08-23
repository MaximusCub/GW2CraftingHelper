using Blish_HUD;
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
        private readonly ModalDialog _modalDialog;
        private readonly ModuleSettings _settings;
        private readonly Action _clearCache;
        private readonly Action<string> _saveStatus;
        private readonly Action<string> _saveStatusThreadSafe;

        // Session-sticky search/filter state: Build() recreates every
        // control per tab visit, so anything that has to survive a tab
        // switch lives here and is read back in when Build() reruns.
        private string _lastSearchText = "";
        private string _lastFilterSelection = "All";
        private bool _bankEnabled = true;
        private bool _materialStorageEnabled = true;
        private bool _sharedInventoryEnabled = true;

        // Exclusion set, keyed by character name: absent means checked, so
        // a character new in a fresh snapshot defaults to visible. Stale
        // names are never pruned.
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

        // Width-driven re-fit of the rows already on screen - see
        // ScheduleRowRefit. Deliberately NOT the search debounce above: that
        // one is cancel-and-replace, so routing a resize drag through it
        // allocated a CancellationTokenSource and threw a cancellation
        // exception per drag FRAME, and its callback disposes and rebuilds
        // every row (re-running the search, and putting the scroll position
        // at risk) to change nothing but text that no longer fits.
        // _lastRowLayoutWidth is the width the rows on screen were actually
        // laid out at, so a drag that ends where it started re-fits nothing.
        private const int ResizeSettleMs = 150;
        private readonly List<Action<int>> _rowRefitActions = new List<Action<int>>();
        private int _lastRowLayoutWidth = -1;
        private bool _rowRefitPending;
        private long _lastResizeEventTicks;

        // Layout constants
        private const int HeaderRowY = 5;
        private const int HeaderHeight = 40;

        // Vertically centred in the 40px header panel, derived rather than
        // written down: these two buttons were the module's only 30px ones
        // and dropping them to the shared UiMetrics.ButtonHeight would
        // otherwise have left them sitting 2px high against the "Account
        // Snapshot" label beside them.
        private const int HeaderButtonY = (HeaderHeight - UiMetrics.ButtonHeight) / 2;

        // The status label gets its own full-width row beneath the
        // header rather than sharing _headerPanel with the buttons - a
        // long status string slid under the button row at the window's
        // clamped minimum size. So
        // every row below shifts down by StatusRowHeight + the same 5px
        // gap the header already used before SearchRowY.
        private const int StatusRowY = HeaderRowY + HeaderHeight + 5;
        private const int StatusRowHeight = SnapshotHeaderLayout.StatusRowHeight;
        private const int SearchRowY = StatusRowY + StatusRowHeight + 5;
        private const int SearchRowHeight = 35;

        // The source-filter checkboxes share the search row - same Y,
        // offset X - while the whole run fits beside the search box in one
        // row: everything right of the content-type dropdown was empty. Past
        // one row they drop back to their own full-width row below it, this
        // gap beneath the search row, because sharing halves the width they
        // flow into and a wrapped run hides filters behind the 4-row cap's
        // scrollbar. Which mode is live is decided by the flow itself, in
        // ApplyTopRegionLayout - see Services/SnapshotHeaderLayout.
        private const int SearchToFilterGapY = 3;
        private const int CoinHeight = 24;
        private const int SectionGapY = 4;

        // The source-filter run's height is account-driven: it carries one
        // checkbox per character (1 to 15+) and wraps onto extra rows rather
        // than running off the window's right edge. While it shares the
        // search row the rows below shift down only by what it needs BEYOND
        // the search row's own height (see SearchBandHeight) - a run that
        // fits beside the search box costs the header nothing.
        // _sourceFilterHeight holds the current measured value (see
        // ApplyTopRegionLayout); SourceFilterSingleRowHeight is its floor.
        private const int SourceFilterCellHeight = 25;
        private const int SourceFilterCellGapX = 10;
        private const int SourceFilterRowGapY = 4;
        private const int SourceFilterTopPad = 3;
        private const int SourceFilterBottomPad = 2;
        private const int SourceFilterSingleRowHeight = SourceFilterTopPad + SourceFilterCellHeight + SourceFilterBottomPad;

        // The run grows one cell per character, so it must have an upper
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
        // text gap. Approximates the four widths this row previously
        // hardcoded (e.g. "Bank" 70, "Material Storage" 170) from the
        // measured text - close, not equal; only the single-row height is
        // reproduced exactly.
        private const int CheckboxChromeWidth = 40;

        private int _sourceFilterHeight = SourceFilterSingleRowHeight;
        private int _containerWidth;
        private int _containerHeight;

        // The inputs the source-filter row was last flowed against: the
        // container width (the flow's own width is a pure function of it and
        // of the mode the flow itself picks) and MaxSourceFilterHeight in
        // BOTH modes - height-driven, and it decides whether the row scrolls
        // and so re-flows narrower. A resize moving none of them - most of a
        // vertical drag - reuses the placements instead of re-running the
        // flow and rewriting every checkbox Location. -1 is the invalid
        // marker, set wherever the cell set itself changes.
        private int _lastFlowWidth = -1;
        private int _lastFlowSharedCap = -1;
        private int _lastFlowOwnRowCap = -1;

        // Which of the two modes the last flow resolved to - see
        // SearchToFilterGapY. Shared until a flow says otherwise, so the
        // pre-first-snapshot header (zero cells) reserves nothing.
        private bool _sharesSearchRow = true;

        private SnapshotHeaderLayout.SourceFilterPlacement CurrentPlacement =>
            SnapshotHeaderLayout.PlaceSourceFilterRun(
                _containerWidth, SourceFilterX, SearchRowHeight, SearchToFilterGapY, _sharesSearchRow);

        private int SearchBandHeight =>
            SnapshotHeaderLayout.SearchBandHeight(SearchRowHeight, _sourceFilterHeight, CurrentPlacement);

        private int CoinRowY => SearchRowY + SearchBandHeight + SectionGapY;
        private int ContentY => CoinRowY + CoinHeight + SectionGapY;
        private int TopRegionHeight => ContentY;

        // Fixed distance from the search band's bottom edge to the content
        // region's top: the coin row and the gap on either side of it.
        private const int BelowSourceFilterHeight = SectionGapY + CoinHeight + SectionGapY;

        // Height the filter row may not exceed, given the y it starts at
        // (which differs by mode - see SearchToFilterGapY): never tall
        // enough to drop the result list below MinContentHeight, never more
        // than SourceFilterMaxRows of cells, and never below the single-row
        // height the row had before it became account-sized.
        private int MaxSourceFilterHeight(int filterRowY)
        {
            int budget = _containerHeight - filterRowY - BelowSourceFilterHeight - MinContentHeight;
            int cap = budget < SourceFilterMaxRowsHeight ? budget : SourceFilterMaxRowsHeight;
            return cap > SourceFilterSingleRowHeight ? cap : SourceFilterSingleRowHeight;
        }

        private const int SearchBoxWidth = 300;
        private const int FilterDropdownWidth = 140;
        private const int FilterDropdownX = SearchBoxWidth + 10;

        // Left x of the source-filter run, clear of the dropdown. The
        // panel itself carries this offset, so SourceFilterFlowLayout keeps
        // placing cells from 0 in the panel's own coordinates and only its
        // available width changes.
        private const int SourceFilterX = FilterDropdownX + FilterDropdownWidth + 20;

        // Dim caption ahead of the wallet coin total, so the row reads as a
        // labelled figure rather than a stray unlabelled list row.
        private const string CoinCaption = "Coin";
        private const int CoinCaptionGap = 8;
        private static readonly Color CoinCaptionColor = new Color(130, 130, 130);

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

        // Bumped on every completed Clear Cache. RefreshNowAsync captures it
        // before awaiting and drops its own status/snapshot tail if it
        // changed while the fetch was in flight - see ConfirmClearCache.
        // volatile because the capture is compared on the awaited
        // continuation, which Blish's context-less XNA host may resume on a
        // ThreadPool thread, while every write is a main-thread click.
        private volatile int _clearGeneration;

        // True for the Clear Cache confirm dialog's lifetime. Survives the
        // tab switch that recreates both buttons, which is why the gate
        // lives here and not only in the buttons' Enabled flags.
        private bool _clearConfirmOpen;

        private Panel _coinPanel;
        private Label _statusLabel;
        private LoadingSpinner _statusSpinner;

        // Whether a Refresh Now is still in flight. Read by Build() so a
        // rebuild mid-refresh restores the spinner, and by the refresh's own
        // start/finally pair so the two can never disagree.
        private bool _refreshInFlight;
        private Color _defaultStatusColor;

        public MainView(
            AccountSnapshot snapshot,
            string initialStatus,
            Func<Task<AccountSnapshot>> refreshAsync,
            ApiAccessDialog apiAccessDialog,
            ModalDialog modalDialog,
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
            _modalDialog = modalDialog ?? throw new ArgumentNullException(nameof(modalDialog));
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

            _clearButton = new FeedbackButton()
            {
                Text = "Clear Cache",
                Size = new Point(100, UiMetrics.ButtonHeight),
                Location = new Point(w - 220, HeaderButtonY),
                Parent = _headerPanel,
                Enabled = _clearCache != null
            };
            TooltipFacility.ApplyPlain(
                _clearButton,
                "Discard the cached account snapshot. It can only be rebuilt when the GW2 API is reachable.");

            _refreshButton = new FeedbackButton()
            {
                Text = "Refresh Now",
                Size = new Point(100, UiMetrics.ButtonHeight),
                Location = new Point(w - 110, HeaderButtonY),
                Parent = _headerPanel,
                Enabled = _refreshAsync != null
            };

            _clearButton.Click += (_, __) => ConfirmClearCache();

            _refreshButton.Click += async (_, __) => await RefreshNowAsync();

            // Re-applies the confirm-dialog gate to the buttons this call
            // just recreated: a tab switch while the Clear Cache confirm is
            // open must not hand the user back a live Refresh Now.
            SetSnapshotActionsEnabled(true);

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

            // Trails the status text for the whole of a Refresh Now. A tab
            // switch rebuilds this row while the refresh is still running,
            // so its visibility comes from _refreshInFlight rather than
            // defaulting to hidden - otherwise the returning user sees
            // "Refreshing..." with nothing turning.
            _statusSpinner = InlineSpinner.Create(_statusPanel, InlineSpinnerLayout.SnapshotStatusSize);
            _statusSpinner.Visible = _refreshInFlight;
            InlineSpinner.PlaceAfter(_statusSpinner, _statusLabel, InlineSpinnerLayout.LabelGap);

            // Capture Blish's own real default rather than guessing/
            // hardcoding one, so the non-stale case is byte-identical to
            // today's unset-TextColor appearance once ApplyStatusDisplay
            // below starts writing to this property.
            _defaultStatusColor = _statusLabel.TextColor;

            // Search row: plain TextBox (not SuggestionPanel/
            // AutocompleteTextBox - see class doc comment) + the existing
            // content-type dropdown alongside it.
            // While the filter run shares this row its width stops at
            // SourceFilterX rather than spanning: the source-filter panel is
            // a later sibling occupying the rest of the row, and two
            // overlapping full-width panels would leave which one receives a
            // checkbox click up to child ordering. On the run's own-row
            // fallback there is no overlap, and it spans as it always did.
            _filterPanel = new Panel()
            {
                Size = new Point(FilterPanelWidth(w), SearchRowHeight),
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

            // Source-filter run, in the empty right half of the search row:
            // one checkbox per storage location plus one
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
            // swap leaves it a consistent list either way, PROVIDED each
            // reader takes the field into a local once rather than
            // re-reading it after its own guard - SetAllCharactersChecked,
            // OnCharacterToggled and ApplyTopRegionLayout all do. Every
            // reader also tolerates
            // the empty/null state, which is the state before the first
            // Build anyway: ApplyTopRegionLayout flows zero cells to the
            // single-row height, SetAllCharactersChecked bounds-checks the
            // parallel list, and OnCharacterToggled null-checks the master.
            _sourceFilterCells = new List<Checkbox>();
            _characterCheckboxes = new List<Checkbox>();
            _charactersMasterCheckbox = null;
            _lastFlowWidth = -1;

            // Placeholder placement in whichever mode is current: the flow
            // that decides the mode needs the checkboxes, and they are
            // created by RebuildSourceFilterRow from the marshaled tail
            // below. _lastFlowWidth is invalidated just above, so the
            // ApplyTopRegionLayout that tail ends with cannot early-out.
            var placement = CurrentPlacement;
            _sourceFilterPanel = new Panel()
            {
                Size = new Point(placement.Width, _sourceFilterHeight),
                Location = new Point(placement.X, SearchRowY + placement.OffsetY),
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

            bool widthChanged = w != _containerWidth;
            _containerWidth = w;
            _containerHeight = h;

            _headerPanel.Size = new Point(w, HeaderHeight);
            _clearButton.Location = new Point(w - 220, HeaderButtonY);
            _refreshButton.Location = new Point(w - 110, HeaderButtonY);
            _statusPanel.Size = new Point(w, StatusRowHeight);

            // Re-flows the source-filter checkboxes at the new width (a
            // narrower window can push them onto more rows, and past one row
            // off the search row entirely) and re-anchors the search, coin
            // and content panels beneath whatever that needs - the reason
            // those three are not sized here directly.
            ApplyTopRegionLayout();

            // Result rows are ellipsized to the content width at build
            // time, so a width change has to re-fit them or a widened
            // window keeps showing "..." on text that now fits. A
            // height-only drag re-ellipsizes nothing and arms nothing.
            if (widthChanged)
            {
                ScheduleRowRefit();
            }
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
            _lastFlowWidth = -1;

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
            // One read of the field, not one per iteration: Build may swap
            // in a fresh empty list at any point (see its own comment), and
            // a bound taken from the old list must not index the new one.
            var checkboxes = _characterCheckboxes;

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

                    if (i < checkboxes.Count)
                    {
                        checkboxes[i].Checked = isChecked;
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

            // Read once: Build may null the field between the guard and the
            // write (see its own comment).
            var master = _charactersMasterCheckbox;
            if (master != null)
            {
                _suppressSourceFilterEvents = true;
                try
                {
                    master.Checked = AllCharactersChecked();
                }
                finally
                {
                    _suppressSourceFilterEvents = false;
                }
            }
        }

        /// <summary>
        /// Width the search row's own panel occupies before the
        /// source-filter run takes over - the whole row when the run is not
        /// sharing it, or while the window is too narrow to host both.
        /// </summary>
        private int FilterPanelWidth(int panelWidth)
        {
            if (!_sharesSearchRow) return panelWidth;

            return panelWidth < SourceFilterX ? panelWidth : SourceFilterX;
        }

        private static SourceFilterFlowResult Flow(IReadOnlyList<int> cellWidths, int availableWidth)
        {
            return SourceFilterFlowLayout.Layout(
                cellWidths, availableWidth, SourceFilterCellHeight, SourceFilterCellGapX, SourceFilterRowGapY);
        }

        /// <summary>
        /// Flows the source-filter checkboxes at the current width and
        /// re-anchors the search, coin and content rows around the height
        /// and the mode that needs - the one place
        /// <see cref="_sourceFilterHeight"/> and
        /// <see cref="_sharesSearchRow"/> (and therefore CoinRowY/ContentY/
        /// TopRegionHeight) are written.
        /// <para>
        /// The flow pass itself is skipped when none of its inputs moved
        /// (see <see cref="_lastFlowWidth"/>); the rows below are
        /// re-anchored either way, since a height-only resize still moves
        /// the content panel's bottom edge.
        /// </para>
        /// <para>
        /// The run is flowed beside the search box first and re-flowed on
        /// its own full-width row below when that wrapped it - the mode is
        /// the flow's outcome, not an input to it, which is why both modes'
        /// caps are read up front and both are part of the cache key.
        /// </para>
        /// </summary>
        private void ApplyTopRegionLayout()
        {
            int w = _containerWidth;

            if (_sourceFilterPanel != null)
            {
                // Read before the early-out: the caps are height-driven, so
                // a height-only resize can change them, and with them
                // whether the row scrolls (and therefore re-flows narrower).
                int sharedCap = MaxSourceFilterHeight(SearchRowY);
                int ownRowCap = MaxSourceFilterHeight(SearchRowY + SearchRowHeight + SearchToFilterGapY);

                if (w != _lastFlowWidth || sharedCap != _lastFlowSharedCap || ownRowCap != _lastFlowOwnRowCap)
                {
                    // Single read: Build's ThreadPool body swaps this field,
                    // so the count and the indexer below must come from the
                    // same list.
                    var cells = _sourceFilterCells;

                    var widths = new List<int>(cells.Count);
                    foreach (var checkbox in cells)
                    {
                        widths.Add(checkbox.Width);
                    }

                    var placement = SnapshotHeaderLayout.PlaceSourceFilterRun(
                        w, SourceFilterX, SearchRowHeight, SearchToFilterGapY, sharesSearchRow: true);
                    var flow = Flow(widths, placement.Width);

                    // Sharing the search row halves the width the run flows
                    // into; a run that wraps there would hide filters behind
                    // the cap's scrollbar to save 38px of header, so it takes
                    // its own full-width row instead.
                    if (!SnapshotHeaderLayout.SharesSearchRow(flow.RowCount))
                    {
                        placement = SnapshotHeaderLayout.PlaceSourceFilterRun(
                            w, SourceFilterX, SearchRowHeight, SearchToFilterGapY, sharesSearchRow: false);
                        flow = Flow(widths, placement.Width);
                    }

                    int cap = placement.SharesSearchRow ? sharedCap : ownRowCap;
                    int height = SourceFilterTopPad + flow.TotalHeight + SourceFilterBottomPad;

                    // Past the cap the row scrolls rather than growing, so the
                    // cells have to be re-flowed clear of the scrollbar strip -
                    // which can itself wrap one more cell, hence the second pass.
                    bool scroll = height > cap;
                    if (scroll)
                    {
                        flow = Flow(widths, placement.Width - SourceFilterScrollbarAllowance);
                        height = SourceFilterTopPad + flow.TotalHeight + SourceFilterBottomPad;
                    }

                    for (int i = 0; i < cells.Count; i++)
                    {
                        cells[i].Location = new Point(flow.Cells[i].X, SourceFilterTopPad + flow.Cells[i].Y);
                    }

                    if (height < SourceFilterSingleRowHeight)
                    {
                        height = SourceFilterSingleRowHeight;
                    }

                    _sharesSearchRow = placement.SharesSearchRow;
                    _sourceFilterHeight = height < cap ? height : cap;
                    _sourceFilterPanel.CanScroll = scroll;
                    _sourceFilterPanel.Location = new Point(placement.X, SearchRowY + placement.OffsetY);
                    _sourceFilterPanel.Size = new Point(placement.Width, _sourceFilterHeight);

                    _lastFlowWidth = w;
                    _lastFlowSharedCap = sharedCap;
                    _lastFlowOwnRowCap = ownRowCap;
                }
            }

            if (_filterPanel != null)
            {
                // Sized here rather than in OnPanelResized: its width depends
                // on the mode the flow above just resolved.
                _filterPanel.Size = new Point(FilterPanelWidth(w), SearchRowHeight);
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
        /// The Clear Cache button's click flow, behind the same ModalDialog
        /// confirm the Log tab's Delete Log File and the Crafting Plan tab's
        /// regenerate gate use. The confirm is unconditional: clearing
        /// deletes the only copy of the account snapshot, and rebuilding it
        /// requires a reachable GW2 API - the exact condition a user is
        /// often already stuck on when they reach for this button.
        /// <para>
        /// Unlike Delete Log File, the destructive work stays inline on the
        /// main thread: ClearCache is a token cancel plus a single
        /// SnapshotStore.Delete and three field resets under
        /// SnapshotCommitGate's lock - no queue drain, no lock a background
        /// loop can hold - and SetSnapshot/SetStatus below are control
        /// mutations that must run on the main thread anyway.
        /// </para>
        /// <para>
        /// Interposing a dialog opens a window in which a refresh can start,
        /// which the single-click version could not: Refresh Now disables
        /// Clear Cache for its whole duration, but not the reverse. Both
        /// buttons are therefore disabled for the dialog's lifetime, and
        /// because Build() recreates them on every tab visit (which would
        /// re-enable them mid-dialog), the confirm also bumps
        /// _clearGeneration so an already-in-flight refresh drops its own
        /// tail instead of repainting the snapshot the user just discarded.
        /// </para>
        /// </summary>
        private void ConfirmClearCache()
        {
            if (_clearCache == null)
            {
                return;
            }

            bool shown = _modalDialog.Show(
                "Discard the cached account snapshot? It can only be rebuilt when the GW2 API is reachable.",
                () =>
                {
                    _clearConfirmOpen = false;
                    _clearGeneration++;
                    SetSnapshotActionsEnabled(true);
                    _clearCache();
                    SetSnapshot(null);
                    var status = StatusText.Stamp("Cache cleared", DateTime.Now);
                    SetStatus(status);
                    _saveStatus(status);
                },
                () =>
                {
                    // Runs on Cancel AND on the window's own X/Escape, which
                    // is what stops a dismissed dialog from leaving both
                    // buttons dead for the session - see ModalDialog.Dismiss.
                    _clearConfirmOpen = false;
                    SetSnapshotActionsEnabled(true);
                },
                confirmText: "Discard");

            // False means another caller's dialog is already on screen, so
            // this request never opened and nothing must be armed for it.
            if (shown)
            {
                _clearConfirmOpen = true;
                SetSnapshotActionsEnabled(false);
            }
        }

        // Both Snapshot-tab actions move together: each invalidates the
        // other's work. Null-tolerant because Build() may not have run yet,
        // and each button keeps its own "was this action wired at all"
        // condition from Build so re-enabling never revives a dead button.
        private void SetSnapshotActionsEnabled(bool enabled)
        {
            bool allow = enabled && !_clearConfirmOpen;

            if (_clearButton != null)
            {
                _clearButton.Enabled = allow && _clearCache != null;
            }

            if (_refreshButton != null)
            {
                _refreshButton.Enabled = allow && _refreshAsync != null;
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
        /// <para>
        /// A Clear Cache that completes while this fetch is in flight wins:
        /// Module.ClearCache cancels the refresh token and deletes the store
        /// under SnapshotCommitGate, but this continuation is outside that
        /// gate, so the captured _clearGeneration is what keeps it from
        /// repainting a discarded snapshot or overwriting "Cache cleared"
        /// with a cancellation status.
        /// </para>
        /// </summary>
        private async Task RefreshNowAsync()
        {
            if (_refreshAsync == null) return;

            int generation = _clearGeneration;

            SetSnapshotActionsEnabled(false);
            SetRefreshSpinnerVisible(true);
            SetStatus("Refreshing...");

            try
            {
                var snapshot = await _refreshAsync();

                // A Clear Cache landed while this was in flight: the store is
                // already gone and the status already reads "Cache cleared",
                // so neither the persist below nor the repaint after it has
                // anything valid left to say. The finally still re-enables.
                if (_clearGeneration != generation)
                {
                    return;
                }

                string status = snapshot != null
                    ? StatusText.Stamp("Updated", snapshot.CapturedAt.ToLocalTime())
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

                    // Re-checked here, not just before the persist above:
                    // the clear runs on the main thread while this
                    // continuation may still be on a ThreadPool one, so a
                    // Discard can land between the two points.
                    if (_clearGeneration != generation)
                    {
                        return;
                    }

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

                // Module.ClearCache cancels _refreshCts, so a Discard during
                // an in-flight refresh surfaces here as a cancellation. The
                // log line above is kept - the failure did happen - but the
                // user-facing status must stay "Cache cleared", which is
                // what they actually asked for.
                if (_clearGeneration != generation)
                {
                    return;
                }

                var classification = SnapshotFailureClassifier.Classify(ex);
                string cause = StatusText.ForRefreshFailure(classification.Kind, classification.FailedSourceCount, classification.TotalSourceCount);
                var status = StatusText.Stamp(cause, DateTime.Now);
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
                    // Ahead of the teardown guard below on purpose: this
                    // clears a flag a later rebuild reads, and leaving it
                    // set would spin a spinner over a finished refresh.
                    SetRefreshSpinnerVisible(false);
                    if (_headerPanel == null || _headerPanel.Parent == null) return;
                    SetSnapshotActionsEnabled(true);
                });
            }
        }

        /// <summary>
        /// The refresh spinner's single writer. The flag is set even when
        /// the control is gone (module torn down mid-refresh, or Build has
        /// not run yet), so a rebuild that happens between the two calls
        /// still restores the right state - the control write itself is
        /// null-tolerant for the same reason.
        /// </summary>
        private void SetRefreshSpinnerVisible(bool visible)
        {
            _refreshInFlight = visible;
            if (_statusSpinner != null)
            {
                _statusSpinner.Visible = visible;
            }
        }

        /// <summary>
        /// Composes the header status label's text (base status text plus
        /// a staleness-age suffix, e.g. "Updated - Aug 15, 2026 3:41 PM
        /// (snapshot 2m old)" - the suffix names its subject so it cannot
        /// be misread as a restatement of the timestamp beside it, see
        /// StatusText.ForSnapshotAgeSuffix) and recolors it once the
        /// snapshot is older than the
        /// SnapshotRefreshIntervalMinutes setting - the same threshold
        /// Module.Update()'s auto-refresh gate reads, re-read (clamped)
        /// on every call here just like that gate does, so a Settings tab
        /// save is picked up by the next call rather than needing a
        /// rebuild. Called from every place the
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
                string ageText = StatusText.ForSnapshotAgeSuffix(age);
                text = string.IsNullOrEmpty(text) ? ageText : $"{text} ({ageText})";
                var staleThreshold = TimeSpan.FromMinutes(_settings.GetClampedSnapshotRefreshIntervalMinutes());
                _statusLabel.TextColor = StatusText.IsStale(age, staleThreshold) ? WarningTextColor : _defaultStatusColor;
            }
            else
            {
                _statusLabel.TextColor = _defaultStatusColor;
            }

            _statusLabel.Text = text;
            InlineSpinner.PlaceAfter(_statusSpinner, _statusLabel, InlineSpinnerLayout.LabelGap);
        }

        /// <summary>
        /// Arms ONE trailing re-fit per resize drag, however many resize
        /// events that drag produces. Each event only stamps
        /// <see cref="_lastResizeEventTicks"/>; the pending waiter re-arms
        /// itself against that stamp until the drag has been quiet for
        /// <see cref="ResizeSettleMs"/>. Bounded to a single in-flight
        /// waiter by <see cref="_rowRefitPending"/> - the same shape
        /// CraftingPlanView's _resizeSettlePending ticker uses, for the same
        /// reason: a cancel-and-replace timer per drag frame is allocation
        /// and a thrown cancellation exception per frame, on the UI thread's
        /// own event path.
        /// <para>
        /// Main thread only (the Resized handler), which is what makes the
        /// flag a plain bool; the ticks stamp crosses to a ThreadPool thread
        /// and so goes through <see cref="Interlocked"/>.
        /// </para>
        /// </summary>
        private void ScheduleRowRefit()
        {
            Interlocked.Exchange(ref _lastResizeEventTicks, DateTime.UtcNow.Ticks);

            if (_rowRefitPending)
            {
                return;
            }

            _rowRefitPending = true;
            RunRowRefitAfterSettleAsync();
        }

        /// <summary>
        /// Waits out the drag, then marshals <see cref="RefitResultRows"/>
        /// back onto the main thread - Blish HUD's XNA host installs no
        /// SynchronizationContext, so the continuation after
        /// <see cref="Task.Delay"/> may resume on a ThreadPool thread (the
        /// same reason RunSearchDebounceAsync marshals).
        /// </summary>
        private async void RunRowRefitAfterSettleAsync()
        {
            try
            {
                while (true)
                {
                    long elapsedMs =
                        (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastResizeEventTicks)) / TimeSpan.TicksPerMillisecond;
                    if (elapsedMs >= ResizeSettleMs)
                    {
                        break;
                    }

                    // Clamped: a stamp landing between the two reads above
                    // can make this negative, which Task.Delay rejects.
                    int remaining = (int)(ResizeSettleMs - elapsedMs);
                    await Task.Delay(remaining > 0 ? remaining : 1);
                }

                // A dropped queue attempt (overlay gone) would otherwise
                // leave the pending flag set forever and starve every later
                // drag of a re-fit. Cleared from this thread only in that
                // case, when no main-thread work can be racing it.
                if (!MainThreadMarshal.Run(RefitResultRows))
                {
                    _rowRefitPending = false;
                }
            }
            catch (Exception ex)
            {
                // async void: an escaping exception has no caller to reach
                // and would take down the host rather than this one wait.
                _rowRefitPending = false;
                Logger.Warn(ex, "Snapshot row re-fit wait failed");
            }
        }

        /// <summary>
        /// Re-fits the rows already on screen to the content panel's current
        /// width, in place: each row's Panel takes the new width and each of
        /// its text lines is re-ellipsized against it (with its tooltip
        /// re-decided). No search re-run, no dispose-and-recreate, so the
        /// user's scroll position is untouched - the reason a width change
        /// no longer goes through RebuildContent.
        /// </summary>
        private void RefitResultRows()
        {
            _rowRefitPending = false;

            if (_contentPanel == null || _contentPanel.Parent == null)
            {
                return;
            }

            int width = _contentPanel.Width;
            if (width == _lastRowLayoutWidth)
            {
                return;
            }

            try
            {
                foreach (var refit in _rowRefitActions)
                {
                    refit(width);
                }

                _lastRowLayoutWidth = width;
            }
            catch (Exception ex)
            {
                // The registry is cleared by RebuildContent, so its closures
                // outlive their controls only between a fresh Build's
                // ThreadPool body swapping _contentPanel and that build's own
                // marshaled RebuildContent tail. Whichever build is current
                // renders its rows at its own width regardless.
                Logger.Warn(ex, "Snapshot row re-fit skipped");
            }
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

            // The rows about to be disposed own every registered re-fit
            // closure; the fresh ones register their own below. Cleared
            // before the disposal loop so no window exists in which a
            // closure could be replayed against a disposed row.
            _rowRefitActions.Clear();
            _lastRowLayoutWidth = _contentPanel.Width;

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

        // Left edge of a result row's text column (past the 32px icon at
        // x=2) and the gap kept clear of the row's right edge.
        private const int RowTextX = 40;
        private const int RowTextRightPad = 8;

        /// <summary>
        /// One result-row text line, ellipsized to the width the row
        /// actually has and returning whether it had to shorten. Both
        /// lines of an item row used to be AutoSizeWidth Labels inside a
        /// fixed-width Panel, so a multi-character breakdown was hard-clipped
        /// mid-word with nothing to say text had been lost ("...Maximus
        /// Test 10  Chara"). Same EllipsizeToWidth + tooltip pattern the
        /// Log tab's rows and the plan's tables use.
        /// </summary>
        private static Label CreateRowTextLabel(
            Panel rowPanel, string text, int panelWidth, int y, Color? color, out bool shortened)
        {
            var label = new Label()
            {
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(RowTextX, y),
                Parent = rowPanel
            };
            if (color.HasValue)
            {
                label.TextColor = color.Value;
            }

            shortened = FitRowTextLabel(label, text, panelWidth);

            // Both lines of a snapshot row - the item name and the
            // "Character: ..." source caption - carry names nobody here
            // picks the letters of, so both need the descender clearance
            // (field test, bug 5; the shipped build clips the tail off
            // "Green Wood Log").
            return LabelHelpers.WithDescenderClearance(label);
        }

        /// <summary>
        /// Ellipsizes one line to the row width and re-decides its tooltip,
        /// returning whether it had to shorten. The single rule, so the
        /// build-time fit and the resize-time re-fit
        /// (<see cref="RefitResultRows"/>) cannot drift.
        /// <para>
        /// The label is the deepest control under the cursor, and Blish
        /// resolves a tooltip on that control alone - it does not bubble to
        /// the row Panel (the swallowed-hover class recorded for
        /// ShoppingListSectionRenderer and the tree row). The Panel gets one
        /// too, from the caller, for the strip beside the text. A line that
        /// now fits has its tooltip cleared rather than left stale.
        /// </para>
        /// </summary>
        private static bool FitRowTextLabel(Label label, string text, int panelWidth)
        {
            var font = GameService.Content.DefaultFont14;
            string full = text ?? "";
            string shown = LabelHelpers.EllipsizeToWidth(font, full, panelWidth - RowTextX - RowTextRightPad);

            label.Text = shown;

            bool shortened = shown != full;
            TooltipFacility.ApplyPlain(label, shortened ? full : null);
            return shortened;
        }

        /// <summary>
        /// The row strip's own tooltip, carrying whichever of the row's
        /// lines were shortened - one assignment, since a per-line one here
        /// would leave the later assignment silently winning. Cleared when
        /// neither line is shortened.
        /// </summary>
        private static void ApplyRowStripTooltip(
            Panel rowPanel, string first, bool firstShortened, string second, bool secondShortened)
        {
            string text = null;
            if (firstShortened && secondShortened)
            {
                text = first + "\n" + second;
            }
            else if (firstShortened)
            {
                text = first;
            }
            else if (secondShortened)
            {
                text = second;
            }

            TooltipFacility.ApplyPlain(rowPanel, text);
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
            //
            // Quantity PREFIX ("30x Mystic Clover"), matching the recipe
            // tree, the shopping list and Used Materials. This row used to
            // suffix it ("Mystic Clover x30") and the wallet row below used
            // a colon ("Spirit Shards: 50"), so one tab spelled the same
            // fact three ways (audit batch J, M9). Two things are
            // deliberately NOT swept into this: a tabular Amount column,
            // whose header already labels its bare numbers, and the
            // location breakdown below.
            string nameText = $"{row.TotalCount}x {row.Name}";
            var nameLabel = CreateRowTextLabel(rowPanel, nameText, panelWidth, 4, null, out bool nameShortened);

            // NOT the prefix notation the name line above uses, and the one
            // deliberate exemption from M9's sweep beside the tabular Amount
            // columns: these labels are LOCATIONS, not items
            // (SnapshotSearchResultBuilder.FormatSourceLabel returns "Bank",
            // "Material Storage", "Shared Inventory", "Character: <name>").
            // "20x Bank" parses as twenty banks, and "10x Character:
            // Maximus Test" collides the multiplier with the label's own
            // colon. The count follows its location, as it did before.
            string breakdown = row.Breakdown == null || row.Breakdown.Count == 0
                ? ""
                : string.Join("   ", row.Breakdown.Select(b => $"{b.Label} {b.Count}"));

            var breakdownLabel =
                CreateRowTextLabel(rowPanel, breakdown, panelWidth, 24, InfoTextColor, out bool breakdownShortened);

            ApplyRowStripTooltip(rowPanel, nameText, nameShortened, breakdown, breakdownShortened);

            _rowRefitActions.Add(w =>
            {
                rowPanel.Size = new Point(w, ItemRowHeight);
                bool nameNowShortened = FitRowTextLabel(nameLabel, nameText, w);
                bool breakdownNowShortened = FitRowTextLabel(breakdownLabel, breakdown, w);
                ApplyRowStripTooltip(rowPanel, nameText, nameNowShortened, breakdown, breakdownNowShortened);
            });
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
            // Quantity prefix, matching CreateItemRow above and the plan's
            // own tables - this row used to be the module's only
            // "Name: value" colon form (audit batch J, M9). The thousands
            // separator stays: wallet balances run to seven figures where
            // an item count does not.
            string name = string.IsNullOrEmpty(entry.CurrencyName) ? "Unknown Currency" : entry.CurrencyName;
            string text = $"{entry.Value:N0}x {name}";
            var label = CreateRowTextLabel(rowPanel, text, panelWidth, 6, null, out bool shortened);
            TooltipFacility.ApplyPlain(rowPanel, shortened ? text : null);

            _rowRefitActions.Add(w =>
            {
                rowPanel.Size = new Point(w, WalletRowHeight);
                bool nowShortened = FitRowTextLabel(label, text, w);
                TooltipFacility.ApplyPlain(rowPanel, nowShortened ? text : null);
            });
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
        // The dim "Coin" caption is built here rather than once at
        // construction because this method disposes every child of
        // _coinPanel on each refresh; a caption parked outside that cycle
        // would be destroyed by the first snapshot update.
        private void UpdateCoinDisplay(int copper)
        {
            if (_coinPanel == null) return;

            foreach (var child in _coinPanel.Children.ToArray())
            {
                child.Dispose();
            }

            var (gold, silver, cop) = CoinSegmentMath.Split(copper);

            var font = GameService.Content.DefaultFont14;
            var captionFont = GameService.Content.DefaultFont12;
            new Label()
            {
                Text = CoinCaption,
                Font = captionFont,
                TextColor = CoinCaptionColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 4),
                Parent = _coinPanel
            };
            int captionWidth = (int)Math.Ceiling(captionFont.MeasureString(CoinCaption).Width);

            var segments = new List<CoinSegmentMath.CoinSegmentSpec>(3);
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, 156904, gold.ToString());
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, 156907, silver.ToString());
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, 156902, cop.ToString());

            CoinCurrencyRenderer.LayoutCoinSegments(
                _coinPanel, segments, captionWidth + CoinCaptionGap, 2, font);
        }
    }
}
