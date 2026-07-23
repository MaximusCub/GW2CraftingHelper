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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// M38 WP-04 (m38-a1-architecture.md S3c/S11): this file is the one deliberate,
// reviewed use of #region in the codebase - navigation markers for an
// ~4800-line class pending the Wave F/G extraction the architecture report
// recommends, mirroring its 11-responsibility map. SA1124 exists to stop
// regions hiding code from review, not to block a documented, plan-mandated
// mapping pass; scoped to this file only, not the shared ruleset.
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

        // M35 (gw2efficiency parity - multi-item plans): the top strip used
        // to be four fixed rows (search+qty, controls, status, separator);
        // it is now InputRowsAreaHeight(N) item rows (N = _itemRows.Count)
        // followed by the same three rows, at a gap identical to the old
        // fixed spacing - see ComputeTopRegionLayout. With N == 1 every Y
        // offset below reproduces the old constants exactly (5, 43, 81,
        // 102, 107, 112), so the single-row case is byte-identical to
        // pre-M35 layout.
        private const int TopRegionRowGap = 3;
        private const int StatusToSeparatorGap = 21;
        private const int SeparatorToContentGap = 5;
        private const int ContentToBottomPad = 5;
        private const int RightEdgePadding = 20;
        private const int SectionSpacing = 16;

        // M36 fix-pass (NICETOHAVE c): overall outer size (icon + both
        // border edges) of IconControls.CreateRarityFramedIcon's DEFAULT frame (32px
        // icon + 1px border each side - see that method's own default
        // parameters). Named so the row-height-vs-icon-frame arithmetic
        // comments this pass touches (CreateRecipeRow) reference one
        // source of truth instead of re-hardcoding "34" independently of
        // IconControls.CreateRarityFramedIcon's actual defaults.
        private const int RarityFramedIconOuterSize = 34;

        // Section divider grey, readable against the parchment texture, one
        // tier below the 180-grey structural separators (window chrome,
        // unrelated to this). The row-divider twin (RowDividerColor) moved
        // to Views/Rendering/LabelHelpers.cs alongside
        // LabelHelpers.CreateRowDivider (M38 WP-21) - it had no other caller.
        private static readonly Color SectionDividerColor = new Color(130, 130, 130);

        /// <summary>
        /// Pure top-strip Y-offset arithmetic (Blish-free math, kept as a
        /// plain struct/method rather than a control mutation so Build()
        /// and every row Add/Remove reflow call the exact same formula -
        /// M35, gw2efficiency parity multi-item plans). See the constants'
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

        private readonly Func<IReadOnlyList<PlanRequestItem>, bool, PriceBasis, CancellationToken, IProgress<PlanStatus>, Task<CraftingPlanResult>> _generateAsync;
        private readonly Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, ISet<int>, CraftingPlanResult> _resolveOverridesSync;
        private readonly ModalDialog _modalDialog;
        private readonly IItemSearchProvider _itemSearchProvider;
        private readonly ModuleSettings _settings;
        private readonly PlanViewModelBuilder _vmBuilder = new PlanViewModelBuilder();

        private PlanViewModel _currentPlan;
        private CraftingPlanResult _lastResult;
        private DateTime _planGeneratedAt;
        private bool _useOwnMaterials;
        // M33 spec item 8 (r1 section 2.1): gw2efficiency's own default is
        // "buy price" (buy orders - patient, usually cheaper), with a
        // per-item fallback to instant-buy only when a listing is missing.
        // Echo that default here so a fresh plan matches gw2e's own view
        // rather than systematically overpricing every material.
        private PriceBasis _priceBasis = PriceBasis.BuyOrder;

        #endregion // General: shared layout constants, colors, top-region geometry & dependencies

        #region 1. Input rows (state) - M35 gw2efficiency parity, multi-item plans

        /// <summary>
        /// One row of the multi-item input strip (M35, gw2efficiency
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

        // Session-persistent row list (M35) - mirrors gw2e's own
        // `e.recipes` array (`[{id: null, amount: 1}]` initial state - see
        // docs/gw2e-parity-spec.md, the M34 r1 multi-item research report).
        // Populated with one empty row the first time Build() ever runs;
        // survives every later Build() call (tab switch) exactly like
        // _nodeOverrides/_ignoredItemIds - no new file/URL persistence (gw2e
        // itself only persists via its own URL, not applicable here - see
        // docs/KNOWN-ISSUES.md's M35 section).
        private readonly List<ItemRowState> _itemRows = new List<ItemRowState>();

        #endregion // 1. Input rows (state) - M35 gw2efficiency parity, multi-item plans

        #region 2. Generate orchestration (state)

        // Bumped at the start of every TriggerGenerate call (Generate button
        // and OnOwnMaterialsToggled's modal-confirm path both funnel through
        // it). Each call captures its own value and every deferred callback
        // it queues re-checks it against the live field before applying
        // anything, so a superseded generation's result cannot clobber a
        // newer one (last-drained-wins) even though both entry points can
        // overlap in flight.
        private int _generateSequence;

        // M34-B1 #4: set true the instant the CURRENT generation (myGen ==
        // _generateSequence) writes its own completion/error status text.
        // Reset to false at the start of every TriggerGenerate call. Guards
        // against a late-draining trailing progress tick from that SAME
        // generation overwriting the completion text it already wrote (see
        // StatusUpdateGuard) - a race myGen alone cannot catch, since both
        // callbacks share the same generation number.
        private bool _statusClosedForCurrentGeneration;

        #endregion // 2. Generate orchestration (state)

        #region 8. Tree rendering (state)

        // Per-node user decision overrides (keyed by solver NodeId) and
        // explicit tree expansion state; both survive local re-solves and
        // reset on a fresh Generate.
        private readonly Dictionary<int, AcquisitionSource> _nodeOverrides =
            new Dictionary<int, AcquisitionSource>();

        // Item ids manually marked "Ignore" this session (M34-B2b, gw2e
        // parity) - keyed by ItemId (not NodeId), matching gw2e's own
        // "Ignore marks every occurrence of that item id, tree-wide"
        // semantics (see PlanSolver.Solve's ignoredItemIds parameter).
        // Independent of _nodeOverrides: neither "Best Path" nor "Craft
        // All"/"Buy All" clears this (gw2e's bulk actions are documented as
        // unrelated to ownership - r2 report Section 3.3); it is only ever
        // toggled per item id (the pill click) or on a fresh Generate.
        private readonly HashSet<int> _ignoredItemIds = new HashSet<int>();
        private readonly Dictionary<int, bool> _nodeExpansion =
            new Dictionary<int, bool>();
        #endregion // 8. Tree rendering (state)

        #region 7. Section builders (state: section expand/collapse)
        private readonly Dictionary<PlanSectionType, bool> _sectionExpansion =
            new Dictionary<PlanSectionType, bool>();

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

        // M35: the Container Build() was called with, retained so
        // AddItemRow/RemoveItemRow (fired by a row button's Click, long
        // after Build() returns) can re-read ContentRegion and reflow the
        // top strip - see ReflowInputRegion.
        private Container _buildPanel;
        private Panel _inputPanel;
        private Panel _controlsPanel;
        private Checkbox _ownMaterialsCheckbox;
        private StandardButton _generateButton;
        private Label _statusLabel;
        private Panel _separator;
        private FlowPanel _contentPanel;

        #endregion // General: Blish UI control fields (shared across all responsibilities)

        #region 5. Resize relayout (state) - KNOWN-ISSUES #13/#19

        // Resize tracking
        private int _lastRenderedWidth;

        // M33 C2b: per-render relayout registry, lifecycle mirrors
        // _treeNodeStates (cleared and repopulated by every full RenderPlan
        // rebuild; appended to by lazy tree-node expansion afterwards).
        // _relayoutActions holds cheap, position/width-only closures (no
        // MeasureString) replayed on EVERY resize tick via OnPanelResized -
        // see ReplayRelayout. _reellipsisActions holds the small subset of
        // sections that truncate text (Used Materials name, Shopping row
        // name, Tree row name - the 3 LabelHelpers.EllipsizeToWidth call sites m2's
        // research inventoried); these are text-only (Label.Text/tooltip)
        // updates on already-existing controls, replayed only once at drag
        // settle - see RunReellipsis. Neither list ever changes a control's
        // Height, so neither can perturb AutoSize/scroll state (M33 C2a
        // made every row height explicit; a pure width/text write on a
        // fixed-height row cannot re-trigger convergence).
        private readonly List<Action<int>> _relayoutActions = new List<Action<int>>();
        private readonly List<Action<int>> _reellipsisActions = new List<Action<int>>();

        // M38 WP-23 (m38-a1-architecture.md S3b-T2 pilot): ISectionRelayoutSink
        // implementation. Explicit-interface (not public) so extracted
        // section renderers can register through the seam without this
        // widening CraftingPlanView's public surface. Both members are a
        // direct pass-through to the two lists immediately above - same
        // list, same append order - so every invariant that reads those
        // lists (CreateCollapsibleSection's DEBUG must-register check,
        // ReplayRelayout's DEBUG scroll-neutral assert, ReplayRelayout/
        // RunReellipsis's own foreach) sees a sink-registered closure
        // exactly as it would have seen one added inline. Zero semantic
        // change - see ISectionRelayoutSink's doc comment for the full
        // rationale.
        void ISectionRelayoutSink.AddRelayout(Action<int> closure)
        {
            _relayoutActions.Add(closure);
        }

        void ISectionRelayoutSink.AddReellipsis(Action<int> closure)
        {
            _reellipsisActions.Add(closure);
        }

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

        // M33 C2c (resize-scroll-preserve regression fix): set by
        // PreserveScrollAcrossResize whenever a height-changing resize tick
        // (dragging the window's bottom edge or a corner) wrote a per-tick
        // scroll-preserve during the current drag. ResizeSettleStep arms a
        // single bounded verify window for it at drag settle (not per
        // tick) via StartResizeScrollVerify, then clears the flag.
        // _resizeScrollSavedOffset holds the last known-good pre-tick
        // pixel offset to verify against - PreserveScrollAcrossResize only
        // updates it when a tick's freshly captured offset is > 0, so an
        // uncontested reset that lands on some frame between two ticks
        // (and would otherwise corrupt the NEXT tick's own capture to 0)
        // cannot erase the real target. PreserveScrollAcross (the rebuild
        // path) clears _resizeScrollRestorePending up front, since a
        // rebuild disposes and recreates the content panel's children -
        // any resize-drag offset captured against the old content is
        // meaningless once that happens.
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

        #endregion // 6. The FrameTicker control (ticker instance fields) - KNOWN-ISSUES #12/#13

        #region 4. Wheel-wrap correction (state) - KNOWN-ISSUES #12 (reopened)

        // M36 fix-pass (KNOWN-ISSUES #12, CRITICAL-1): defensive one-shot
        // re-assert ticker for ApplyWheelWrapCorrection - see
        // StartWheelWrapVerify's own doc comment. Kept as its own field
        // (not shared with _scrollVerifyTicker above) since the two guard
        // unrelated writers with different targets/semantics (a ratio
        // derived from a saved pixel offset vs. an already-computed
        // absolute ScrollDistance) and a rebuild/resize verify could
        // otherwise cancel-and-replace an in-flight wheel-wrap verify (or
        // vice versa) for no reason.
        private FrameTicker _wheelWrapVerifyTicker;

        private const int WheelWrapVerifyMaxFrames = 2;

        // Matches StartScrollVerify's own stable-match tolerance.
        private const float WheelWrapVerifyEpsilon = 0.004f;

        #endregion // 4. Wheel-wrap correction (state) - KNOWN-ISSUES #12 (reopened)

        #region 3. Scroll preserve/restore/verify (state, continued) - KNOWN-ISSUES #12/#14/#19

        // M33 C2a (directive B): with container heights now finalized
        // synchronously during build (PlanContentHeightMath), the restore
        // ratio is correct the instant PreserveScrollAcross writes it - no
        // multi-frame AutoSize convergence remains to wait out. The
        // FrameTicker that used to run RestoreScrollOffset's up-to-30-frame
        // convergence loop, then hand off to a further 20-frame (up to
        // 120-frame hard-capped) guard, shrinks to a short defensive verify
        // that only exists to contest a genuine LATE Blish-internal
        // scrollbar reset (Scrollbar.RecalculateLayout zeroes ScrollDistance
        // whenever _scrollbarPercent changes, which can still land on the
        // frame or two right after the synchronous write) and to yield
        // immediately the moment real user input is observed.
        private const int ScrollVerifyMaxFrames = 3;

        // M33 C2a (directive C): bounds the guard's zero-reassert
        // back-and-forth (Blish resets the bar to 0, we contest, it resets
        // again...) so a user genuinely holding the bar at top through
        // repeated library resets eventually wins rather than being fought
        // forever - see docs/KNOWN-ISSUES.md's carried follow-up note.
        // Naturally bounded further by ScrollVerifyMaxFrames itself now
        // that the window is only 2-3 frames long.
        private const int ScrollVerifyZeroReassertCap = 4;

        // M33 C2a (directive C): timestamp of the most recent user
        // mouse-wheel event observed over the content panel. Tracked
        // unconditionally - not gated on ScrollDiagnosticsEnabled, unlike
        // the diagnostic wheel logging below - because StartScrollVerify
        // uses it for one real behavioral decision: any wheel event
        // observed since a verify window armed yields that window
        // immediately, no further contest. (M33 fix-pass: a second,
        // recency-only "suppress the zero-reassert" use of this timestamp
        // was removed - it could only ever fire for a wheel that predated
        // the window's arm time, in which case suppressing was wrong: see
        // StartScrollVerify's zero-reassert comment.) Reset at the top of
        // every Build() so a stale value from a previous render cannot
        // influence a brand new one.
        private DateTime? _lastWheelEventUtc;

        #endregion // 3. Scroll preserve/restore/verify (state, continued) - KNOWN-ISSUES #12/#14/#19

        #region 4. Wheel-wrap correction (state, continued) - KNOWN-ISSUES #12 (reopened)

        // M36 (KNOWN-ISSUES #12 reopened/root-caused): Blish HUD's
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

        // M33 C1 (#12 diagnostics): instrumentation-only. Gated on
        // ModuleSettings.ScrollDiagnosticsEnabled (default false); every
        // call site below checks the live setting value BEFORE doing any
        // work so the cost when disabled is a single bool read, not a
        // formatted-string allocation. Never read by, or fed back into,
        // any scroll/guard/restore decision - diagnostics only observe.
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

        // M38 WP-04: single read-through for the ~7 call sites below that
        // used to repeat "_settings != null && _settings.ScrollDiagnosticsEnabled.Value"
        // verbatim. Pure property, same short-circuit null-guard, no behavior
        // change - see docs/KNOWN-ISSUES.md #12 for why this stays gated.
        // M39 (log system, tab-roadmap-proposal Section 2.1): ALSO true when
        // the new unified LogDiagnosticsEnabled setting is on -
        // LogDiagnosticsEnabled subsumes ScrollDiagnosticsEnabled (one
        // Settings-tab checkbox for the whole module going forward), but
        // ScrollDiagnosticsEnabled itself is kept readable here (a plain
        // bool OR - trivially cheap, no extra I/O) so an already-persisted
        // true for the old setting keeps gating this channel exactly as
        // before, rather than silently losing it on upgrade - see
        // ModuleSettings.ScrollDiagnosticsEnabled's own doc comment.
        private bool ScrollDiagEnabled => _settings != null &&
            (_settings.LogDiagnosticsEnabled.Value || _settings.ScrollDiagnosticsEnabled.Value);

        /// <summary>
        /// M39 (log system, d2-log-system.md Section 8): routes every
        /// [scrolldiag] line to BOTH sinks - Blish's own Logger (unchanged,
        /// additive) and the new module-wide ModuleLog at Debug level, tag
        /// "scrolldiag" - so the channel is visible in-module via the Log
        /// tab, gated on the same ScrollDiagEnabled the call sites already
        /// check before formatting anything. Centralized here (rather than
        /// duplicating the ModuleLog.Shared.Write call at each of the ~14
        /// sites) so the tag/level is defined exactly once.
        /// </summary>
        private void LogScrollDiag(string message)
        {
            Logger.Debug(message);
            ModuleLog.Shared.Write(ModuleLogLevel.Debug, "scrolldiag", message);
        }

        #endregion // Diagnostics: scroll/wheel instrumentation (shared by #3 and #4) - KNOWN-ISSUES #12

        #region General: construction & status
        public CraftingPlanView(
            Func<IReadOnlyList<PlanRequestItem>, bool, PriceBasis, CancellationToken, IProgress<PlanStatus>, Task<CraftingPlanResult>> generateAsync,
            ModalDialog modalDialog,
            IItemSearchProvider itemSearchProvider,
            ModuleSettings settings,
            Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, ISet<int>, CraftingPlanResult> resolveOverridesSync = null)
        {
            _generateAsync = generateAsync;
            _modalDialog = modalDialog;
            _itemSearchProvider = itemSearchProvider;
            _settings = settings;
            _resolveOverridesSync = resolveOverridesSync;
        }

        public void SetStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = status ?? "";
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
        /// scroll position afterwards. M33 C2a (directive A/B): every
        /// container mutate() rebuilds now finalizes its explicit Height
        /// synchronously (PlanContentHeightMath), so mutate()'s return means
        /// the new content's true height is already valid - no nested
        /// AutoSize convergence remains to wait out. The restore ratio is
        /// therefore computed and written to the scrollbar synchronously,
        /// in this same call, before the next paint (ApplySavedScrollSynchronously);
        /// a short FrameTicker-driven verify then only defends against a
        /// LATE Blish-internal scrollbar reset over the following couple of
        /// real frames (StartScrollVerify).
        /// </summary>
        private void PreserveScrollAcross(Action mutate)
        {
            int saved = _contentPanel?.VerticalScrollOffset ?? 0;
            int capturedGeneration = ++_scrollRestoreGeneration;

            // M33 C2c: a rebuild is about to dispose and recreate every
            // content-panel child, so any resize-drag scroll-preserve still
            // pending from before it (see _resizeScrollRestorePending) is
            // now meaningless - clear it so a later ResizeSettleStep tick
            // never arms a stale-offset verify (StartResizeScrollVerify)
            // against the new content using the old content's dimensions,
            // which could otherwise cancel and replace this rebuild's own
            // in-flight verify with wrong math.
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

            // M33 C1 (#12 diagnostics): observation-only - lets external
            // code (the wheel diagnostic handler) ask "is this ticker still
            // running" without altering any ticker behavior. _scrollVerifyTicker
            // is never nulled out when a ticker self-cancels (only
            // reassigned or explicitly cleared at the top of the next
            // Build()), so a plain null-check on that field cannot tell
            // "never started" apart from "ran once and finished long ago" -
            // this property is the accurate signal.
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

        #region 6. The FrameTicker control (teardown) - KNOWN-ISSUES #12/#13, M39/WP-17

        /// <summary>
        /// Cancels every live FrameTicker (scroll-verify, resize-debounce,
        /// wheel-wrap-verify) and resets their associated pending state.
        /// Two callers: the top of every <see cref="Build"/> (a fresh build
        /// cycle supersedes any ticker from the previous one - unchanged
        /// behavior, just factored out of that method) and
        /// Module.Unload (M39/WP-17: these tickers are parented to the
        /// SpriteScreen, not this view's own control tree - see the ticker
        /// fields' own comments - so nothing else tears them down if the
        /// module unloads while a tab holding this view is open and a
        /// ticker is mid-flight; each ticker also bails itself out on its
        /// own next frame as a second line of defense, but Unload should
        /// not depend on "one more frame runs after unload" being true).
        /// </summary>
        public void StopLiveTickers()
        {
            _scrollVerifyTicker?.Cancel();
            _scrollVerifyTicker = null;
            _resizeDebounceTicker?.Cancel();
            _resizeDebounceTicker = null;
            _wheelWrapVerifyTicker?.Cancel();
            _wheelWrapVerifyTicker = null;
            _resizeSettlePending = false;
            _resizeScrollRestorePending = false;
            _resizeScrollSavedOffset = 0;
            _lastWheelEventUtc = null;
        }

        #endregion // 6. The FrameTicker control (teardown) - KNOWN-ISSUES #12/#13, M39/WP-17

        #region 3. Scroll preserve/restore/verify (continued) - KNOWN-ISSUES #12/#14/#19

        /// <summary>
        /// M33 C2a (directive B): writes the restore ratio to the scrollbar
        /// synchronously, using the content height that mutate() already
        /// finalized before this method runs (directive A). This is the
        /// change that eliminates the #14 flash: nothing paints between
        /// mutate() returning and this write landing, so the viewport never
        /// visibly reaches a wrong position at all - there is no "restore a
        /// frame late" gap left to close.
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
        /// M33 C2a (directives A-C): short defensive verify that runs after
        /// ApplySavedScrollSynchronously's write. With container heights
        /// finalized synchronously at build time, no multi-frame AutoSize
        /// convergence remains to race - this only exists to contest a
        /// single expected class of LATE write: Blish's own
        /// Scrollbar.RecalculateLayout zeroes ScrollDistance whenever the
        /// panel's content/viewport ratio changes, which the scrollbar
        /// re-evaluates every real frame (Scrollbar.DoUpdate calls
        /// Invalidate() unconditionally) and so can still land on the frame
        /// or two immediately following our synchronous write. The window
        /// exits on the FIRST frame that confirms the write is holding
        /// (directive B - no multi-frame stable streak required, since
        /// height is not still drifting) and is capped at
        /// ScrollVerifyMaxFrames regardless.
        ///
        /// Directive C: any user wheel event observed since this window
        /// armed yields it immediately and unconditionally - no
        /// heightUnchanged precondition, since height is already valid at
        /// arm time. This is the ONLY wheel-driven exit: a zero-reassert
        /// (scrollbar reads exactly 0 while target sits well above it) is
        /// always contested and never suppressed by wheel recency. A wheel
        /// at or after armedAtUtc already exits via the check above before
        /// reaching the zero-reassert branch; a wheel that predates
        /// armedAtUtc is the input that produced savedOffset in the first
        /// place (PreserveScrollAcross captures it before mutate() runs),
        /// so treating it as "user meant to land at the top" would abandon
        /// restoring their real, non-top position - exactly the #14 flash
        /// this window exists to prevent. (An earlier revision suppressed
        /// the reassert on any wheel within a short recency window
        /// regardless of arm time; that bled across the mutation boundary
        /// and was removed in the M33 fix-pass.) The zero-reassert cap
        /// (ScrollVerifyZeroReassertCap) is kept as a last-resort guarantee
        /// that a persistent fight eventually ends.
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
                        // Scrollbar reads exactly zero while our target sits
                        // well above it: Blish's own reset landed on this
                        // frame (see the class doc comment). This is
                        // ALWAYS a library reset, never a genuine "user
                        // wheeled to exactly top" - any wheel event at or
                        // after armedAtUtc already exited via the
                        // wheel-observed check above before reaching this
                        // line, and a wheel event that predates armedAtUtc
                        // reflects the user's real pre-mutation position
                        // (the ratio ApplySavedScrollSynchronously just
                        // wrote), which this reassert must restore rather
                        // than treat as "user meant to be at the top".
                        // (M33 fix-pass: a recency-only suppression here
                        // previously let a wheel just BEFORE the mutation
                        // veto the restore of a real non-top position -
                        // see git history for the removed check.)
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
        /// M33 C2a (directive C): unconditional (NOT diagnostics-gated) tap
        /// on the same MouseWheelScrolled event OnScrollDiagWheelScrolled
        /// below observes, recording only a timestamp. StartScrollVerify
        /// reads _lastWheelEventUtc to yield a live verify window
        /// immediately the moment a wheel event lands in it (scoped to
        /// wheels at or after the window's arm time - see that method's
        /// doc comment). This is a real behavioral decision now, not
        /// diagnostics, so unlike the tap below this must run regardless
        /// of ScrollDiagnosticsEnabled - cost is a single DateTime.UtcNow
        /// call per wheel notch, not per frame.
        /// </summary>
        private void OnContentWheelObserved(object sender, MouseEventArgs e)
        {
            _lastWheelEventUtc = DateTime.UtcNow;

            // M36 (KNOWN-ISSUES #12 reopened/root-caused): classification
            // is unconditional (zero-allocation, a plain value tuple) - see
            // WheelDeltaSanitizer's own doc comment for the full root
            // cause and threshold derivation. GameService.Input.Mouse.
            // State.ScrollWheelValue is the SAME raw value
            // OnScrollDiagWheelScrolled's diagnostic log already reads as
            // "raw" - this is the field the live 2026-07-21 histogram was
            // measured from.
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
        /// M36 (KNOWN-ISSUES #12 reopened/root-caused): corrects the
        /// damage from a wrapped wheel delta. Blish HUD's own
        /// Scrollbar.HandleWheelScroll looks only at Math.Sign of the
        /// (here, corrupted-negative) raw delta, so for every wrapped
        /// up-flick it has already queued exactly ONE step DOWN via
        /// ScrollAnimated by the time this handler runs - OnContentWheel-
        /// Observed is subscribed after Blish's own Scrollbar (see
        /// OnScrollDiagWheelScrolled's doc comment), so Blish's own
        /// HandleWheelScroll for this same event has always already run.
        ///
        /// MECHANISM (M36 fix-pass, re-verified against the decompiled
        /// Glide source rather than assumed): an earlier revision of this
        /// comment claimed Tweener.TargetCancel was a no-op here, on the
        /// theory that Glide defers a freshly-created Tween's by-target
        /// dictionary registration to the NEXT Tweener.Update() call. That
        /// theory is FALSE for the vendored Glide (decompiled from the
        /// shipped "Blish HUD.exe", Glide.Tween.TweenerImpl.Tween&lt;T&gt;()):
        /// the SAME method that enqueues a new tween onto its private
        /// toAdd queue also calls its own AddAndRemove() synchronously,
        /// before returning - which dequeues toAdd and registers the
        /// tween in the by-target "tweens" ConcurrentDictionary right
        /// there, not deferred to any later frame. So by the time
        /// Scrollbar.ScrollAnimated's call to Tweener.Tween(...) returns
        /// (still inside Blish's own HandleWheelScroll, still before this
        /// handler ever runs for the same event), the wrong duration-0
        /// tween is ALREADY registered in that dictionary. TargetCancel
        /// therefore finds it immediately: Tween.Cancel(string[]) nulls
        /// the tween's own vars/lerpers slot for "ScrollDistance"
        /// synchronously, so even if the tween's Update() runs before it
        /// is fully removed from the per-target list (removal itself is
        /// queued, applied by the next AddAndRemove() pass), that Update()
        /// skips writing ScrollDistance entirely (Tween.Update() null-
        /// guards every var/lerper slot before writing) - the wrong step
        /// never lands, full stop, not merely "canceled one frame late".
        /// This is why the cancel-then-direct-write shape below is kept
        /// rather than replaced with a counter-tween or a one-frame-
        /// deferred correction: TargetCancel already wins synchronously,
        /// in the same call stack, with no wrong frame ever rendered - a
        /// counter-tween would add complexity for no behavioral gain, and
        /// a deferred correction would manufacture a wrong frame this
        /// mechanism does not actually have.
        /// (Also corrected: an earlier revision claimed Scrollbar.
        /// ScrollAnimated "implicitly relies on" this same public
        /// TargetCancel API for its own between-events overwrite
        /// behavior. False on the literal text - decompiled Scrollbar.cs
        /// calls only Tweener.Tween(this, new { ScrollDistance = ... },
        /// 0f).Ease(...), with no TargetCancel call and no explicit
        /// "overwrite: true" anywhere in that file. The real mechanism
        /// for two rapid ScrollAnimated calls in a row is Tween&lt;T&gt;'s own
        /// overwrite PARAMETER, true by default whenever the caller omits
        /// it (as Scrollbar always does), which internally cancels any
        /// PRE-EXISTING same-target/same-property tween via its own
        /// private ForAllTweens+Cancel loop - conceptually similar to
        /// TargetCancel, but a distinct, internal-only code path, never
        /// the public API this method calls.)
        ///
        /// Despite the above, a bounded defensive re-assert
        /// (StartWheelWrapVerify) still runs for a frame or two after this
        /// write - insurance against a future Blish/Glide vendor change or
        /// an interaction this analysis missed, not evidence this
        /// mechanism is expected to fail.
        ///
        /// The KNOWN-ISSUES #19 "stale-cached-percent" hazard (Scrollbar.
        /// RecalculateLayout resetting ScrollDistance to 0 the first time
        /// _scrollbarPercent's cached value goes stale-to-fresh) does NOT
        /// apply here: that hazard is specific to a resize tick changing
        /// the viewport/content ratio for the first time since the last
        /// RecalculateLayout call. A wheel event alone never changes
        /// content or viewport height, so _scrollbarPercent is already
        /// fresh and RecalculateLayout is not needed before this write.
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

            // Blish's own per-notch step convention (Scrollbar.
            // HandleWheelScroll/ScrollAnimated, see BlishScrollWheelStep-
            // Pixels' own provenance comment): one wheel EVENT moves the
            // bar by BlishScrollWheelStepPixels * MouseWheelScrollLines
            // pixels, sign-only (never magnitude-scaled). Read live here
            // too, matching Blish's own live read, so this stays correct
            // for any POSITIVE MouseWheelScrollLines value if the user
            // changes their OS mouse-wheel-lines setting. MUSTFIX-2:
            // Windows' "one screen at a time" setting reports
            // MouseWheelScrollLines == -1, which would flip deltaPixels'
            // sign if used directly - Blish's own HandleWheelScroll has
            // this identical defect (its Math.Sign(...) * -30 *
            // MouseWheelScrollLines scrolls the WRONG direction for every
            // wheel event under that setting, wrapped or not - we cannot
            // fix Blish's own arithmetic). WheelDeltaSanitizer.
            // SanitizeScrollLines substitutes Windows' documented default
            // of 3 lines whenever the raw value is not a usable positive
            // count, which keeps THIS correction's direction right; it
            // cannot make a corrected flick match Blish's own (equally
            // wrong) step size under that setting - direction-correctness
            // is chosen over unreachable step-parity for this one OS
            // setting value.
            // intendedDelta is always a clean multiple of 120 in practice
            // (the wrap always adds back a whole ushort span to a raw
            // value that started as N*120), but this scales proportionally
            // rather than assuming an exact multiple, so a non-multiple
            // value degrades gracefully instead of losing a partial notch.
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
        /// M36 fix-pass (KNOWN-ISSUES #12, CRITICAL-1 finding response): a
        /// bounded, one-shot defensive re-assert for
        /// ApplyWheelWrapCorrection's write. That method's own doc comment
        /// verifies Tweener.TargetCancel is synchronously effective
        /// against Blish's wrong tween here, so this ticker exists as
        /// insurance against a future Blish/Glide vendor change or an
        /// interaction this analysis missed - not evidence of an expected
        /// failure. Unlike StartScrollVerify's zero-reassert loop (which
        /// fights a KNOWN recurring adversary up to a cap), this re-
        /// asserts AT MOST ONCE and then stops regardless of outcome - a
        /// mundane insurance check, not an ongoing contest - and yields
        /// immediately to any NEWER wheel event so it can never contest
        /// genuine subsequent user input.
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
        /// M33 C1 (#12 diagnostics): observation-only wheel handler.
        /// Subscribes to the same MouseWheelScrolled event Blish's own
        /// Scrollbar subscribes to in its constructor (which runs before
        /// this handler is ever wired up, since _contentPanel must exist
        /// first) - so this always observes the scrollbar AFTER Blish's own
        /// HandleWheelScroll has already run for the same event (tween
        /// created, not yet applied). Never writes to the scrollbar, never
        /// influences restore/verify decisions - purely a read-and-log tap
        /// (OnContentWheelObserved above is what actually drives behavior).
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
        /// them from _itemRows (M35, gw2efficiency parity multi-item
        /// plans). Called by Build() (initial construction) and by
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
        /// One input row's controls: search box + qty (unchanged from the
        /// pre-M35 single row), plus a Remove button (gw2e's own
        /// `ng-if="recipes.length > 1"` gate - ItemRowRequestBuilder.
        /// CanRemoveRow) and, on the last row only, an Add button (echoing
        /// gw2e's own single trailing "Add another item" link, attached to
        /// the last row instead of its own separate strip row so the
        /// single-row case keeps today's exact row height/position - see
        /// the TopRegionRowGap constants' own doc comment).
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
        /// the row count changes - M35's Add/Remove counterpart to
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
            // Screen-parented popups from the previous build cycle (one per
            // item row - M35 replaces the single _suggestionPanel this used
            // to be) are cleaned up by RebuildItemRowControls below, which
            // every row already routes through - no separate loop needed
            // here.

            // Cleanup for any leftover scroll-verify/resize-debounce/
            // wheel-wrap-verify tickers from the previous build cycle, plus
            // their associated pending state - see StopLiveTickers' own
            // doc comment (M39 factored this block out into a named method
            // so Module.Unload can also call it - see that method's doc
            // comment for why unload needs the same cleanup).
            StopLiveTickers();

            _buildPanel = buildPanel;
            int w = buildPanel.ContentRegion.Width;

            // M35: gw2e's own initial state is one empty row
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

            // M33 C2a (directive C): unconditional wheel-recency tracking
            // that StartScrollVerify's yield/suppress logic depends on,
            // plus the pre-existing diagnostic-only tap (gated inside the
            // handler on ScrollDiagnosticsEnabled). _contentPanel is a
            // fresh instance every Build() call, so there is nothing stale
            // to unsubscribe here - the previous cycle's panel (and its
            // subscriptions) is discarded with the previous buildPanel.
            _contentPanel.MouseWheelScrolled += OnContentWheelObserved;
            _contentPanel.MouseWheelScrolled += OnScrollDiagWheelScrolled;

            // Subscribe to resize
            buildPanel.Resized += OnPanelResized;

            if (_currentPlan != null)
            {
                _lastRenderedWidth = w;
                RenderPlan(_currentPlan);
            }
        }

        #endregion // General: view construction (Build) - wires every section/handler together

        #region 5. Resize relayout (continued) - KNOWN-ISSUES #13/#19
        private void OnPanelResized(object sender, ResizedEventArgs e)
        {
            var container = (Container)sender;
            int w = container.ContentRegion.Width;
            int h = container.ContentRegion.Height;

            // M33 C2c: capture the content panel's absolute scroll offset
            // (pixels) and height BEFORE either changes below - see
            // PreserveScrollAcrossResize's doc comment for why this must
            // happen pre-mutation.
            int savedScrollOffset = _contentPanel?.VerticalScrollOffset ?? 0;
            int previousContentHeight = _contentPanel?.Height ?? 0;

            // Update widths of layout panels. Top-strip controls keep their
            // pre-existing direct updates (M33 C2b directive 1) - these were
            // never part of the dispose+rebuild problem the relayout
            // registry below replaces. M35: the input strip is now N rows
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

            // M33 C2c (KNOWN-ISSUES resize-scroll regression): a
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

            // M33 C2b: live in-place relayout, every real drag tick - no
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

            // M33 C2c: the trailing settle pass (re-ellipsis, a defensive
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
        /// M33 C2c: per-tick counterpart to ApplySavedScrollSynchronously
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
        /// M33 C2c: arms StartScrollVerify's existing bounded window once,
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
        /// M33 C2b: replays every registered relayout closure at the given
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
        /// heights stay fixed per M33 C2a), the coalesced reflow is a no-op
        /// for vertical position anyway - SingleTopToBottom flow positions
        /// children from cumulative Height, not Width.
        ///
        /// PERF CAVEAT (KNOWN-ISSUES #13): this replaces a ONE-TIME
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
            // M33 C2b invariant (task directive 6): a pure width/text
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
        /// M33 C2b: the settle-only text-measurement pass. Every relayout
        /// closure already ran (and re-ran) synchronously on every drag
        /// tick via ReplayRelayout; this only re-runs the 3 LabelHelpers.EllipsizeToWidth
        /// call sites' MEASURE work (Used Materials, Shopping List, Tree row
        /// names), since MeasureString is comparatively expensive to run on
        /// every tick across a long list/deep tree and the visible cost of
        /// deferring it (truncated text unchanged mid-drag, corrected once
        /// the drag settles) is small - per M33 C2b directive 2. Neither
        /// this pass nor the defensive ReplayRelayout repeat below ever
        /// changes a row's Height, so - unlike the pre-C2a settle rebuild
        /// this replaces - nothing in RunReellipsis/ReplayRelayout can
        /// perturb scroll position; no PreserveScrollAcross wrapper is
        /// needed around them. M33 C2c: this method also arms the resize
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

            // M33 C2c: bounded to a single window per settled drag (not per
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
        /// M33 C2b: replays every registered re-ellipsis closure - see
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
            // M35 (gw2efficiency parity - multi-item plans): gather every
            // row's selection + quantity into the request list the
            // pipeline needs. Per-row quantity validation mirrors the
            // pre-M35 single-quantity-box behavior exactly (invalid/blank/
            // &lt;1 silently corrected to 1, with a user-visible notice) -
            // just applied once per row instead of once total.
            bool anyQtyInvalid = false;
            var rowInputs = new List<ItemRowRequestBuilder.RowInput>(_itemRows.Count);
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
            }

            var requestItems = ItemRowRequestBuilder.Build(rowInputs);
            if (requestItems.Count == 0)
            {
                // KNOWN-ISSUES 31a-F2: this no-op validation failure must
                // NOT consume a generation-sequence slot. Bumping
                // _generateSequence before this early-return (the previous
                // behavior) would invalidate an in-flight generation's
                // guarded button re-enable (myGen != _generateSequence in
                // its finally below) even though this call never disables
                // or re-enables the button itself - leaving Generate stuck
                // disabled. The button-disable/re-enable pairing below only
                // ever runs once we know a generation will actually start.
                SetStatus("Select at least one item before generating.");
                return;
            }

            // Captured only once we know this call will actually run a
            // generation (past the early-return above). Both entry points
            // that reach here (the Generate button's Click and the modal
            // confirm callback wired in OnOwnMaterialsToggled/ModalDialog)
            // are Blish UI event handlers, so this increment always runs on
            // the main thread before any await - no lock needed, and every
            // deferred callback below reads _generateSequence from the main
            // thread too (inside a MainThreadMarshal.Run callback).
            int myGen = ++_generateSequence;
            _statusClosedForCurrentGeneration = false;

            _generateButton.Enabled = false;
            _lastDebugLog = null;
            SetStatus(anyQtyInvalid
                ? "Quantity was invalid - reset to 1. Generating..."
                : "Generating...");

            // Progress<T> captures the SynchronizationContext at construction
            // time and posts callbacks through it; with none installed (see
            // MainThreadMarshal), the callback runs on a ThreadPool thread,
            // so the SetStatus call must be marshaled. Guarded by myGen so a
            // progress tick from a since-superseded generation cannot
            // overwrite a newer generation's status text.
            var statusProgress = new Progress<PlanStatus>(ps =>
            {
                if (ps != null && !string.IsNullOrEmpty(ps.Message))
                {
                    MainThreadMarshal.Run(() =>
                    {
                        // M34-B1 #4: rechecked at drain time (not queue
                        // time) - a trailing tick from this same generation
                        // must not overwrite a completion status that
                        // generation has already written, however the two
                        // callbacks actually happened to drain relative to
                        // each other. See StatusUpdateGuard.
                        if (!StatusUpdateGuard.ShouldApply(myGen, _generateSequence, _statusClosedForCurrentGeneration)) return;
                        SetStatus(ps.Message);
                    });
                }
            });

            try
            {
                var result = await _generateAsync(
                    requestItems, _useOwnMaterials, _priceBasis,
                    CancellationToken.None, statusProgress);

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
                    _nodeOverrides.Clear();
                    _ignoredItemIds.Clear();
                    _nodeExpansion.Clear();
                    _sectionExpansion.Clear();
                    _lastResult = result;
                    _lastDebugLog = result.DebugLog;
                    _currentPlan = vm;
                    _planGeneratedAt = DateTime.Now;

                    // The view may have been torn down (tab switched away,
                    // module disabled) while generation was in flight - a
                    // disposed control's Parent is nulled on disposal (see
                    // ResizeDebounceStep) - nothing left to render into.
                    if (_contentPanel == null || _contentPanel.Parent == null) return;

                    _lastRenderedWidth = _contentPanel.Width;
                    RenderPlan(vm);
                    // M34-B1 #4: close this generation's status stream
                    // right before writing its completion text, so any
                    // trailing progress tick for this same generation that
                    // drains AFTER this point (StatusUpdateGuard) is
                    // dropped instead of overwriting it.
                    _statusClosedForCurrentGeneration = true;
                    SetStatus($"Plan generated - {_planGeneratedAt:MMM d, yyyy h:mm tt}");
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

                    if (_contentPanel == null || _contentPanel.Parent == null) return;
                    // M34-B1 #4: see the matching comment on the success
                    // path above.
                    _statusClosedForCurrentGeneration = true;
                    SetStatus($"Error: {ex.Message}");
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
                    if (_contentPanel == null || _contentPanel.Parent == null) return;
                    _generateButton.Enabled = true;
                });
            }
        }

        #endregion // 2. Generate orchestration (continued)

        #region General: current panel width helper

        /// <summary>
        /// The content panel's LIVE usable width (RightEdgePadding already
        /// subtracted). M33 C2b: OnPanelResized updates _contentPanel's own
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
        private void RenderPlan(PlanViewModel vm)
        {
            if (_contentPanel == null) return;

            // Drop tree states up front so a plan without a tree section
            // does not retain disposed controls from the previous render.
            _treeNodeStates.Clear();
            _treeRoots = null;
            _treeFlow = null;

            // M33 C2b: the relayout/re-ellipsis registries are rebuilt from
            // scratch alongside every other per-render state above - same
            // lifecycle as _treeNodeStates. Every closure captures controls
            // from the render about to happen below; nothing here can
            // outlive the dispose loop that follows.
            _relayoutActions.Clear();
            _reellipsisActions.Clear();

            foreach (var child in _contentPanel.Children.ToArray())
            {
                child.Dispose();
            }

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

            // M35 (gw2efficiency parity - multi-item plans): a multi-item
            // batch supplies N roots directly (vm.MultiItemRoots); a
            // single-item plan is wrapped into a one-element list here so
            // CreateTreeSection/RefreshTreeContainerHeights always deal
            // with "a list of roots" - one root renders byte-identically to
            // the pre-M35 single-tree path (see PlanContentHeightMath.
            // MultiRootTreeFlowHeight's own doc comment).
            List<CraftingTreeNode> treeRoots = vm.MultiItemRoots != null && vm.MultiItemRoots.Count > 0
                ? vm.MultiItemRoots
                : (vm.TreeRoot != null ? new List<CraftingTreeNode> { vm.TreeRoot } : null);
            if (treeRoots != null)
            {
                CreateTreeSection(treeRoots, panelWidth);
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

            string tsText = $"Generated: {_planGeneratedAt:MMM d, yyyy h:mm tt}";
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

            // M33 C2b: every measured width here (prefixWidth, nameWidth,
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
            // M36: 2px, bottom-anchored inside the 30px headerPanel
            // (Location.Y = 28, i.e. headerPanel.Height - 2) - see
            // LabelHelpers.CreateRowDivider's doc comment for why 1px is unsafe under
            // Blish's non-integer UI-scale GPU transform (KNOWN-ISSUES #23).
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

            // M33 C2a (directive A): Standard (explicit) height, not
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

            // M33 C2b: shared chrome relayout for every section (and the
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
            var header = CreateSectionHeader(section.Title, section.SectionType, panelWidth, section.IsDefaultExpanded);
            var contentFlow = header.ContentFlow;

#if DEBUG
            // M33 C2b (m2 risk 3): a section type added later without
            // registering its own width relayout would silently freeze at
            // build-time width on every future resize - labels just stop
            // moving, easy to miss in review. Fail loud in DEBUG builds
            // instead of relying on call-site discipline alone.
            int relayoutCountBeforeBody = _relayoutActions.Count;
#endif

            // Every section gets its own table-column layout (spec: aligned
            // columns everywhere, not free-flowing text rows), so each has a
            // dedicated body builder rather than a generic per-row dispatch.
            switch (section.SectionType)
            {
                case PlanSectionType.Summary:
                    CreateSummarySectionBody(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.UsedMaterials:
                    // M38 WP-23b: row rendering moved to
                    // Views/Rendering/UsedMaterialsSectionRenderer.
                    new UsedMaterialsSectionRenderer(this).Render(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.ShoppingList:
                    CreateShoppingListBody(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.CraftingSteps:
                    CreateCraftingStepsBody(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.RequiredDisciplines:
                    // M38 WP-23: row rendering moved to
                    // Views/Rendering/DisciplinesSectionRenderer; the shared
                    // c-table header stays here (see that class's doc
                    // comment - it is also used by CreateRecipesBody below).
                    CreateCTableHeaderRow(contentFlow, panelWidth, "Discipline", 8, "Level");
                    new DisciplinesSectionRenderer(this).Render(section, contentFlow, panelWidth);
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

#if DEBUG
            if (section.Rows.Count > 0 && _relayoutActions.Count == relayoutCountBeforeBody)
            {
                Logger.Warn(
                    "M33 C2b: section {0} rendered {1} row(s) but its body registered no relayout closures - it will not track live window resize. See CreateCollapsibleSection.",
                    section.SectionType, section.Rows.Count);
            }
#endif

            // M33 C2a (directive A): finalize contentFlow's real height
            // synchronously now that every row is populated, instead of
            // leaving it to Blish's per-frame AutoSize convergence. Pure
            // function of the same section data just rendered above, so it
            // cannot drift from what was actually built.
            contentFlow.Size = new Point(panelWidth, PlanContentHeightMath.SectionBodyHeight(section.SectionType, section.Rows));
        }

        #endregion // 7. Section builders

        #region 7. Section builders (continued)

        // --- Used Materials section ---
        //
        // M38 WP-23b: row rendering moved to
        // Views/Rendering/UsedMaterialsSectionRenderer (see the
        // RequiredDisciplines-style call in CreateCollapsibleSection above).

        // --- Shopping List section ---

        // Right-aligned price columns for the shopping list's Each and
        // Total prices: both anchor to a fixed right edge and grow
        // LEFTWARD, so a gold-value amount in either column can never grow
        // into the other's space. Previously each column reserved a fixed
        // width (150/90) regardless of content; a 3+ digit gold value in
        // Each or Total could still exceed its fixed band and bleed into
        // the Amount column to its left. Column widths are now derived from
        // the actual widest rendered value per column, clamped to those
        // same fixed minimums so short/low-value lists don't look cramped -
        // see ShoppingColumnMath (Blish-free, unit-tested arithmetic).
        private void CreateShoppingListBody(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            var coinFont = GameService.Content.DefaultFont14;

            // Pre-scan: widest actual coin+currency value width per column
            // this render (CoinCurrencyRenderer.MeasureValueWidth accounts for a currency-only
            // or mixed row's icon(s) too, not just coin - KNOWN-ISSUES
            // #16). One pass over the section's rows (shopping lists run to
            // maybe 50-60 rows in practice) - negligible next to the
            // per-row control creation this method already does.
            int maxEachWidth = 0;
            int maxTotalWidth = 0;
            foreach (var row in section.Rows)
            {
                int eachW = CoinCurrencyRenderer.MeasureValueWidth(row.UnitCoinValue, row.UnitCurrencyCosts, coinFont);
                if (eachW > maxEachWidth) maxEachWidth = eachW;

                int totalW = CoinCurrencyRenderer.MeasureValueWidth(row.CoinValue, row.CurrencyCosts, coinFont);
                if (totalW > maxTotalWidth) maxTotalWidth = totalW;
            }

            int totalRightEdge = panelWidth - 8;
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge, maxEachWidth, maxTotalWidth);

            // Both the header and every data row are handed this SAME
            // ColumnEdges instance (for the build), and the same cached
            // maxEachWidth/maxTotalWidth (for their relayout closures) - a
            // relayout tick re-invokes ShoppingColumnMath.ComputeEdges with
            // the new panelWidth but these SAME data-derived maxima (M33
            // C2b: the pre-scan above depends only on row data, never on
            // panelWidth, so it does not need to re-run on resize at all).
            CreateShoppingListHeaderRow(contentFlow, panelWidth, edges, maxEachWidth, maxTotalWidth);
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateShoppingRow(section.Rows[i], contentFlow, panelWidth, edges, maxEachWidth, maxTotalWidth, i == section.Rows.Count - 1);
            }
        }

        private void CreateShoppingListHeaderRow(
            FlowPanel parent, int panelWidth, ShoppingColumnMath.ColumnEdges edges, int maxEachWidth, int maxTotalWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, PlanContentHeightMath.ShoppingHeaderRowHeight),
                Parent = parent
            };
            var font = GameService.Content.DefaultFont12;
            var color = new Color(153, 153, 153);

            new Label()
            {
                Text = "Item", Font = font, TextColor = color,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(50, 4), Parent = rowPanel
            };
            var amountLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Amount", font, color, edges.QtyRightEdge, 4);
            var eachLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Each", font, color, edges.EachRightEdge, 4);
            var totalLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, "Total", font, color, edges.TotalRightEdge, 4);

            // M33 C2b: header column labels are font-only (fixed text) -
            // pure reposition on every drag tick, recomputing edges from
            // the SAME cached maxEachWidth/maxTotalWidth ComputeEdges was
            // built with (ShoppingColumnMath is the single source of truth
            // both paths call).
            _relayoutActions.Add(w =>
            {
                rowPanel.Size = new Point(w, PlanContentHeightMath.ShoppingHeaderRowHeight);
                var e = ShoppingColumnMath.ComputeEdges(w - 8, maxEachWidth, maxTotalWidth);
                amountLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.QtyRightEdge, amountLabel.Width), 4);
                eachLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.EachRightEdge, eachLabel.Width), 4);
                totalLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(e.TotalRightEdge, totalLabel.Width), 4);
            });
        }

        private static string ShoppingSourceTag(PlanRowViewModel row)
        {
            switch (row.RowType)
            {
                case PlanRowType.ShoppingVendor: return "VENDOR";
                case PlanRowType.ShoppingCurrency: return "CURRENCY";
                case PlanRowType.ShoppingUnknown:
                    // Prefer the seeded wiki hint's badge (e.g. "SALVAGE",
                    // "EXPLORE") when one exists - "UNKNOWN" remains the
                    // fallback for no-source items with no seeded hint.
                    return !string.IsNullOrEmpty(row.BadgeText) ? row.BadgeText : "UNKNOWN";
                default: return null; // ShoppingBuy: plain TP purchase, no tag needed
            }
        }

        private void CreateShoppingRow(
            PlanRowViewModel row, FlowPanel parent, int panelWidth, ShoppingColumnMath.ColumnEdges edges,
            int maxEachWidth, int maxTotalWidth, bool isLast)
        {
            const int rowHeight = PlanContentHeightMath.ShoppingRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            // M36: y=0 (was 1) - see the identical note in
            // CreateUsedMaterialRow; same 36px rowHeight / 34px icon frame
            // shape, same 1px shortfall against the new 2px divider.
            IconControls.CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 8, 0);

            const int nameX = 50;
            var font = GameService.Content.DefaultFont14;

            string qtyText = $"{row.Quantity}x";
            int qtyWidth = (int)System.Math.Ceiling(font.MeasureString(qtyText).Width);
            int nameMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(edges.QtyRightEdge, qtyWidth, 12, nameX);

            string fullName = row.Label ?? "";
            string hintText = row.HintText;
            string displayName = LabelHelpers.EllipsizeToWidth(font, fullName, nameMaxWidth);
            var nameLabel = new Label()
            {
                Text = displayName,
                Font = font,
                TextColor = RarityColors.GetRarityNameColor(row.Rarity),
                ShowShadow = true,
                ShadowColor = Color.Black * 0.8f,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(nameX, 9),
                Parent = rowPanel
            };
            var tooltipParts = new List<string>();
            if (displayName != fullName)
            {
                tooltipParts.Add(fullName);
            }
            if (!string.IsNullOrEmpty(hintText))
            {
                tooltipParts.Add(hintText);
            }
            // M34-B2b: owned/needed split for this row's currency cost(s),
            // cosmetic-only tooltip (avoids new inline layout math for a
            // fixed-height shopping row - see PlanContentHeightMath).
            if (row.CurrencyCosts != null)
            {
                foreach (var cc in row.CurrencyCosts)
                {
                    if (cc.OwnedQuantity.HasValue)
                    {
                        long needed = cc.Amount - cc.OwnedQuantity.Value;
                        tooltipParts.Add($"{cc.Name}: {cc.OwnedQuantity.Value} owned, {needed} needed");
                    }
                }
            }
            if (tooltipParts.Count > 0)
            {
                rowPanel.BasicTooltipText = string.Join("\n", tooltipParts);
            }

            string sourceTag = ShoppingSourceTag(row);
            Panel tagPanel = null;
            if (!string.IsNullOrEmpty(sourceTag))
            {
                GetPillColors(PillKind.Locked, false, out Color tagBorder, out Color tagFill);
                tagPanel = LabelHelpers.CreateSmallTag(
                    rowPanel, sourceTag, nameX + nameLabel.Width + 8, 9, tagBorder, tagFill);
            }

            var qtyLabel = new Label()
            {
                Text = qtyText,
                Font = font,
                TextColor = new Color(200, 200, 200),
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(edges.QtyRightEdge - qtyWidth, 9),
                Parent = rowPanel
            };

            // Each/Total cells: coin-only rows render exactly as before;
            // a row priced wholly or partly in a non-coin currency (e.g. a
            // vendor offer paid in spirit shards) renders currency segments
            // alongside/instead of coin; a row with neither (genuinely
            // unpriceable - gw2e: "Not sold or crafted") renders a dash,
            // never a blank cell (KNOWN-ISSUES #16).
            var eachCell = CoinCurrencyRenderer.RenderValueCellRightAligned(rowPanel, row.UnitCoinValue, row.UnitCurrencyCosts, edges.EachRightEdge, 9, font);
            var totalCell = CoinCurrencyRenderer.RenderValueCellRightAligned(rowPanel, row.CoinValue, row.CurrencyCosts, edges.TotalRightEdge, 9, font);

            // M36b: bottomClearance 0 - ShoppingRowHeight (36) is immune to
            // the Container.Paint round-trip defect (see LabelHelpers.CreateRowDivider's
            // doc comment) and its icon frame is flush-fit with zero
            // slack; see the identical note in CreateUsedMaterialRow.
            Panel divider = isLast ? null : LabelHelpers.CreateRowDivider(rowPanel, panelWidth, rowHeight, 0);

            // M33 C2b: qty + Each/Total cells reposition every drag tick
            // (no MeasureString - CoinCurrencyRenderer.RepositionValueCellRightAligned uses only
            // cached segment text widths). The name label and its source
            // tag are untouched here; both depend on ellipsis truncation
            // and only update at settle (RunReellipsis) below.
            _relayoutActions.Add(w =>
            {
                var e = ShoppingColumnMath.ComputeEdges(w - 8, maxEachWidth, maxTotalWidth);
                rowPanel.Size = new Point(w, rowHeight);
                qtyLabel.Location = new Point(e.QtyRightEdge - qtyWidth, 9);
                CoinCurrencyRenderer.RepositionValueCellRightAligned(eachCell, e.EachRightEdge, 9);
                CoinCurrencyRenderer.RepositionValueCellRightAligned(totalCell, e.TotalRightEdge, 9);
                if (divider != null) divider.Size = new Point(w, 2);
            });
            _reellipsisActions.Add(w =>
            {
                var e = ShoppingColumnMath.ComputeEdges(w - 8, maxEachWidth, maxTotalWidth);
                int newMaxWidth = PlanRelayoutMath.NameMaxWidthBeforeColumn(e.QtyRightEdge, qtyWidth, 12, nameX);
                string newDisplayName = LabelHelpers.EllipsizeToWidth(font, fullName, newMaxWidth);
                if (nameLabel.Text != newDisplayName)
                {
                    nameLabel.Text = newDisplayName;
                    var parts = new List<string>();
                    if (newDisplayName != fullName) parts.Add(fullName);
                    if (!string.IsNullOrEmpty(hintText)) parts.Add(hintText);
                    rowPanel.BasicTooltipText = parts.Count > 0 ? string.Join("\n", parts) : null;
                }
                if (tagPanel != null)
                {
                    tagPanel.Location = new Point(nameX + nameLabel.Width + 8, 9);
                }
            });
        }

        // --- Crafting Steps section ---

        private void CreateCraftingStepsBody(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            // M34-B1 #3: a TimegatedNotice row (vendor-cap informational
            // line) is a plain text row, not a numbered craft step - render
            // it via the same generic CreateTextRow pattern every other
            // section's fallback rows use, and don't consume a step number
            // for it (stepNumber only advances for real CraftStep rows).
            int stepNumber = 1;
            for (int i = 0; i < section.Rows.Count; i++)
            {
                var row = section.Rows[i];
                bool isLast = i == section.Rows.Count - 1;
                if (row.RowType == PlanRowType.TimegatedNotice)
                {
                    CreateTextRow(row.Label, contentFlow, panelWidth);
                }
                else
                {
                    CreateCraftStepRow(row, stepNumber++, contentFlow, panelWidth, isLast);
                }
            }
        }

        private void CreateCraftStepRow(
            PlanRowViewModel row, int stepNumber, FlowPanel parent, int panelWidth, bool isLast)
        {
            const int rowHeight = PlanContentHeightMath.CraftStepRowHeight;
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

            IconControls.CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, iconX, 5);

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
                Text = row.Label ?? "", Font = textFont, TextColor = RarityColors.GetRarityNameColor(row.Rarity),
                ShowShadow = true, ShadowColor = Color.Black * 0.8f,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(x, 13), Parent = rowPanel
            };

            Label sublabelLabel = null;
            if (!string.IsNullOrEmpty(row.Sublabel))
            {
                sublabelLabel = LabelHelpers.CreateRightAlignedLabel(
                    rowPanel, row.Sublabel, GameService.Content.DefaultFont12,
                    new Color(153, 153, 153), panelWidth - 8, 16);
            }

            // M36b: bottomClearance 1 - CraftStepRowHeight (44) is
            // VULNERABLE to the Container.Paint round-trip defect (see
            // LabelHelpers.CreateRowDivider's doc comment): its icon frame bottom
            // (iconY 5 + 34 = 39) sits 2px clear of the new divider top
            // (rowHeight-3 = 41), so the 1px shift is free of
            // icon-clearance side effects.
            Panel divider = isLast ? null : LabelHelpers.CreateRowDivider(rowPanel, panelWidth, rowHeight, 1);

            // M33 C2b: name/qty labels sit at a fixed x (font-only, not
            // width-dependent - textX never depended on panelWidth); only
            // the row width, its divider, and the right-aligned sublabel
            // need to move.
            _relayoutActions.Add(w =>
            {
                rowPanel.Size = new Point(w, rowHeight);
                if (sublabelLabel != null)
                {
                    sublabelLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, sublabelLabel.Width), 16);
                }
                if (divider != null) divider.Size = new Point(w, 2);
            });
        }

        // --- Required Disciplines / Required Recipes sections (c-table) ---

        private void CreateCTableHeaderRow(
            FlowPanel parent, int panelWidth, string leftLabel, int leftX, string rightLabel)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, PlanContentHeightMath.CTableHeaderRowHeight),
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
            var rightLabelControl = LabelHelpers.CreateRightAlignedLabel(rowPanel, rightLabel, font, Color.White, panelWidth - 8, 5);

            _relayoutActions.Add(w =>
            {
                rowPanel.Size = new Point(w, PlanContentHeightMath.CTableHeaderRowHeight);
                rightLabelControl.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, rightLabelControl.Width), 5);
            });
        }

        private void CreateRecipesBody(PlanSectionViewModel section, FlowPanel contentFlow, int panelWidth)
        {
            CreateCTableHeaderRow(contentFlow, panelWidth, "Recipe", 50, "Status");
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateRecipeRow(section.Rows[i], contentFlow, panelWidth, i == section.Rows.Count - 1);
            }
        }

        // M36 fix-pass (MUSTFIX-3): the no-sublabel branch's rowHeight (32)
        // left the RarityFramedIconOuterSize (34) icon frame at y=1
        // overflowing rowHeight by 3px even BEFORE the M36 divider-width
        // change (icon bottom = 1 + 34 = 35, rowHeight = 32) - pre-existing
        // negative headroom, not "several pixels of headroom" as
        // KNOWN-ISSUES #23 previously (incorrectly) claimed for this row,
        // and made 1px worse once that row's divider grew from 1px to 2px
        // (needed 34 + 2 = 36 to sit flush, still only had 32). Fixed
        // coherently, mirroring the Used Materials/Shopping List pattern
        // already on this branch: RecipeRowHeightNoSublabel raised to 36
        // (icon at y=0, 34 tall, + the 2px divider = exact fit, zero
        // overlap) and this branch's icon y nudged from 1 to 0 to match.
        // The WithSublabel branch (44) already had ample headroom and is
        // unchanged.
        private void CreateRecipeRow(PlanRowViewModel row, FlowPanel parent, int panelWidth, bool isLast)
        {
            bool hasSublabel = !string.IsNullOrEmpty(row.Sublabel);
            int rowHeight = hasSublabel
                ? PlanContentHeightMath.RecipeRowHeightWithSublabel
                : PlanContentHeightMath.RecipeRowHeightNoSublabel;

            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            IconControls.CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 8, hasSublabel ? 1 : 0);

            var font = GameService.Content.DefaultFont14;
            int nameY = hasSublabel ? 4 : 8;
            new Label()
            {
                Text = row.Label ?? "",
                Font = font,
                TextColor = RarityColors.GetRarityNameColor(row.Rarity),
                ShowShadow = true,
                ShadowColor = Color.Black * 0.8f,
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

            Label statusLabel = null;
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
                statusLabel = LabelHelpers.CreateRightAlignedLabel(rowPanel, row.StatusTag, font, statusColor, panelWidth - 8, hasSublabel ? 10 : 8);
            }

            // M36b: bottomClearance depends on which rowHeight this branch
            // used. hasSublabel (44px, RecipeRowHeightWithSublabel) is
            // VULNERABLE to the Container.Paint round-trip defect (see
            // LabelHelpers.CreateRowDivider's doc comment) - icon frame bottom (1 + 34 =
            // 35) leaves ample headroom below rowHeight-3 (41). The
            // no-sublabel branch (36px, RecipeRowHeightNoSublabel) is
            // immune and flush-fit with zero slack (M36); giving it
            // clearance it doesn't need would reintroduce that overlap.
            Panel divider = isLast ? null : LabelHelpers.CreateRowDivider(rowPanel, panelWidth, rowHeight, hasSublabel ? 1 : 0);

            _relayoutActions.Add(w =>
            {
                rowPanel.Size = new Point(w, rowHeight);
                if (statusLabel != null)
                {
                    statusLabel.Location = new Point(PlanRelayoutMath.RightAlignedX(w - 8, statusLabel.Width), hasSublabel ? 10 : 8);
                }
                if (divider != null) divider.Size = new Point(w, 2);
            });
        }

        /// <summary>
        /// gw2e's cost-breakdown: a centered row of equal-width stat tiles,
        /// one per CoinTotal row (Total, Sell value, Profit/Loss - up to the
        /// spec's 5 when all are applicable). Non-coin rows (currency costs)
        /// are handled separately as full-width rows underneath.
        /// </summary>
        /// <summary>
        /// One tile's already-created controls, cached for relayout - m2
        /// 3.5's [FANOUT] case: unlike a single-anchor row, every tile's
        /// caption AND coin segments are independently re-centered inside
        /// their own tileWidth-wide slice on every drag tick.
        /// </summary>
        private sealed class CostTileHandle
        {
            public Label CaptionLabel;
            public CoinCurrencyRenderer.SegmentLayoutHandle Segments;
        }

        private void CreateCostTileRow(List<PlanRowViewModel> coinRows, FlowPanel parent, int panelWidth)
        {
            int tileCount = coinRows.Count;
            if (tileCount == 0) return;

            const int rowHeight = PlanContentHeightMath.CostTileRowHeight;
            const int totalMargin = 40;
            const int minTileWidth = 80;
            var geometry = PlanRelayoutMath.ComputeCostTileGeometry(panelWidth, tileCount, totalMargin, minTileWidth);

            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, rowHeight),
                Parent = parent
            };

            var captionFont = GameService.Content.DefaultFont12;
            var amountFont = GameService.Content.DefaultFont16;
            var captionColor = new Color(153, 153, 153);

            var tiles = new List<CostTileHandle>(tileCount);
            for (int i = 0; i < tileCount; i++)
            {
                int tileX = geometry.StartX + i * geometry.TileWidth;
                var row = coinRows[i];

                string caption = TileCaptionFor(row.Label);
                int captionWidth = (int)System.Math.Ceiling(captionFont.MeasureString(caption).Width);
                var captionLabel = new Label()
                {
                    Text = caption,
                    Font = captionFont,
                    TextColor = captionColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(tileX + PlanRelayoutMath.CenterX(geometry.TileWidth, captionWidth), 6),
                    Parent = rowPanel
                };

                var segments = CoinCurrencyRenderer.BuildCoinSegments(row.CoinValue, amountFont);
                int segmentsWidth = CoinCurrencyRenderer.TotalCoinSegmentsWidth(segments);
                int coinStartX = tileX + PlanRelayoutMath.CenterX(geometry.TileWidth, segmentsWidth);
                var segmentHandle = CoinCurrencyRenderer.LayoutCoinSegments(rowPanel, segments, coinStartX, 30, amountFont);

                tiles.Add(new CostTileHandle { CaptionLabel = captionLabel, Segments = segmentHandle });
            }

            // M33 C2b [FANOUT]: every tile's caption + coin segments are
            // font-only (invariant to panelWidth) - only tileWidth/startX
            // and each tile's own centering offset move. No MeasureString.
            _relayoutActions.Add(w =>
            {
                rowPanel.Size = new Point(w, rowHeight);
                var g = PlanRelayoutMath.ComputeCostTileGeometry(w, tileCount, totalMargin, minTileWidth);
                for (int i = 0; i < tiles.Count; i++)
                {
                    int tileX = g.StartX + i * g.TileWidth;
                    var tile = tiles[i];

                    tile.CaptionLabel.Location = new Point(tileX + PlanRelayoutMath.CenterX(g.TileWidth, tile.CaptionLabel.Width), 6);

                    int segmentsWidth = ShoppingColumnMath.SegmentRunWidth(tile.Segments.TextWidths, CoinSegmentMath.CoinIconSize, CoinSegmentMath.CoinLabelIconGap, CoinSegmentMath.CoinSegmentGap);
                    int coinStartX = tileX + PlanRelayoutMath.CenterX(g.TileWidth, segmentsWidth);
                    CoinCurrencyRenderer.RepositionSegments(tile.Segments, coinStartX, 30);
                }
            });
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
            var noteRows = new List<PlanRowViewModel>();
            foreach (var row in section.Rows)
            {
                if (row.RowType == PlanRowType.CoinTotal) coinRows.Add(row);
                // M35 (gw2efficiency parity - multi-item plans): the
                // multi-item batch note is a plain text row, not a
                // CurrencyCost row - must not fall into the CreateCurrencyRow
                // branch below (which assumes an icon/quantity that a note
                // row never has).
                else if (row.RowType == PlanRowType.MultiItemNote) noteRows.Add(row);
                else otherRows.Add(row);
            }

            if (coinRows.Count > 0)
            {
                CreateCostTileRow(coinRows, contentFlow, panelWidth);
            }

            // The only other row type in this section is CurrencyCost.
            foreach (var row in otherRows)
            {
                CreateCurrencyRow(row, contentFlow, panelWidth);
            }

            foreach (var row in noteRows)
            {
                CreateTextRow(row.Label, contentFlow, panelWidth);
            }
        }

        private void CreateTextRow(string text, FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, PlanContentHeightMath.FallbackTextRowHeight),
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

            // Not width-dependent beyond the row's own cosmetic width (fixed
            // left-anchored text, m2 3.6's "no relayout needed" case).
            _relayoutActions.Add(w => rowPanel.Size = new Point(w, PlanContentHeightMath.FallbackTextRowHeight));
        }

        // Sized between the tree/row item-icon (32px) and the coin-segment
        // icon (20px) since it sits inside a plain 28px text row; reuses
        // CoinSegmentMath.CoinLabelIconGap (M38 WP-21 findings fix: moved out of
        // CoinCurrencyRenderer) for the text-to-icon gap so both follow the
        // same "number/text first, gap, icon" convention.
        private const int CurrencyRowHeight = PlanContentHeightMath.CurrencyRowHeight;
        private const int CurrencyIconSize = 18;

        /// <summary>
        /// CurrencyCost row: identical "  {label}" text to CreateTextRow,
        /// plus the currency's icon immediately to its right when known.
        /// IconUrl null (no data available - service not wired up, fetch
        /// not yet complete, or the currency was absent from the API
        /// response) renders exactly like CreateTextRow - never a
        /// placeholder guess for a missing icon. When CurrencyOwnedQuantity
        /// is set (M34-B2b, wallet data present), an "(X owned, Y needed)"
        /// annotation follows the icon - gw2e's ownedCurrencies/
        /// shoppingCurrencies split (r2 report Section 4.3), cosmetic only.
        /// </summary>
        private void CreateCurrencyRow(PlanRowViewModel row, FlowPanel parent, int panelWidth)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(panelWidth, CurrencyRowHeight),
                Parent = parent
            };
            var label = new Label()
            {
                Text = "  " + row.Label,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(8, 4),
                Parent = rowPanel
            };

            int cursorX = 8 + label.Width;
            if (!string.IsNullOrEmpty(row.IconUrl))
            {
                int iconX = cursorX + CoinSegmentMath.CoinLabelIconGap;
                int iconY = (CurrencyRowHeight - CurrencyIconSize) / 2;
                IconControls.CreateItemIcon(rowPanel, row.IconUrl, iconX, iconY, CurrencyIconSize);
                cursorX = iconX + CurrencyIconSize;
            }

            if (row.CurrencyOwnedQuantity.HasValue)
            {
                int needed = row.Quantity - row.CurrencyOwnedQuantity.Value;
                new Label()
                {
                    Text = $"({row.CurrencyOwnedQuantity.Value} owned, {needed} needed)",
                    TextColor = new Color(153, 153, 153),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(cursorX + CoinSegmentMath.CoinLabelIconGap, 4),
                    Parent = rowPanel
                };
            }

            // Not width-dependent beyond the row's own cosmetic width (m2
            // 3.6): label/icon/owned-annotation sit at a fixed left-anchored
            // x regardless of panelWidth.
            _relayoutActions.Add(w => rowPanel.Size = new Point(w, CurrencyRowHeight));
        }

        #endregion // 7. Section builders (continued)

        #region 8. Tree rendering (continued)

        // --- Recipe tree section ---

        private class TreeNodeState
        {
            public bool ChildrenBuilt;
            public bool IsExpanded;
            public FlowPanel ChildContainer;
            public Label ArrowLabel;
            public CraftingTreeNode Node;
            public int Depth;

            // M33 C2b: PanelWidth removed - a captured build-time width
            // would go stale once resize no longer triggers a full rebuild
            // (see GetCurrentPanelWidth, which every remaining reader of
            // "current tree width" uses instead).

            // Whether lazily-built children (built on first expand) should
            // render dimmed - computed once from this node's own dimmed
            // state and decision, so it stays correct however many frames
            // later the user actually expands the node.
            public bool ChildDimmed;
        }

        // States for the current render pass; rebuilt with the tree itself.
        private readonly List<TreeNodeState> _treeNodeStates = new List<TreeNodeState>();

        // Root nodes + top-level content FlowPanel for the current render's
        // Recipe Tree section (null when the plan has no tree). Held so
        // RefreshTreeContainerHeights - called from the tree row toggle
        // handler deep inside RenderTreeNode's recursion, as well as from
        // CreateTreeSection itself - can recompute treeFlow's own explicit
        // Height without threading both through every recursive call. M35
        // (gw2efficiency parity - multi-item plans): a single-item plan
        // still populates this with exactly one root, so every consumer
        // below is unchanged in that case (see MultiRootTreeFlowHeight's
        // own doc comment for the "N==1 is byte-identical" guarantee).
        private List<CraftingTreeNode> _treeRoots;
        private FlowPanel _treeFlow;

        /// <summary>
        /// Renders the Recipe Tree section's single shared content
        /// FlowPanel: one root per requested item, stacked - N top-level
        /// trees for a multi-item batch (the synthetic wrapper root never
        /// surfacing - see CraftingPlanResult.MultiItemRoots' own doc
        /// comment), or the familiar single tree when treeRoots has one
        /// element. Each root node already carries its own full icon/name/
        /// quantity/pill/cost row (RenderTreeNode), so no separate
        /// per-root header row is needed - gw2e's own "N independent
        /// top-level recipe trees" look falls out for free.
        /// </summary>
        private void CreateTreeSection(IReadOnlyList<CraftingTreeNode> treeRoots, int panelWidth)
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
            _treeRoots = treeRoots as List<CraftingTreeNode> ?? new List<CraftingTreeNode>(treeRoots);
            _treeFlow = treeFlow;

            // Header-row buttons, right-to-left per the spec's fixed
            // offsets-from-the-right layout: Collapse All, Expand All, then
            // the presets (Buy All / Craft All / Best Path) continuing
            // leftward with 4px gaps so they never collide with the title.
            int cursorX = panelWidth;
            var headerButtons = new List<(StandardButton Button, int Width)>(5);
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
                headerButtons.Add((button, width));
                cursorX -= 4;
                return button;
            }

            collapseAllButton = PlaceButtonRight("Collapse All", 96);
            expandAllButton = PlaceButtonRight("Expand All", 92);
            buyAllButton = PlaceButtonRight("Buy All", 70);
            craftAllButton = PlaceButtonRight("Craft All", 76);
            bestPathButton = PlaceButtonRight("Best Path", 80);

            // M33 C2b: right-to-left button placement is font-only (fixed
            // widths) - pure reposition on every drag tick, same order as
            // PlaceButtonRight built them so the right-to-left offsets
            // reproduce identically.
            _relayoutActions.Add(w =>
            {
                int x = w;
                foreach (var (button, width) in headerButtons)
                {
                    x -= width;
                    button.Location = new Point(x, 3);
                    x -= 4;
                }
            });

