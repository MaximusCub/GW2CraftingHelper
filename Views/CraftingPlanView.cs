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

        // Layout constants. The top strip's own Y arithmetic lives in the
        // Blish-free Services/TopRegionLayoutMath (three call sites lay the
        // strip out from it); these two are aliases so the row builders in
        // this file keep reading naturally.
        private const int RowHeight = TopRegionLayoutMath.RowHeight;
        private const int InputRowY = TopRegionLayoutMath.InputRowY;

        // Item-row geometry, left to right: search box, "Qty:" label,
        // quantity field, then the add/remove buttons. The buttons keep a
        // clear gap from the quantity field so "+" does not read as its
        // stepper.
        private const int QtyInputX = 240;
        private const int QtyInputWidth = 50;
        private const int RowButtonsX = 320;

        // The row's +/- pair: square, and the same height as every other
        // button in the module - which is also the height of the search and
        // quantity boxes they sit beside, so the run now shares one baseline
        // instead of mixing 28px inputs with 24px buttons.
        private const int RowButtonSize = UiMetrics.ButtonHeight;
        private const int RowButtonGap = 8;
        private const int RowButtonY = 3;

        private const int RightEdgePadding = 20;
        private const int SectionSpacing = 16;

        // Aliased, not duplicated: the band height, its title y and its
        // caret y are one piece of arithmetic against the section-title
        // font's measured ink - see PlanContentHeightMath.
        private const int SectionHeaderRowHeight = PlanContentHeightMath.SectionHeaderRowHeight;

        // Section divider grey, readable against the parchment texture, one
        // tier below the 180-grey structural separators (window chrome,
        // unrelated to this). The row-divider twin (RowDividerColor) moved
        // to Views/Rendering/LabelHelpers.cs alongside
        // LabelHelpers.CreateRowDivider - it had no other caller.
        private static readonly Color SectionDividerColor = new Color(130, 130, 130);

        /// <summary>
        /// This view's own binding of TopRegionLayoutMath.Compute: the
        /// row count and the tree toolbar's visibility are view state, the
        /// arithmetic is not. Every caller goes through here so no call
        /// site can lay the strip out against a different answer to "is the
        /// toolbar row showing".
        /// </summary>
        private TopRegionLayout ComputeTopRegionLayout()
        {
            return TopRegionLayoutMath.Compute(_itemRows.Count, _treeToolbarVisible);
        }

        // phaseProgress carries live coarse-phase events for the status
        // strip; requestLabel is a best-effort item-name label; the
        // valueOwnMaterials bool is a per-plan session choice, like useOwn.
        private readonly Func<IReadOnlyList<PlanRequestItem>, bool, bool, PriceBasis, CancellationToken, IProgress<PlanStatus>, IProgress<PlanPhaseEvent>, string, Task<CraftingPlanResult>> _generateAsync;
        private readonly Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, ISet<int>, CraftingPlanResult> _resolveOverridesSync;
        private readonly ModalDialog _modalDialog;
        // Session-scoped item stat lookup (ItemMetadataService's own cache -
        // never a fetch), or null when the host did not wire one. Null and
        // a null RESULT both mean the same thing here: fall back to the
        // tooltip this surface had before stat tooltips existed.
        private readonly Func<int, ItemStatBlock> _getItemStatBlock;

        // Q13: fills the session stat cache for a restored plan's items in
        // the background. Never on the hover path - see
        // ItemMetadataService.GetCachedStatBlock.
        private readonly Func<IReadOnlyList<int>, Task<int>> _warmItemStatsAsync;
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

            // What the search box last read, kept whether or not it
            // resolved to an item. ItemName alone cannot carry this: it is
            // dropped the moment the text stops describing the picked item,
            // so seeding a rebuilt row from it would wipe half-typed text
            // on every row add/remove.
            public string TypedText;
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
        private DateTime _lastSpinnerTickUtc;

        // How often the strip re-renders itself from the status board while
        // a generation is in flight. The whirly part is Blish's own
        // LoadingSpinner control, which animates off global game time and
        // needs no help from here (see InlineSpinner); this throttle exists
        // only to keep the ticker from rewriting an AutoSizeWidth Label's
        // Text - and re-triggering its text measure - 60x/sec for the whole
        // of every generation.
        private static readonly TimeSpan SpinnerTickInterval = TimeSpan.FromMilliseconds(150);

        // The toolbar's Use Own Materials / Prices / Value Own Materials
        // controls only take effect on the next Generate, unlike the
        // instant-apply controls that look just like them on other tabs.
        // Every one of them says so through the status label at the moment
        // it changes.
        // "press" is filler and "update" said nothing about WHAT updates;
        // "apply" says what happens to the settings, and the button is
        // named exactly.
        private const string SettingsChangedStatus = "Settings changed - Generate Plan to apply";

        // Shown while Generate resolves typed-but-unpicked row names against
        // the search provider, before any plan work starts.
        private const string ResolvingStatus = "Resolving items...";

        // Separates the strip's standing notices from the status board's own
        // text (and from each other).
        private const string StatusNoticeSeparator = "  |  ";

        // Two things that stay true about the plan on screen for longer than
        // one status write: a toolbar change it does not include, and rows
        // that were left out of it. Held as state rather than written
        // straight into the label because RenderFromBoard re-renders the
        // strip from _statusBoard about seven times a second during a
        // generation and again on every rebuild - a bare SetStatus is erased
        // within one spinner tick by the very run the notice is about.
        private bool _settingsChangedPending;
        private string _unresolvedRowsNotice;

        // How far the plan on screen is dimmed while a new one generates -
        // enough to read as superseded, not so far that it stops being
        // readable while you wait.
        private const float StalePlanOpacity = 0.45f;

        // How many results the typed-name resolution pass asks for. It has
        // to be wide enough to hold EVERY item sharing the typed name, not
        // just the first: a window that cut the second one off would turn
        // an ambiguous name into a confident wrong pick. The shipped
        // provider returns prefix matches in name order, so the items named
        // exactly what was typed are the shortest of those and come first -
        // several of them are visible well inside this limit whatever else
        // matches. A provider that ranked results some other way would need
        // this re-checked.
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

        // Click-to-sort state for the two sortable plan tables. Session
        // state with exactly the lifetime _sectionExpansion has, and it is
        // reset in the same place for the same reason: it survives every
        // RenderPlan rebuild a re-sort, a pill override or a re-solve
        // triggers - those all re-render the SAME plan, and a user who
        // sorted by Amount must stay sorted by Amount through them - but a
        // NEW plan generation clears it back to None.
        //
        // Why a new plan is different: the sort described a table that no
        // longer exists. A fresh Generate can carry an entirely different
        // row set, so an inherited sort silently re-orders rows the user
        // never sorted, and the header indicator on a table they did not
        // touch this plan reads as the module's own doing rather than
        // theirs (maintainer decision, field-test round). The reset sits at
        // TriggerGenerate's commit point, which the override/re-solve paths
        // do not run through at all - see ResetPerPlanSortState.
        //
        // TableSortState/PlanTableSorter (Blish-free) own the click cycle
        // and the comparators; these fields are only the live state.
        private readonly TableSortState<PlanTableColumn> _usedMaterialsSort =
            new TableSortState<PlanTableColumn>();
        private readonly TableSortState<PlanTableColumn> _shoppingListSort =
            new TableSortState<PlanTableColumn>();

        /// <summary>
        /// Clears BOTH tables' sort back to
        /// <see cref="TableSortDirection.None"/>. One method so a future
        /// third sortable table cannot be added to one reset site and
        /// forgotten at another.
        /// <para>
        /// Called from every site that clears <c>_sectionExpansion</c> -
        /// TriggerGenerate's commit point, ApplyRestoredPlan and
        /// RollBackFailedPlanRender - so "new plan state" is one pairing
        /// rather than three independent ones. Only the first can actually
        /// carry a stale sort today (a restore cannot follow a Generate in
        /// the same session, and a rolled-back render leaves no table), but
        /// pinning that to a guard in another file is what invites the
        /// forgotten site this method exists to prevent.
        /// </para>
        /// </summary>
        private void ResetPerPlanSortState()
        {
            _usedMaterialsSort.Reset();
            _shoppingListSort.Reset();
        }

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
        // top strip - see ReflowTopRegion.
        private Container _buildPanel;
        private Panel _inputPanel;
        private Panel _controlsPanel;
        private Checkbox _ownMaterialsCheckbox;
        private Checkbox _valueOwnMaterialsCheckbox;
        private StandardButton _generateButton;
        private Label _statusLabel;

        // Trails _statusLabel while a generation is in flight. A sibling of
        // the label rather than a decoration inside it: it is a Control that
        // paints a texture, not a glyph the label could carry.
        private LoadingSpinner _statusSpinner;
        private Panel _separator;
        private FlowPanel _contentPanel;

        // Recipe Tree toolbar row. The five buttons used to live in the
        // tree's section header inside the scroll flow, which meant a long
        // plan scrolled Collapse All away at exactly the moment it became
        // useful. They sit in the non-scrolling strip now; the state they
        // act on stays with TreeSectionController and reaches them through
        // _treeToolbarCommands, republished by every tree render and
        // withdrawn (null) by every render that produces no tree.
        //
        // _treeToolbarVisible is the single answer to "does the strip
        // reserve a row for this?" - the panel's Visible flag and
        // TopRegionLayoutMath both read it, so they cannot disagree.
        private Panel _treeToolbarPanel;
        private bool _treeToolbarVisible;
        private TreeToolbarCommands _treeToolbarCommands;
        private readonly List<(StandardButton Button, int Width, int GapToLeft)> _treeToolbarButtons =
            new List<(StandardButton, int, int)>(5);

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

        // The Recipe Tree's own half of those two registries, kept apart
        // for one reason: the tree section SURVIVES a re-render that a
        // decision pill triggers (see RenderPlan's preserveTree path), and
        // a closure whose controls survive must survive with them.
        // Everything else about them is identical - ReplayRelayout and
        // RunReellipsis replay both, and the closures are position-only
        // either way, so the order between the two lists cannot matter
        // (they touch disjoint controls).
        private readonly List<Action<int>> _treeRelayoutActions = new List<Action<int>>();
        private readonly List<Action<int>> _treeReellipsisActions = new List<Action<int>>();

        // The tree section's own children of _contentPanel (its top gap,
        // header band and content flow), captured when it is built. Held so
        // a preserving re-render can detach them before the dispose sweep
        // and re-attach them at the same point in the flow afterwards -
        // _contentPanel lays its children out in child order, so
        // re-parenting at the right moment IS the ordering.
        private List<Control> _treeSectionControls;

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

        void ISectionRelayoutSink.RequestRerenderAfterSettle()
        {
            _rerenderAfterSettlePending = true;
        }

        int ISectionRelayoutSink.RelayoutCount => _relayoutActions.Count;

        /// <summary>
        /// The sink TreeSectionController registers through - the same
        /// contract as the view's own, routed to the tree-scoped registries
        /// above. A separate OBJECT rather than a flag on the view because
        /// the tree registers closures outside its own build pass too
        /// (every lazy expand adds rows), and those closures must land in
        /// the surviving registry just as the build pass's do.
        /// </summary>
        private sealed class TreeRelayoutSink : ISectionRelayoutSink
        {
            private readonly CraftingPlanView _view;

            internal TreeRelayoutSink(CraftingPlanView view)
            {
                _view = view;
            }

            public void AddRelayout(Action<int> closure) => _view._treeRelayoutActions.Add(closure);

            public void AddReellipsis(Action<int> closure) => _view._treeReellipsisActions.Add(closure);

            public void RequestRerenderAfterSettle() => _view._rerenderAfterSettlePending = true;

            public int RelayoutCount => _view._treeRelayoutActions.Count;
        }

        // Set only for the duration of the tree's own CreateSectionHeader
        // call, so the shared section chrome that call registers joins the
        // tree's registry rather than the one a preserving re-render
        // clears. Every other section's header is unaffected.
        private bool _routeSectionChromeToTree;

        // Set by a re-ellipsis closure that cannot honour the registry's
        // no-height-change contract at the settled width (today only the
        // Notes section, whose row COUNT is width-dependent - see
        // ISectionRelayoutSink.RequestRerenderAfterSettle). Set only from
        // inside RunReellipsis and always cleared by the same
        // ResizeSettleStep call, so it never carries across drags - the
        // rebuild is deferred to the end of that call only because
        // RenderPlan clears the registry RunReellipsis is iterating.
        private bool _rerenderAfterSettlePending;

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
        // Two spellings of one tag, for two sinks with different shapes:
        // ModuleLogEntry carries the tag as a FIELD (the Log tab renders it
        // as "[scrolldiag]" in its own prefix column), while Blish's Logger
        // has no tag column and needs it inside the message. Every call
        // site used to prepend the bracketed form to the message text AND
        // hand it to ModuleLog under the same tag, so every Log tab line
        // read "[scrolldiag] [scrolldiag] wheel frame=..." - fixed here, in
        // the one place that writes to both sinks, rather than at fourteen
        // call sites.
        private const string ScrollDiagLogTag = "scrolldiag";
        private const string ScrollDiagTag = "[" + ScrollDiagLogTag + "]";

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
            Logger.Debug($"{ScrollDiagTag} {message}");
            ModuleLog.Shared.Write(ModuleLogLevel.Debug, ScrollDiagLogTag, message);
        }

        #endregion // Diagnostics: scroll/wheel instrumentation (shared by #3 and #4) - KNOWN-ISSUES #12

        #region General: construction & status
        public CraftingPlanView(
            Func<IReadOnlyList<PlanRequestItem>, bool, bool, PriceBasis, CancellationToken, IProgress<PlanStatus>, IProgress<PlanPhaseEvent>, string, Task<CraftingPlanResult>> generateAsync,
            ModalDialog modalDialog,
            IItemSearchProvider itemSearchProvider,
            ModuleSettings settings,
            PlanStripStatusBoard statusBoard,
            Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, ISet<int>, CraftingPlanResult> resolveOverridesSync = null,
            Func<int, ItemStatBlock> getItemStatBlock = null,
            // Background stat top-up for a plan restored from disk (Q13) -
            // see StartRestoredStatWarmup. Optional; without it a restored
            // plan simply has no stat blocks until the user regenerates,
            // which is the pre-existing behaviour.
            Func<IReadOnlyList<int>, Task<int>> warmItemStatsAsync = null)
        {
            _generateAsync = generateAsync;
            _modalDialog = modalDialog;
            _itemSearchProvider = itemSearchProvider;
            _settings = settings;
            _statusBoard = statusBoard ?? throw new ArgumentNullException(nameof(statusBoard));
            _resolveOverridesSync = resolveOverridesSync;
            _getItemStatBlock = getItemStatBlock;
            _warmItemStatsAsync = warmItemStatsAsync;

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
                new TreeRelayoutSink(this),
                _resolveOverridesSync,
                _vmBuilder,
                PreserveScrollAcross,
                SetStatus,
                RenderPlanAfterResolve,
                GetCurrentPanelWidth,
                () => _currentPlan,
                vm => _currentPlan = vm,
                log => _lastDebugLog = log,
                (title, sectionKey, panelWidth, defaultExpanded, suppressToggle) =>
                {
                    _routeSectionChromeToTree = true;
                    try
                    {
                        var header = CreateSectionHeader(title, sectionKey, panelWidth, defaultExpanded, suppressToggle);
                        return (header.HeaderPanel, header.ArrowLabel, header.ContentFlow);
                    }
                    finally
                    {
                        _routeSectionChromeToTree = false;
                    }
                },
                commands => _treeToolbarCommands = commands,
                getItemStatBlock);
        }


        public void SetStatus(string status)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = status ?? "";

                // The label is AutoSizeWidth, so its right edge moves with
                // every text change and the spinner has to follow it. Done
                // here rather than only in RenderFromBoard because the
                // strip has several other writers (Resolving..., the
                // invalid-quantity notice, the section renderers' own
                // SetStatus callback) and any of them can land mid-flight.
                InlineSpinner.PlaceAfter(_statusSpinner, _statusLabel, InlineSpinnerLayout.LabelGap);
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
            ResetPerPlanSortState();
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

            // The stamped half goes through StatusText.Stamp, which owns
            // the module's one timestamp format and its InvariantCulture
            // policy (English-only strings; several locales' short time
            // pattern has no AM/PM designator at all). ONE trailing hyphen
            // clause: the dash separates verb from timestamp, a hyphen
            // separates clauses, and two hyphen clauses at one level put
            // two unrelated facts on the same footing. It also names a
            // button that exists, and states the payoff (fresh prices)
            // rather than the fear ("prices may have changed").
            _statusBoard.SeedRestored(
                StatusText.Stamp("Generated", generatedAt) + " - Generate Plan to refresh prices");
            RenderFromBoard(_statusBoard.Snapshot());

            // Started BEFORE the render below and regardless of whether
            // the tab is live: the rows read the stat cache at hover time,
            // not at render time, so there is nothing to wait for.
            StartRestoredStatWarmup(result);

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
        /// Q13: a restored plan makes no network call by design, so its
        /// session stat cache is empty and every row's item tooltip
        /// degrades to the plain one. This fills that cache in the
        /// background for the restored plan's own items.
        /// <para>
        /// The rows do not have to be re-rendered for the result to show:
        /// their tooltips are composed when the box is about to be drawn
        /// (TooltipFacility.ApplyRichDeferred), so the next hover picks the
        /// blocks up on its own. The one case that needs a nudge is a
        /// cursor already resting on a row when the fetch lands, which
        /// RefreshCurrent redraws - on the main thread, like every other
        /// control mutation.
        /// </para>
        /// <para>
        /// Fire-and-forget and fully swallowed: failing means the tooltips
        /// stay exactly as they were before this ran, which is not an error
        /// worth a banner.
        /// </para>
        /// </summary>
        private void StartRestoredStatWarmup(CraftingPlanResult result)
        {
            if (_warmItemStatsAsync == null || result?.ItemMetadata == null || result.ItemMetadata.Count == 0)
            {
                return;
            }

            var ids = new List<int>(result.ItemMetadata.Keys);
            _ = WarmRestoredStatsAsync(ids);
        }

        private async Task WarmRestoredStatsAsync(IReadOnlyList<int> ids)
        {
            try
            {
                int filled = await _warmItemStatsAsync(ids).ConfigureAwait(false);
                if (filled > 0)
                {
                    MainThreadMarshal.Run(TooltipFacility.RefreshCurrent);
                }
            }
            catch (Exception ex)
            {
                ModuleLog.Shared.Write(ModuleLogLevel.Debug, "plan",
                    $"Restored-plan stat top-up did not complete: {ex.GetType().Name} - {ex.Message}");
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
            ResetPerPlanSortState();
            _lastDebugLog = null;
            _currentPlan = null;
            _planGeneratedAt = default(DateTime);

            ResetContentPanelToEmpty();
            // ResetContentPanelToEmpty withdrew the toolbar commands; the
            // row itself would otherwise stay reserved over a plan that no
            // longer exists.
            ApplyTreeToolbarVisibility(false);
            RefreshTreeStateChips();

            // Leaves the tab in the SAME no-plan state a first visit shows,
            // rather than the blank panel a rolled-back render used to
            // leave behind - the status strip carries the failure, the
            // content area says what to do next.
            ShowEmptyPlanState();

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
                LogScrollDiag($"write writer=SyncRestore frame={ScrollDiagFrame()} before={before:0.0000} after={ratio:0.0000} contentHeight={contentHeight} savedOffset={savedOffset} generation={capturedGeneration}");
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
                LogScrollDiag($"verify-armed frame={ScrollDiagFrame()} savedOffset={savedOffset} generation={capturedGeneration}");
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
                        LogScrollDiag($"verify exit reason=stale-generation frame={ScrollDiagFrame()} realFrame={frame} generation={capturedGeneration} liveGeneration={_scrollRestoreGeneration}");
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
                            LogScrollDiag($"verify exit reason=wheel-observed frame={ScrollDiagFrame()} realFrame={frame}");
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
                            LogScrollDiag($"write writer=Verify/zeroReassert frame={ScrollDiagFrame()} realFrame={frame} before={current:0.0000} after={target:0.0000} contentHeight={contentHeight} bounceCount={zeroReassert}");
                        }

                        if (zeroReassert >= ScrollVerifyZeroReassertCap)
                        {
                            if (diagEnabled)
                            {
                                LogScrollDiag($"verify exit reason=zero-reassert-cap-exceeded frame={ScrollDiagFrame()} realFrame={frame} bounceCount={zeroReassert}");
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
                            LogScrollDiag($"verify exit reason=user-scroll-detected frame={ScrollDiagFrame()} realFrame={frame} observed={current:0.0000} target={target:0.0000} contentHeight={contentHeight}");
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
                            LogScrollDiag($"verify exit reason=stable frame={ScrollDiagFrame()} realFrame={frame} target={target:0.0000} contentHeight={contentHeight}");
                        }
                        return false;
                    }

                    if (frame < ScrollVerifyMaxFrames)
                    {
                        return true;
                    }

                    if (diagEnabled)
                    {
                        LogScrollDiag($"verify exit reason=max-frames frame={ScrollDiagFrame()} realFrame={frame} target={target:0.0000} contentHeight={contentHeight}");
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
                        LogScrollDiag($"verify exit reason=disposed-exception frame={ScrollDiagFrame()} realFrame={frame} error={ex.GetType().Name}");
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
                LogScrollDiag($"write writer=WheelWrapFix frame={ScrollDiagFrame()} rawIn={rawIn} intendedDelta={intendedDelta} before={before:0.0000} after={after:0.0000}");
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
                            LogScrollDiag($"write writer=WheelWrapFix/reassert frame={ScrollDiagFrame()} before={current:0.0000} after={target:0.0000}");
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

            LogScrollDiag($"wheel frame={ScrollDiagFrame()} sign={System.Math.Sign(wheelValue)} raw={wheelValue} scrollDistance={(scrollbar?.ScrollDistance ?? -1f):0.0000} contentHeight={contentHeight} verifyLive={verifyLive}");
        }

        #endregion // 4. Wheel-wrap correction (continued) - KNOWN-ISSUES #12 (reopened)

        #region 1. Input rows (continued)

        /// <summary>
        /// Disposes every current item row's live controls and rebuilds
        /// them from _itemRows.
        /// Called by Build() (initial construction) and by
        /// AddItemRow/RemoveItemRow via ReflowTopRegion (row-count
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
                // Add/Remove reflow (ReflowTopRegion, _inputPanel still
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
                Text = row.TypedText ?? row.ItemName ?? "",
                Size = new Point(200, 28),
                Location = new Point(0, 3),
                Parent = rowPanel
            };
            row.SearchBox = searchBox;

            // The list drops straight under this box (see
            // SuggestionPanel.PositionPanel).
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
                row.TypedText = searchBox.Text;

                if (!ItemRowSelection.SelectionIsStale(row.ItemId, row.ItemName, searchBox.Text))
                {
                    return;
                }

                row.ItemId = null;
                row.ItemName = null;
            };

            new Label()
            {
                Font = UiFonts.Body,
                Text = "Qty:",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(210, 7),
                Parent = rowPanel
            };

            var qtyInput = new TextBox()
            {
                Text = string.IsNullOrEmpty(row.QuantityText) ? "1" : row.QuantityText,
                Size = new Point(QtyInputWidth, 28),
                Location = new Point(QtyInputX, 3),
                Parent = rowPanel
            };
            qtyInput.TextChanged += (_, __) => row.QuantityText = qtyInput.Text;
            row.QtyInput = qtyInput;

            int nextX = RowButtonsX;
            if (ItemRowRequestBuilder.CanRemoveRow(_itemRows.Count))
            {
                var removeButton = new FeedbackButton()
                {
                    Text = "-",
                    Size = new Point(RowButtonSize, RowButtonSize),
                    Location = new Point(nextX, RowButtonY),
                    Parent = rowPanel,
                    BasicTooltipText = "Remove this item from the plan"
                };
                removeButton.Click += (_, __) => RemoveItemRow(row);
                nextX += RowButtonSize + RowButtonGap;
            }

            if (index == _itemRows.Count - 1)
            {
                var addButton = new FeedbackButton()
                {
                    Text = "+",
                    Size = new Point(RowButtonSize, RowButtonSize),
                    Location = new Point(nextX, RowButtonY),
                    Parent = rowPanel,
                    // Sitting next to the quantity field, a bare "+" reads
                    // as a stepper. Say what it actually adds.
                    BasicTooltipText = "Add another item to this plan"
                };
                addButton.Click += (_, __) => AddItemRow();
            }
        }

        private void AddItemRow()
        {
            _itemRows.Add(new ItemRowState());
            ReflowTopRegion(rebuildItemRows: true);
        }

        private void RemoveItemRow(ItemRowState row)
        {
            if (!ItemRowRequestBuilder.CanRemoveRow(_itemRows.Count)) return;

            int index = _itemRows.IndexOf(row);
            if (index < 0) return;

            row.SuggestionPanel?.Dispose();
            row.RowPanel?.Dispose();
            _itemRows.RemoveAt(index);
            ReflowTopRegion(rebuildItemRows: true);
        }

        /// <summary>
        /// Repositions every fixed element of the top strip
        /// (controls/toolbar/status/separator/content) after something
        /// changed its total height - an item row added or removed, or the
        /// Recipe Tree toolbar row appearing/disappearing with the plan.
        /// The width-driven counterpart is OnPanelResized. Neither trigger
        /// changes width, only height, so this mirrors OnPanelResized's
        /// heightChanged branch (scroll-preserve) without needing its
        /// widthChanged branch (no relayout replay).
        /// <para>
        /// <paramref name="rebuildItemRows"/> is the one part that belongs
        /// to the row add/remove trigger alone: the toolbar appearing must
        /// not tear down and rebuild every search box (and with it the
        /// user's in-progress typing and open suggestion list).
        /// </para>
        /// </summary>
        private void ReflowTopRegion(bool rebuildItemRows = false)
        {
            if (_buildPanel == null || _inputPanel == null) return;

            int w = _buildPanel.ContentRegion.Width;
            int h = _buildPanel.ContentRegion.Height;
            var layout = ComputeTopRegionLayout();

            int savedScrollOffset = _contentPanel?.VerticalScrollOffset ?? 0;
            int previousContentHeight = _contentPanel?.Height ?? 0;

            _inputPanel.Size = new Point(w, layout.InputPanelHeight);
            if (rebuildItemRows)
            {
                RebuildItemRowControls(w);
            }

            _controlsPanel.Location = new Point(0, layout.ControlsRowY);
            PlaceTreeToolbarRow(w, layout.TreeToolbarRowY);
            _statusLabel.Location = new Point(0, layout.StatusRowY);
            InlineSpinner.PlaceAfter(_statusSpinner, _statusLabel, InlineSpinnerLayout.LabelGap);
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

            // Settled BEFORE the layout is computed, from the plan this
            // Build is about to render (a tab switch re-renders whatever
            // _currentPlan holds, at the bottom of this method). Deriving
            // it here rather than letting RenderPlan discover it means the
            // strip is laid out once, with the row already accounted for,
            // instead of being rebuilt a moment later.
            _treeToolbarVisible = ResolveTreeRoots(_currentPlan) != null;
            _treeToolbarCommands = null;

            // A Build gives the tab a brand new content panel; everything
            // the previous one held - including the tree section this view
            // otherwise preserves across a re-render - dies with it. Held
            // controls and the closures that reposition them have to go
            // with it, or the first preserving render after a tab rebuild
            // re-parents disposed controls.
            _treeController.ResetTreeRenderState();
            _treeSectionControls = null;
            _treeRelayoutActions.Clear();
            _treeReellipsisActions.Clear();

            var layout = ComputeTopRegionLayout();

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
                Font = UiFonts.Body,
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
                MarkSettingsChanged();
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
            };
            TooltipFacility.ApplyPlain(
                _valueOwnMaterialsCheckbox,
                "Compare recipe options at fresh market prices, as if you owned nothing - may recommend buying materials you already have instead of using them, if a different option is cheaper. Also force-buys materials where buying beats crafting by more than 15%, and deducts owned materials' sell value from Crafting Profit. Off: always uses what you already own first, treated as free.");
            _valueOwnMaterialsCheckbox.CheckedChanged += (_, e) =>
            {
                _valueOwnMaterials = e.Checked;
                MarkSettingsChanged();
            };

            // Wired here, after _valueOwnMaterialsCheckbox is fully
            // constructed - see the comment at that construction site.
            _ownMaterialsCheckbox.CheckedChanged += OnOwnMaterialsToggled;

            _generateButton = new FeedbackButton()
            {
                Text = "Generate Plan",
                Size = new Point(120, UiMetrics.ButtonHeight),
                Location = new Point(w - 120 - RightEdgePadding, 3),
                Parent = _controlsPanel
            };
            _generateButton.Click += async (_, __) => await TriggerGenerate();

            // This tooltip is Generate Plan's ENTIRE safety mechanism: it
            // is the one action in the tree's vocabulary that destroys
            // manual decisions without a confirm dialog (see the tree
            // confirm matrix for why it is exempt), so the second sentence
            // is load-bearing and ships with the first.
            TooltipFacility.ApplyPlain(
                _generateButton,
                "Fetches current prices and rebuilds the plan from scratch. " +
                "Clears all manual craft/buy decisions and ignore marks.");

            CreateTreeToolbarRow(buildPanel, w, layout.TreeToolbarRowY);

            // Status label. Its own tier: the strip reports what the module
            // is doing, and had been reporting it at the same size as
            // every row in the plan below.
            _statusLabel = new Label()
            {
                Font = UiFonts.Status,
                Text = "Ready",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, layout.StatusRowY),
                Parent = buildPanel
            };

            _statusSpinner = InlineSpinner.Create(buildPanel, InlineSpinnerLayout.PlanStripSize);
            InlineSpinner.PlaceAfter(_statusSpinner, _statusLabel, InlineSpinnerLayout.LabelGap);

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
            else if (!boardSnapshot.InFlight)
            {
                // Only when nothing is running. A solver started before the
                // user switched tabs is still in flight on the way back, and
                // "No plan yet. Search for an item above, then click
                // Generate Plan." beside a status strip reading
                // "Generating..." instructs the user to do the thing that is
                // already happening. The spinner armed above is the whole
                // message in that case; the content area stays empty until
                // the render the board's own completion drives.
                ShowEmptyPlanState();
            }
        }

        // Toolbar row geometry. The five widths are the ones the buttons
        // carried in the section header; only their home changed.
        private const int TreeToolbarButtonHeight = UiMetrics.ButtonHeight;
        private const int TreeToolbarButtonY =
            (TopRegionLayoutMath.TreeToolbarRowHeight - TreeToolbarButtonHeight) / 2;
        private const int TreeToolbarButtonGap = 4;

        // Separates the three plan-mutating presets from the two view-only
        // actions. Wider than TreeToolbarButtonGap on purpose: "Buy All"
        // re-solves the whole plan and "Expand All" only opens branches,
        // and sitting them 4px apart in one undifferentiated run invited
        // exactly the misclick that costs a set of manual overrides.
        private const int TreeToolbarGroupGap = 20;

        /// <summary>
        /// The Recipe Tree's action row, in the non-scrolling strip. It is
        /// built once per Build() and hidden - not disposed - whenever the
        /// current plan has no tree, because the strip's Y arithmetic
        /// already collapses the row in that state
        /// (TopRegionLayoutMath.Compute) and a hidden panel costs nothing.
        /// <para>
        /// The buttons hold no tree state of their own: each one reads
        /// _treeToolbarCommands at click time, which is null between a
        /// render dropping the old tree and the next one publishing a new
        /// one. A click in that window does nothing rather than reaching
        /// into disposed controls.
        /// </para>
        /// </summary>
        private void CreateTreeToolbarRow(Container buildPanel, int w, int rowY)
        {
            _treeToolbarButtons.Clear();

            // Size/Location/Visible are all settled by the
            // PlaceTreeToolbarRow call at the bottom of this method, the
            // one writer of them.
            _treeToolbarPanel = new Panel()
            {
                Parent = buildPanel
            };

            CreateTreeStateChips();

            // Right to left, so the row stays anchored to the right edge at
            // every window width. gapToLeft is the space left BEFORE the
            // next button placed (which lands to this one's left).
            void PlaceRight(string text, int width, int gapToLeft, string tooltipText, Action onClick)
            {
                var button = new FeedbackButton()
                {
                    Text = text,
                    Size = new Point(width, TreeToolbarButtonHeight),
                    Parent = _treeToolbarPanel
                };
                TooltipFacility.ApplyPlain(button, tooltipText);
                button.Click += (_, __) => onClick();
                _treeToolbarButtons.Add((button, width, gapToLeft));
            }

            // The two view-only actions go straight through; the three that
            // destroy manual decisions go through the confirm matrix.
            PlaceRight("Collapse All", 96, TreeToolbarButtonGap,
                "Collapses every branch of the Recipe Tree back down to the top level.",
                () => InvokeTreeCommand(c => c.CollapseAll));
            PlaceRight("Expand All", 92, TreeToolbarGroupGap,
                "Expands every branch of the Recipe Tree, including nested children, so the full tree is visible.",
                () => InvokeTreeCommand(c => c.ExpandAll));
            PlaceRight("Buy All", 70, TreeToolbarButtonGap,
                "Forces every ingredient with a Trading Post price to Buy from TP, throughout the whole tree " +
                "including nodes hidden under bought items - replacing any manual choices already made. " +
                "Ingredients with no Trading Post price fall back to the solver's normal choice.",
                ConfirmBuyAll);
            PlaceRight("Craft All", 76, TreeToolbarButtonGap,
                "Forces every ingredient with a known recipe to Craft, throughout the whole tree including " +
                "nodes hidden under bought items - replacing any manual choices already made. Ingredients " +
                "with no recipe fall back to the solver's normal choice.",
                ConfirmCraftAll);
            PlaceRight("Best Path", 80, 0,
                "Clears every manual override, including Craft All/Buy All, and re-solves for the solver's " +
                "cheapest plan. Ignore selections are left unchanged.",
                ConfirmBestPath);

            PlaceTreeToolbarRow(w, rowY);
        }

        #region 4b. Tree action confirms - a dialog only when the click would change something

        // The matrix, in one sentence: a dialog appears ONLY when the
        // click would actually change the plan; otherwise the click skips
        // the dialog AND the re-solve, and the status line says why.
        // A dialog that protects nothing teaches people to click through
        // dialogs; a dead click with no feedback teaches them to click
        // again harder.
        //
        // Generate Plan is deliberately absent. It clears both overrides
        // and ignore marks, but it is the tab's primary action and gating
        // it would punish the ordinary case - its tooltip carries the
        // warning instead, which is why that tooltip is not optional.
        //
        // Every predicate is read at CLICK time from the live tree state
        // (TreeToolbarCommands), never cached per render: two of them
        // build a preset to compare against.

        private void InvokeTreeCommand(Func<TreeToolbarCommands, Action> pick)
        {
            var commands = _treeToolbarCommands;
            if (commands == null) return;
            pick(commands)?.Invoke();
        }

        /// <summary>
        /// Asks one question, with the clicked button's own verb as the
        /// confirm label, so the dialog reads as "you clicked X - really
        /// X?". A refused Show (another dialog already up) simply loses
        /// the click, which is correct under a modal: nothing was armed
        /// before asking.
        /// </summary>
        private void ShowTreeConfirm(string message, string confirmText, Action onConfirm)
        {
            if (onConfirm == null) return;
            _modalDialog?.Show(message, onConfirm, null, confirmText);
        }

        /// <summary>
        /// The matrix's zeroth question, asked by every entry before its
        /// own: can this plan be re-solved at all? A plan restored without
        /// its solve context renders and shows this toolbar, and nothing
        /// on it can run - so the answer is a line saying so, never a
        /// dialog for an action that will do nothing.
        /// </summary>
        private bool TreeCommandUnavailable(TreeToolbarCommands commands)
        {
            if (commands.CanReSolve?.Invoke() != false) return false;

            SetStatus(WithStandingNotices(StatusText.ReSolveUnavailable));
            return true;
        }

        private void ConfirmBestPath()
        {
            var commands = _treeToolbarCommands;
            if (commands == null) return;
            if (TreeCommandUnavailable(commands)) return;

            int overrides = commands.GetOverrideCount?.Invoke() ?? 0;
            if (overrides == 0)
            {
                SetStatus(WithStandingNotices(StatusText.NoOverridesToClear));
                return;
            }

            ShowTreeConfirm(
                "Clear " + StatusText.Count(overrides, "manual decision") +
                " and re-solve for the cheapest plan? Ignore marks are kept.",
                "Best Path", commands.BestPath);
        }

        private void ConfirmClearOverrides()
        {
            var commands = _treeToolbarCommands;
            if (commands == null) return;
            if (TreeCommandUnavailable(commands)) return;

            int overrides = commands.GetOverrideCount?.Invoke() ?? 0;
            if (overrides == 0)
            {
                SetStatus(WithStandingNotices(StatusText.NoOverridesToClear));
                return;
            }

            ShowTreeConfirm(
                "Clear " + StatusText.Count(overrides, "manual decision") +
                " and re-solve with the solver's own choices? Ignore marks are kept.",
                "Clear Overrides", commands.ClearOverrides);
        }

        private void ConfirmCraftAll()
        {
            ConfirmPreset(
                c => c.CraftAllWouldChange, c => c.CraftAll,
                StatusText.AlreadyCraftingEverything,
                "Craft everything with a known recipe?", "Craft All");
        }

        private void ConfirmBuyAll()
        {
            ConfirmPreset(
                c => c.BuyAllWouldChange, c => c.BuyAll,
                StatusText.AlreadyBuyingEverything,
                "Buy everything with a Trading Post price?", "Buy All");
        }

        /// <summary>
        /// Craft All and Buy All are the same shape: skip when the preset
        /// already IS the current override map, otherwise ask, naming what
        /// the click replaces. The "this replaces N" sentence is dropped
        /// when N is zero - there is nothing to replace, and a dialog that
        /// says "replaces 0 manual decisions" is asking about nothing.
        /// <para>
        /// Three answers, not two. UNAVAILABLE (no solve context to build a
        /// preset from) is not the same as UNNECESSARY, and reporting it as
        /// the no-op line would state something about the plan's contents
        /// that nothing has read - see TreeToolbarCommands. The null branch
        /// is the predicate's own contract rather than a second copy of
        /// TreeCommandUnavailable's answer: the two read the same field,
        /// and a predicate that can return null must have a caller that
        /// handles null.
        /// </para>
        /// </summary>
        private void ConfirmPreset(
            Func<TreeToolbarCommands, Func<bool?>> pickPredicate,
            Func<TreeToolbarCommands, Action> pickAction,
            string noOpStatus, string question, string confirmText)
        {
            var commands = _treeToolbarCommands;
            if (commands == null) return;
            if (TreeCommandUnavailable(commands)) return;

            bool? wouldChange = pickPredicate(commands)?.Invoke();
            if (wouldChange == null)
            {
                SetStatus(WithStandingNotices(StatusText.ReSolveUnavailable));
                return;
            }

            if (wouldChange == false)
            {
                SetStatus(WithStandingNotices(noOpStatus));
                return;
            }

            int overrides = commands.GetOverrideCount?.Invoke() ?? 0;
            string message = overrides > 0
                ? question + " This replaces " + StatusText.Count(overrides, "manual decision") + "."
                : question;

            ShowTreeConfirm(message, confirmText, pickAction(commands));
        }

        private void ConfirmClearIgnored()
        {
            var commands = _treeToolbarCommands;
            if (commands == null) return;
            if (TreeCommandUnavailable(commands)) return;

            // The control is hidden at zero, so the predicate is always
            // true when it is clickable - the guard is what makes that a
            // fact rather than an assumption.
            int ignored = commands.GetIgnoredCount?.Invoke() ?? 0;
            if (ignored == 0) return;

            ShowTreeConfirm(
                "Stop ignoring " + StatusText.Count(ignored, "item") +
                "? Their material costs count toward the plan again.",
                "Clear Ignored", commands.ClearIgnored);
        }

        #endregion // 4b. Tree action confirms - a dialog only when the click would change something

        // The two per-plan STATE chips, in the slot the grey "Recipe Tree:"
        // caption used to hold. Built once per Build() and shown/hidden by
        // RefreshTreeStateChips, which every render calls.
        private Label _overridesChipLabel;
        private StandardButton _clearOverridesButton;
        private Label _ignoredChipLabel;
        private StandardButton _clearIgnoredButton;

        private const int ClearOverridesButtonWidth = 124;
        private const int ClearIgnoredButtonWidth = 110;

        /// <summary>
        /// Rightmost x the chip strip may reach, written by
        /// PlaceTreeToolbarRow from the live row width and read by
        /// RefreshTreeStateChips. Zero until the first placement, which
        /// hides the chips rather than guessing - Build() places the row
        /// before anything can ask for a count.
        /// </summary>
        private int _treeChipLimitX;

        /// <summary>
        /// Builds the Overrides/Ignored chips. Their TEXT and visibility
        /// come from RefreshTreeStateChips - these are per-plan state, and
        /// a Build() may happen with a plan already on screen.
        /// </summary>
        private void CreateTreeStateChips()
        {
            _overridesChipLabel = ChipLabel();
            _clearOverridesButton = ChipButton(
                "Clear Overrides", ClearOverridesButtonWidth,
                "Drops every manual craft/buy decision and re-solves with the solver's own choices. " +
                "Ignore marks are kept.",
                ConfirmClearOverrides);

            _ignoredChipLabel = ChipLabel();
            TooltipFacility.ApplyPlain(
                _ignoredChipLabel, IgnoredChipTooltip);
            _clearIgnoredButton = ChipButton(
                "Clear Ignored", ClearIgnoredButtonWidth,
                IgnoredChipTooltip + "\nClears every ignore mark and re-solves.",
                ConfirmClearIgnored);

            TooltipFacility.ApplyPlain(
                _overridesChipLabel,
                "Craft/buy decisions you have set by hand. They survive a re-solve and are cleared by " +
                "Generate Plan.");
        }

        private const string IgnoredChipTooltip =
            "Ignored items are treated as fully in-hand and cost nothing in this plan.";

        private Label ChipLabel()
        {
            return new Label()
            {
                Font = UiFonts.Body,
                Text = "",
                TextColor = Color.White,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Visible = false,
                Location = new Point(0, TreeToolbarButtonY + 3),
                Parent = _treeToolbarPanel
            };
        }

        private StandardButton ChipButton(string text, int width, string tooltipText, Action onClick)
        {
            var button = new FeedbackButton()
            {
                Text = text,
                Size = new Point(width, TreeToolbarButtonHeight),
                Visible = false,
                Parent = _treeToolbarPanel
            };
            TooltipFacility.ApplyPlain(button, tooltipText);
            button.Click += (_, __) => onClick();
            return button;
        }

        /// <summary>
        /// Re-reads both counts from the live tree state and shows, hides
        /// and lays out the chips accordingly. Called after every render
        /// that can have changed them, which is every render: a pill click,
        /// a preset, a chip's own clear, and a fresh Generate (which clears
        /// both) - and by PlaceTreeToolbarRow, because the room the strip
        /// has changes with the row's width and not only with the counts.
        /// <para>
        /// Two independent reasons a control here is hidden, deliberately
        /// combined rather than merged: the chip's own count is zero, or
        /// the strip does not fit beside the right-hand buttons. They
        /// answer different questions and neither implies the other.
        /// </para>
        /// </summary>
        private void RefreshTreeStateChips()
        {
            if (_overridesChipLabel == null) return;

            var commands = _treeToolbarCommands;
            int overrides = commands?.GetOverrideCount?.Invoke() ?? 0;
            int ignored = commands?.GetIgnoredCount?.Invoke() ?? 0;

            bool showOverrides = _treeToolbarVisible && overrides > 0;
            bool showIgnored = _treeToolbarVisible && ignored > 0;

            // Measured from the font, not read back off the Label: an
            // AutoSizeWidth Label recomputes its Width during Blish's next
            // layout pass, so reading .Width in the same call that wrote
            // .Text yields the PREVIOUS text's width - and these two are
            // the only labels in the strip whose text changes at runtime.
            int overridesWidth = 0;
            int ignoredWidth = 0;
            if (showOverrides)
            {
                overridesWidth = SetChipText(_overridesChipLabel, StatusText.ForOverridesChip(overrides));
            }
            if (showIgnored)
            {
                ignoredWidth = SetChipText(_ignoredChipLabel, StatusText.ForIgnoredChip(ignored));
            }

            var placement = TreeChipStripLayout.Fit(
                0, _treeChipLimitX,
                showOverrides, overridesWidth, ClearOverridesButtonWidth,
                showIgnored, ignoredWidth, ClearIgnoredButtonWidth);

            _overridesChipLabel.Visible = showOverrides && placement.ShowCounts;
            _clearOverridesButton.Visible = showOverrides && placement.ShowButtons;
            _ignoredChipLabel.Visible = showIgnored && placement.ShowCounts;
            _clearIgnoredButton.Visible = showIgnored && placement.ShowButtons;

            var slots = placement.Slots;
            _overridesChipLabel.Location = new Point(slots.OverridesLabelX, TreeToolbarButtonY + 3);
            _clearOverridesButton.Location = new Point(slots.OverridesButtonX, TreeToolbarButtonY);
            _ignoredChipLabel.Location = new Point(slots.IgnoredLabelX, TreeToolbarButtonY + 3);
            _clearIgnoredButton.Location = new Point(slots.IgnoredButtonX, TreeToolbarButtonY);
        }

        /// <summary>
        /// Writes a chip's text and returns the width it will render at,
        /// measured in its own font. Also re-pins the label's height: both
        /// chips carry a descender ("Ignored:" has its g) and a label
        /// autosized to its exact text height loses it to Blish's scissor
        /// round trip - see LabelHelpers.WithDescenderClearance.
        /// </summary>
        private static int SetChipText(Label label, string text)
        {
            label.Text = text;
            LabelHelpers.WithDescenderClearance(label);
            return (int)Math.Ceiling(label.Font.MeasureString(text).Width);
        }

        /// <summary>
        /// Repositions and re-sizes the toolbar row and its right-anchored
        /// buttons - pure geometry, no rebuild, so it is safe on every
        /// resize tick. The sole writer of the panel's Visible/Size, and it
        /// reads _treeToolbarVisible, the same flag TopRegionLayoutMath is
        /// handed.
        /// <para>
        /// A hidden row is given zero height as well as Visible = false.
        /// The strip's arithmetic collapses the row entirely when it is
        /// hidden, which puts its Y exactly on the status row - so a
        /// full-height panel there would sit over the top few pixels of
        /// the scrollable content area, and this way it cannot intercept
        /// anything even if Blish's hit-testing ever stopped honouring
        /// Visible.
        /// </para>
        /// <para>
        /// The walk that anchors the buttons also PUBLISHES where their
        /// cluster starts, and the chips are re-fitted against it. The two
        /// clusters share one row and only this method knows its width, so
        /// a left cluster laid out without that number is a left cluster
        /// laid out over the buttons - which is what the chips did before
        /// TreeChipStripLayout.Fit existed.
        /// </para>
        /// </summary>
        private void PlaceTreeToolbarRow(int w, int rowY)
        {
            if (_treeToolbarPanel == null) return;

            _treeToolbarPanel.Visible = _treeToolbarVisible;
            _treeToolbarPanel.Size = new Point(
                w, _treeToolbarVisible ? TopRegionLayoutMath.TreeToolbarRowHeight : 0);
            _treeToolbarPanel.Location = new Point(0, rowY);

            int x = w - RightEdgePadding;
            foreach (var (button, width, gapToLeft) in _treeToolbarButtons)
            {
                x -= width;
                button.Location = new Point(x, TreeToolbarButtonY);
                x -= gapToLeft;
            }

            // The same group gap that separates the presets from the
            // view-only actions: the two clusters have to read apart, not
            // merely not overlap.
            _treeChipLimitX = x - TreeToolbarGroupGap;
            RefreshTreeStateChips();
        }

        /// <summary>
        /// Shows or hides the toolbar row, reflowing the strip below it
        /// only when the answer actually changed - a plan re-render that
        /// keeps its tree (every pill click, every preset) must not shift
        /// the layout at all.
        /// </summary>
        private void ApplyTreeToolbarVisibility(bool visible)
        {
            if (_treeToolbarVisible == visible) return;

            _treeToolbarVisible = visible;
            ReflowTopRegion();
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
            // Build()/ReflowTopRegion use rather than fixed constants.
            var layout = ComputeTopRegionLayout();
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
            PlaceTreeToolbarRow(w, layout.TreeToolbarRowY);
            _statusLabel.Location = new Point(0, layout.StatusRowY);
            InlineSpinner.PlaceAfter(_statusSpinner, _statusLabel, InlineSpinnerLayout.LabelGap);
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
            //
            // NOT gated on _currentPlan: the empty state registers relayout
            // closures too (its centered label and the spacer above it are
            // width-sized), and gating this on a plan left them dead - a
            // no-plan tab dragged narrower kept the label centered on the
            // build-time width and overflowed the panel. ReplayRelayout
            // already returns immediately on an empty registry, which is
            // the same guard for the same cost.
            if (widthChanged)
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
            // tickers - see ResizeSettleStep. Still gated on _currentPlan,
            // unlike the replay above: every job this pass does (re-ellipsis,
            // the defensive replay, the scroll verify, the notes re-render)
            // is about rendered plan content, so a no-plan tab would spawn a
            // ticker per drag to do nothing.
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
                LogScrollDiag($"write writer=ResizePreserve frame={ScrollDiagFrame()} before={before:0.0000} after={ratio:0.0000} contentHeight={contentHeight} savedOffset={savedOffsetPx} newHeight={newContentPanelHeight}");
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
            if (_contentPanel == null) return;
            if (_relayoutActions.Count == 0 && _treeRelayoutActions.Count == 0) return;

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
                foreach (var relayout in _treeRelayoutActions)
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
        /// needed around them. The one case that genuinely needs a new
        /// height (a Notes line count that moved with the width) does not
        /// stretch that contract: the closure requests a rebuild instead,
        /// and this method runs it afterwards through PreserveScrollAcross
        /// like every other rebuild - see RequestRerenderAfterSettle.
        /// This method also arms the resize
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

                // A closure asked for a rebuild because it could not honour
                // the no-height-change contract at this width - the Notes
                // section renders one fixed-height row per WRAPPED LINE, so
                // a width that changes a note's line count changes the
                // section's height. Deferred to here rather than done
                // inside the closure because RenderPlan clears the very
                // registry RunReellipsis was iterating, and routed through
                // PreserveScrollAcross for the same reason every other
                // rebuild (Generate, pill re-solve, hide-unlocked toggle)
                // is. Once per settled drag at most, and only when a line
                // count actually moved.
                if (_rerenderAfterSettlePending && _currentPlan != null)
                {
                    PreserveScrollAcross(() => RenderPlan(_currentPlan));
                }
            }
            catch (Exception ex)
            {
                // Typically the content panel was disposed between the last
                // resize tick and the debounce firing (e.g. Build() ran
                // again for a tab reload mid-drag). Degrade silently:
                // whichever Build() call is current already rendered fresh
                // content at its own width.
                Logger.Warn(ex, "Resize settle pass skipped");
            }
            finally
            {
                _rerenderAfterSettlePending = false;
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
            foreach (var reellipsis in _treeReellipsisActions)
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

                // Undoes the optimistic arm above. Used for both the dialog's
                // Cancel (which its X/Escape path also runs) and a refused
                // Show: the shared dialog is one instance, so another tab's
                // confirm being on screen would otherwise leave this checkbox
                // disabled with no callback left to re-enable it.
                Action revert = () =>
                {
                    _useOwnMaterials = !_useOwnMaterials;
                    _suppressToggle = true;
                    _ownMaterialsCheckbox.Checked = _useOwnMaterials;
                    _suppressToggle = false;
                    _ownMaterialsCheckbox.Enabled = true;
                    _valueOwnMaterialsCheckbox.Enabled = _useOwnMaterials;
                };

                // Aligned to the tree's confirm matrix: state the outcome
                // in the user's terms AND what it costs them. It was the
                // one dialog in the tab that did not say what is lost.
                bool shown = _modalDialog.Show(
                    newValue
                        ? "Regenerate the plan with own materials counted? Manual decisions and ignore marks are cleared."
                        : "Regenerate the plan with own materials excluded? Manual decisions and ignore marks are cleared.",
                    () =>
                    {
                        _ownMaterialsCheckbox.Enabled = true;
                        _ = TriggerGenerate();
                    },
                    revert,
                    confirmText: "Regenerate");

                if (!shown)
                {
                    revert();
                }

                return;
            }

            _useOwnMaterials = newValue;
            _valueOwnMaterialsCheckbox.Enabled = _useOwnMaterials;

            // Only reached with no plan on screen (the branch above
            // regenerates behind a confirm), so nothing is being made
            // stale here - but the toggle still only takes effect on the
            // next Generate, and saying so beats leaving "Ready" up.
            MarkSettingsChanged();
        }

        /// <summary>
        /// Records that a toolbar control the next Generate will act on has
        /// changed, and re-renders the strip so the warning appears at once.
        /// The warning is standing state, not a one-shot status write: a
        /// generation already in flight re-renders the strip every spinner
        /// tick and would otherwise wipe it within 150ms, ending on
        /// "Plan generated - &lt;time&gt;" for a plan built with the setting
        /// the user just changed away from.
        /// </summary>
        private void MarkSettingsChanged()
        {
            _settingsChangedPending = true;
            RenderFromBoard(_statusBoard.Snapshot());
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
        /// <para>
        /// It also owns the Generate button for the length of that
        /// resolution - the generate body's own disable/re-enable pair
        /// starts too late to cover it - and every path out of here hands
        /// the button back, including the one where the marshaled callback
        /// is dropped instead of queued.
        /// </para>
        /// </summary>
        private async Task TriggerGenerate()
        {
            var pending = CollectUnresolvedTypedRows();
            if (pending.Count == 0)
            {
                await GenerateFromResolvedRows(false);
                return;
            }

            // The search below may genuinely await (the shipped provider
            // does not, but the interface allows it and this whole hop
            // exists because of that). Nothing downstream disables the
            // button until a generation actually starts, so without this a
            // click during the search would look like it did nothing and
            // every further click would start another full generation -
            // _generateSequence makes the last result win, it does not stop
            // the redundant work.
            SetGenerateEnabled(false);
            SetStatus(ResolvingStatus);

            var matches = await FindTypedRowMatchesAsync(pending);
            bool queued = MainThreadMarshal.Run(() =>
            {
                // Resolution is over either way, so hand the button back
                // first; GenerateFromResolvedRows disables it again itself,
                // synchronously, if a run actually starts.
                SetGenerateEnabled(true);

                // Torn down while the search was in flight (tab
                // switched away, module unloading) - nothing to
                // generate into, same bail every other deferred
                // callback in this file takes.
                if (_contentPanel == null || _contentPanel.Parent == null) return;

                bool anyAmbiguous = AdoptTypedRowMatches(matches);
                _ = GenerateFromResolvedRows(anyAmbiguous);
            });

            if (!queued)
            {
                // Overlay is gone, so that callback will never drain and
                // the button would stay disabled for the rest of this
                // panel's life. The main-thread update loop this would
                // otherwise race with is exactly what is missing.
                SetGenerateEnabled(true);
            }
        }

        private void SetGenerateEnabled(bool enabled)
        {
            if (_generateButton != null)
            {
                _generateButton.Enabled = enabled;
            }
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
        /// rather than adopting whatever ranked first, a name several items
        /// share stays unresolved too (see
        /// <see cref="ItemRowSelection.MatchTypedName"/>), and a failing
        /// search resolves nothing - all three land on a status pointing at
        /// the suggestion list. Runs off the main thread after its first
        /// await, so it only reads its own snapshot and never touches row
        /// state; adoption is AdoptTypedRowMatches' job.
        /// <para>
        /// Every provider call is guarded, so this does not throw - which is
        /// what lets the caller pair its button disable with a single
        /// re-enable rather than a try/finally around the await.
        /// </para>
        /// </summary>
        private async Task<List<(ItemRowState Row, string Text, TypedNameMatch Match)>> FindTypedRowMatchesAsync(
            IReadOnlyList<(ItemRowState Row, string Text)> pending)
        {
            var matches = new List<(ItemRowState Row, string Text, TypedNameMatch Match)>(pending.Count);
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

                var match = ItemRowSelection.MatchTypedName(results, entry.Text);
                if (match.Kind != TypedNameMatchKind.None)
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
        /// TextChanged handler exists to prevent. Rows removed while the
        /// search was in flight are skipped outright: their state belongs to
        /// nothing once _itemRows no longer holds them, and their search box
        /// has been disposed with the row panel.
        /// <para>
        /// Returns true when some row's name turned out to belong to more
        /// than one item, which the caller reports rather than resolving.
        /// </para>
        /// </summary>
        private bool AdoptTypedRowMatches(IReadOnlyList<(ItemRowState Row, string Text, TypedNameMatch Match)> matches)
        {
            bool anyAmbiguous = false;
            foreach (var entry in matches)
            {
                if (entry.Row.ItemId.HasValue || !_itemRows.Contains(entry.Row))
                {
                    continue;
                }

                if (!ItemRowSelection.NamesMatch(entry.Row.SearchBox?.Text, entry.Text))
                {
                    continue;
                }

                if (entry.Match.Kind == TypedNameMatchKind.Ambiguous)
                {
                    anyAmbiguous = true;
                    continue;
                }

                entry.Row.ItemId = entry.Match.Result.ItemId;
                entry.Row.ItemName = entry.Match.Result.Name;
            }

            return anyAmbiguous;
        }

        /// <summary>
        /// How many rows the user typed into that still resolve to no item.
        /// Read after adoption: these rows are absent from the request the
        /// plan is built from, so either nothing can be generated at all or
        /// the plan is missing something the strip has to admit to.
        /// </summary>
        private int CountUnresolvedTypedRows()
        {
            int count = 0;
            foreach (var row in _itemRows)
            {
                if (!row.ItemId.HasValue && !string.IsNullOrWhiteSpace(row.SearchBox?.Text))
                {
                    count++;
                }
            }

            return count;
        }

        private async Task GenerateFromResolvedRows(bool anyAmbiguousTypedName)
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

            // Rows with text that resolved to nothing. They are absent from
            // requestItems either way; whether that means "nothing to
            // generate" or "a plan missing an item you asked for" is
            // decided below.
            int unresolvedTypedRows = CountUnresolvedTypedRows();

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
                // Three different mistakes: nothing typed anywhere, text
                // that resolved to no item (a partial name, or one the
                // typed-name pass above could not match), or a name several
                // items share. The old single "select an item" line was
                // misleading for the last two - the box looked filled in.
                SetStatus(WithStandingNotices(
                    ItemRowSelection.EmptyRequestStatus(unresolvedTypedRows > 0, anyAmbiguousTypedName)));
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

            // The strip's standing notices now describe THIS run: the
            // toolbar settings it is being built with are no longer pending,
            // and the rows it leaves out are the ones still unresolved. Set
            // before ArmSpinnerTicker so the very first spinner render
            // already carries them, and kept for the life of the plan they
            // describe rather than written once and overwritten 150ms later.
            _settingsChangedPending = false;
            _unresolvedRowsNotice = ItemRowSelection.UnresolvedRowsNotice(unresolvedTypedRows);

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
                // replaces it like any other status text. Unlike the
                // standing notices above, the thing it reports is already
                // fixed on screen - the corrected quantity is in the box.
                SetStatus(WithStandingNotices("Quantity reset to 1 - generating..."));
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
                    ResetPerPlanSortState();
                    _lastDebugLog = result.DebugLog;
                    _currentPlan = vm;
                    _planGeneratedAt = DateTime.Now;

                    // Unconditional board write, deliberately BEFORE the
                    // panel-liveness bail: a completion landing while the
                    // panel is torn down must not drop the "Plan
                    // generated" text - a later Build() pulls it from the
                    // board instead.
                    _statusBoard.Finish(myGen, StatusText.Stamp("Plan generated", _planGeneratedAt));

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
                    _statusBoard.Finish(myGen, StatusText.ForGenerationFailure(ex.Message));
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
        /// Dims (or restores) the plan area, the Recipe Tree's action row
        /// included. A panel rebuilt mid-generation starts undimmed and is
        /// left that way - Build renders whatever plan state exists into a
        /// fresh FlowPanel, and the generation that dimmed the old one has
        /// nothing left to restore.
        /// <para>
        /// The toolbar row sits in the non-scrolling strip, outside
        /// _contentPanel, so it does not inherit that dim - but its five
        /// buttons mutate the very plan being superseded, and leaving them
        /// at full brightness above a faded tree says the opposite of what
        /// the dim says. Disabled as well as dimmed: Opacity does not block
        /// hit-testing, so without this a Best Path click mid-run re-solves
        /// a plan that is about to be thrown away. Both panels are created
        /// in the same Build pass, so the _contentPanel guard above covers
        /// the toolbar too.
        /// </para>
        /// </summary>
        private void SetContentDimmed(bool dimmed)
        {
            if (_contentPanel == null || _contentPanel.Parent == null) return;

            float opacity = dimmed ? StalePlanOpacity : 1f;
            _contentPanel.Opacity = opacity;

            if (_treeToolbarPanel != null)
            {
                _treeToolbarPanel.Opacity = opacity;
            }
            foreach (var entry in _treeToolbarButtons)
            {
                entry.Button.Enabled = !dimmed;
            }

            // The chips' clear buttons act on the plan being superseded,
            // so they go dead with the five beside them. The count labels
            // dim with the panel and keep reading, which is right: the
            // counts are still true of what is still on screen.
            if (_clearOverridesButton != null) _clearOverridesButton.Enabled = !dimmed;
            if (_clearIgnoredButton != null) _clearIgnoredButton.Enabled = !dimmed;
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

            // The spinner is shown on exactly the condition the old ASCII
            // glyph was appended on, and every branch below re-anchors it
            // through SetStatus.
            if (_statusSpinner != null)
            {
                _statusSpinner.Visible = snapshot.InFlight;
            }

            if (snapshot.InFlight)
            {
                string text = string.IsNullOrEmpty(snapshot.PhaseText) ? "Generating..." : snapshot.PhaseText;
                // The spinner trails the text rather than leading it, as
                // the ASCII glyph did before it: the phase text then always
                // lays out from the label's fixed x=0 origin, and only the
                // spinner moves as the text changes. Leading it would shift
                // every character of the phase text horizontally whenever
                // the standing notices changed the label's leading run.
                SetStatus(WithStandingNotices(text));
            }
            else if (!string.IsNullOrEmpty(snapshot.FinalStatusText))
            {
                SetStatus(WithStandingNotices(snapshot.FinalStatusText));
            }
            else
            {
                // Nothing generated this session: the fresh label already
                // reads "Ready", so only write when there is a standing
                // notice that would otherwise be lost on this rebuild.
                string standing = WithStandingNotices(null);
                if (!string.IsNullOrEmpty(standing))
                {
                    SetStatus(standing);
                }
            }
        }

        /// <summary>
        /// <paramref name="status"/> with the strip's standing notices
        /// appended - the facts that outlive any single status write (see
        /// _settingsChangedPending / _unresolvedRowsNotice). Returns
        /// <paramref name="status"/> itself, allocating nothing, in the
        /// ordinary case where there are none: this runs on every spinner
        /// render for the whole of every generation.
        /// </summary>
        private string WithStandingNotices(string status)
        {
            if (_unresolvedRowsNotice == null && !_settingsChangedPending)
            {
                return status;
            }

            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(status))
            {
                parts.Add(status);
            }

            if (_unresolvedRowsNotice != null)
            {
                parts.Add(_unresolvedRowsNotice);
            }

            if (_settingsChangedPending)
            {
                parts.Add(SettingsChangedStatus);
            }

            return string.Join(StatusNoticeSeparator, parts);
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
        /// re-render throttling.
        /// <see cref="PlanStripTickAction.RenderFinalAndStop"/> is what
        /// makes "the board reports finished -> render final status and
        /// stop" true without any separate completion-callback write into
        /// this control ever being needed. The strip re-renders once per
        /// SpinnerTickInterval,
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
        private void ResetContentPanelToEmpty(bool preserveTree = false)
        {
            if (!preserveTree)
            {
                _treeController.ResetTreeRenderState();
                _treeRelayoutActions.Clear();
                _treeReellipsisActions.Clear();
                _treeSectionControls = null;
            }

            _relayoutActions.Clear();
            _reellipsisActions.Clear();

            if (_contentPanel == null) return;

            // Detached, not disposed, and BEFORE the sweep: a preserved
            // tree's controls are children of the very panel being emptied.
            // They re-enter at the point the tree belongs in the flow -
            // see RenderPlan.
            if (preserveTree && _treeSectionControls != null)
            {
                foreach (var control in _treeSectionControls)
                {
                    control.Parent = null;
                }
            }

            foreach (var child in _contentPanel.Children.ToArray())
            {
                child.Dispose();
            }
        }

        // What the tab says when it holds no plan. The default state was
        // blank parchment plus a small "Ready" on the status strip, which
        // names no next action - the Log tab already answers the same
        // question with a dim label in its own empty content panel, and
        // this is that pattern.
        private const string EmptyPlanText =
            "No plan yet. Search for an item above, then click Generate Plan.";
        private const int EmptyPlanTopGap = 48;
        private static readonly Color EmptyPlanTextColor = new Color(150, 150, 150);

        /// <summary>
        /// Parents the empty-state label into the (already emptied) content
        /// panel. Nothing disposes it explicitly: it is a child of
        /// _contentPanel like every rendered section, so
        /// ResetContentPanelToEmpty sweeps it on the first render of a real
        /// plan - which is the "disposed on first render" the finding asks
        /// for, through the path that already exists rather than a second
        /// one that could drift from it.
        /// <para>
        /// The gap is a spacer Panel, not a Location: _contentPanel is a
        /// SingleTopToBottom FlowPanel and positions its own children, the
        /// same reason CreateSectionHeader emits a topGap panel.
        /// </para>
        /// </summary>
        private void ShowEmptyPlanState()
        {
            if (_contentPanel == null) return;

            // Starts from the same "nothing rendered yet" point RenderPlan
            // does, and for the same reason: this method registers a
            // relayout closure, and _relayoutActions is cleared ONLY here.
            // Without it, a tab visit with no plan would leave the previous
            // visit's closures in the registry, each one writing Size into
            // a control that visit already disposed. Idempotent - both call
            // sites reach it with the panel already empty (the rollback
            // path calls it explicitly first, deliberately, and both the
            // tree-state reset and the registry clears are repeat-safe).
            ResetContentPanelToEmpty();

            int panelWidth = _contentPanel.Width - RightEdgePadding;
            if (panelWidth < 0) panelWidth = 0;

            var topGap = new Panel()
            {
                Size = new Point(panelWidth, EmptyPlanTopGap),
                Parent = _contentPanel
            };

            var label = new Label()
            {
                Font = UiFonts.Body,
                Text = EmptyPlanText,
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = panelWidth,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = EmptyPlanTextColor,
                Parent = _contentPanel
            };

            _relayoutActions.Add(w =>
            {
                int width = w > 0 ? w : 0;
                topGap.Size = new Point(width, EmptyPlanTopGap);
                label.Width = width;
            });
        }

        /// <summary>
        /// The render a local re-solve (a decision pill, an ignore toggle,
        /// a tree preset) runs. It asks the tree to update itself in place
        /// first, and rebuilds the plan AROUND the tree when it can - see
        /// TreeSectionController.TryRefreshInPlace for the measured reason
        /// a shorter rebuild frame is what stops rapid clicking from
        /// dropping clicks. Any doubt inside that method is a full rebuild,
        /// which is exactly what this path did before it existed.
        /// </summary>
        private void RenderPlanAfterResolve(PlanViewModel vm)
        {
            if (_contentPanel == null) return;

            var treeRoots = ResolveTreeRoots(vm);
            if (treeRoots != null && _treeSectionControls != null &&
                _treeController.TryRefreshInPlace(treeRoots))
            {
                RenderPlan(vm, preserveTree: true);
                return;
            }

            RenderPlan(vm);
        }

        private void RenderPlan(PlanViewModel vm, bool preserveTree = false)
        {
            if (_contentPanel == null) return;

            ResetContentPanelToEmpty(preserveTree);

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

            var treeRoots = ResolveTreeRoots(vm);
            if (treeRoots != null && preserveTree)
            {
                // Re-attached at the point the tree occupies in the flow.
                // _contentPanel positions its children in child order, so
                // re-parenting here IS the ordering, and the refresh has
                // already brought their contents up to this solve.
                foreach (var control in _treeSectionControls)
                {
                    control.Parent = _contentPanel;
                }
            }
            else if (treeRoots != null)
            {
                int childrenBeforeTree = _contentPanel.Children.Count;
                _treeController.CreateTreeSection(treeRoots, panelWidth);
                _treeSectionControls = CapturedChildrenFrom(childrenBeforeTree);
            }
            else if (preserveTree && _treeSectionControls != null)
            {
                // Unreachable while the only caller decides preserveTree
                // from this same ResolveTreeRoots answer - but a detached
                // control with nowhere to go is a leak, not a no-op, so
                // this branch is the one that pays for it rather than a
                // future caller discovering it.
                foreach (var control in _treeSectionControls)
                {
                    control.Dispose();
                }
                _treeSectionControls = null;
                _treeController.ResetTreeRenderState();
                _treeRelayoutActions.Clear();
                _treeReellipsisActions.Clear();
            }

            foreach (var section in vm.Sections)
            {
                if (section.SectionType == PlanSectionType.Summary) continue;
                CreateCollapsibleSection(section, panelWidth);
            }

            // After the tree, so CreateTreeSection has already published
            // this render's toolbar commands (ResetContentPanelToEmpty
            // withdrew the previous render's at the top of this method).
            // A re-render that keeps its tree finds the visibility
            // unchanged and reflows nothing.
            ApplyTreeToolbarVisibility(treeRoots != null);

            // Last, and unconditional: both counts are per-plan state that
            // any render can have changed - a pill click, a preset, a
            // chip's own clear, or a fresh Generate, which clears both.
            RefreshTreeStateChips();
        }

        /// <summary>
        /// The content-panel children added since a recorded child count -
        /// how the tree section's own controls are identified without
        /// threading a handle for each one out through the section-header
        /// seam. A section's controls are contiguous by construction: they
        /// are appended, in order, by one builder call.
        /// </summary>
        private List<Control> CapturedChildrenFrom(int firstIndex)
        {
            var captured = new List<Control>();
            for (int i = firstIndex; i < _contentPanel.Children.Count; i++)
            {
                captured.Add(_contentPanel.Children[i]);
            }
            return captured;
        }

        /// <summary>
        /// The plan's top-level tree roots, or null when it has no tree at
        /// all. A multi-item batch supplies N roots directly
        /// (vm.MultiItemRoots); a single-item plan is wrapped into a
        /// one-element list so CreateTreeSection/RefreshTreeContainerHeights
        /// always deal with "a list of roots" - one root renders
        /// byte-identically to the single-tree path (see
        /// PlanContentHeightMath.MultiRootTreeFlowHeight's own doc comment).
        /// <para>
        /// Build() asks the same question before laying the top strip out,
        /// so "does this plan have a tree" is answered in exactly one
        /// place: the strip reserving a toolbar row and RenderPlan building
        /// a tree section cannot disagree.
        /// </para>
        /// </summary>
        private static List<CraftingTreeNode> ResolveTreeRoots(PlanViewModel vm)
        {
            if (vm == null) return null;
            if (vm.MultiItemRoots != null && vm.MultiItemRoots.Count > 0)
            {
                return vm.MultiItemRoots;
            }
            return vm.TreeRoot != null ? new List<CraftingTreeNode> { vm.TreeRoot } : null;
        }

        /// <summary>
        /// Plan header: rarity-framed item icon + the item's own name in
        /// its rarity colour + a grey quantity, left-aligned at the
        /// content gutter every section below it also starts at.
        ///
        /// Three separate things used to compete here. The block was
        /// CENTRED while everything under it was left-aligned, so the plan
        /// had no single left edge. It carried a right-aligned "Generated:
        /// ..." panel duplicating - to the minute - the timestamp the
        /// fixed status strip 70px above already shows, so a plan opened
        /// with the same text twice. And its title shared DefaultFont18
        /// with every collapsible section header, leaving the page with no
        /// typographic top level at all.
        ///
        /// So: the in-scroll timestamp is gone (the strip keeps it, and it
        /// never scrolls away); the title is left-aligned and rendered at
        /// DefaultFont32, and CreateSectionHeader drops to DefaultFont16,
        /// so Font18-and-up now belongs to the page title alone. The
        /// "Crafting Plan for " prefix is gone with it - the tab is
        /// already titled "Crafting Plan" and the strip already says "Plan
        /// generated", so the prefix cost half the title's width to repeat
        /// what two other elements say.
        /// </summary>
        private void CreatePlanHeader(PlanViewModel vm, int panelWidth)
        {
            const int headerHeight = 56;
            const int iconSize = 40;
            const int iconBorder = 2;
            const int iconPad = 10;

            // Same 8px content gutter the Summary section's tiles, the
            // currency table's icon column and the footnote all start at.
            const int headerX = 8;

            int frameSize = iconSize + iconBorder * 2;

            var titleFont = UiFonts.Display;

            // Regular weight, one tier down from the title it annotates -
            // and not the 18-regular it used to be, whose 4px space glyph
            // rendered " x 42 needed" no wider than Body did.
            var qtyFont = UiFonts.SmallHeading;

            string nameText = vm.TargetItemName ?? "Unknown Item";

            // "needed", not a bare count: the quantity here is what the
            // plan still has to obtain after owned materials were
            // subtracted, which is routinely smaller than the number in
            // the Qty box the user typed (live capture ph13: box 77,
            // header 42, 35 already owned). A bare "x 42" beside a box
            // reading 77 reads as a bug. Deliberately not "to craft" -
            // a root the solver decided to BUY is just as legitimate.
            string qtyText = vm.TargetQuantity > 1 ? $" x {vm.TargetQuantity} needed" : "";

            var nameMeasure = titleFont.MeasureString(nameText);
            int nameWidth = (int)System.Math.Ceiling(nameMeasure.Width);
            int textHeight = (int)System.Math.Ceiling(nameMeasure.Height);

            int qtyHeight = 0;
            if (qtyText.Length > 0)
            {
                qtyHeight = (int)System.Math.Ceiling(qtyFont.MeasureString(qtyText).Height);
            }

            int iconY = (headerHeight - frameSize) / 2;
            int textY = iconY + (frameSize - textHeight) / 2;
            // Bottom-aligned against the much taller name rather than
            // top-aligned, with a small optical lift off the descender
            // line, so the two sit on one reading line.
            int qtyY = textY + textHeight - qtyHeight - 4;

            var titlePanel = new Panel()
            {
                Size = new Point(panelWidth, headerHeight),
                Parent = _contentPanel
            };

            var iconFrame = IconControls.CreateRarityFramedIcon(
                titlePanel, vm.TargetIconUrl, vm.TargetRarity, headerX, iconY,
                iconSize: iconSize, borderThickness: iconBorder);

            int textX = headerX + frameSize + iconPad;
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

            // PlanViewModel carries no target item id of its own, so the
            // tree root - the very item this header names - is the id. A
            // multi-item batch has no single target and no single tooltip
            // either (TreeRoot is null there by design).
            //
            // Composed at hover time, so a plan restored from disk shows
            // its stats as soon as the background top-up lands (Q13).
            // Stamped on the Label and the icon as well as the panel:
            // anything lying over the panel wins the hover outright
            // (Control.ActiveControl is the deepest capturing control),
            // the same swallowed-hover class already fixed on tree rows.
            // The 44px icon is the header's largest target and the most
            // natural one to point at.
            var treeRoot = vm.TreeRoot;
            Func<TooltipContent> buildStatContent =
                () => TreeRowTooltipComposer.BuildStatTooltipContent(treeRoot, _getItemStatBlock);
            TooltipFacility.ApplyRichDeferred(titlePanel, buildStatContent);
            TooltipFacility.ApplyRichDeferred(nameLabel, buildStatContent);

            // The icon only for a real item root: a multi-item batch has
            // no single target (TreeRoot is null by design), and stamping
            // an always-empty builder over the icon would replace its own
            // "no icon available" note with silence.
            if (TreeRowTooltipComposer.RowIdIsAnItemId(treeRoot))
            {
                IconControls.ApplyRichDeferredToIconTree(iconFrame, buildStatContent);
            }

            if (qtyText.Length > 0)
            {
                var qtyLabel = new Label()
                {
                    Text = qtyText,
                    Font = qtyFont,
                    TextColor = new Color(170, 170, 170),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(textX + nameWidth, qtyY),
                    Parent = titlePanel
                };
                TooltipFacility.ApplyRichDeferred(qtyLabel, buildStatContent);
            }

            // Every x here is now a constant or a font-only measurement,
            // so nothing in the title moves with the panel width - only
            // the panel's own cosmetic width, same as TextRowRenderer's
            // rows. The centring anchor (and the right-aligned timestamp
            // that needed one) is gone.
            _relayoutActions.Add(w => titlePanel.Size = new Point(w, headerHeight));
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
        /// sections and the Recipe Tree alike): caret + Font16 title, a 2px
        /// divider spanning the full width under the header, a hover wash on
        /// the whole clickable row, and click-to-toggle with expansion state
        /// persisted in _sectionExpansion under sectionKey. suppressToggle
        /// lets a caller with its own header-row control veto the toggle
        /// when the click landed on that control, and suppressPress does the
        /// same for the press feedback (Container.TriggerMouseInput raises
        /// the header's own press before walking to that control, so without
        /// it one press on the control dims the whole header and plays the
        /// click sound twice) - only Required Recipes' "Hide Unlocked"
        /// checkbox still needs either.
        /// </summary>
        private SectionHeaderHandle CreateSectionHeader(
            string title, PlanSectionType sectionKey, int panelWidth, bool defaultExpanded,
            Func<bool> suppressToggle = null, Func<bool> suppressPress = null)
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
                Size = new Point(panelWidth, SectionHeaderRowHeight),
                BackgroundColor = Color.Transparent,
                Parent = _contentPanel
            };
            headerPanel.MouseEntered += (_, __) => headerPanel.BackgroundColor = Color.White * 0.05f;
            headerPanel.MouseLeft += (_, __) => headerPanel.BackgroundColor = Color.Transparent;
            PressFeedback.Wire(headerPanel, suppressPress);

            // ASCII "v"/">" rather than the U+25BC/U+25B6 triangle glyphs:
            // pixel-level screenshot scans showed the triangles failing to
            // render here (and even on the tree's own row caret), so ASCII
            // is the only glyph confirmed to render. Do not re-attempt
            // Unicode without a fresh render check.
            var headerArrow = new Label()
            {
                Font = UiFonts.Body,
                Text = expanded ? "v" : ">",
                TextColor = Color.White,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(4, PlanContentHeightMath.SectionHeaderCaretY),
                Parent = headerPanel
            };

            // The top of the ramp below the plan title: a section header
            // outranks the column headers inside it, which in turn outrank
            // the rows. It used to be 18-regular - one nominal step over
            // Body, and the size whose space glyph collapses word gaps in
            // exactly these multi-word titles.
            new Label()
            {
                Text = title,
                Font = UiFonts.SectionTitle,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(22, PlanContentHeightMath.SectionHeaderTitleY),
                Parent = headerPanel
            };

            // Divider under the header - identical chrome for every section.
            // 2px, bottom-anchored inside the SectionHeaderRowHeight
            // headerPanel - see
            // LabelHelpers.CreateRowDivider's doc comment for why 1px is unsafe under
            // Blish's non-integer UI-scale GPU transform.
            // NOT built via LabelHelpers.CreateRowDivider (headerPanel is not a row of a
            // list, it has its own fixed SectionHeaderRowHeight) but it is built the
            // SAME way (a Panel child bottom-anchored near its parent's
            // bottom edge) and is subject to the identical Container.Paint
            // scissor round-trip defect. Simulation (M36b investigation)
            // showed a bottom-flush 2px line under the header's then-30px
            // height immune at the default 0.897 scale but vulnerable
            // (~16-17%) at the "Small" 0.81 scale. It gets the same 1px
            // bottom clearance as the vulnerable row types
            // (y = SectionHeaderRowHeight - 2 - 1). What the band's height
            // buys is the 2px between the title's lowest ink and this
            // rule's top - the arithmetic is in PlanContentHeightMath,
            // beside the constants.
            var headerDivider = new Panel()
            {
                Size = new Point(panelWidth, 2),
                Location = new Point(0, SectionHeaderRowHeight - 3),
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
            (_routeSectionChromeToTree ? _treeRelayoutActions : _relayoutActions).Add(w =>
            {
                topGap.Size = new Point(w, SectionSpacing);
                headerPanel.Size = new Point(w, SectionHeaderRowHeight);
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

        /// <summary>
        /// Rebuilds the plan after a sortable column header was clicked.
        /// The rows of a section are a FlowPanel's children in flow order,
        /// which is not reorderable in place, so the sort is applied the
        /// one way it can be - by rebuilding - and the rebuild goes through
        /// PreserveScrollAcross like every other one, so the reader keeps
        /// their scroll position. Row COUNT and row heights are identical
        /// before and after, so PlanContentHeightMath lands on exactly the
        /// same section height.
        /// <para>
        /// Rebuilding synchronously from inside a control's own click, and
        /// so disposing that control, is the established shape here - the
        /// "Hide Unlocked Recipes" checkbox rebuilds the same way from its
        /// own CheckedChanged, and a tree pill's re-solve from its own
        /// Click. No second, deferred mechanism is introduced for this one.
        /// </para>
        /// </summary>
        private void RerenderForSortChange()
        {
            if (_currentPlan == null) return;

            // The tree is a pure function of the plan, and a sort click
            // does not change the plan - only the row ORDER of one flat
            // table. So the tree section is not rebuilt, and not even
            // refreshed: its contents are already this plan's.
            PreserveScrollAcross(() => RenderPlan(_currentPlan, CanPreserveTree(_currentPlan)));
            // A sort header rebuilds the table it sits in - including
            // itself, under a cursor that has not moved. See
            // HoverChainResync.
            HoverChainResync.AfterRebuild();
        }

        /// <summary>
        /// Whether a re-render may keep the Recipe Tree section it already
        /// built: there has to BE one, and the plan it was built from has
        /// to be the plan being rendered. Callers that changed the plan
        /// itself go through TreeSectionController.TryRefreshInPlace
        /// instead, which brings the tree up to the new solve first.
        /// </summary>
        private bool CanPreserveTree(PlanViewModel vm)
        {
            return _treeSectionControls != null && ResolveTreeRoots(vm) != null;
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

            // Notes is the one section whose height is not a function of
            // its row list alone - a note wraps to as many fixed-height
            // line rows as its text needs at this width - so its renderer
            // reports the height it actually built. See
            // Views/Rendering/NotesSectionRenderer's doc comment.
            int? notesBodyHeight = null;

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
                    new UsedMaterialsSectionRenderer(
                        this, _usedMaterialsSort, RerenderForSortChange, _getItemStatBlock)
                        .Render(section, contentFlow, panelWidth);
                    break;
                case PlanSectionType.ShoppingList:
                    // Row rendering moved to
                    // Views/Rendering/ShoppingListSectionRenderer.
                    new ShoppingListSectionRenderer(
                        this, _shoppingListSort, RerenderForSortChange, _getItemStatBlock)
                        .Render(section, contentFlow, panelWidth);
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
                    notesBodyHeight = new NotesSectionRenderer(this)
                        .Render(section, contentFlow, panelWidth);
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
            // - see SummarySectionLayoutMath's own doc comment. Notes is
            // special-cased one step further: its renderer already returned
            // the wrapped-line height it built, which cannot drift from the
            // rows on screen because it IS those rows.
            int bodyHeight = notesBodyHeight ?? (section.SectionType == PlanSectionType.Summary
                ? SummarySectionLayoutMath.BodyHeight(section.Rows)
                : PlanContentHeightMath.SectionBodyHeight(section.SectionType, section.Rows));
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
        /// The header-row "Hide Unlocked" checkbox is now the only
        /// interactive control left in any section header (the Recipe
        /// Tree's five buttons moved to the non-scrolling strip - see
        /// TreeToolbarCommands), so this is the sole remaining user of
        /// CreateSectionHeader's suppressToggle guard:
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

            // suppressToggle reads the press-time flag (a click that began
            // off the checkbox still toggles the section); the press
            // feedback has to read MouseOver live instead, because it runs
            // during the very press that sets that flag and would otherwise
            // see the previous press's value. The checkbox is built below,
            // after the header it parents to - both predicates are only ever
            // called from a mouse event, long after that.
            Checkbox hideUnlockedCheckbox = null;
            bool pressStartedOnCheckbox = false;
            var header = CreateSectionHeader(
                headerTitle, section.SectionType, panelWidth, section.IsDefaultExpanded,
                () => pressStartedOnCheckbox,
                () => hideUnlockedCheckbox != null && hideUnlockedCheckbox.MouseOver);
            var headerPanel = header.HeaderPanel;
            var contentFlow = header.ContentFlow;

            const int checkboxWidth = 200;
            hideUnlockedCheckbox = new Checkbox()
            {
                Text = "Hide Unlocked Recipes",
                Checked = _hideUnlockedRecipes,
                Size = new Point(checkboxWidth, 24),
                Location = new Point(panelWidth - checkboxWidth, 3),
                Parent = headerPanel
            };
            TooltipFacility.ApplyPlain(
                hideUnlockedCheckbox,
                "Hide recipes you already know (Learned/Auto-learned) - show only the ones you are missing.");
            _relayoutActions.Add(w => hideUnlockedCheckbox.Location = new Point(w - checkboxWidth, 3));

            headerPanel.LeftMouseButtonPressed += (_, __) =>
            {
                pressStartedOnCheckbox = hideUnlockedCheckbox.MouseOver;
            };

            hideUnlockedCheckbox.CheckedChanged += (_, e) =>
            {
                // Same reasoning as RerenderForSortChange: this filters
                // one section's rows, it does not re-solve the plan.
                _hideUnlockedRecipes = e.Checked;
                PreserveScrollAcross(() => RenderPlan(_currentPlan, CanPreserveTree(_currentPlan)));
                HoverChainResync.AfterRebuild();
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
