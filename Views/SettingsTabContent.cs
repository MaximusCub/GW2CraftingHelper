using System;
using System.Collections.Generic;
using System.Globalization;
using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Views.Rendering;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// Settings tab content: lets the user set the coin value of
    /// non-coin currencies (see Models/CurrencyValuation.cs) used to
    /// compare vendor offers, persisted through ModuleSettings. Plan-level
    /// defaults (price basis, own materials) remain on the Crafting Plan
    /// tab - only informational text about them is shown here.
    /// </summary>
    public class SettingsTabContent
    {
        // Curated list of common plan currencies: Karma, Laurels, Spirit
        // Shards, the Rift Essence tiers, and Astral Acclaim. The coin
        // currency itself (Gw2Constants.CoinCurrencyId) is never listed here
        // - it is already directly comparable and CurrencyValuation rejects
        // coin-keyed entries outright.
        //
        // Astral Acclaim (63) - addendum-astral-acclaim.md P1: added so a
        // user CAN value it if they choose to, but with no suggested rate
        // (see the info line in BuildCurrencyValuationsSection below) - a
        // single implied copper-per-AA
        // anchor was rejected, since AA's per-item
        // deal quality varies across the Wizard's Vault and any one implied
        // rate would misrepresent that. Leaving this row blank (the
        // default) keeps AA out of price comparisons entirely, same as any
        // other unset currency.
        // Ids with no
        // CurrencyDecisionDefaults entry but still worth surfacing for a
        // user to value by hand - see CurrencyDecisionDefaults' own doc
        // comment for why each is absent from that table: Astral Acclaim's
        // per-item deal quality varies too much for any single suggested
        // rate, and the three Rift Essence tiers have no row at all in
        // gw2efficiency's own source table.
        private static readonly int[] CuratedCurrencyIdsWithoutDefault =
        {
            63, // Astral Acclaim
            78, // Fine Rift Essence
            79, // Rare Rift Essence
            80  // Masterwork Rift Essence
        };

        // ModuleSettings.GetEffectiveCurrencyValuation applies EVERY entry
        // in that table to every real solve regardless of whether a
        // Settings row exists for it, so a defaulted currency with no row
        // here was invisible and unclearable - no way to inspect it, know
        // it was silently tipping a vendor-vs-TP comparison, or turn it
        // off (Feature 1's own three-state requirement: set / default /
        // cleared, each visible and each reachable). CurrencyDecisionDefaults
        // is now the single source of truth for which defaulted ids get a
        // row - adding a new default there automatically gets a Settings
        // row here too, with no second list to remember to keep in sync.
        private static readonly int[] CuratedCurrencyIds = BuildCuratedCurrencyIds();

        private static int[] BuildCuratedCurrencyIds()
        {
            var ids = new SortedSet<int>(CurrencyDecisionDefaults.DefaultCopperPerUnit.Keys);
            foreach (int id in CuratedCurrencyIdsWithoutDefault)
            {
                ids.Add(id);
            }
            var result = new int[ids.Count];
            ids.CopyTo(result);
            return result;
        }

        private static readonly Color InfoTextColor = new Color(170, 170, 170);
        private static readonly Color SectionDividerColor = new Color(130, 130, 130);
        private static readonly Color ErrorTextColor = new Color(255, 100, 100);
        private static readonly Color SuccessTextColor = new Color(150, 200, 150);
        private static readonly Color WarningTextColor = new Color(255, 200, 60);

        private const int RightEdgePadding = 20;
        private const int SaveBarHeight = 40;
        private const int RowHeight = 30;
        // 22, not 20: an info line sits at y=2 and its lowest Font16 ink is
        // y=23, so 22 leaves the same 1px overhang 20 left Font14's y=21.
        private const int InfoRowHeight = 22;
        private const int NameColumnX = 16;
        private const int NameColumnWidth = 220;
        private const int InputWidth = 80;
        private const int HintX = NameColumnX + NameColumnWidth + InputWidth + 8;
        private const int ErrorX = HintX + 130;

        private static readonly Logger Logger = Logger.GetLogger<SettingsTabContent>();

        private class CurrencyRow
        {
            public int CurrencyId;
            public bool HasDefault;
            public long DefaultCopperPerUnit;

            // The row's own one-line cell inside the currency grid, and the
            // rule along its bottom - both driven by ApplyCurrencyFilter.
            public Panel Cell;
            public Panel Divider;

            public TextBox Input;

            // The cell's single tag slot, shared by two labels at the same
            // spot: DefaultLabel carries the persisted default/cleared state
            // (Feature 1 requires it VISIBLE, not hover-only), ErrorLabel
            // replaces it while the typed amount is unparseable. Only one is
            // ever shown - see SetCurrencyRowError.
            public Label DefaultLabel;
            public Label ErrorLabel;

            // Null for a currency with no CurrencyDecisionDefaults entry
            // (nothing to clear - see AddCurrencyRow). Holds the user's
            // in-memory intent for the NEXT Save (see SaveValuations); the
            // persisted state is shown by DefaultLabel.
            public Checkbox ClearCheckbox;
        }

        // One row per Homestead Refinement material
        // family. MaterialItemId is internal-only bookkeeping (never
        // displayed - see MaterialLabel) used solely to route the parsed
        // tier back to the right ModuleSettings entry.
        private class HomesteadTierRow
        {
            public int MaterialItemId;
            public string MaterialLabel;
            public TextBox Input;
            public Label ErrorLabel;
        }

        private readonly ModuleSettings _settings;
        private readonly List<CurrencyRow> _rows = new List<CurrencyRow>();

        // Row names in _rows order, held so the filter keystroke path does
        // not rebuild a 47-entry list per character typed.
        private readonly List<string> _currencyNames = new List<string>();
        private readonly List<HomesteadTierRow> _homesteadRows = new List<HomesteadTierRow>();

        private FlowPanel _rootPanel;

        // One status label for the whole tab, next to the one Save button in
        // the header bar (see BuildSaveBar) - the four per-section Save rows
        // and their four status labels this replaced are recorded in
        // KNOWN-ISSUES (audit batch G supersedes B14).
        private Label _statusLabel;

        // The currency list's absolutely-positioned grid: one Panel holding
        // every cell, repacked by ApplyCurrencyFilter as rows are hidden and
        // held at its unfiltered height throughout (SetCurrencyGridHeight).
        private Panel _currencyGridPanel;
        private TextBox _currencyFilterInput;
        private Label _currencyCountLabel;

        // Column header over that grid: one "Currency"/"Copper per unit"
        // pair per grid column, repositioned with the columns themselves.
        // The unit belongs here rather than inside each 70px box - as a
        // placeholder it read as a label naming the box, not as a prompt to
        // type a number into it (field test, bug 2).
        private Panel _currencyHeaderPanel;
        private readonly Label[] _currencyHeaderNames = new Label[2];
        private readonly Label[] _currencyHeaderUnits = new Label[2];

        // Reused per filter pass (one entry per row, in _rows order) rather
        // than reallocated per keystroke: the rows whose amount did not
        // parse, which stay on screen through any filter.
        private bool[] _currencyForceVisible = new bool[0];

        // Every direct child of _rootPanel that spans the panel width, plus
        // the section-header rules inside them - re-widened together when
        // the window is resized (see ApplyPanelWidth). Without this the
        // controls keep the width the tab was first opened at, and a
        // narrowed window pushes the grid's right-hand column off-panel.
        private readonly List<Panel> _fullWidthPanels = new List<Panel>();

        // Width the content is currently laid out at (see ApplyPanelWidth).
        private int _panelWidth;

        // The ONE "Diagnostics" checkbox + the two log-file
        // policy rows (max size / retention) - d2-log-system.md Section 5.
        // No separate
        // ScrollDiagnosticsEnabled checkbox is surfaced here - see
        // ModuleSettings' own doc comment on that setting's backward-compat
        // read.
        private Checkbox _logDiagnosticsCheckbox;
        private TextBox _logMaxSizeInput;
        private Label _logMaxSizeErrorLabel;
        private TextBox _logRetentionDaysInput;
        private Label _logRetentionDaysErrorLabel;

        // Standalone
        // "Snapshot" section, its own new section (not folded into "Plan
        // Defaults", which is about per-plan choices - a different
        // concern). TextBox+Save+error-label idiom, same shape as the
        // Homestead tier rows above.
        private TextBox _snapshotRefreshIntervalInput;
        private Label _snapshotRefreshIntervalErrorLabel;

        // The control values as of the last load or successful save - what
        // an edit is measured against (see UnsavedChangeCount). Null until
        // the tab has been built once, which SettingsFormState reads as
        // "nothing to compare", not as "everything changed".
        private SettingsFormState _baseline;

        // False while Build is midway through replacing the row lists.
        // Blish runs Build off the UI thread (WindowBase2.ShowView does
        // view.DoLoad(...).ContinueWith(BuildView) with no scheduler),
        // while UnsavedChangeCount is called from the main thread's tab
        // handler - so without this, a tab switch landing during a build
        // would enumerate _rows while AddCurrencyRow appends to it and
        // throw "Collection was modified" out of Blish's input dispatch.
        // Volatile so the reader that sees true also sees the finished
        // lists. Same philosophy as the null-baseline early-out above:
        // a half-built form has nothing to compare, not everything.
        private volatile bool _buildComplete;

        public SettingsTabContent(ModuleSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Announces, on the caller's thread, that a rebuild has been
        /// committed to. <see cref="Build"/> clears the same flag, but Blish
        /// only queues Build (ShowView does
        /// <c>view.DoLoad(...).ContinueWith(BuildView)</c>) after the main
        /// thread has already switched tabs - so between the switch and
        /// Build's first statement the flag would still read true from the
        /// PREVIOUS build, and a dirty check in that gap would enumerate the
        /// row lists the queued Build is about to clear. Called from the
        /// Settings tab's view factory, which TabbedWindow2.OnTabChanged
        /// evaluates on the main thread before either of those.
        /// </summary>
        public void BeginRebuild()
        {
            _buildComplete = false;
        }

        public void Build(Container container)
        {
            _buildComplete = false;

            _rows.Clear();
            _currencyNames.Clear();
            _currencyForceVisible = new bool[0];
            _fullWidthPanels.Clear();

            // Same hazard as the stale _homesteadRows below: these point at
            // the previous Build cycle's already-disposed controls until the
            // currency section is rebuilt further down.
            _currencyGridPanel = null;
            _currencyFilterInput = null;
            _currencyCountLabel = null;
            _currencyHeaderPanel = null;
            _statusLabel = null;

            // Dropped before the controls it describes are replaced: a
            // baseline left over from the previous Build cycle would be
            // compared against a freshly loaded form and report the
            // difference between two sessions as unsaved edits. LoadAll
            // below takes the new one.
            _baseline = null;

            // Module.cs's Settings tab reuses this
            // SAME SettingsTabContent instance across every tab re-open
            // (unlike the Log tab's "new instance per open" factory), so
            // without clearing here, re-opening Settings more than once
            // per session would accumulate stale HomesteadTierRow entries
            // pointing at controls the previous Build() cycle's container
            // already disposed - LoadCurrentHomesteadTiers/
            // SaveHomesteadTiers would then read/write through them
            // alongside the current cycle's real rows.
            _homesteadRows.Clear();

            int panelWidth = container.ContentRegion.Width - RightEdgePadding;
            _panelWidth = panelWidth;

            var saveBar = BuildSaveBar(container);

            _rootPanel = new FlowPanel()
            {
                Size = ContentSizeBelowSaveBar(container),
                Location = new Point(0, SaveBarHeight),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = container
            };

            container.Resized += (_, __) =>
            {
                saveBar.Size = new Point(container.ContentRegion.Width, SaveBarHeight);
                _rootPanel.Size = ContentSizeBelowSaveBar(container);
                ApplyPanelWidth(container.ContentRegion.Width - RightEdgePadding);
            };

            BuildHomesteadRefinementSection(panelWidth);
            BuildLoggingSection(panelWidth);
            BuildSnapshotSection(panelWidth);
            BuildCurrencyValuationsSection(panelWidth);

            LoadAll();
        }

        /// <summary>
        /// Loads every section from persisted settings and takes the
        /// baseline the dirty check compares against. Shared by Build and
        /// DiscardChanges so a discard restores exactly the state a fresh
        /// build would show.
        /// </summary>
        private void LoadAll()
        {
            LoadCurrentValuations();
            LoadCurrentHomesteadTiers();
            LoadCurrentLoggingSettings();
            LoadCurrentSnapshotSettings();

            // LoadCurrentValuations clears the per-row error tags, so the
            // rows a failed Save forced past the filter have to be
            // re-evaluated - otherwise a discard leaves them pinned.
            ApplyCurrencyFilter();

            _baseline = CaptureFormState();

            // Last line, deliberately: it publishes everything above it.
            _buildComplete = true;
        }

        /// <summary>
        /// Every save-gated control value on the tab, as the Blish-free
        /// SettingsFormState. The Diagnostics checkbox is absent by
        /// design - see that type's own doc comment.
        /// </summary>
        private SettingsFormState CaptureFormState()
        {
            var state = new SettingsFormState();

            foreach (var row in _rows)
            {
                state.AddText(
                    SettingsFormState.CurrencyAmountKey(row.CurrencyId),
                    row.Input?.Text);
                state.AddFlag(
                    SettingsFormState.CurrencyIgnoreKey(row.CurrencyId),
                    row.ClearCheckbox != null && row.ClearCheckbox.Checked);
            }

            foreach (var row in _homesteadRows)
            {
                state.AddText(
                    SettingsFormState.HomesteadTierKey(row.MaterialItemId),
                    row.Input?.Text);
            }

            // Captured through null-conditionals rather than skipped when
            // the control is missing: the key set has to be identical
            // between baseline and capture, or an absent control would
            // itself read as a change.
            state.AddText(SettingsFormState.LogMaxSizeMbKey, _logMaxSizeInput?.Text);
            state.AddText(SettingsFormState.LogRetentionDaysKey, _logRetentionDaysInput?.Text);
            state.AddText(
                SettingsFormState.SnapshotRefreshIntervalMinutesKey,
                _snapshotRefreshIntervalInput?.Text);

            return state;
        }

        /// <summary>
        /// How many fields differ from the last load or successful save.
        /// Returns the count rather than the changed keys because those
        /// keys carry currency and item ids, which are internal-only and
        /// must never reach a caller that might display them.
        ///
        /// <para>
        /// Zero until the tab has finished building once - see
        /// _buildComplete for the cross-thread reason.
        /// </para>
        /// </summary>
        public int UnsavedChangeCount()
        {
            if (!_buildComplete) return 0;

            return CaptureFormState().ChangedKeys(_baseline).Count;
        }

        /// <summary>
        /// Restores the last loaded/saved values into the controls and
        /// clears the save bar's status line, which would otherwise still
        /// report the outcome of a save the user has just walked back.
        /// </summary>
        public void DiscardChanges()
        {
            LoadAll();

            if (_statusLabel != null)
            {
                _statusLabel.Text = "";
            }
        }

        private static Point ContentSizeBelowSaveBar(Container container)
        {
            int height = container.ContentRegion.Height - SaveBarHeight;
            return new Point(container.ContentRegion.Width, height > 0 ? height : 0);
        }

        /// <summary>
        /// Re-lays the whole scrolling content out at a new panel width.
        /// Every row/header panel is built at the width the tab happened to
        /// open at, and the currency grid additionally derives its column
        /// count, column width and cell X positions from it - so without
        /// this, narrowing the window leaves the second column of cells
        /// beyond the panel's right edge, unreachable until the tab is
        /// closed and re-opened.
        /// </summary>
        private void ApplyPanelWidth(int panelWidth)
        {
            // Resized fires on height-only changes too (and repeatedly while
            // the window is dragged), and re-widening every row re-flows the
            // scrolling FlowPanel once per row - so do nothing unless the
            // width actually moved.
            if (panelWidth <= 0 || panelWidth == _panelWidth) return;

            _panelWidth = panelWidth;

            foreach (var panel in _fullWidthPanels)
            {
                panel.Width = panelWidth;
            }

            if (_currencyGridPanel == null) return;

            _currencyGridPanel.Width = panelWidth;
            LayoutCurrencyGridHeader();

            int columnWidth = SettingsCurrencyGridLayout.ComputeColumnWidth(panelWidth);
            foreach (var row in _rows)
            {
                row.Cell.Width = columnWidth;
                row.Divider.Width = columnWidth;
            }

            SetCurrencyGridHeight();
            ApplyCurrencyFilter();
        }

        /// <summary>
        /// Holds the grid panel at its UNFILTERED height. Blish's Scrollbar
        /// resets the scroll position to the top whenever the scrolling
        /// container's content height changes (RecalculateLayout compares
        /// the previous scrollbar percent against the freshly recomputed one
        /// and zeroes ScrollDistance/TargetScrollDistance when they differ -
        /// decompiled from the shipped 1.3.0 binary), and it does so a frame
        /// later, so a resize cannot simply be undone in place. Sizing the
        /// grid to the filtered list would therefore snap the tab to the top
        /// on every filter keystroke that changes the match count; the cost
        /// of the fixed height is trailing blank space below a filtered
        /// list, which is why the grid is the last thing in the panel.
        /// </summary>
        private void SetCurrencyGridHeight()
        {
            if (_currencyGridPanel == null) return;

            _currencyGridPanel.Height = SettingsCurrencyGridLayout.ComputeHeight(
                _rows.Count, _panelWidth, CurrencyRowHeight);
        }

        private void BuildCurrencyValuationsSection(int panelWidth)
        {
            AddSectionHeader("Currency Valuations", panelWidth);
            AddInfoLine("Coin value per unit of each currency, used to compare vendor offers.", panelWidth);
            // The one sentence that names the interaction. Field test, bug
            // 2: with the unit inside the box and only a grey "default N"
            // beside it, the row read as three read-only labels - nothing
            // said an amount could be typed over the default at all.
            AddInfoLine("Type a whole number of copper in a currency's box and press Save to override its default.", panelWidth);
            AddInfoLine("Leave a currency unset to keep it out of price comparisons.", panelWidth);
            // A currency with a curated
            // default (see CurrencyDecisionDefaults) is used automatically
            // even when its box is left blank - "unset" now means "use the
            // default, if any", not "excluded". Tick Ignore to suppress a
            // default entirely.
            AddInfoLine("Some currencies show a default estimate and are valued automatically unless ignored.", panelWidth);
            // What the "Plan Defaults" section header used to introduce: it
            // owned three info lines and no controls at all, so it is a note
            // under the pricing section it points at, not a section.
            AddInfoLine("Price basis and both \"own materials\" choices are set per plan in the Crafting Plan tab.", panelWidth);
            // addendum-astral-acclaim.md P1: neutral, no-single-anchor hint
            // for Astral Acclaim specifically - it is untradable and earned
            // via capped seasonal play, so unlike the other currencies
            // below, there is no rate this settings row can honestly
            // suggest. Left blank (the default) simply keeps it out of
            // price comparisons, same as any other unset currency. Above the
            // grid rather than below it because the grid is deliberately the
            // last thing in the panel - see SetCurrencyGridHeight.
            AddInfoLine("Astral Acclaim is untradable and earned via capped play - its value is personal, so no rate is suggested here.", panelWidth);

            AddCurrencyFilterRow(panelWidth);
            AddCurrencyGridHeader(panelWidth);

            _currencyGridPanel = new Panel()
            {
                Size = new Point(panelWidth, 0),
                Parent = _rootPanel
            };

            int columnWidth = SettingsCurrencyGridLayout.ComputeColumnWidth(panelWidth);
            foreach (int currencyId in CuratedCurrencyIds)
            {
                AddCurrencyRow(currencyId, columnWidth);
            }

            _currencyForceVisible = new bool[_rows.Count];
            SetCurrencyGridHeight();
            ApplyCurrencyFilter();
        }

        /// <summary>
        /// Three per-material efficiency
        /// tier rows (Fiber/Metal/Wood), each an integer 0/1/2 entered as
        /// text and validated on Save - same TextBox+Save shape as the
        /// Currency Valuations section above (a plain Checkbox's immediate-
        /// apply pattern doesn't fit a 3-valued integer, and no Dropdown/
        /// stepper control is otherwise used in this codebase's Views).
        /// Labels name the material family only - no raw item/vendor ids
        /// are ever displayed (repo invariant).
        /// </summary>
        private void BuildHomesteadRefinementSection(int panelWidth)
        {
            AddSectionHeader("Homestead Refinement", panelWidth);
            AddInfoLine("Efficiency upgrades owned per material (0 = none, 1 = one upgrade, 2 = both).", panelWidth);
            AddInfoLine("Raises how much Refined Homestead material each trade produces.", panelWidth);

            AddHomesteadTierRow(Gw2Constants.RefinedHomesteadFiberItemId, "Fiber (Farm)", panelWidth);
            AddHomesteadTierRow(Gw2Constants.RefinedHomesteadMetalItemId, "Metal (Metal Forge)", panelWidth);
            AddHomesteadTierRow(Gw2Constants.RefinedHomesteadWoodItemId, "Wood (Lumber Mill)", panelWidth);
        }

        private void AddHomesteadTierRow(int materialItemId, string materialLabel, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };
            _fullWidthPanels.Add(rowPanel);

            new Label()
            {
                Font = UiFonts.Body,
                Text = materialLabel,
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = NameColumnWidth,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
            };

            var input = new TextBox()
            {
                Size = new Point(InputWidth, 26),
                Location = new Point(NameColumnX + NameColumnWidth, 3),
                Parent = rowPanel
            };

            new Label()
            {
                Font = UiFonts.Body,
                Text = "tier (0-2)",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(HintX, 7),
                Parent = rowPanel
            };

            var errorLabel = new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = ErrorTextColor,
                Location = new Point(ErrorX, 7),
                Parent = rowPanel
            };

            _homesteadRows.Add(new HomesteadTierRow
            {
                MaterialItemId = materialItemId,
                MaterialLabel = materialLabel,
                Input = input,
                ErrorLabel = errorLabel
            });
        }

        private void LoadCurrentHomesteadTiers()
        {
            var tiers = _settings.GetHomesteadEfficiencyTiers();

            foreach (var row in _homesteadRows)
            {
                row.Input.Text = tiers.GetTier(row.MaterialItemId).ToString(CultureInfo.InvariantCulture);
                row.ErrorLabel.Text = "";
            }
        }

        private int SaveHomesteadTiers()
        {
            int invalidCount = 0;
            var parsedTiers = new Dictionary<int, int>();

            foreach (var row in _homesteadRows)
            {
                row.ErrorLabel.Text = "";

                if (SettingsInputParser.TryParseTier(row.Input.Text, out int tier))
                {
                    parsedTiers[row.MaterialItemId] = tier;
                }
                else
                {
                    // Left out of this save entirely - whatever was
                    // previously persisted for this material is preserved,
                    // matching the currency valuation Save button's
                    // "invalid rows are not saved" contract.
                    row.ErrorLabel.Text = "Must be 0, 1, or 2";
                    invalidCount++;
                }
            }

            if (parsedTiers.TryGetValue(Gw2Constants.RefinedHomesteadFiberItemId, out int fiberTier))
            {
                _settings.HomesteadFiberTier.Value = fiberTier;
            }
            if (parsedTiers.TryGetValue(Gw2Constants.RefinedHomesteadMetalItemId, out int metalTier))
            {
                _settings.HomesteadMetalTier.Value = metalTier;
            }
            if (parsedTiers.TryGetValue(Gw2Constants.RefinedHomesteadWoodItemId, out int woodTier))
            {
                _settings.HomesteadWoodTier.Value = woodTier;
            }

            return invalidCount;
        }

        /// <summary>
        /// One "Diagnostics" checkbox (idiom (a),
        /// immediate-apply - matches ValueOwnMaterials above) plus two
        /// TextBox+Save rows (idiom (b) - matches the Homestead section
        /// above) for the log file's size cap and retention window. This is
        /// the ONE diagnostics toggle for the whole module per the
        /// tab-roadmap-proposal synthesis (Section 2.1) - no separate
        /// ScrollDiagnosticsEnabled checkbox is added alongside it.
        /// </summary>
        private void BuildLoggingSection(int panelWidth)
        {
            AddSectionHeader("Logging", panelWidth);
            AddInfoLine("Controls the module's own log file (data/module_log.jsonl), separate from Blish HUD's own log.", panelWidth);
            AddInfoLine("The Log tab always shows the current session regardless of these settings.", panelWidth);

            AddLogDiagnosticsRow(panelWidth);
            AddLogMaxSizeRow(panelWidth);
            AddLogRetentionDaysRow(panelWidth);
        }

        private void AddLogDiagnosticsRow(int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };
            _fullWidthPanels.Add(rowPanel);

            _logDiagnosticsCheckbox = new Checkbox()
            {
                Text = "Diagnostics logging",
                Checked = _settings.LogDiagnosticsEnabled.Value,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
            };
            _logDiagnosticsCheckbox.CheckedChanged += (_, e) =>
            {
                _settings.LogDiagnosticsEnabled.Value = e.Checked;
                ModuleLog.Shared.DiagnosticsEnabled = e.Checked;
            };

            new Label()
            {
                Font = UiFonts.Body,
                Text = "Log fine-grained diagnostic events (including scroll machinery) to the Log tab and file",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(NameColumnX + 170, 7),
                Parent = rowPanel
            };
        }

        private void AddLogMaxSizeRow(int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };
            _fullWidthPanels.Add(rowPanel);

            new Label()
            {
                Font = UiFonts.Body,
                Text = "Log max size",
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = NameColumnWidth,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
            };

            _logMaxSizeInput = new TextBox()
            {
                Size = new Point(InputWidth, 26),
                Location = new Point(NameColumnX + NameColumnWidth, 3),
                Parent = rowPanel
            };

            new Label()
            {
                Font = UiFonts.Body,
                Text = "MB (1-1000)",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(HintX, 7),
                Parent = rowPanel
            };

            _logMaxSizeErrorLabel = new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = ErrorTextColor,
                Location = new Point(ErrorX, 7),
                Parent = rowPanel
            };
        }

        private void AddLogRetentionDaysRow(int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };
            _fullWidthPanels.Add(rowPanel);

            new Label()
            {
                Font = UiFonts.Body,
                Text = "Log retention",
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = NameColumnWidth,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
            };

            _logRetentionDaysInput = new TextBox()
            {
                Size = new Point(InputWidth, 26),
                Location = new Point(NameColumnX + NameColumnWidth, 3),
                Parent = rowPanel
            };

            new Label()
            {
                Font = UiFonts.Body,
                Text = "days (1-365)",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(HintX, 7),
                Parent = rowPanel
            };

            _logRetentionDaysErrorLabel = new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = ErrorTextColor,
                Location = new Point(ErrorX, 7),
                Parent = rowPanel
            };
        }

        private void LoadCurrentLoggingSettings()
        {
            if (_logMaxSizeInput != null)
            {
                long mb = _settings.LogMaxSizeBytes.Value / (1024 * 1024);
                _logMaxSizeInput.Text = mb.ToString(CultureInfo.InvariantCulture);
            }
            if (_logMaxSizeErrorLabel != null)
            {
                _logMaxSizeErrorLabel.Text = "";
            }

            if (_logRetentionDaysInput != null)
            {
                _logRetentionDaysInput.Text = _settings.LogRetentionDays.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (_logRetentionDaysErrorLabel != null)
            {
                _logRetentionDaysErrorLabel.Text = "";
            }
        }

        private int SaveLoggingSettings()
        {
            int invalidCount = 0;

            if (_logMaxSizeErrorLabel != null)
            {
                _logMaxSizeErrorLabel.Text = "";
            }
            if (SettingsInputParser.TryParseLogMaxSizeMb(_logMaxSizeInput?.Text, out long maxSizeBytes))
            {
                _settings.LogMaxSizeBytes.Value = (int)maxSizeBytes;

                // Pushed live immediately (not just persisted) - the
                // running ModuleLog instance otherwise would not pick up a
                // smaller/larger cap until the next module reload. Mirrors
                // DiagnosticsEnabled's own immediate-apply behavior above,
                // even though this row uses the TextBox+Save idiom rather
                // than a plain checkbox. Routed through the same clamp as
                // Module.cs's own Configure call (redundant here in
                // practice, since TryParseLogMaxSizeMb already rejected
                // anything outside 1-1000 MB above, but keeps every live
                // consumer of this setting going through one clamped
                // accessor rather than two separately-trusted paths).
                ModuleLog.Shared.MaxFileSizeBytes = _settings.GetClampedLogMaxSizeBytes();
            }
            else if (_logMaxSizeErrorLabel != null)
            {
                _logMaxSizeErrorLabel.Text = "Must be 1-1000";
                invalidCount++;
            }

            if (_logRetentionDaysErrorLabel != null)
            {
                _logRetentionDaysErrorLabel.Text = "";
            }
            if (SettingsInputParser.TryParseRetentionDays(_logRetentionDaysInput?.Text, out int retentionDays))
            {
                // Retention is only enforced once per session at
                // Module.Initialize (age-based pruning does not need
                // per-write cost - d2-log-system.md Section 4.2), so a
                // saved value here intentionally takes effect next session,
                // not immediately - no live push needed, unlike the size
                // cap above.
                _settings.LogRetentionDays.Value = retentionDays;
            }
            else if (_logRetentionDaysErrorLabel != null)
            {
                _logRetentionDaysErrorLabel.Text = "Must be 1-365";
                invalidCount++;
            }

            return invalidCount;
        }

        /// <summary>
        /// One TextBox+Save
        /// row for SnapshotRefreshIntervalMinutes - replaces Module.cs's
        /// previously-hardcoded StaleThreshold constant, so the Snapshot
        /// tab's own staleness indicator and Module's auto-refresh trigger
        /// read the same number and can never silently disagree about what
        /// "stale" means.
        /// </summary>
        private void BuildSnapshotSection(int panelWidth)
        {
            AddSectionHeader("Snapshot", panelWidth);
            AddInfoLine("How long a cached account snapshot may sit before an automatic background refresh runs.", panelWidth);

            AddSnapshotRefreshIntervalRow(panelWidth);
        }

        private void AddSnapshotRefreshIntervalRow(int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };
            _fullWidthPanels.Add(rowPanel);

            new Label()
            {
                Font = UiFonts.Body,
                Text = "Refresh interval",
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = NameColumnWidth,
                Location = new Point(NameColumnX, 7),
                Parent = rowPanel
            };

            _snapshotRefreshIntervalInput = new TextBox()
            {
                Size = new Point(InputWidth, 26),
                Location = new Point(NameColumnX + NameColumnWidth, 3),
                Parent = rowPanel
            };

            new Label()
            {
                Font = UiFonts.Body,
                Text = "minutes (1-120)",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(HintX, 7),
                Parent = rowPanel
            };

            _snapshotRefreshIntervalErrorLabel = new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = ErrorTextColor,
                Location = new Point(ErrorX, 7),
                Parent = rowPanel
            };
        }

        private void LoadCurrentSnapshotSettings()
        {
            if (_snapshotRefreshIntervalInput != null)
            {
                _snapshotRefreshIntervalInput.Text = _settings.SnapshotRefreshIntervalMinutes.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (_snapshotRefreshIntervalErrorLabel != null)
            {
                _snapshotRefreshIntervalErrorLabel.Text = "";
            }
        }

        private int SaveSnapshotSettings()
        {
            int invalidCount = 0;

            if (_snapshotRefreshIntervalErrorLabel != null)
            {
                _snapshotRefreshIntervalErrorLabel.Text = "";
            }
            if (SettingsInputParser.TryParseRefreshIntervalMinutes(_snapshotRefreshIntervalInput?.Text, out int minutes))
            {
                _settings.SnapshotRefreshIntervalMinutes.Value = minutes;
            }
            else if (_snapshotRefreshIntervalErrorLabel != null)
            {
                _snapshotRefreshIntervalErrorLabel.Text = "Must be 1-120";
                invalidCount++;
            }

            return invalidCount;
        }

        private void AddSectionHeader(string title, int panelWidth)
        {
            var headerPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };
            _fullWidthPanels.Add(headerPanel);

            new Label()
            {
                Text = title,
                Font = UiFonts.Title,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(NameColumnX, 4),
                Parent = headerPanel
            };

            // Same header rule as every CraftingPlanView section: 2px in
            // SectionDividerColor, bottom-anchored with 1px clearance
            // inside a 30px header (see LabelHelpers.CreateRowDivider for
            // why 1px lines and flush anchoring are unsafe here).
            _fullWidthPanels.Add(new Panel()
            {
                Size = new Point(panelWidth, 2),
                Location = new Point(0, RowHeight - 3),
                BackgroundColor = SectionDividerColor,
                Parent = headerPanel
            });
        }

        private void AddInfoLine(string text, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, InfoRowHeight),
                Parent = _rootPanel
            };
            _fullWidthPanels.Add(rowPanel);

            new Label()
            {
                Font = UiFonts.Body,
                Text = text,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(NameColumnX, 2),
                Parent = rowPanel
            };
        }

        // One line per currency: name, input, Clear, and one tag slot that
        // shows either the default/cleared state or an "Invalid" warning.
        // The horizontal constants live in SettingsCurrencyGridLayout so its
        // MinColumnWidth (the one/two-column threshold) is derived from the
        // same numbers, not hand-copied from them; these are compile-time
        // aliases, not a second copy.
        // 32, not 30: the cell's labels sit at y=6, whose lowest Font16 ink
        // is y=27 - exactly the top of the 30px row's own divider
        // (30 - 2 - CellDividerClearance).
        private const int CurrencyRowHeight = 32;
        private const int CellNameX = SettingsCurrencyGridLayout.CellNameX;
        private const int CellNameWidth = SettingsCurrencyGridLayout.CellNameWidth;
        private const int CellInputX = SettingsCurrencyGridLayout.CellInputX;
        private const int CellInputWidth = SettingsCurrencyGridLayout.CellInputWidth;
        private const int CellClearX = SettingsCurrencyGridLayout.CellClearX;
        private const int CellTagX = SettingsCurrencyGridLayout.CellTagX;
        private const int CellTextY = 6;
        // 1, not 2: the input then ends at y=27, clear of the row rule
        // LabelHelpers.CreateRowDivider puts at
        // CurrencyRowHeight - 2 - CellDividerClearance.
        private const int CellInputY = 1;
        private const int CellDividerClearance = 1;
        private const int CurrencyFilterWidth = 200;

        private void AddCurrencyFilterRow(int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, RowHeight),
                Parent = _rootPanel
            };
            _fullWidthPanels.Add(rowPanel);

            _currencyFilterInput = new TextBox()
            {
                Size = new Point(CurrencyFilterWidth, 26),
                Location = new Point(CellNameX, CellInputY),
                // "Search {scope}..." - the one placeholder shape the
                // module's other three search boxes use (audit batch J,
                // M12). This box was the lone "Filter ..." spelling.
                PlaceholderText = "Search currencies...",
                Parent = rowPanel
            };
            _currencyFilterInput.TextChanged += (_, __) => ApplyCurrencyFilter();

            _currencyCountLabel = new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(CellNameX + CurrencyFilterWidth + 12, CellTextY),
                Parent = rowPanel
            };
        }

        // 26, not 24: the header labels sit at CurrencyHeaderTextY and
        // their lowest Font16 ink is y=25.
        private const int CurrencyHeaderRowHeight = 26;
        private const int CurrencyHeaderTextY = 4;

        /// <summary>
        /// One "Currency"/"Copper per unit" pair per grid column, sitting on
        /// the same X's as the cells below it. Both pairs are built once and
        /// the second is simply hidden in the one-column layout - the grid's
        /// column count only ever changes with the panel width, and building
        /// two labels costs less than rebuilding them per resize tick.
        /// </summary>
        private void AddCurrencyGridHeader(int panelWidth)
        {
            _currencyHeaderPanel = new Panel()
            {
                Size = new Point(panelWidth, CurrencyHeaderRowHeight),
                Parent = _rootPanel
            };
            _fullWidthPanels.Add(_currencyHeaderPanel);

            for (int i = 0; i < _currencyHeaderNames.Length; i++)
            {
                _currencyHeaderNames[i] = new Label()
                {
                    Font = UiFonts.Body,
                    Text = "Currency",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(CellNameX, CurrencyHeaderTextY),
                    Parent = _currencyHeaderPanel
                };
                _currencyHeaderUnits[i] = new Label()
                {
                    Font = UiFonts.Body,
                    Text = "Copper per unit",
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(CellInputX, CurrencyHeaderTextY),
                    Parent = _currencyHeaderPanel
                };
            }

            LayoutCurrencyGridHeader();
        }

        private void LayoutCurrencyGridHeader()
        {
            if (_currencyHeaderPanel == null) return;

            int columnCount = SettingsCurrencyGridLayout.ComputeColumnCount(_panelWidth);
            int columnWidth = SettingsCurrencyGridLayout.ComputeColumnWidth(_panelWidth);

            for (int i = 0; i < _currencyHeaderNames.Length; i++)
            {
                bool visible = i < columnCount;
                _currencyHeaderNames[i].Visible = visible;
                _currencyHeaderUnits[i].Visible = visible;
                if (!visible) continue;

                _currencyHeaderNames[i].Location =
                    new Point((i * columnWidth) + CellNameX, CurrencyHeaderTextY);
                _currencyHeaderUnits[i].Location =
                    new Point((i * columnWidth) + CellInputX, CurrencyHeaderTextY);
            }
        }

        private void AddCurrencyRow(int currencyId, int columnWidth)
        {
            string name = Gw2Constants.ResolveCurrencyName(currencyId);

            var cellPanel = new Panel()
            {
                Size = new Point(columnWidth, CurrencyRowHeight),
                Parent = _currencyGridPanel
            };

            string shortName = LabelHelpers.EllipsizeToWidth(
                UiFonts.Body, name, CellNameWidth);
            var nameLabel = new Label()
            {
                Font = UiFonts.Body,
                Text = shortName,
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = CellNameWidth,
                Location = new Point(CellNameX, CellTextY),
                Parent = cellPanel
            };
            // Only when the name did not fit - an always-on tooltip
            // repeating the visible text is noise.
            TooltipFacility.ApplyPlain(nameLabel, shortName == name ? null : name);

            bool hasDefault = CurrencyDecisionDefaults.TryGetDefault(currencyId, out long defaultCopperPerUnit);

            var input = new TextBox()
            {
                Size = new Point(CellInputWidth, 26),
                Location = new Point(CellInputX, CellInputY),
                // Digits, not the unit: "copper" named what the box HELD
                // and so read as a unit label on a read-only field (field
                // test, bug 2). The greyed default is the number currently
                // in effect for this currency, which both prompts the shape
                // of the input and states what typing over it replaces. A
                // currency with no default has nothing to suggest and shows
                // an empty box; "Copper per unit" over the column carries
                // the unit for both. The 70px box leaves ~50px of text
                // region (Blish's TextBox insets the placeholder by 10px a
                // side and does not truncate it), which every value in
                // CurrencyDecisionDefaults - 3600 is the largest - fits.
                // Set once here for the pre-load state;
                // RefreshCurrencyRowDefaultState owns it from then on, and
                // clears it while the currency is ignored.
                PlaceholderText = hasDefault
                    ? defaultCopperPerUnit.ToString(CultureInfo.InvariantCulture)
                    : "",
                Parent = cellPanel
            };
            // Feature 1 spec: the estimate is labeled as such, with
            // attribution/editable/clearable spelled out on hover.
            TooltipFacility.ApplyPlain(input, hasDefault
                ? $"Default estimate {defaultCopperPerUnit} copper per unit, adapted from gw2efficiency (decision-only). Type your own amount here and press Save to override it, or tick Ignore to suppress it."
                : "Coin value of one unit, in copper. Type an amount here and press Save.");

            var defaultLabel = new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = InfoTextColor,
                Location = new Point(CellTagX, CellTextY),
                Parent = cellPanel
            };
            TooltipFacility.ApplyPlain(defaultLabel, hasDefault
                ? "This currency is valued automatically at its default estimate unless you type your own amount or tick Ignore."
                : null);

            var errorLabel = new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                TextColor = ErrorTextColor,
                BasicTooltipText = "Enter a positive whole number of copper, or leave the box blank.",
                Location = new Point(CellTagX, CellTextY),
                Parent = cellPanel
            };

            Checkbox clearCheckbox = null;
            if (hasDefault)
            {
                // "Clear" named an ACTION this control does not perform:
                // it is a persistent three-state flag that suppresses the
                // curated default, not a button that empties the box beside
                // it (audit batch J, M12). "Ignore" names the state.
                //
                // Not the longer "Ignore default": the cell reserves
                // SettingsCurrencyGridLayout.CellClearWidth (70px) for this
                // control, and widening that pushes MinColumnWidth past half
                // the 884px content region the 930px window minimum leaves,
                // dropping the whole grid from two columns to one at
                // minimum width. "Ignore" fits the existing budget; the tag
                // slot immediately right of it ("default 3600" / "ignored")
                // and the tooltip carry the rest of the meaning.
                clearCheckbox = new Checkbox()
                {
                    Text = "Ignore",
                    Location = new Point(CellClearX, CellTextY),
                    Parent = cellPanel
                };
                TooltipFacility.ApplyPlain(
                    clearCheckbox,
                    "Ignore this currency's default estimate - it will not be valued unless you enter your own amount.");
            }

            // Appended in the same step as the row it names - the filter
            // maps grid.Cells[i] back onto _rows[i] by index.
            _currencyNames.Add(name);
            _rows.Add(new CurrencyRow
            {
                CurrencyId = currencyId,
                HasDefault = hasDefault,
                DefaultCopperPerUnit = defaultCopperPerUnit,
                Cell = cellPanel,
                // Shown/hidden per filter pass: the cells on the last
                // populated grid row carry no rule (see ApplyCurrencyFilter).
                Divider = LabelHelpers.CreateRowDivider(
                    cellPanel, columnWidth, CurrencyRowHeight, CellDividerClearance),
                Input = input,
                DefaultLabel = defaultLabel,
                ErrorLabel = errorLabel,
                ClearCheckbox = clearCheckbox
            });
        }

        /// <summary>
        /// Packs the cells matching the filter box two-up (one-up on a
        /// narrow panel) and hides the rest. The grid panel keeps its
        /// unfiltered height throughout - see SetCurrencyGridHeight.
        /// </summary>
        private void ApplyCurrencyFilter()
        {
            if (_currencyGridPanel == null) return;

            for (int i = 0; i < _rows.Count && i < _currencyForceVisible.Length; i++)
            {
                _currencyForceVisible[i] = _rows[i].ErrorLabel.Text.Length > 0;
            }

            var grid = SettingsCurrencyGridLayout.Compute(
                _currencyNames, _currencyFilterInput?.Text, _panelWidth, CurrencyRowHeight,
                _currencyForceVisible);

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var placement = grid.Cells[i];

                row.Cell.Visible = placement.Visible;
                if (placement.Visible)
                {
                    row.Cell.Location = new Point(placement.X, placement.Y);
                }
                // Hidden cells report Row = -1, so the guard below also
                // keeps their rule off.
                row.Divider.Visible = placement.Row >= 0 && placement.Row < grid.RowCount - 1;
            }

            if (_currencyCountLabel != null)
            {
                _currencyCountLabel.Text = grid.VisibleCount == _rows.Count
                    ? $"{_rows.Count} currencies"
                    : $"{grid.VisibleCount} of {_rows.Count} shown";
            }
        }

        /// <summary>
        /// Refreshes one row's Clear checkbox, its input placeholder and its
        /// default/cleared tag
        /// from the given already-loaded or just-saved valuation - shared by
        /// LoadCurrentValuations and SaveValuations so the two can never
        /// disagree about how to render the same state. The tag is a label,
        /// not the input's placeholder: the placeholder is clipped to ~50px
        /// of text region by the 70px box (Blish's TextInputBase draws it
        /// untruncated inside the control's own scissor), which cuts the
        /// number off the default state entirely, and it would vanish behind
        /// any typed override besides.
        /// </summary>
        private static void RefreshCurrencyRowDefaultState(CurrencyRow row, CurrencyValuation valuation)
        {
            if (!row.HasDefault)
            {
                return;
            }

            bool isCleared = valuation.IsCleared(row.CurrencyId);
            bool hasOverride = valuation.TryGetCopperValue(row.CurrencyId, out _);

            row.ClearCheckbox.Checked = isCleared;

            // The placeholder states the number in effect (see
            // AddCurrencyRow), so it has to follow the same state the tag
            // does: an ignored currency has NO number in effect, and a
            // greyed default left in the box there would contradict the
            // "ignored" tag beside it.
            row.Input.PlaceholderText = isCleared
                ? ""
                : row.DefaultCopperPerUnit.ToString(CultureInfo.InvariantCulture);
            row.DefaultLabel.Text = isCleared
                ? "ignored"
                : hasOverride
                    ? $"was {row.DefaultCopperPerUnit}"
                    : $"default {row.DefaultCopperPerUnit}";
            row.DefaultLabel.TextColor = isCleared ? WarningTextColor : InfoTextColor;
        }

        /// <summary>
        /// The cell's two tags share one slot (see CurrencyRow.DefaultLabel):
        /// the red warning takes it while an amount will not parse, and the
        /// default/cleared state comes back when it does.
        /// </summary>
        private static void SetCurrencyRowError(CurrencyRow row, string text)
        {
            row.ErrorLabel.Text = text ?? "";
            row.DefaultLabel.Visible = row.ErrorLabel.Text.Length == 0;
        }

        /// <summary>
        /// The tab's one Save button and its one status label, in a bar that
        /// is a sibling of the scrolling content (not a row inside it), so
        /// Save stays reachable from any scroll position. Anchored at the
        /// TOP rather than as a bottom footer: LogTabContent already builds
        /// a fixed toolbar this way above its own CanScroll FlowPanel, and a
        /// top bar needs only ContentRegion.Width to place correctly - a
        /// bottom footer would additionally depend on ContentRegion.Height
        /// being final at Build time, whose failure mode is a Save bar
        /// floating over the rows.
        /// </summary>
        private Panel BuildSaveBar(Container container)
        {
            var barPanel = new Panel()
            {
                Size = new Point(container.ContentRegion.Width, SaveBarHeight),
                Parent = container
            };

            var saveButton = new FeedbackButton()
            {
                Text = "Save",
                Size = new Point(80, UiMetrics.ButtonHeight),
                Location = new Point(NameColumnX, 6),
                BasicTooltipText = "Save every section on this tab.",
                Parent = barPanel
            };
            saveButton.Click += (_, __) => SaveAll();

            _statusLabel = new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(NameColumnX + 80 + 12, 12),
                Parent = barPanel
            };

            return barPanel;
        }

        /// <summary>
        /// What a SaveAll actually got to disk. The in-tab Save button
        /// ignores it and reads the status label instead; a caller saving
        /// from OUTSIDE the tab has no status label on screen (the save
        /// bar is unparented the moment the view is torn down), so it has
        /// to be told in the return value or the failure is silent.
        /// </summary>
        public readonly struct SaveOutcome
        {
            public SaveOutcome(int invalidCount, bool writeFailed)
            {
                InvalidCount = invalidCount;
                WriteFailed = writeFailed;
            }

            /// <summary>Entries rejected by their section's parser and left at their persisted value.</summary>
            public int InvalidCount { get; }

            /// <summary>The currency valuation write itself failed - see the module log.</summary>
            public bool WriteFailed { get; }

            public bool AllSaved => !WriteFailed && InvalidCount == 0;
        }

        /// <summary>
        /// Persists every section - currency valuations, Homestead tiers,
        /// logging policy, snapshot refresh interval - in place of the four
        /// per-section Save buttons. Each section keeps its own per-row
        /// error labels and its own "invalid rows are left as previously
        /// persisted" contract; only the confirmation is shared.
        /// </summary>
        public SaveOutcome SaveAll()
        {
            bool valuationsSaved = SaveValuations(out int invalidCount);
            invalidCount += SaveHomesteadTiers();
            invalidCount += SaveLoggingSettings();
            invalidCount += SaveSnapshotSettings();

            if (valuationsSaved)
            {
                // Rebased on the CONTROLS, not on what reached disk. An
                // entry that would not parse keeps its previously
                // persisted value but its text stays in the box, so a
                // baseline taken from persisted state would leave the tab
                // permanently dirty and re-prompt on every later tab
                // switch to save a value that can never be saved. The
                // status line below already tells the user those entries
                // were not saved. A failed valuation write is the one case
                // that does NOT rebase - there the edits really are still
                // unsaved, and the next prompt should say so.
                _baseline = CaptureFormState();
            }

            var outcome = new SaveOutcome(invalidCount, !valuationsSaved);

            if (_statusLabel == null) return outcome;

            if (!valuationsSaved)
            {
                // Defensive branch (see SaveValuations' catch): the other
                // three sections did persist, but a failed write is the
                // headline and the per-row errors stay on screen.
                _statusLabel.Text = "Save failed - see log";
                _statusLabel.TextColor = ErrorTextColor;
                return outcome;
            }

            if (invalidCount == 0)
            {
                _statusLabel.Text = StatusText.Stamp("Saved", DateTime.Now);
                _statusLabel.TextColor = SuccessTextColor;
            }
            else
            {
                string entryWord = invalidCount == 1 ? "entry" : "entries";
                _statusLabel.Text = $"Saved - {invalidCount} invalid {entryWord} not saved";
                _statusLabel.TextColor = WarningTextColor;
            }

            return outcome;
        }

        private void LoadCurrentValuations()
        {
            var valuation = _settings.GetCurrencyValuation();

            foreach (var row in _rows)
            {
                row.Input.Text = valuation.TryGetCopperValue(row.CurrencyId, out long copperPerUnit)
                    ? copperPerUnit.ToString(CultureInfo.InvariantCulture)
                    : "";
                SetCurrencyRowError(row, "");
                RefreshCurrencyRowDefaultState(row, valuation);
            }
        }

        /// <summary>
        /// Returns false when the valuation could not be persisted at all;
        /// invalidCount counts rows whose text did not parse (those keep
        /// their previously-persisted value either way).
        /// </summary>
        private bool SaveValuations(out int invalidCount)
        {
            // Seeded from the currently-persisted valuation (not empty) so
            // an invalid row is left untouched below rather than silently
            // dropped: the status label tells the user invalid entries are
            // "not saved", which must mean unchanged, not cleared. Only a
            // row the user deliberately blanks is removed. currency-ux-
            // package (Feature 1): cleared is seeded the same way, for the
            // same reason - a row nobody touched this Save must keep
            // whatever cleared/default state it already had persisted.
            var persisted = _settings.GetCurrencyValuation();
            // .NET Framework 4.8's Dictionary<TKey,TValue> has no
            // constructor overload accepting IReadOnlyDictionary<TKey,
            // TValue> (only IDictionary<TKey,TValue>) - CopperPerUnit is
            // exposed as the former, so this is a manual copy rather than
            // a one-line constructor call.
            var entries = new Dictionary<int, long>();
            foreach (var kvp in persisted.CopperPerUnit)
            {
                entries[kvp.Key] = kvp.Value;
            }
            var cleared = new HashSet<int>(persisted.ClearedCurrencyIds);

            invalidCount = 0;

            foreach (var row in _rows)
            {
                SetCurrencyRowError(row, "");

                string text = row.Input.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    // Blank box = no explicit override. Feature 1: this no
                    // longer means "excluded from comparison" outright - a
                    // currency with a curated default is still valued via
                    // that default unless the Clear checkbox is checked,
                    // which is the ONLY thing that persists a genuine
                    // suppression (see CurrencyValuation's own doc comment
                    // on the three-state precedence).
                    entries.Remove(row.CurrencyId);
                    if (row.HasDefault && row.ClearCheckbox != null && row.ClearCheckbox.Checked)
                    {
                        cleared.Add(row.CurrencyId);
                    }
                    else
                    {
                        cleared.Remove(row.CurrencyId);
                    }
                    continue;
                }

                if (SettingsInputParser.TryParseCopperValue(text, out long copperPerUnit))
                {
                    entries[row.CurrencyId] = copperPerUnit;
                    // An explicit value always wins over a stale cleared
                    // marker - CurrencyValuation's constructor rejects a
                    // currency id that is both valued and cleared at once.
                    cleared.Remove(row.CurrencyId);
                }
                else
                {
                    // Left out of this row's changes entirely - whatever
                    // was previously persisted for this currency (if
                    // anything) is preserved, matching "not saved" below.
                    // Short enough to stay inside a half-width cell; the
                    // label's tooltip carries the full rule.
                    SetCurrencyRowError(row, "Invalid");
                    invalidCount++;
                }
            }

            // A row the filter is hiding still counts towards the save bar's
            // "N invalid entries not saved", so re-run the filter with those
            // rows forced visible - a warning whose tag is off screen points
            // the user at nothing.
            ApplyCurrencyFilter();

            CurrencyValuation saved;
            try
            {
                saved = new CurrencyValuation(entries, cleared);
                _settings.SetCurrencyValuation(saved);
            }
            catch (Exception ex)
            {
                // Defensive: entries/cleared are seeded from the already-
                // valid persisted valuation and only ever updated with
                // SettingsInputParser-validated positive values (removing
                // the same id from `cleared` in the same step) on non-coin
                // currency ids, so CurrencyValuation's constructor should
                // never actually reject this. Still guarded so a future
                // change to either side degrades to a visible status
                // message instead of an unhandled exception on the UI
                // thread.
                Logger.Warn(ex, "Failed to save currency valuations");
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "settings", $"Failed to save currency valuations: {ex.GetType().Name} - {ex.Message}");
                return false;
            }

            foreach (var row in _rows)
            {
                RefreshCurrencyRowDefaultState(row, saved);
            }

            return true;
        }
    }
}