#if DEBUG
            int relayoutCountBeforeTree = _relayoutActions.Count;
#endif
            // M35 (gw2efficiency parity - multi-item plans): a thin gap
            // between consecutive roots so N stacked full item trees read
            // as N distinct blocks (PlanContentHeightMath.
            // MultiRootDividerHeight) - never inserted for a single root,
            // which keeps that case's rendered rows byte-identical to
            // pre-M35.
            for (int i = 0; i < _treeRoots.Count; i++)
            {
                if (i > 0)
                {
                    var rootDivider = new Panel()
                    {
                        Size = new Point(panelWidth, PlanContentHeightMath.MultiRootDividerHeight),
                        Parent = treeFlow
                    };
                    _relayoutActions.Add(w => rootDivider.Size = new Point(w, PlanContentHeightMath.MultiRootDividerHeight));
                }
                RenderTreeNode(_treeRoots[i], treeFlow, panelWidth, 0, dimmed: false);
            }
#if DEBUG
            // M33 C2b (m2 risk 3): every RenderTreeNode call registers its
            // own relayout closure (see the field comment on
            // _relayoutActions) - a single root node still yields at least
            // one. Zero growth here would mean that mechanism itself
            // silently broke.
            if (_relayoutActions.Count == relayoutCountBeforeTree)
            {
                Logger.Warn("M33 C2b: Recipe Tree root rendered but registered no relayout closures - it will not track live window resize.");
            }
