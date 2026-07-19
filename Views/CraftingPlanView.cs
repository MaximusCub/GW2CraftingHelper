using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Views
{
    public class CraftingPlanView
    {
        private static readonly Logger Logger = Logger.GetLogger<CraftingPlanView>();

        // Layout constants
        private const int RowHeight = 35;
        private const int InputRowY = 5;
        private const int ControlsRowY = 43;
        private const int StatusRowY = 81;
        private const int SeparatorY = 102;
        private const int ContentY = 107;
        private const int TopRegionHeight = 112;
        private const int RightEdgePadding = 20;

        private readonly Func<int, int, bool, PriceBasis, CancellationToken, IProgress<PlanStatus>, Task<CraftingPlanResult>> _generateAsync;
        private readonly Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, CraftingPlanResult> _resolveOverridesSync;
        private readonly ModalDialog _modalDialog;
        private readonly IItemSearchProvider _itemSearchProvider;
        private readonly PlanViewModelBuilder _vmBuilder = new PlanViewModelBuilder();

        private PlanViewModel _currentPlan;
        private CraftingPlanResult _lastResult;
        private DateTime _planGeneratedAt;
        private bool _useOwnMaterials;
        private PriceBasis _priceBasis = PriceBasis.InstantBuy;
        private int _selectedItemId;
        private int _quantity = 1;

        // Per-node user decision overrides (keyed by solver NodeId) and
        // explicit tree expansion state; both survive local re-solves and
        // reset on a fresh Generate.
        private readonly Dictionary<int, AcquisitionSource> _nodeOverrides =
            new Dictionary<int, AcquisitionSource>();
        private readonly Dictionary<int, bool> _nodeExpansion =
            new Dictionary<int, bool>();
        private readonly Dictionary<PlanSectionType, bool> _sectionExpansion =
            new Dictionary<PlanSectionType, bool>();

        // Suppress flag for checkbox revert
        private bool _suppressToggle;

        // Debug log from last plan generation
        private IReadOnlyList<string> _lastDebugLog;
        public IReadOnlyList<string> LastDebugLog => _lastDebugLog;

        // UI controls (stored for resize handler)
        private Panel _inputPanel;
        private Panel _controlsPanel;
        private AutocompleteTextBox _searchBox;
        private SuggestionPanel _suggestionPanel;
        private TextBox _qtyInput;
        private Checkbox _ownMaterialsCheckbox;
        private StandardButton _generateButton;
        private Label _statusLabel;
        private Panel _separator;
        private FlowPanel _contentPanel;

        // Resize tracking
        private int _lastRenderedWidth;

        public CraftingPlanView(
            Func<int, int, bool, PriceBasis, CancellationToken, IProgress<PlanStatus>, Task<CraftingPlanResult>> generateAsync,
            ModalDialog modalDialog,
            IItemSearchProvider itemSearchProvider,
            Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, CraftingPlanResult> resolveOverridesSync = null)
        {
            _generateAsync = generateAsync;
            _modalDialog = modalDialog;
            _itemSearchProvider = itemSearchProvider;
            _resolveOverridesSync = resolveOverridesSync;
        }

        public void SetStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = status ?? "";
            }
        }

        private void OnSelectedItemChanged(int itemId)
        {
            _selectedItemId = itemId;
        }

        public void Build(Container buildPanel)
        {
            // Clean up screen-parented popup from previous build cycle
            _suggestionPanel?.Dispose();

            int w = buildPanel.ContentRegion.Width;

            // Input row: search box + quantity
            _inputPanel = new Panel()
            {
                Size = new Point(w, RowHeight),
                Location = new Point(0, InputRowY),
                Parent = buildPanel
            };

            _searchBox = new AutocompleteTextBox()
            {
                PlaceholderText = "Search items...",
                Size = new Point(200, 28),
                Location = new Point(0, 3),
                Parent = _inputPanel
            };

            _suggestionPanel = new SuggestionPanel(_searchBox, _itemSearchProvider);
            _suggestionPanel.ItemSelected += (_, args) =>
            {
                OnSelectedItemChanged(args.ItemId);
            };

            new Label()
            {
                Text = "Qty:",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(210, 7),
                Parent = _inputPanel
            };

            _qtyInput = new TextBox()
            {
                Text = "1",
                Size = new Point(50, 28),
                Location = new Point(240, 3),
                Parent = _inputPanel
            };

            // Controls row: checkbox + generate button
            _controlsPanel = new Panel()
            {
                Size = new Point(w, RowHeight),
                Location = new Point(0, ControlsRowY),
                Parent = buildPanel
            };

            _ownMaterialsCheckbox = new Checkbox()
            {
                Text = "Use Own Materials",
                Checked = _useOwnMaterials,
                Location = new Point(0, 7),
                Parent = _controlsPanel
            };
            _ownMaterialsCheckbox.CheckedChanged += OnOwnMaterialsToggled;

            // Price basis selector; applies on the next Generate.
            new Label()
            {
                Text = "Prices:",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(170, 7),
                Parent = _controlsPanel
            };
            var priceBasisDropdown = new Dropdown()
            {
                Size = new Point(110, 28),
                Location = new Point(218, 3),
                Parent = _controlsPanel
            };
            priceBasisDropdown.Items.Add("Instant Buy");
            priceBasisDropdown.Items.Add("Buy Orders");
            priceBasisDropdown.SelectedItem = _priceBasis == PriceBasis.BuyOrder
                ? "Buy Orders"
                : "Instant Buy";
            priceBasisDropdown.ValueChanged += (_, e) =>
            {
                _priceBasis = e.CurrentValue == "Buy Orders"
                    ? PriceBasis.BuyOrder
                    : PriceBasis.InstantBuy;
            };

            _generateButton = new StandardButton()
            {
                Text = "Generate Plan",
                Size = new Point(120, 28),
                Location = new Point(w - 120 - RightEdgePadding, 3),
                Parent = _controlsPanel
            };
            _generateButton.Click += async (_, __) => await TriggerGenerate();

            // Status label
            _statusLabel = new Label()
            {
                Text = "Ready",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, StatusRowY),
                Parent = buildPanel
            };

            // Static separator between controls and content
            _separator = new Panel()
            {
                Size = new Point(w - RightEdgePadding, 2),
                Location = new Point(0, SeparatorY),
                BackgroundColor = new Color(180, 180, 180),
                Parent = buildPanel
            };

            // Scrollable content area - full width so scrollbar sits at the window edge.
            // Children use (Width - RightEdgePadding) to keep content clear of the scrollbar.
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

            if (_currentPlan != null)
            {
                _lastRenderedWidth = w;
                RenderPlan(_currentPlan);
            }
        }

        private void OnPanelResized(object sender, ResizedEventArgs e)
        {
            var container = (Container)sender;
            int w = container.ContentRegion.Width;
            int h = container.ContentRegion.Height;

            // Update widths of layout panels
            _inputPanel.Size = new Point(w, RowHeight);
            _controlsPanel.Size = new Point(w, RowHeight);
            _generateButton.Location = new Point(w - 120 - RightEdgePadding, 3);
            _separator.Size = new Point(w - RightEdgePadding, 2);
            _contentPanel.Size = new Point(w, h - TopRegionHeight);

            // Re-render plan content when width changes (centered title, right-aligned timestamps)
            if (_currentPlan != null && w != _lastRenderedWidth)
            {
                _lastRenderedWidth = w;
                RenderPlan(_currentPlan);
            }
        }

        private void OnOwnMaterialsToggled(object sender, CheckChangedEvent e)
        {
            if (_suppressToggle) return;

            bool newValue = e.Checked;

            if (_currentPlan != null)
            {
                // Show modal confirmation before regenerating
                _useOwnMaterials = newValue;
                _ownMaterialsCheckbox.Enabled = false;
                _modalDialog.Show(
                    "This will regenerate the plan. Continue?",
                    () =>
                    {
                        _ownMaterialsCheckbox.Enabled = true;
                        _ = TriggerGenerate();
                    },
                    () =>
                    {
                        _useOwnMaterials = !_useOwnMaterials;
                        _suppressToggle = true;
                        _ownMaterialsCheckbox.Checked = _useOwnMaterials;
                        _suppressToggle = false;
                        _ownMaterialsCheckbox.Enabled = true;
                    });
                return;
            }

            _useOwnMaterials = newValue;
        }

        private async Task TriggerGenerate()
        {
            // Parse quantity; tell the user when their input was discarded
            // instead of silently resetting it.
            bool qtyInvalid = !int.TryParse(_qtyInput?.Text, out int qty) || qty < 1;
            if (qtyInvalid)
            {
                qty = 1;
                if (_qtyInput != null) _qtyInput.Text = "1";
            }
            _quantity = qty;

            _generateButton.Enabled = false;
            _lastDebugLog = null;
            SetStatus(qtyInvalid
                ? "Quantity was invalid - reset to 1. Generating..."
                : "Generating...");

            var statusProgress = new Progress<PlanStatus>(ps =>
            {
                if (ps != null && !string.IsNullOrEmpty(ps.Message))
                {
                    SetStatus(ps.Message);
                }
            });

            try
            {
                var result = await _generateAsync(
                    _selectedItemId, _quantity, _useOwnMaterials, _priceBasis,
                    CancellationToken.None, statusProgress);

                _nodeOverrides.Clear();
                _nodeExpansion.Clear();
                _sectionExpansion.Clear();
                _lastResult = result;
                _lastDebugLog = result.DebugLog;
                var vm = _vmBuilder.Build(result);
                _currentPlan = vm;
                _planGeneratedAt = DateTime.Now;
                _lastRenderedWidth = _contentPanel?.Width ?? 0;
                RenderPlan(vm);
                SetStatus($"Plan generated - {_planGeneratedAt:MMM d, yyyy h:mm tt}");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Plan generation failed");
                _lastDebugLog = new[] { $"Generation failed: {ex.Message}" };
                SetStatus($"Error: {ex.Message}");
            }
            finally
            {
                _generateButton.Enabled = true;
            }
        }

        private void RenderPlan(PlanViewModel vm)
        {
            if (_contentPanel == null) return;

            // Drop tree states up front so a plan without a tree section
            // does not retain disposed controls from the previous render.
            _treeNodeStates.Clear();

            foreach (var child in _contentPanel.Children.ToArray())
            {
                child.Dispose();
            }

            int panelWidth = _contentPanel.Width - RightEdgePadding;

            // Plan header: fixed-height container with vertically centered icon + title
            const int headerHeight = 56;
            const int headerTopPad = 10;
            const int headerBottomPad = 4;
            const int iconSize = 32;
            const int iconPad = 8;

            var titleFont = GameService.Content.DefaultFont18;
            string titleText = $"{vm.TargetItemName} Crafting Plan";
            var measured = titleFont.MeasureString(titleText);
            int textWidth = (int)System.Math.Ceiling(measured.Width);
            int textHeight = (int)System.Math.Ceiling(measured.Height);

            int totalTitleWidth = iconSize + iconPad + textWidth;
            int startX = System.Math.Max(0, (panelWidth - totalTitleWidth) / 2);
            int centerRegion = headerHeight - headerTopPad - headerBottomPad;
            int iconY = headerTopPad + (centerRegion - iconSize) / 2;
            // Anchor text to icon's visual center with -2px optical nudge for descenders
            int textY = iconY + (iconSize - textHeight) / 2 - 2;

            var titlePanel = new Panel()
            {
                Size = new Point(panelWidth, headerHeight),
                Parent = _contentPanel
            };

            // Target item icon
            AsyncTexture2D titleIcon;
            if (!string.IsNullOrEmpty(vm.TargetIconUrl))
            {
                titleIcon = GameService.Content.GetRenderServiceTexture(vm.TargetIconUrl);
            }
            else
            {
                titleIcon = new AsyncTexture2D(ContentService.Textures.Error);
            }

            new Panel()
            {
                Size = new Point(iconSize, iconSize),
                Location = new Point(startX, iconY),
                BackgroundTexture = titleIcon,
                Parent = titlePanel
            };

            new Label()
            {
                Text = titleText,
                Font = titleFont,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(startX + iconSize + iconPad, textY),
                Parent = titlePanel
            };

            // Generated timestamp: right-aligned
            var tsPanel = new Panel()
            {
                Size = new Point(panelWidth, 22),
                Parent = _contentPanel
            };

            string tsText = $"Generated: {_planGeneratedAt:MMM d, yyyy h:mm tt}";
            var tsFont = GameService.Content.DefaultFont14;
            var tsMeasured = tsFont.MeasureString(tsText);
            int tsWidth = (int)System.Math.Ceiling(tsMeasured.Width);

            new Label()
            {
                Text = tsText,
                Font = tsFont,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(System.Math.Max(0, panelWidth - tsWidth - 8), 2),
                Parent = tsPanel
            };

            // Separator under header
            new Panel()
            {
                Size = new Point(panelWidth, 2),
                BackgroundColor = new Color(180, 180, 180),
                Parent = _contentPanel
            };

            foreach (var section in vm.Sections)
            {
                CreateCollapsibleSection(section, panelWidth);
            }

            // Recipe Tree section (if tree data available)
            if (vm.TreeRoot != null)
            {
                CreateTreeSection(vm.TreeRoot, panelWidth);
            }
        }

        private void CreateCollapsibleSection(PlanSectionViewModel section, int panelWidth)
        {
            // User collapse state survives re-renders (width changes, local
            // re-solves); resets on a fresh Generate.
            bool expanded = _sectionExpansion.TryGetValue(section.SectionType, out bool userExpanded)
                ? userExpanded
                : section.IsDefaultExpanded;

            // Section header (clickable). The arrow gets its own label in
            // the default font: DefaultFont18 has no glyph for the triangle
            // characters, so an arrow embedded in the title never rendered.
            var headerPanel = new Panel()
            {
                Size = new Point(panelWidth, 30),
                Parent = _contentPanel
            };

            var headerArrow = new Label()
            {
                Text = expanded ? "\u25BC" : "\u25B6",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(4, 8),
                Parent = headerPanel
            };

            var headerLabel = new Label()
            {
                Text = section.Title,
                Font = GameService.Content.DefaultFont18,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(22, 4),
                Parent = headerPanel
            };

            // Content panel
            var contentFlow = new FlowPanel()
            {
                Size = new Point(panelWidth, 0),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Visible = expanded,
                Parent = _contentPanel,
                HeightSizingMode = SizingMode.AutoSize
            };

            // Populate rows
            foreach (var row in section.Rows)
            {
                CreateRow(row, contentFlow, panelWidth);
            }

            // Toggle on click
            headerPanel.Click += (_, __) =>
            {
                contentFlow.Visible = !contentFlow.Visible;
                _sectionExpansion[section.SectionType] = contentFlow.Visible;
                headerArrow.Text = contentFlow.Visible ? "\u25BC" : "\u25B6";
                _contentPanel.Invalidate();
            };
        }

        private void CreateRow(PlanRowViewModel row, FlowPanel parent, int panelWidth)
        {
            switch (row.RowType)
            {
                case PlanRowType.CoinTotal:
                    CreateCoinTotalRow(row, parent, panelWidth);
                    break;

                case PlanRowType.CurrencyCost:
                    CreateTextRow(row.Label, parent, panelWidth);
                    break;

                case PlanRowType.UsedMaterial:
                    CreateIconQuantityRow(row, parent, panelWidth);
                    break;

                case PlanRowType.ShoppingBuy:
                case PlanRowType.ShoppingVendor:
                case PlanRowType.ShoppingCurrency:
                case PlanRowType.ShoppingUnknown:
                    CreateShoppingRow(row, parent, panelWidth);
                    break;

                case PlanRowType.CraftStep:
                    CreateCraftStepRow(row, parent, panelWidth);
                    break;

                case PlanRowType.DisciplineRow:
                    CreateDisciplineRow(row, parent, panelWidth);
                    break;

                case PlanRowType.RecipeRow:
                    CreateRecipeRow(row, parent, panelWidth);
                    break;
            }
        }

        private void CreateCoinTotalRow(PlanRowViewModel row, FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, 28),
                Parent = parent
            };
            BuildCoinDisplay(rowPanel, row.CoinValue, row.Label);
        }

        private void CreateTextRow(string text, FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, 28),
                Parent = parent
            };
            new Label()
            {
                Text = "  " + text,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(8, 4),
                Parent = rowPanel
            };
        }

        private void CreateIconQuantityRow(PlanRowViewModel row, FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, 36),
                Parent = parent
            };

            CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 4, 1);

            new Label()
            {
                Text = $"{row.Quantity}x {row.Label}",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(42, 6),
                Parent = rowPanel
            };
        }

        private void CreateShoppingRow(PlanRowViewModel row, FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, 36),
                Parent = parent
            };

            CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 4, 1);

            string prefix;
            switch (row.RowType)
            {
                case PlanRowType.ShoppingBuy: prefix = "Buy"; break;
                case PlanRowType.ShoppingVendor: prefix = "Buy (vendor)"; break;
                case PlanRowType.ShoppingCurrency: prefix = "Acquire"; break;
                default: prefix = "???"; break;
            }

            var textLabel = new Label()
            {
                Text = $"{prefix} {row.Quantity}x {row.Label}",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(42, 6),
                Parent = rowPanel
            };

            // Inline coin display for shopping rows with coin value
            if (row.CoinValue > 0 &&
                (row.RowType == PlanRowType.ShoppingBuy || row.RowType == PlanRowType.ShoppingVendor))
            {
                var dashLabel = new Label()
                {
                    Text = " - ",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(42 + textLabel.Width, 6),
                    Parent = rowPanel
                };
                int coinX = 42 + textLabel.Width + dashLabel.Width;
                BuildInlineCoin(rowPanel, row.CoinValue, coinX);
            }
        }

        private void CreateCraftStepRow(PlanRowViewModel row, FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, 36),
                Parent = parent
            };

            CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 4, 1);

            string text = $"Craft {row.Quantity}x {row.Label}";
            if (!string.IsNullOrEmpty(row.Sublabel))
            {
                text += $" - {row.Sublabel}";
            }

            new Label()
            {
                Text = text,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(42, 6),
                Parent = rowPanel
            };
        }

        private void CreateDisciplineRow(PlanRowViewModel row, FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, 28),
                Parent = parent
            };

            new Label()
            {
                Text = $"  {row.Label} - {row.Sublabel}",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(8, 4),
                Parent = rowPanel
            };
        }

        private void CreateRecipeRow(PlanRowViewModel row, FlowPanel parent, int panelWidth)
        {
            bool hasSublabel = !string.IsNullOrEmpty(row.Sublabel);
            int rowHeight = hasSublabel ? 48 : 36;

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, rowHeight),
                Parent = parent
            };

            CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 4, 1);

            string statusSuffix = !string.IsNullOrEmpty(row.StatusTag)
                ? $" - {row.StatusTag}"
                : "";

            int nameY = hasSublabel ? 2 : 6;
            var label = new Label()
            {
                Text = $"{row.Label}{statusSuffix}",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(42, nameY),
                Parent = rowPanel
            };

            // Color the status tag
            if (row.StatusTag == "Missing!")
            {
                label.TextColor = new Color(255, 100, 100);
            }
            else if (row.StatusTag == "Auto-learned")
            {
                label.TextColor = new Color(150, 200, 150);
            }

            if (hasSublabel)
            {
                new Label()
                {
                    Text = $"  {row.Sublabel}",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(42, 22),
                    TextColor = new Color(170, 170, 170),
                    Parent = rowPanel
                };
            }
        }

        // --- Recipe tree section ---

        private class TreeNodeState
        {
            public bool ChildrenBuilt;
            public bool IsExpanded;
            public FlowPanel ChildContainer;
            public Label ArrowLabel;
            public CraftingTreeNode Node;
            public int Depth;
            public int PanelWidth;
        }

        // States for the current render pass; rebuilt with the tree itself.
        private readonly List<TreeNodeState> _treeNodeStates = new List<TreeNodeState>();

        private void CreateTreeSection(CraftingTreeNode treeRoot, int panelWidth)
        {
            _treeNodeStates.Clear();

            var headerPanel = new Panel()
            {
                Size = new Point(panelWidth, 30),
                Parent = _contentPanel
            };

            var headerArrow = new Label()
            {
                Text = "\u25BC",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(4, 8),
                Parent = headerPanel
            };

            var headerLabel = new Label()
            {
                Text = "Recipe Tree",
                Font = GameService.Content.DefaultFont18,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(22, 4),
                Parent = headerPanel
            };

            var expandAllButton = new StandardButton()
            {
                Text = "Expand All",
                Size = new Point(92, 24),
                Location = new Point(panelWidth - 196, 3),
                Parent = headerPanel
            };

            var collapseAllButton = new StandardButton()
            {
                Text = "Collapse All",
                Size = new Point(96, 24),
                Location = new Point(panelWidth - 100, 3),
                Parent = headerPanel
            };

            var treeFlow = new FlowPanel()
            {
                Size = new Point(panelWidth, 0),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Visible = true,
                Parent = _contentPanel,
                HeightSizingMode = SizingMode.AutoSize
            };

            RenderTreeNode(treeRoot, treeFlow, panelWidth, 0);

            // Decision presets: clear overrides / force craft-everywhere /
            // force buy-everywhere (feasibility respected by the solver).
            var bestPathButton = new StandardButton()
            {
                Text = "Best Path",
                Size = new Point(80, 24),
                Location = new Point(150, 3),
                Parent = headerPanel
            };
            var craftAllButton = new StandardButton()
            {
                Text = "Craft All",
                Size = new Point(76, 24),
                Location = new Point(234, 3),
                Parent = headerPanel
            };
            var buyAllButton = new StandardButton()
            {
                Text = "Buy All",
                Size = new Point(70, 24),
                Location = new Point(314, 3),
                Parent = headerPanel
            };

            bestPathButton.Click += (_, __) =>
            {
                if (_nodeOverrides.Count == 0) return;
                _nodeOverrides.Clear();
                ApplyOverridesAndResolve();
            };
            craftAllButton.Click += (_, __) => ApplyPreset(AcquisitionSource.Craft);
            buyAllButton.Click += (_, __) => ApplyPreset(AcquisitionSource.BuyFromTp);

            expandAllButton.Click += (_, __) =>
            {
                // Building children appends to _treeNodeStates; index loop
                // deliberately walks the growing list.
                for (int i = 0; i < _treeNodeStates.Count; i++)
                {
                    var s = _treeNodeStates[i];
                    if (!s.ChildrenBuilt)
                    {
                        foreach (var child in s.Node.Children)
                        {
                            RenderTreeNode(child, s.ChildContainer, s.PanelWidth, s.Depth + 1);
                        }
                        s.ChildrenBuilt = true;
                    }
                    s.IsExpanded = true;
                    _nodeExpansion[s.Node.NodeId] = true;
                    s.ChildContainer.Visible = true;
                    s.ArrowLabel.Text = "\u25BC";
                }
                treeFlow.Invalidate();
            };

            collapseAllButton.Click += (_, __) =>
            {
                foreach (var s in _treeNodeStates)
                {
                    s.IsExpanded = false;
                    _nodeExpansion[s.Node.NodeId] = false;
                    s.ChildContainer.Visible = false;
                    s.ArrowLabel.Text = "\u25B6";
                }
                treeFlow.Invalidate();
            };

            // Guard uses PRESS-time hover state: with a release-time check,
            // pressing on the header background and releasing over a button
            // dropped the click entirely (neither toggle nor button fired).
            bool pressStartedOnButton = false;
            headerPanel.LeftMouseButtonPressed += (_, __) =>
            {
                pressStartedOnButton =
                    expandAllButton.MouseOver || collapseAllButton.MouseOver ||
                    bestPathButton.MouseOver || craftAllButton.MouseOver ||
                    buyAllButton.MouseOver;
            };
            headerPanel.Click += (_, __) =>
            {
                if (pressStartedOnButton)
                {
                    return;
                }
                treeFlow.Visible = !treeFlow.Visible;
                headerArrow.Text = treeFlow.Visible ? "\u25BC" : "\u25B6";
                _contentPanel.Invalidate();
            };
        }

        /// <summary>
        /// The next feasible acquisition source when cycling this node's
        /// decision, or null when fewer than two paths are feasible.
        /// </summary>
        private static AcquisitionSource? GetNextCyclableSource(CraftingTreeNode node)
        {
            var order = new List<AcquisitionSource>(3);
            if (node.CanCraft) order.Add(AcquisitionSource.Craft);
            if (node.CanBuyTp) order.Add(AcquisitionSource.BuyFromTp);
            if (node.CanBuyVendor) order.Add(AcquisitionSource.BuyFromVendor);
            if (order.Count < 2)
            {
                return null;
            }

            AcquisitionSource current;
            switch (node.Decision)
            {
                case CraftingDecision.Craft: current = AcquisitionSource.Craft; break;
                case CraftingDecision.BuyFromTp: current = AcquisitionSource.BuyFromTp; break;
                case CraftingDecision.BuyFromVendor: current = AcquisitionSource.BuyFromVendor; break;
                default: return null;
            }

            int idx = order.IndexOf(current);
            return order[(idx + 1) % order.Count];
        }

        private void ApplyPreset(AcquisitionSource source)
        {
            if (_lastResult?.SolveContext == null) return;
            _nodeOverrides.Clear();
            // Walk the full solver tree (not the display tree, which hides
            // children under bought nodes) so one click reaches every level.
            var preset = CraftingPlanPipeline.BuildPresetOverrides(
                _lastResult.SolveContext, source);
            foreach (var kvp in preset)
            {
                _nodeOverrides[kvp.Key] = kvp.Value;
            }
            ApplyOverridesAndResolve();
        }

        private void ApplyOverridesAndResolve()
        {
            if (_lastResult?.SolveContext == null || _resolveOverridesSync == null)
            {
                return;
            }

            try
            {
                var result = _resolveOverridesSync(_lastResult.SolveContext, _nodeOverrides);
                _lastResult = result;
                _lastDebugLog = result.DebugLog;
                var vm = _vmBuilder.Build(result);
                _currentPlan = vm;
                RenderPlan(vm);
                SetStatus(_nodeOverrides.Count == 0
                    ? "Best path restored"
                    : $"Decisions updated ({_nodeOverrides.Count} override(s))");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Override re-solve failed");
                SetStatus($"Error: {ex.Message}");
            }
        }

        private void RenderTreeNode(CraftingTreeNode node, FlowPanel parent, int panelWidth, int depth)
        {
            const int indentPer = 24;
            const int arrowWidth = 18;
            const int iconSize = 32;
            const int borderSize = iconSize + 2;
            const int iconPad = 4;
            const int rowHeight = 40;

            int indent = depth * indentPer;
            bool hasChildren = node.Children.Count > 0;

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, rowHeight),
                BackgroundColor = Color.Transparent,
                Parent = parent
            };

            // Hover wash (pattern per SuggestionPanel row highlighting).
            // Color.White * 0.07f premultiplies alpha; a raw
            // Color(255,255,255,18) renders as near-opaque white in XNA's
            // premultiplied pipeline (verified via screenshot loop).
            rowPanel.MouseEntered += (_, __) =>
            {
                rowPanel.BackgroundColor = Color.White * 0.07f;
            };
            rowPanel.MouseLeft += (_, __) =>
            {
                rowPanel.BackgroundColor = Color.Transparent;
            };

            // Expand/collapse arrow. Explicit user expansion state survives
            // local re-solves; unvisited nodes default to expanded above
            // depth 2.
            bool isExpanded = _nodeExpansion.TryGetValue(node.NodeId, out bool userExpanded)
                ? userExpanded
                : depth < 2;
            Label arrowLabel = null;
            if (hasChildren)
            {
                arrowLabel = new Label()
                {
                    Text = isExpanded ? "\u25BC" : "\u25B6",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(indent, 12),
                    Parent = rowPanel
                };
            }

            // Item icon with 1px rarity-colored border
            int iconX = indent + (hasChildren ? arrowWidth : 0);
            CreateRarityFramedIcon(rowPanel, node.IconUrl, node.Rarity, iconX, 3);

            // Quantity + name
            int textX = iconX + borderSize + iconPad;
            string nameText = node.Quantity > 0
                ? $"{node.Quantity}x {node.Name}"
                : node.Name;
            var nameLabel = new Label()
            {
                Text = nameText,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(textX, 12),
                Parent = rowPanel
            };

            // Decision badge rendered as a subtle pill: measure the label
            // first, then wrap it in a tinted background panel. When more
            // than one acquisition path is feasible, clicking the pill
            // cycles the decision and re-solves locally.
            int badgeX = textX + nameLabel.Width + 6;
            string badgeText = GetDecisionBadgeText(node.Decision);
            Color badgeColor = GetDecisionBadgeColor(node.Decision);
            var badgeLabel = new Label()
            {
                Text = badgeText,
                TextColor = badgeColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Parent = rowPanel
            };
            var badgePill = new Panel()
            {
                Size = new Point(badgeLabel.Width + 8, badgeLabel.Height + 4),
                Location = new Point(badgeX, 10),
                BackgroundColor = badgeColor * 0.25f,
                Parent = rowPanel
            };
            badgeLabel.Parent = badgePill;
            badgeLabel.Location = new Point(4, 2);

            if (node.UnitCost.HasValue && node.Quantity > 1 &&
                (node.Decision == CraftingDecision.BuyFromTp ||
                 node.Decision == CraftingDecision.BuyFromVendor))
            {
                rowPanel.BasicTooltipText = "Unit price: " + FormatCoinText(node.UnitCost.Value);
            }

            AcquisitionSource? nextSource = GetNextCyclableSource(node);
            if (nextSource.HasValue && _resolveOverridesSync != null)
            {
                badgePill.BackgroundColor = badgeColor * 0.35f;
                badgePill.BasicTooltipText = "Click: switch acquisition source";
                badgePill.Click += (_, __) =>
                {
                    _nodeOverrides[node.NodeId] = nextSource.Value;
                    ApplyOverridesAndResolve();
                };
            }

            // Cost display: inline coin for nodes with SubtreeCost
            if (node.SubtreeCost.HasValue && node.SubtreeCost.Value > 0)
            {
                int costX = badgeX + badgePill.Width + 6;
                BuildInlineCoin(rowPanel, node.SubtreeCost.Value, costX, 10);
            }

            // Child container
            if (hasChildren)
            {
                var childFlow = new FlowPanel()
                {
                    Size = new Point(panelWidth, 0),
                    FlowDirection = ControlFlowDirection.SingleTopToBottom,
                    Parent = parent,
                    HeightSizingMode = SizingMode.AutoSize
                };

                var state = new TreeNodeState
                {
                    Node = node,
                    Depth = depth,
                    ChildContainer = childFlow,
                    ArrowLabel = arrowLabel,
                    PanelWidth = panelWidth
                };
                _treeNodeStates.Add(state);
                if (isExpanded)
                {
                    foreach (var child in node.Children)
                    {
                        RenderTreeNode(child, childFlow, panelWidth, depth + 1);
                    }
                    state.ChildrenBuilt = true;
                    state.IsExpanded = true;
                    childFlow.Visible = true;
                }
                else
                {
                    state.IsExpanded = false;
                    childFlow.Visible = false;
                }

                EventHandler<MouseEventArgs> toggleHandler = (_, __) =>
                {
                    // The badge pill has its own click action; do not treat
                    // it as an expand/collapse toggle.
                    if (badgePill.MouseOver)
                    {
                        return;
                    }
                    if (!state.ChildrenBuilt)
                    {
                        foreach (var child in state.Node.Children)
                        {
                            RenderTreeNode(child, state.ChildContainer, state.PanelWidth, state.Depth + 1);
                        }
                        state.ChildrenBuilt = true;
                    }
                    state.IsExpanded = !state.IsExpanded;
                    _nodeExpansion[state.Node.NodeId] = state.IsExpanded;
                    state.ChildContainer.Visible = state.IsExpanded;
                    state.ArrowLabel.Text = state.IsExpanded ? "\u25BC" : "\u25B6";
                    state.ChildContainer.Parent.Invalidate();
                };
                rowPanel.Click += toggleHandler;
            }
        }

        private static string GetDecisionBadgeText(CraftingDecision decision)
        {
            switch (decision)
            {
                case CraftingDecision.Craft: return "CRAFT";
                case CraftingDecision.BuyFromTp: return "TP";
                case CraftingDecision.BuyFromVendor: return "VENDOR";
                case CraftingDecision.Have: return "HAVE";
                case CraftingDecision.Currency: return "CURRENCY";
                default: return "?";
            }
        }

        private static Color GetDecisionBadgeColor(CraftingDecision decision)
        {
            switch (decision)
            {
                case CraftingDecision.Craft: return new Color(100, 200, 100);
                case CraftingDecision.BuyFromTp: return new Color(255, 200, 60);
                case CraftingDecision.BuyFromVendor: return new Color(180, 130, 255);
                case CraftingDecision.Have: return new Color(170, 170, 170);
                case CraftingDecision.Currency: return new Color(255, 220, 100);
                default: return new Color(255, 100, 100);
            }
        }

        /// <summary>
        /// Standard GW2 rarity palette for icon borders. Unknown/absent rarity
        /// (and Basic, whose canonical white would look borderless next to
        /// tinted ones) renders a neutral dark grey - never guess a rarity.
        /// </summary>
        private static string FormatCoinText(long copper)
        {
            if (copper < 0) copper = 0;
            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;
            return $"{gold}g {silver}s {cop}c";
        }

        private static Color GetRarityBorderColor(string rarity)
        {
            switch (rarity)
            {
                case "Junk": return new Color(170, 170, 170);
                case "Fine": return new Color(98, 164, 218);
                case "Masterwork": return new Color(26, 147, 6);
                case "Rare": return new Color(252, 208, 11);
                case "Exotic": return new Color(255, 164, 5);
                case "Ascended": return new Color(251, 62, 141);
                case "Legendary": return new Color(76, 19, 157);
                default: return new Color(60, 60, 60);
            }
        }

        // --- Coin display helpers (reused from original) ---

        private static void BuildCoinDisplay(Panel parent, long copper, string label = "Total")
        {
            if (copper < 0) copper = 0;

            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;

            int x = 0;
            var totalLabel = new Label()
            {
                Text = "  " + (string.IsNullOrEmpty(label) ? "Total" : label) + ": ",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(8, 4),
                Parent = parent
            };
            x = 8 + totalLabel.Width;

            x = AddCoinSegment(parent, x, 156904, gold.ToString(), 4);
            x = AddCoinSegment(parent, x, 156907, silver.ToString(), 4);
            AddCoinSegment(parent, x, 156902, cop.ToString(), 4);
        }

        private static void BuildInlineCoin(Panel parent, long copper, int startX, int y = 6)
        {
            if (copper < 0) copper = 0;

            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;

            int x = startX;
            x = AddCoinSegment(parent, x, 156904, gold.ToString(), y);
            x = AddCoinSegment(parent, x, 156907, silver.ToString(), y);
            AddCoinSegment(parent, x, 156902, cop.ToString(), y);
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

        private static int AddCoinSegment(Panel parent, int x, int assetId, string value, int y)
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
                Location = new Point(x, y),
                Parent = parent
            };

            new Panel()
            {
                Size = new Point(iconSize, iconSize),
                Location = new Point(x + label.Width + gap, y),
                BackgroundTexture = AsyncTexture2D.FromAssetId(assetId),
                Parent = parent
            };

            return x + label.Width + gap + iconSize + segmentGap;
        }

        // --- Icon helper ---

        /// <summary>
        /// 32px item icon inside a 1px rarity-colored frame (34px overall).
        /// </summary>
        private static void CreateRarityFramedIcon(
            Panel parent, string iconUrl, string rarity, int x, int y)
        {
            var frame = new Panel()
            {
                Size = new Point(34, 34),
                Location = new Point(x, y),
                BackgroundColor = GetRarityBorderColor(rarity),
                Parent = parent
            };
            CreateItemIcon(frame, iconUrl, 1, 1);
        }

        private static void CreateItemIcon(Panel parent, string iconUrl, int x, int y)
        {
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
                Location = new Point(x, y),
                BackgroundTexture = icon,
                Parent = parent
            };
        }
    }
}
