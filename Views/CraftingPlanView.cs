using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
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
    public class CraftingPlanView : View
    {
        private static readonly Logger Logger = Logger.GetLogger<CraftingPlanView>();

        // Layout constants
        private const int TabHeight = 35;
        private const int InputRowY = 40;
        private const int ControlsRowY = 78;
        private const int StatusRowY = 116;
        private const int SeparatorY = 137;
        private const int ContentY = 142;
        private const int TopRegionHeight = 147;
        private const int RightEdgePadding = 20;

        private readonly Func<int, int, bool, CancellationToken, IProgress<PlanStatus>, Task<CraftingPlanResult>> _generateAsync;
        private readonly Action _switchToSnapshot;
        private readonly ModalDialog _modalDialog;
        private readonly IItemSearchProvider _itemSearchProvider;
        private readonly PlanViewModelBuilder _vmBuilder = new PlanViewModelBuilder();

        // Populated from IItemSearchProvider on view build
        private IReadOnlyList<ItemSearchResult> _itemChoices = Array.Empty<ItemSearchResult>();

        private PlanViewModel _currentPlan;
        private DateTime _planGeneratedAt;
        private bool _useOwnMaterials;
        private int _selectedItemId;
        private int _quantity = 1;

        // Suppress flag for checkbox revert
        private bool _suppressToggle;

        // UI controls (stored for resize handler)
        private Panel _tabPanel;
        private Panel _inputPanel;
        private Panel _controlsPanel;
        private Dropdown _itemDropdown;
        private TextBox _qtyInput;
        private Checkbox _ownMaterialsCheckbox;
        private StandardButton _generateButton;
        private Label _statusLabel;
        private Panel _separator;
        private FlowPanel _contentPanel;

        // Resize tracking
        private int _lastRenderedWidth;

        public CraftingPlanView(
            Func<int, int, bool, CancellationToken, IProgress<PlanStatus>, Task<CraftingPlanResult>> generateAsync,
            Action switchToSnapshot,
            ModalDialog modalDialog,
            IItemSearchProvider itemSearchProvider)
        {
            _generateAsync = generateAsync;
            _switchToSnapshot = switchToSnapshot;
            _modalDialog = modalDialog;
            _itemSearchProvider = itemSearchProvider;
        }

        public void SetStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = status ?? "";
            }
        }

        private async void PopulateDropdownAsync()
        {
            try
            {
                _itemChoices = await _itemSearchProvider.SearchAsync(
                    "", 100, CancellationToken.None);

                foreach (var item in _itemChoices)
                {
                    _itemDropdown.Items.Add(item.Name);
                }

                if (_itemChoices.Count > 0)
                {
                    _itemDropdown.SelectedItem = _itemChoices[0].Name;
                    _selectedItemId = _itemChoices[0].ItemId;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to populate item dropdown");
            }
        }

        protected override void Build(Container buildPanel)
        {
            int w = buildPanel.ContentRegion.Width;

            // Tab bar
            _tabPanel = new Panel()
            {
                Size = new Point(w, TabHeight),
                Parent = buildPanel
            };

            var snapshotTab = new StandardButton()
            {
                Text = "Snapshot",
                Size = new Point(100, 28),
                Location = new Point(0, 3),
                Parent = _tabPanel
            };
            snapshotTab.Click += (_, __) => _switchToSnapshot?.Invoke();

            new StandardButton()
            {
                Text = "Crafting Plan",
                Size = new Point(110, 28),
                Location = new Point(105, 3),
                Enabled = false,
                Parent = _tabPanel
            };

            // Input row: dropdown + quantity
            _inputPanel = new Panel()
            {
                Size = new Point(w, TabHeight),
                Location = new Point(0, InputRowY),
                Parent = buildPanel
            };

            _itemDropdown = new Dropdown()
            {
                Size = new Point(200, 28),
                Location = new Point(0, 3),
                Parent = _inputPanel
            };
            PopulateDropdownAsync();
            _itemDropdown.ValueChanged += (_, __) =>
            {
                if (_itemDropdown.SelectedItem != null)
                {
                    var match = _itemChoices.FirstOrDefault(
                        i => i.Name == _itemDropdown.SelectedItem);
                    if (match != null)
                    {
                        _selectedItemId = match.ItemId;
                    }
                }
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
                Size = new Point(w, TabHeight),
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
            _tabPanel.Size = new Point(w, TabHeight);
            _inputPanel.Size = new Point(w, TabHeight);
            _controlsPanel.Size = new Point(w, TabHeight);
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
            // Parse quantity
            if (!int.TryParse(_qtyInput?.Text, out int qty) || qty < 1)
            {
                qty = 1;
                if (_qtyInput != null) _qtyInput.Text = "1";
            }
            _quantity = qty;

            _generateButton.Enabled = false;
            SetStatus("Generating...");

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
                    _selectedItemId, _quantity, _useOwnMaterials,
                    CancellationToken.None, statusProgress);

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
            // Section header (clickable)
            string arrow = section.IsDefaultExpanded ? "\u25BC" : "\u25B6";
            var headerPanel = new Panel()
            {
                Size = new Point(panelWidth, 30),
                Parent = _contentPanel
            };

            var headerLabel = new Label()
            {
                Text = $"{arrow} {section.Title}",
                Font = GameService.Content.DefaultFont18,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(4, 4),
                Parent = headerPanel
            };

            // Content panel
            var contentFlow = new FlowPanel()
            {
                Size = new Point(panelWidth, 0),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Visible = section.IsDefaultExpanded,
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
                headerLabel.Text = (contentFlow.Visible ? "\u25BC" : "\u25B6")
                    + " " + section.Title;
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
            BuildCoinDisplay(rowPanel, row.CoinValue);
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

            CreateItemIcon(rowPanel, row.IconUrl, 4, 2);

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

            CreateItemIcon(rowPanel, row.IconUrl, 4, 2);

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

            CreateItemIcon(rowPanel, row.IconUrl, 4, 2);

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

            CreateItemIcon(rowPanel, row.IconUrl, 4, 2);

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

        private void CreateTreeSection(CraftingTreeNode treeRoot, int panelWidth)
        {
            string arrow = "\u25BC";
            var headerPanel = new Panel()
            {
                Size = new Point(panelWidth, 30),
                Parent = _contentPanel
            };

            var headerLabel = new Label()
            {
                Text = $"{arrow} Recipe Tree",
                Font = GameService.Content.DefaultFont18,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(4, 4),
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

            headerPanel.Click += (_, __) =>
            {
                treeFlow.Visible = !treeFlow.Visible;
                headerLabel.Text = (treeFlow.Visible ? "\u25BC" : "\u25B6")
                    + " Recipe Tree";
                _contentPanel.Invalidate();
            };
        }

        private void RenderTreeNode(CraftingTreeNode node, FlowPanel parent, int panelWidth, int depth)
        {
            const int indentPer = 24;
            const int arrowWidth = 18;
            const int iconSize = 32;
            const int iconPad = 4;
            const int rowHeight = 36;

            int indent = depth * indentPer;
            bool hasChildren = node.Children != null && node.Children.Count > 0;

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, rowHeight),
                Parent = parent
            };

            // Expand/collapse arrow
            Label arrowLabel = null;
            if (hasChildren)
            {
                bool defaultExpanded = depth < 2;
                arrowLabel = new Label()
                {
                    Text = defaultExpanded ? "\u25BC" : "\u25B6",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(indent, 8),
                    Parent = rowPanel
                };
            }

            // Item icon
            int iconX = indent + (hasChildren ? arrowWidth : 0);
            CreateItemIcon(rowPanel, node.IconUrl, iconX, 2);

            // Quantity + name
            int textX = iconX + iconSize + iconPad;
            string nameText = node.Quantity > 0
                ? $"{node.Quantity}x {node.Name}"
                : node.Name;
            var nameLabel = new Label()
            {
                Text = nameText,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(textX, 8),
                Parent = rowPanel
            };

            // Decision badge
            int badgeX = textX + nameLabel.Width + 6;
            string badgeText = GetDecisionBadgeText(node.Decision);
            Color badgeColor = GetDecisionBadgeColor(node.Decision);
            var badgeLabel = new Label()
            {
                Text = badgeText,
                TextColor = badgeColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(badgeX, 8),
                Parent = rowPanel
            };

            // Cost display: inline coin for nodes with SubtreeCost
            if (node.SubtreeCost.HasValue && node.SubtreeCost.Value > 0)
            {
                int costX = badgeX + badgeLabel.Width + 6;
                BuildInlineCoin(rowPanel, node.SubtreeCost.Value, costX);
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
                if (depth < 2)
                {
                    // Default-expanded: build children now
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

                arrowLabel.Click += (_, __) =>
                {
                    if (!state.ChildrenBuilt)
                    {
                        foreach (var child in state.Node.Children)
                        {
                            RenderTreeNode(child, state.ChildContainer, state.PanelWidth, state.Depth + 1);
                        }
                        state.ChildrenBuilt = true;
                    }
                    state.IsExpanded = !state.IsExpanded;
                    state.ChildContainer.Visible = state.IsExpanded;
                    state.ArrowLabel.Text = state.IsExpanded ? "\u25BC" : "\u25B6";
                    state.ChildContainer.Parent.Invalidate();
                };
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

        // --- Coin display helpers (reused from original) ---

        private static void BuildCoinDisplay(Panel parent, long copper)
        {
            if (copper < 0) copper = 0;

            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;

            int x = 0;
            var totalLabel = new Label()
            {
                Text = "  Total: ",
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

        private static void BuildInlineCoin(Panel parent, long copper, int startX)
        {
            if (copper < 0) copper = 0;

            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;

            int x = startX;
            x = AddCoinSegment(parent, x, 156904, gold.ToString(), 6);
            x = AddCoinSegment(parent, x, 156907, silver.ToString(), 6);
            AddCoinSegment(parent, x, 156902, cop.ToString(), 6);
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