#endif

            // M33 C2a (directive A): every container this initial build
            // populated (treeFlow plus every childFlow created for a
            // default-expanded node) still reads its construction-time
            // Size.Y of 0 at this point - one synchronous pass now finalizes
            // every one of them from the same PlanContentHeightMath
            // arithmetic the rows above were just laid out with, before
            // this method returns to RenderPlan/PreserveScrollAcross.
            RefreshTreeContainerHeights();

            // Decision presets: clear overrides / force craft-everywhere /
            // force buy-everywhere (feasibility respected by the solver).
            bestPathButton.Click += (_, __) =>
            {
                if (_nodeOverrides.Count == 0) return;
                _nodeOverrides.Clear();
                ApplyOverridesAndResolve(isBestPathPreset: true);
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
                            RenderTreeNode(child, s.ChildContainer, GetCurrentPanelWidth(), s.Depth + 1, s.ChildDimmed);
                        }
                        s.ChildrenBuilt = true;
                    }
                    s.IsExpanded = true;
                    _nodeExpansion[s.Node.NodeId] = true;
                    s.ChildContainer.Visible = true;
                    s.ArrowLabel.Text = "\u25BC";
                }
                RefreshTreeContainerHeights();
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
                RefreshTreeContainerHeights();
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

        // M37 (KNOWN-ISSUES #22/#27): isBestPathPreset must come from which
        // control fired this call, not be inferred from the resulting
        // _nodeOverrides count - see StatusText.ForOverrideResolve for why.
        private void ApplyOverridesAndResolve(bool isBestPathPreset = false)
        {
            if (_lastResult?.SolveContext == null || _resolveOverridesSync == null)
            {
                return;
            }

            try
            {
                var result = _resolveOverridesSync(_lastResult.SolveContext, _nodeOverrides, _ignoredItemIds);
                _lastResult = result;
                _lastDebugLog = result.DebugLog;
                var vm = _vmBuilder.Build(result);
                _currentPlan = vm;
                PreserveScrollAcross(() => RenderPlan(vm));
                SetStatus(StatusText.ForOverrideResolve(isBestPathPreset, _nodeOverrides.Count));
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
        private const int TreeRowHeight = PlanContentHeightMath.TreeRowHeight;
        private const int TreePillColumnWidth = 240;
        private const int TreeCostColumnWidth = 150;
        private const int TreeRightMargin = 8;

        /// <summary>
        /// M33 C2a (directive A): recomputes and re-assigns the explicit
        /// Height of every tree childFlow container plus the top-level
        /// treeFlow, from the SAME PlanContentHeightMath arithmetic used to
        /// build the rows in the first place. Replaces the old
        /// InvalidateUpToContentPanel, which only repositioned siblings and
        /// relied on Blish's AutoSize convergence (one nested level per real
        /// frame) to eventually grow/shrink ancestor containers to match -
        /// the direct cause of #12/#14's multi-frame windows. Setting each
        /// container's Size fires its own Resized event, which FlowPanel
        /// already wires to reflow its own parent's sibling positions (see
        /// ChangedChildOnResized in the vendored FlowPanel source) - so this
        /// call alone both resizes and repositions every affected row,
        /// synchronously, with no separate Invalidate() needed.
        /// Recomputes every node currently in _treeNodeStates rather than
        /// walking only the toggled node's ancestor chain: each computation
        /// is a pure function of that node's own structure + the shared
        /// _nodeExpansion map, independent of any other container's current
        /// state, so recomputing a few unaffected containers alongside the
        /// affected ones is harmless - and this only runs once per user
        /// toggle, never per frame, so the extra work is not a hot-path
        /// concern.
        /// </summary>
        private void RefreshTreeContainerHeights()
        {
            int panelWidth = GetCurrentPanelWidth();
            foreach (var state in _treeNodeStates)
            {
                state.ChildContainer.Size = new Point(
                    panelWidth,
                    PlanContentHeightMath.ChildrenHeight(
                        state.Node.Children, state.Depth + 1, state.ChildDimmed, _nodeExpansion));
            }

            if (_treeRoots != null && _treeRoots.Count > 0 && _treeFlow != null)
            {
                _treeFlow.Size = new Point(
                    panelWidth, PlanContentHeightMath.MultiRootTreeFlowHeight(_treeRoots, _nodeExpansion));
            }
        }

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
            // Non-reference nodes keep the existing depth<2 default. Calls
            // PlanContentHeightMath.IsNodeExpanded (not a hand-duplicated
            // ternary) so this decision and RefreshTreeContainerHeights'
            // height arithmetic share one formula and cannot silently
            // desync - see that method's doc comment.
            bool isExpanded = PlanContentHeightMath.IsNodeExpanded(node.NodeId, depth, dimmed, _nodeExpansion);
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
            Color frameColor = dimmed ? new Color(60, 60, 60) : RarityColors.GetRarityBorderColor(node.Rarity);
            IconControls.CreateRarityFramedIcon(rowPanel, node.IconUrl, frameColor, iconX, 3, TreeIconSize, TreeIconBorder);
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
            // M33 C2b: pillColX/costRightEdge/nameMaxWidth now come from
            // PlanRelayoutMath.ComputeTreeColumnEdges - the SAME pure
            // function the relayout/re-ellipsis closures below call, so the
            // build and every later resize tick can never disagree about
            // these columns.
            int nameX = indent + TreeCaretColWidth + TreeIconFrameSize + TreeNameGap;

            var nameFont = GameService.Content.DefaultFont14;
            string qtyPrefix = node.Quantity > 0 ? $"{node.Quantity}x " : "";
            int qtyWidth = qtyPrefix.Length > 0
                ? (int)System.Math.Ceiling(nameFont.MeasureString(qtyPrefix).Width)
                : 0;

            var edges = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, nameX, qtyWidth, TreePillColumnWidth, TreeCostColumnWidth, TreeRightMargin);
            int pillColX = edges.PillColX;
            int costRightEdge = edges.CostRightEdge;

            string fullName = node.Name ?? "";
            string displayName = LabelHelpers.EllipsizeToWidth(nameFont, fullName, edges.NameMaxWidth);

            Color qtyColor = new Color(170, 170, 170);
            Color nameColor = RarityColors.GetRarityNameColor(node.Rarity);
            if (dimmed)
            {
                qtyColor *= 0.45f;
                // Lift dark hues toward readable before dimming (premultiplied-
                // correct: Lerp opaque colors first, then apply alpha via *).
                nameColor = Color.Lerp(nameColor, Color.White, 0.30f) * 0.50f;
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
            var nameLabel = new Label()
            {
                Text = displayName,
                Font = nameFont,
                TextColor = nameColor,
                ShowShadow = true,
                ShadowColor = dimmed ? Color.Black * 0.4f : Color.Black * 0.8f,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(nameX + qtyWidth, 12),
                Parent = rowPanel
            };

            // M33 C2b: extraTooltipLines never depends on panelWidth (unit
            // price / acquisition hint text is fixed), so it is computed
            // once and reused verbatim by the settle re-ellipsis pass -
            // only the "is the name actually truncated" line needs to be
            // reconsidered when nameMaxWidth changes.
            var extraTooltipLines = new List<string>();
            if (node.UnitCost.HasValue && node.Quantity > 1 &&
                (node.Decision == CraftingDecision.BuyFromTp ||
                 node.Decision == CraftingDecision.BuyFromVendor))
            {
                extraTooltipLines.Add("Unit price: " + CoinCurrencyRenderer.FormatCoinText(node.UnitCost.Value));
            }
            if (node.Decision == CraftingDecision.Unknown && !string.IsNullOrEmpty(node.AcquisitionHint))
            {
                extraTooltipLines.Add(node.AcquisitionHint);
            }
            UpdateTreeRowTooltip(rowPanel, displayName, fullName, extraTooltipLines);

            // Decision pill column: one pill per feasible source (direct
            // selection - click sets the override and re-solves), or a
            // single locked/HAVE/CURRENCY pill when there is no choice.
            var pillPanels = RenderDecisionPills(rowPanel, node, pillColX, 10, dimmed);

            // Cost column: right-aligned so coin amounts line up vertically
            // across every row regardless of digit count. Only rendered
            // when this node has a real committed decision with a cost
            // figure at all (SubtreeCost.HasValue) - HAVE/CURRENCY/UNKNOWN
            // nodes carry no SubtreeCost and keep the column blank exactly
            // as before (their own pill already communicates "no price").
            // Within that: a BuyFromVendor node priced wholly or partly in
            // a non-coin currency renders currency segments alongside/
            // instead of coin (sibling site to the shopping list's #16
            // fix, same CoinCurrencyRenderer.RenderValueCellRightAligned entry point); a
            // decision whose real cost is genuinely zero-and-uncosted
            // renders a dash instead of an invented "0".
            CoinCurrencyRenderer.ValueCellHandle costCell = null;
            if (node.SubtreeCost.HasValue)
            {
                var costFont = GameService.Content.DefaultFont14;
                var currencyAmounts = CurrencyDisplayResolver.ResolveAmounts(
                    node.VendorCurrencyCosts, _currentPlan?.CurrencyMetadata);
                costCell = CoinCurrencyRenderer.RenderValueCellRightAligned(
                    rowPanel, node.SubtreeCost.Value, currencyAmounts, costRightEdge, 12, costFont, dimmed ? 0.35f : 1f);
            }

            // Child container. Children of a non-Craft decision are gw2e's
            // ".not-crafted" informational reference branch (what it would
            // cost to craft instead) - dimmed, and the flag does not stack
            // on already-dimmed branches.
            FlowPanel childFlow = null;
            if (hasChildren)
            {
                bool childDimmed = dimmed || node.Decision != CraftingDecision.Craft;

                // M33 C2a (directive A): Standard (explicit) height, same
                // as the section header's contentFlow - see that
                // construction site's comment. Starts at 0; the caller that
                // ultimately owns this build pass (CreateTreeSection's
                // initial call, or a toggle handler below) finalizes the
                // real height via RefreshTreeContainerHeights before
                // control returns to PreserveScrollAcross's caller.
                childFlow = new FlowPanel()
                {
                    Size = new Point(panelWidth, 0),
                    FlowDirection = ControlFlowDirection.SingleTopToBottom,
                    Parent = parent
                };

                var state = new TreeNodeState
                {
                    Node = node,
                    Depth = depth,
                    ChildContainer = childFlow,
                    ArrowLabel = arrowLabel,
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
                        if (pill.MouseOver)
                        {
                            return;
                        }
                    }
                    PreserveScrollAcross(() =>
                    {
                        if (!state.ChildrenBuilt)
                        {
                            // M33 C2b: read the LIVE width rather than the
                            // (possibly long-stale, since resize no longer
                            // triggers a rebuild) width this node itself was
                            // built at - see GetCurrentPanelWidth.
                            int currentWidth = GetCurrentPanelWidth();
                            foreach (var child in state.Node.Children)
                            {
                                RenderTreeNode(
                                    child, state.ChildContainer, currentWidth, state.Depth + 1, state.ChildDimmed);
                            }
                            state.ChildrenBuilt = true;
                        }
                        state.IsExpanded = !state.IsExpanded;
                        _nodeExpansion[state.Node.NodeId] = state.IsExpanded;
                        state.ChildContainer.Visible = state.IsExpanded;
                        state.ArrowLabel.Text = state.IsExpanded ? "\u25BC" : "\u25B6";
                        RefreshTreeContainerHeights();
                    });
                };
                rowPanel.Click += toggleHandler;
            }

            // M33 C2b: pills/cost cell reposition every drag tick (no
            // MeasureString - pill widths are already-known control Width,
            // CoinCurrencyRenderer.RepositionValueCellRightAligned uses only cached segment text
            // widths); childFlow's width tracks panelWidth with its Height
            // preserved exactly (never perturbs scroll - M33 C2a already
            // made every row/container height explicit). The name label is
            // untouched here; it only re-ellipsizes at settle below.
            _relayoutActions.Add(w =>
            {
                rowPanel.Size = new Point(w, TreeRowHeight);
                var e = PlanRelayoutMath.ComputeTreeColumnEdges(
                    w, nameX, qtyWidth, TreePillColumnWidth, TreeCostColumnWidth, TreeRightMargin);

                if (pillPanels.Count > 0)
                {
                    int x = e.PillColX;
                    foreach (var pill in pillPanels)
                    {
                        pill.Location = new Point(x, 10);
                        x += pill.Width + 6;
                    }
                }
                if (costCell != null)
                {
                    CoinCurrencyRenderer.RepositionValueCellRightAligned(costCell, e.CostRightEdge, 12);
                }
                if (childFlow != null)
                {
                    childFlow.Size = new Point(w, childFlow.Height);
                }
            });
            _reellipsisActions.Add(w =>
            {
                var e = PlanRelayoutMath.ComputeTreeColumnEdges(
                    w, nameX, qtyWidth, TreePillColumnWidth, TreeCostColumnWidth, TreeRightMargin);
                string newDisplayName = LabelHelpers.EllipsizeToWidth(nameFont, fullName, e.NameMaxWidth);
                if (nameLabel.Text != newDisplayName)
                {
                    nameLabel.Text = newDisplayName;
                    UpdateTreeRowTooltip(rowPanel, newDisplayName, fullName, extraTooltipLines);
                }
            });
        }

        /// <summary>
        /// Rebuilds a tree row's tooltip from its (possibly re-ellipsized)
        /// display name plus its width-invariant extra lines - shared by
        /// RenderTreeNode's initial build and its settle re-ellipsis
        /// closure so the two can never disagree about tooltip content.
        /// </summary>
        private static void UpdateTreeRowTooltip(
            Panel rowPanel, string displayName, string fullName, List<string> extraLines)
        {
            var parts = new List<string>();
            if (displayName != fullName)
            {
                parts.Add(fullName);
            }
            parts.AddRange(extraLines);
            rowPanel.BasicTooltipText = parts.Count > 0 ? string.Join("\n", parts) : null;
        }

        #endregion // 8. Tree rendering (continued)

        #region 9. Decision pills

        // --- Decision pills ---
        //
        // PillKind/PillSpec/BuildPillSpecs (the decision -> pill mapping,
        // gw2e's multi-pill model, KNOWN-ISSUES #18) live in
        // Services/DecisionPillPlanner.cs - Blish-free and directly unit
        // tested (DecisionPillPlannerTests) - so only the actual
        // Panel/Label rendering below stays view-only.

        /// <summary>
        /// isIgnoreActive is only meaningful for PillKind.Ignore (whether
        /// THIS specific Ignore pill is the active/"IGNORED" state, i.e.
        /// node.IsIgnored) - ignored for every other kind.
        /// </summary>
        private static void GetPillColors(PillKind kind, bool isIgnoreActive, out Color border, out Color fill)
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
                case PillKind.OwnedInfo:
                    // Muted gold, distinct from every other pill hue -
                    // informational only, never confused with a selectable
                    // source (M34-B2b).
                    border = new Color(201, 162, 39); // #C9A227
                    fill = border * 0.15f;
                    break;
                case PillKind.Ignore:
                    // Amber when active ("IGNORED", currently toggled on);
                    // plain clickable grey (matching Available) otherwise -
                    // never Selected's green, to avoid reading as "the
                    // chosen acquisition source" (M34-B2b).
                    border = isIgnoreActive ? new Color(229, 168, 60) : new Color(138, 138, 138); // #E5A83C / #8A8A8A
                    fill = isIgnoreActive ? border * 0.15f : Color.Transparent;
                    break;
                case PillKind.AchievementBitDeduped:
                    // Muted violet - distinct from Have's blue and
                    // OwnedInfo's gold: nothing here is actually owned, just
                    // already required elsewhere (M37, KNOWN-ISSUES #26).
                    border = new Color(155, 118, 219); // #9B76DB
                    fill = border * 0.15f;
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
        ///
        /// M34 fix (MustFix review finding): TreePillColumnWidth (240px) is
        /// a fixed budget, but DecisionPillPlanner.AppendOwnershipPills now
        /// unconditionally adds an "IGNORE" pill (plus "USING N OWNED" when
        /// applicable) to every ordinary node, on top of its 1-3 source
        /// pills - realistic combinations regularly exceed 240px. Rather
        /// than let trailing pills render on top of the right-aligned cost
        /// column (this row has no wrap/second-line support - TreeRowHeight
        /// is a fixed per-row height shared by every layout/scroll-height
        /// calculation in this file), only as many pills as
        /// PlanRelayoutMath.ComputeVisiblePillCount says fit are rendered -
        /// see that method's doc comment for why this naturally drops the
        /// lower-priority (OwnedInfo/Ignore) pills first while always
        /// keeping at least the first (most important) pill.
        /// </summary>
        private List<Panel> RenderDecisionPills(
            Panel rowPanel, CraftingTreeNode node, int pillColX, int pillY, bool dimmed)
        {
            var specs = DecisionPillPlanner.BuildPillSpecs(node);
            var font = GameService.Content.DefaultFont12;
            var pillPanels = new List<Panel>(specs.Count);
            int x = pillColX;

            var pillWidths = new List<int>(specs.Count);
            foreach (var spec in specs)
            {
                int measuredWidth = (int)System.Math.Ceiling(font.MeasureString(spec.Text).Width) + 12;
                pillWidths.Add(measuredWidth);
            }
            int maxRightEdge = pillColX + TreePillColumnWidth - 4;
            int visibleCount = PlanRelayoutMath.ComputeVisiblePillCount(pillWidths, 6, pillColX, maxRightEdge);

            for (int specIndex = 0; specIndex < visibleCount; specIndex++)
            {
                var spec = specs[specIndex];
                int pillWidth = pillWidths[specIndex];
                int textWidth = pillWidth - 12;

                GetPillColors(spec.Kind, node.IsIgnored, out Color borderColor, out Color fillColor);
                // White, not borderColor: Selected/Available fills expose the
                // border hue behind the label, so border-colored text has zero
                // contrast against its own backdrop (M30 #11).
                Color textColor = Color.White;
                if (dimmed)
                {
                    borderColor *= 0.35f;
                    fillColor *= 0.35f;
                    textColor *= 0.35f;
                }

                // Border simulated as an outer colored panel with a 1px-inset
                // fill panel - same nesting technique as IconControls.CreateRarityFramedIcon.
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
                bool ignoreInteractive = !dimmed && spec.Kind == PillKind.Ignore && _resolveOverridesSync != null;
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
                else if (ignoreInteractive)
                {
                    // M34-B2b: toggles this ITEM id (not just this node) in
                    // or out of _ignoredItemIds, matching gw2e's own
                    // tree-wide-by-item-id "Ignore" semantics.
                    outer.BasicTooltipText = node.IsIgnored
                        ? "Stop treating this item as fully in-hand"
                        : "Treat this item as fully in-hand (ignore its owned-stock requirement)";
                    int itemId = node.ItemId;
                    outer.Click += (_, __) =>
                    {
                        if (!_ignoredItemIds.Remove(itemId))
                        {
                            _ignoredItemIds.Add(itemId);
                        }
                        ApplyOverridesAndResolve();
                    };
                    Color restingBorder = borderColor;
                    outer.MouseEntered += (_, __) => outer.BackgroundColor = Color.White;
                    outer.MouseLeft += (_, __) => outer.BackgroundColor = restingBorder;
                }
                else if (spec.Kind == PillKind.Locked)
                {
                    // The UNKNOWN pill (node.Decision == Unknown - no
                    // feasible source at all) is a different situation from
                    // every other locked pill (exactly one feasible source,
                    // just not a choice): "Only available source" is
                    // misleading there since there IS no available source.
                    // Prefer the seeded wiki hint when one exists.
                    if (node.Decision == CraftingDecision.Unknown)
                    {
                        outer.BasicTooltipText = !string.IsNullOrEmpty(node.AcquisitionHint)
                            ? node.AcquisitionHint
                            : "No known acquisition source";
                    }
                    else
                    {
                        outer.BasicTooltipText = "Only available source";
                    }
                }

                pillPanels.Add(outer);
                x += pillWidth + 6;
            }

            return pillPanels;
        }

        #endregion // 9. Decision pills
    }
}

#pragma warning restore SA1124 // Do not use regions
