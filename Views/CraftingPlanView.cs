using Blish_HUD;
using Blish_HUD.Content;
using MonoGame.Extended.BitmapFonts;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using GW2CraftingHelper.Contracts;
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

// The one deliberate use of #region in the codebase - navigation markers
// for a very large class pending further extraction; scoped to this file
// only, not the shared ruleset. See docs/ARCHITECTURE.md sections 1, 3,
// and 5 for the FrameTicker/scroll preserve-restore-verify rationale and
// the section-renderer decomposition.
#pragma warning disable SA1124 // Do not use regions

namespace GW2CraftingHelper.Views
{
    public class CraftingPlanView : ISectionRelayoutSink
    {
        #region General: shared layout constants, colors, top-region geometry & dependencies

        // Not one of the architecture report's 11 responsibilities - shared
        // substrate consumed by several regions below (see m38-a1-architecture.md S3).
        private static readonly Logger Logger = Logger.GetLogger<CraftingPlanView>();

        // Layout constants
        private const int RowHeight = 35;
        private const int InputRowY = 5;

        // The top strip is InputRowsAreaHeight(N) item rows followed by
        // the controls/status/separator rows (see ComputeTopRegionLayout);
        // with N == 1 every Y offset reproduces the original fixed
        // single-row layout exactly.
        private const int TopRegionRowGap = 3;
        private const int StatusToSeparatorGap = 21;
        private const int SeparatorToContentGap = 5;
        private const int ContentToBottomPad = 5;
        private const int RightEdgePadding = 20;
        private const int SectionSpacing = 16;

        // Section divider grey, readable against the parchment texture, one
        // tier below the 180-grey structural separators (window chrome,
        // unrelated to this). The row-divider twin (RowDividerColor) moved
        // to Views/Rendering/LabelHelpers.cs alongside
        // LabelHelpers.CreateRowDivider - it had no other caller.
        private static readonly Color SectionDividerColor = new Color(130, 130, 130);

        /// <summary>
        /// Pure top-strip Y-offset arithmetic (Blish-free math, kept as a
        /// plain struct/method rather than a control mutation so Build()
        /// and every row Add/Remove reflow call the exact same formula).
        /// See the constants'
        /// own doc comment for the rowCount==1 byte-identical guarantee.
        /// </summary>
        private struct TopRegionLayout
        {
            public int InputPanelHeight;
            public int ControlsRowY;
            public int StatusRowY;
            public int SeparatorY;
            public int ContentY;
            public int TopRegionHeight;
        }

        private static TopRegionLayout ComputeTopRegionLayout(int rowCount)
        {
            int inputPanelHeight = rowCount * RowHeight;
            int controlsRowY = InputRowY + inputPanelHeight + TopRegionRowGap;
            int statusRowY = controlsRowY + RowHeight + TopRegionRowGap;
            int separatorY = statusRowY + StatusToSeparatorGap;
            int contentY = separatorY + SeparatorToContentGap;
            return new TopRegionLayout
            {
                InputPanelHeight = inputPanelHeight,
                ControlsRowY = controlsRowY,
                StatusRowY = statusRowY,
                SeparatorY = separatorY,
                ContentY = contentY,
                TopRegionHeight = contentY + ContentToBottomPad
            };
        }

        // phaseProgress carries live coarse-phase events for the status
        // strip; requestLabel is a best-effort item-name label; the
        // valueOwnMaterials bool is a per-plan session choice, like useOwn.
        private readonly Func<IReadOnlyList<PlanRequestItem>, bool, bool, PriceBasis, CancellationToken, IProgress<PlanStatus>, IProgress<PlanPhaseEvent>, string, Task<CraftingPlanResult>> _generateAsync;
        private readonly Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, ISet<int>, CraftingPlanResult> _resolveOverridesSync;
        private readonly ModalDialog _modalDialog;
        private readonly IItemSearchProvider _itemSearchProvider;
        private readonly ModuleSettings _settings;
        private readonly PlanViewModelBuilder _vmBuilder = new PlanViewModelBuilder();

        private PlanViewModel _currentPlan;

        private DateTime _planGeneratedAt;
        // Defaults to true - a deliberate divergence from gw2efficiency,
        // whose default is unchecked. Purely in-memory session state,
        // reset on every module reload.
        private bool _useOwnMaterials = true;
        // gw2efficiency's own default is "buy price" (buy orders); echoed
        // here so a fresh plan matches gw2e's view rather than
        // systematically overpricing every material.
        private PriceBasis _priceBasis = PriceBasis.BuyOrder;
        // The "Value own materials" toggle - a per-plan session choice,
        // never written back to ModuleSettings and never re-read after
        // construction (the constructor seeds it from the persisted
        // setting so a prior user choice applies to a fresh session's
        // first plan). Only meaningful while _useOwnMaterials is on; the
        // last-chosen value is preserved while disabled.
        private bool _valueOwnMaterials = true;

        #endregion // General: shared layout constants, colors, top-region geometry & dependencies

        #region 1. Input rows (state) - multi-item plans (gw2efficiency parity)

        /// <summary>
        /// One row of the multi-item input strip (gw2efficiency
        /// parity): the plain session-persistent selection fields survive
        /// across Build() calls (tab switches) exactly like _nodeOverrides/
        /// _ignoredItemIds below - the live Blish controls do not (they are
        /// disposed and recreated by every Build()/RebuildInputRows() call,
        /// same lifecycle as _searchBox/_qtyInput used to have).
        /// </summary>
        private sealed class ItemRowState
        {
            public int? ItemId;
            public string ItemName;
            public string QuantityText = "1";

            public Panel RowPanel;
            public AutocompleteTextBox SearchBox;
            public SuggestionPanel SuggestionPanel;
            public TextBox QtyInput;
        }

        // Session-persistent row list, mirroring gw2e's `e.recipes`
        // array. Populated with one empty row on the first Build();
        // survives every later Build() (tab switch). No file persistence.
        private readonly List<ItemRowState> _itemRows = new List<ItemRowState>();

        #endregion // 1. Input rows (state) - multi-item plans (gw2efficiency parity)

        #region 2. Generate orchestration (state)

        // Bumped at the start of every TriggerGenerate call (Generate button
        // and OnOwnMaterialsToggled's modal-confirm path both funnel through
        // it). Each call captures its own value and every deferred callback
        // it queues re-checks it against the live field before applying
        // anything, so a superseded generation's result cannot clobber a
        // newer one (last-drained-wins) even though both entry points can
        // overlap in flight.
        private int _generateSequence;

        // The module-owned, thread-safe holder of record for the status
        // strip's live phase text and final completion/error text (see
        // PlanStripStatusBoard). Constructor-injected so it survives any
        // single Build() cycle; every writer goes through the board's own
        // internal guards, and SpinnerTick/RenderFromBoard/Build()'s
        // re-arm block all PULL from it.
        private readonly PlanStripStatusBoard _statusBoard;
        private int _spinnerFrameIndex;
        private DateTime _lastSpinnerTickUtc;
        private static readonly char[] SpinnerFrames = { '|', '/', '-', '\\' };
        private static readonly TimeSpan SpinnerTickInterval = TimeSpan.FromMilliseconds(150);

        // The toolbar's Use Own Materials / Prices / Value Own Materials
        // controls only take effect on the next Generate, unlike the
        // instant-apply controls that look just like them on other tabs.
        // Every one of them says so through the status label at the moment
        // it changes.
        private const string SettingsChangedStatus = "Settings changed - press Generate Plan to update";

        // How far the plan on screen is dimmed while a new one generates -
        // enough to read as superseded, not so far that it stops being
        // readable while you wait.
        private const float StalePlanOpacity = 0.45f;

        // How many results the typed-name resolution pass asks for. Only an
        // exact name match is adopted, so this just has to be wide enough
        // for the wanted name to survive the provider's own ranking among
        // items that share a prefix ("Mystic Clover" vs "Mystic Coin"); the
        // suggestion list itself shows 8.
        private const int TypedNameSearchResults = 8;

        #endregion // 2. Generate orchestration (state)

        #region 8. Tree rendering (state)

        // The tree section renderer and its interactive override loop
        // (see TreeSectionController) - a single persistent instance,
        // constructed once in the ctor since its state must survive every
        // RenderPlan call, unlike the stateless section renderers that
        // are freshly constructed per section.
        private readonly TreeSectionController _treeController;

        #endregion // 8. Tree rendering (state)

        #region 7. Section builders (state: section expand/collapse)
        private readonly Dictionary<PlanSectionType, bool> _sectionExpansion =
            new Dictionary<PlanSectionType, bool>();

        // "Hide Unlocked Recipes" checkbox state - default-checked, plain
        // session state, not persisted. RequiredRecipesVisibility
        // (Blish-free) owns the filter predicate/header-text logic; this
        // field is only the live toggle state.
        private bool _hideUnlockedRecipes = true;

        #endregion // 7. Section builders (state: section expand/collapse)

        #region 2. Generate orchestration (state, continued)

        // Suppress flag for checkbox revert
        private bool _suppressToggle;

        // Debug log from last plan generation
        private IReadOnlyList<string> _lastDebugLog;
        public IReadOnlyList<string> LastDebugLog => _lastDebugLog;

        #endregion // 2. Generate orchestration (state, continued)

        #region General: Blish UI control fields (shared across all responsibilities)

        // UI controls (stored for resize handler)

        // The Container Build() was called with, retained so
        // AddItemRow/RemoveItemRow (fired by a row button's Click, long
        // after Build() returns) can re-read ContentRegion and reflow the
        // top strip - see ReflowInputRegion.
        private Container _buildPanel;
        private Panel _inputPanel;
        private Panel _controlsPanel;
        private Checkbox _ownMaterialsCheckbox;
        private Checkbox _valueOwnMaterialsCheckbox;
        private StandardButton _generateButton;
        private Label _statusLabel;
        private Panel _separator;
        private FlowPanel _contentPanel;

        #endregion // General: Blish UI control fields (shared across all responsibilities)

        #region 5. Resize relayout (state) - KNOWN-ISSUES #13/#19

        // Resize tracking
        private int _lastRenderedWidth;

        // Per-render relayout registry, cleared and repopulated by every
        // full RenderPlan rebuild and appended to by lazy tree expansion.
        // _relayoutActions holds cheap position/width-only closures (no
        // MeasureString) replayed on every resize tick; _reellipsisActions
        // holds the text-truncating subset, replayed only at drag settle.
        // Neither list ever changes a control's Height, so neither can
        // perturb AutoSize/scroll state.
        private readonly List<Action<int>> _relayoutActions = new List<Action<int>>();
        private readonly List<Action<int>> _reellipsisActions = new List<Action<int>>();

        // ISectionRelayoutSink implementation - explicit-interface so
        // extracted renderers register through the seam without widening
        // the public surface. Both members pass straight through to the
        // two lists above, so every invariant reading those lists sees a
        // sink-registered closure exactly like an inline one.
        void ISectionRelayoutSink.AddRelayout(Action<int> closure)
        {
            _relayoutActions.Add(closure);
        }

        void ISectionRelayoutSink.AddReellipsis(Action<int> closure)
        {
            _reellipsisActions.Add(closure);
        }

        int ISectionRelayoutSink.RelayoutCount => _relayoutActions.Count;

        // Trailing debounce for the resize-settle re-ellipsis pass. Every
        // relayout tick already runs synchronously in OnPanelResized (no
        // debounce needed for pure width/position writes - see
        // ReplayRelayout); this debounce exists ONLY to bound how often the
        // 3 LabelHelpers.EllipsizeToWidth call sites re-measure text, which is
        // comparatively expensive over a long shopping list or deep tree.
        // _resizeSettlePending gates a single in-flight settle ticker; each
        // real frame it checks whether ResizeDebounceMs has elapsed since
        // the last resize tick, then runs the re-ellipsis pass (plus one
        // best-effort full relayout replay, as a defensive correctness net)
        // once it has - see ResizeSettleStep and FrameTicker.
        private const int ResizeDebounceMs = 150;
        private DateTime _lastResizeEventUtc;
        private bool _resizeSettlePending;

        #endregion // 5. Resize relayout (state) - KNOWN-ISSUES #13/#19

        #region 3. Scroll preserve/restore/verify (state) - KNOWN-ISSUES #12/#14/#19

        // Bumped by every PreserveScrollAcross call; an in-flight
        // StartScrollVerify loop compares its captured value against the
        // current one each frame and bails as soon as a newer restore has
        // superseded it.
        private int _scrollRestoreGeneration;

        #endregion // 3. Scroll preserve/restore/verify (state) - KNOWN-ISSUES #12/#14/#19

        #region 5. Resize relayout (state, continued) - KNOWN-ISSUES #13/#19

        // Set by PreserveScrollAcrossResize whenever a height-changing
        // resize tick wrote a per-tick scroll-preserve; ResizeSettleStep
        // arms one bounded verify window at drag settle, then clears it.
        // _resizeScrollSavedOffset holds the last known-good pre-tick
        // offset and is only updated when a tick's capture is > 0, so an
        // uncontested reset landing between ticks cannot erase the real
        // target. PreserveScrollAcross (the rebuild path) clears the
        // pending flag up front - an offset captured against disposed
        // content is meaningless.
        private bool _resizeScrollRestorePending;
        private int _resizeScrollSavedOffset;

        #endregion // 5. Resize relayout (state, continued) - KNOWN-ISSUES #13/#19

        #region 6. The FrameTicker control (ticker instance fields) - KNOWN-ISSUES #12/#13

        // Live FrameTicker instances (null when idle). Tracked so Build()
        // can cancel a leftover ticker from the previous build cycle before
        // starting a new one, using the same SpriteScreen-parented cleanup
        // pattern _suggestionPanel uses: these tickers are parented to the
        // SpriteScreen rather than this view's own control tree, so nothing
        // else tears them down when a tab is torn down or the module
        // unloads. Each ticker also bails itself out on its own next frame
        // (generation mismatch, panel swap, or panel detached) as a second
        // line of defense.
        private FrameTicker _scrollVerifyTicker;
        private FrameTicker _resizeDebounceTicker;

        // Drives the status strip's rotating spinner during
        // TriggerGenerate; re-armed by Build() whenever _statusBoard
        // reports a generation still in flight across a tab switch.
        private FrameTicker _spinnerTicker;

        #endregion // 6. The FrameTicker control (ticker instance fields) - KNOWN-ISSUES #12/#13

        #region 4. Wheel-wrap correction (state) - KNOWN-ISSUES #12 (reopened)

        // Defensive one-shot re-assert ticker for
        // ApplyWheelWrapCorrection (see StartWheelWrapVerify). Its own
        // field, not shared with _scrollVerifyTicker: the two guard
        // unrelated writers with different targets, and one verify must
        // not cancel-and-replace the other.
        private FrameTicker _wheelWrapVerifyTicker;

        private const int WheelWrapVerifyMaxFrames = 2;

        // Matches StartScrollVerify's own stable-match tolerance.
        private const float WheelWrapVerifyEpsilon = 0.004f;

        #endregion // 4. Wheel-wrap correction (state) - KNOWN-ISSUES #12 (reopened)

        #region 3. Scroll preserve/restore/verify (state, continued) - KNOWN-ISSUES #12/#14/#19

        // With container heights finalized synchronously during build
        // (PlanContentHeightMath), the restore ratio is correct the
        // instant PreserveScrollAcross writes it. This short defensive
        // verify exists only to contest a genuinely LATE Blish-internal
        // scrollbar reset (RecalculateLayout zeroes ScrollDistance when
        // _scrollbarPercent changes, which can land a frame or two after
        // the synchronous write) and yields the moment real user input is
        // observed.
        private const int ScrollVerifyMaxFrames = 3;

