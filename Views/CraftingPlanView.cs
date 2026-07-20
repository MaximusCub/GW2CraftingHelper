using Blish_HUD;
using Blish_HUD.Content;
using MonoGame.Extended.BitmapFonts;
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

        // Bumped by every PreserveScrollAcross call; an in-flight restore
        // Tick loop compares its captured value against the current one
        // each frame and bails as soon as a newer restore has superseded it.
        private int _scrollRestoreGeneration;

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

        // Blish HUD keeps a Panel's Scrollbar in a private field and resets
        // it to top whenever content height changes; the field is the only
        // handle that lets us restore the position (VerticalScrollOffset is
        // overwritten from the scrollbar every frame). Resolved once; if a
        // future Blish rename removes it we degrade to today's reset-to-top.
        private static readonly System.Reflection.FieldInfo PanelScrollbarField =
            typeof(Panel).GetField(
                "_panelScrollbar",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

        /// <summary>
        /// Runs a layout-mutating action and restores the content panel's
        /// scroll position afterwards. Nested AutoSize flow panels converge
        /// height over several frames, so the restore re-asserts each frame
        /// until the computed ratio stabilizes (max 10 frames).
        /// </summary>
        private void PreserveScrollAcross(Action mutate)
        {
            int saved = _contentPanel?.VerticalScrollOffset ?? 0;
            int capturedGeneration = ++_scrollRestoreGeneration;
            mutate();
            if (saved > 0)
            {
                RestoreScrollOffset(saved, capturedGeneration);
            }
        }

        private void RestoreScrollOffset(int savedOffset, int capturedGeneration)
        {
            if (_contentPanel == null || PanelScrollbarField == null)
            {
                return;
            }

            var capturedPanel = _contentPanel;
            int attempts = 0;
            float lastRatio = -1f;

            void Tick(GameTime _)
            {
                // A newer restore superseded this loop, or Build() swapped
                // in a fresh content panel: stop immediately rather than
                // fight the current restore or scroll a stale/disposed panel.
                if (capturedGeneration != _scrollRestoreGeneration || capturedPanel != _contentPanel)
                {
                    return;
                }

                try
                {
                    var scrollbar = PanelScrollbarField.GetValue(capturedPanel) as Scrollbar;
                    if (scrollbar == null)
                    {
                        return;
                    }

                    int contentHeight = 0;
                    foreach (var child in capturedPanel.Children)
                    {
                        if (child.Visible && child.Bottom > contentHeight)
                        {
                            contentHeight = child.Bottom;
                        }
                    }

                    float ratio = ScrollMath.RatioForOffset(
                        savedOffset, contentHeight, capturedPanel.Height);
                    scrollbar.ScrollDistance = ratio;

                    attempts++;
                    bool stable = System.Math.Abs(ratio - lastRatio) < 0.0005f;
                    lastRatio = ratio;
                    if (attempts < 10 && !stable)
                    {
                        GameService.Overlay.QueueMainThreadUpdate(Tick);
                    }
                }
                catch
                {
                    // Reflection/layout mismatch: degrade to reset-to-top.
                }
            }

            GameService.Overlay.QueueMainThreadUpdate(Tick);
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
                PreserveScrollAcross(() => RenderPlan(_currentPlan));
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

            CreatePlanHeader(vm, panelWidth);

            // Separator under header
            new Panel()
            {
                Size = new Point(panelWidth, 2),
                BackgroundColor = new Color(180, 180, 180),
                Parent = _contentPanel
            };

            // Section order mirrors gw2efficiency's calculator page: total
            // cost breakdown, then the recipe tree, then everything else in
            // the builder's emission order (used materials, shopping list,
            // required disciplines, required recipes, crafting steps). The
            // tree lives outside vm.Sections (it renders from vm.TreeRoot),
            // so it is positioned explicitly between the two loops below.
            PlanSectionViewModel summarySection = null;
            foreach (var section in vm.Sections)
            {
                if (section.SectionType == PlanSectionType.Summary)
                {
                    summarySection = section;
                    break;
                }
            }
            if (summarySection != null)
            {
                CreateCollapsibleSection(summarySection, panelWidth);
            }

            if (vm.TreeRoot != null)
            {
                CreateTreeSection(vm.TreeRoot, panelWidth);
            }

            foreach (var section in vm.Sections)
            {
                if (section.SectionType == PlanSectionType.Summary) continue;
                CreateCollapsibleSection(section, panelWidth);
            }
        }

        /// <summary>
        /// Plan header: rarity-framed item icon + two-tone title ("Crafting
        /// Plan for " in white, item name in its rarity color) + grey
        /// quantity, centered as a unit; timestamp right-aligned below.
        /// Mirrors gw2e's centered .tooltip-item + name header block.
        /// </summary>
        private void CreatePlanHeader(PlanViewModel vm, int panelWidth)
        {
            const int headerHeight = 60;
            const int headerTopPad = 10;
            const int headerBottomPad = 4;
            const int iconSize = 40;
            const int iconBorder = 2;
            const int iconPad = 8;

            int frameSize = iconSize + iconBorder * 2;

            var titleFont = GameService.Content.DefaultFont18;
            var qtyFont = GameService.Content.DefaultFont16;

            string prefixText = "Crafting Plan for ";
            string nameText = vm.TargetItemName ?? "Unknown Item";
            string qtyText = vm.TargetQuantity > 1 ? $" x {vm.TargetQuantity}" : "";

            var prefixMeasure = titleFont.MeasureString(prefixText);
            var nameMeasure = titleFont.MeasureString(nameText);
            int prefixWidth = (int)System.Math.Ceiling(prefixMeasure.Width);
            int nameWidth = (int)System.Math.Ceiling(nameMeasure.Width);
            int textHeight = (int)System.Math.Ceiling(prefixMeasure.Height);

            int qtyWidth = 0;
            if (qtyText.Length > 0)
            {
                qtyWidth = (int)System.Math.Ceiling(qtyFont.MeasureString(qtyText).Width);
            }

            int totalTitleWidth = frameSize + iconPad + prefixWidth + nameWidth + qtyWidth;
            int startX = System.Math.Max(0, (panelWidth - totalTitleWidth) / 2);
            int centerRegion = headerHeight - headerTopPad - headerBottomPad;
            int iconY = headerTopPad + (centerRegion - frameSize) / 2;
            // Anchor text to icon's visual center with -2px optical nudge for descenders
            int textY = iconY + (frameSize - textHeight) / 2 - 2;

            var titlePanel = new Panel()
            {
                Size = new Point(panelWidth, headerHeight),
                Parent = _contentPanel
            };

            CreateRarityFramedIcon(
                titlePanel, vm.TargetIconUrl, vm.TargetRarity, startX, iconY,
                iconSize: iconSize, borderThickness: iconBorder);

            int textX = startX + frameSize + iconPad;
            new Label()
            {
                Text = prefixText,
                Font = titleFont,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(textX, textY),
                Parent = titlePanel
            };
            textX += prefixWidth;

            new Label()
            {
                Text = nameText,
                Font = titleFont,
                TextColor = GetRarityNameColor(vm.TargetRarity),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(textX, textY),
                Parent = titlePanel
            };
            textX += nameWidth;

            if (qtyText.Length > 0)
            {
                // DefaultFont16 sits a little taller than Font18's cap
                // height at this weight; +3 keeps its baseline visually
                // aligned with the name label instead of reading "raised".
                new Label()
                {
                    Text = qtyText,
                    Font = qtyFont,
                    TextColor = new Color(170, 170, 170),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(textX, textY + 3),
                    Parent = titlePanel
                };
            }

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
        }

        /// <summary>
        /// Bundle returned by CreateSectionHeader: the header panel (parent
        /// for any extra header-row buttons a caller adds), its arrow label,
        /// and the already-wired content FlowPanel rows should be added to.
        /// </summary>
        private sealed class SectionHeaderHandle
        {
            public Panel HeaderPanel;
            public Label ArrowLabel;
            public FlowPanel ContentFlow;
        }

        /// <summary>
        /// Shared chrome for every collapsible section (the 6 PlanSectionType
        /// sections and the Recipe Tree alike): caret + Font18 title, a 1px
        /// divider spanning the full width under the header, a hover wash on
        /// the whole clickable row, and click-to-toggle with expansion state
        /// persisted in _sectionExpansion under sectionKey. suppressToggle
        /// lets a caller with its own header-row buttons (the tree's
        /// Expand All / Collapse All / presets) veto the toggle when the
        /// click landed on one of them.
        /// </summary>
        private SectionHeaderHandle CreateSectionHeader(
            string title, PlanSectionType sectionKey, int panelWidth, bool defaultExpanded,
            Func<bool> suppressToggle = null)
        {
            bool expanded = _sectionExpansion.TryGetValue(sectionKey, out bool userExpanded)
                ? userExpanded
                : defaultExpanded;

            var headerPanel = new Panel()
            {
                Size = new Point(panelWidth, 30),
                BackgroundColor = Color.Transparent,
                Parent = _contentPanel
            };
            headerPanel.MouseEntered += (_, __) => headerPanel.BackgroundColor = Color.White * 0.05f;
            headerPanel.MouseLeft += (_, __) => headerPanel.BackgroundColor = Color.Transparent;

            // ASCII "v"/">" rather than the U+25BC/U+25B6 triangle glyphs used
            // by the tree's own node carets. Re-attempted during the M24
            // adversarial-review pass on the theory that a separate
            // default-font Label (the tree's own pattern) would render the
            // triangle correctly here too; a pixel-level scan of a fresh
            // screenshot showed the triangle failing to render for BOTH the
            // section header AND, in that same session, the tree's own row
            // caret (previously "confirmed working") - so the premise that
            // motivated re-attempting Unicode did not hold this time either,
            // and ASCII remains the only glyph confirmed to render here.
            var headerArrow = new Label()
            {
                Text = expanded ? "v" : ">",
                TextColor = Color.White,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(4, 6),
                Parent = headerPanel
            };

            new Label()
            {
                Text = title,
                Font = GameService.Content.DefaultFont18,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(22, 4),
                Parent = headerPanel
            };

            // Divider under the header - identical chrome for every section.
            new Panel()
            {
                Size = new Point(panelWidth, 1),
                Location = new Point(0, 29),
                BackgroundColor = new Color(90, 90, 90),
                Parent = headerPanel
            };

            var contentFlow = new FlowPanel()
            {
                Size = new Point(panelWidth, 0),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Visible = expanded,
                Parent = _contentPanel,
                HeightSizingMode = SizingMode.AutoSize
            };

            headerPanel.Click += (_, __) =>
            {
                if (suppressToggle != null && suppressToggle())
                {
                    return;
                }
                PreserveScrollAcross(() =>
                {
                    contentFlow.Visible = !contentFlow.Visible;
                    _sectionExpansion[sectionKey] = contentFlow.Visible;
                    headerArrow.Text = contentFlow.Visible ? "v" : ">";
                    _contentPanel.Invalidate();
                });
            };

            return new SectionHeaderHandle
            {
                HeaderPanel = headerPanel,
                ArrowLabel = headerArrow,
                ContentFlow = contentFlow
            };
        }

        private void CreateCollapsibleSection(PlanSectionViewModel section, int panelWidth)
        {
            var header = CreateSectionHeader(section.Title, section.SectionType, panelWidth, section.IsDefaultExpanded);
            var contentFlow = header.ContentFlow;

            // Every section gets its own table-column layout (spec: aligned
            // columns everywhere, not free-flowing text rows), so each has a
            // dedicated body builder rather than a generic per-row dispatch.
            switch (section.SectionType)
            {
                case PlanSectionType.Summary:
                    CreateSummarySectionBody(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.UsedMaterials:
                    CreateUsedMaterialsBody(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.ShoppingList:
                    CreateShoppingListBody(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.CraftingSteps:
                    CreateCraftingStepsBody(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.RequiredDisciplines:
                    CreateDisciplinesBody(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.RequiredRecipes:
                    CreateRecipesBody(section, contentFlow, panelWidth);
                    break;
                default:
                    // Defensive fallback for a future section type added
                    // without a dedicated body builder - never leave a
                    // section silently empty.
                    foreach (var row in section.Rows)
                    {
                        CreateTextRow(row.Label, contentFlow, panelWidth);
                    }
                    break;
            }
        }

        /// <summary>
        /// 1px divider at the bottom edge of a row panel - the shared "list
        /// row" chrome used by every table-style section except the tree
        /// (which uses indent guidelines instead, per gw2e's own convention).
        /// </summary>
        private static void CreateRowDivider(Panel rowPanel, int panelWidth, int rowHeight)
        {
            new Panel()
            {
                Size = new Point(panelWidth, 1),
                Location = new Point(0, rowHeight - 1),
                BackgroundColor = new Color(70, 70, 70),
                Parent = rowPanel
            };
        }

        private static Label CreateRightAlignedLabel(
            Panel parent, string text, BitmapFont font, Color color, int rightEdgeX, int y)
        {
            int width = (int)System.Math.Ceiling(font.MeasureString(text ?? "").Width);
            return new Label()
            {
                Text = text ?? "",
                Font = font,
                TextColor = color,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(rightEdgeX - width, y),
                Parent = parent
            };
        }

        /// <summary>
        /// Small grey informational tag (reuses the tree's Locked pill
        /// styling) - used for the shopping list's source tag and anywhere
        /// else a short non-interactive label needs pill chrome.
        /// </summary>
        private static void CreateSmallTag(Panel parent, string text, int x, int y)
        {
            var font = GameService.Content.DefaultFont12;
            int textWidth = (int)System.Math.Ceiling(font.MeasureString(text).Width);
            int width = textWidth + 12;
            GetPillColors(PillKind.Locked, out Color border, out Color fill);

            var outer = new Panel()
            {
                Size = new Point(width, 18),
                Location = new Point(x, y),
                BackgroundColor = border,
                Parent = parent
            };
            var inner = new Panel()
            {
                Size = new Point(width - 2, 16),
                Location = new Point(1, 1),
                BackgroundColor = fill,
                Parent = outer
            };
            new Label()
            {
                Text = text,
                Font = font,
                TextColor = border,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point((width - 2 - textWidth) / 2, 1),
                Parent = inner
            };
        }

        // --- Used Materials section ---

        private void CreateUsedMaterialsBody(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateUsedMaterialRow(section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1);
            }
        }

        private static void CreateUsedMaterialRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast)
        {
            const int rowHeight = 36;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 8, 1);

            const int nameX = 50;
            int qtyRightEdge = panelWidth - 8;
            var font = GameService.Content.DefaultFont14;

            string qtyText = $"{row.Quantity}x";
            int qtyWidth = (int)System.Math.Ceiling(font.MeasureString(qtyText).Width);
            int nameMaxWidth = System.Math.Max(20, qtyRightEdge - qtyWidth - 12 - nameX);

            string fullName = row.Label ?? "";
            string displayName = EllipsizeToWidth(font, fullName, nameMaxWidth);
            new Label()
            {
                Text = displayName,
                Font = font,
                TextColor = GetRarityNameColor(row.Rarity),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(nameX, 9),
                Parent = rowPanel
            };
            if (displayName != fullName)
            {
                rowPanel.BasicTooltipText = fullName;
            }

            new Label()
            {
                Text = qtyText,
                Font = font,
                TextColor = new Color(200, 200, 200),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(qtyRightEdge - qtyWidth, 9),
                Parent = rowPanel
            };

            if (!isLast) CreateRowDivider(rowPanel, panelWidth, rowHeight);
        }

        // --- Shopping List section ---

        private void CreateShoppingListBody(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            CreateShoppingListHeaderRow(contentFlow, panelWidth);
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateShoppingRow(section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1);
            }
        }

        // Reserved right-aligned price columns for the shopping list's Each
        // and Total prices: both anchor to a fixed right edge and grow
        // LEFTWARD, so a gold-value amount in either column can never grow
        // into the other's space. Previously "Each" was left-aligned at a
        // fixed start x with no bound on its right edge, sharing an
        // effectively unbounded budget with "Total" - the two overlapped
        // for routine gold-value rows.
        private const int ShoppingColTotalWidth = 150;
        private const int ShoppingColGap = 12;

        private static void CreateShoppingListHeaderRow(FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel() { Size = new Point(panelWidth, 22), Parent = parent };
            var font = GameService.Content.DefaultFont12;
            var color = new Color(153, 153, 153);

            int qtyRightEdge = panelWidth - 260;
            int totalRightEdge = panelWidth - 8;
            int eachRightEdge = totalRightEdge - ShoppingColTotalWidth - ShoppingColGap;

            new Label()
            {
                Text = "Item", Font = font, TextColor = color,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(50, 4), Parent = rowPanel
            };
            CreateRightAlignedLabel(rowPanel, "Amount", font, color, qtyRightEdge, 4);
            CreateRightAlignedLabel(rowPanel, "Each", font, color, eachRightEdge, 4);
            CreateRightAlignedLabel(rowPanel, "Total", font, color, totalRightEdge, 4);
        }

        private static string ShoppingSourceTag(PlanRowType rowType)
        {
            switch (rowType)
            {
                case PlanRowType.ShoppingVendor: return "VENDOR";
                case PlanRowType.ShoppingCurrency: return "CURRENCY";
                case PlanRowType.ShoppingUnknown: return "UNKNOWN";
                default: return null; // ShoppingBuy: plain TP purchase, no tag needed
            }
        }

        private static void CreateShoppingRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast)
        {
            const int rowHeight = 36;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 8, 1);

            const int nameX = 50;
            int qtyRightEdge = panelWidth - 260;
            int totalRightEdge = panelWidth - 8;
            int eachRightEdge = totalRightEdge - ShoppingColTotalWidth - ShoppingColGap;
            var font = GameService.Content.DefaultFont14;

            string qtyText = $"{row.Quantity}x";
            int qtyWidth = (int)System.Math.Ceiling(font.MeasureString(qtyText).Width);
            int nameMaxWidth = System.Math.Max(20, qtyRightEdge - qtyWidth - 12 - nameX);

            string fullName = row.Label ?? "";
            string displayName = EllipsizeToWidth(font, fullName, nameMaxWidth);
            var nameLabel = new Label()
            {
                Text = displayName,
                Font = font,
                TextColor = GetRarityNameColor(row.Rarity),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(nameX, 9),
                Parent = rowPanel
            };
            if (displayName != fullName)
            {
                rowPanel.BasicTooltipText = fullName;
            }

            string sourceTag = ShoppingSourceTag(row.RowType);
            if (!string.IsNullOrEmpty(sourceTag))
            {
                CreateSmallTag(rowPanel, sourceTag, nameX + nameLabel.Width + 8, 9);
            }

            new Label()
            {
                Text = qtyText,
                Font = font,
                TextColor = new Color(200, 200, 200),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(qtyRightEdge - qtyWidth, 9),
                Parent = rowPanel
            };

            if (row.UnitCoinValue > 0)
            {
                LayoutCoinSegmentsRightAligned(rowPanel, BuildCoinSegments(row.UnitCoinValue, font), eachRightEdge, 9, font);
            }
            if (row.CoinValue > 0)
            {
                LayoutCoinSegmentsRightAligned(rowPanel, BuildCoinSegments(row.CoinValue, font), totalRightEdge, 9, font);
            }

            if (!isLast) CreateRowDivider(rowPanel, panelWidth, rowHeight);
        }

        // --- Crafting Steps section ---

        private void CreateCraftingStepsBody(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateCraftStepRow(section.Rows[i], i + 1, contentFlow, panelWidth, i == section.Rows.Count - 1);
            }
        }

        private static void CreateCraftStepRow(
            PlanRowViewModel row, int stepNumber, FlowPanel parent, int panelWidth, bool isLast)
        {
            const int rowHeight = 44;
            const int badgeSize = 36;
            const int badgeX = 8;
            const int badgeY = 4;
            const int iconX = 52;
            const int textX = 94; // iconX(52) + frame(34) + gap(8)

            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            new Panel()
            {
                Size = new Point(badgeSize, badgeSize),
                Location = new Point(badgeX, badgeY),
                BackgroundColor = Color.White * 0.08f,
                Parent = rowPanel
            };
            string numberText = stepNumber.ToString();
            var numberFont = GameService.Content.DefaultFont18;
            var numberMeasure = numberFont.MeasureString(numberText);
            int numberWidth = (int)System.Math.Ceiling(numberMeasure.Width);
            int numberHeight = (int)System.Math.Ceiling(numberMeasure.Height);
            new Label()
            {
                Text = numberText,
                Font = numberFont,
                TextColor = Color.White,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(badgeX + (badgeSize - numberWidth) / 2, badgeY + (badgeSize - numberHeight) / 2),
                Parent = rowPanel
            };

            CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, iconX, 5);

            var textFont = GameService.Content.DefaultFont16;
            var greyColor = new Color(170, 170, 170);
            int x = textX;

            var craftLabel = new Label()
            {
                Text = "Craft ", Font = textFont, TextColor = greyColor,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(x, 13), Parent = rowPanel
            };
            x += craftLabel.Width;

            var qtyLabel = new Label()
            {
                Text = $"{row.Quantity}x ", Font = textFont, TextColor = greyColor,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(x, 13), Parent = rowPanel
            };
            x += qtyLabel.Width;

            new Label()
            {
                Text = row.Label ?? "", Font = textFont, TextColor = GetRarityNameColor(row.Rarity),
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(x, 13), Parent = rowPanel
            };

            if (!string.IsNullOrEmpty(row.Sublabel))
            {
                CreateRightAlignedLabel(
                    rowPanel, row.Sublabel, GameService.Content.DefaultFont12,
                    new Color(153, 153, 153), panelWidth - 8, 16);
            }

            if (!isLast) CreateRowDivider(rowPanel, panelWidth, rowHeight);
        }

        // --- Required Disciplines / Required Recipes sections (c-table) ---

        private static void CreateCTableHeaderRow(
            FlowPanel parent, int panelWidth, string leftLabel, int leftX, string rightLabel)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, 26),
                BackgroundColor = new Color(35, 35, 35),
                Parent = parent
            };
            var font = GameService.Content.DefaultFont14;
            new Label()
            {
                Text = leftLabel, Font = font, TextColor = Color.White,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(leftX, 5), Parent = rowPanel
            };
            CreateRightAlignedLabel(rowPanel, rightLabel, font, Color.White, panelWidth - 8, 5);
        }

        private void CreateDisciplinesBody(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            CreateCTableHeaderRow(contentFlow, panelWidth, "Discipline", 8, "Level");
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateDisciplineRow(section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1);
            }
        }

        private static void CreateDisciplineRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast)
        {
            const int rowHeight = 32;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };
            var font = GameService.Content.DefaultFont14;

            new Label()
            {
                Text = row.Label ?? "", Font = font,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(8, 7), Parent = rowPanel
            };
            CreateRightAlignedLabel(rowPanel, row.Sublabel, font, Color.White, panelWidth - 8, 7);

            if (!isLast) CreateRowDivider(rowPanel, panelWidth, rowHeight);
        }

        private void CreateRecipesBody(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            CreateCTableHeaderRow(contentFlow, panelWidth, "Recipe", 50, "Status");
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateRecipeRow(section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1);
            }
        }

        private static void CreateRecipeRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast)
        {
            bool hasSublabel = !string.IsNullOrEmpty(row.Sublabel);
            int rowHeight = hasSublabel ? 44 : 32;

            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 8, 1);

            var font = GameService.Content.DefaultFont14;
            int nameY = hasSublabel ? 4 : 8;
            new Label()
            {
                Text = row.Label ?? "",
                Font = font,
                TextColor = GetRarityNameColor(row.Rarity),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(50, nameY),
                Parent = rowPanel
            };

            if (hasSublabel)
            {
                new Label()
                {
                    Text = row.Sublabel,
                    Font = GameService.Content.DefaultFont12,
                    TextColor = new Color(170, 170, 170),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(50, 22),
                    Parent = rowPanel
                };
            }

            if (!string.IsNullOrEmpty(row.StatusTag))
            {
                Color statusColor = Color.White;
                if (row.StatusTag == "Missing!")
                {
                    statusColor = new Color(255, 100, 100);
                }
                else if (row.StatusTag == "Auto-learned")
                {
                    statusColor = new Color(150, 200, 150);
                }
                CreateRightAlignedLabel(rowPanel, row.StatusTag, font, statusColor, panelWidth - 8, hasSublabel ? 10 : 8);
            }

            if (!isLast) CreateRowDivider(rowPanel, panelWidth, rowHeight);
        }

        /// <summary>
        /// gw2e's cost-breakdown: a centered row of equal-width stat tiles,
        /// one per CoinTotal row (Total, Sell value, Profit/Loss - up to the
        /// spec's 5 when all are applicable). Non-coin rows (currency costs)
        /// are handled separately as full-width rows underneath.
        /// </summary>
        private static void CreateCostTileRow(List<PlanRowViewModel> coinRows, FlowPanel parent, int panelWidth)
        {
            int tileCount = coinRows.Count;
            if (tileCount == 0) return;

            const int rowHeight = 56;
            const int totalMargin = 40;
            const int minTileWidth = 80;
            int tileWidth = System.Math.Max(minTileWidth, (panelWidth - totalMargin) / tileCount);
            int rowContentWidth = tileWidth * tileCount;
            int startX = System.Math.Max(0, (panelWidth - rowContentWidth) / 2);

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, rowHeight),
                Parent = parent
            };

            var captionFont = GameService.Content.DefaultFont12;
            var amountFont = GameService.Content.DefaultFont16;
            var captionColor = new Color(153, 153, 153);

            for (int i = 0; i < tileCount; i++)
            {
                int tileX = startX + i * tileWidth;
                var row = coinRows[i];

                string caption = TileCaptionFor(row.Label);
                int captionWidth = (int)System.Math.Ceiling(captionFont.MeasureString(caption).Width);
                new Label()
                {
                    Text = caption,
                    Font = captionFont,
                    TextColor = captionColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(tileX + System.Math.Max(0, (tileWidth - captionWidth) / 2), 6),
                    Parent = rowPanel
                };

                var segments = BuildCoinSegments(row.CoinValue, amountFont);
                int segmentsWidth = TotalCoinSegmentsWidth(segments);
                int coinStartX = tileX + System.Math.Max(0, (tileWidth - segmentsWidth) / 2);
                LayoutCoinSegments(rowPanel, segments, coinStartX, 30, amountFont);
            }
        }

        /// <summary>
        /// Strips the parenthetical qualifier off a Summary row label
        /// ("Sell value (5x, after 15% TP fees)" -> "Sell value") so tile
        /// captions stay short, like gw2e's "Buy price" / "Sell price".
        /// </summary>
        private static string TileCaptionFor(string rowLabel)
        {
            if (string.IsNullOrEmpty(rowLabel)) return "";
            int parenIdx = rowLabel.IndexOf('(');
            return (parenIdx > 0 ? rowLabel.Substring(0, parenIdx) : rowLabel).Trim();
        }

        private void CreateSummarySectionBody(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            var coinRows = new List<PlanRowViewModel>();
            var otherRows = new List<PlanRowViewModel>();
            foreach (var row in section.Rows)
            {
                if (row.RowType == PlanRowType.CoinTotal) coinRows.Add(row);
                else otherRows.Add(row);
            }

            if (coinRows.Count > 0)
            {
                CreateCostTileRow(coinRows, contentFlow, panelWidth);
            }

            // The only other row type in this section is CurrencyCost.
            foreach (var row in otherRows)
            {
                CreateTextRow(row.Label, contentFlow, panelWidth);
            }
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

            // Whether lazily-built children (built on first expand) should
            // render dimmed - computed once from this node's own dimmed
            // state and decision, so it stays correct however many frames
            // later the user actually expands the node.
            public bool ChildDimmed;
        }

        // States for the current render pass; rebuilt with the tree itself.
        private readonly List<TreeNodeState> _treeNodeStates = new List<TreeNodeState>();

        private void CreateTreeSection(CraftingTreeNode treeRoot, int panelWidth)
        {
            _treeNodeStates.Clear();

            // The header's Click-to-toggle is wired inside CreateSectionHeader
            // before these buttons exist; suppressToggle captures them by
            // reference and reads their (assigned-below) MouseOver lazily,
            // at click time - not at subscription time.
            StandardButton expandAllButton = null;
            StandardButton collapseAllButton = null;
            StandardButton bestPathButton = null;
            StandardButton craftAllButton = null;
            StandardButton buyAllButton = null;

            // Guard uses PRESS-time hover state: with a release-time check,
            // pressing on the header background and releasing over a button
            // dropped the click entirely (neither toggle nor button fired).
            bool pressStartedOnButton = false;

            var header = CreateSectionHeader(
                "Recipe Tree", PlanSectionType.RecipeTree, panelWidth, true,
                suppressToggle: () => pressStartedOnButton);
            var headerPanel = header.HeaderPanel;
            var treeFlow = header.ContentFlow;

            // Header-row buttons, right-to-left per the spec's fixed
            // offsets-from-the-right layout: Collapse All, Expand All, then
            // the presets (Buy All / Craft All / Best Path) continuing
            // leftward with 4px gaps so they never collide with the title.
            int cursorX = panelWidth;
            StandardButton PlaceButtonRight(string text, int width)
            {
                cursorX -= width;
                var button = new StandardButton()
                {
                    Text = text,
                    Size = new Point(width, 24),
                    Location = new Point(cursorX, 3),
                    Parent = headerPanel
                };
                cursorX -= 4;
                return button;
            }

            collapseAllButton = PlaceButtonRight("Collapse All", 96);
            expandAllButton = PlaceButtonRight("Expand All", 92);
            buyAllButton = PlaceButtonRight("Buy All", 70);
            craftAllButton = PlaceButtonRight("Craft All", 76);
            bestPathButton = PlaceButtonRight("Best Path", 80);

            RenderTreeNode(treeRoot, treeFlow, panelWidth, 0, dimmed: false);

            // Decision presets: clear overrides / force craft-everywhere /
            // force buy-everywhere (feasibility respected by the solver).
            bestPathButton.Click += (_, __) =>
            {
                if (_nodeOverrides.Count == 0) return;
                _nodeOverrides.Clear();
                ApplyOverridesAndResolve();
            };
            craftAllButton.Click += (_, __) => ApplyPreset(AcquisitionSource.Craft);
            buyAllButton.Click += (_, __) => ApplyPreset(AcquisitionSource.BuyFromTp);

            expandAllButton.Click += (_, __) => PreserveScrollAcross(() =>
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
                            RenderTreeNode(child, s.ChildContainer, s.PanelWidth, s.Depth + 1, s.ChildDimmed);
                        }
                        s.ChildrenBuilt = true;
                    }
                    s.IsExpanded = true;
                    _nodeExpansion[s.Node.NodeId] = true;
                    s.ChildContainer.Visible = true;
                    s.ArrowLabel.Text = "\u25BC";
                }
                treeFlow.Invalidate();
            });

            collapseAllButton.Click += (_, __) => PreserveScrollAcross(() =>
            {
                foreach (var s in _treeNodeStates)
                {
                    s.IsExpanded = false;
                    _nodeExpansion[s.Node.NodeId] = false;
                    s.ChildContainer.Visible = false;
                    s.ArrowLabel.Text = "\u25B6";
                }
                treeFlow.Invalidate();
            });

            headerPanel.LeftMouseButtonPressed += (_, __) =>
            {
                pressStartedOnButton =
                    expandAllButton.MouseOver || collapseAllButton.MouseOver ||
                    bestPathButton.MouseOver || craftAllButton.MouseOver ||
                    buyAllButton.MouseOver;
            };
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
                PreserveScrollAcross(() => RenderPlan(vm));
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

        // Fixed tree-row column grid (spec: "the key gw2e table look" - every
        // row aligns regardless of depth). Right-anchored columns (pills,
        // cost) sit at the same x on every row; only the left side (caret,
        // icon, name) shifts with indent.
        private const int TreeIndentPer = 24;
        private const int TreeCaretColWidth = 18;
        private const int TreeIconSize = 32;
        private const int TreeIconBorder = 1;
        private const int TreeIconFrameSize = TreeIconSize + TreeIconBorder * 2;
        private const int TreeNameGap = 6;
        private const int TreeRowHeight = 40;
        private const int TreePillColumnWidth = 240;
        private const int TreeCostColumnWidth = 150;
        private const int TreeRightMargin = 8;

        private void RenderTreeNode(CraftingTreeNode node, FlowPanel parent, int panelWidth, int depth, bool dimmed)
        {
            int indent = depth * TreeIndentPer;
            bool hasChildren = node.Children.Count > 0;

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, TreeRowHeight),
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

            // Caret column: fixed width even for leaf rows (no children ->
            // no glyph, but the icon column still starts at the same x as
            // every sibling), so caret state is scannable at a glance.
            // Reference-branch nodes (dimmed - see the childDimmed comment
            // below) always start collapsed regardless of depth, so a bought
            // node's "what it would cost to craft instead" subtree does not
            // visually explode the plan the moment its parent expands.
            // Non-reference nodes keep the existing depth<2 default.
            bool isExpanded = _nodeExpansion.TryGetValue(node.NodeId, out bool userExpanded)
                ? userExpanded
                : (!dimmed && depth < 2);
            Label arrowLabel = null;
            if (hasChildren)
            {
                Color arrowColor = dimmed ? Color.White * 0.35f : Color.White;
                arrowLabel = new Label()
                {
                    Text = isExpanded ? "\u25BC" : "\u25B6",
                    TextColor = arrowColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(indent, 12),
                    Parent = rowPanel
                };
            }

            // Icon column: rarity-framed, dimmed reference branches get a
            // neutral frame plus a dark scrim over the icon itself (Blish
            // panels have no tint/filter property, so a translucent black
            // overlay approximates gw2e's grayscale+opacity filter).
            int iconX = indent + TreeCaretColWidth;
            Color frameColor = dimmed ? new Color(60, 60, 60) : GetRarityBorderColor(node.Rarity);
            CreateRarityFramedIcon(rowPanel, node.IconUrl, frameColor, iconX, 3, TreeIconSize, TreeIconBorder);
            if (dimmed)
            {
                new Panel()
                {
                    Size = new Point(TreeIconSize, TreeIconSize),
                    Location = new Point(iconX + TreeIconBorder, 3 + TreeIconBorder),
                    BackgroundColor = Color.Black * 0.5f,
                    Parent = rowPanel
                };
            }

            // Name column: fixed x regardless of depth's remaining width;
            // clipped with an ellipsis against the pill column so long names
            // never collide with the fixed-position columns to its right.
            int nameX = indent + TreeCaretColWidth + TreeIconFrameSize + TreeNameGap;
            int pillColX = panelWidth - (TreeRightMargin + TreeCostColumnWidth) - TreePillColumnWidth;
            int costRightEdge = panelWidth - TreeRightMargin;
            int nameMaxWidth = System.Math.Max(20, pillColX - nameX - 8);

            var nameFont = GameService.Content.DefaultFont14;
            string qtyPrefix = node.Quantity > 0 ? $"{node.Quantity}x " : "";
            int qtyWidth = qtyPrefix.Length > 0
                ? (int)System.Math.Ceiling(nameFont.MeasureString(qtyPrefix).Width)
                : 0;
            int nameAvailWidth = System.Math.Max(10, nameMaxWidth - qtyWidth);
            string fullName = node.Name ?? "";
            string displayName = EllipsizeToWidth(nameFont, fullName, nameAvailWidth);

            Color qtyColor = new Color(170, 170, 170);
            Color nameColor = GetRarityNameColor(node.Rarity);
            if (dimmed)
            {
                qtyColor *= 0.35f;
                nameColor *= 0.35f;
            }

            if (qtyPrefix.Length > 0)
            {
                new Label()
                {
                    Text = qtyPrefix,
                    Font = nameFont,
                    TextColor = qtyColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(nameX, 12),
                    Parent = rowPanel
                };
            }
            new Label()
            {
                Text = displayName,
                Font = nameFont,
                TextColor = nameColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(nameX + qtyWidth, 12),
                Parent = rowPanel
            };

            var tooltipParts = new List<string>();
            if (displayName != fullName)
            {
                tooltipParts.Add(fullName);
            }
            if (node.UnitCost.HasValue && node.Quantity > 1 &&
                (node.Decision == CraftingDecision.BuyFromTp ||
                 node.Decision == CraftingDecision.BuyFromVendor))
            {
                tooltipParts.Add("Unit price: " + FormatCoinText(node.UnitCost.Value));
            }
            if (tooltipParts.Count > 0)
            {
                rowPanel.BasicTooltipText = string.Join("\n", tooltipParts);
            }

            // Decision pill column: one pill per feasible source (direct
            // selection - click sets the override and re-solves), or a
            // single locked/HAVE/CURRENCY pill when there is no choice.
            var pillPanels = RenderDecisionPills(rowPanel, node, pillColX, 10, dimmed);

            // Cost column: right-aligned so coin amounts line up vertically
            // across every row regardless of digit count.
            if (node.SubtreeCost.HasValue && node.SubtreeCost.Value > 0)
            {
                var costFont = GameService.Content.DefaultFont14;
                var segments = BuildCoinSegments(node.SubtreeCost.Value, costFont);
                LayoutCoinSegmentsRightAligned(
                    rowPanel, segments, costRightEdge, 12, costFont, dimmed ? 0.35f : 1f);
            }

            // Child container. Children of a non-Craft decision are gw2e's
            // ".not-crafted" informational reference branch (what it would
            // cost to craft instead) - dimmed, and the flag does not stack
            // on already-dimmed branches.
            if (hasChildren)
            {
                bool childDimmed = dimmed || node.Decision != CraftingDecision.Craft;

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
                    PanelWidth = panelWidth,
                    ChildDimmed = childDimmed
                };
                _treeNodeStates.Add(state);
                if (isExpanded)
                {
                    foreach (var child in node.Children)
                    {
                        RenderTreeNode(child, childFlow, panelWidth, depth + 1, childDimmed);
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
                    // Pills have their own click actions; do not also treat
                    // a pill click as an expand/collapse toggle.
                    foreach (var pill in pillPanels)
                    {
                        if (pill.MouseOver) return;
                    }
                    PreserveScrollAcross(() =>
                    {
                        if (!state.ChildrenBuilt)
                        {
                            foreach (var child in state.Node.Children)
                            {
                                RenderTreeNode(
                                    child, state.ChildContainer, state.PanelWidth, state.Depth + 1, state.ChildDimmed);
                            }
                            state.ChildrenBuilt = true;
                        }
                        state.IsExpanded = !state.IsExpanded;
                        _nodeExpansion[state.Node.NodeId] = state.IsExpanded;
                        state.ChildContainer.Visible = state.IsExpanded;
                        state.ArrowLabel.Text = state.IsExpanded ? "\u25BC" : "\u25B6";
                        state.ChildContainer.Parent.Invalidate();
                    });
                };
                rowPanel.Click += toggleHandler;
            }
        }

        // --- Decision pills ---

        private enum PillKind
        {
            Selected,
            Available,
            Locked,
            Have
        }

        private struct PillSpec
        {
            public string Text;
            public AcquisitionSource? Source; // non-null => clickable
            public PillKind Kind;
        }

        /// <summary>
        /// One pill per feasible acquisition source (gw2e's multi-pill
        /// model): 2-3 pills means a real choice, exactly 1 pill means the
        /// source is locked - the pill count itself is the affordance.
        /// HAVE/CURRENCY/UNKNOWN are always single, non-interactive pills
        /// (no AcquisitionSource value represents "force use owned
        /// materials", so there is nothing to override to).
        /// </summary>
        private static List<PillSpec> BuildPillSpecs(CraftingTreeNode node)
        {
            var specs = new List<PillSpec>(3);

            if (node.Decision == CraftingDecision.Have)
            {
                specs.Add(new PillSpec { Text = "HAVE", Source = null, Kind = PillKind.Have });
                return specs;
            }
            if (node.Decision == CraftingDecision.Currency)
            {
                specs.Add(new PillSpec { Text = "CURRENCY", Source = null, Kind = PillKind.Locked });
                return specs;
            }

            var options = new List<(AcquisitionSource src, string text)>(3);
            if (node.CanCraft) options.Add((AcquisitionSource.Craft, "CRAFT"));
            if (node.CanBuyTp) options.Add((AcquisitionSource.BuyFromTp, "TP"));
            if (node.CanBuyVendor) options.Add((AcquisitionSource.BuyFromVendor, "VENDOR"));

            if (options.Count == 0)
            {
                specs.Add(new PillSpec { Text = "UNKNOWN", Source = null, Kind = PillKind.Locked });
                return specs;
            }
            if (options.Count == 1)
            {
                specs.Add(new PillSpec { Text = options[0].text, Source = null, Kind = PillKind.Locked });
                return specs;
            }

            AcquisitionSource current;
            switch (node.Decision)
            {
                case CraftingDecision.Craft: current = AcquisitionSource.Craft; break;
                case CraftingDecision.BuyFromTp: current = AcquisitionSource.BuyFromTp; break;
                case CraftingDecision.BuyFromVendor: current = AcquisitionSource.BuyFromVendor; break;
                default: current = options[0].src; break; // defensive; solver always matches one of the options
            }

            foreach (var opt in options)
            {
                bool selected = opt.src == current;
                specs.Add(new PillSpec
                {
                    Text = opt.text,
                    // The selected pill is already the active choice -
                    // clicking it would be a no-op re-solve, so it is
                    // rendered non-interactive rather than wired up.
                    Source = selected ? (AcquisitionSource?)null : opt.src,
                    Kind = selected ? PillKind.Selected : PillKind.Available
                });
            }
            return specs;
        }

        private static void GetPillColors(PillKind kind, out Color border, out Color fill)
        {
            switch (kind)
            {
                case PillKind.Selected:
                    border = new Color(45, 197, 14); // #2DC50E
                    fill = border * 0.15f;
                    break;
                case PillKind.Have:
                    border = new Color(113, 113, 255); // #7171FF
                    fill = border * 0.15f;
                    break;
                case PillKind.Available:
                    border = new Color(138, 138, 138); // #8A8A8A
                    fill = Color.Transparent;
                    break;
                case PillKind.Locked:
                default:
                    border = new Color(107, 107, 107); // #6B6B6B
                    fill = Color.Black * 0.3f;
                    break;
            }
        }

        /// <summary>
        /// Renders the pill column and returns the created pill panels so
        /// the row's expand/collapse click handler can exclude them from
        /// its own hit-test (a pill click is a decision, not a toggle).
        /// </summary>
        private List<Panel> RenderDecisionPills(
            Panel rowPanel, CraftingTreeNode node, int pillColX, int pillY, bool dimmed)
        {
            var specs = BuildPillSpecs(node);
            var font = GameService.Content.DefaultFont12;
            var pillPanels = new List<Panel>(specs.Count);
            int x = pillColX;

            foreach (var spec in specs)
            {
                int textWidth = (int)System.Math.Ceiling(font.MeasureString(spec.Text).Width);
                int pillWidth = textWidth + 12;

                GetPillColors(spec.Kind, out Color borderColor, out Color fillColor);
                Color textColor = borderColor;
                if (dimmed)
                {
                    borderColor *= 0.35f;
                    fillColor *= 0.35f;
                    textColor *= 0.35f;
                }

                // Border simulated as an outer colored panel with a 1px-inset
                // fill panel - same nesting technique as CreateRarityFramedIcon.
                var outer = new Panel()
                {
                    Size = new Point(pillWidth, 20),
                    Location = new Point(x, pillY),
                    BackgroundColor = borderColor,
                    Parent = rowPanel
                };
                var inner = new Panel()
                {
                    Size = new Point(pillWidth - 2, 18),
                    Location = new Point(1, 1),
                    BackgroundColor = fillColor,
                    Parent = outer
                };
                new Label()
                {
                    Text = spec.Text,
                    Font = font,
                    TextColor = textColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point((pillWidth - 2 - textWidth) / 2, 2),
                    Parent = inner
                };

                bool interactive = !dimmed && spec.Source.HasValue && _resolveOverridesSync != null;
                if (interactive)
                {
                    outer.BasicTooltipText = $"Switch to {spec.Text}";
                    var source = spec.Source.Value;
                    outer.Click += (_, __) =>
                    {
                        _nodeOverrides[node.NodeId] = source;
                        ApplyOverridesAndResolve();
                    };
                    Color restingBorder = borderColor;
                    outer.MouseEntered += (_, __) => outer.BackgroundColor = Color.White;
                    outer.MouseLeft += (_, __) => outer.BackgroundColor = restingBorder;
                }
                else if (spec.Kind == PillKind.Locked)
                {
                    outer.BasicTooltipText = "Only available source";
                }

                pillPanels.Add(outer);
                x += pillWidth + 6;
            }

            return pillPanels;
        }

        // Plain "12g 34s 56c" text for contexts that cannot render coin
        // icons (BasicTooltipText has no inline-image support).
        private static string FormatCoinText(long copper)
        {
            if (copper < 0) copper = 0;
            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;
            return $"{gold}g {silver}s {cop}c";
        }

        /// <summary>
        /// Truncates text to fit maxWidth, appending "..." when it doesn't
        /// fit whole. Binary-searches the longest prefix (rather than
        /// trimming one character at a time) since MeasureString is not
        /// free and item names can run long.
        /// </summary>
        private static string EllipsizeToWidth(BitmapFont font, string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            if (maxWidth <= 0) return "";

            int fullWidth = (int)System.Math.Ceiling(font.MeasureString(text).Width);
            if (fullWidth <= maxWidth) return text;

            const string ellipsis = "...";
            int ellipsisWidth = (int)System.Math.Ceiling(font.MeasureString(ellipsis).Width);
            if (ellipsisWidth >= maxWidth)
            {
                // Degenerate (extremely narrow column): still show the
                // ellipsis rather than nothing, so the row reads as
                // "truncated" instead of "blank/broken".
                return ellipsis;
            }

            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                int width = (int)System.Math.Ceiling(font.MeasureString(text.Substring(0, mid)).Width) + ellipsisWidth;
                if (width <= maxWidth) lo = mid; else hi = mid - 1;
            }
            return lo <= 0 ? ellipsis : text.Substring(0, lo) + ellipsis;
        }

        /// <summary>
        /// Standard GW2 rarity palette for icon borders. Unknown/absent
        /// rarity renders a neutral dark grey - never guess a rarity.
        /// </summary>
        private static Color GetRarityBorderColor(string rarity)
        {
            switch (rarity)
            {
                case "Junk": return new Color(170, 170, 170);
                // Deliberately NOT white: a white border reads as borderless
                // next to the tinted frames around it (this row's icon frame
                // in particular sits beside Fine/Rare/etc. frames that are
                // clearly colored). Distinct from the (60, 60, 60)
                // unknown/absent-rarity fallback below - M19 design intent.
                case "Basic": return new Color(90, 90, 90);
                case "Fine": return new Color(98, 164, 218);
                case "Masterwork": return new Color(26, 147, 6);
                case "Rare": return new Color(252, 208, 11);
                case "Exotic": return new Color(255, 164, 5);
                case "Ascended": return new Color(251, 62, 141);
                case "Legendary": return new Color(76, 19, 157);
                default: return new Color(60, 60, 60);
            }
        }

        /// <summary>
        /// GW2's in-game-bright rarity palette for item NAME text on Blish's
        /// dark background (gw2efficiency's own name-color palette is
        /// deliberately dimmed for a white page and is illegible here).
        /// Unknown/absent rarity renders a neutral light grey - never guess.
        /// </summary>
        private static Color GetRarityNameColor(string rarity)
        {
            switch (rarity)
            {
                case "Junk": return new Color(170, 170, 170);
                case "Basic": return new Color(255, 255, 255);
                case "Fine": return new Color(98, 164, 218);
                case "Masterwork": return new Color(26, 147, 6);
                case "Rare": return new Color(252, 208, 11);
                case "Exotic": return new Color(255, 164, 5);
                case "Ascended": return new Color(251, 62, 141);
                case "Legendary": return new Color(76, 19, 157);
                default: return new Color(200, 200, 200);
            }
        }

        // --- Coin display helpers ---
        //
        // gw2e's Coins component renders NumberFormat(gold) -> icon ->
        // NumberFormat(silver, zero-padded once gold precedes it) -> icon ->
        // NumberFormat(copper, zero-padded once silver precedes it) -> icon,
        // omitting leading all-zero units (a sub-1-gold amount starts at
        // silver, un-padded). Segments are measured up front so the same
        // spec list can be laid out left-anchored, right-anchored (table
        // price columns), or centered (cost tiles) without re-measuring.

        private const int CoinIconSize = 20;
        private const int CoinLabelIconGap = 2;
        private const int CoinSegmentGap = 6;

        private struct CoinSegmentSpec
        {
            public int AssetId;
            public string Text;
            public int TextWidth;
        }

        private static List<CoinSegmentSpec> BuildCoinSegments(long copper, BitmapFont font)
        {
            if (copper < 0) copper = 0;

            long gold = copper / 10000;
            long silver = (copper % 10000) / 100;
            long cop = copper % 100;

            bool showGold = gold > 0;
            bool showSilver = showGold || silver > 0;

            var segments = new List<CoinSegmentSpec>(3);
            if (showGold)
            {
                AddSegmentSpec(segments, font, 156904, gold.ToString());
            }
            if (showSilver)
            {
                AddSegmentSpec(segments, font, 156907, showGold ? silver.ToString("D2") : silver.ToString());
            }
            // Copper always renders (even "0") so a zero total is never a blank row.
            AddSegmentSpec(segments, font, 156902, showSilver ? cop.ToString("D2") : cop.ToString());
            return segments;
        }

        private static void AddSegmentSpec(List<CoinSegmentSpec> segments, BitmapFont font, int assetId, string text)
        {
            int width = (int)System.Math.Ceiling(font.MeasureString(text).Width);
            segments.Add(new CoinSegmentSpec { AssetId = assetId, Text = text, TextWidth = width });
        }

        private static int TotalCoinSegmentsWidth(List<CoinSegmentSpec> segments)
        {
            if (segments.Count == 0) return 0;
            int width = 0;
            foreach (var seg in segments)
            {
                width += seg.TextWidth + CoinLabelIconGap + CoinIconSize + CoinSegmentGap;
            }
            return width - CoinSegmentGap;
        }

        /// <summary>
        /// Lays out coin segments left-to-right starting at x. alphaScale
        /// dims the number labels (not the icons - Panel has no tint
        /// property) for dimmed not-crafted subtree rows.
        /// </summary>
        private static void LayoutCoinSegments(
            Panel parent, List<CoinSegmentSpec> segments, int startX, int y, BitmapFont font, float alphaScale = 1f)
        {
            int x = startX;
            foreach (var seg in segments)
            {
                Color textColor = GetCoinColor(seg.AssetId);
                if (alphaScale < 1f) textColor *= alphaScale;

                new Label()
                {
                    Text = seg.Text,
                    Font = font,
                    TextColor = textColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(x, y),
                    Parent = parent
                };

                new Panel()
                {
                    Size = new Point(CoinIconSize, CoinIconSize),
                    Location = new Point(x + seg.TextWidth + CoinLabelIconGap, y),
                    BackgroundTexture = AsyncTexture2D.FromAssetId(seg.AssetId),
                    Parent = parent
                };

                x += seg.TextWidth + CoinLabelIconGap + CoinIconSize + CoinSegmentGap;
            }
        }

        private static void LayoutCoinSegmentsRightAligned(
            Panel parent, List<CoinSegmentSpec> segments, int rightEdgeX, int y, BitmapFont font, float alphaScale = 1f)
        {
            int startX = rightEdgeX - TotalCoinSegmentsWidth(segments);
            LayoutCoinSegments(parent, segments, startX, y, font, alphaScale);
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

        // --- Icon helper ---

        /// <summary>
        /// Item icon inside a rarity-colored frame. Defaults to the tree/row
        /// size (32px icon, 1px border = 34px overall); the plan header uses
        /// a larger 40px/2px variant (44px overall, gw2e's .tooltip-item).
        /// </summary>
        private static void CreateRarityFramedIcon(
            Panel parent, string iconUrl, string rarity, int x, int y,
            int iconSize = 32, int borderThickness = 1)
        {
            CreateRarityFramedIcon(
                parent, iconUrl, GetRarityBorderColor(rarity), x, y, iconSize, borderThickness);
        }

        /// <summary>
        /// Same as above with an explicit frame color, for dimmed
        /// not-crafted subtree rows (neutral grey frame instead of rarity).
        /// </summary>
        private static void CreateRarityFramedIcon(
            Panel parent, string iconUrl, Color frameColor, int x, int y,
            int iconSize = 32, int borderThickness = 1)
        {
            int frameSize = iconSize + borderThickness * 2;
            var frame = new Panel()
            {
                Size = new Point(frameSize, frameSize),
                Location = new Point(x, y),
                BackgroundColor = frameColor,
                Parent = parent
            };
            CreateItemIcon(frame, iconUrl, borderThickness, borderThickness, iconSize);
        }

        private static void CreateItemIcon(Panel parent, string iconUrl, int x, int y, int size = 32)
        {
            // Missing icon: render a neutral empty-slot square, not the
            // alarming red error texture - a data gap is not a failure.
            if (string.IsNullOrEmpty(iconUrl))
            {
                new Panel()
                {
                    Size = new Point(size, size),
                    Location = new Point(x, y),
                    BackgroundColor = new Color(45, 45, 45),
                    Parent = parent
                };
                return;
            }

            new Panel()
            {
                Size = new Point(size, size),
                Location = new Point(x, y),
                BackgroundTexture = GameService.Content.GetRenderServiceTexture(iconUrl),
                Parent = parent
            };
        }
    }
}
