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
        private const int RefreshButtonWidth = 132;
        private const int BannerHeight = 30;

        private static readonly Color DimColor = new Color(150, 150, 150);
        private static readonly Color StatusColor = new Color(200, 200, 200);
        private static readonly Color ErrorColor = new Color(255, 100, 100);
        private static readonly Color SectionDividerColor = new Color(130, 130, 130);

        private readonly CraftingPlanPipeline _pipeline;
        private readonly IItemSearchProvider _itemSearchProvider;
        private readonly ModuleSettings _settings;
        private readonly RankerStore _store;
        private readonly Func<AccountSnapshot> _getSnapshot;
        private readonly Func<string> _getActiveCharacterName;
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

        // Table-wide, not per row: the Ready, chip and Days cells all sit to
        // the LEFT of the coin cell, so letting each row size its own coin
        // band would put those three columns in a different place on every
        // row and leave the header labelling nothing.
        private int _remainingBandWidth;
        private int _chipBandWidth;
        private int _refreshGeneration;
        private CancellationTokenSource _refreshCts;
        private bool _isRefreshing;
        private bool _firstRefreshDone;
        private DateTime? _lastRefreshLocal;
        private string _statusOverride;
        private bool _statusIsError;

        public RankerTabContent(
            CraftingPlanPipeline pipeline,
            IItemSearchProvider itemSearchProvider,
            ModuleSettings settings,
            RankerStore store,
            Func<AccountSnapshot> getSnapshot,
            Func<string> getActiveCharacterName)
        {
            _pipeline = pipeline;
            _itemSearchProvider = itemSearchProvider;
            _settings = settings;
            _store = store;
            _getSnapshot = getSnapshot ?? (() => null);
            _getActiveCharacterName = getActiveCharacterName ?? (() => null);
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
                // so the in-flight state has to be restamped onto it.
                if (_isRefreshing)
                {
                    _refreshButton.Text = "Refreshing... (click to cancel)";
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
                Size = new Point(RefreshButtonWidth, UiMetrics.ButtonHeight),
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

            _refreshButton.Location = new Point(
                Math.Max(0, barWidth - RefreshButtonWidth), _refreshButton.Location.Y);

            int statusRight = _refreshButton.Location.X - InlineSpinnerLayout.SnapshotStatusSize
                - 2 * InlineSpinnerLayout.LabelGap;
            _statusLabel.Width = Math.Max(0, statusRight - RankerRowLayout.Inset);
            InlineSpinner.PlaceAfter(_spinner, _statusLabel, InlineSpinnerLayout.LabelGap);

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
            "Each item is measured against what the items above it leave behind: higher-priority rows have first claim on your materials, currencies, coin and daily crafts. Move a row up to give it that claim instead.",
            "Ready blends four separate barriers - materials at buy-order prices, account currencies, time-gated daily crafts and crafting disciplines - and counts only the ones this item actually has. Hover it for the breakdown.",
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

            int barWidth = Math.Max(0, _contentPanel.Width - ScrollbarAllowance);
            _lastLayoutWidth = _contentPanel.Width;

            BuildBanner(barWidth);

            if (Entries.Count == 0)
            {
                BuildEmptyState(barWidth);
            }
            else
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    _rows.Add(CreateRow(Entries[i], i, barWidth));
                }

                // Every row's cells are measured before any is rendered, so
                // the whole table shares one column geometry.
                RecomputeBandWidths();
                for (int i = 0; i < _rows.Count; i++)
                {
                    RenderRowContent(_rows[i], Entries[i], barWidth);
                }
            }

            _refreshButton.Enabled = Entries.Count > 0;
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
                TextColor = DimColor,
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
            "Every row scores four separate barriers - materials, account currencies, time-gated daily crafts and crafting disciplines - and combines only the ones that item actually has into one Ready percentage you can rank by.",
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
        /// Widest coin cell and widest chip across the whole table, so every
        /// row shares one column geometry and the header labels sit on the
        /// columns they name. Returns true when either changed.
        /// </summary>
        private bool RecomputeBandWidths()
        {
            int remaining = MeasureDashWidth();
            int chip = 0;
            foreach (var row in _rows)
            {
                if (row.RemainingCellWidth > remaining)
                {
                    remaining = row.RemainingCellWidth;
                }

                if (row.ChipWidth > chip)
                {
                    chip = row.ChipWidth;
                }
            }

            bool changed = remaining != _remainingBandWidth || chip != _chipBandWidth;
            _remainingBandWidth = remaining;
            _chipBandWidth = chip;
            return changed;
        }

        private RankerRowLayout.Bands BandsFor(int barWidth)
        {
            return RankerRowLayout.Compute(barWidth, _remainingBandWidth, _chipBandWidth);
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
            row.Metrics = metrics != null && metrics.PriorityIndex == index ? metrics : null;

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
                TextColor = DimColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(bands.RankX, 14),
                Parent = row.Panel,
            };

            row.IconName = IconNameRowHelpers.CreateIconAndEllipsizedName(
                row.Panel, entry.IconUrl, entry.Rarity,
                bands.IconX, 5, row.FullName, UiFonts.Body,
                bands.NameX + bands.NameWidth, 0, 0, bands.NameX, 12);
            TooltipFacility.ApplyPlain(row.IconName.NameLabel, row.FullName);
            IconControls.ApplyPlainToIconTree(row.IconName.IconFrame, row.FullName);

            row.ReadyLabel = new Label
            {
                Font = UiFonts.Body,
                Text = metrics == null ? RankerReadinessCalculator.DashText : RankerReadinessCalculator.FormatReadiness(metrics),
                TextColor = metrics == null || metrics.Kind != RankerReadinessKind.Measured
                    ? RankerReadinessColors.Neutral
                    : RankerReadinessColors.ForReadiness(metrics.Readiness),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 12),
                Parent = row.Panel,
            };
            TooltipFacility.ApplyPlain(row.ReadyLabel, ReadyTooltip(metrics));

            if (chipText != null)
            {
                row.Chip = LabelHelpers.CreateSmallTag(
                    row.Panel, chipText, bands.ChipX, 11,
                    ChipBorderColor(metrics), ChipBorderColor(metrics) * 0.15f);
                LabelHelpers.ApplyTagTooltip(row.Chip, ChipTooltip(metrics));
            }

            row.DaysLabel = new Label
            {
                Font = UiFonts.Body,
                Text = metrics == null ? RankerReadinessCalculator.DashText : RankerReadinessCalculator.FormatDays(metrics),
                TextColor = metrics == null
                    ? RankerReadinessColors.Neutral
                    : RankerReadinessColors.ForDays(metrics.DaysRemaining),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 12),
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
                    TextColor = RankerReadinessColors.Neutral,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(0, 12),
                    Parent = row.Panel,
                };
                TooltipFacility.ApplyPlain(row.RemainingDash, metrics == null
                    ? "Not yet calculated - press Refresh."
                    : "Nothing left to buy - the materials you hold cover this item's coin cost.");
            }
            else
            {
                row.RemainingCell = CoinCurrencyRenderer.RenderValueCellRightAligned(
                    row.Panel, metrics.RemainingCoinCost, null, bands.RemainingRightEdge, 12, UiFonts.Body);
            }

            row.Up = CreateRowButton(row.Panel, "\u25B2", bands.UpX,
                "Raise this item's priority. It then has first claim on materials the row above it was using.");
            row.Down = CreateRowButton(row.Panel, "\u25BC", bands.DownX,
                "Lower this item's priority.");
            row.Remove = CreateRowButton(row.Panel, "\u2715", bands.RemoveX,
                "Remove this item from your list.");

            row.Up.Enabled = !_isRefreshing && RankerPriorityOrdering.CanMoveUp(row.Index, Entries.Count);
            row.Down.Enabled = !_isRefreshing && RankerPriorityOrdering.CanMoveDown(row.Index, Entries.Count);
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
                Location = new Point(x, 8),
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
            // the four barriers read as one strip rather than a left-packed
            // sentence with dead space to its right.
            int gateY = RankerRowLayout.RowHeight + line * RankerRowLayout.SubLineHeight;
            for (int i = 0; i < metrics.Gates.Count && i < RankerRowLayout.GateCellCount; i++)
            {
                var gate = metrics.Gates[i];
                row.GateNameLabels.Add(new Label
                {
                    Font = UiFonts.Caption,
                    Text = RankerReadinessCalculator.GateLabel(gate.Gate),
                    TextColor = DimColor,
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
                        : RankerReadinessColors.Neutral,
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

                row.CurrencyNameFulls.Add(CurrencyName(shortfall));
                row.CurrencyNameLabels.Add(new Label
                {
                    Font = UiFonts.Caption,
                    Text = row.CurrencyNameFulls[row.CurrencyNameFulls.Count - 1],
                    TextColor = DimColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(0, y),
                    Parent = row.Panel,
                });
                row.CurrencyValueLabels.Add(new Label
                {
                    Font = UiFonts.Caption,
                    Text = FormatShortfall(shortfall),
                    TextColor = shortfall.Short > 0 ? DimColor : RankerReadinessColors.ForReadiness(1.0),
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
                    TextColor = DimColor,
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
            row.RankLabel.Location = new Point(bands.RankX, 14);

            if (measureText)
            {
                if (IconNameRowHelpers.ReellipsizeName(row.IconName, UiFonts.Body,
                        bands.NameX + bands.NameWidth, 0, 0))
                {
                    TooltipFacility.ApplyPlain(row.IconName.NameLabel, row.FullName);
                }
            }

            row.IconName.IconFrame.Location = new Point(bands.IconX, row.IconName.IconFrame.Location.Y);
            row.IconName.NameLabel.Location = new Point(bands.NameX, row.IconName.NameLabel.Location.Y);

            row.ReadyLabel.Location = new Point(
                Math.Max(0, bands.ReadyRightEdge - row.ReadyLabel.Width), 12);

            if (row.Chip != null)
            {
                row.Chip.Location = new Point(bands.ChipX, 11);
            }

            row.DaysLabel.Location = new Point(
                Math.Max(0, bands.DaysRightEdge - row.DaysLabel.Width), 12);

            if (row.RemainingDash != null)
            {
                row.RemainingDash.Location = new Point(
                    Math.Max(0, bands.RemainingRightEdge - row.RemainingDash.Width), 12);
            }
            else if (row.RemainingCell != null)
            {
                CoinCurrencyRenderer.RepositionValueCellRightAligned(
                    row.RemainingCell, bands.RemainingRightEdge, 12);
            }

            row.Up.Location = new Point(bands.UpX, 8);
            row.Down.Location = new Point(bands.DownX, 8);
            row.Remove.Location = new Point(bands.RemoveX, 8);

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
                // Same rails as the gate strip above, so every sub-line value
                // in the row right-aligns at the same four x positions.
                RankerRowLayout.GateCell(bands, i % RankerRowLayout.CurrenciesPerLine,
                    out int cellX, out int cellWidth);

                var name = row.CurrencyNameLabels[i];
                var value = row.CurrencyValueLabels[i];
                int valueX = Math.Max(cellX, cellX + cellWidth - value.Width - RankerRowLayout.CellGap);

                name.Location = new Point(cellX, name.Location.Y);
                value.Location = new Point(valueX, value.Location.Y);

                if (measureText)
                {
                    // A long currency name must clear the value in its own
                    // cell rather than running under it.
                    string full = row.CurrencyNameFulls[i];
                    string shown = LabelHelpers.EllipsizeToWidth(
                        UiFonts.Caption, full, Math.Max(0, valueX - cellX - RankerRowLayout.IconGap));
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

        private static Color ChipBorderColor(RankerRowMetrics metrics)
        {
            return metrics != null && metrics.AffordableNow
                ? RankerReadinessColors.ForReadiness(1.0)
                : RankerReadinessColors.Neutral;
        }

        private static string ChipTooltip(RankerRowMetrics metrics)
        {
            if (metrics == null)
            {
                return null;
            }

            if (metrics.AffordableNow)
            {
                return "You have enough coin for what is left of this item, after paying for everything above it on the list.";
            }

            return "You are " + CoinSegmentMath.GameStyleText(metrics.ShortfallCoin) +
                " short of what is left of this item, counting coin that the higher-priority items above it would already have spent.";
        }

        private static int MeasureDashWidth()
        {
            return LabelHelpers.MeasureWith(UiFonts.Body)(RankerReadinessCalculator.DashText);
        }

        private string CurrencyName(RankerCurrencyShortfall shortfall)
        {
            return CurrencyDisplayResolver.ResolveName(shortfall.CurrencyId, CurrencyMetadataFor(shortfall.CurrencyId));
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

                notes.Add(name + ": " + capped.NeededCount.ToString(CultureInfo.InvariantCulture) +
                    " needed, " + capped.CapValue.ToString(CultureInfo.InvariantCulture) + " per " +
                    CapWord(capped.CapType) + " cap");
                break;
            }

            return notes;
        }

        private static string CapWord(TimegatedCapType capType)
        {
            switch (capType)
            {
                case TimegatedCapType.Daily: return "day";
                case TimegatedCapType.Weekly: return "week";
                default: return "season";
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
                InvalidateFrom(existing);
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
            if (_isRefreshing)
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
            InvalidateFrom(invalidatedFrom);

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
            if (_isRefreshing)
            {
                CancelRefresh();
                return;
            }

            if (Entries.Count == 0)
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
            _refreshButton.Text = "Refreshing... (click to cancel)";
            TooltipFacility.ApplyPlain(_refreshButton, "Stop the current refresh. Items already updated keep their results.");
            _spinner.Visible = true;

            var entries = Entries.Select(e => new RankerWatchlistEntry
            {
                ItemId = e.ItemId,
                Quantity = e.Quantity,
                Name = e.Name,
                IconUrl = e.IconUrl,
                Rarity = e.Rarity,
            }).ToList();

            Task.Run(() => RunRefreshAsync(entries, myGen, cts.Token));
        }

        private async Task RunRefreshAsync(
            List<RankerWatchlistEntry> entries, int myGen, CancellationToken ct)
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

                for (int i = 0; i < entries.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var entry = entries[i];
                    int slot = i;
                    string name = entry.Name;
                    int position = i + 1;
                    int total = entries.Count;
                    MainThreadMarshal.Run(() => ReportProgress(myGen, position, total, name));

                    var availability = cascade.CurrentAvailability;

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

                    var metrics = RankerReadinessCalculator.Compute(baseline, owned, availability, slot);
                    cascade.Consume(owned);
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
            if (myGen != _refreshGeneration || !_buildComplete || !IsLive)
            {
                return;
            }

            _metricsByItemId[itemId] = metrics;
            _lastOwnedResults[itemId] = owned;

            var row = _rows.FirstOrDefault(r => r.ItemId == itemId);
            var entry = Entries.FirstOrDefault(e => e.ItemId == itemId);
            if (row == null || entry == null)
            {
                return;
            }

            row.Metrics = metrics;
            MeasureRowCells(row);

            int barWidth = Math.Max(0, _contentPanel.Width - ScrollbarAllowance);
            if (RecomputeBandWidths())
            {
                // A wider coin cell moves the Ready, chip and Days columns for
                // EVERY row, so they all have to follow rather than drifting
                // out of alignment as results land one at a time.
                for (int i = 0; i < _rows.Count && i < Entries.Count; i++)
                {
                    RenderRowContent(_rows[i], Entries[i], barWidth);
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

            if (!_buildComplete || !IsLive)
            {
                return;
            }

            _spinner.Visible = false;
            _refreshButton.Text = "Refresh";
            TooltipFacility.ApplyPlain(_refreshButton,
                "Recalculate every row. Each item is solved twice, so the first refresh of a session can take a while.");
            SetControlsEnabled(true);

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
            foreach (var row in _rows)
            {
                row.Up.Enabled = enabled && RankerPriorityOrdering.CanMoveUp(row.Index, Entries.Count);
                row.Down.Enabled = enabled && RankerPriorityOrdering.CanMoveDown(row.Index, Entries.Count);
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
