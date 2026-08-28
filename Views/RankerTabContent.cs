using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Views.Rendering;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// The Crafting Ranker: a persisted, priority-ordered list of items the
    /// user is working toward, each row answering "how close am I, given that
    /// everything above this has first claim on my materials?".
    ///
    /// Structurally a LogTabContent-shaped tab (fixed chrome siblings plus one
    /// scrolling FlowPanel), but held for the module's lifetime like
    /// AboutTabContent, because the watchlist, the ephemeral per-row metrics
    /// and the in-flight refresh token all have to survive a tab switch.
    /// </summary>
    internal class RankerTabContent
    {
        private static readonly Logger Logger = Logger.GetLogger<RankerTabContent>();

        private const int SectionBandHeight = PlanContentHeightMath.SectionHeaderRowHeight;
        private const int SectionTitleY = PlanContentHeightMath.SectionHeaderTitleY;
        private const int AddRowHeight = 40;
        private const int ToolbarHeight = 40;
        private const int ColumnHeaderRowHeight = PlanContentHeightMath.ColumnHeaderRowHeight;
        private const int ColumnHeaderLabelY = PlanContentHeightMath.ColumnHeaderLabelY;
        private const int TopChromeHeight =
            SectionBandHeight + AddRowHeight + ToolbarHeight + ColumnHeaderRowHeight;

        private const int ScrollbarAllowance = WindowSizing.ScrollbarAllowance;
        private const int CaptionLineHeight = 18;
        private const int CaptionsPadding = 10;
        private const int SearchBoxWidth = 260;
        private const int QuantityBoxWidth = 56;
        private const int AddButtonWidth = 72;
        private const int ModeDropdownWidth = 150;
        private const int ModeGap = 8;
        private const int BannerHeight = 30;

        // Vertical centring inside the 60px tier-1 main line (see
        // RankerRowLayout.RowHeight): a Body text line, the rank caption,
        // the 22px chip, the 28px row buttons and the 54px icon frame.
        private const int MainLineTextY = 20;
        private const int MainLineRankY = 22;
        private const int MainLineChipY = 19;
        private const int MainLineButtonY = 16;
        private const int MainLineIconY = 3;

        // Muted grey is reserved for content meant to leave the user's
        // focus: the footer captions and the empty-state onboarding prose
        // (matching EmptyPlanStateRenderer). Field test: primary row data at
        // this grey on the grey window read "as if disabled".
        private static readonly Color DimColor = new Color(150, 150, 150);

        // Primary sub-line data matches the Crafting Plan's currency table
        // rows, the direct analogue: names in white, figures at 220 grey.
        private static readonly Color ValueTextColor = new Color(220, 220, 220);

        private static readonly Color StatusColor = new Color(200, 200, 200);
        private static readonly Color ErrorColor = new Color(255, 100, 100);
        private static readonly Color SectionDividerColor = new Color(130, 130, 130);

        // The affordability chip reuses SummarySectionRenderer's
        // full-coverage tag colors (PillKind.Selected's darkened green,
        // 4.21:1 against CreateSmallTag's white label) - the field test
        // showed white text on RankerReadinessColors' pale #7EBA7E was
        // unreadable. The readiness TEXT bands keep their own palette; only
        // the pill chrome borrows the proven badge combination.
        private static readonly Color AffordableChipBorder = new Color(31, 143, 12);
        private static readonly Color AffordableChipFill = AffordableChipBorder * 0.15f;

        private readonly CraftingPlanPipeline _pipeline;
        private readonly IItemSearchProvider _itemSearchProvider;
        private readonly ModuleSettings _settings;
        private readonly RankerStore _store;
        private readonly Func<AccountSnapshot> _getSnapshot;
        private readonly Func<string> _getActiveCharacterName;
        private readonly Func<int, ItemStatBlock> _getItemStatBlock;
        private readonly ResizeSettleDebounce _resizeSettle;

        private readonly RankerWatchlist _watchlist;

        // Ephemeral, session-scoped, keyed by item id. Never persisted: a
        // readiness number goes stale the moment Trading Post prices move.
        private readonly Dictionary<int, RankerRowMetrics> _metricsByItemId =
            new Dictionary<int, RankerRowMetrics>();

        private readonly Dictionary<int, CraftingPlanResult> _lastOwnedResults =
            new Dictionary<int, CraftingPlanResult>();

        private readonly List<RenderedRow> _rows = new List<RenderedRow>();

        private Panel _headerPanel;
        private Panel _headerDivider;
        private Panel _addPanel;
        private Panel _toolbarPanel;
        private Panel _columnHeaderPanel;
        private FlowPanel _contentPanel;
        private Panel _captionsPanel;
        private readonly List<Label> _captionLabels = new List<Label>();

        private AutocompleteTextBox _searchBox;
        private SuggestionPanel _suggestionPanel;
        private TextBox _quantityBox;
        private FeedbackButton _addButton;
        private FeedbackButton _refreshButton;
        private Label _modeLabel;
        private Dropdown _modeDropdown;
        private bool _suppressModeChange;
        private Label _statusLabel;
        private LoadingSpinner _spinner;
        private Panel _bannerPanel;
        private Label _bannerLabel;

        private readonly List<Label> _columnHeaderLabels = new List<Label>();

        private int? _pendingItemId;
        private string _pendingItemName;
        private string _pendingItemIconUrl;

        private volatile bool _buildComplete;
        private int _lastLayoutWidth = -1;

        // Table-wide, not per row: the Ready and Days cells sit to the LEFT
        // of the coin cell, so letting each row size its own coin band
        // would put those columns in a different place on every row and
        // leave the header labelling nothing.
        private int _remainingBandWidth;
        private int _refreshGeneration;
        private CancellationTokenSource _refreshCts;
        private bool _isRefreshing;
        private bool _firstRefreshDone;
        private bool _rarityDirty;
        private DateTime? _lastRefreshLocal;
        private string _statusOverride;
        private bool _statusIsError;

        public RankerTabContent(
            CraftingPlanPipeline pipeline,
            IItemSearchProvider itemSearchProvider,
            ModuleSettings settings,
            RankerStore store,
            Func<AccountSnapshot> getSnapshot,
            Func<string> getActiveCharacterName,
            Func<int, ItemStatBlock> getItemStatBlock = null)
        {
            _pipeline = pipeline;
            _itemSearchProvider = itemSearchProvider;
            _settings = settings;
            _store = store;
            _getSnapshot = getSnapshot ?? (() => null);
            _getActiveCharacterName = getActiveCharacterName ?? (() => null);
            _getItemStatBlock = getItemStatBlock;
            _watchlist = store?.Load() ?? new RankerWatchlist();

            _resizeSettle = new ResizeSettleDebounce(
                RefitAfterResizeSettle,
                MainThreadMarshal.Run,
                ResizeSettleDebounce.DefaultSettleMs,
                ex => Logger.Warn(ex, "Ranker row re-fit wait failed"));
        }

        /// <summary>
        /// The last Refresh's owned solve per item id, for a future
        /// next-action classifier. Session-scoped and never persisted.
        /// </summary>
        public IReadOnlyDictionary<int, CraftingPlanResult> LastOwnedResults => _lastOwnedResults;

        /// <summary>Main thread, immediately before Blish queues the off-thread Build.</summary>
        public void BeginRebuild()
        {
            _buildComplete = false;
        }

        public void Build(Container container)
        {
            _buildComplete = false;
            _rows.Clear();
            _captionLabels.Clear();
            _columnHeaderLabels.Clear();
            _lastLayoutWidth = -1;

            int w = container.ContentRegion.Width;

            BuildSectionBand(container, w);
            BuildAddRow(container, w);
            BuildToolbar(container, w);
            BuildColumnHeader(container, w);

            _contentPanel = new FlowPanel
            {
                Size = new Point(w, Math.Max(0, container.ContentRegion.Height - TopChromeHeight)),
                Location = new Point(0, TopChromeHeight),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = container,
            };

            _captionsPanel = new Panel
            {
                Size = new Point(w, 0),
                Location = new Point(0, TopChromeHeight),
                Parent = container,
            };

            PositionChrome(container, w);

            container.Resized += (_, __) =>
            {
                if (!_buildComplete)
                {
                    return;
                }

                PositionChrome(container, container.ContentRegion.Width);
                RefitRows();
            };

            // Build() runs on a ThreadPool thread; every control touch below
            // this point has to land on the main thread, and _buildComplete
            // must be set inside the same queued callback so no entry point
            // can observe a half-built tab.
            MainThreadMarshal.Run(() =>
            {
                RebuildRows();

                // A tab switch during a run rebuilds the chrome from scratch,
                // so the in-flight state has to be restamped onto it. The
                // button keeps its fixed "Refresh" label - progress text
                // belongs to the status band (field bug: status-length text
                // on the 132px button spilled past its edges).
                if (_isRefreshing)
                {
                    _spinner.Visible = true;
                    SetControlsEnabled(false);
                }

                _buildComplete = true;
            });
        }

        /// <summary>View-only refresh on tab switch. Never triggers a solve.</summary>
        public void Refresh()
        {
            if (!_buildComplete || !IsLive)
            {
                return;
            }

            UpdateStatusLine();
            UpdateBanner();
        }

        public void CancelRefresh()
        {
            try
            {
                _refreshCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Teardown()
        {
            CancelRefresh();
            _resizeSettle.Cancel();
            // SpriteScreen-parented, so disposing the row never disposes it.
            _suggestionPanel?.Dispose();
            _suggestionPanel = null;
        }

        private bool IsLive => _contentPanel != null && _contentPanel.Parent != null;

        private List<RankerWatchlistEntry> Entries => _watchlist.Entries;

        private RankerMode Mode => _watchlist.Mode;

        // ---------------------------------------------------------------
        // Comparison mode
        // ---------------------------------------------------------------
        private const string CascadeModeItem = "In priority order";
        private const string IndependentModeItem = "Each on its own";

        private static string ModeItem(RankerMode mode)
        {
            return mode == RankerMode.Independent ? IndependentModeItem : CascadeModeItem;
        }

        private static string ModeTooltip(RankerMode mode)
        {
            return mode == RankerMode.Independent
                ? "Every row is measured against your full account, ignoring the other rows - which is closest to done right now? Closest sorts to the top; your priority order is kept and restored when you switch back."
                : "Each row is measured after the rows above it claim your materials, currencies and daily crafts.";
        }

        /// <summary>
        /// A mode toggle never re-solves: metrics computed under the other
        /// mode go stale via MetricsAreCurrent (and revive if the user
        /// toggles straight back), exactly the staleness a reorder causes.
        /// </summary>
        private void OnModeChanged(RankerMode mode)
        {
            if (_isRefreshing || mode == _watchlist.Mode)
            {
                return;
            }

            _watchlist.Mode = mode;
            Persist();
            TooltipFacility.ApplyPlain(_modeDropdown, ModeTooltip(mode));
            RebuildRows();

            bool anyStale = false;
            for (int i = 0; i < Entries.Count; i++)
            {
                _metricsByItemId.TryGetValue(Entries[i].ItemId, out var metrics);
                if (!RankerPriorityOrdering.MetricsAreCurrent(metrics, i, mode))
                {
                    anyStale = true;
                    break;
                }
            }

            if (Entries.Count > 0 && anyStale)
            {
                SetStatus("Comparison mode changed - press Refresh to recalculate.", isError: false);
            }
            else
            {
                _statusOverride = null;
                UpdateStatusLine();
            }
        }

        /// <summary>
        /// Priority indices in the order the table displays them: the
        /// stored order in Cascade mode, readiness-descending in
        /// Independent mode. The stored order itself is never touched.
        /// </summary>
        private List<int> DisplayOrder()
        {
            if (Mode != RankerMode.Independent)
            {
                var order = new List<int>(Entries.Count);
                for (int i = 0; i < Entries.Count; i++)
                {
                    order.Add(i);
                }

                return order;
            }

            return RankerPriorityOrdering.IndependentDisplayOrder(Entries, entry =>
            {
                _metricsByItemId.TryGetValue(entry.ItemId, out var metrics);
                return RankerPriorityOrdering.MetricsAreCurrent(
                    metrics, Entries.IndexOf(entry), Mode) ? metrics : null;
            });
        }

        // ---------------------------------------------------------------
        // Chrome
        // ---------------------------------------------------------------
        private void BuildSectionBand(Container container, int width)
        {
            _headerPanel = new Panel
            {
                Size = new Point(width, SectionBandHeight),
                Parent = container,
            };

            new Label
            {
                Font = UiFonts.SectionTitle,
                Text = "Crafting Ranker",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(RankerRowLayout.Inset, SectionTitleY),
                Parent = _headerPanel,
            };

            _headerDivider = new Panel
            {
                Size = new Point(Math.Max(0, width - ScrollbarAllowance), 2),
                Location = new Point(0, SectionBandHeight - 3),
                BackgroundColor = SectionDividerColor,
                Parent = _headerPanel,
            };
        }

        private void BuildAddRow(Container container, int width)
        {
            _addPanel = new Panel
            {
                Size = new Point(width, AddRowHeight),
                Location = new Point(0, SectionBandHeight),
                Parent = container,
            };

            // SpriteScreen-parented and holding a global mouse subscription,
            // so disposing the old container never reaches it - a tab revisit
            // would otherwise leak one popup per visit.
            _suggestionPanel?.Dispose();

            _searchBox = new AutocompleteTextBox
            {
                PlaceholderText = "Search for an item to track...",
                Size = new Point(SearchBoxWidth, UiMetrics.ButtonHeight),
                Location = new Point(RankerRowLayout.Inset, 6),
                Parent = _addPanel,
            }.ReleaseOnDispose().ReleaseOnEnter();

            _suggestionPanel = new SuggestionPanel(_searchBox, _itemSearchProvider);
            _suggestionPanel.ItemSelected += (_, args) =>
            {
                _pendingItemId = args.ItemId;
                _pendingItemName = args.Name;
                _pendingItemIconUrl = args.IconUrl;
                UpdateAddButtonState();
            };

            _searchBox.TextChanged += (_, __) =>
            {
                // A pick is the only thing that resolves the field; editing it
                // afterwards has to drop that resolution, or Add would queue a
                // different item than the box reads.
                if (ItemRowSelection.SelectionIsStale(_pendingItemId, _pendingItemName, _searchBox.Text))
                {
                    _pendingItemId = null;
                    _pendingItemName = null;
                    _pendingItemIconUrl = null;
                }

                UpdateAddButtonState();
            };

            _quantityBox = new TextBox
            {
                PlaceholderText = "Qty",
                Text = "1",
                Size = new Point(QuantityBoxWidth, UiMetrics.ButtonHeight),
                Location = new Point(RankerRowLayout.Inset + SearchBoxWidth + 8, 6),
                Parent = _addPanel,
            }.ReleaseOnDispose().ReleaseOnEnter();

            _addButton = new FeedbackButton
            {
                Text = "Add",
                Size = new Point(AddButtonWidth, UiMetrics.ButtonHeight),
                Location = new Point(RankerRowLayout.Inset + SearchBoxWidth + QuantityBoxWidth + 16, 6),
                Enabled = false,
                Parent = _addPanel,
            };
            TooltipFacility.ApplyPlain(_addButton, "Add this item to the bottom of your priority list.");
            _addButton.Click += (_, __) => AddPendingItem();

            // The comparison-mode selector, right-anchored on this row (the
            // toolbar row below is the status band's full width). Same
            // labelled-Dropdown shape as the plan tab's "Prices:" control -
            // the module's established two-way mode switch.
            _modeLabel = new Label
            {
                Font = UiFonts.Body,
                Text = "Compare:",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 10),
                Parent = _addPanel,
            };
            _modeDropdown = new Dropdown
            {
                Size = new Point(ModeDropdownWidth, 28),
                Location = new Point(0, 6),
                Parent = _addPanel,
            };
            _modeDropdown.Items.Add(ModeItem(RankerMode.Cascade));
            _modeDropdown.Items.Add(ModeItem(RankerMode.Independent));
            _suppressModeChange = true;
            _modeDropdown.SelectedItem = ModeItem(Mode);
            _suppressModeChange = false;
            TooltipFacility.ApplyPlain(_modeDropdown, ModeTooltip(Mode));
            _modeDropdown.ValueChanged += (_, e) =>
            {
                if (_suppressModeChange)
                {
                    return;
                }

                OnModeChanged(e.CurrentValue == ModeItem(RankerMode.Independent)
                    ? RankerMode.Independent
                    : RankerMode.Cascade);
            };
        }

        private void BuildToolbar(Container container, int width)
        {
            _toolbarPanel = new Panel
            {
                Size = new Point(width, ToolbarHeight),
                Location = new Point(0, SectionBandHeight + AddRowHeight),
                Parent = container,
            };

            _statusLabel = new Label
            {
                Font = UiFonts.Status,
                Text = "",
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                TextColor = StatusColor,
                Location = new Point(RankerRowLayout.Inset, 8),
                Parent = _toolbarPanel,
            };

            _spinner = InlineSpinner.Create(_toolbarPanel, InlineSpinnerLayout.SnapshotStatusSize);

            _refreshButton = new FeedbackButton
            {
                Text = "Refresh",
                Size = new Point(RankerRowLayout.RefreshButtonWidth, UiMetrics.ButtonHeight),
                Location = new Point(0, 6),
                Parent = _toolbarPanel,
            };
            _refreshButton.Click += (_, __) => OnRefreshClicked();
        }

        private void BuildColumnHeader(Container container, int width)
        {
            _columnHeaderPanel = new Panel
            {
                Size = new Point(width, ColumnHeaderRowHeight),
                Location = new Point(0, SectionBandHeight + AddRowHeight + ToolbarHeight),
                BackgroundColor = TableHeaderStyle.BandColor,
                Parent = container,
            };

            foreach (string text in new[] { "#", "Item", "Ready", "Days", "Remaining" })
            {
                _columnHeaderLabels.Add(new Label
                {
                    Font = TableHeaderStyle.Font,
                    TextColor = TableHeaderStyle.LabelColor,
                    Text = text,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(0, ColumnHeaderLabelY),
                    Parent = _columnHeaderPanel,
                });
            }
        }

        private void PositionChrome(Container container, int width)
        {
            int height = container.ContentRegion.Height;
            int barWidth = Math.Max(0, width - ScrollbarAllowance);

            _headerPanel.Size = new Point(width, SectionBandHeight);
            _headerDivider.Size = new Point(barWidth, 2);
            _addPanel.Size = new Point(width, AddRowHeight);
            _toolbarPanel.Size = new Point(width, ToolbarHeight);
            _columnHeaderPanel.Size = new Point(width, ColumnHeaderRowHeight);

            var toolbar = RankerRowLayout.Toolbar(
                barWidth, InlineSpinnerLayout.SnapshotStatusSize, InlineSpinnerLayout.LabelGap);
            _refreshButton.Location = new Point(toolbar.RefreshX, _refreshButton.Location.Y);
            _statusLabel.Width = toolbar.StatusWidth;
            InlineSpinner.PlaceAfter(_spinner, _statusLabel, InlineSpinnerLayout.LabelGap);

            _modeDropdown.Location = new Point(
                Math.Max(0, barWidth - ModeDropdownWidth), _modeDropdown.Location.Y);
            _modeLabel.Location = new Point(
                Math.Max(0, _modeDropdown.Location.X - ModeGap - _modeLabel.Width),
                _modeLabel.Location.Y);

            PositionColumnHeader(barWidth);

            int captionsHeight = MeasureCaptionsHeight(barWidth);
            _captionsPanel.Size = new Point(width, captionsHeight);
            _captionsPanel.Location = new Point(0, Math.Max(TopChromeHeight, height - captionsHeight));

            _contentPanel.Size = new Point(
                width, Math.Max(0, height - TopChromeHeight - captionsHeight));
            _contentPanel.Location = new Point(0, TopChromeHeight);
        }

        private void PositionColumnHeader(int barWidth)
        {
            // The header labels sit on the columns they name because every
            // row shares these same table-wide band widths.
            var bands = BandsFor(barWidth);

            SetHeaderLabel(0, bands.RankX);
            SetHeaderLabel(1, bands.NameX);
            SetHeaderLabelRight(2, bands.ReadyRightEdge);
            SetHeaderLabelRight(3, bands.DaysRightEdge);
            SetHeaderLabelRight(4, bands.RemainingRightEdge);
        }

        private void SetHeaderLabel(int index, int x)
        {
            if (index < _columnHeaderLabels.Count)
            {
                _columnHeaderLabels[index].Location = new Point(x, ColumnHeaderLabelY);
            }
        }

        private void SetHeaderLabelRight(int index, int rightEdge)
        {
            if (index >= _columnHeaderLabels.Count)
            {
                return;
            }

            var label = _columnHeaderLabels[index];
            label.Location = new Point(Math.Max(0, rightEdge - label.Width), ColumnHeaderLabelY);
        }

        // ---------------------------------------------------------------
        // Captions - the standing honesty text, below the list so it never
        // scrolls out of view
        // ---------------------------------------------------------------
        private static readonly string[] Captions =
        {
            "In priority order, each item is measured against what the items above it leave behind - higher rows have first claim on your materials, currencies, coin and daily crafts. Each on its own measures every item against your full account, ignoring the other rows, and sorts the closest-to-done to the top.",
            "Ready blends five separate barriers - materials at buy-order prices, account currencies, time-gated daily crafts, crafting disciplines and recipe unlocks - and counts only the ones this item actually has. Hover it for the breakdown.",
        };

        private int MeasureCaptionsHeight(int barWidth)
        {
            if (Entries.Count == 0)
            {
                return 0;
            }

            int usable = Math.Max(1, barWidth - 2 * RankerRowLayout.Inset);
            var measure = LabelHelpers.MeasureWith(UiFonts.Caption);
            int lines = 0;
            foreach (string caption in Captions)
            {
                lines += TextWrapMath.Wrap(caption, usable, usable, measure).Lines.Count;
            }

            return lines * CaptionLineHeight + CaptionsPadding;
        }

        private void RebuildCaptions(int barWidth)
        {
            foreach (var label in _captionLabels)
            {
                label.Parent = null;
                label.Dispose();
            }

            _captionLabels.Clear();

            if (Entries.Count == 0)
            {
                return;
            }

            int usable = Math.Max(1, barWidth - 2 * RankerRowLayout.Inset);
            var measure = LabelHelpers.MeasureWith(UiFonts.Caption);
            int y = 4;
            foreach (string caption in Captions)
            {
                foreach (string line in TextWrapMath.Wrap(caption, usable, usable, measure).Lines)
                {
                    _captionLabels.Add(new Label
                    {
                        Font = UiFonts.Caption,
                        Text = line,
                        TextColor = DimColor,
                        AutoSizeWidth = false,
                        AutoSizeHeight = true,
                        Width = usable,
                        Location = new Point(RankerRowLayout.Inset, y),
                        Parent = _captionsPanel,
                    });
                    y += CaptionLineHeight;
                }
            }
        }

        // ---------------------------------------------------------------
        // Rows
        // ---------------------------------------------------------------
        private class RenderedRow
        {
            public int ItemId;
            public int Index;
            public string FullName;
            public Panel Panel;
            public Label RankLabel;
            public IconNameRowHelpers.IconNameHandle IconName;
            public Label ReadyLabel;
            public Panel Chip;
            public int ChipWidth;
            public Label DaysLabel;
            public CoinCurrencyRenderer.ValueCellHandle RemainingCell;
            public Label RemainingDash;
            public int RemainingCellWidth;
            public FeedbackButton Up;
            public FeedbackButton Down;
            public FeedbackButton Remove;
            public readonly List<Label> GateNameLabels = new List<Label>();
            public readonly List<Label> GateValueLabels = new List<Label>();
            public readonly List<Panel> CurrencyIconFrames = new List<Panel>();
            public readonly List<Label> CurrencyNameLabels = new List<Label>();
            public readonly List<string> CurrencyNameFulls = new List<string>();
            public readonly List<Label> CurrencyValueLabels = new List<Label>();
            public readonly List<Label> NoteLabels = new List<Label>();
            public RankerRowMetrics Metrics;
        }

        private void RebuildRows()
        {
            if (_contentPanel == null)
            {
                return;
            }

            // Dispose, not ClearChildren: ClearChildren only detaches
            // (docs/ARCHITECTURE.md - "a tab switch detaches, it does not
            // dispose"), leaving every orphaned row tree to the GC and any
            // control type that hooks a static event in its constructor
            // (TrackBar does) rooted forever. Same idiom as
            // RichTooltipSurface.DisposeContent.
            foreach (var child in _contentPanel.Children.ToArray())
            {
                child.Dispose();
            }

            _rows.Clear();
            _bannerPanel = null;
            _bannerLabel = null;

            AdoptRarityFromStatCache();

            int barWidth = Math.Max(0, _contentPanel.Width - ScrollbarAllowance);
            _lastLayoutWidth = _contentPanel.Width;

            BuildBanner(barWidth);

            if (Entries.Count == 0)
            {
                BuildEmptyState(barWidth);
            }
            else
            {
                foreach (int priorityIndex in DisplayOrder())
                {
                    _rows.Add(CreateRow(Entries[priorityIndex], priorityIndex, barWidth));
                }

                // Every row's cells are measured before any is rendered, so
                // the whole table shares one column geometry.
                RecomputeBandWidths();
                foreach (var row in _rows)
                {
                    RenderRowContent(row, Entries[row.Index], barWidth);
                }
            }

            _refreshButton.Enabled = !_isRefreshing && Entries.Count > 0;
            TooltipFacility.ApplyPlain(_refreshButton, Entries.Count > 0
                ? "Recalculate every row. Each item is solved twice, so the first refresh of a session can take a while."
                : "Add an item to your list first.");

            RebuildCaptions(barWidth);
            if (_contentPanel.Parent is Container container)
            {
                PositionChrome(container, container.ContentRegion.Width);
            }

            UpdateStatusLine();
        }

        /// <summary>
        /// Colours rows whose rarity this session already knows from some
        /// other tab's work, before their own first refresh has run. Cheap
        /// (one dictionary read per uncoloured row) and saves only when
        /// something actually changed, so the common case writes nothing.
        /// </summary>
        private void AdoptRarityFromStatCache()
        {
            if (_getItemStatBlock == null)
            {
                return;
            }

            if (RankerRarityAdoption.AdoptFromStatCache(Entries, _getItemStatBlock))
            {
                Persist();
            }
        }

        private void BuildBanner(int barWidth)
        {
            if (_getSnapshot() != null)
            {
                return;
            }

            _bannerPanel = new Panel
            {
                Size = new Point(barWidth, BannerHeight),
                Parent = _contentPanel,
            };
            _bannerLabel = new Label
            {
                Font = UiFonts.Body,
                Text = "No account snapshot available - every item will read 0% until you fetch one from the Snapshot tab.",
                TextColor = StatusColor,
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = Math.Max(0, barWidth - RankerRowLayout.Inset),
                Location = new Point(RankerRowLayout.Inset, 6),
                Parent = _bannerPanel,
            };
        }

        private static readonly string[] EmptyStateLines =
        {
            "Nothing on your priority list yet.",
            "",
            "Add the items you are working toward, in the order you want to finish them. The Ranker then answers a question the Crafting Plan tab cannot: given that everything above it already has first claim on your materials, your currencies and your daily crafts, how close is each one really?",
            "",
            "Every row scores five separate barriers - materials, account currencies, time-gated daily crafts, crafting disciplines and recipe unlocks - and combines only the ones that item actually has into one Ready percentage you can rank by.",
            "",
            "Search above to add your first item, then press Refresh.",
        };

        private void BuildEmptyState(int barWidth)
        {
            var panel = new Panel
            {
                Size = new Point(barWidth, 0),
                Parent = _contentPanel,
            };

            int usable = Math.Max(1, barWidth - 2 * RankerRowLayout.Inset);
            var measure = LabelHelpers.MeasureWith(UiFonts.Body);
            int y = 8;
            foreach (string paragraph in EmptyStateLines)
            {
                if (paragraph.Length == 0)
                {
                    y += CaptionLineHeight / 2;
                    continue;
                }

                foreach (string line in TextWrapMath.Wrap(paragraph, usable, usable, measure).Lines)
                {
                    new Label
                    {
                        Font = UiFonts.Body,
                        Text = line,
                        TextColor = DimColor,
                        AutoSizeWidth = false,
                        AutoSizeHeight = true,
                        Width = usable,
                        Location = new Point(8, y),
                        Parent = panel,
                    };
                    y += 20;
                }
            }

            panel.Size = new Point(barWidth, y + 8);
        }

        /// <summary>
        /// Widest coin cell across the whole table, so every row shares one
        /// column geometry and the header labels sit on the columns they
        /// name. Returns true when it changed.
        /// </summary>
        private bool RecomputeBandWidths()
        {
            int remaining = MeasureDashWidth();
            foreach (var row in _rows)
            {
                if (row.RemainingCellWidth > remaining)
                {
                    remaining = row.RemainingCellWidth;
                }
            }

            bool changed = remaining != _remainingBandWidth;
            _remainingBandWidth = remaining;
            return changed;
        }

        private RankerRowLayout.Bands BandsFor(int barWidth)
        {
            return RankerRowLayout.Compute(barWidth, _remainingBandWidth);
        }

        private RenderedRow CreateRow(RankerWatchlistEntry entry, int index, int barWidth)
        {
            var row = new RenderedRow
            {
                ItemId = entry.ItemId,
                Index = index,
                FullName = BuildDisplayName(entry),
            };
            _metricsByItemId.TryGetValue(entry.ItemId, out var metrics);
            row.Metrics = RankerPriorityOrdering.MetricsAreCurrent(metrics, index, Mode) ? metrics : null;

            row.Panel = new Panel
            {
                Size = new Point(barWidth, RankerRowLayout.RowHeight),
                Parent = _contentPanel,
            };

            MeasureRowCells(row);
            return row;
        }

        private void RenderRowContent(RenderedRow row, RankerWatchlistEntry entry, int barWidth)
        {
            // Dispose, not ClearChildren - see RebuildRows. This runs per
            // row per band-width recompute (a Refresh over a full
            // watchlist re-renders every row at least once, often twice),
            // so a detach-only clear orphans hundreds of controls per
            // click.
            foreach (var child in row.Panel.Children.ToArray())
            {
                child.Dispose();
            }

            row.GateNameLabels.Clear();
            row.GateValueLabels.Clear();
            row.CurrencyIconFrames.Clear();
            row.CurrencyNameLabels.Clear();
            row.CurrencyNameFulls.Clear();
            row.CurrencyValueLabels.Clear();
            row.NoteLabels.Clear();
            row.Chip = null;
            row.RemainingCell = null;
            row.RemainingDash = null;

            var metrics = row.Metrics;
            string chipText = ChipText(metrics);
            var bands = BandsFor(barWidth);

            row.RankLabel = new Label
            {
                Font = UiFonts.Caption,
                Text = (row.Index + 1).ToString(CultureInfo.InvariantCulture) + ".",
                TextColor = ValueTextColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(bands.RankX, MainLineRankY),
                Parent = row.Panel,
            };

            // The chip trails the name inside the name band (see
            // RankerRowLayout.Compute's comment), so the name's budget
            // reserves the chip's width first.
            row.IconName = IconNameRowHelpers.CreateIconAndEllipsizedName(
                row.Panel, entry.IconUrl, entry.Rarity,
                bands.IconX, MainLineIconY, row.FullName, UiFonts.Body,
                NameBudgetRightEdge(bands, row.ChipWidth), 0, 0, bands.NameX, MainLineTextY,
                iconSize: RankerRowLayout.IconSize);
            ApplyItemTooltip(row, entry);

            if (chipText != null)
            {
                ChipColors(metrics, out Color chipBorder, out Color chipFill);
                row.Chip = LabelHelpers.CreateSmallTag(
                    row.Panel, chipText, ChipXFor(row), MainLineChipY, chipBorder, chipFill);
                LabelHelpers.ApplyTagTooltip(row.Chip, ChipTooltip(metrics));
            }

            row.ReadyLabel = new Label
            {
                Font = UiFonts.Body,
                Text = metrics == null ? RankerReadinessCalculator.DashText : RankerReadinessCalculator.FormatReadiness(metrics),
                TextColor = metrics == null
                    ? RankerReadinessColors.Neutral
                    : metrics.Kind != RankerReadinessKind.Measured
                        ? ValueTextColor
                        : RankerReadinessColors.ForReadiness(metrics.Readiness),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, MainLineTextY),
                Parent = row.Panel,
            };
            TooltipFacility.ApplyPlain(row.ReadyLabel, ReadyTooltip(metrics));

            // Measured absences render at ValueTextColor: the field test
            // showed the Neutral dash disappearing into the background
            // under its own column header. Neutral is only for "not yet
            // calculated".
            row.DaysLabel = new Label
            {
                Font = UiFonts.Body,
                Text = metrics == null ? RankerReadinessCalculator.DashText : RankerReadinessCalculator.FormatDays(metrics),
                TextColor = metrics == null
                    ? RankerReadinessColors.Neutral
                    : metrics.DaysRemaining <= 0
                        ? ValueTextColor
                        : RankerReadinessColors.ForDays(metrics.DaysRemaining),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, MainLineTextY),
                Parent = row.Panel,
            };
            TooltipFacility.ApplyPlain(row.DaysLabel, DaysTooltip(metrics));

            if (metrics == null || metrics.RemainingCoinCost <= 0)
            {
                // The coin renderer's own zero-value cell is the gw2e-style
                // "not sold or crafted" em dash, which claims unpriceable.
                // A refreshed row's zero is a measured zero, so it gets the
                // module's plain dash and says why on hover instead.
                row.RemainingDash = new Label
                {
                    Font = UiFonts.Body,
                    Text = RankerReadinessCalculator.DashText,
                    TextColor = metrics == null ? RankerReadinessColors.Neutral : ValueTextColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(0, MainLineTextY),
                    Parent = row.Panel,
                };
                TooltipFacility.ApplyPlain(row.RemainingDash, metrics == null
                    ? "Not yet calculated - press Refresh."
                    : "Nothing left to buy - the materials you hold cover this item's coin cost.");
            }
            else
            {
                row.RemainingCell = CoinCurrencyRenderer.RenderValueCellRightAligned(
                    row.Panel, metrics.RemainingCoinCost, null, bands.RemainingRightEdge, MainLineTextY, UiFonts.Body);
            }

            row.Up = CreateRowButton(row.Panel, "\u25B2", bands.UpX, MoveUpTooltip());
            row.Down = CreateRowButton(row.Panel, "\u25BC", bands.DownX, MoveDownTooltip());
            row.Remove = CreateRowButton(row.Panel, "\u2715", bands.RemoveX,
                "Remove this item from your list.");

            row.Up.Enabled = CanReorder && RankerPriorityOrdering.CanMoveUp(row.Index, Entries.Count);
            row.Down.Enabled = CanReorder && RankerPriorityOrdering.CanMoveDown(row.Index, Entries.Count);
            row.Remove.Enabled = !_isRefreshing;

            int rowIndex = row.Index;
            row.Up.Click += (_, __) => MoveRow(rowIndex, up: true);
            row.Down.Click += (_, __) => MoveRow(rowIndex, up: false);
            row.Remove.Click += (_, __) => RemoveRow(rowIndex);

            int subLines = RenderSubLines(row, bands);
            row.Panel.Size = new Point(barWidth, RankerRowLayout.TotalRowHeight(subLines));

            LayoutRow(row, bands, measureText: true);
        }

        /// <summary>Cell widths only - no controls built, so it is safe before the table's bands are known.</summary>
        private static void MeasureRowCells(RenderedRow row)
        {
            string chipText = ChipText(row.Metrics);
            row.ChipWidth = chipText == null ? 0 : LabelHelpers.MeasureSmallTagWidth(chipText);
            row.RemainingCellWidth = row.Metrics == null || row.Metrics.RemainingCoinCost <= 0
                ? MeasureDashWidth()
                : CoinCurrencyRenderer.MeasureValueWidth(row.Metrics.RemainingCoinCost, null, UiFonts.Body);
        }

        private FeedbackButton CreateRowButton(Panel parent, string glyph, int x, string tooltip)
        {
            var button = new FeedbackButton
            {
                Text = glyph,
                Size = new Point(RankerRowLayout.ButtonWidth, UiMetrics.ButtonHeight),
                Location = new Point(x, MainLineButtonY),
                Parent = parent,
            };
            TooltipFacility.ApplyPlain(button, tooltip);
            return button;
        }

        /// <summary>Returns the number of sub-lines rendered.</summary>
        private int RenderSubLines(RenderedRow row, in RankerRowLayout.Bands bands)
        {
            var metrics = row.Metrics;
            if (metrics == null)
            {
                return 0;
            }

            int line = 0;

            // The gate breakdown, justified across the full sub-line band so
            // the five barriers read as one strip rather than a left-packed
            // sentence with dead space to its right.
            int gateY = RankerRowLayout.RowHeight + line * RankerRowLayout.SubLineHeight;
            for (int i = 0; i < metrics.Gates.Count && i < RankerRowLayout.GateCellCount; i++)
            {
                var gate = metrics.Gates[i];
                row.GateNameLabels.Add(new Label
                {
                    Font = UiFonts.Caption,
                    Text = RankerReadinessCalculator.GateLabel(gate.Gate),
                    TextColor = Color.White,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(0, gateY),
                    Parent = row.Panel,
                });
                row.GateValueLabels.Add(new Label
                {
                    Font = UiFonts.Caption,
                    Text = RankerReadinessCalculator.FormatGate(gate),
                    TextColor = gate.Applies
                        ? RankerReadinessColors.ForReadiness(gate.Completion)
                        : ValueTextColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(0, gateY),
                    Parent = row.Panel,
                });
            }

            line++;

            int currencyLines = RankerRowLayout.CurrencyLineCount(metrics.CurrencyShortfalls.Count);
            int shown = Math.Min(
                metrics.CurrencyShortfalls.Count,
                RankerRowLayout.CurrenciesPerLine * RankerRowLayout.MaxCurrencyLines);
            for (int i = 0; i < shown; i++)
            {
                var shortfall = metrics.CurrencyShortfalls[i];
                int y = RankerRowLayout.RowHeight
                    + (line + i / RankerRowLayout.CurrenciesPerLine) * RankerRowLayout.SubLineHeight;

                string fullName = CurrencyName(shortfall);
                row.CurrencyNameFulls.Add(fullName);
                row.CurrencyIconFrames.Add(IconControls.CreateItemIcon(
                    row.Panel, CurrencyIconUrl(shortfall), (string)null,
                    0, y + 1, RankerRowLayout.CurrencyIconSize, 1, fullName));
                row.CurrencyNameLabels.Add(new Label
                {
                    Font = UiFonts.Caption,
                    Text = fullName,
                    TextColor = Color.White,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(0, y),
                    Parent = row.Panel,
                });
                row.CurrencyValueLabels.Add(new Label
                {
                    Font = UiFonts.Caption,
                    Text = FormatShortfall(shortfall),
                    TextColor = shortfall.Short > 0 ? ValueTextColor : RankerReadinessColors.ForReadiness(1.0),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(0, y),
                    Parent = row.Panel,
                });
            }

            line += currencyLines;

            foreach (string note in BuildNotes(metrics))
            {
                row.NoteLabels.Add(new Label
                {
                    Font = UiFonts.Caption,
                    Text = note,
                    TextColor = ValueTextColor,
                    AutoSizeWidth = false,
                    AutoSizeHeight = true,
                    Width = Math.Max(0, bands.SubLineWidth),
                    Location = new Point(bands.SubLineX,
                        RankerRowLayout.RowHeight + line * RankerRowLayout.SubLineHeight),
                    Parent = row.Panel,
                });
                line++;
            }

            return line;
        }

        private void LayoutRow(RenderedRow row, in RankerRowLayout.Bands bands, bool measureText)
        {
            row.Panel.Size = new Point(bands.RowWidth, row.Panel.Height);
            row.RankLabel.Location = new Point(bands.RankX, MainLineRankY);

            if (measureText)
            {
                // The rich deferred tooltip already carries the full name,
                // so a truncation change needs no re-stamp here.
                IconNameRowHelpers.ReellipsizeName(row.IconName, UiFonts.Body,
                    NameBudgetRightEdge(bands, row.ChipWidth), 0, 0);
            }

            row.IconName.IconFrame.Location = new Point(bands.IconX, row.IconName.IconFrame.Location.Y);
            row.IconName.NameLabel.Location = new Point(bands.NameX, row.IconName.NameLabel.Location.Y);

            row.ReadyLabel.Location = new Point(
                Math.Max(0, bands.ReadyRightEdge - row.ReadyLabel.Width), MainLineTextY);

            if (row.Chip != null)
            {
                row.Chip.Location = new Point(ChipXFor(row), MainLineChipY);
            }

            row.DaysLabel.Location = new Point(
                Math.Max(0, bands.DaysRightEdge - row.DaysLabel.Width), MainLineTextY);

            if (row.RemainingDash != null)
            {
                row.RemainingDash.Location = new Point(
                    Math.Max(0, bands.RemainingRightEdge - row.RemainingDash.Width), MainLineTextY);
            }
            else if (row.RemainingCell != null)
            {
                CoinCurrencyRenderer.RepositionValueCellRightAligned(
                    row.RemainingCell, bands.RemainingRightEdge, MainLineTextY);
            }

            row.Up.Location = new Point(bands.UpX, MainLineButtonY);
            row.Down.Location = new Point(bands.DownX, MainLineButtonY);
            row.Remove.Location = new Point(bands.RemoveX, MainLineButtonY);

            for (int i = 0; i < row.GateNameLabels.Count; i++)
            {
                RankerRowLayout.GateCell(bands, i, out int cellX, out int cellWidth);
                row.GateNameLabels[i].Location = new Point(cellX, row.GateNameLabels[i].Location.Y);
                var value = row.GateValueLabels[i];
                value.Location = new Point(
                    Math.Max(cellX, cellX + cellWidth - value.Width - RankerRowLayout.CellGap),
                    value.Location.Y);
            }

            for (int i = 0; i < row.CurrencyNameLabels.Count; i++)
            {
                // The currency list's own indented grid - deliberately NOT
                // the gate rails; see RankerRowLayout.CurrenciesPerLine.
                RankerRowLayout.CurrencyCell(bands, i % RankerRowLayout.CurrenciesPerLine,
                    out int cellX, out int cellWidth);

                var icon = row.CurrencyIconFrames[i];
                var name = row.CurrencyNameLabels[i];
                var value = row.CurrencyValueLabels[i];
                int nameX = cellX + RankerRowLayout.CurrencyIconSize + 2
                    + RankerRowLayout.CurrencyIconGap;
                int valueX = Math.Max(nameX, cellX + cellWidth - value.Width - RankerRowLayout.CellGap);

                icon.Location = new Point(cellX, icon.Location.Y);
                name.Location = new Point(nameX, name.Location.Y);
                value.Location = new Point(valueX, value.Location.Y);

                if (measureText)
                {
                    // A long currency name must clear the value in its own
                    // cell rather than running under it.
                    string full = row.CurrencyNameFulls[i];
                    string shown = LabelHelpers.EllipsizeToWidth(
                        UiFonts.Caption, full, Math.Max(0, valueX - nameX - RankerRowLayout.IconGap));
                    if (!string.Equals(name.Text, shown, StringComparison.Ordinal))
                    {
                        name.Text = shown;
                    }

                    TooltipFacility.ApplyPlain(name,
                        string.Equals(shown, full, StringComparison.Ordinal) ? null : full);
                }
            }

            foreach (var note in row.NoteLabels)
            {
                note.Location = new Point(bands.SubLineX, note.Location.Y);
                note.Width = Math.Max(0, bands.SubLineWidth);
            }
        }

        private void RefitRows()
        {
            if (!IsLive || _contentPanel.Width == _lastLayoutWidth)
            {
                return;
            }

            RefitEveryRow(measureText: false);
            _resizeSettle.Schedule();
        }

        private void RefitAfterResizeSettle()
        {
            if (!IsLive)
            {
                return;
            }

            RefitEveryRow(measureText: true);
            RebuildCaptions(Math.Max(0, _contentPanel.Width - ScrollbarAllowance));
        }

        private void RefitEveryRow(bool measureText)
        {
            int barWidth = Math.Max(0, _contentPanel.Width - ScrollbarAllowance);
            _lastLayoutWidth = _contentPanel.Width;

            _contentPanel.SuspendLayout();
            try
            {
                if (_bannerPanel != null)
                {
                    _bannerPanel.Size = new Point(barWidth, BannerHeight);
                    _bannerLabel.Width = Math.Max(0, barWidth - RankerRowLayout.Inset);
                }

                var bands = BandsFor(barWidth);
                foreach (var row in _rows)
                {
                    LayoutRow(row, bands, measureText);
                }
            }
            finally
            {
                _contentPanel.ResumeLayout(false);
            }

            PositionColumnHeader(barWidth);
        }

        // ---------------------------------------------------------------
        // Row text
        // ---------------------------------------------------------------
        private static string BuildDisplayName(RankerWatchlistEntry entry)
        {
            string name = string.IsNullOrEmpty(entry.Name) ? "Unknown item" : entry.Name;
            return entry.Quantity > 1
                ? name + " x" + entry.Quantity.ToString(CultureInfo.InvariantCulture)
                : name;
        }

        private static string ChipText(RankerRowMetrics metrics)
        {
            if (metrics == null || !metrics.HasSnapshot)
            {
                return null;
            }

            return metrics.AffordableNow
                ? "Affordable now"
                : "Short " + CoinSegmentMath.GameStyleText(metrics.ShortfallCoin);
        }

        /// <summary>
        /// Affordable reuses the module's proven badge combination (the
        /// decision pills' darkened CRAFT green, already reused by the
        /// summary's full-coverage "OK" tag); Short takes the shopping
        /// source tags' recessed Locked plate. Both carry CreateSmallTag's
        /// white label at field-proven contrast - the pale readiness green
        /// behind white text was the reported unreadable case.
        /// </summary>
        private static void ChipColors(RankerRowMetrics metrics, out Color border, out Color fill)
        {
            if (metrics != null && metrics.AffordableNow)
            {
                border = AffordableChipBorder;
                fill = AffordableChipFill;
                return;
            }

            PillColors.GetPillColors(PillKind.Locked, false, out border, out fill);
        }

        /// <summary>Chip x: trailing the name label, inside the name band.</summary>
        private static int ChipXFor(RenderedRow row)
        {
            return row.IconName.NameLabel.Location.X + row.IconName.NameLabel.Width + 8;
        }

        /// <summary>The name's right budget, with the chip's width reserved out of it.</summary>
        private static int NameBudgetRightEdge(in RankerRowLayout.Bands bands, int chipWidth)
        {
            int rightEdge = bands.NameX + bands.NameWidth;
            return chipWidth > 0 ? Math.Max(bands.NameX, rightEdge - chipWidth - 8) : rightEdge;
        }

        /// <summary>
        /// The standard rich item tooltip on the icon and name, stamped
        /// deferred so a stat block the session caches later shows without
        /// a re-render, falling back to the plain full name until then -
        /// the same shape MainView.ApplyItemRowTooltip established.
        /// </summary>
        private void ApplyItemTooltip(RenderedRow row, RankerWatchlistEntry entry)
        {
            int itemId = entry.ItemId;
            string fullName = row.FullName;
            Func<TooltipContent> build = () => ItemRowTooltipComposer.BuildRowContent(
                _getItemStatBlock == null || itemId <= 0 ? null : _getItemStatBlock(itemId),
                fullName,
                true,
                (IReadOnlyList<string>)null);

            TooltipFacility.ApplyRichDeferred(row.IconName.NameLabel, build);
            IconControls.ApplyRichDeferredToIconTree(row.IconName.IconFrame, build);
        }

        private bool CanReorder => !_isRefreshing && Mode == RankerMode.Cascade;

        private string MoveUpTooltip()
        {
            return Mode == RankerMode.Independent
                ? "Priority order applies in \"" + CascadeModeItem + "\" mode - switch back to change it."
                : "Raise this item's priority. It then has first claim on materials the row above it was using.";
        }

        private string MoveDownTooltip()
        {
            return Mode == RankerMode.Independent
                ? "Priority order applies in \"" + CascadeModeItem + "\" mode - switch back to change it."
                : "Lower this item's priority.";
        }

        private string ChipTooltip(RankerRowMetrics metrics)
        {
            if (metrics == null)
            {
                return null;
            }

            bool independent = metrics.Mode == RankerMode.Independent;
            if (metrics.AffordableNow)
            {
                return independent
                    ? "You have enough coin for what is left of this item, measured against your full account."
                    : "You have enough coin for what is left of this item, after paying for everything above it on the list.";
            }

            return "You are " + CoinSegmentMath.GameStyleText(metrics.ShortfallCoin) +
                (independent
                    ? " short of what is left of this item, measured against your full account."
                    : " short of what is left of this item, counting coin that the higher-priority items above it would already have spent.");
        }

        private static int MeasureDashWidth()
        {
            return LabelHelpers.MeasureWith(UiFonts.Body)(RankerReadinessCalculator.DashText);
        }

        private string CurrencyName(RankerCurrencyShortfall shortfall)
        {
            return CurrencyDisplayResolver.ResolveName(shortfall.CurrencyId, CurrencyMetadataFor(shortfall.CurrencyId));
        }

        private string CurrencyIconUrl(RankerCurrencyShortfall shortfall)
        {
            var metadata = CurrencyMetadataFor(shortfall.CurrencyId);
            return metadata != null && metadata.TryGetValue(shortfall.CurrencyId, out var entry)
                ? entry?.IconUrl
                : null;
        }

        private IReadOnlyDictionary<int, CurrencyMetadata> CurrencyMetadataFor(int currencyId)
        {
            foreach (var result in _lastOwnedResults.Values)
            {
                if (result?.CurrencyMetadata != null && result.CurrencyMetadata.ContainsKey(currencyId))
                {
                    return result.CurrencyMetadata;
                }
            }

            return null;
        }

        private static string FormatShortfall(RankerCurrencyShortfall shortfall)
        {
            if (shortfall.Short <= 0)
            {
                return "covered";
            }

            return shortfall.Short.ToString("N0", CultureInfo.InvariantCulture) + " short";
        }

        private IEnumerable<string> BuildNotes(RankerRowMetrics metrics)
        {
            var notes = new List<string>();

            var missing = metrics.DisciplineGaps
                .Where(g => g.BestRating < g.RequiredRating)
                .ToList();
            if (missing.Count > 0)
            {
                var gap = missing[0];
                string text = gap.BestRating <= 0
                    ? "Needs " + gap.Discipline + " at " + gap.RequiredRating.ToString(CultureInfo.InvariantCulture) +
                      " - no character has learned it"
                    : "Needs " + gap.Discipline + " at " + gap.RequiredRating.ToString(CultureInfo.InvariantCulture) +
                      " - your best is " + gap.BestRating.ToString(CultureInfo.InvariantCulture);
                if (missing.Count > 1)
                {
                    text += " (and " + (missing.Count - 1).ToString(CultureInfo.InvariantCulture) + " more)";
                }

                notes.Add(text);
            }

            if (metrics.ContestedItemCount > 0 || metrics.ContestedCurrencyCount > 0)
            {
                var parts = new List<string>();
                if (metrics.ContestedItemCount > 0)
                {
                    parts.Add(StatusText.Count(metrics.ContestedItemCount, "material"));
                }

                if (metrics.ContestedCurrencyCount > 0)
                {
                    parts.Add(StatusText.Count(metrics.ContestedCurrencyCount, "currency", "currencies"));
                }

                notes.Add("Claimed by higher priority: " + string.Join(", ", parts));
            }

            foreach (var capped in metrics.VendorCappedItems)
            {
                string name = VendorCappedName(capped.ItemId);
                if (name == null)
                {
                    // Never render an id. No name means no note.
                    continue;
                }

                // Same wording as the plan tab's TimegatedNotice rows, and
                // named for what it is - a vendor purchase limit, not an
                // earning cooldown (the calculator already drops caps on
                // TP-liquid items, where the cap is coin rather than time).
                notes.Add(name + " is timegated - vendor " + CapLabel(capped.CapType) +
                    " limit: " + capped.CapValue.ToString(CultureInfo.InvariantCulture) +
                    " (plan needs " + capped.NeededCount.ToString(CultureInfo.InvariantCulture) + ")");
                break;
            }

            return notes;
        }

        private static string CapLabel(TimegatedCapType capType)
        {
            switch (capType)
            {
                case TimegatedCapType.Daily: return "Daily";
                case TimegatedCapType.Weekly: return "Weekly";
                default: return "Season";
            }
        }

        private string VendorCappedName(int itemId)
        {
            foreach (var result in _lastOwnedResults.Values)
            {
                if (result?.ItemMetadata != null &&
                    result.ItemMetadata.TryGetValue(itemId, out var meta) &&
                    !string.IsNullOrEmpty(meta.Name))
                {
                    return meta.Name;
                }
            }

            return null;
        }

        private static string ReadyTooltip(RankerRowMetrics metrics)
        {
            if (metrics == null)
            {
                return "Not yet calculated - press Refresh.";
            }

            if (metrics.Kind != RankerReadinessKind.Measured)
            {
                return "This item has no measurable barrier left that the Ranker can score. Read the lines under the row for what is actually outstanding.";
            }

            var lines = new List<string>
            {
                "Ready blends the barriers this item actually has, each measured only against itself - nothing is converted into coin.",
                "",
            };
            foreach (var gate in metrics.Gates)
            {
                string label = RankerReadinessCalculator.GateLabel(gate.Gate);
                lines.Add(gate.Applies
                    ? label + ": " + RankerReadinessCalculator.FormatPercent(gate.Completion) +
                      " at weight " + gate.Weight.ToString("0.00", CultureInfo.InvariantCulture)
                    : label + ": this item has none");
            }

            lines.Add("");
            lines.Add("Weights are renormalised over the barriers that apply, so an item with only materials scores exactly its materials figure.");
            return string.Join("\n", lines);
        }

        private static string DaysTooltip(RankerRowMetrics metrics)
        {
            if (metrics == null)
            {
                return "Not yet calculated - press Refresh.";
            }

            if (metrics.DaysRemaining <= 0)
            {
                return "Nothing in this item's plan is a once-per-day craft.";
            }

            string text = "The earliest day this could finish, counting from today. Its plan needs " +
                StatusText.Count(metrics.DaysRemaining, "day") +
                " of once-per-day crafts, which no amount of coin shortens.";
            int queued = metrics.DaysRemaining - metrics.DaysAlone;
            if (queued > 0)
            {
                text += " " + StatusText.Count(queued, "day") +
                    " of that is spent on higher-priority items first - move this row up to take those days back.";
            }

            return text;
        }

        // ---------------------------------------------------------------
        // Mutations
        // ---------------------------------------------------------------
        private void AddPendingItem()
        {
            if (_isRefreshing || !_pendingItemId.HasValue)
            {
                return;
            }

            int quantity = ItemRowRequestBuilder.NormalizeQuantity(_quantityBox.Text);
            int existing = RankerPriorityOrdering.IndexOfItem(Entries, _pendingItemId.Value);

            if (existing >= 0)
            {
                // Updating in place rather than re-prioritising: the list is a
                // priority order, and silently moving an item because the user
                // re-searched it is the surprising outcome.
                Entries[existing].Quantity = quantity;
                InvalidateAfterChangeAt(existing);
                SetStatus($"{Entries[existing].Name} is already on your list - quantity updated to {quantity}.", isError: false);
            }
            else if (Entries.Count >= RankerWatchlistLimits.MaxEntries)
            {
                SetStatus($"Your list is full ({RankerWatchlistLimits.MaxEntries} items). Remove one to add another.", isError: false);
                return;
            }
            else
            {
                Entries.Add(new RankerWatchlistEntry
                {
                    ItemId = _pendingItemId.Value,
                    Quantity = quantity,
                    Name = _pendingItemName,
                    IconUrl = _pendingItemIconUrl,
                });
                SetStatus("Added " + _pendingItemName, isError: false);
            }

            _pendingItemId = null;
            _pendingItemName = null;
            _pendingItemIconUrl = null;
            _searchBox.Text = "";
            _quantityBox.Text = "1";
            _suggestionPanel?.HidePanel();

            Persist();
            RebuildRows();
            UpdateAddButtonState();
        }

        private void MoveRow(int index, bool up)
        {
            if (!CanReorder)
            {
                return;
            }

            int invalidatedFrom = up
                ? RankerPriorityOrdering.MoveUp(Entries, index)
                : RankerPriorityOrdering.MoveDown(Entries, index);

            if (invalidatedFrom == RankerPriorityOrdering.NoInvalidation)
            {
                return;
            }

            InvalidateFrom(invalidatedFrom);
            Persist();
            RebuildRows();
            SetStatus("Order changed - press Refresh to recalculate the rows below it.", isError: false);
        }

        private void RemoveRow(int index)
        {
            if (_isRefreshing || index < 0 || index >= Entries.Count)
            {
                return;
            }

            string name = Entries[index].Name;
            int removedItemId = Entries[index].ItemId;
            int invalidatedFrom = RankerPriorityOrdering.RemoveAt(Entries, index);
            _metricsByItemId.Remove(removedItemId);
            _lastOwnedResults.Remove(removedItemId);
            if (Mode == RankerMode.Cascade)
            {
                // Independent rows are position-free, so the survivors'
                // numbers are untouched by a removal.
                InvalidateFrom(invalidatedFrom);
            }

            Persist();
            RebuildRows();
            SetStatus("Removed " + name, isError: false);
            UpdateAddButtonState();
        }

        /// <summary>
        /// Drops every cached metric from <paramref name="index"/> down. A
        /// row's numbers are a function of its POSITION under the cascade, so
        /// a row that moved is showing a figure for a slot it no longer
        /// occupies. Rows above the change are genuinely unaffected: the
        /// cascade never depends on anything below.
        /// </summary>
        private void InvalidateFrom(int index)
        {
            if (index < 0)
            {
                return;
            }

            for (int i = index; i < Entries.Count; i++)
            {
                _metricsByItemId.Remove(Entries[i].ItemId);
            }
        }

        /// <summary>
        /// Invalidation after the entry at index changed in place (a
        /// quantity update): Cascade stales it and everything below it;
        /// Independent rows are position-free, so only the row itself.
        /// </summary>
        private void InvalidateAfterChangeAt(int index)
        {
            if (Mode == RankerMode.Cascade)
            {
                InvalidateFrom(index);
            }
            else if (index >= 0 && index < Entries.Count)
            {
                _metricsByItemId.Remove(Entries[index].ItemId);
            }
        }

        private void Persist()
        {
            if (_store != null && !_store.Save(_watchlist))
            {
                SetStatus("Your list could not be saved - see the Log tab.", isError: true);
            }
        }

        private void UpdateAddButtonState()
        {
            if (_addButton == null)
            {
                return;
            }

            _addButton.Enabled = !_isRefreshing && _pendingItemId.HasValue;
        }

        // ---------------------------------------------------------------
        // Refresh
        // ---------------------------------------------------------------
        private void OnRefreshClicked()
        {
            // Disabled while a run is in flight (the module's standing
            // long-run pattern - see the plan tab's Generate button), so no
            // cancel path hangs off this click.
            if (_isRefreshing || Entries.Count == 0)
            {
                return;
            }

            StartRefresh();
        }

        private void StartRefresh()
        {
            int myGen = ++_refreshGeneration;
            var cts = new CancellationTokenSource();
            _refreshCts = cts;
            _isRefreshing = true;
            SetControlsEnabled(false);
            _spinner.Visible = true;

            var entries = Entries.Select(e => new RankerWatchlistEntry
            {
                ItemId = e.ItemId,
                Quantity = e.Quantity,
                Name = e.Name,
                IconUrl = e.IconUrl,
                Rarity = e.Rarity,
            }).ToList();

            // Read on the main thread; the run keeps this mode even if a
            // toggle could slip in mid-run.
            var mode = Mode;

            Task.Run(() => RunRefreshAsync(entries, mode, myGen, cts.Token));
        }

        private async Task RunRefreshAsync(
            List<RankerWatchlistEntry> entries, RankerMode mode, int myGen, CancellationToken ct)
        {
            int updated = 0;
            string failure = null;
            bool cancelled = false;

            try
            {
                // Read once per run, so every row in a run is measured against
                // the same account state.
                var snapshot = _getSnapshot();
                string activeCharacter = _getActiveCharacterName();
                var valuation = _settings?.GetEffectiveCurrencyValuation();
                var homesteadTiers = _settings?.GetHomesteadEfficiencyTiers();
                var cascade = new RankerPriorityCascade(snapshot);

                // Independent mode is slot 1 semantics for EVERY row: one
                // unconsumed availability (the full account), no Consume
                // threading between rows. Still 2N solves either way - the
                // mode only changes which snapshot the owned solve sees,
                // which is why toggling stales metrics instead of re-solving.
                var fullAvailability = mode == RankerMode.Independent
                    ? cascade.CurrentAvailability
                    : null;

                for (int i = 0; i < entries.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var entry = entries[i];
                    int slot = i;
                    string name = entry.Name;
                    int position = i + 1;
                    int total = entries.Count;
                    MainThreadMarshal.Run(() => ReportProgress(myGen, position, total, name));

                    var availability = fullAvailability ?? cascade.CurrentAvailability;

                    var baseline = await _pipeline.GenerateStructuredAsync(
                        entry.ItemId, entry.Quantity, null, ct, null,
                        activeCharacterName: null,
                        priceBasis: PriceBasis.BuyOrder,
                        currencyValuation: valuation,
                        ownMaterialsMode: OwnMaterialsMode.Free,
                        homesteadTiers: homesteadTiers,
                        phaseProgress: null,
                        characterDisciplines: snapshot?.CharacterDisciplines).ConfigureAwait(false);

                    if (myGen != _refreshGeneration)
                    {
                        return;
                    }

                    ct.ThrowIfCancellationRequested();

                    var owned = await _pipeline.GenerateStructuredAsync(
                        entry.ItemId, entry.Quantity, availability.Snapshot, ct, null,
                        activeCharacterName: activeCharacter,
                        priceBasis: PriceBasis.BuyOrder,
                        currencyValuation: valuation,
                        ownMaterialsMode: OwnMaterialsMode.Free,
                        homesteadTiers: homesteadTiers,
                        phaseProgress: null,
                        characterDisciplines: snapshot?.CharacterDisciplines).ConfigureAwait(false);

                    if (myGen != _refreshGeneration)
                    {
                        return;
                    }

                    ct.ThrowIfCancellationRequested();

                    var metrics = RankerReadinessCalculator.Compute(baseline, owned, availability, slot, mode);
                    if (mode == RankerMode.Cascade)
                    {
                        cascade.Consume(owned);
                    }

                    updated++;

                    int itemId = entry.ItemId;
                    MainThreadMarshal.Run(() => ApplyRowMetrics(myGen, itemId, metrics, owned));
                }
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                failure = ex.Message;
                Logger.Warn(ex, "Ranker refresh failed");
            }

            int finalUpdated = updated;
            string finalFailure = failure;
            bool wasCancelled = cancelled;
            MainThreadMarshal.Run(() => FinishRefresh(myGen, finalUpdated, finalFailure, wasCancelled));
        }

        private void ReportProgress(int myGen, int position, int total, string name)
        {
            if (myGen != _refreshGeneration || !_buildComplete || !IsLive)
            {
                return;
            }

            string text = $"Refreshing {position} of {total} - {name}";
            if (!_firstRefreshDone)
            {
                text += ". The first refresh of a session downloads recipe data and can take a while.";
            }

            SetStatus(text, isError: false);
        }

        private void ApplyRowMetrics(int myGen, int itemId, RankerRowMetrics metrics, CraftingPlanResult owned)
        {
            if (myGen != _refreshGeneration)
            {
                return;
            }

            var entry = Entries.FirstOrDefault(e => e.ItemId == itemId);

            // Adopted BEFORE the liveness gate below: the solve's metadata
            // knows the rarity the Add-time search result never carried, and
            // that is a fact about the item rather than about the view, so a
            // run the user tabbed away from must not lose it. Persisted once
            // per run, in FinishRefresh, which is not gated either.
            if (RankerRarityAdoption.AdoptFromMetadata(entry, owned?.ItemMetadata))
            {
                _rarityDirty = true;
            }

            if (!_buildComplete || !IsLive)
            {
                return;
            }

            _metricsByItemId[itemId] = metrics;
            _lastOwnedResults[itemId] = owned;

            var row = _rows.FirstOrDefault(r => r.ItemId == itemId);
            if (row == null || entry == null)
            {
                return;
            }

            row.Metrics = metrics;
            MeasureRowCells(row);

            int barWidth = Math.Max(0, _contentPanel.Width - ScrollbarAllowance);
            if (RecomputeBandWidths())
            {
                // A wider coin cell moves the Ready and Days columns for
                // EVERY row, so they all have to follow rather than drifting
                // out of alignment as results land one at a time.
                foreach (var each in _rows)
                {
                    if (each.Index < Entries.Count)
                    {
                        RenderRowContent(each, Entries[each.Index], barWidth);
                    }
                }

                return;
            }

            RenderRowContent(row, entry, barWidth);
        }

        private void FinishRefresh(int myGen, int updated, string failure, bool cancelled)
        {
            if (myGen != _refreshGeneration)
            {
                return;
            }

            _isRefreshing = false;
            _firstRefreshDone = true;
            _lastRefreshLocal = DateTime.Now;

            if (_rarityDirty)
            {
                // Rarity adopted from the run's solves (see ApplyRowMetrics)
                // is worth keeping across sessions; one save per run.
                _rarityDirty = false;
                Persist();
            }

            if (!_buildComplete || !IsLive)
            {
                return;
            }

            _spinner.Visible = false;
            SetControlsEnabled(true);

            // Independent mode's display order is the refresh's answer -
            // re-sort now that the metrics are in.
            if (Mode == RankerMode.Independent)
            {
                RebuildRows();
            }

            if (failure != null)
            {
                SetStatus(StatusText.ForGenerationFailure(failure), isError: true);
            }
            else if (cancelled)
            {
                SetStatus("Refresh cancelled - " + StatusText.Count(updated, "item") + " updated", isError: false);
            }
            else
            {
                _statusOverride = null;
                UpdateStatusLine();
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            if (!IsLive)
            {
                return;
            }

            _addButton.Enabled = enabled && _pendingItemId.HasValue;
            _searchBox.Enabled = enabled;
            _quantityBox.Enabled = enabled;
            _modeDropdown.Enabled = enabled;
            _refreshButton.Enabled = enabled && Entries.Count > 0;
            foreach (var row in _rows)
            {
                bool reorder = enabled && Mode == RankerMode.Cascade;
                row.Up.Enabled = reorder && RankerPriorityOrdering.CanMoveUp(row.Index, Entries.Count);
                row.Down.Enabled = reorder && RankerPriorityOrdering.CanMoveDown(row.Index, Entries.Count);
                row.Remove.Enabled = enabled;
            }
        }

        // ---------------------------------------------------------------
        // Status line
        // ---------------------------------------------------------------
        private void SetStatus(string text, bool isError)
        {
            _statusOverride = text;
            _statusIsError = isError;
            ApplyStatusText(text, isError);
        }

        private void UpdateStatusLine()
        {
            if (_statusOverride != null)
            {
                ApplyStatusText(_statusOverride, _statusIsError);
                return;
            }

            if (Entries.Count == 0)
            {
                ApplyStatusText("Add the items you are working toward, in priority order.", isError: false);
                return;
            }

            if (!_lastRefreshLocal.HasValue)
            {
                ApplyStatusText("Not yet calculated - press Refresh.", isError: false);
                return;
            }

            string text = StatusText.Stamp("Refreshed", _lastRefreshLocal.Value);
            var snapshot = _getSnapshot();
            if (snapshot != null)
            {
                text += " - " + StatusText.ForSnapshotAgeSuffix(DateTime.UtcNow - snapshot.CapturedAt);
            }

            ApplyStatusText(text, isError: false);
        }

        private void ApplyStatusText(string text, bool isError)
        {
            if (_statusLabel == null)
            {
                return;
            }

            string shown = LabelHelpers.EllipsizeToWidth(UiFonts.Status, text, Math.Max(0, _statusLabel.Width));
            _statusLabel.Text = shown;
            _statusLabel.TextColor = isError ? ErrorColor : StatusColor;
            TooltipFacility.ApplyPlain(_statusLabel, string.Equals(shown, text, StringComparison.Ordinal) ? null : text);
            InlineSpinner.PlaceAfter(_spinner, _statusLabel, InlineSpinnerLayout.LabelGap);
        }

        private void UpdateBanner()
        {
            bool wantsBanner = _getSnapshot() == null;
            if (wantsBanner == (_bannerPanel != null))
            {
                return;
            }

            RebuildRows();
        }
    }
}
