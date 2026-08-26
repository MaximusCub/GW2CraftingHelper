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

        private readonly Func<int, ItemStatBlock> _getItemStatBlock;
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

        private readonly List<ResultCell> _itemCells = new List<ResultCell>();
        private readonly List<ResultCell> _walletCells = new List<ResultCell>();

        // In the SEARCH's order, never the sorted one.
        private readonly List<SnapshotSearchRow> _itemRows = new List<SnapshotSearchRow>();
        private readonly List<SnapshotWalletEntry> _walletRows = new List<SnapshotWalletEntry>();

        // Display position i shows cell [order[i]]; null is the identity.
        private IReadOnlyList<int> _itemOrder;
        private IReadOnlyList<int> _walletOrder;
        // The width the rows on screen were actually laid out at, so a drag
        // that ends where it started repacks nothing.
        private int _lastRowLayoutWidth = -1;

        // Width-driven repack of the rows already on screen. Deliberately
        // NOT the search debounce above: that one is cancel-and-replace, so
        // routing a resize drag through it allocated a
        // CancellationTokenSource and threw a cancellation exception per
        // drag FRAME, and its callback disposes and rebuilds every row
        // (re-running the search, and putting the scroll position at risk)
        // to change nothing but the cells' placement and the text that no
        // longer fits.
        private readonly ResizeSettleDebounce _rowRefitSettle;

        // Layout constants
        private const int HeaderRowY = 5;

        // The section-header band every other heading in the module draws:
        // 38 tall with its 2px rule at 35, rather than the 40px band with a
        // flush rule this tab had of its own.
        private const int HeaderHeight = PlanContentHeightMath.SectionHeaderRowHeight;
        private const int HeaderTitleY = PlanContentHeightMath.SectionHeaderTitleY;

        // Vertically centred in the header band, derived rather than written
        // down - and it clears the rule beneath it by two.
        private const int HeaderButtonY = (HeaderHeight - UiMetrics.ButtonHeight) / 2;

        /// <summary>Left gutter every element on this tab starts at.</summary>
        private const int Inset = SnapshotHeaderLayout.Inset;

        private const int HeaderButtonWidth = 100;

        // Room the inline spinner trailing the status line needs, so a long
        // status ellipsizes before it reaches the spinner rather than under
        // it.
        private const int StatusSpinnerReserve =
            InlineSpinnerLayout.SnapshotStatusSize + InlineSpinnerLayout.LabelGap;

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

        private const int SourceFilterScrollbarAllowance = WindowSizing.ScrollbarAllowance;
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
        private const int SearchBoxHeight = 26;
        private const int FilterDropdownWidth = 140;
        private const int FilterDropdownHeight = 30;
        private const int FilterDropdownX = Inset + SearchBoxWidth + 10;

        // Left x of the source-filter run, clear of the dropdown. The
        // panel itself carries this offset, so SourceFilterFlowLayout keeps
        // placing cells from 0 in the panel's own coordinates and only its
        // available width changes.
        private const int SourceFilterX = FilterDropdownX + FilterDropdownWidth + 20;

        // Dim caption ahead of the wallet coin total, so the row reads as a
        // labelled figure rather than a stray unlabelled list row.
        private Panel _coinBlockPanel;
        private Label _resultLineLabel;
        private int _coinBlockWidth;
        private string _resultLineText = "";

        private const string CoinCaption = "Coin";
        private const int CoinCaptionGap = 8;
        private static readonly Color CoinCaptionColor = new Color(130, 130, 130);

        // Same rule under every section heading in the module - see
        // SettingsTabContent's own AddSectionHeader.
        private static readonly Color SectionDividerColor = new Color(130, 130, 130);

        // PlanContentHeightMath's own heights, not re-derived here.
        private const int SectionTitleBandHeight = PlanContentHeightMath.SectionHeaderRowHeight;
        private const int SectionTitleTextY = PlanContentHeightMath.SectionHeaderTitleY;

        // The treatment the plan tables give a quantity column.
        private static readonly Color AmountTextColor = new Color(200, 200, 200);

        // 56, not 52. An item cell stacks a name line at y=4 and a
        // breakdown line under it; at Font16 the name's line box ends at
        // y=24, so the breakdown moved from y=24 to y=26 and its lowest ink
        // from y=43 to y=47. 56 keeps the 9px of bottom slack the 52px cell
        // had. The wallet cell is unchanged: it is ICON-driven (a 32px icon
        // at y=2 plus 2), and its single Font16 line's ink (y=27) still sits
        // well inside 36.
        private const int ItemRowHeight = 56;
        private const int WalletRowHeight = 36;

        // UI controls (stored for resize handler)
        private Panel _headerPanel;
        private Panel _headerDivider;
        private Panel _statusPanel;
        private Panel _filterPanel;
        private Panel _sourceFilterPanel;
        private FlowPanel _contentPanel;

        // The result grid: ONE fixed-height panel inside the scrolling
        // content panel, holding the item run and then the wallet run
        // beneath it. The two runs keep their own cell heights
        // (ItemRowHeight vs WalletRowHeight) - a single uniform height would
        // have to stretch every wallet row to the taller of the two - but
        // they are laid out as two sections of one panel rather than two
        // sibling panels, so the wallet run's position is this file's own
        // arithmetic (LayoutResultGrid) and not a bet on Blish's FlowPanel
        // re-flowing a later sibling when an earlier one changes height.
        // Null until the first rebuild, and whenever the result set is empty
        // enough to render a message instead.
        private Panel _resultGridPanel;

        // Null for a run with no rows: the section is absent, not empty.
        private SectionChrome _itemChrome;
        private SectionChrome _walletChrome;

        // Session-sticky like the search text. One state per run: sorting
        // the items must not disturb the currencies beneath them.
        private readonly TableSortState<SnapshotTableColumn> _itemSortState =
            new TableSortState<SnapshotTableColumn>();

        private readonly TableSortState<SnapshotTableColumn> _walletSortState =
            new TableSortState<SnapshotTableColumn>();

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

        // The same, for Module's timer-driven auto-refresh. Two independent
        // flags rather than one shared one because the two paths have
        // independent lifetimes as far as THIS class can see: a Refresh Now
        // clicked while an auto-refresh is already running returns null
        // immediately (Module.UserRefreshAsync's own _refreshInProgress
        // gate) and runs its finally straight away, so a single flag would
        // let that no-op click switch the running auto-refresh's spinner
        // off. The spinner shows the OR of the two - see
        // ApplySpinnerVisibility.
        private bool _backgroundRefreshInFlight;
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
            Action<string> saveStatusThreadSafe,
            // Session item-stat lookup (ItemMetadataService's own cache),
            // for the Snapshot result rows' item tooltips. Optional, and a
            // pure cache read - a snapshot row whose item no plan has
            // fetched this session degrades to the ellipsis tooltip it
            // always had. See KNOWN-ISSUES #42.
            Func<int, ItemStatBlock> getItemStatBlock = null)
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
            _getItemStatBlock = getItemStatBlock;

            _rowRefitSettle = new ResizeSettleDebounce(
                RefitResultRows,
                MainThreadMarshal.Run,
                ResizeSettleDebounce.DefaultSettleMs,
                ex =>
                {
                    Logger.Warn(ex, "Snapshot row re-fit wait failed");
                    ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot",
                        $"Snapshot row re-fit wait failed: {ex.GetType().Name} - {ex.Message}");
                });
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
                Parent = buildPanel,
            };

            new Label()
            {
                Font = UiFonts.SectionTitle,
                Text = "Account Snapshot",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(Inset, HeaderTitleY),
                Parent = _headerPanel,
            };

            // Bottom-anchored with 1px clearance, like every other section
            // header in the module - see LabelHelpers.CreateRowDivider for
            // why flush anchoring is unsafe. It stops at the tab's one right
            // edge rather than the container's.
            _headerDivider = new Panel()
            {
                Size = new Point(SnapshotHeaderLayout.ChromeRightEdge(w), 2),
                Location = new Point(0, HeaderHeight - 3),
                BackgroundColor = SectionDividerColor,
                Parent = _headerPanel,
            };

            _clearButton = new FeedbackButton()
            {
                Text = "Clear Cache",
                Size = new Point(HeaderButtonWidth, UiMetrics.ButtonHeight),
                Location = new Point(Inset, HeaderButtonY),
                Parent = _headerPanel,
                Enabled = _clearCache != null,
            };
            TooltipFacility.ApplyPlain(
                _clearButton,
                "Discard the cached account snapshot. It can only be rebuilt when the GW2 API is reachable.");

            _refreshButton = new FeedbackButton()
            {
                Text = "Refresh Now",
                Size = new Point(HeaderButtonWidth, UiMetrics.ButtonHeight),
                Location = new Point(Inset, HeaderButtonY),
                Parent = _headerPanel,
                Enabled = _refreshAsync != null,
            };
            LayoutHeaderRow(w);

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
                Parent = buildPanel,
            };

            // Explicit width, not AutoSizeWidth: a long failure string ran
            // off the panel with nothing to say it had. Y=2, the coin row's
            // precedent; StatusRowHeight carries the Status tier's clearance
            // derivation.
            _statusLabel = new Label()
            {
                Font = UiFonts.Status,
                Text = "",
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Location = new Point(Inset, 2),
                Parent = _statusPanel,
            };
            _statusBudget = SnapshotHeaderLayout.StatusMaxWidth(w, StatusSpinnerReserve);

            // Trails the status text for the whole of a refresh, clicked or
            // automatic. A tab switch rebuilds this row while the refresh is
            // still running, so its visibility comes from the flags rather
            // than defaulting to hidden - otherwise the returning user sees
            // "Refreshing..." with nothing turning.
            _statusSpinner = InlineSpinner.Create(_statusPanel, InlineSpinnerLayout.SnapshotStatusSize);
            _statusSpinner.Visible = _refreshInFlight || _backgroundRefreshInFlight;
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
                Parent = buildPanel,
            };

            _searchBox = new TextBox()
            {
                Size = new Point(SearchBoxWidth, SearchBoxHeight),
                Location = new Point(
                    Inset, PlanRelayoutMath.CenterX(SearchRowHeight, SearchBoxHeight)),
                PlaceholderText = "Search items, currencies, characters...",
                Text = _lastSearchText ?? "",
                Parent = _filterPanel,
            }.ReleaseOnDispose().ReleaseOnEnter();
            _searchBox.TextChanged += (_, __) =>
            {
                _lastSearchText = _searchBox.Text ?? "";
                ScheduleSearchRebuild();
            };

            _filterDropdown = new Dropdown()
            {
                Size = new Point(FilterDropdownWidth, FilterDropdownHeight),
                Location = new Point(
                    FilterDropdownX,
                    PlanRelayoutMath.CenterX(SearchRowHeight, FilterDropdownHeight)),
                Parent = _filterPanel,
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
                Parent = buildPanel,
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
                Parent = buildPanel,
            };

            // The coin row was a caption and ~150px of coin run left-packed
            // at x=0, with the rest of the band empty. It is a justified
            // summary row now: what the list is showing on the left, the
            // wallet's coin on the right.
            _resultLineLabel = new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(Inset, 2),
                Parent = _coinPanel,
            };

            // Its own child panel so UpdateCoinDisplay's dispose-and-rebuild
            // cannot destroy the result line beside it.
            _coinBlockPanel = new Panel()
            {
                Size = new Point(0, CoinHeight),
                Location = new Point(0, 0),
                Parent = _coinPanel,
            };

            // Scrollable content
            _contentPanel = new FlowPanel()
            {
                Size = new Point(w, buildPanel.ContentRegion.Height - TopRegionHeight),
                Location = new Point(0, ContentY),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = buildPanel,
            };

            // Subscribe to resize
            buildPanel.Resized += OnPanelResized;

            // Build() runs on a ThreadPool thread (docs/ARCHITECTURE.md
            // section 1), and this instance is never recreated: Module.cs
            // creates ONE MainView and Module.Update() calls SetSnapshot()/
            // SetStatus() on it on the main thread every tick a background
            // refresh completes, whatever tab is selected. Both paths reach
            // UpdateCoinDisplay/ApplyStatusDisplay/RebuildContent, which
            // dispose-then-add into _coinPanel/_contentPanel Children - a
            // compound sequence Blish's own per-call Children lock does not
            // cover, so two interleaved rebuilds can each finish disposing
            // before either adds, and both survive (KNOWN-ISSUES #36, and
            // docs/ARCHITECTURE.md section 1 for the decompiled evidence).
            // The same tail also cancels+disposes _searchDebounceCts, which
            // ScheduleSearchRebuild/RebuildContent write from the main
            // thread; whichever Cancel() landed second on a disposed source
            // would throw ObjectDisposedException. Marshaling the whole tail
            // serializes it against both.
            //
            // This does NOT make MainView main-thread-only: the control
            // fields are still published by the rest of Build()'s body on
            // the ThreadPool thread, and the click handlers wired mid-body
            // become live before Build() finishes - which is survivable only
            // because every downstream call null-guards the field it
            // touches. UpdateCoinDisplay is called here, rather than right
            // after _coinPanel is created, so all three calls land in the
            // same queued callback as the _searchDebounceCts cleanup.
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
                if (_headerPanel == null || _headerPanel.Parent == null)
                {
                    return;
                }

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
            LayoutHeaderRow(w);
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
                _rowRefitSettle.Schedule();
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
            if (_sourceFilterPanel == null)
            {
                return;
            }

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
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                {
                    return false;
                }
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
                Parent = _sourceFilterPanel,
            };

            checkbox.CheckedChanged += (_, __) =>
            {
                if (_suppressSourceFilterEvents)
                {
                    return;
                }

                onChanged(checkbox.Checked);
                RebuildContent();
            };

            _sourceFilterCells.Add(checkbox);
            return checkbox;
        }

        private static int MeasureCheckboxWidth(string text)
        {
            // Caption, not Body: Blish_HUD.Controls.Checkbox draws its label
            // in DefaultFont14 and exposes no Font seam to change that, so
            // measuring in Body would reserve ~11% more width than the
            // control ever paints. See UiFonts' note on the exclusions.
            var font = UiFonts.Caption;
            int textWidth = (int)Math.Ceiling(font.MeasureString(text ?? "").Width);
            return textWidth + CheckboxChromeWidth;
        }

        private bool AllCharactersChecked()
        {
            foreach (string name in _characterNames)
            {
                if (_uncheckedCharacters.Contains(name))
                {
                    return false;
                }
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
            if (!_sharesSearchRow)
            {
                return panelWidth;
            }

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
                LayoutCoinRow(w);
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
            if (_refreshAsync == null)
            {
                return;
            }

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
                    if (_headerPanel == null || _headerPanel.Parent == null)
                    {
                        return;
                    }

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
                    if (_headerPanel == null || _headerPanel.Parent == null)
                    {
                        return;
                    }

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
                    if (_headerPanel == null || _headerPanel.Parent == null)
                    {
                        return;
                    }

                    SetSnapshotActionsEnabled(true);
                });
            }
        }

        /// <summary>
        /// The clicked-Refresh-Now half of the spinner switch.
        /// </summary>
        private void SetRefreshSpinnerVisible(bool visible)
        {
            _refreshInFlight = visible;
            ApplySpinnerVisibility();
        }

        /// <summary>
        /// The auto-refresh half of the same switch, called by Module's
        /// Update() drain (main thread) when its background refresh starts
        /// and again when it ends - see
        /// <see cref="_backgroundRefreshInFlight"/> for why this is a second
        /// flag and not a second writer of the first one.
        /// <para>
        /// Deliberately spinner-only: it does not touch the status TEXT the
        /// way a clicked Refresh Now does. The user did not ask for this
        /// refresh, so replacing the timestamp they are reading with
        /// "Refreshing..." is a surprise rather than feedback - and the
        /// background path's cancellation arm writes no status at all, so a
        /// label overwritten here would have nothing to restore it.
        /// </para>
        /// </summary>
        public void SetBackgroundRefreshInFlight(bool inFlight)
        {
            _backgroundRefreshInFlight = inFlight;
            ApplySpinnerVisibility();
        }

        /// <summary>
        /// One writer for the control, from both flags. Null-tolerant: the
        /// flags are still updated when the control is gone (module torn
        /// down mid-refresh, or Build has not run yet) so a rebuild between
        /// the start and end calls restores the right state.
        /// </summary>
        private void ApplySpinnerVisibility()
        {
            if (_statusSpinner != null)
            {
                _statusSpinner.Visible = _refreshInFlight || _backgroundRefreshInFlight;
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
            if (_statusLabel == null)
            {
                return;
            }

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

            _statusFullText = text;
            ApplyStatusText();
        }

        // The status line's full text, so a resize re-takes the ellipsis
        // from the original rather than compounding it onto an
        // already-shortened string.
        private string _statusFullText = "";

        // Budget the line ellipsizes against. Held separately from
        // Label.Width, which stays the width of the TEXT: the inline spinner
        // is placed after the label's right edge, so a label sized to the
        // whole budget would strand the spinner at the panel's edge.
        private int _statusBudget;

        private void ApplyStatusText()
        {
            if (_statusLabel == null)
            {
                return;
            }

            var font = UiFonts.Status;
            string shown = LabelHelpers.EllipsizeToWidth(font, _statusFullText, _statusBudget);
            if (!string.Equals(_statusLabel.Text, shown, StringComparison.Ordinal))
            {
                _statusLabel.Text = shown;
            }

            _statusLabel.Width = (int)Math.Ceiling(font.MeasureString(shown).Width);
            TooltipFacility.ApplyPlain(
                _statusLabel,
                string.Equals(shown, _statusFullText, StringComparison.Ordinal) ? null : _statusFullText);

            InlineSpinner.PlaceAfter(_statusSpinner, _statusLabel, InlineSpinnerLayout.LabelGap);
        }

        /// <summary>
        /// Repacks the rows already on screen against the content panel's
        /// current width, in place: the grid is recomputed (a wider window
        /// can gain a column, a narrower one drop back to the single-column
        /// fallback), every cell moves to its new slot, and each of its text
        /// lines is re-ellipsized against the new COLUMN width - not the
        /// panel width - with its amount re-pinned. Text and position only:
        /// an item row's tooltips are stamped once at build and say the same
        /// at any width, and the one exception is the wallet row, whose
        /// plain note exists only where the name had to shorten and so is
        /// re-decided here (StampWalletRowTooltip). No search re-run and no
        /// dispose-and-recreate, which is why a width change no longer goes
        /// through RebuildContent.
        /// <para>
        /// The scroll position survives a repack that KEEPS the column count
        /// - the grid panel's width moves, its height does not. A repack that
        /// changes the column count writes a new grid-panel height, and
        /// Blish's Scrollbar zeroes the scroll position a frame after any
        /// content-height change (measured: KNOWN-ISSUES #55, "The grid panel holds its
        /// unfiltered height"), so the list snaps to top.
        /// Not defended against here: the tab has no scroll-restore
        /// machinery (CraftingPlanView.PreserveScrollAcross is the module's
        /// only one), and a column-count change re-flows every row anyway, so
        /// there is no old position left to hold.
        /// </para>
        /// </summary>
        private void RefitResultRows()
        {
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
                LayoutResultGrid(refitText: true);
                _lastRowLayoutWidth = width;
            }
            catch (Exception ex)
            {
                // The cell lists are cleared by RebuildContent, so their
                // closures outlive their controls only between a fresh
                // Build's ThreadPool body swapping _contentPanel and that
                // build's own marshaled RebuildContent tail. Whichever build
                // is current renders its rows at its own width regardless.
                Logger.Warn(ex, "Snapshot row re-fit skipped");
            }
        }

        /// <summary>
        /// The single writer for the result grid's geometry: places the item
        /// run and then the wallet run beneath it, both in reading order at
        /// the current column count, and gives the grid panel exactly the
        /// height the two need (nothing here auto-sizes, and a short panel
        /// would clip its own last row). Shared by the rebuild path - which
        /// has just created the cells at this same column width, hence
        /// <paramref name="refitText"/> false there - and the resize repack,
        /// which must also re-ellipsize.
        /// <para>
        /// HOW OFTEN, since the plan tab differs and they share
        /// <see cref="HeaderCellPlan"/>: three callers, and the only one a
        /// drag reaches (<see cref="RefitResultRows"/>) is trailing-
        /// debounced. Once per drag, not once per frame of one.
        /// </para>
        /// </summary>
        private void LayoutResultGrid(bool refitText)
        {
            // Parent-null means the panel this field points at was disposed
            // by a fresh Build cycle whose own RebuildContent tail has not
            // landed yet - the window in which a resize would otherwise
            // repack the PREVIOUS cycle's dead controls. RefitResultRows'
            // catch would survive that; this simply never enters it.
            if (_contentPanel == null || _resultGridPanel == null || _resultGridPanel.Parent == null)
            {
                return;
            }

            int gridWidth = SnapshotItemGridLayout.ComputeGridWidth(_contentPanel.Width);

            var layout = SnapshotResultLayout.Compute(
                _itemCells.Count, _walletCells.Count, gridWidth, ItemRowHeight, WalletRowHeight,
                SectionTitleBandHeight, PlanContentHeightMath.ColumnHeaderRowHeight);

            _resultGridPanel.Size = new Point(gridWidth, layout.TotalHeight);

            LayoutSectionChrome(_itemChrome, layout.Items, gridWidth);
            LayoutSectionChrome(_walletChrome, layout.Wallet, gridWidth);

            PlaceCells(_itemCells, _itemOrder, layout.Items.Grid, ItemRowHeight, refitText);
            PlaceCells(_walletCells, _walletOrder, layout.Wallet.Grid, WalletRowHeight, refitText);
        }

        /// <summary>Places one run's cells. They are held in the search's
        /// order and <paramref name="order"/> is the sort over them, so a
        /// click moves controls instead of recreating them.</summary>
        private static void PlaceCells(
            List<ResultCell> cells, IReadOnlyList<int> order,
            SnapshotItemGridLayout.Grid grid, int rowHeight, bool refitText)
        {
            bool ordered = order != null && order.Count == cells.Count;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[ordered ? order[i] : i];
                cell.Panel.Location = new Point(grid.Cells[i].X, grid.Cells[i].Y);
                cell.Panel.Size = new Point(grid.ColumnWidth, rowHeight);

                if (refitText)
                {
                    cell.Fit(grid.ColumnWidth);
                }
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
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (_contentPanel == null || _contentPanel.Parent == null)
                {
                    return;
                }

                RebuildContent();
            });
        }

        private void RebuildContent()
        {
            if (_contentPanel == null)
            {
                return;
            }

            // Whatever triggered this rebuild (an explicit checkbox/
            // dropdown click, or the debounced search callback itself)
            // supersedes any older still-pending debounced rebuild - avoids
            // a redundant extra rebuild landing a moment after this one.
            CancelSearchDebounce();

            // The rows about to be disposed own every registered cell and
            // its re-fit closure; the fresh ones register their own below.
            // Cleared before the disposal loop so no window exists in which
            // a closure could be replayed against a disposed row - and the
            // grid panel is dropped with them, since the disposal loop below
            // destroys the very panel that field points at.
            _itemCells.Clear();
            _walletCells.Clear();
            _itemRows.Clear();
            _walletRows.Clear();
            _itemOrder = null;
            _walletOrder = null;
            _resultGridPanel = null;
            _itemChrome = null;
            _walletChrome = null;
            _lastRowLayoutWidth = _contentPanel.Width;

            foreach (var child in _contentPanel.Children.ToArray())
            {
                child.Dispose();
            }

            if (_snapshot == null)
            {
                SetResultLine(0, 0, 0, 0);
                new Label()
                {
                    Font = UiFonts.Body,
                    Text = "No snapshot available. Click Refresh Now.",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(8, 8),
                    Parent = _contentPanel,
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
                    UncheckedCharacters = new HashSet<string>(_uncheckedCharacters, StringComparer.Ordinal),
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

            // Counts only, and only for the runs the content-type dropdown
            // admits: a "0 of 1204 items" clause under a Wallet-only filter
            // would report a filter the user chose as if it were a result.
            SetResultLine(
                itemRows?.Count ?? 0,
                itemRows == null ? 0 : (_itemsById?.Count ?? 0),
                walletRows?.Count ?? 0,
                walletRows == null ? 0 : (_snapshot.Wallet?.Count ?? 0));

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

                    // Only reachable on the items side: character-name
                    // matching does not exist for the Wallet filter above,
                    // so the hint would be an offer this tab cannot keep.
                    string hint = SnapshotSearchResultBuilder.ShortQueryCharacterHint(
                        trimmedSearch, _characterNames, _uncheckedCharacters);
                    if (hint != null)
                    {
                        message += "\n" + hint;
                    }
                }

                new Label()
                {
                    Font = UiFonts.Body,
                    Text = message,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(8, 8),
                    TextColor = InfoTextColor,
                    Parent = _contentPanel,
                };
                return;
            }

            // Both runs are laid out in the width the content panel has left
            // once its scrollbar is accounted for, so the rightmost column
            // ellipsizes before it runs under the bar rather than behind it.
            // A panel too narrow for two columns gets the single-column
            // fallback, which is the list this tab shipped with.
            int gridWidth = SnapshotItemGridLayout.ComputeGridWidth(_contentPanel.Width);
            int columnWidth = SnapshotItemGridLayout.ComputeColumnWidth(gridWidth);

            _resultGridPanel = new Panel()
            {
                Size = new Point(gridWidth, 0),
                Parent = _contentPanel,
            };

            _itemChrome = anyItemRows ? CreateSectionChrome("Items", "Item", _itemSortState) : null;
            _walletChrome = anyWalletRows ? CreateSectionChrome("Currencies", "Currency", _walletSortState) : null;

            // Data-derived and width-invariant, so measured once here; the
            // header label it is floored at is what a sort click moves.
            if (_itemChrome != null)
            {
                _itemRows.AddRange(itemRows);
                _itemChrome.ReapplyOrder =
                    () => _itemOrder = SnapshotTableSorter.ItemOrder(_itemRows, _itemSortState);
                _itemChrome.WidestAmount = MeasureWidestAmount(itemRows, r => AmountText(r.TotalCount));
                _itemChrome.RefreshHeaders();
                foreach (var row in itemRows)
                {
                    CreateItemRow(row, columnWidth, _itemChrome);
                }

                _itemChrome.ReapplyOrder();
            }

            // After the item cells, and laid out below them by
            // LayoutResultGrid - the order the single-column list had.
            if (_walletChrome != null)
            {
                _walletRows.AddRange(walletRows);
                _walletChrome.ReapplyOrder =
                    () => _walletOrder = SnapshotTableSorter.WalletOrder(_walletRows, _walletSortState);
                _walletChrome.WidestAmount = MeasureWidestAmount(walletRows, e => AmountText(e.Value));
                _walletChrome.RefreshHeaders();
                foreach (var entry in walletRows)
                {
                    CreateWalletRow(entry, columnWidth, _walletChrome);
                }

                _walletChrome.ReapplyOrder();
            }

            // Places the cells the two loops just created and gives the grid
            // panel its height. refitText: false - every cell was built at
            // this same columnWidth, and re-ellipsizing each label a second
            // time would double the MeasureString work of a rebuild that
            // already runs once per pause in typing over a list that can
            // reach into the thousands of rows.
            LayoutResultGrid(refitText: false);
        }

        /// <summary>Amount column text: the module's "30x" quantity
        /// spelling, thousands separator kept because a wallet balance runs
        /// to seven figures where an item count does not.</summary>
        private static string AmountText(int amount)
        {
            return amount.ToString("N0", CultureInfo.CurrentCulture) + "x";
        }

        /// <summary>The widest amount one run renders: data-derived, so
        /// measured once per rebuild. The header label it is floored
        /// against is the band's only other term.</summary>
        private static int MeasureWidestAmount<T>(IReadOnlyList<T> rows, Func<T, string> amountOf)
        {
            var font = UiFonts.Body;
            int widest = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                int width = (int)Math.Ceiling(font.MeasureString(amountOf(rows[i])).Width);
                if (width > widest)
                {
                    widest = width;
                }
            }

            return widest;
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

        // Left edge of a row's text column, past the icon at x=2. It lives
        // in SnapshotItemGridLayout with every other edge in the cell.
        private const int RowTextX = SnapshotItemGridLayout.CellTextX;

        /// <summary>One run's chrome: its section title with the rule under
        /// it, and the sortable header band above its cells. The band spans
        /// the whole grid and carries ONE label pair per column - the
        /// alternative labels columns two and three with nothing.</summary>
        private sealed class SectionChrome
        {
            public Panel TitlePanel;
            public Panel TitleDivider;
            public Panel HeaderPanel;

            /// <summary>The name column's header title, without its sort
            /// indicator - "Item" or "Currency".</summary>
            public string NameTitle;

            public TableSortState<SnapshotTableColumn> Sort;

            /// <summary>Widest amount this run renders - see
            /// MeasureWidestAmount.</summary>
            public int WidestAmount;

            /// <summary>Width the Amount column reserves: the widest amount
            /// floored at its header label. Read live by every cell's re-fit
            /// closure - the sort indicator moves the floor.</summary>
            public int AmountBand;

            public readonly List<Label> NameHeaders = new List<Label>();
            public readonly List<Label> AmountHeaders = new List<Label>();

            /// <summary>Cycles this run's sort and re-places its cells.</summary>
            public Action<SnapshotTableColumn> SortBy;

            /// <summary>Re-derives this run's placement order - the one
            /// place that knows which run a chrome belongs to.</summary>
            public Action ReapplyOrder;

            /// <summary>Per-column click actions, built once, so a
            /// re-describe never allocates a closure per column.</summary>
            public Action SortByName;
            public Action SortByAmount;

            /// <summary>The band's hover/click cells, re-described whenever
            /// the grid's column count or width changes.</summary>
            public SortableHeaderCells Cells;

            /// <summary>The cell split over those labels. Rebuilt only when
            /// the column count or a header's width changes; Sync is what a
            /// re-layout runs.</summary>
            public HeaderCellPlan CellPlan;
            public int PlanColumns;

            /// <summary>Header text, and everything measured from it. Fixed
            /// between sort clicks - the indicator is the only part that
            /// moves - so a re-layout measures no string.</summary>
            public string NameText;
            public string AmountText;
            public int NameWidth;
            public int AmountWidth;

            /// <summary>
            /// Re-resolves both header labels against the sort state and
            /// re-floors the Amount band on the new label width. The cell
            /// plan caches those widths, so it is dropped, not patched.
            /// </summary>
            public void RefreshHeaders()
            {
                var font = TableHeaderStyle.Font;
                NameText = SortableHeaderLabel.Decorate(
                    NameTitle, Sort.IndicatorFor(SnapshotTableColumn.Name));
                AmountText = SortableHeaderLabel.Decorate(
                    AmountHeaderTitle, Sort.IndicatorFor(SnapshotTableColumn.Amount));
                NameWidth = (int)Math.Ceiling(font.MeasureString(NameText).Width);
                AmountWidth = (int)Math.Ceiling(font.MeasureString(AmountText).Width);
                AmountBand = SnapshotItemGridLayout.CellAmountBandWidth(WidestAmount, AmountWidth);

                for (int i = 0; i < NameHeaders.Count; i++)
                {
                    NameHeaders[i].Text = NameText;
                    AmountHeaders[i].Text = AmountText;
                }

                CellPlan = null;
                PlanColumns = 0;
            }

            public const string AmountHeaderTitle = "Amount";
        }

        /// <summary>
        /// Builds one run's title band and its (empty) header band; the
        /// per-column labels come from <see cref="LayoutSectionChrome"/>,
        /// the only place that knows the resolved column count.
        /// </summary>
        private SectionChrome CreateSectionChrome(
            string title, string nameTitle, TableSortState<SnapshotTableColumn> sort)
        {
            var chrome = new SectionChrome { NameTitle = nameTitle, Sort = sort };

            chrome.TitlePanel = new Panel()
            {
                Size = new Point(0, SectionTitleBandHeight),
                Parent = _resultGridPanel,
            };
            new Label()
            {
                Font = UiFonts.SectionTitle,
                Text = title,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, SectionTitleTextY),
                Parent = chrome.TitlePanel,
            };
            chrome.TitleDivider = new Panel()
            {
                Size = new Point(0, 2),
                Location = new Point(0, SectionTitleBandHeight - 3),
                BackgroundColor = SectionDividerColor,
                Parent = chrome.TitlePanel,
            };

            chrome.HeaderPanel = new Panel()
            {
                Size = new Point(0, PlanContentHeightMath.ColumnHeaderRowHeight),
                BackgroundColor = TableHeaderStyle.BandColor,
                Parent = _resultGridPanel,
            };

            chrome.Cells = new SortableHeaderCells(chrome.HeaderPanel);
            chrome.SortBy = column => SortSection(chrome, column);
            chrome.SortByName = () => chrome.SortBy(SnapshotTableColumn.Name);
            chrome.SortByAmount = () => chrome.SortBy(SnapshotTableColumn.Amount);
            chrome.RefreshHeaders();

            return chrome;
        }

        /// <summary>Applies a header click: the sort state cycles, the
        /// headers re-resolve, the cells are RE-PLACED. Nothing is
        /// re-queried or disposed, so the row count and grid height are
        /// identical across it - which is why neither the scroll offset nor
        /// the hover chain needs defending here.</summary>
        private void SortSection(SectionChrome chrome, SnapshotTableColumn column)
        {
            // A chrome with no order to reapply was never handed its rows.
            if (chrome?.ReapplyOrder == null)
            {
                return;
            }

            if (_resultGridPanel == null || _resultGridPanel.Parent == null)
            {
                return;
            }

            chrome.Sort.Cycle(column);

            // The indicator changes the label's width, which floors the
            // Amount band - the only reason a click re-ellipsizes.
            int bandBefore = chrome.AmountBand;
            chrome.RefreshHeaders();

            chrome.ReapplyOrder();

            LayoutResultGrid(refitText: chrome.AmountBand != bandBefore);
        }

        /// <summary>Places one run's chrome against the grid it labels.
        /// Surplus labels from a wider window are hidden rather than
        /// disposed, so a drag across the threshold churns nothing.</summary>
        private void LayoutSectionChrome(
            SectionChrome chrome, SnapshotResultLayout.Section section, int gridWidth)
        {
            if (chrome == null)
            {
                return;
            }

            chrome.TitlePanel.Visible = section.Present;
            chrome.HeaderPanel.Visible = section.Present;
            if (!section.Present)
            {
                return;
            }

            chrome.TitlePanel.Location = new Point(0, section.TitleY);
            chrome.TitlePanel.Size = new Point(gridWidth, SectionTitleBandHeight);
            chrome.TitleDivider.Size = new Point(gridWidth, 2);

            chrome.HeaderPanel.Location = new Point(0, section.HeaderY);
            chrome.HeaderPanel.Size = new Point(gridWidth, PlanContentHeightMath.ColumnHeaderRowHeight);

            int columnCount = section.Grid.ColumnCount;
            int columnWidth = section.Grid.ColumnWidth;

            while (chrome.NameHeaders.Count < columnCount)
            {
                chrome.NameHeaders.Add(CreateHeaderLabel(chrome, chrome.NameText));
                chrome.AmountHeaders.Add(CreateHeaderLabel(chrome, chrome.AmountText));
            }

            for (int i = 0; i < chrome.NameHeaders.Count; i++)
            {
                bool used = i < columnCount;
                chrome.NameHeaders[i].Visible = used;
                chrome.AmountHeaders[i].Visible = used;
                if (!used)
                {
                    continue;
                }

                int columnX = i * columnWidth;
                int amountX =
                    SnapshotItemGridLayout.CellAmountRightEdge(columnWidth) - chrome.AmountWidth;

                chrome.NameHeaders[i].Location =
                    new Point(columnX + SnapshotItemGridLayout.CellTextX, PlanContentHeightMath.ColumnHeaderLabelY);
                chrome.AmountHeaders[i].Location = new Point(columnX + amountX, PlanContentHeightMath.ColumnHeaderLabelY);
            }

            SyncHeaderCells(chrome, columnCount, columnWidth, gridWidth);
        }

        /// <summary>
        /// Re-describes the header band's cells for the grid it now spans.
        /// Position-and-width work only - the labels, widths and actions
        /// live on the chrome's <see cref="HeaderCellPlan"/>. Each cell's
        /// right edge is its own column's, not a midpoint between two
        /// header words, and the last column absorbs the remainder.
        /// </summary>
        private static void SyncHeaderCells(
            SectionChrome chrome, int columnCount, int columnWidth, int gridWidth)
        {
            if (columnCount <= 0)
            {
                return;
            }

            if (chrome.CellPlan == null || chrome.PlanColumns != columnCount)
            {
                chrome.CellPlan = new HeaderCellPlan(columnCount * 2, chrome.Cells);
                for (int i = 0; i < columnCount; i++)
                {
                    chrome.CellPlan.Set(i * 2, chrome.NameHeaders[i], chrome.NameWidth, chrome.SortByName);
                    chrome.CellPlan.Set(
                        (i * 2) + 1, chrome.AmountHeaders[i], chrome.AmountWidth, chrome.SortByAmount);
                }

                chrome.PlanColumns = columnCount;
            }

            int splitX = SnapshotItemGridLayout.CellHeaderSplitX(columnWidth, chrome.AmountBand);
            for (int i = 0; i < columnCount; i++)
            {
                int columnX = i * columnWidth;
                chrome.CellPlan.SetBoundary(i * 2, columnX + splitX);
                chrome.CellPlan.SetBoundary(
                    (i * 2) + 1, i == columnCount - 1 ? gridWidth : columnX + columnWidth);
            }

            chrome.CellPlan.Sync(gridWidth);
        }

        private static Label CreateHeaderLabel(SectionChrome chrome, string text)
        {
            var label = LabelHelpers.WithDescenderClearance(new Label()
            {
                Font = TableHeaderStyle.Font,
                Text = text,
                TextColor = TableHeaderStyle.LabelColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = chrome.HeaderPanel,
            });

            // The hit area is the whole cell (SortableHeaderCells); the
            // label carries only the note, which it would swallow.
            SortableHeaderLabel.MarkSortable(label);
            return label;
        }

        /// <summary>
        /// One placed result cell: the row Panel the grid moves and sizes,
        /// and the closure that re-ellipsizes its text lines against a new
        /// column width and re-pins its amount. Text and position only -
        /// see RefitResultRows for the wallet row's one exception.
        /// </summary>
        private sealed class ResultCell
        {
            public readonly Panel Panel;
            public readonly Action<int> Fit;

            public ResultCell(Panel panel, Action<int> fit)
            {
                Panel = panel;
                Fit = fit;
            }
        }

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
            Panel rowPanel, string text, int maxWidth, int y, Color? color, out bool shortened)
        {
            var label = new Label()
            {
                Font = UiFonts.Body,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(RowTextX, y),
                Parent = rowPanel,
            };
            if (color.HasValue)
            {
                label.TextColor = color.Value;
            }

            shortened = FitRowTextLabel(label, text, maxWidth);

            // Both lines of a snapshot row - the item name and the
            // "Character: ..." source caption - carry names nobody here
            // picks the letters of, so both need the descender clearance
            // (field test, bug 5; the shipped build clips the tail off
            // "Green Wood Log").
            return LabelHelpers.WithDescenderClearance(label);
        }

        /// <summary>
        /// Ellipsizes one line to the width of the CELL it sits in - one
        /// grid column, not the whole content panel - so the build-time fit
        /// and the repack cannot drift. Text only: the ROW owns its
        /// tooltips, and a note written from this path would drop the rich
        /// surface over the same label on every repack (a non-null
        /// BasicTooltipText nulls Control._tooltip).
        /// </summary>
        private static bool FitRowTextLabel(Label label, string text, int maxWidth)
        {
            string full = text ?? "";
            string shown = LabelHelpers.EllipsizeToWidth(UiFonts.Body, full, maxWidth);

            label.Text = shown;
            return shown != full;
        }

        private void CreateItemRow(SnapshotSearchRow row, int columnWidth, SectionChrome chrome)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(columnWidth, ItemRowHeight),
                Parent = _resultGridPanel,
            };

            // The module's one icon component, at the plan rows' 32px art
            // in a 1px frame. Rarity comes from the session stat cache
            // because an AccountSnapshot carries none; a miss is neutral.
            string rarity = RarityFor(row.ItemId);
            var icon = IconControls.CreateItemIcon(rowPanel, row.IconUrl, rarity, 2, 2);

            // Never display raw item IDs (repo invariant) - row.Name is
            // already the resolved display name.
            //
            // The count is a COLUMN, not a prefix: a quantity a reader can
            // sort by has to line up. The name takes a rarity colour only
            // when one is KNOWN - the unknown entry is a 200-grey that would
            // dim every name on a fresh session (see RarityFor).
            string nameText = row.Name ?? "";
            string amountText = AmountText(row.TotalCount);
            var nameLabel = CreateRowTextLabel(
                rowPanel, nameText,
                SnapshotItemGridLayout.CellNameMaxWidth(columnWidth, chrome.AmountBand),
                4, rarity == null ? (Color?)null : RarityColors.GetRarityNameColor(rarity),
                out _);

            // Measured here, not in the closure: the text is fixed and the
            // repack walks every row on screen.
            int amountWidth = (int)Math.Ceiling(UiFonts.Body.MeasureString(amountText).Width);
            var amountLabel = CreateAmountLabel(rowPanel, amountText, amountWidth, columnWidth, 4);

            // NOT the Amount column's prefix notation, and the one
            // deliberate exemption from M9's sweep: these labels are
            // LOCATIONS. "20x Bank" parses as twenty banks, and "10x
            // Character: Maximus Test" collides with the label's own colon.
            string breakdown = row.Breakdown == null || row.Breakdown.Count == 0
                ? ""
                : string.Join("   ", row.Breakdown.Select(b => $"{b.Label} {b.Count}"));

            // Runs UNDER the Amount column: that is one short line.
            var breakdownLabel = CreateRowTextLabel(
                rowPanel, breakdown, SnapshotItemGridLayout.CellFullLineMaxWidth(columnWidth),
                26, InfoTextColor, out _);

            // The degrade path, stamped before the rich one takes the
            // control over: Register captures this as the source's
            // FallbackText, which is what a hover shows if the deferred
            // builder throws (TooltipFacility.ResolveContent - the stat
            // lookup runs inside Blish's mouse-moved handler). Width-
            // independent, so the repack still owns no tooltip.
            TooltipFacility.ApplyPlain(nameLabel, nameText);
            TooltipFacility.ApplyPlain(breakdownLabel, breakdown);

            // The plan's own rich item tooltip, composed at hover time so a
            // stat block fetched later shows without a re-render. Stamped
            // ONCE: it says the same at any width, so the repack leaves
            // every tooltip on the row alone.
            ApplyItemRowTooltip(
                rowPanel, nameLabel, breakdownLabel, amountLabel, icon, row, nameText, breakdown);

            // The cell's own Size is the grid's to write (LayoutResultGrid),
            // so this closure only re-fits what the new column width changed.
            _itemCells.Add(new ResultCell(rowPanel, w =>
            {
                FitRowTextLabel(
                    nameLabel, nameText, SnapshotItemGridLayout.CellNameMaxWidth(w, chrome.AmountBand));
                FitRowTextLabel(breakdownLabel, breakdown, SnapshotItemGridLayout.CellFullLineMaxWidth(w));
                PlaceAmountLabel(amountLabel, amountWidth, w, 4);
            }));
        }

        /// <summary>
        /// The cell's Amount column: right-aligned on the edge every cell
        /// pins to, so the numbers line up however long the names are.
        /// </summary>
        private static Label CreateAmountLabel(
            Panel rowPanel, string text, int textWidth, int columnWidth, int y)
        {
            var label = LabelHelpers.WithDescenderClearance(new Label()
            {
                Font = UiFonts.Body,
                Text = text,
                TextColor = AmountTextColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = rowPanel,
            });
            PlaceAmountLabel(label, textWidth, columnWidth, y);
            return label;
        }

        /// <summary>Re-pins one amount to its column's right edge, on the
        /// width the caller measured at build: the text never changes, and
        /// this is a position-and-width-only path.</summary>
        private static void PlaceAmountLabel(Label label, int textWidth, int columnWidth, int y)
        {
            label.Location = new Point(
                SnapshotItemGridLayout.CellAmountRightEdge(columnWidth) - textWidth, y);
        }

        /// <summary>
        /// One item row's tooltip, on the strip AND on every control over
        /// it - Blish resolves a tooltip on the deepest control under the
        /// cursor and never bubbles, so an unstamped label or icon is a
        /// hole in the row's hover. Deferred, so a stat block fetched later
        /// in the session shows without a re-render; the name and the whole
        /// breakdown ride along either way, so this says the same thing at
        /// every column width and is stamped once, at build.
        /// </summary>
        private void ApplyItemRowTooltip(
            Panel rowPanel, Label nameLabel, Label breakdownLabel, Label amountLabel, Panel icon,
            SnapshotSearchRow row, string nameText, string breakdown)
        {
            Func<TooltipContent> build = () =>
            {
                // ALWAYS, not only when the line was shortened: the hover
                // is where a reader goes for the whole run of it.
                var extras = new List<string>();
                if (!string.IsNullOrEmpty(breakdown))
                {
                    extras.Add(breakdown);
                }

                // ...and so does the name: a stat block usually does not
                // exist on this tab (see RarityFor), and a row answering
                // nothing is the reported "no tooltips". With one, its own
                // header wins instead.
                const bool alwaysHeadWithTheName = true;

                return ItemRowTooltipComposer.BuildRowContent(
                    _getItemStatBlock == null || row.ItemId <= 0 ? null : _getItemStatBlock(row.ItemId),
                    nameText,
                    alwaysHeadWithTheName,
                    extras);
            };

            TooltipFacility.ApplyRichDeferred(rowPanel, build);
            TooltipFacility.ApplyRichDeferred(nameLabel, build);
            TooltipFacility.ApplyRichDeferred(breakdownLabel, build);
            TooltipFacility.ApplyRichDeferred(amountLabel, build);

            // Only when the row has a real item id: a non-item row's icon
            // names what it actually is, and an item builder has nothing
            // better to say about it. (An EMPTY payload is no longer the
            // hazard here - ApplyRichDeferredToIconTree keeps the control's
            // own note as the builder's fallback.)
            if (row.ItemId > 0)
            {
                IconControls.ApplyRichDeferredToIconTree(icon, build);
            }
        }

        /// <summary>The rarity this tab can know for an item, or null -
        /// see CreateItemRow for why the session stat cache is the only
        /// source here.</summary>
        private string RarityFor(int itemId)
        {
            if (_getItemStatBlock == null || itemId <= 0)
            {
                return null;
            }

            return _getItemStatBlock(itemId)?.Rarity;
        }

        private void CreateWalletRow(SnapshotWalletEntry entry, int columnWidth, SectionChrome chrome)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(columnWidth, WalletRowHeight),
                Parent = _resultGridPanel,
            };

            // Same component as the item rows; no rarity, so neutral.
            var icon = IconControls.CreateItemIcon(
                rowPanel, entry.IconUrl, (string)null, 2, 2,
                tooltipText: string.IsNullOrEmpty(entry.CurrencyName) ? null : entry.CurrencyName);

            // Never display raw currency IDs (repo invariant). Same two
            // columns as the item run above, so one header pair shape
            // labels both and a balance lines up under a count. The
            // thousands
            // separator stays: wallet balances run to seven figures where
            // an item count does not.
            string name = string.IsNullOrEmpty(entry.CurrencyName) ? "Unknown Currency" : entry.CurrencyName;
            string amountText = AmountText(entry.Value);
            var label = CreateRowTextLabel(
                rowPanel, name, SnapshotItemGridLayout.CellNameMaxWidth(columnWidth, chrome.AmountBand),
                6, null, out bool shortened);
            int amountWidth = (int)Math.Ceiling(UiFonts.Body.MeasureString(amountText).Width);
            var amountLabel = CreateAmountLabel(rowPanel, amountText, amountWidth, columnWidth, 6);

            // Width-invariant, so stamped here and not from the repack.
            // Re-stated over the component's resolution, which took the raw
            // CurrencyName where this is the fallback.
            IconControls.ApplyPlainToIconTree(icon, name);
            StampWalletRowTooltip(rowPanel, label, name, shortened);

            _walletCells.Add(new ResultCell(rowPanel, w =>
            {
                bool nowShortened = FitRowTextLabel(
                    label, name, SnapshotItemGridLayout.CellNameMaxWidth(w, chrome.AmountBand));
                PlaceAmountLabel(amountLabel, amountWidth, w, 6);
                StampWalletRowTooltip(rowPanel, label, name, nowShortened);
            }));
        }

        /// <summary>A wallet row's hover: the full currency name wherever
        /// the line shortened, on the panel AND the label (a tooltip
        /// resolves on the deepest control and never bubbles). The icon is
        /// stamped once at build.</summary>
        private static void StampWalletRowTooltip(
            Panel rowPanel, Label nameLabel, string name, bool shortened)
        {
            TooltipFacility.ApplyPlain(rowPanel, shortened ? name : null);
            TooltipFacility.ApplyPlain(nameLabel, shortened ? name : null);
        }

        // Builds its CoinSegmentSpec list through the shared
        // CoinCurrencyRenderer.AddSegmentSpec, so the coin invariant has one
        // encoding rather than a per-view copy. Always exactly 3 segments -
        // gold, silver, copper - via plain ToString(), with no
        // leading-zero-unit omission and no zero-padding: this tab shows the
        // full wallet, unlike a plan's cost cells. Handed to
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
        /// <summary>
        /// Places the header band's two buttons and its rule against the
        /// tab's ONE right edge - the edge the scrolling grid's last column
        /// also ends on. Build and the resize handler both come through
        /// here, so the two cannot drift.
        /// </summary>
        private void LayoutHeaderRow(int containerWidth)
        {
            if (_refreshButton == null)
            {
                return;
            }

            int rightEdge = SnapshotHeaderLayout.ChromeRightEdge(containerWidth);
            int refreshX = PlanRelayoutMath.RightAlignedX(rightEdge, HeaderButtonWidth);

            _refreshButton.Location = new Point(refreshX, HeaderButtonY);
            _clearButton.Location = new Point(
                refreshX - SnapshotHeaderLayout.HeaderButtonGap - HeaderButtonWidth, HeaderButtonY);
            _headerDivider.Size = new Point(rightEdge, 2);

            _statusBudget = SnapshotHeaderLayout.StatusMaxWidth(containerWidth, StatusSpinnerReserve);
            ApplyStatusText();
        }

        /// <summary>
        /// Right-pins the coin block as a unit and gives the result line
        /// what is left of the row. CoinCurrencyRenderer is untouched, so
        /// the icons stay to the RIGHT of their numbers - only the block's
        /// origin moves.
        /// </summary>
        private void LayoutCoinRow(int containerWidth)
        {
            if (_coinBlockPanel == null)
            {
                return;
            }

            _coinBlockPanel.Size = new Point(_coinBlockWidth, CoinHeight);
            _coinBlockPanel.Location = new Point(
                SnapshotHeaderLayout.CoinBlockX(containerWidth, _coinBlockWidth), 0);

            if (_resultLineLabel == null)
            {
                return;
            }

            int budget = SnapshotHeaderLayout.ResultLineMaxWidth(containerWidth, _coinBlockWidth);
            _resultLineLabel.Width = budget;

            string shown = LabelHelpers.EllipsizeToWidth(UiFonts.Body, _resultLineText, budget);
            if (!string.Equals(_resultLineLabel.Text, shown, StringComparison.Ordinal))
            {
                _resultLineLabel.Text = shown;
            }

            string full = string.Equals(shown, _resultLineText, StringComparison.Ordinal)
                ? null
                : _resultLineText;
            TooltipFacility.ApplyPlain(_resultLineLabel, full);
            TooltipFacility.ApplyPlain(_coinPanel, full);
        }

        /// <summary>
        /// What the result grid is currently showing, in the "N of M shown"
        /// shape the Settings tab's currency filter already uses. Counts and
        /// names only - ids never reach it (repo invariant).
        /// </summary>
        private void SetResultLine(int shownItems, int totalItems, int shownWallet, int totalWallet)
        {
            var parts = new List<string>(2);
            if (totalItems > 0)
            {
                parts.Add(shownItems == totalItems
                    ? StatusText.Count(totalItems, "item")
                    : $"{shownItems} of {totalItems} items");
            }

            if (totalWallet > 0)
            {
                parts.Add(shownWallet == totalWallet
                    ? StatusText.Count(totalWallet, "currency", "currencies")
                    : $"{shownWallet} of {totalWallet} currencies");
            }

            _resultLineText = parts.Count == 0 ? "" : "Showing " + string.Join(" - ", parts);
            LayoutCoinRow(_containerWidth);
        }

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
            if (_coinBlockPanel == null)
            {
                return;
            }

            foreach (var child in _coinBlockPanel.Children.ToArray())
            {
                child.Dispose();
            }

            var (gold, silver, cop) = CoinSegmentMath.Split(copper);

            var font = UiFonts.Body;

            // Body, not Caption: this was the one text on the tab both
            // smaller AND greyer than what it labels. One channel of
            // de-emphasis, and it lines up with the numbers it introduces.
            new Label()
            {
                Text = CoinCaption,
                Font = font,
                TextColor = CoinCaptionColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 2),
                Parent = _coinBlockPanel,
            };
            int captionWidth = (int)Math.Ceiling(font.MeasureString(CoinCaption).Width);

            var segments = new List<CoinSegmentMath.CoinSegmentSpec>(3);
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, CoinSegmentMath.GoldAssetId, gold.ToString());
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, CoinSegmentMath.SilverAssetId, silver.ToString());
            CoinCurrencyRenderer.AddSegmentSpec(segments, font, CoinSegmentMath.CopperAssetId, cop.ToString());

            CoinCurrencyRenderer.LayoutCoinSegments(
                _coinBlockPanel, segments, captionWidth + CoinCaptionGap, 2, font);

            // The block's exact extent, from the same arithmetic that laid
            // it out - so nothing re-measures it to right-pin it.
            // TotalCoinSegmentsWidth already drops the run's trailing gap.
            _coinBlockWidth = captionWidth + CoinCaptionGap
                + CoinSegmentMath.TotalCoinSegmentsWidth(segments);

            LayoutCoinRow(_containerWidth);
        }
    }
}