        // Bounds the guard's zero-reassert back-and-forth (Blish resets
        // the bar to 0, we contest, it resets again...) so a user
        // genuinely holding the bar at top eventually wins rather than
        // being fought forever.
        private const int ScrollVerifyZeroReassertCap = 4;

        // Timestamp of the most recent user mouse-wheel event over the
        // content panel. Tracked unconditionally (not diagnostics-gated):
        // any wheel event observed since a verify window armed yields
        // that window immediately. Reset at the top of every Build() so
        // a stale value cannot influence a new render.
        private DateTime? _lastWheelEventUtc;

        #endregion // 3. Scroll preserve/restore/verify (state, continued) - KNOWN-ISSUES #12/#14/#19

        #region 4. Wheel-wrap correction (state, continued) - KNOWN-ISSUES #12 (reopened)

        // Blish HUD's
        // Scrollbar.SCROLL_WHEEL private const (vendored Controls/
        // Scrollbar.cs, BlishHUD v1.3.0, confirmed by decompiling the
        // shipped "Blish HUD.exe") - one wheel EVENT (regardless of how
        // many raw notches Windows coalesced into it) moves the bar by
        // exactly this many pixels times SystemInformation.
        // MouseWheelScrollLines, per Scrollbar.HandleWheelScroll/
        // ScrollAnimated (sign-only, never magnitude-scaled). A private
        // const has no runtime field to reflect (unlike PanelScrollbarField
        // above), so this is hardcoded with this provenance note -
        // re-verify against the vendored source on any BlishHUD upgrade.
        private const int BlishScrollWheelStepPixels = 30;

        #endregion // 4. Wheel-wrap correction (state, continued) - KNOWN-ISSUES #12 (reopened)

        #region Diagnostics: scroll/wheel instrumentation (shared by #3 and #4) - KNOWN-ISSUES #12

        // Instrumentation-only, gated on ScrollDiagnosticsEnabled; every
        // call site checks the setting before doing any work, so the
        // disabled cost is a single bool read. Diagnostics only observe -
        // never fed back into any scroll/guard/restore decision.
        private const string ScrollDiagTag = "[scrolldiag]";

        // Monotonic frame index shared by every scroll-diagnostic log line
        // (wheel handler, SyncRestore, Verify) so a human reading the log can
        // tell same-frame vs cross-frame ordering apart even when wall-clock
        // log timestamps collide. GameService.Overlay.CurrentGameTime
        // already advances exactly once per real engine frame (the vendor
        // Scrollbar/Control use it the same way for double-click timing), so
        // comparing it to the last-seen value is a cheap, ticker-free way to
        // detect "a new real frame happened" without adding a dedicated
        // always-on ticker. Only ever touched from diagnostics-gated call
        // sites.
        private TimeSpan? _scrollDiagLastFrameTime;
        private long _scrollDiagFrameCounter;

        private long ScrollDiagFrame()
        {
            TimeSpan? current = GameService.Overlay?.CurrentGameTime?.TotalGameTime;
            if (current.HasValue && current.Value != _scrollDiagLastFrameTime)
            {
                _scrollDiagFrameCounter++;
                _scrollDiagLastFrameTime = current.Value;
            }
            return _scrollDiagFrameCounter;
        }

        // Single read-through for the diagnostics gate. Also true when
        // the unified LogDiagnosticsEnabled setting is on - it subsumes
        // ScrollDiagnosticsEnabled, but the old setting stays readable so
        // an already-persisted true keeps gating this channel across the
        // upgrade.
        private bool ScrollDiagEnabled => _settings != null &&
            (_settings.LogDiagnosticsEnabled.Value || _settings.ScrollDiagnosticsEnabled.Value);

        /// <summary>
        /// Routes every [scrolldiag] line to both sinks - Blish's Logger
        /// and the module-wide ModuleLog (Debug, tag "scrolldiag") - so
        /// the channel is visible in the Log tab. Centralized so the
        /// tag/level is defined exactly once.
        /// </summary>
        private void LogScrollDiag(string message)
        {
            Logger.Debug(message);
            ModuleLog.Shared.Write(ModuleLogLevel.Debug, "scrolldiag", message);
        }

        #endregion // Diagnostics: scroll/wheel instrumentation (shared by #3 and #4) - KNOWN-ISSUES #12

        #region General: construction & status
        public CraftingPlanView(
            Func<IReadOnlyList<PlanRequestItem>, bool, bool, PriceBasis, CancellationToken, IProgress<PlanStatus>, IProgress<PlanPhaseEvent>, string, Task<CraftingPlanResult>> generateAsync,
            ModalDialog modalDialog,
            IItemSearchProvider itemSearchProvider,
            ModuleSettings settings,
            PlanStripStatusBoard statusBoard,
            Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, ISet<int>, CraftingPlanResult> resolveOverridesSync = null)
        {
            _generateAsync = generateAsync;
            _modalDialog = modalDialog;
            _itemSearchProvider = itemSearchProvider;
            _settings = settings;
            _statusBoard = statusBoard ?? throw new ArgumentNullException(nameof(statusBoard));
            _resolveOverridesSync = resolveOverridesSync;

            // Seed the per-plan default from the persisted setting so a
            // user who turned "Value own materials" off is not silently
            // switched back on module reload. Session-only from here on -
            // never written back to settings.
            if (settings != null)
            {
                _valueOwnMaterials = settings.ValueOwnMaterials.Value;
            }

            // Wires TreeSectionController's collaborator delegates: four
            // plain method groups, plus three small adapters over state
            // with no method to bind (including unpacking the private
            // SectionHeaderHandle into a ValueTuple so the nested type
            // never becomes visible outside this class).
            _treeController = new TreeSectionController(
                this,
                _resolveOverridesSync,
                _vmBuilder,
                PreserveScrollAcross,
                SetStatus,
                RenderPlan,
                GetCurrentPanelWidth,
                () => _currentPlan,
                vm => _currentPlan = vm,
                log => _lastDebugLog = log,
                (title, sectionKey, panelWidth, defaultExpanded, suppressToggle) =>
                {
                    var header = CreateSectionHeader(title, sectionKey, panelWidth, defaultExpanded, suppressToggle);
                    return (header.HeaderPanel, header.ArrowLabel, header.ContentFlow);
                });
        }

        public void SetStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = status ?? "";
            }
        }

        /// <summary>
        /// Applies a plan loaded from disk at module load, rendering it
        /// instantly - no network call, no re-solve. Called from
        /// Module.Update()'s dirty-flag drain (main thread), at most once
        /// per session, always before the user could have clicked
        /// Generate. Mirrors TriggerGenerate's success-path shape: adopts
        /// <paramref name="result"/> as the override loop's baseline,
        /// restores the user's prior decision-pill overrides
        /// (RestoreOverrides - required, not optional), resets section
        /// expansion, rebuilds the view model, and seeds the status board
        /// with the staleness banner text.
        /// <para>
        /// The tab has usually not been Build() yet, in which case only
        /// the state fields are set and Build()'s render tail renders on
        /// first visit; a live tab renders directly, and also calls
        /// RenderFromBoard since Build()'s re-arm never runs again for it.
        /// </para>
        /// <para>
        /// Two narrow try/catches guard this: PlanStoreHelpers' tolerance
        /// gate is only structural, so a degraded plan.json can still
        /// throw inside the vm build or RenderPlan (the builder copies
        /// the tree by reference, so a null child is only dereferenced
        /// when RenderPlan walks it). The vm build happens before any
        /// state field is mutated, so a build failure leaves a clean
        /// fresh start; a render failure rolls back via
        /// <see cref="RollBackFailedPlanRender"/>, shared with Build()'s
        /// guarded tail so a poisoned vm can never be committed on either
        /// path.
        /// </para>
        /// </summary>
        public void ApplyRestoredPlan(
            CraftingPlanResult result,
            DateTime generatedAt,
            IReadOnlyDictionary<int, AcquisitionSource> nodeOverrides,
            IReadOnlyList<int> ignoredItemIds,
            // "Value Own Materials" checkbox state at the generation
            // time this plan was persisted - restoring it into the live
            // checkbox is the whole reason PersistedPlan.ValueOwnMaterials
            // exists. UseOwnMaterials/PriceBasis have the same gap (their
            // live controls are not restored) - see docs/KNOWN-ISSUES.md.
            bool valueOwnMaterials)
        {
            if (result == null) return;

            PlanViewModel vm;
            try
            {
                vm = _vmBuilder.Build(result);
            }
            catch (Exception ex)
            {
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "plan",
                    $"Failed to render restored plan, starting fresh: {ex.GetType().Name} - {ex.Message}");
                return;
            }

            _treeController.ResetForNewPlan(result);
            _treeController.RestoreOverrides(nodeOverrides, ignoredItemIds);
            _sectionExpansion.Clear();
            _lastDebugLog = result.DebugLog;
            _currentPlan = vm;
            _planGeneratedAt = generatedAt;

            // Restore the checkbox's
            // backing field AND its displayed Checked state - see this
            // method's valueOwnMaterials parameter doc comment.
            _valueOwnMaterials = valueOwnMaterials;
            if (_valueOwnMaterialsCheckbox != null)
            {
                _valueOwnMaterialsCheckbox.Checked = valueOwnMaterials;
            }

            // Culture policy (applies to every ToString(..., CultureInfo.
            // InvariantCulture) timestamp in this file, not just this one):
            // this module's UI/log strings are English-only, so timestamp
            // formatting is pinned to InvariantCulture rather than the
            // ambient CurrentCulture - matching MainView.cs, Module.cs,
            // SettingsTabContent.cs and LogTabContent.cs. This file's three
            // sites predate that policy (they originated the "MMM d, yyyy
            // h:mm tt" format string) and were converted to match it rather
            // than left on CurrentCulture, which would go on to produce a
            // blank AM/PM designator under several locales (e.g. de-DE's
            // short time pattern has no AM/PM marker) and would disagree
            // with the Log tab's own InvariantCulture timestamps.
            _statusBoard.SeedRestored(
                $"Generated {generatedAt.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)} - prices may have changed - Regenerate");
            RenderFromBoard(_statusBoard.Snapshot());

            if (_contentPanel == null || _contentPanel.Parent == null) return;

            _lastRenderedWidth = _contentPanel.Width;
            try
            {
                RenderPlan(vm);
            }
            catch (Exception ex)
            {
                RollBackFailedPlanRender(ex, "into the live tab");
            }
        }

        /// <summary>
        /// Shared rollback for a RenderPlan call that threw while
        /// rendering a restored plan - called from both places that can
        /// reach a still-unvalidated restored vm: ApplyRestoredPlan's
        /// live-tab branch and Build()'s guarded render tail. Restores
        /// every piece of state either call site may have committed back
        /// to the "nothing restored, nothing generated yet" shape:
        /// <list type="bullet">
        /// <item><description>_treeController's override/ignore/expansion
        /// baseline (ResetForNewPlan(null)) and its per-render tree
        /// state.</description></item>
        /// <item><description>_lastDebugLog/_currentPlan/_planGeneratedAt
        /// - a committed vm that cannot render would re-throw out of
        /// Build()'s tail on every later visit.</description></item>
        /// <item><description>_contentPanel's children - a mid-build
        /// exception can leave a partially-built plan parented in a live
        /// panel; ResetContentPanelToEmpty sweeps it.</description></item>
        /// <item><description>the status board's seeded staleness banner
        /// and its painted label text - both skipped when
        /// ClearRestoredSeed reports a real Generate has raced in, so a
        /// superseding generation's status is never
        /// clobbered.</description></item>
        /// </list>
        /// </summary>
        private void RollBackFailedPlanRender(Exception ex, string context)
        {
            ModuleLog.Shared.Write(ModuleLogLevel.Warn, "plan",
                $"Failed to render restored plan {context}: {ex.GetType().Name} - {ex.Message}");

            _treeController.ResetForNewPlan(null);
            _sectionExpansion.Clear();
            _lastDebugLog = null;
            _currentPlan = null;
            _planGeneratedAt = default(DateTime);

            ResetContentPanelToEmpty();

            if (_statusBoard.ClearRestoredSeed())
            {
                SetStatus("Ready");
            }
        }

        #endregion // General: construction & status

        #region 3. Scroll preserve/restore/verify (reflection handle + PreserveScrollAcross) - KNOWN-ISSUES #12/#14/#19

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
        /// scroll position afterwards. Every mutate() rebuild finalizes
        /// its explicit Height synchronously (PlanContentHeightMath), so
        /// the restore ratio is computed and written synchronously before
        /// the next paint; a short FrameTicker verify then only defends
        /// against a late Blish-internal scrollbar reset
        /// (StartScrollVerify).
        /// </summary>
        private void PreserveScrollAcross(Action mutate)
        {
            int saved = _contentPanel?.VerticalScrollOffset ?? 0;
            int capturedGeneration = ++_scrollRestoreGeneration;

            // A rebuild disposes and recreates every content-panel child,
            // so a still-pending resize-drag scroll-preserve is now
            // meaningless - clear it so a later settle tick never arms a
            // stale-offset verify against the new content.
            _resizeScrollRestorePending = false;

            mutate();
            if (saved > 0)
            {
                ApplySavedScrollSynchronously(saved, capturedGeneration);
            }
        }

        #endregion // 3. Scroll preserve/restore/verify (reflection handle + PreserveScrollAcross) - KNOWN-ISSUES #12/#14/#19

        #region 6. The FrameTicker control (nested Control subclass) - KNOWN-ISSUES #12/#13

        /// <summary>
        /// Drives a per-real-frame step callback from Control.DoUpdate,
        /// which the SpriteScreen invokes exactly once per real engine
        /// Update() pass (GraphicsService.Update -> SpriteScreen.Update ->
        /// Container.DoUpdate iterates its visible children -> child.Update
        /// -> child.DoUpdate). Unlike GameService.Overlay.QueueMainThread-
        /// Update - which drains a re-queued callback again within the SAME
        /// frame instead of waiting for the next Update() tick, the defect
        /// this class replaces - DoUpdate never fires more than once per
        /// real frame, so no frame-gating is needed here.
        ///
        /// The step callback returns true to keep ticking next frame, false
        /// to stop. A false return or an unhandled exception from the step
        /// both dispose the ticker (detaching it from the SpriteScreen) so
        /// it cannot keep running against stale state. A 1x1, fully
        /// transparent, empty-Paint control parented to the SpriteScreen is
        /// imperceptible; Visible must stay true because Container only
        /// calls Update on children that are Visible (or not yet laid out).
        /// </summary>
        private sealed class FrameTicker : Control
        {
            private readonly Func<GameTime, bool> _step;
            private TimeSpan? _lastFrameTime;
            private bool _canceled;

            // Observation-only. _scrollVerifyTicker is never nulled when
            // a ticker self-cancels, so a plain null-check cannot tell
            // "never started" from "finished long ago" - this property is
            // the accurate signal.
            public bool IsActive => !_canceled;

            public FrameTicker(Func<GameTime, bool> step)
            {
                _step = step ?? throw new ArgumentNullException(nameof(step));
                Size = new Point(1, 1);
                Location = new Point(0, 0);
                Visible = true;

                var screen = GameService.Graphics?.SpriteScreen;
                if (screen != null)
                {
                    Parent = screen;
                }
            }

            public override void DoUpdate(GameTime gameTime)
            {
                // Cheap belt-and-braces: DoUpdate is documented to fire
                // once per real frame, so this should never trigger, but
                // guards against a duplicate call with an unchanged
                // TotalGameTime regardless.
                TimeSpan? current = gameTime?.TotalGameTime;
                if (_lastFrameTime.HasValue && current.HasValue && current.Value == _lastFrameTime.Value)
                {
                    return;
                }
                _lastFrameTime = current;

                bool keepGoing;
                try
                {
                    keepGoing = _step(gameTime);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "FrameTicker step failed; stopping");
                    keepGoing = false;
                }

                if (!keepGoing)
                {
                    Cancel();
                }
            }

            protected override void Paint(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, Rectangle bounds)
            {
                // Intentionally empty - this control exists only to receive
                // per-frame DoUpdate calls and must never draw anything.
            }

            protected override CaptureType CapturesInput()
            {
                // A default Control intercepts mouse input over its bounds;
                // this one sits at the screen's (0,0) corner purely to
                // receive DoUpdate calls and must never intercept a click or
                // hover meant for whatever else is at that pixel.
                return CaptureType.None;
            }

            public void Cancel()
            {
                if (_canceled) return;
                _canceled = true;
                Dispose();
            }
        }

        #endregion // 6. The FrameTicker control (nested Control subclass) - KNOWN-ISSUES #12/#13

        #region 6. The FrameTicker control (teardown) - KNOWN-ISSUES #12/#13

        /// <summary>
        /// Cancels every live FrameTicker (scroll-verify, resize-debounce,
        /// wheel-wrap-verify, spinner) and resets their pending state.
        /// Two callers: the top of every <see cref="Build"/>, and
        /// Module.Unload - the tickers are parented to the SpriteScreen,
        /// so nothing else tears them down if the module unloads while
        /// one is mid-flight. This only cancels the local ticker
        /// controls; live-phase-text state lives on the module-owned
        /// _statusBoard, which Build() re-reads fresh on every rebuild.
        /// </summary>
        public void StopLiveTickers()
        {
            _scrollVerifyTicker?.Cancel();
            _scrollVerifyTicker = null;
            _resizeDebounceTicker?.Cancel();
            _resizeDebounceTicker = null;
            _wheelWrapVerifyTicker?.Cancel();
            _wheelWrapVerifyTicker = null;
            _spinnerTicker?.Cancel();
            _spinnerTicker = null;
            _resizeSettlePending = false;
            _resizeScrollRestorePending = false;
            _resizeScrollSavedOffset = 0;
            _lastWheelEventUtc = null;
        }

        #endregion // 6. The FrameTicker control (teardown) - KNOWN-ISSUES #12/#13

        #region 3. Scroll preserve/restore/verify (continued) - KNOWN-ISSUES #12/#14/#19

        /// <summary>
        /// Writes the restore ratio to the scrollbar synchronously, using
        /// the content height mutate() already finalized. Nothing paints
        /// between mutate() returning and this write landing, so the
        /// viewport never visibly reaches a wrong position at all.
        /// </summary>
        private void ApplySavedScrollSynchronously(int savedOffset, int capturedGeneration)
        {
            if (_contentPanel == null || PanelScrollbarField == null)
            {
                return;
            }

            var capturedPanel = _contentPanel;

            // Resolved once per restore run rather than via reflection on
            // every frame - see the perf note on PanelScrollbarField. A
            // missing scrollbar degrades to today's reset-to-top.
            var scrollbar = PanelScrollbarField.GetValue(capturedPanel) as Scrollbar;
            if (scrollbar == null)
            {
                return;
            }

            bool diagEnabled = ScrollDiagEnabled;

            int contentHeight = MeasureContentHeight(capturedPanel);
            float ratio = ScrollMath.RatioForOffset(savedOffset, contentHeight, capturedPanel.Height);
            float before = scrollbar.ScrollDistance;
            scrollbar.ScrollDistance = ratio;

            if (diagEnabled)
            {
                LogScrollDiag($"{ScrollDiagTag} write writer=SyncRestore frame={ScrollDiagFrame()} before={before:0.0000} after={ratio:0.0000} contentHeight={contentHeight} savedOffset={savedOffset} generation={capturedGeneration}");
            }

            StartScrollVerify(capturedPanel, capturedGeneration, savedOffset, scrollbar);
        }

        /// <summary>
        /// Sum-free measure of a scroll-restorable panel's real content
        /// height: the furthest Bottom edge among its currently VISIBLE
        /// direct children (an invisible child - a collapsed section, a
        /// collapsed tree childFlow - contributes nothing, matching how
        /// Blish's own FlowPanel reflow already excludes invisible children
        /// from its own layout accumulation). Shared by
        /// ApplySavedScrollSynchronously and StartScrollVerify's per-frame
        /// tick so the two can never compute this differently.
        /// </summary>
        private static int MeasureContentHeight(Panel panel)
        {
            int contentHeight = 0;
            foreach (var child in panel.Children)
            {
                if (child.Visible && child.Bottom > contentHeight)
                {
                    contentHeight = child.Bottom;
                }
            }
            return contentHeight;
        }

        /// <summary>
        /// Short defensive verify after ApplySavedScrollSynchronously's
        /// write. With heights finalized synchronously at build time, this
        /// only contests one expected class of LATE write: Blish's
        /// Scrollbar.RecalculateLayout zeroes ScrollDistance whenever the
        /// content/viewport ratio changes, which can land a frame or two
        /// after our synchronous write. Exits on the first frame that
        /// confirms the write is holding, capped at ScrollVerifyMaxFrames.
        ///
        /// Any user wheel event observed since the window armed yields it
        /// immediately - the ONLY wheel-driven exit. A zero-reassert is
        /// always contested, never suppressed by wheel recency: a wheel
        /// predating the arm time is the input that produced savedOffset
        /// in the first place, so treating it as "user meant the top"
        /// would abandon their real position. The zero-reassert cap
        /// guarantees a persistent fight eventually ends.
        /// </summary>
        private void StartScrollVerify(Panel capturedPanel, int capturedGeneration, int savedOffset, Scrollbar scrollbar)
        {
            int frame = 0;
            int zeroReassert = 0;
            DateTime armedAtUtc = DateTime.UtcNow;

            if (ScrollDiagEnabled)
            {
                LogScrollDiag($"{ScrollDiagTag} verify-armed frame={ScrollDiagFrame()} savedOffset={savedOffset} generation={capturedGeneration}");
            }

            bool VerifyTick(GameTime gameTime)
            {
                bool diagEnabled = ScrollDiagEnabled;

                // A newer restore superseded this loop, Build() swapped in
                // a fresh content panel, or the panel was torn down (tab
                // switch / module unload): stop immediately rather than
                // fight the current restore or scroll a stale/disposed
                // panel.
                if (capturedGeneration != _scrollRestoreGeneration ||
                    capturedPanel != _contentPanel || capturedPanel.Parent == null)
                {
                    if (diagEnabled)
                    {
                        LogScrollDiag($"{ScrollDiagTag} verify exit reason=stale-generation frame={ScrollDiagFrame()} realFrame={frame} generation={capturedGeneration} liveGeneration={_scrollRestoreGeneration}");
                    }
                    return false;
                }

                try
                {
                    frame++;

                    // Directive C: yield hardening. Any wheel event observed
                    // since this window armed is real user input landing
                    // inside a live verify window - never contest it,
                    // regardless of what the scrollbar currently reads.
                    if (_lastWheelEventUtc.HasValue && _lastWheelEventUtc.Value >= armedAtUtc)
                    {
                        if (diagEnabled)
                        {
                            LogScrollDiag($"{ScrollDiagTag} verify exit reason=wheel-observed frame={ScrollDiagFrame()} realFrame={frame}");
                        }
                        return false;
                    }

                    int contentHeight = MeasureContentHeight(capturedPanel);
                    float target = ScrollMath.RatioForOffset(savedOffset, contentHeight, capturedPanel.Height);
                    float current = scrollbar.ScrollDistance;

                    if (current <= 0.0005f && target > 0.01f)
                    {
                        // Scrollbar reads exactly zero while our target
                        // sits well above it: always a library reset,
                        // never a genuine "user wheeled to top" - a wheel
                        // at or after arm time already exited above, and
                        // one predating it reflects the user's real
                        // pre-mutation position, which this reassert must
                        // restore. Do not add recency-only suppression
                        // here - it lets a wheel just before the mutation
                        // veto restoring a real non-top position.
                        scrollbar.ScrollDistance = target;
                        zeroReassert++;

                        if (diagEnabled)
                        {
                            LogScrollDiag($"{ScrollDiagTag} write writer=Verify/zeroReassert frame={ScrollDiagFrame()} realFrame={frame} before={current:0.0000} after={target:0.0000} contentHeight={contentHeight} bounceCount={zeroReassert}");
                        }

                        if (zeroReassert >= ScrollVerifyZeroReassertCap)
                        {
                            if (diagEnabled)
                            {
                                LogScrollDiag($"{ScrollDiagTag} verify exit reason=zero-reassert-cap-exceeded frame={ScrollDiagFrame()} realFrame={frame} bounceCount={zeroReassert}");
                            }
                            return false;
                        }
                    }
                    else if (System.Math.Abs(current - target) > 0.004f)
                    {
                        // Scrollbar reads something other than our target
                        // and it is not the zero-reset pattern above: real
                        // user scroll. Stop contesting entirely - never
                        // re-assert over legitimate user input.
                        if (diagEnabled)
                        {
                            LogScrollDiag($"{ScrollDiagTag} verify exit reason=user-scroll-detected frame={ScrollDiagFrame()} realFrame={frame} observed={current:0.0000} target={target:0.0000} contentHeight={contentHeight}");
                        }
                        return false;
                    }
                    else
                    {
                        // Matches target within tolerance: the write is
                        // holding. Exit on this first confirmed-stable
                        // frame rather than requiring a multi-frame streak -
                        // height is not still drifting (directive A), so one
                        // clean frame is sufficient evidence nothing is
                        // fighting the restore.
                        if (diagEnabled)
                        {
                            LogScrollDiag($"{ScrollDiagTag} verify exit reason=stable frame={ScrollDiagFrame()} realFrame={frame} target={target:0.0000} contentHeight={contentHeight}");
                        }
                        return false;
                    }

                    if (frame < ScrollVerifyMaxFrames)
                    {
                        return true;
                    }

                    if (diagEnabled)
                    {
                        LogScrollDiag($"{ScrollDiagTag} verify exit reason=max-frames frame={ScrollDiagFrame()} realFrame={frame} target={target:0.0000} contentHeight={contentHeight}");
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    // Reflection/layout mismatch, or the panel/scrollbar was
                    // disposed out from under us: stop verifying.
                    Logger.Warn(ex, "Scroll verify stopped by exception");
                    if (diagEnabled)
                    {
                        LogScrollDiag($"{ScrollDiagTag} verify exit reason=disposed-exception frame={ScrollDiagFrame()} realFrame={frame} error={ex.GetType().Name}");
                    }
                    return false;
                }
            }

            _scrollVerifyTicker?.Cancel();
            _scrollVerifyTicker = new FrameTicker(VerifyTick);
        }

        #endregion // 3. Scroll preserve/restore/verify (continued) - KNOWN-ISSUES #12/#14/#19

        #region 4. Wheel-wrap correction (continued) - KNOWN-ISSUES #12 (reopened)

        /// <summary>
        /// Unconditional (not diagnostics-gated) tap on the same
        /// MouseWheelScrolled event the diagnostic handler observes,
        /// recording only a timestamp. StartScrollVerify reads it to
        /// yield a live verify window the moment a wheel event lands -
        /// a real behavioral decision, so it must run regardless of
        /// ScrollDiagnosticsEnabled.
        /// </summary>
        private void OnContentWheelObserved(object sender, MouseEventArgs e)
        {
            _lastWheelEventUtc = DateTime.UtcNow;

            // Classification is unconditional (zero-allocation) - see
            // WheelDeltaSanitizer for the root cause and threshold
            // derivation. ScrollWheelValue is the same raw value the
            // diagnostic log reads.
            try
            {
                int raw = GameService.Input.Mouse.State.ScrollWheelValue;
                var classification = WheelDeltaSanitizer.Classify(raw);
                if (classification.IsWrapped)
                {
                    ApplyWheelWrapCorrection(raw, classification.IntendedDelta);
                }
            }
            catch (Exception ex)
            {
                // Defensive, matching StartScrollVerify's own precedent for
                // reflection/layout-touching scroll code (see that
                // method's own catch): this handler runs unconditionally
                // on every wheel event, not diagnostics-gated, so a
                // disposed panel/scrollbar (tab switch, module unload) or
                // a future Blish internal change must degrade to "no
                // correction this event" rather than take down the whole
                // wheel input pipeline.
                Logger.Warn(ex, "Wheel-wrap correction failed");
            }
        }

        /// <summary>
        /// Corrects the damage from a wrapped wheel delta. Blish's
        /// Scrollbar.HandleWheelScroll looks only at Math.Sign of the
        /// corrupted-negative raw delta, so it has already queued exactly
        /// one step DOWN by the time this handler runs (this handler is
        /// subscribed after Blish's own Scrollbar).
        ///
        /// Mechanism (verified against the decompiled vendored Glide):
        /// TweenerImpl.Tween registers a new tween in the by-target
        /// dictionary synchronously, before returning - so by the time
        /// this handler runs, the wrong duration-0 tween is already
        /// registered and TargetCancel finds it immediately.
        /// Tween.Cancel nulls the "ScrollDistance" lerper slot
        /// synchronously, so even an Update() that runs before removal
        /// skips the write - the wrong step never lands, not merely
        /// "canceled one frame late". That is why the
        /// cancel-then-direct-write shape is kept over a counter-tween or
        /// a deferred correction, which would add a wrong frame this
        /// mechanism does not have. (Scrollbar itself never calls
        /// TargetCancel; rapid ScrollAnimated calls overwrite each other
        /// via Tween's default overwrite parameter, an internal-only
        /// path.)
        ///
        /// A bounded defensive re-assert (StartWheelWrapVerify) still
        /// runs for a frame or two - insurance against a future
        /// Blish/Glide vendor change, not an expected failure.
        ///
        /// The stale-cached-percent hazard does not apply here: a wheel
        /// event alone never changes content or viewport height, so
        /// _scrollbarPercent is already fresh and RecalculateLayout is
        /// not needed before this write.
        /// </summary>
        private void ApplyWheelWrapCorrection(int rawIn, int intendedDelta)
        {
            if (_contentPanel == null || PanelScrollbarField == null)
            {
                return;
            }

            var scrollbar = PanelScrollbarField.GetValue(_contentPanel) as Scrollbar;
            if (scrollbar == null)
            {
                return;
            }

            // Baseline captured before touching the tween at all. Provably
            // tween-independent regardless of read order: Tween.Cancel()
            // only nulls the tween's OWN internal lerp-state slots, it
            // never writes to ScrollDistance itself (see this method's
            // MECHANISM note) - reading the baseline here first just makes
            // that independence visible in the code, not just the comment.
            float before = scrollbar.ScrollDistance;

            // Cancel Blish's own mis-signed single-step-down tween before
            // its next Update() can apply it - see this method's own doc
            // comment for why this is synchronously effective here, not a
            // no-op. Still harmless to call when none is pending (e.g. the
            // scrollbar wasn't visible/scrollable when Blish's
            // HandleWheelScroll ran, so it never queued one).
            if (GameService.Animation?.Tweener != null)
            {
                GameService.Animation.Tweener.TargetCancel(scrollbar, nameof(Scrollbar.ScrollDistance));
            }

            // Blish's per-notch convention: one wheel event moves the bar
            // by BlishScrollWheelStepPixels * MouseWheelScrollLines
            // pixels, sign-only. Read live so any positive OS
            // wheel-lines setting stays correct. Windows' "one screen at
            // a time" setting reports -1, which would flip the sign;
            // SanitizeScrollLines substitutes the documented default of 3
            // to keep this correction's direction right (Blish's own
            // arithmetic has the same defect, which we cannot fix, so
            // direction-correctness is chosen over unreachable
            // step-parity there). intendedDelta scales proportionally
            // rather than assuming a clean multiple of 120, so a
            // non-multiple value degrades gracefully.
            double notches = intendedDelta / 120.0;
            int lines = WheelDeltaSanitizer.SanitizeScrollLines(System.Windows.Forms.SystemInformation.MouseWheelScrollLines);
            int deltaPixels = (int)System.Math.Round(-notches * BlishScrollWheelStepPixels * lines);

            int contentHeight = MeasureContentHeight(_contentPanel);
            float after = ScrollMath.ApplyPixelDelta(before, deltaPixels, contentHeight, _contentPanel.Height);
            scrollbar.ScrollDistance = after;

            if (ScrollDiagEnabled)
            {
                LogScrollDiag($"{ScrollDiagTag} write writer=WheelWrapFix frame={ScrollDiagFrame()} rawIn={rawIn} intendedDelta={intendedDelta} before={before:0.0000} after={after:0.0000}");
            }

            StartWheelWrapVerify(scrollbar, after);
        }

        /// <summary>
        /// A bounded, one-shot defensive re-assert for
        /// ApplyWheelWrapCorrection's write - insurance against a future
        /// Blish/Glide vendor change, not an expected failure. Unlike
        /// StartScrollVerify's zero-reassert loop, this re-asserts at
        /// most once and yields immediately to any newer wheel event.
        /// </summary>
        private void StartWheelWrapVerify(Scrollbar scrollbar, float target)
        {
            int frame = 0;
            DateTime correctedAtUtc = _lastWheelEventUtc ?? DateTime.UtcNow;
            bool diagEnabled = ScrollDiagEnabled;

            bool VerifyTick(GameTime gameTime)
            {
                frame++;

                try
                {
                    if (_contentPanel == null || _contentPanel.Parent == null)
                    {
                        return false;
                    }

                    // A newer wheel event landed since this correction -
                    // real subsequent user input, never contest it.
                    if (_lastWheelEventUtc.HasValue && _lastWheelEventUtc.Value > correctedAtUtc)
                    {
                        return false;
                    }

                    float current = scrollbar.ScrollDistance;
                    if (System.Math.Abs(current - target) > WheelWrapVerifyEpsilon)
                    {
                        scrollbar.ScrollDistance = target;
                        if (diagEnabled)
                        {
                            LogScrollDiag($"{ScrollDiagTag} write writer=WheelWrapFix/reassert frame={ScrollDiagFrame()} before={current:0.0000} after={target:0.0000}");
                        }
                        return false;
                    }

                    return frame < WheelWrapVerifyMaxFrames;
                }
                catch (Exception ex)
                {
                    // Disposed panel/scrollbar or a layout mismatch: stop
                    // rather than risk touching torn-down state.
                    Logger.Warn(ex, "Wheel-wrap verify stopped by exception");
                    return false;
                }
            }

            _wheelWrapVerifyTicker?.Cancel();
            _wheelWrapVerifyTicker = new FrameTicker(VerifyTick);
        }

        /// <summary>
        /// Observation-only wheel handler, always observing the scrollbar
        /// after Blish's own HandleWheelScroll has run for the same event
        /// (Blish's Scrollbar subscribes first, in its constructor).
        /// Never writes to the scrollbar or influences restore/verify
        /// decisions - purely a read-and-log tap.
        /// </summary>
        private void OnScrollDiagWheelScrolled(object sender, MouseEventArgs e)
        {
            if (!ScrollDiagEnabled)
            {
                return;
            }

            var scrollbar = PanelScrollbarField != null
                ? PanelScrollbarField.GetValue(_contentPanel) as Scrollbar
                : null;

            int contentHeight = MeasureContentHeight(_contentPanel);
            int wheelValue = GameService.Input.Mouse.State.ScrollWheelValue;
            bool verifyLive = _scrollVerifyTicker != null && _scrollVerifyTicker.IsActive;

            LogScrollDiag($"{ScrollDiagTag} wheel frame={ScrollDiagFrame()} sign={System.Math.Sign(wheelValue)} raw={wheelValue} scrollDistance={(scrollbar?.ScrollDistance ?? -1f):0.0000} contentHeight={contentHeight} verifyLive={verifyLive}");
        }

        #endregion // 4. Wheel-wrap correction (continued) - KNOWN-ISSUES #12 (reopened)

        #region 1. Input rows (continued)

        /// <summary>
        /// Disposes every current item row's live controls and rebuilds
        /// them from _itemRows.
        /// Called by Build() (initial construction) and by
        /// AddItemRow/RemoveItemRow via ReflowInputRegion (row-count
        /// changes) - a full rebuild rather than a patch, matching this
        /// file's existing dispose+recreate pattern (e.g. RenderPlan
        /// disposes all of _contentPanel's children on every render rather
        /// than diffing). N is always small (a handful of rows at most), so
        /// this is not a hot path.
        /// </summary>
        private void RebuildItemRowControls(int w)
        {
            foreach (var row in _itemRows)
            {
                // SuggestionPanel is SpriteScreen-parented (never a child of
                // _inputPanel/buildPanel), so it always needs an explicit
                // Dispose() regardless of which cycle this is - same
                // reasoning the old single-_suggestionPanel field's Build()
                // cleanup always had. SuggestionPanel.Dispose() itself is
                // idempotent (`if (_disposed) return;`), so this is safe to
                // call even on a row whose SuggestionPanel was already
                // disposed by a previous rebuild this same Build() cycle.
                row.SuggestionPanel?.Dispose();

                // RowPanel, by contrast, IS a child of _inputPanel/buildPanel
                // - across a tab-switch Build() cycle it (and its own
                // children) were already torn down by ViewAdapter's own
                // "clear existing children before rebuilding" cascade before
                // this method ever runs again, which nulls a disposed
                // control's Parent (see TriggerGenerate's own "a disposed
                // control's Parent is nulled on disposal" comment). Disposing
                // it again here would be a double-Dispose on an
                // already-torn-down control; only a genuine same-cycle
                // Add/Remove reflow (ReflowInputRegion, _inputPanel still
                // live) leaves RowPanel.Parent non-null, meaning THIS row
                // genuinely still needs disposing before its replacement is
                // built.
                if (row.RowPanel != null && row.RowPanel.Parent != null)
                {
                    row.RowPanel.Dispose();
                }

                row.SuggestionPanel = null;
                row.RowPanel = null;
                row.SearchBox = null;
                row.QtyInput = null;
            }

            for (int i = 0; i < _itemRows.Count; i++)
            {
                CreateItemRowControls(_itemRows[i], i, w);
            }
        }

        /// <summary>
        /// One input row's controls: search box + qty, a Remove button
        /// (gw2e's own 2+-rows gate), and on the last row only an Add
        /// button - attached to the last row rather than its own strip
        /// row so the single-row case keeps the exact original layout.
        /// </summary>
        private void CreateItemRowControls(ItemRowState row, int index, int w)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(w, RowHeight),
                Location = new Point(0, index * RowHeight),
                Parent = _inputPanel
            };
            row.RowPanel = rowPanel;

            var searchBox = new AutocompleteTextBox()
            {
                PlaceholderText = "Search items...",
                Text = row.ItemName ?? "",
                Size = new Point(200, 28),
                Location = new Point(0, 3),
                Parent = rowPanel
            };
            row.SearchBox = searchBox;

            var suggestionPanel = new SuggestionPanel(searchBox, _itemSearchProvider);
            suggestionPanel.ItemSelected += (_, args) =>
            {
                row.ItemId = args.ItemId;
                row.ItemName = args.Name;
            };
            row.SuggestionPanel = suggestionPanel;

            // A pick is the only thing that resolves a row, so editing the
            // box afterwards has to drop that resolution - otherwise the
            // box reads one item while Generate still plans the previously
            // picked one. Subscribed after SuggestionPanel so a pick's own
            // Text write clears here first and is re-resolved by the
            // ItemSelected handler above, in that order.
            searchBox.TextChanged += (_, __) =>
            {
                if (!ItemRowSelection.SelectionIsStale(row.ItemId, row.ItemName, searchBox.Text))
                {
                    return;
                }

                row.ItemId = null;
                row.ItemName = null;
            };

            new Label()
            {
                Text = "Qty:",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(210, 7),
                Parent = rowPanel
            };

            var qtyInput = new TextBox()
            {
                Text = string.IsNullOrEmpty(row.QuantityText) ? "1" : row.QuantityText,
                Size = new Point(50, 28),
                Location = new Point(240, 3),
                Parent = rowPanel
            };
            qtyInput.TextChanged += (_, __) => row.QuantityText = qtyInput.Text;
            row.QtyInput = qtyInput;

            int nextX = 300;
            if (ItemRowRequestBuilder.CanRemoveRow(_itemRows.Count))
            {
                var removeButton = new StandardButton()
                {
                    Text = "-",
                    Size = new Point(24, 24),
                    Location = new Point(nextX, 3),
                    Parent = rowPanel
                };
                removeButton.Click += (_, __) => RemoveItemRow(row);
                nextX += 24 + 8;
            }

            if (index == _itemRows.Count - 1)
            {
                var addButton = new StandardButton()
                {
                    Text = "+",
                    Size = new Point(24, 24),
                    Location = new Point(nextX, 3),
                    Parent = rowPanel
                };
                addButton.Click += (_, __) => AddItemRow();
            }
        }

        private void AddItemRow()
        {
            _itemRows.Add(new ItemRowState());
            ReflowInputRegion();
        }

        private void RemoveItemRow(ItemRowState row)
        {
            if (!ItemRowRequestBuilder.CanRemoveRow(_itemRows.Count)) return;

            int index = _itemRows.IndexOf(row);
            if (index < 0) return;

            row.SuggestionPanel?.Dispose();
            row.RowPanel?.Dispose();
            _itemRows.RemoveAt(index);
            ReflowInputRegion();
        }

        /// <summary>
        /// Rebuilds the item-row controls and repositions every fixed
        /// element below them (controls/status/separator/content) after
        /// the row count changes - the Add/Remove counterpart to
        /// OnPanelResized's own width-driven repositioning. Row add/remove
        /// never changes width, only the top strip's total height, so this
        /// mirrors OnPanelResized's heightChanged branch (scroll-preserve)
        /// without needing its widthChanged branch (no relayout replay).
        /// </summary>
        private void ReflowInputRegion()
        {
            if (_buildPanel == null || _inputPanel == null) return;

            int w = _buildPanel.ContentRegion.Width;
            int h = _buildPanel.ContentRegion.Height;
            var layout = ComputeTopRegionLayout(_itemRows.Count);

            int savedScrollOffset = _contentPanel?.VerticalScrollOffset ?? 0;
            int previousContentHeight = _contentPanel?.Height ?? 0;

            _inputPanel.Size = new Point(w, layout.InputPanelHeight);
            RebuildItemRowControls(w);

            _controlsPanel.Location = new Point(0, layout.ControlsRowY);
            _statusLabel.Location = new Point(0, layout.StatusRowY);
            _separator.Location = new Point(0, layout.SeparatorY);
            _contentPanel.Location = new Point(0, layout.ContentY);
            _contentPanel.Size = new Point(w, h - layout.TopRegionHeight);

            if (_currentPlan != null && _contentPanel.Height != previousContentHeight)
            {
                PreserveScrollAcrossResize(savedScrollOffset, _contentPanel.Height);

                // Row add/remove is a discrete one-shot action, not a
                // continuous drag - ResizeSettleStep's own debounced follow-
                // up verify (armed only while further drag ticks keep
                // resetting _lastResizeEventUtc) would never naturally fire
                // for it. Arm the settle-time verify directly here instead,
                // the same way ResizeSettleStep itself does right after a
                // drag settles - see PreserveScrollAcrossResize's own doc
                // comment for why a late Blish-internal scrollbar reset
                // still needs contesting even for a single height change.
                if (_resizeScrollRestorePending)
                {
                    _resizeScrollRestorePending = false;
                    StartResizeScrollVerify();
                }
            }
        }

        #endregion // 1. Input rows (continued)

        #region General: view construction (Build) - wires every section/handler together

        // Wires Input Rows (1), the wheel handlers (3/4), and the resize
        // handler (5) together onto the freshly built controls; not itself
        // one of the 11 - see m38-a1-architecture.md S3.
        public void Build(Container buildPanel)
        {
            // Screen-parented popups from the previous build cycle (one
            // per item row) are cleaned up by RebuildItemRowControls below, which
            // every row already routes through - no separate loop needed
            // here.

            // Cleanup for any leftover tickers from the previous build
            // cycle, plus their pending state - see StopLiveTickers,
            // which Module.Unload also calls.
            StopLiveTickers();

            _buildPanel = buildPanel;
            int w = buildPanel.ContentRegion.Width;

            // Gw2e's own initial state is one empty row
            // (`e.recipes = [{id: null, amount: 1}]`) - see _itemRows' own
            // doc comment. Only ever seeded once; every later Build() call
            // (tab switch) reuses whatever the session already has.
            if (_itemRows.Count == 0)
            {
                _itemRows.Add(new ItemRowState());
            }

            var layout = ComputeTopRegionLayout(_itemRows.Count);

            // Input rows: search box + quantity per requested item.
            _inputPanel = new Panel()
            {
                Size = new Point(w, layout.InputPanelHeight),
                Location = new Point(0, InputRowY),
                Parent = buildPanel
            };
            RebuildItemRowControls(w);

            // Controls row: checkbox + generate button
            _controlsPanel = new Panel()
            {
                Size = new Point(w, RowHeight),
                Location = new Point(0, layout.ControlsRowY),
                Parent = buildPanel
            };

            _ownMaterialsCheckbox = new Checkbox()
            {
                Text = "Use Own Materials",
                Checked = _useOwnMaterials,
                Location = new Point(0, 7),
                Parent = _controlsPanel
            };
            // CheckedChanged is wired further down, AFTER
            // _valueOwnMaterialsCheckbox is constructed - the handler
            // dereferences that field unconditionally, and wiring it
            // earlier would leave a live handler that NREs on the first
            // click if any intervening construction throws.

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
                SetStatus(SettingsChangedStatus);
            };

            // Inline per-plan toggle, disabled (not hidden) when Use Own
            // Materials is off - its effect is inert without a snapshot
            // driving reduction. Placed after the price-basis dropdown,
            // clear of the right-anchored Generate button even at minimum
            // window width.
            _valueOwnMaterialsCheckbox = new Checkbox()
            {
                Text = "Value Own Materials",
                Checked = _valueOwnMaterials,
                Enabled = _useOwnMaterials,
                Location = new Point(350, 7),
                Parent = _controlsPanel,
                // With this ON, owned materials are priced at market rate
                // up front - the plan may tell you to buy ingredients you
                // already have when a different option is cheaper fresh.
                // The tooltip also mentions the 15% force-buy guard and
                // the MaterialOpportunityCost deduction, both of which
                // change numbers this plan displays.
                BasicTooltipText = "Compare recipe options at fresh market prices, as if you owned nothing - may recommend buying materials you already have instead of using them, if a different option is cheaper. Also force-buys materials where buying beats crafting by more than 15%, and deducts owned materials' sell value from Crafting Profit. Off: always uses what you already own first, treated as free."
            };
            _valueOwnMaterialsCheckbox.CheckedChanged += (_, e) =>
            {
                _valueOwnMaterials = e.Checked;
                SetStatus(SettingsChangedStatus);
            };

            // Wired here, after _valueOwnMaterialsCheckbox is fully
            // constructed - see the comment at that construction site.
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
                Location = new Point(0, layout.StatusRowY),
                Parent = buildPanel
            };

            // Static separator between controls and content
            _separator = new Panel()
            {
                Size = new Point(w - RightEdgePadding, 2),
                Location = new Point(0, layout.SeparatorY),
                BackgroundColor = new Color(180, 180, 180),
                Parent = buildPanel
            };

            // Scrollable content area - full width so scrollbar sits at the window edge.
            // Children use (Width - RightEdgePadding) to keep content clear of the scrollbar.
            _contentPanel = new FlowPanel()
            {
                Size = new Point(w, buildPanel.ContentRegion.Height - layout.TopRegionHeight),
                Location = new Point(0, layout.ContentY),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                CanScroll = true,
                Parent = buildPanel
            };

            // Unconditional wheel-recency tracking StartScrollVerify
            // depends on, plus the diagnostic-only tap. _contentPanel is
            // a fresh instance every Build(), so there is nothing stale
            // to unsubscribe.
            _contentPanel.MouseWheelScrolled += OnContentWheelObserved;
            _contentPanel.MouseWheelScrolled += OnScrollDiagWheelScrolled;

            // Subscribe to resize
            buildPanel.Resized += OnPanelResized;

            // The fresh _statusLabel starts on "Ready", only correct for
            // "nothing generated this session". Every rebuild consults
            // the module-owned _statusBoard directly rather than any
            // instance field a torn-down panel could leave stale. Three
            // cases:
            //   in-flight            -> arm a fresh ticker, which
            //                            immediately renders the board's
            //                            current phase text (no waiting for
            //                            the next phase event).
            //   finished, has status -> render that final text directly -
            //                            this also fixes the pre-existing
            //                            quirk where a rebuilt view showed
            //                            "Ready" despite an already-
            //                            completed plan.
            //   nothing yet          -> leave "Ready" as set above.
            // This MUST run after _contentPanel is reassigned to the new
            // FlowPanel: RenderFromBoard bails when _contentPanel is null
            // or disposed, and until the reassignment it still holds the
            // previous build cycle's already-disposed panel. The
            // not-in-flight branch calls RenderFromBoard directly rather
            // than re-deriving its render ladder inline - one place only.
            var boardSnapshot = _statusBoard.Snapshot();
            if (boardSnapshot.InFlight)
            {
                ArmSpinnerTicker(boardSnapshot.Sequence);
            }
            else
            {
                RenderFromBoard(boardSnapshot);
            }

            // The DOMINANT restore-render path: ApplyRestoredPlan runs at
            // module load, so a restored plan is committed to
            // _currentPlan and only actually rendered on the tab's first
            // Build() - this call. Unguarded, a degraded plan.json would
            // throw here, escape into Blish's view construction, and
            // re-throw on every visit since nothing cleared _currentPlan.
            // Shares RollBackFailedPlanRender with ApplyRestoredPlan's
            // live-tab branch.
            if (_currentPlan != null)
            {
                _lastRenderedWidth = w;
                try
                {
                    RenderPlan(_currentPlan);
                }
                catch (Exception ex)
                {
                    RollBackFailedPlanRender(ex, "on tab visit");
                }
            }
        }

        #endregion // General: view construction (Build) - wires every section/handler together

        #region 5. Resize relayout (continued) - KNOWN-ISSUES #13/#19
        private void OnPanelResized(object sender, ResizedEventArgs e)
        {
            var container = (Container)sender;
            int w = container.ContentRegion.Width;
            int h = container.ContentRegion.Height;

            // Capture the content panel's absolute scroll offset
            // (pixels) and height BEFORE either changes below - see
            // PreserveScrollAcrossResize's doc comment for why this must
            // happen pre-mutation.
            int savedScrollOffset = _contentPanel?.VerticalScrollOffset ?? 0;
            int previousContentHeight = _contentPanel?.Height ?? 0;

            // Update widths of layout panels. Top-strip controls keep their
            // pre-existing direct updates - these were
            // never part of the dispose+rebuild problem the relayout
            // registry below replaces. The input strip is N rows
            // (_itemRows.Count) rather than a fixed one, so its own and
            // every row panel's width need updating too, and the Y offsets
            // below it come from the same ComputeTopRegionLayout formula
            // Build()/ReflowInputRegion use rather than fixed constants.
            var layout = ComputeTopRegionLayout(_itemRows.Count);
            _inputPanel.Size = new Point(w, layout.InputPanelHeight);
            foreach (var row in _itemRows)
            {
                if (row.RowPanel != null)
                {
                    row.RowPanel.Size = new Point(w, RowHeight);
                }
            }
            _controlsPanel.Size = new Point(w, RowHeight);
            _controlsPanel.Location = new Point(0, layout.ControlsRowY);
            _generateButton.Location = new Point(w - 120 - RightEdgePadding, 3);
            _statusLabel.Location = new Point(0, layout.StatusRowY);
            _separator.Size = new Point(w - RightEdgePadding, 2);
            _separator.Location = new Point(0, layout.SeparatorY);
            _contentPanel.Location = new Point(0, layout.ContentY);
            _contentPanel.Size = new Point(w, h - layout.TopRegionHeight);

            bool widthChanged = w != _lastRenderedWidth;
            bool heightChanged = _contentPanel.Height != previousContentHeight;

            // A
            // height-changing drag tick (dragging the window's bottom edge
            // or a corner) resets Blish's own scrollbar to top one real
            // frame later - see PreserveScrollAcrossResize's doc comment
            // for the vendor-source-grounded mechanism. Gated on
            // _currentPlan so an empty content panel never does this work;
            // the offset/height capture above stays unconditional since it
            // is two cheap property reads either way.
            if (_currentPlan != null && heightChanged)
            {
                PreserveScrollAcrossResize(savedScrollOffset, _contentPanel.Height);
            }

            // Live in-place relayout, every real drag tick - no
            // dispose+rebuild, no debounce wait. Perf guard: skip entirely
            // when the width genuinely did not change (e.g. a height-only
            // resize, or a duplicate event) so an idle window never pays
            // for a registry walk.
            if (_currentPlan != null && widthChanged)
            {
                _lastRenderedWidth = w;

                int panelWidth = w - RightEdgePadding;
                ReplayRelayout(panelWidth);
            }

            // The trailing settle pass (re-ellipsis, a defensive
            // relayout replay, and now the resize-scroll verify armed by
            // PreserveScrollAcrossResize above) must be scheduled whenever
            // EITHER dimension changed. Previously this ticker was
            // scheduled only on a width change, which silently starved a
            // pure height-only drag (e.g. dragging just the bottom edge) of
            // any settle handling at all - exactly the drag shape the live
            // regression was found under. Bounded to a single in-flight
            // ticker (_resizeSettlePending) so repeated ticks during a drag
            // just extend _lastResizeEventUtc rather than spawning parallel
            // tickers - see ResizeSettleStep.
            if (_currentPlan != null && (widthChanged || heightChanged))
            {
                _lastResizeEventUtc = DateTime.UtcNow;

                if (!_resizeSettlePending)
                {
                    _resizeSettlePending = true;
                    _resizeDebounceTicker?.Cancel();
                    _resizeDebounceTicker = new FrameTicker(ResizeSettleStep);
                }
            }
        }

        /// <summary>
        /// Per-tick counterpart to ApplySavedScrollSynchronously
        /// for a resize drag that changes the content panel's viewport
        /// HEIGHT, as opposed to a content rebuild (PreserveScrollAcross's
        /// case). Root cause, confirmed by decompiling the vendor assembly
        /// (packages/BlishHUD.1.3.0/lib/net472/Blish HUD.exe,
        /// Blish_HUD.Controls.Scrollbar and Panel):
        ///
        /// Scrollbar.RecalculateLayout caches
        /// _scrollbarPercent = ContentRegion.Height / containerLowestContent
        /// and zeroes ScrollDistance (and, via UpdateAssocContainer,
        /// VerticalScrollOffset) whenever that ratio differs from the
        /// previously cached value. RecalculateLayout runs from two places:
        /// (1) synchronously, nested inside Panel's own "Height"
        /// PropertyChanged handler (UpdatePanelScrollbarOnOwnPropertyChanged
        /// sets _panelScrollbar.Height, itself a Control.Height write that
        /// invalidates/recalculates the scrollbar) - but .NET's
        /// PropertyChanged event fires BEFORE Control.Size's own
        /// OnPropertyChanged("Height", invalidateLayout: true) call to
        /// Invalidate(), so this nested call runs before Panel's own
        /// RecalculateLayout has refreshed ContentRegion for the new size
        /// and reads the STALE (pre-resize) ContentRegion.Height, seeing no
        /// change; and (2) once every real engine frame, unconditionally,
        /// from Scrollbar.DoUpdate's own Invalidate() call - by the time
        /// THAT runs, ContentRegion.Height has already been refreshed (the
        /// panel's own RecalculateLayout already ran synchronously earlier
        /// in the same Height-setter chain), so it now sees a genuine
        /// change and resets. Net effect: the reset lands on a later real
        /// frame - typically the next one - not synchronously inside this
        /// tick's Size write. This is the same delayed-reset window
        /// ApplySavedScrollSynchronously's class doc already describes for
        /// rebuilds (StartScrollVerify exists there for exactly this
        /// reason).
        ///
        /// A write here keeps the visible position correct for the
        /// remainder of THIS tick (no flash mid-drag, matching directive
        /// B's zero-flash goal); OnPanelResized separately arms a bounded
        /// verify window at drag SETTLE (ResizeSettleStep), not per tick,
        /// to contest that trailing later-frame reset once the drag stops
        /// producing new ticks - see StartResizeScrollVerify. A per-tick
        /// verify window was deliberately not used: it would spawn (or
        /// cancel-and-replace) a FrameTicker on every single drag frame,
        /// which is the "spam" the task explicitly ruled out, and the
        /// per-tick synchronous write already keeps each tick visually
        /// correct without one.
        /// </summary>
        private void PreserveScrollAcrossResize(int savedOffsetPx, int newContentPanelHeight)
        {
            if (_contentPanel == null || PanelScrollbarField == null || savedOffsetPx <= 0)
            {
                return;
            }

            var scrollbar = PanelScrollbarField.GetValue(_contentPanel) as Scrollbar;
            if (scrollbar == null)
            {
                return;
            }

            // Force the scrollbar to resolve its own cached _scrollbarPercent
            // (ContentRegion.Height / containerLowestContent) against the
            // ALREADY-fresh ContentRegion.Height _contentPanel.Size just set,
            // BEFORE writing our restore ratio below. Skipping this call
            // would make our own write below self-defeating: Scrollbar.
            // ScrollDistance's setter always calls Invalidate(), and on a
            // pure height-only tick nothing else has touched the scrollbar
            // yet this tick, so _scrollbarPercent is still stale (last
            // refreshed against the OLD height); OUR write would then be the
            // first thing to trigger Scrollbar.RecalculateLayout against the
            // new ratio, which would detect the just-changed percent and
            // reset ScrollDistance back to 0 synchronously, inside the same
            // statement, undoing our own write before this method even
            // returns. Calling RecalculateLayout directly (bypassing
            // Control.UpdateLayout's once-per-LayoutState guard - see that
            // guard's own doc comment) lets this expected reset happen NOW,
            // harmlessly (nothing paints between these two statements), so
            // the restore write immediately below is the one that actually
            // sticks: _scrollbarPercent is stable by then, so ScrollDistance's
            // own cascading RecalculateLayout finds no further change to
            // react to. (A rebuild does not need this: PreserveScrollAcross's
            // mutate() churns through many of _contentPanel's own direct
            // children - each write reaching Panel.UpdateContentRegionBounds
            // - which already forces this same stale-to-fresh transition
            // organically before ApplySavedScrollSynchronously's write runs.
            // A pure height-only resize tick has no such churn: ReplayRelayout
            // does not even run when only height changed, since it is gated
            // on widthChanged.)
            scrollbar.RecalculateLayout();

            int contentHeight = MeasureContentHeight(_contentPanel);
            float ratio = ScrollMath.RatioForOffset(savedOffsetPx, contentHeight, newContentPanelHeight);
            float before = scrollbar.ScrollDistance;
            scrollbar.ScrollDistance = ratio;

            // Remember the last known-good pre-tick offset so the
            // settle-time verify (StartResizeScrollVerify) restores the
            // user's real position even if this was not the final tick of
            // the drag - see the field comment on _resizeScrollSavedOffset.
            _resizeScrollRestorePending = true;
            _resizeScrollSavedOffset = savedOffsetPx;

            if (ScrollDiagEnabled)
            {
                LogScrollDiag($"{ScrollDiagTag} write writer=ResizePreserve frame={ScrollDiagFrame()} before={before:0.0000} after={ratio:0.0000} contentHeight={contentHeight} savedOffset={savedOffsetPx} newHeight={newContentPanelHeight}");
            }
        }

        /// <summary>
        /// Arms StartScrollVerify's existing bounded window once,
        /// at resize-drag settle, using the last known-good offset a
        /// resize tick captured via PreserveScrollAcrossResize. Reuses
        /// StartScrollVerify unmodified, so the existing wheel-yield,
        /// zero-reassert-cap, and generation-staleness semantics all apply
        /// unchanged - see that method's doc comment. Deliberately called
        /// only from ResizeSettleStep (once per settled drag), never per
        /// tick.
        /// </summary>
        private void StartResizeScrollVerify()
        {
            if (_contentPanel == null || PanelScrollbarField == null || _resizeScrollSavedOffset <= 0)
            {
                return;
            }

            var scrollbar = PanelScrollbarField.GetValue(_contentPanel) as Scrollbar;
            if (scrollbar == null)
            {
                return;
            }

            int capturedGeneration = ++_scrollRestoreGeneration;
            StartScrollVerify(_contentPanel, capturedGeneration, _resizeScrollSavedOffset, scrollbar);
        }

        /// <summary>
        /// Replays every registered relayout closure at the given
        /// panelWidth - position/width writes on already-existing controls
        /// only, never a MeasureString call, never a Height change (see the
        /// _relayoutActions field comment). Wrapped in the vendor
        /// SuspendLayout/ResumeLayout pair (m2 risk 2): resizing a row
        /// Panel's Width fires its own Resized event, which FlowPanel wires
        /// to a full sibling reflow of its parent on every single write:
        /// for a long shopping list or deep tree, replaying dozens of
        /// per-row closures in a single tick would otherwise trigger that
        /// many redundant reflow passes in the same frame (m2's O(rows^2)
        /// comparison-cost risk). SuspendLayout on _contentPanel propagates
        /// down (Blish's own IsLayoutSuspended check walks the parent
        /// chain), so every nested FlowPanel's reflow this tick is
        /// deferred; ResumeLayout(false) does not force it back
        /// synchronously - Blish's own per-frame Control.Update ->
        /// UpdateLayout call resolves any still-Invalidated FlowPanel
        /// automatically on the very next real frame, so nothing is lost,
        /// only coalesced. Since these writes only ever touch Width/X (row
        /// heights stay fixed), the coalesced reflow is a no-op
        /// for vertical position anyway - SingleTopToBottom flow positions
        /// children from cumulative Height, not Width.
        ///
        /// PERF CAVEAT: this replaces a ONE-TIME
        /// dispose+rebuild 150ms after the drag settled with a full replay
        /// of _relayoutActions on EVERY real drag frame - a genuine change
        /// in perf character, not just a different trigger. The mitigation
        /// above is reasoned, not measured: no live drag-resize check on a
        /// large, fully-expanded plan (deep tree + long shopping list) has
        /// been performed against a running Blish instance. If this ever
        /// needs tightening, look here first.
        /// </summary>
        private void ReplayRelayout(int panelWidth)
        {
            if (_contentPanel == null || _relayoutActions.Count == 0) return;

#if DEBUG
            // Invariant (KNOWN-ISSUES #13): a pure width/text
            // relayout must never touch scroll position. DEBUG-only (reuses
            // the same cached PanelScrollbarField reflection handle the
            // scroll-restore machinery already resolved once) so this costs
            // nothing in Release builds; a violation here would mean some
            // relayout closure reached into the scrollbar, which no closure
            // in this file is supposed to do.
            var debugScrollbar = PanelScrollbarField?.GetValue(_contentPanel) as Scrollbar;
            float debugScrollBefore = debugScrollbar?.ScrollDistance ?? -1f;
#endif

            _contentPanel.SuspendLayout();
            try
            {
                foreach (var relayout in _relayoutActions)
                {
                    relayout(panelWidth);
                }
            }
            catch (Exception ex)
            {
                // Unlike the settle pass (already inside a try/catch-guarded
                // FrameTicker step), this runs synchronously and directly
                // off Blish's own Resized event - an uncaught exception here
                // would propagate into the library's event dispatch on
                // every remaining drag tick, not just degrade this one
                // resize. A stale-control edge case (e.g. Build() ran again
                // mid-drag, disposing the very controls a closure captured)
                // must degrade to "this tick's relayout is incomplete", not
                // take down the resize interaction.
                Logger.Warn(ex, "Relayout tick failed partway through; some controls may be stale until the next resize or rebuild");
            }
            finally
            {
                _contentPanel.ResumeLayout(false);
            }

#if DEBUG
            if (debugScrollbar != null && debugScrollbar.ScrollDistance != debugScrollBefore)
            {
                Logger.Warn(
                    "M33 C2b invariant violated: a relayout closure changed the scrollbar position (before={0:0.0000} after={1:0.0000}) - relayout must be scroll-neutral.",
                    debugScrollBefore, debugScrollbar.ScrollDistance);
            }
#endif
        }

        /// <summary>
        /// The settle-only text-measurement pass. Every relayout
        /// closure already ran (and re-ran) synchronously on every drag
        /// tick via ReplayRelayout; this only re-runs the 3 LabelHelpers.EllipsizeToWidth
        /// call sites' MEASURE work (Used Materials, Shopping List, Tree row
        /// names), since MeasureString is comparatively expensive to run on
        /// every tick across a long list/deep tree and the visible cost of
        /// deferring it (truncated text unchanged mid-drag, corrected once
        /// the drag settles) is small. Neither
        /// this pass nor the defensive ReplayRelayout repeat below ever
        /// changes a row's Height, so - unlike the settle rebuild
        /// this replaces - nothing in RunReellipsis/ReplayRelayout can
        /// perturb scroll position; no PreserveScrollAcross wrapper is
        /// needed around them. This method also arms the resize
        /// drag's single settle-time scroll-verify window, if a
        /// height-changing tick during the drag needs one - see
        /// StartResizeScrollVerify and _resizeScrollRestorePending.
        /// </summary>
        private bool ResizeSettleStep(GameTime gameTime)
        {
            // The view may have been unloaded (tab switched away, module
            // disabled) while this was pending - nothing to render into.
            if (_contentPanel == null || _contentPanel.Parent == null)
            {
                _resizeSettlePending = false;
                _resizeScrollRestorePending = false;
                return false;
            }

            if ((DateTime.UtcNow - _lastResizeEventUtc).TotalMilliseconds < ResizeDebounceMs)
            {
                return true;
            }

            _resizeSettlePending = false;

            try
            {
                // Re-read the panel width fresh rather than trust whatever w
                // was captured by the resize tick that started this ticker -
                // only the width at the moment the drag actually settled
                // matters.
                int panelWidth = _contentPanel.Width - RightEdgePadding;
                RunReellipsis(panelWidth);
                // Defensive correctness net (m2 4.2): a single extra
                // position-only replay at the final settled width, in case
                // any per-tick relayout closure was ever skipped or landed
                // on a stale intermediate width. Cheap - no MeasureString.
                ReplayRelayout(panelWidth);
            }
            catch (Exception ex)
            {
                // The content panel was disposed between the last resize tick
                // and the debounce firing (e.g. Build() ran again for a tab
                // reload mid-drag). Degrade silently: whichever Build() call
                // is current already rendered fresh content at its own width.
                Logger.Warn(ex, "Resize settle pass skipped; content panel unavailable");
            }

            // Bounded to a single window per settled drag (not per
            // tick) - see PreserveScrollAcrossResize's doc comment for why
            // one settle-time window is sufficient to contest the trailing
            // Blish-internal reset.
            if (_resizeScrollRestorePending)
            {
                _resizeScrollRestorePending = false;
                StartResizeScrollVerify();
            }

            return false;
        }

        /// <summary>
        /// Replays every registered re-ellipsis closure - see
        /// ResizeSettleStep and the _reellipsisActions field comment.
        /// </summary>
        private void RunReellipsis(int panelWidth)
        {
            foreach (var reellipsis in _reellipsisActions)
            {
                reellipsis(panelWidth);
            }
        }

        #endregion // 5. Resize relayout (continued) - KNOWN-ISSUES #13/#19

        #region 2. Generate orchestration (continued)
        private void OnOwnMaterialsToggled(object sender, CheckChangedEvent e)
        {
            if (_suppressToggle) return;

            bool newValue = e.Checked;

            if (_currentPlan != null)
            {
                // Show modal confirmation before regenerating
                _useOwnMaterials = newValue;
                _ownMaterialsCheckbox.Enabled = false;
                // Keep the Value Own Materials
                // checkbox's Enabled state in lockstep with the optimistic
                // _useOwnMaterials value at every point it changes here -
                // its own Checked value is preserved either way, only
                // whether it can be clicked follows Use Own Materials.
                _valueOwnMaterialsCheckbox.Enabled = _useOwnMaterials;
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
                        _valueOwnMaterialsCheckbox.Enabled = _useOwnMaterials;
                    },
                    confirmText: "Regenerate");
                return;
            }

            _useOwnMaterials = newValue;
            _valueOwnMaterialsCheckbox.Enabled = _useOwnMaterials;

            // Only reached with no plan on screen (the branch above
            // regenerates behind a confirm), so nothing is being made
            // stale here - but the toggle still only takes effect on the
            // next Generate, and saying so beats leaving "Ready" up.
            SetStatus(SettingsChangedStatus);
        }

        /// <summary>
        /// Generate's entry point. Rows the user typed a full item name
        /// into but never picked from the suggestion list carry no item id;
        /// they are resolved against the search provider here, before
        /// GenerateFromResolvedRows decides whether anything is selected at
        /// all. The resolution await lives in this thin wrapper rather than
        /// inside GenerateFromResolvedRows because IItemSearchProvider may
        /// complete asynchronously and Blish's host installs no
        /// SynchronizationContext - everything after such an await would
        /// otherwise run on a ThreadPool thread, and the generate body
        /// touches controls from its first line. The marshal hop puts it
        /// back on the main thread; two overlapping calls are handled the
        /// way they always were, by _generateSequence.
        /// </summary>
        private async Task TriggerGenerate()
        {
            var pending = CollectUnresolvedTypedRows();
            if (pending.Count > 0)
            {
                var matches = await FindTypedRowMatchesAsync(pending);
                MainThreadMarshal.Run(() =>
                {
                    // Torn down while the search was in flight (tab
                    // switched away, module unloading) - nothing to
                    // generate into, same bail every other deferred
                    // callback in this file takes.
                    if (_contentPanel == null || _contentPanel.Parent == null) return;

                    AdoptTypedRowMatches(matches);
                    _ = GenerateFromResolvedRows();
                });
                return;
            }

            await GenerateFromResolvedRows();
        }

        /// <summary>
        /// The rows with search text but no resolved item, snapshotted with
        /// their text on the main thread so the async resolution pass never
        /// reads a Blish control off-thread.
        /// </summary>
        private List<(ItemRowState Row, string Text)> CollectUnresolvedTypedRows()
        {
            var pending = new List<(ItemRowState Row, string Text)>();
            if (_itemSearchProvider == null)
            {
                return pending;
            }

            foreach (var row in _itemRows)
            {
                if (row.ItemId.HasValue)
                {
                    continue;
                }

                string text = row.SearchBox?.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                pending.Add((row, text.Trim()));
            }

            return pending;
        }

        /// <summary>
        /// Looks up each typed row name, keeping only exact matches, so
        /// Generate works for someone who typed the whole name and never
        /// opened the suggestion list. A partial name stays unresolved
        /// rather than adopting whatever ranked first, and a failing search
        /// resolves nothing - both land on the "pick an item from the
        /// suggestion list" status. Runs off the main thread after its
        /// first await, so it only reads its own snapshot and never touches
        /// row state; adoption is AdoptTypedRowMatches' job.
        /// </summary>
        private async Task<List<(ItemRowState Row, string Text, ItemSearchResult Match)>> FindTypedRowMatchesAsync(
            IReadOnlyList<(ItemRowState Row, string Text)> pending)
        {
            var matches = new List<(ItemRowState Row, string Text, ItemSearchResult Match)>(pending.Count);
            foreach (var entry in pending)
            {
                IReadOnlyList<ItemSearchResult> results;
                try
                {
                    results = await _itemSearchProvider.SearchAsync(
                        entry.Text, TypedNameSearchResults, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Item search failed while resolving a typed row name");
                    continue;
                }

                var match = ItemRowSelection.FindExactNameMatch(results, entry.Text);
                if (match != null)
                {
                    matches.Add((entry.Row, entry.Text, match));
                }
            }

            return matches;
        }

        /// <summary>
        /// Commits the typed-name matches on the main thread, re-checking
        /// each row against what it holds NOW: the user can pick a
        /// suggestion or keep typing while the search is in flight, and
        /// neither may be overwritten by a result that describes the older
        /// text - that is the same stale-selection bug the search box's own
        /// TextChanged handler exists to prevent.
        /// </summary>
        private void AdoptTypedRowMatches(IReadOnlyList<(ItemRowState Row, string Text, ItemSearchResult Match)> matches)
        {
            foreach (var entry in matches)
            {
                if (entry.Row.ItemId.HasValue)
                {
                    continue;
                }

                if (!ItemRowSelection.NamesMatch(entry.Row.SearchBox?.Text, entry.Text))
                {
                    continue;
                }

                entry.Row.ItemId = entry.Match.ItemId;
                entry.Row.ItemName = entry.Match.Name;
            }
        }

        private bool AnyRowHasSearchText()
        {
            foreach (var row in _itemRows)
            {
                if (!string.IsNullOrWhiteSpace(row.SearchBox?.Text))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task GenerateFromResolvedRows()
        {
            // Gather every
            // row's selection + quantity into the request list the
            // pipeline needs. Per-row quantity validation mirrors the
            // old single-quantity-box behavior exactly (invalid/blank/
            // &lt;1 silently corrected to 1, with a user-visible notice) -
            // just applied once per row instead of once total.
            bool anyQtyInvalid = false;
            var rowInputs = new List<ItemRowRequestBuilder.RowInput>(_itemRows.Count);
            // Folded together with the label-part collection
            // below (previously a separate foreach over the same _itemRows)
            // now that both need nothing from each other but this loop's own
            // per-row qty correction - see RequestLabelFormatter's own doc
            // comment for why the label itself is capped.
            var labelParts = new List<string>(_itemRows.Count);
            foreach (var row in _itemRows)
            {
                bool qtyInvalid = !int.TryParse(row.QtyInput?.Text, out int qty) || qty < 1;
                if (qtyInvalid)
                {
                    qty = 1;
                    if (row.QtyInput != null) row.QtyInput.Text = "1";
                    anyQtyInvalid = true;
                }
                row.QuantityText = qty.ToString();
                rowInputs.Add(new ItemRowRequestBuilder.RowInput(row.ItemId, row.QuantityText));

                // Best-effort "name x quantity[, name x quantity...]"
                // label (e.g. "Orrax Manifested x1") for the pipeline's rich
                // ModuleLog lines - see
                // CraftingPlanPipeline.GenerateStructuredAsync's requestLabel
                // parameter doc comment. Mirrors ItemRowRequestBuilder.
                // Build's own row.ItemId.HasValue filter so this stays in the
                // same order/count as requestItems, using the name the
                // row's own search selection already resolved (no extra
                // network round trip).
                if (!row.ItemId.HasValue) continue;
                string name = string.IsNullOrEmpty(row.ItemName) ? "Unknown Item" : row.ItemName;
                labelParts.Add($"{name} x{row.QuantityText}");
            }

            var requestItems = ItemRowRequestBuilder.Build(rowInputs);
            if (requestItems.Count == 0)
            {
                // This no-op validation failure must
                // NOT consume a generation-sequence slot. Bumping
                // _generateSequence before this early-return (the previous
                // behavior) would invalidate an in-flight generation's
                // guarded button re-enable (myGen != _generateSequence in
                // its finally below) even though this call never disables
                // or re-enables the button itself - leaving Generate stuck
                // disabled. The button-disable/re-enable pairing below only
                // ever runs once we know a generation will actually start.
                // Two different mistakes: nothing typed anywhere, or text
                // that resolved to no item (a partial name, or one the
                // typed-name pass above could not match). The old single
                // "select an item" line was misleading for the second -
                // the box looked filled in.
                SetStatus(ItemRowSelection.EmptyRequestStatus(AnyRowHasSearchText()));
                return;
            }

            // Capped to the first 3 names (+ "N more") -
            // see RequestLabelFormatter's own doc comment for why an
            // uncapped label is a ModuleLog-line-length hazard on large
            // plans.
            string requestLabel = RequestLabelFormatter.Format(labelParts);

            // Captured only once we know this call will actually run a
            // generation (past the early-return above). Both entry points
            // that reach here (the Generate button's Click and the modal
            // confirm callback wired in OnOwnMaterialsToggled/ModalDialog)
            // are Blish UI event handlers, so this increment always runs on
            // the main thread before any await - no lock needed, and every
            // deferred callback below reads _generateSequence from the main
            // thread too (inside a MainThreadMarshal.Run callback).
            int myGen = ++_generateSequence;
            // Begin() atomically resets the board's
            // own phase-text/final-status state for this new generation
            // (replacing the old direct _statusClosedForCurrentGeneration/
            // _currentPhaseText/_currentPhaseOrdinal resets here) - see
            // PlanStripStatusBoard.Begin's own doc comment.
            _statusBoard.Begin(myGen);

            _generateButton.Enabled = false;
            _lastDebugLog = null;

            // Everything below the separator still shows the PREVIOUS
            // plan, timestamp and all, for as long as this run takes. Dim
            // it so it reads as superseded rather than current; the
            // finally below restores it on every exit path.
            SetContentDimmed(true);

            // Live spinner + phase-text status strip, replacing the old
            // static "Generating..." for the whole run. ArmSpinnerTicker
            // (an instance method, not a TriggerGenerate-local closure) lets
            // Build() also call it later to re-arm a generation that
            // outlives a tab switch - see that method's own doc comment.
            ArmSpinnerTicker(myGen);

            if (anyQtyInvalid)
            {
                // A one-shot notice takes priority over the very first
                // spinner frame; the next phase event or spinner tick
                // replaces it like any other status text.
                SetStatus("Quantity was invalid - reset to 1. Generating...");
            }

            // Live coarse-phase events drive the status strip's phase
            // text. The finer-grained IProgress<PlanStatus> channel is
            // intentionally passed null below - its two genuinely
            // important diagnostics now reach ModuleLog directly, and the
            // first-run hint rides PlanPhaseEvent.Detail; everything else
            // was routine per-step text the coarse events supersede.
            // This callback writes straight to the thread-safe
            // _statusBoard (no marshal hop - nothing here touches a Blish
            // control; the spinner ticker pulls on the main thread).
            // Progress<T> with no SynchronizationContext posts each
            // Report on an independent ThreadPool work item, so two
            // events milliseconds apart can arrive out of order - the
            // board re-applies PhaseOrdinalGuard/StatusUpdateGuard under
            // its own lock to reject exactly that.
            var phaseProgress = new Progress<PlanPhaseEvent>(pe =>
            {
                if (pe == null) return;
                _statusBoard.UpdatePhase(myGen, (int)pe.Phase, PlanStripTickDecision.FormatPhaseText(pe));
            });

            try
            {
                var result = await _generateAsync(
                    requestItems, _useOwnMaterials, _valueOwnMaterials, _priceBasis,
                    CancellationToken.None, null, phaseProgress, requestLabel);

                // Blish HUD's XNA host has no SynchronizationContext, so this
                // continuation may resume on a ThreadPool thread. vm-building
                // is pure CPU work over already-fetched data - no controls
                // touched - so it stays off the UI thread. The rest mutates
                // shared view state (_nodeOverrides etc.) as well as Blish
                // HUD controls (RenderPlan, SetStatus); bundling the state
                // mutation into the same main-thread callback as the control
                // mutation prevents a torn/interleaved write on these fields
                // between two ThreadPool continuations. That alone would
                // still let an older generation's result overwrite a newer
                // one once both land (last-drained-wins), so the myGen check
                // below is what actually discards a stale generation's
                // result instead of just serializing it.
                var vm = _vmBuilder.Build(result);
                MainThreadMarshal.Run(() =>
                {
                    if (myGen != _generateSequence) return;

                    // Plain-state writes happen before any control mutation
                    // so a disposed-control bail below can never strand this
                    // generation's state half-applied.
                    // The per-generation override/ignore/
                    // expansion reset plus adopting `result` as the
                    // override loop's new baseline now lives on
                    // _treeController - see TreeSectionController.
                    // ResetForNewPlan's own doc comment.
                    _treeController.ResetForNewPlan(result);
                    _sectionExpansion.Clear();
                    _lastDebugLog = result.DebugLog;
                    _currentPlan = vm;
                    _planGeneratedAt = DateTime.Now;

                    // Unconditional board write, deliberately BEFORE the
                    // panel-liveness bail: a completion landing while the
                    // panel is torn down must not drop the "Plan
                    // generated" text - a later Build() pulls it from the
                    // board instead.
                    _statusBoard.Finish(myGen, $"Plan generated - {_planGeneratedAt.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)}");

                    // Plan CONTENT still requires a live panel to render
                    // into - unlike the strip status above, this part of
                    // completion is unaffected by the pull-based rewrite
                    // (mandate scope: strip status path only). The view may
                    // have been torn down (tab switched away, module
                    // disabled) while generation was in flight - a disposed
                    // control's Parent is nulled on disposal (see
                    // ResizeDebounceStep) - nothing left to render into.
                    if (_contentPanel == null || _contentPanel.Parent == null) return;

                    _lastRenderedWidth = _contentPanel.Width;
                    RenderPlan(vm);
                });
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Plan generation failed");
                MainThreadMarshal.Run(() =>
                {
                    // A superseded generation's failure must not clobber a
                    // newer generation's (possibly successful) state or
                    // status - same reasoning as the success path above.
                    if (myGen != _generateSequence) return;

                    _lastDebugLog = new[] { $"Generation failed: {ex.Message}" };

                    // Unconditional board write - see the matching comment
                    // on the success path.
                    _statusBoard.Finish(myGen, $"Error: {ex.Message}");
                });
            }
            finally
            {
                // Runs later on the main thread once queued - the button is
                // still guaranteed to re-enable on every path (success,
                // exception, or cancellation) since finally always executes
                // and Run always queues. The myGen check is evaluated here,
                // inside the queued callback, rather than before queuing:
                // if it were checked up front, a stale generation could pass
                // the check, queue an unconditional enable, and have that
                // enable drain AFTER a newer generation has since started
                // and disabled the button again - re-enabling it while the
                // newer generation is still genuinely in flight. Checking at
                // drain time closes that window; only the generation that
                // matches _generateSequence at the moment its own finally
                // actually runs is allowed to re-enable.
                MainThreadMarshal.Run(() =>
                {
                    if (myGen != _generateSequence) return;
                    // This callback runs back-to-back with the
                    // success/catch callback in the same main-thread
                    // drain - no engine frame can land between them. A
                    // bare _spinnerTicker?.Cancel() here would dispose
                    // the ticker before SpinnerTick ever observes this
                    // generation's Finish() write (a pure state write
                    // with no render side effect), freezing the strip on
                    // the last phase text forever on the ordinary
                    // no-tab-switch path. Rendering the board's snapshot
                    // first, through the same RenderFromBoard every
                    // writer funnels through, flushes the final text
                    // before the ticker is torn down.
                    RenderFromBoard(_statusBoard.Snapshot());
                    _spinnerTicker?.Cancel();
                    _spinnerTicker = null;
                    if (_contentPanel == null || _contentPanel.Parent == null) return;
                    _generateButton.Enabled = true;

                    // The single restore point for the dim applied at the
                    // start of this generation - this finally runs on
                    // success, failure and cancellation alike. A
                    // superseded generation returns at the myGen check
                    // above instead, leaving the dim to the newer
                    // generation that now owns it.
                    SetContentDimmed(false);
                });
            }
        }

        /// <summary>
        /// Dims (or restores) the plan area. A panel rebuilt mid-generation
        /// starts undimmed and is left that way - Build renders whatever
        /// plan state exists into a fresh FlowPanel, and the generation
        /// that dimmed the old one has nothing left to restore.
        /// </summary>
        private void SetContentDimmed(bool dimmed)
        {
            if (_contentPanel == null || _contentPanel.Parent == null) return;

            _contentPanel.Opacity = dimmed ? StalePlanOpacity : 1f;
        }

        /// <summary>
        /// Renders
        /// whatever <paramref name="snapshot"/> says the strip should show
        /// right now - the live spinner-glyph + phase text while in
        /// flight, or the final completion/error text once finished. This
        /// is the ONLY place that reads a PlanStripStatusBoard snapshot and
        /// writes it into _statusLabel; both the spinner ticker's own
        /// per-tick step (<see cref="SpinnerTick"/>) and an immediate
        /// render at (re-)arm time (<see cref="ArmSpinnerTicker"/>, which
        /// Build() also calls on every rebuild) funnel through it. No
        /// generation-identity guard needed here any more (unlike the
        /// pre-fix RenderSpinnerStatus) - PlanStripStatusBoard.UpdatePhase/
        /// Finish already reject a stale generation's writes at the
        /// source, so any Snapshot() this method is handed is already
        /// known-current by construction. Still checks _contentPanel
        /// liveness before writing, matching the established discipline
        /// every other deferred writer in this file already follows -
        /// without it, a ticker tick marshaled after full teardown (module
        /// disabled) could still write into a disposed _statusLabel.
        /// </summary>
        private void RenderFromBoard(PlanStripStatusSnapshot snapshot)
        {
            if (_contentPanel == null || _contentPanel.Parent == null) return;

            if (snapshot.InFlight)
            {
                string text = string.IsNullOrEmpty(snapshot.PhaseText) ? "Generating..." : snapshot.PhaseText;
                // The glyph goes at the END
                // of the string, not the start. SpinnerFrames' proportional-
                // font glyphs ('|' '/' '-' '\') each have a different
                // advance width; with the glyph first, every character
                // after it in the same AutoSizeWidth label shifts
                // horizontally by that width delta ~7x/sec
                // (SpinnerTickInterval), making the phase text visibly
                // jitter left-right during generation even though the
                // label's own Location never changes. Putting the glyph
                // last means the phase text is always laid out identically
                // from the label's fixed x=0 origin - only the trailing
                // glyph (and the label's overall AutoSizeWidth) moves,
                // which reads as a normal spinner rather than shifting
                // text.
                SetStatus($"{text} {SpinnerFrames[_spinnerFrameIndex]}");
            }
            else if (!string.IsNullOrEmpty(snapshot.FinalStatusText))
            {
                SetStatus(snapshot.FinalStatusText);
            }
        }

        /// <summary>
        /// FrameTicker step for generation
        /// <paramref name="myGen"/>. Pulls a fresh snapshot from
        /// _statusBoard every real frame and hands it, together with
        /// <paramref name="myGen"/>, to the pure
        /// <see cref="PlanStripTickDecision.Decide"/> - the race-sensitive
        /// "stop, render the spinner, or render the final text and stop"
        /// decision itself lives there (Blish-free, so the "finish landed
        /// before/between ticks" orderings are directly testable); this
        /// method only carries out whatever it returns and owns the
        /// spinner-glyph throttling.
        /// <see cref="PlanStripTickAction.RenderFinalAndStop"/> is what
        /// makes "the board reports finished -> render final status and
        /// stop" true without any separate completion-callback write into
        /// this control ever being needed. The spinner glyph itself only
        /// advances (and only then re-renders) once per SpinnerTickInterval,
        /// not every frame - DoUpdate fires ~60x/sec, and writing to an
        /// AutoSizeWidth Label's Text re-triggers a text measure/layout
        /// pass even when the string is unchanged, so re-rendering every
        /// single frame instead of ~7x/sec would be a real, avoidable
        /// per-frame cost on the UI thread for the entire duration of every
        /// generation.
        /// </summary>
        private bool SpinnerTick(int myGen, GameTime gameTime)
        {
            if (_contentPanel == null || _contentPanel.Parent == null) return false;

            var snapshot = _statusBoard.Snapshot();
            switch (PlanStripTickDecision.Decide(snapshot, myGen))
            {
                case PlanStripTickAction.RenderFinalAndStop:
                    RenderFromBoard(snapshot);
                    return false;

                case PlanStripTickAction.RenderSpinner:
                    var now = DateTime.UtcNow;
                    if (now - _lastSpinnerTickUtc >= SpinnerTickInterval)
                    {
                        _lastSpinnerTickUtc = now;
                        _spinnerFrameIndex = (_spinnerFrameIndex + 1) % SpinnerFrames.Length;
                        RenderFromBoard(snapshot);
                    }
                    return true;

                default: // Stop (or any future action - fail safe by stopping, never spin forever)
                    return false;
            }
        }

        /// <summary>
        /// (re-)arms the spinner ticker for
        /// generation <paramref name="myGen"/> and renders the board's
        /// current snapshot immediately (so the strip never waits up to a
        /// full SpinnerTickInterval to show something after arming). Two
        /// callers: TriggerGenerate itself (myGen == the generation it just
        /// started - _statusBoard.Begin has already run, so the immediate
        /// render shows "Generating..." until the first phase event lands)
        /// and Build() (myGen == the board's own current Sequence,
        /// re-arming a still-in-flight generation's ticker after
        /// StopLiveTickers canceled the previous one earlier in the SAME
        /// Build() call - see Build()'s own re-arm block). Always cancels/
        /// replaces whatever ticker is already live, matching
        /// TriggerGenerate's pre-existing "a fresh arm always supersedes"
        /// behavior.
        /// </summary>
        private void ArmSpinnerTicker(int myGen)
        {
            _spinnerTicker?.Cancel();
            _spinnerFrameIndex = 0;
            _lastSpinnerTickUtc = DateTime.UtcNow;
            _spinnerTicker = new FrameTicker(gameTime => SpinnerTick(myGen, gameTime));
            RenderFromBoard(_statusBoard.Snapshot());
        }

        #endregion // 2. Generate orchestration (continued)

        #region General: current panel width helper

        /// <summary>
        /// The content panel's LIVE usable width (RightEdgePadding already
        /// subtracted). OnPanelResized updates _contentPanel's own
        /// Width synchronously on every drag tick (no rebuild, no debounce),
        /// so this is always current - unlike a panelWidth value captured
        /// once at a control's build time (e.g. a TreeNodeState created
        /// before a since-completed resize), which the removal of the
        /// settle rebuild would otherwise leave stale indefinitely. Callers
        /// that need "the width this plan was last rendered at" (tree
        /// height/width bookkeeping, lazy child construction) read this
        /// instead of a stored field.
        /// </summary>
        private int GetCurrentPanelWidth()
        {
            return _contentPanel != null ? _contentPanel.Width - RightEdgePadding : 0;
        }

        #endregion // General: current panel width helper

        #region 7. Section builders

        /// <summary>
        /// Factored out of
        /// RenderPlan's own top so the restore-render rollback helper
        /// below can reach the exact same "nothing rendered yet" starting
        /// point RenderPlan itself builds from - drops the tree render
        /// state (_treeController.ResetTreeRenderState - see
        /// that method's own doc comment), clears the relayout/re-ellipsis
        /// action registries (every closure in them captures
        /// controls from a render that is about to be discarded, so
        /// nothing here may outlive the dispose loop below), and disposes
        /// whatever controls currently live in _contentPanel. Order
        /// matters: the state resets happen before disposal so nothing
        /// downstream can observe stale tree/relayout state pointing at
        /// controls that are about to be gone.
        /// </summary>
        private void ResetContentPanelToEmpty()
        {
            _treeController.ResetTreeRenderState();
            _relayoutActions.Clear();
            _reellipsisActions.Clear();

            if (_contentPanel == null) return;

            foreach (var child in _contentPanel.Children.ToArray())
            {
                child.Dispose();
            }
        }

        private void RenderPlan(PlanViewModel vm)
        {
            if (_contentPanel == null) return;

            ResetContentPanelToEmpty();

            int panelWidth = _contentPanel.Width - RightEdgePadding;

            CreatePlanHeader(vm, panelWidth);

            // Separator under header
            var headerSeparator = new Panel()
            {
                Size = new Point(panelWidth, 2),
                BackgroundColor = new Color(180, 180, 180),
                Parent = _contentPanel
            };
            _relayoutActions.Add(w => headerSeparator.Size = new Point(w, 2));

            // Section order mirrors gw2efficiency's calculator page: total
            // cost breakdown, then the recipe tree, then everything else in
            // the builder's emission order (used materials, shopping list,
            // required disciplines, required recipes, crafting steps). The
            // tree lives outside vm.Sections (it renders from vm.TreeRoot/
            // vm.MultiItemRoots), so it is positioned explicitly between the
            // two loops below.
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

            // A multi-item
            // batch supplies N roots directly (vm.MultiItemRoots); a
            // single-item plan is wrapped into a one-element list here so
            // CreateTreeSection/RefreshTreeContainerHeights always deal
            // with "a list of roots" - one root renders byte-identically to
            // the single-tree path (see PlanContentHeightMath.
            // MultiRootTreeFlowHeight's own doc comment).
            List<CraftingTreeNode> treeRoots = vm.MultiItemRoots != null && vm.MultiItemRoots.Count > 0
                ? vm.MultiItemRoots
                : (vm.TreeRoot != null ? new List<CraftingTreeNode> { vm.TreeRoot } : null);
            if (treeRoots != null)
            {
                _treeController.CreateTreeSection(treeRoots, panelWidth);
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
            int startX = PlanRelayoutMath.CenterX(panelWidth, totalTitleWidth);
            int centerRegion = headerHeight - headerTopPad - headerBottomPad;
            int iconY = headerTopPad + (centerRegion - frameSize) / 2;
            // Anchor text to icon's visual center with -2px optical nudge for descenders
            int textY = iconY + (frameSize - textHeight) / 2 - 2;

            var titlePanel = new Panel()
            {
                Size = new Point(panelWidth, headerHeight),
                Parent = _contentPanel
            };

            var iconFrame = IconControls.CreateRarityFramedIcon(
                titlePanel, vm.TargetIconUrl, vm.TargetRarity, startX, iconY,
                iconSize: iconSize, borderThickness: iconBorder);

            int textX = startX + frameSize + iconPad;
            var prefixLabel = new Label()
            {
                Text = prefixText,
                Font = titleFont,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(textX, textY),
                Parent = titlePanel
            };
            textX += prefixWidth;

            var nameLabel = new Label()
            {
                Text = nameText,
                Font = titleFont,
                TextColor = RarityColors.GetRarityNameColor(vm.TargetRarity),
                ShowShadow = true,
                ShadowColor = Color.Black * 0.8f,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(textX, textY),
                Parent = titlePanel
            };
            textX += nameWidth;

            Label qtyLabel = null;
            if (qtyText.Length > 0)
            {
                // DefaultFont16 sits a little taller than Font18's cap
                // height at this weight; +3 keeps its baseline visually
                // aligned with the name label instead of reading "raised".
                qtyLabel = new Label()
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

            string tsText = $"Generated: {_planGeneratedAt.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)}";
            var tsFont = GameService.Content.DefaultFont14;
            var tsMeasured = tsFont.MeasureString(tsText);
            int tsWidth = (int)System.Math.Ceiling(tsMeasured.Width);

            var tsLabel = new Label()
            {
                Text = tsText,
                Font = tsFont,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(PlanRelayoutMath.RightAlignedX(panelWidth - 8, tsWidth), 2),
                Parent = tsPanel
            };

            // Every measured width here (prefixWidth, nameWidth,
            // qtyWidth, tsWidth) is font-only and invariant to panelWidth -
            // only the centering/right-alignment anchors shift, so this is a
            // pure reposition, no re-measure, on every drag tick.
            _relayoutActions.Add(w =>
            {
                int newStartX = PlanRelayoutMath.CenterX(w, totalTitleWidth);
                titlePanel.Size = new Point(w, headerHeight);
                iconFrame.Location = new Point(newStartX, iconY);

                int x = newStartX + frameSize + iconPad;
                prefixLabel.Location = new Point(x, textY);
                x += prefixWidth;
                nameLabel.Location = new Point(x, textY);
                x += nameWidth;
                if (qtyLabel != null)
                {
                    qtyLabel.Location = new Point(x, textY + 3);
                }

                tsPanel.Size = new Point(w, 22);
                tsLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, tsWidth), 2);
            });
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
        /// sections and the Recipe Tree alike): caret + Font18 title, a 2px
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
            // Consistent top gap before every section (including the tree),
            // so sections do not sit flush against whatever preceded them.
            var topGap = new Panel()
            {
                Size = new Point(panelWidth, SectionSpacing),
                Parent = _contentPanel
            };

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

            // ASCII "v"/">" rather than the U+25BC/U+25B6 triangle glyphs:
            // pixel-level screenshot scans showed the triangles failing to
            // render here (and even on the tree's own row caret), so ASCII
            // is the only glyph confirmed to render. Do not re-attempt
            // Unicode without a fresh render check.
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
            // 2px, bottom-anchored inside the 30px headerPanel
            // (Location.Y = 28, i.e. headerPanel.Height - 2) - see
            // LabelHelpers.CreateRowDivider's doc comment for why 1px is unsafe under
            // Blish's non-integer UI-scale GPU transform.
            // NOT built via LabelHelpers.CreateRowDivider (headerPanel is not a row of a
            // list, it has its own fixed 30px height) but it is built the
            // SAME way (a Panel child bottom-anchored near its parent's
            // bottom edge) and is subject to the identical Container.Paint
            // scissor round-trip defect. Simulation (M36b investigation)
            // shows a bottom-flush 2px line under H=30 is immune at the
            // default 0.897 scale but vulnerable (~16-17%) at the "Small"
            // 0.81 scale, so it gets the same 1px bottom clearance as the
            // vulnerable row types (y = 30 - 2 - 1 = 27). Title text sits
            // at y=4 with DefaultFont18 and remains clear of y=27.
            var headerDivider = new Panel()
            {
                Size = new Point(panelWidth, 2),
                Location = new Point(0, 27),
                BackgroundColor = SectionDividerColor,
                Parent = headerPanel
            };

            // Standard (explicit) height, not
            // AutoSize - every row this FlowPanel will ever hold is a fixed
            // constant height (PlanContentHeightMath), so the caller sets
            // Height synchronously right after populating rows instead of
            // waiting for Blish's per-frame AutoSize convergence. Starts at
            // 0 and is corrected before the first paint in every case: a
            // caller that builds rows immediately (every CreateXBody, and
            // RenderTreeNode's own root call) sets the true height in the
            // same call; nothing observes this FlowPanel's height between
            // construction and that set.
            var contentFlow = new FlowPanel()
            {
                Size = new Point(panelWidth, 0),
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Visible = expanded,
                Parent = _contentPanel
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

            // Shared chrome relayout for every section (and the
            // tree) - width-only writes, contentFlow's Height is preserved
            // exactly (whatever it was most recently finalized to by
            // PlanContentHeightMath) so this can never disturb scroll
            // state.
            _relayoutActions.Add(w =>
            {
                topGap.Size = new Point(w, SectionSpacing);
                headerPanel.Size = new Point(w, 30);
                headerDivider.Size = new Point(w, 2);
                contentFlow.Size = new Point(w, contentFlow.Height);
            });

            return new SectionHeaderHandle
            {
                HeaderPanel = headerPanel,
                ArrowLabel = headerArrow,
                ContentFlow = contentFlow
            };
        }

        private void CreateCollapsibleSection(PlanSectionViewModel section, int panelWidth)
        {
            // Required Recipes is the only section whose header needs
            // both a non-static title and an extra header-row control
            // (the "Hide Unlocked" checkbox) - handled by its own method
            // rather than special-casing the shared path.
            if (section.SectionType == PlanSectionType.RequiredRecipes)
            {
                CreateRequiredRecipesSection(section, panelWidth);
                return;
            }

            var header = CreateSectionHeader(section.Title, section.SectionType, panelWidth, section.IsDefaultExpanded);
            var contentFlow = header.ContentFlow;

#if DEBUG
            // A section type added without registering its own width
            // relayout would silently freeze at build-time width on every
            // future resize; fail loud in DEBUG builds instead.
            int relayoutCountBeforeBody = _relayoutActions.Count;
#endif

            // Every section gets its own table-column layout (spec: aligned
            // columns everywhere, not free-flowing text rows), so each has a
            // dedicated body builder rather than a generic per-row dispatch.
            switch (section.SectionType)
            {
                case PlanSectionType.Summary:
                    // Row rendering (the cost-tile row, the
                    // MultiItemNote banner, and the per-currency rows) moved
                    // to Views/Rendering/SummarySectionRenderer.
                    new SummarySectionRenderer(this).Render(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.UsedMaterials:
                    // Row rendering moved to
                    // Views/Rendering/UsedMaterialsSectionRenderer.
                    new UsedMaterialsSectionRenderer(this).Render(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.ShoppingList:
                    // Row rendering moved to
                    // Views/Rendering/ShoppingListSectionRenderer.
                    new ShoppingListSectionRenderer(this).Render(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.CraftingSteps:
                    // Row rendering (including the TimegatedNotice
                    // informational rows) moved to
                    // Views/Rendering/CraftStepsSectionRenderer.
                    new CraftStepsSectionRenderer(this).Render(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.RequiredDisciplines:
                    // Row rendering lives in
                    // Views/Rendering/DisciplinesSectionRenderer, which
                    // also owns its own c-table header call (see
                    // DisciplinesSectionRenderer's doc comment).
                    new DisciplinesSectionRenderer(this).Render(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.Notes:
                    // design-plan-notes.md (Notes section, Option 1): row
                    // rendering lives in Views/Rendering/NotesSectionRenderer -
                    // needs its own case rather than the default fallback
                    // below, since CreateTextRow never draws a coin value
                    // and this section's excess/reclaim lines carry one.
                    new NotesSectionRenderer(this).Render(section, contentFlow, panelWidth);
                    break;
                // PlanSectionType.RequiredRecipes is handled entirely by
                // CreateRequiredRecipesSection (early return above) - never
                // reaches this switch, so no case for it here.
                default:
                    // Defensive fallback for a future section type added
                    // without a dedicated body builder - never leave a
                    // section silently empty. CreateTextRow lives in
                    // Views/Rendering/TextRowRenderer (see that class's
                    // doc comment); this is the only remaining call site
                    // inside CraftingPlanView itself.
                    foreach (var row in section.Rows)
                    {
                        TextRowRenderer.CreateTextRow(row.Label, contentFlow, panelWidth, this);
                    }
                    break;
            }

#if DEBUG
            if (section.Rows.Count > 0 && _relayoutActions.Count == relayoutCountBeforeBody)
            {
                Logger.Warn(
                    "M33 C2b: section {0} rendered {1} row(s) but its body registered no relayout closures - it will not track live window resize. See CreateCollapsibleSection.",
                    section.SectionType, section.Rows.Count);
            }
#endif

            // Finalize contentFlow's real height
            // synchronously now that every row is populated, instead of
            // leaving it to Blish's per-frame AutoSize convergence. Pure
            // function of the same section data just rendered above, so it
            // cannot drift from what was actually built.
            //
            // Summary is special-cased to SummarySectionLayoutMath.
            // BodyHeight instead of PlanContentHeightMath.SectionBodyHeight
            // - see SummarySectionLayoutMath's own doc comment.
            int bodyHeight = section.SectionType == PlanSectionType.Summary
                ? SummarySectionLayoutMath.BodyHeight(section.Rows)
                : PlanContentHeightMath.SectionBodyHeight(section.SectionType, section.Rows);
            contentFlow.Size = new Point(panelWidth, bodyHeight);
        }

        /// <summary>
        /// Required Recipes' own CreateCollapsibleSection variant.
        /// section.Rows is guaranteed non-empty here (the builder only
        /// adds this section when a non-Mystic-Forge recipe survives its
        /// filter), so this method's job is purely the second,
        /// session-toggleable filter: RequiredRecipesVisibility.ApplyFilter
        /// hides Learned/Auto-learned rows when _hideUnlockedRecipes is
        /// checked (the default), and the header title always states the
        /// TOTAL alongside the visible count so it can never read as
        /// dishonest about how many recipes the plan actually needs.
        ///
        /// The header-row "Hide Unlocked" checkbox mirrors
        /// TreeSectionController.CreateTreeSection's own header-button
        /// pattern exactly (the only other place in this file needs a
        /// suppressToggle-guarded extra control in a section header):
        /// pressStartedOnCheckbox is declared before CreateSectionHeader
        /// runs (its click-to-toggle wiring captures the suppressToggle
        /// closure by reference, reading the checkbox's MouseOver lazily at
        /// click time, well after the checkbox itself exists below) so a
        /// click landing on the checkbox never also collapses/expands the
        /// section.
        ///
        /// Toggling the checkbox re-renders through RenderPlan(_currentPlan)
        /// - the same full rebuild path a pill click's local re-solve and a
        /// fresh Generate both already use (TreeSectionController's own
        /// _preserveScrollAcross(() => _renderPlan(vm)) call) - rather than
        /// inventing a second, parallel relayout mechanism just for this
        /// section.
        /// </summary>
        private void CreateRequiredRecipesSection(PlanSectionViewModel section, int panelWidth)
        {
            var visibleRows = RequiredRecipesVisibility.ApplyFilter(section.Rows, _hideUnlockedRecipes);
            string headerTitle = RequiredRecipesVisibility.BuildHeaderTitle(
                section.Rows.Count, visibleRows.Count, _hideUnlockedRecipes);

            bool pressStartedOnCheckbox = false;
            var header = CreateSectionHeader(
                headerTitle, section.SectionType, panelWidth, section.IsDefaultExpanded,
                () => pressStartedOnCheckbox);
            var headerPanel = header.HeaderPanel;
            var contentFlow = header.ContentFlow;

            const int checkboxWidth = 200;
            var hideUnlockedCheckbox = new Checkbox()
            {
                Text = "Hide Unlocked Recipes",
                Checked = _hideUnlockedRecipes,
                Size = new Point(checkboxWidth, 24),
                Location = new Point(panelWidth - checkboxWidth, 3),
                Parent = headerPanel,
                BasicTooltipText = "Hide recipes you already know (Learned/Auto-learned) - show only the ones you are missing."
            };
            _relayoutActions.Add(w => hideUnlockedCheckbox.Location = new Point(w - checkboxWidth, 3));

            headerPanel.LeftMouseButtonPressed += (_, __) =>
            {
                pressStartedOnCheckbox = hideUnlockedCheckbox.MouseOver;
            };

            hideUnlockedCheckbox.CheckedChanged += (_, e) =>
            {
                _hideUnlockedRecipes = e.Checked;
                PreserveScrollAcross(() => RenderPlan(_currentPlan));
            };

#if DEBUG
            int relayoutCountBeforeBody = _relayoutActions.Count;
#endif

            if (visibleRows.Count == 0)
            {
                // Every recipe is unlocked and the filter is hiding them all -
                // a friendly single line instead of a c-table header sitting
                // over an empty body.
                TextRowRenderer.CreateTextRow(
                    RequiredRecipesVisibility.AllUnlockedMessage(section.Rows.Count), contentFlow, panelWidth, this);
            }
            else
            {
                var filteredSection = new PlanSectionViewModel
                {
                    SectionType = section.SectionType,
                    Title = headerTitle,
                    Rows = visibleRows,
                    IsDefaultExpanded = section.IsDefaultExpanded
                };
                new RecipesSectionRenderer(this).Render(filteredSection, contentFlow, panelWidth);
            }

#if DEBUG
            if (_relayoutActions.Count == relayoutCountBeforeBody)
            {
                Logger.Warn(
                    "M33 C2b: section {0} rendered but its body registered no relayout closures - it will not track live window resize. See CreateRequiredRecipesSection.",
                    section.SectionType);
            }
#endif

            contentFlow.Size = new Point(
                panelWidth,
                visibleRows.Count == 0
                    ? PlanContentHeightMath.FallbackTextRowHeight
                    : PlanContentHeightMath.SectionBodyHeight(section.SectionType, visibleRows));
        }

        #endregion // 7. Section builders

        #region 7. Section builders (continued)

        // --- Used Materials section ---
        //
        // Row rendering moved to
        // Views/Rendering/UsedMaterialsSectionRenderer (see the
        // RequiredDisciplines-style call in CreateCollapsibleSection above).

        // --- Shopping List section ---
        //
        // Row rendering, header row, and the ShoppingSourceTag
        // helper moved to Views/Rendering/ShoppingListSectionRenderer (see
        // the RequiredDisciplines-style call in CreateCollapsibleSection
        // above). GetPillColors, which CreateShoppingRow used for its
        // source-tag panel colors, moved to Views/Rendering/PillColors.cs
        // instead (see that file's doc comment).

        // --- Crafting Steps section ---
        //
        // Row rendering (including the TimegatedNotice
        // informational rows and the step-number badge) moved to
        // Views/Rendering/CraftStepsSectionRenderer (see the
        // RequiredDisciplines-style call in CreateCollapsibleSection above).

        // --- Required Disciplines / Required Recipes sections (c-table) ---
        //
        // Required Disciplines' row rendering lives in
        // Views/Rendering/DisciplinesSectionRenderer; Required
        // Recipes' row rendering (both row heights) in
        // Views/Rendering/RecipesSectionRenderer; the shared
        // c-table header (CreateCTableHeaderRow) in
        // Views/Rendering/CTableHeaderRenderer - see that class's doc comment.

        // --- Summary / Total Cost section ---
        //
        // Row rendering (the cost-tile row and its
        // CostTileHandle/TileCaptionFor helpers, the MultiItemNote
        // banner row, and the per-currency CreateCurrencyRow rows) moved to
        // Views/Rendering/SummarySectionRenderer (see the
        // RequiredDisciplines-style call in CreateCollapsibleSection above).

        #endregion // 7. Section builders (continued)

        #region 8. Tree rendering (continued)

        // The Recipe Tree
        // section renderer (TreeNodeState, CreateTreeSection,
        // RenderTreeNode, RefreshTreeContainerHeights,
        // UpdateTreeRowTooltip), the interactive override loop
        // (ApplyPreset, ApplyOverridesAndResolve), and the Decision Pills
        // renderer (RenderDecisionPills - formerly its own "9. Decision
        // pills" region, folded into this one since both regions moved
        // together) all moved onto Views/Rendering/TreeSectionController -
        // see that class's own doc comment for the full inventory and
        // every non-move edit.

        #endregion // 8. Tree rendering (continued)
    }
}

#pragma warning restore SA1124 // Do not use regions
