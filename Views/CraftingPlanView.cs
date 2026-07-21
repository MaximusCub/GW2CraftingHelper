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
        private const int SectionSpacing = 16;

        // Shared divider greys. Both readable against the parchment texture;
        // SectionDividerColor is the brighter of the two, one tier below the
        // 180-grey structural separators (window chrome, unrelated to these).
        private static readonly Color RowDividerColor = new Color(100, 100, 100);
        private static readonly Color SectionDividerColor = new Color(130, 130, 130);

        private readonly Func<int, int, bool, PriceBasis, CancellationToken, IProgress<PlanStatus>, Task<CraftingPlanResult>> _generateAsync;
        private readonly Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, CraftingPlanResult> _resolveOverridesSync;
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
        private int _selectedItemId;
        private int _quantity = 1;

        // Bumped at the start of every TriggerGenerate call (Generate button
        // and OnOwnMaterialsToggled's modal-confirm path both funnel through
        // it). Each call captures its own value and every deferred callback
        // it queues re-checks it against the live field before applying
        // anything, so a superseded generation's result cannot clobber a
        // newer one (last-drained-wins) even though both entry points can
        // overlap in flight.
        private int _generateSequence;

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

        // Trailing debounce for resize-triggered re-renders. A re-render is a
        // full dispose+rebuild of the plan tree, which restarts nested
        // AutoSize height convergence; firing it on every >=1px resize tick
        // during a window drag flickers and transiently squashes deep tree
        // nodes. _resizeRenderPending gates a single in-flight debounce
        // ticker; each real frame it checks whether ResizeDebounceMs has
        // elapsed since the last resize tick, then fires one render once it
        // has - see ResizeDebounceStep and FrameTicker.
        private const int ResizeDebounceMs = 150;
        private DateTime _lastResizeEventUtc;
        private bool _resizeRenderPending;

        // Bumped by every PreserveScrollAcross call; an in-flight
        // StartScrollVerify loop compares its captured value against the
        // current one each frame and bails as soon as a newer restore has
        // superseded it.
        private int _scrollRestoreGeneration;

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
        // uses it for two real behavioral decisions: (1) any wheel event
        // observed since a verify window armed yields that window
        // immediately, no further contest; (2) a wheel event observed
        // within the last ~250ms suppresses the zero-reassert contest (a
        // user who just wheeled to exactly the top is not indistinguishable
        // from a library reset the way an idle bar reading zero is). Reset
        // at the top of every Build() so a stale value from a previous
        // render cannot influence a brand new one.
        private const int WheelSuppressWindowMs = 250;
        private DateTime? _lastWheelEventUtc;

        // M33 C1 (#12 diagnostics): instrumentation-only. Gated on
        // ModuleSettings.ScrollDiagnosticsEnabled (default false); every
        // call site below checks the live setting value BEFORE doing any
        // work so the cost when disabled is a single bool read, not a
        // formatted-string allocation. Never read by, or fed back into,
        // any scroll/guard/restore decision - diagnostics only observe.
        private const string ScrollDiagTag = "[scrolldiag]";

        // Monotonic frame index shared by every scroll-diagnostic log line
        // (wheel handler, Tick, GuardTick) so a human reading the log can
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

        public CraftingPlanView(
            Func<int, int, bool, PriceBasis, CancellationToken, IProgress<PlanStatus>, Task<CraftingPlanResult>> generateAsync,
            ModalDialog modalDialog,
            IItemSearchProvider itemSearchProvider,
            ModuleSettings settings,
            Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, CraftingPlanResult> resolveOverridesSync = null)
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
            mutate();
            if (saved > 0)
            {
                ApplySavedScrollSynchronously(saved, capturedGeneration);
            }
        }

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

            bool diagEnabled = _settings != null && _settings.ScrollDiagnosticsEnabled.Value;

            int contentHeight = MeasureContentHeight(capturedPanel);
            float ratio = ScrollMath.RatioForOffset(savedOffset, contentHeight, capturedPanel.Height);
            float before = scrollbar.ScrollDistance;
            scrollbar.ScrollDistance = ratio;

            if (diagEnabled)
            {
                Logger.Debug("{0} write writer=SyncRestore frame={1} before={2:0.0000} after={3:0.0000} contentHeight={4} savedOffset={5} generation={6}",
                    ScrollDiagTag, ScrollDiagFrame(), before, ratio, contentHeight, savedOffset, capturedGeneration);
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
        /// arm time. A wheel event observed within WheelSuppressWindowMs of
        /// a would-be zero-reassert suppresses that contest instead of
        /// bouncing the bar back down: a user who just wheeled to exactly
        /// the top is not distinguishable from a library reset by value
        /// alone (both read exactly 0), so recency of real input breaks the
        /// tie in the user's favor. The zero-reassert cap
        /// (ScrollVerifyZeroReassertCap) is kept as a last-resort guarantee
        /// that a persistent fight eventually ends even without a wheel
        /// signal to disambiguate it.
        /// </summary>
        private void StartScrollVerify(Panel capturedPanel, int capturedGeneration, int savedOffset, Scrollbar scrollbar)
        {
            int frame = 0;
            int zeroReassert = 0;
            DateTime armedAtUtc = DateTime.UtcNow;

            if (_settings != null && _settings.ScrollDiagnosticsEnabled.Value)
            {
                Logger.Debug("{0} verify-armed frame={1} savedOffset={2} generation={3}",
                    ScrollDiagTag, ScrollDiagFrame(), savedOffset, capturedGeneration);
            }

            bool VerifyTick(GameTime gameTime)
            {
                bool diagEnabled = _settings != null && _settings.ScrollDiagnosticsEnabled.Value;

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
                        Logger.Debug("{0} verify exit reason=stale-generation frame={1} realFrame={2} generation={3} liveGeneration={4}",
                            ScrollDiagTag, ScrollDiagFrame(), frame, capturedGeneration, _scrollRestoreGeneration);
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
                            Logger.Debug("{0} verify exit reason=wheel-observed frame={1} realFrame={2}",
                                ScrollDiagTag, ScrollDiagFrame(), frame);
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
                        // frame (see the class doc comment). Directive C:
                        // a wheel event inside the last WheelSuppressWindowMs
                        // means the user just wheeled to top themselves -
                        // that is not a library reset, so do not contest it.
                        bool recentWheel = _lastWheelEventUtc.HasValue &&
                            (DateTime.UtcNow - _lastWheelEventUtc.Value).TotalMilliseconds < WheelSuppressWindowMs;
                        if (recentWheel)
                        {
                            if (diagEnabled)
                            {
                                Logger.Debug("{0} verify exit reason=user-wheel-to-top frame={1} realFrame={2} target={3:0.0000}",
                                    ScrollDiagTag, ScrollDiagFrame(), frame, target);
                            }
                            return false;
                        }

                        scrollbar.ScrollDistance = target;
                        zeroReassert++;

                        if (diagEnabled)
                        {
                            Logger.Debug("{0} write writer=Verify/zeroReassert frame={1} realFrame={2} before={3:0.0000} after={4:0.0000} contentHeight={5} bounceCount={6}",
                                ScrollDiagTag, ScrollDiagFrame(), frame, current, target, contentHeight, zeroReassert);
                        }

                        if (zeroReassert >= ScrollVerifyZeroReassertCap)
                        {
                            if (diagEnabled)
                            {
                                Logger.Debug("{0} verify exit reason=zero-reassert-cap-exceeded frame={1} realFrame={2} bounceCount={3}",
                                    ScrollDiagTag, ScrollDiagFrame(), frame, zeroReassert);
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
                            Logger.Debug("{0} verify exit reason=user-scroll-detected frame={1} realFrame={2} observed={3:0.0000} target={4:0.0000} contentHeight={5}",
                                ScrollDiagTag, ScrollDiagFrame(), frame, current, target, contentHeight);
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
                            Logger.Debug("{0} verify exit reason=stable frame={1} realFrame={2} target={3:0.0000} contentHeight={4}",
                                ScrollDiagTag, ScrollDiagFrame(), frame, target, contentHeight);
                        }
                        return false;
                    }

                    if (frame < ScrollVerifyMaxFrames)
                    {
                        return true;
                    }

                    if (diagEnabled)
                    {
                        Logger.Debug("{0} verify exit reason=max-frames frame={1} realFrame={2} target={3:0.0000} contentHeight={4}",
                            ScrollDiagTag, ScrollDiagFrame(), frame, target, contentHeight);
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
                        Logger.Debug("{0} verify exit reason=disposed-exception frame={1} realFrame={2} error={3}",
                            ScrollDiagTag, ScrollDiagFrame(), frame, ex.GetType().Name);
                    }
                    return false;
                }
            }

            _scrollVerifyTicker?.Cancel();
            _scrollVerifyTicker = new FrameTicker(VerifyTick);
        }

        /// <summary>
        /// M33 C2a (directive C): unconditional (NOT diagnostics-gated) tap
        /// on the same MouseWheelScrolled event OnScrollDiagWheelScrolled
        /// below observes, recording only a timestamp. StartScrollVerify
        /// reads _lastWheelEventUtc to (1) yield a live verify window
        /// immediately the moment a wheel event lands in it, and (2)
        /// suppress a zero-reassert contest when a wheel event landed
        /// within the last WheelSuppressWindowMs. Both are real behavioral
        /// decisions now, not diagnostics, so unlike the tap below this
        /// must run regardless of ScrollDiagnosticsEnabled - cost is a
        /// single DateTime.UtcNow call per wheel notch, not per frame.
        /// </summary>
        private void OnContentWheelObserved(object sender, MouseEventArgs e)
        {
            _lastWheelEventUtc = DateTime.UtcNow;
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
            if (_settings == null || !_settings.ScrollDiagnosticsEnabled.Value)
            {
                return;
            }

            var scrollbar = PanelScrollbarField != null
                ? PanelScrollbarField.GetValue(_contentPanel) as Scrollbar
                : null;

            int contentHeight = MeasureContentHeight(_contentPanel);
            int wheelValue = GameService.Input.Mouse.State.ScrollWheelValue;
            bool verifyLive = _scrollVerifyTicker != null && _scrollVerifyTicker.IsActive;

            Logger.Debug(
                "{0} wheel frame={1} sign={2} raw={3} scrollDistance={4:0.0000} contentHeight={5} verifyLive={6}",
                ScrollDiagTag, ScrollDiagFrame(), System.Math.Sign(wheelValue), wheelValue,
                scrollbar?.ScrollDistance ?? -1f, contentHeight, verifyLive);
        }

        private void OnSelectedItemChanged(int itemId)
        {
            _selectedItemId = itemId;
        }

        public void Build(Container buildPanel)
        {
            // Clean up screen-parented popup from previous build cycle
            _suggestionPanel?.Dispose();

            // Same cleanup for any leftover scroll-verify/resize-debounce
            // tickers from the previous build cycle - see the field
            // comments above. Reset _resizeRenderPending too, or a ticker
            // canceled mid-debounce here would leave it stuck true and
            // silently disable all future resize debouncing. Also drop any
            // wheel-recency state from the previous render's tab so it
            // cannot influence a brand new one's verify window.
            _scrollVerifyTicker?.Cancel();
            _scrollVerifyTicker = null;
            _resizeDebounceTicker?.Cancel();
            _resizeDebounceTicker = null;
            _resizeRenderPending = false;
            _lastWheelEventUtc = null;

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

            // Re-render plan content when width changes (centered title, right-aligned
            // timestamps). Debounced to a single trailing render fired once the
            // resize drag settles - see ResizeDebounceStep and the _resize*
            // fields for why.
            if (_currentPlan != null && w != _lastRenderedWidth)
            {
                _lastResizeEventUtc = DateTime.UtcNow;
                if (!_resizeRenderPending)
                {
                    _resizeRenderPending = true;
                    _resizeDebounceTicker?.Cancel();
                    _resizeDebounceTicker = new FrameTicker(ResizeDebounceStep);
                }
            }
        }

        /// <summary>
        /// Trailing edge of the resize debounce. Ticks once per real frame
        /// while resize events keep landing within ResizeDebounceMs of one
        /// another, then fires a single re-render once the drag settles.
        /// _resizeRenderPending guarantees only one of these tickers is ever
        /// running, so repeated resize ticks just extend _lastResizeEventUtc
        /// rather than spawning parallel tickers.
        /// </summary>
        private bool ResizeDebounceStep(GameTime gameTime)
        {
            // The view may have been unloaded (tab switched away, module
            // disabled) while this was pending - nothing to render into.
            if (_contentPanel == null || _contentPanel.Parent == null)
            {
                _resizeRenderPending = false;
                return false;
            }

            if ((DateTime.UtcNow - _lastResizeEventUtc).TotalMilliseconds < ResizeDebounceMs)
            {
                return true;
            }

            _resizeRenderPending = false;

            try
            {
                // Re-read the panel width fresh rather than trust whatever w
                // was captured by the resize tick that started this ticker -
                // only the width at the moment the drag actually settled
                // matters.
                int currentWidth = _contentPanel.Width;
                if (_currentPlan != null && currentWidth != _lastRenderedWidth)
                {
                    _lastRenderedWidth = currentWidth;
                    PreserveScrollAcross(() => RenderPlan(_currentPlan));
                }
            }
            catch (Exception ex)
            {
                // The content panel was disposed between the last resize tick
                // and the debounce firing (e.g. Build() ran again for a tab
                // reload mid-drag). Degrade silently: whichever Build() call
                // is current already rendered fresh content at its own width.
                Logger.Warn(ex, "Resize debounce render skipped; content panel unavailable");
            }

            return false;
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
            // Captured before anything else. Both entry points that reach
            // TriggerGenerate (the Generate button's Click and the modal
            // confirm callback wired in OnOwnMaterialsToggled/ModalDialog)
            // are Blish UI event handlers, so this increment always runs on
            // the main thread before any await - no lock needed, and every
            // deferred callback below reads _generateSequence from the main
            // thread too (inside a MainThreadMarshal.Run callback).
            int myGen = ++_generateSequence;

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
                        if (myGen != _generateSequence) return;
                        SetStatus(ps.Message);
                    });
                }
            });

            try
            {
                var result = await _generateAsync(
                    _selectedItemId, _quantity, _useOwnMaterials, _priceBasis,
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

        private void RenderPlan(PlanViewModel vm)
        {
            if (_contentPanel == null) return;

            // Drop tree states up front so a plan without a tree section
            // does not retain disposed controls from the previous render.
            _treeNodeStates.Clear();
            _treeRoot = null;
            _treeFlow = null;

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
                ShowShadow = true,
                ShadowColor = Color.Black * 0.8f,
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
            // Consistent top gap before every section (including the tree),
            // so sections do not sit flush against whatever preceded them.
            new Panel()
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
            new Panel()
            {
                Size = new Point(panelWidth, 1),
                Location = new Point(0, 29),
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

            // M33 C2a (directive A): finalize contentFlow's real height
            // synchronously now that every row is populated, instead of
            // leaving it to Blish's per-frame AutoSize convergence. Pure
            // function of the same section data just rendered above, so it
            // cannot drift from what was actually built.
            contentFlow.Size = new Point(panelWidth, PlanContentHeightMath.SectionBodyHeight(section.SectionType, section.Rows));
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
                BackgroundColor = RowDividerColor,
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
                // White, not border: the fill exposes the border hue behind
                // the label, so border-colored text has zero contrast
                // against its own backdrop - same fix as RenderDecisionPills
                // (M30 #11); KNOWN-ISSUES #15 is this same bug on this tag.
                TextColor = Color.White,
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
            const int rowHeight = PlanContentHeightMath.UsedMaterialRowHeight;
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
                ShowShadow = true,
                ShadowColor = Color.Black * 0.8f,
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
            // this render (MeasureValueWidth accounts for a currency-only
            // or mixed row's icon(s) too, not just coin - KNOWN-ISSUES
            // #16). One pass over the section's rows (shopping lists run to
            // maybe 50-60 rows in practice) - negligible next to the
            // per-row control creation this method already does.
            int maxEachWidth = 0;
            int maxTotalWidth = 0;
            foreach (var row in section.Rows)
            {
                int eachW = MeasureValueWidth(row.UnitCoinValue, row.UnitCurrencyCosts, coinFont);
                if (eachW > maxEachWidth) maxEachWidth = eachW;

                int totalW = MeasureValueWidth(row.CoinValue, row.CurrencyCosts, coinFont);
                if (totalW > maxTotalWidth) maxTotalWidth = totalW;
            }

            int totalRightEdge = panelWidth - 8;
            var edges = ShoppingColumnMath.ComputeEdges(totalRightEdge, maxEachWidth, maxTotalWidth);

            // Both the header and every data row are handed this SAME
            // ColumnEdges instance, so they cannot drift apart for this
            // render.
            CreateShoppingListHeaderRow(contentFlow, panelWidth, edges);
            for (int i = 0; i < section.Rows.Count; i++)
            {
                CreateShoppingRow(section.Rows[i], contentFlow, panelWidth, edges, i == section.Rows.Count - 1);
            }
        }

        private static void CreateShoppingListHeaderRow(
            FlowPanel parent, int panelWidth, ShoppingColumnMath.ColumnEdges edges)
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
            CreateRightAlignedLabel(rowPanel, "Amount", font, color, edges.QtyRightEdge, 4);
            CreateRightAlignedLabel(rowPanel, "Each", font, color, edges.EachRightEdge, 4);
            CreateRightAlignedLabel(rowPanel, "Total", font, color, edges.TotalRightEdge, 4);
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

        private static void CreateShoppingRow(
            PlanRowViewModel row, FlowPanel parent, int panelWidth, ShoppingColumnMath.ColumnEdges edges, bool isLast)
        {
            const int rowHeight = PlanContentHeightMath.ShoppingRowHeight;
            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 8, 1);

            const int nameX = 50;
            var font = GameService.Content.DefaultFont14;

            string qtyText = $"{row.Quantity}x";
            int qtyWidth = (int)System.Math.Ceiling(font.MeasureString(qtyText).Width);
            int nameMaxWidth = System.Math.Max(20, edges.QtyRightEdge - qtyWidth - 12 - nameX);

            string fullName = row.Label ?? "";
            string displayName = EllipsizeToWidth(font, fullName, nameMaxWidth);
            var nameLabel = new Label()
            {
                Text = displayName,
                Font = font,
                TextColor = GetRarityNameColor(row.Rarity),
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
            if (!string.IsNullOrEmpty(row.HintText))
            {
                tooltipParts.Add(row.HintText);
            }
            if (tooltipParts.Count > 0)
            {
                rowPanel.BasicTooltipText = string.Join("\n", tooltipParts);
            }

            string sourceTag = ShoppingSourceTag(row);
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
                Location = new Point(edges.QtyRightEdge - qtyWidth, 9),
                Parent = rowPanel
            };

            // Each/Total cells: coin-only rows render exactly as before;
            // a row priced wholly or partly in a non-coin currency (e.g. a
            // vendor offer paid in spirit shards) renders currency segments
            // alongside/instead of coin; a row with neither (genuinely
            // unpriceable - gw2e: "Not sold or crafted") renders a dash,
            // never a blank cell (KNOWN-ISSUES #16).
            RenderValueCellRightAligned(rowPanel, row.UnitCoinValue, row.UnitCurrencyCosts, edges.EachRightEdge, 9, font);
            RenderValueCellRightAligned(rowPanel, row.CoinValue, row.CurrencyCosts, edges.TotalRightEdge, 9, font);

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
                ShowShadow = true, ShadowColor = Color.Black * 0.8f,
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
            const int rowHeight = PlanContentHeightMath.DisciplineRowHeight;
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
            int rowHeight = hasSublabel
                ? PlanContentHeightMath.RecipeRowHeightWithSublabel
                : PlanContentHeightMath.RecipeRowHeightNoSublabel;

            var rowPanel = new Panel() { Size = new Point(panelWidth, rowHeight), Parent = parent };

            CreateRarityFramedIcon(rowPanel, row.IconUrl, row.Rarity, 8, 1);

            var font = GameService.Content.DefaultFont14;
            int nameY = hasSublabel ? 4 : 8;
            new Label()
            {
                Text = row.Label ?? "",
                Font = font,
                TextColor = GetRarityNameColor(row.Rarity),
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

            const int rowHeight = PlanContentHeightMath.CostTileRowHeight;
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
                CreateCurrencyRow(row, contentFlow, panelWidth);
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
        }

        // Sized between the tree/row item-icon (32px) and the coin-segment
        // icon (20px) since it sits inside a plain 28px text row; reuses
        // CoinLabelIconGap (below, in the coin display helpers) for the
        // text-to-icon gap so both follow the same "number/text first, gap,
        // icon" convention.
        private const int CurrencyRowHeight = PlanContentHeightMath.CurrencyRowHeight;
        private const int CurrencyIconSize = 18;

        /// <summary>
        /// CurrencyCost row: identical "  {label}" text to CreateTextRow,
        /// plus the currency's icon immediately to its right when known.
        /// IconUrl null (no data available - service not wired up, fetch
        /// not yet complete, or the currency was absent from the API
        /// response) renders exactly like CreateTextRow - never a
        /// placeholder guess for a missing icon.
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

            if (!string.IsNullOrEmpty(row.IconUrl))
            {
                int iconX = 8 + label.Width + CoinLabelIconGap;
                int iconY = (CurrencyRowHeight - CurrencyIconSize) / 2;
                CreateItemIcon(rowPanel, row.IconUrl, iconX, iconY, CurrencyIconSize);
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

            // Whether lazily-built children (built on first expand) should
            // render dimmed - computed once from this node's own dimmed
            // state and decision, so it stays correct however many frames
            // later the user actually expands the node.
            public bool ChildDimmed;
        }

        // States for the current render pass; rebuilt with the tree itself.
        private readonly List<TreeNodeState> _treeNodeStates = new List<TreeNodeState>();

        // Root node + top-level content FlowPanel for the current render's
        // Recipe Tree section (null when the plan has no tree). Held so
        // RefreshTreeContainerHeights - called from the tree row toggle
        // handler deep inside RenderTreeNode's recursion, as well as from
        // CreateTreeSection itself - can recompute treeFlow's own explicit
        // Height without threading both through every recursive call.
        private CraftingTreeNode _treeRoot;
        private FlowPanel _treeFlow;

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
            _treeRoot = treeRoot;
            _treeFlow = treeFlow;

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

            // M33 C2a (directive A): every container this initial build
            // populated (treeFlow plus every childFlow created for a
            // default-expanded node) still reads its construction-time
            // Size.Y of 0 at this point - one synchronous pass now finalizes
            // every one of them from the same PlanContentHeightMath
            // arithmetic the rows above were just laid out with, before
            // this method returns to RenderPlan/PreserveScrollAcross.
            RefreshTreeContainerHeights(panelWidth);

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
                RefreshTreeContainerHeights(panelWidth);
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
                RefreshTreeContainerHeights(panelWidth);
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
        private void RefreshTreeContainerHeights(int panelWidth)
        {
            foreach (var state in _treeNodeStates)
            {
                state.ChildContainer.Size = new Point(
                    panelWidth,
                    PlanContentHeightMath.ChildrenHeight(
                        state.Node.Children, state.Depth + 1, state.ChildDimmed, _nodeExpansion));
            }

            if (_treeRoot != null && _treeFlow != null)
            {
                _treeFlow.Size = new Point(
                    panelWidth, PlanContentHeightMath.TreeNodeHeight(_treeRoot, 0, false, _nodeExpansion));
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
            new Label()
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
            if (node.Decision == CraftingDecision.Unknown && !string.IsNullOrEmpty(node.AcquisitionHint))
            {
                tooltipParts.Add(node.AcquisitionHint);
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
            // across every row regardless of digit count. Only rendered
            // when this node has a real committed decision with a cost
            // figure at all (SubtreeCost.HasValue) - HAVE/CURRENCY/UNKNOWN
            // nodes carry no SubtreeCost and keep the column blank exactly
            // as before (their own pill already communicates "no price").
            // Within that: a BuyFromVendor node priced wholly or partly in
            // a non-coin currency renders currency segments alongside/
            // instead of coin (sibling site to the shopping list's #16
            // fix, same RenderValueCellRightAligned entry point); a
            // decision whose real cost is genuinely zero-and-uncosted
            // renders a dash instead of an invented "0".
            if (node.SubtreeCost.HasValue)
            {
                var costFont = GameService.Content.DefaultFont14;
                var currencyAmounts = CurrencyDisplayResolver.ResolveAmounts(
                    node.VendorCurrencyCosts, _currentPlan?.CurrencyMetadata);
                RenderValueCellRightAligned(
                    rowPanel, node.SubtreeCost.Value, currencyAmounts, costRightEdge, 12, costFont, dimmed ? 0.35f : 1f);
            }

            // Child container. Children of a non-Craft decision are gw2e's
            // ".not-crafted" informational reference branch (what it would
            // cost to craft instead) - dimmed, and the flag does not stack
            // on already-dimmed branches.
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
                var childFlow = new FlowPanel()
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
                        if (pill.MouseOver)
                        {
                            return;
                        }
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
                        RefreshTreeContainerHeights(state.PanelWidth);
                    });
                };
                rowPanel.Click += toggleHandler;
            }
        }

        // --- Decision pills ---
        //
        // PillKind/PillSpec/BuildPillSpecs (the decision -> pill mapping,
        // gw2e's multi-pill model, KNOWN-ISSUES #18) live in
        // Services/DecisionPillPlanner.cs - Blish-free and directly unit
        // tested (DecisionPillPlannerTests) - so only the actual
        // Panel/Label rendering below stays view-only.

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
            var specs = DecisionPillPlanner.BuildPillSpecs(node);
            var font = GameService.Content.DefaultFont12;
            var pillPanels = new List<Panel>(specs.Count);
            int x = pillColX;

            foreach (var spec in specs)
            {
                int textWidth = (int)System.Math.Ceiling(font.MeasureString(spec.Text).Width);
                int pillWidth = textWidth + 12;

                GetPillColors(spec.Kind, out Color borderColor, out Color fillColor);
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
                case "Legendary": return new Color(160, 95, 240);
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
                case "Legendary": return new Color(160, 95, 240);
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

        // --- Currency + mixed value display helpers (KNOWN-ISSUES #16) ---
        //
        // A BuyFromVendor decision can be priced wholly or partly in a
        // non-coin currency (spirit shards, karma, ...). CurrencyAmountViewModel
        // (shopping rows, via PlanViewModelBuilder) and CraftingTreeNode.
        // VendorCurrencyCosts (tree, resolved here via CurrencyDisplayResolver)
        // both feed the same rendering below, so the two sibling sites named
        // in KNOWN-ISSUES #16 (shopping Each/Total cells and the tree cost
        // column) can never drift apart. Currency segments follow the exact
        // same "amount label, then icon to the RIGHT" convention as coin
        // segments (the coin invariant) and reuse its icon size/gaps; a
        // mixed value renders coin segments first, then currency segments.
        // A value with neither a coin price nor a currency cost is
        // genuinely unpriceable (gw2e: "Not sold or crafted") and renders a
        // plain dash - never a blank cell, never an invented "0".

        // ASCII-only source rule: em dash via escape, never a raw pasted
        // Unicode character - this IS the gw2e-style unpriceable dash
        // itself (KNOWN-ISSUES #16b), not incidental prose.
        private const string UnpricedDashText = "\u2014";
        private static readonly Color UnpricedDashColor = new Color(140, 140, 140);

        private struct CurrencySegmentSpec
        {
            public string IconUrl;
            public string Text;
            public int TextWidth;
        }

        private static List<CurrencySegmentSpec> BuildCurrencySegments(
            IReadOnlyList<CurrencyAmountViewModel> amounts, BitmapFont font)
        {
            var segments = new List<CurrencySegmentSpec>();
            if (amounts == null) return segments;

            foreach (var amount in amounts)
            {
                string text = amount.Amount.ToString();
                int width = (int)System.Math.Ceiling(font.MeasureString(text).Width);
                segments.Add(new CurrencySegmentSpec { IconUrl = amount.IconUrl, Text = text, TextWidth = width });
            }
            return segments;
        }

        /// <summary>
        /// The actual width arithmetic lives in ShoppingColumnMath
        /// (Blish-free, tested) so the pre-scan (MeasureValueWidth) and the
        /// real layout (LayoutValueSegmentsRightAligned) below can never
        /// drift apart; only the per-segment text measurement
        /// (BitmapFont.MeasureString) is Blish-bound and stays here.
        /// </summary>
        private static int TotalCurrencySegmentsWidth(List<CurrencySegmentSpec> segments)
        {
            var widths = new List<int>(segments.Count);
            foreach (var seg in segments) widths.Add(seg.TextWidth);
            return ShoppingColumnMath.SegmentRunWidth(widths, CoinIconSize, CoinLabelIconGap, CoinSegmentGap);
        }

        private static void LayoutCurrencySegments(
            Panel parent, List<CurrencySegmentSpec> segments, int startX, int y, BitmapFont font, float alphaScale = 1f)
        {
            int x = startX;
            Color textColor = new Color(220, 220, 220);
            if (alphaScale < 1f) textColor *= alphaScale;

            foreach (var seg in segments)
            {
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

                CreateItemIcon(parent, seg.IconUrl, x + seg.TextWidth + CoinLabelIconGap, y, CoinIconSize);

                x += seg.TextWidth + CoinLabelIconGap + CoinIconSize + CoinSegmentGap;
            }
        }

        /// <summary>
        /// Pixel width a coin/currency/mixed value would occupy if laid out
        /// via LayoutValueSegmentsRightAligned - built from the exact same
        /// BuildCoinSegments/BuildCurrencySegments + Total*SegmentsWidth
        /// path that layout call uses, so the shopping list's pre-scan
        /// (CreateShoppingListBody) can never drift from what actually
        /// renders. copper == 0 with at least one currency amount is a
        /// valid, currency-only case (not a "zero width" special case).
        /// </summary>
        private static int MeasureValueWidth(
            long copper, IReadOnlyList<CurrencyAmountViewModel> currencyAmounts, BitmapFont font)
        {
            int coinWidth = copper > 0 ? TotalCoinSegmentsWidth(BuildCoinSegments(copper, font)) : 0;
            int currencyWidth = TotalCurrencySegmentsWidth(BuildCurrencySegments(currencyAmounts, font));
            return (coinWidth > 0 && currencyWidth > 0) ? coinWidth + CoinSegmentGap + currencyWidth : coinWidth + currencyWidth;
        }

        /// <summary>
        /// Right-aligns coin segments (if copper &gt; 0) followed by
        /// currency segments (if any) to rightEdgeX - the "mixed
        /// coin+currency renders coin segments then currency segments"
        /// rule. Callers must not invoke this for a value with neither
        /// (RenderValueCellRightAligned handles that dash case instead).
        /// </summary>
        private static void LayoutValueSegmentsRightAligned(
            Panel parent, long copper, IReadOnlyList<CurrencyAmountViewModel> currencyAmounts,
            int rightEdgeX, int y, BitmapFont font, float alphaScale = 1f)
        {
            var coinSegments = copper > 0 ? BuildCoinSegments(copper, font) : new List<CoinSegmentSpec>();
            var currencySegments = BuildCurrencySegments(currencyAmounts, font);
            int coinWidth = TotalCoinSegmentsWidth(coinSegments);
            int currencyWidth = TotalCurrencySegmentsWidth(currencySegments);
            int gap = (coinWidth > 0 && currencyWidth > 0) ? CoinSegmentGap : 0;

            int startX = rightEdgeX - (coinWidth + gap + currencyWidth);
            LayoutCoinSegments(parent, coinSegments, startX, y, font, alphaScale);
            LayoutCurrencySegments(parent, currencySegments, startX + coinWidth + gap, y, font, alphaScale);
        }

        /// <summary>
        /// Single entry point for a shopping/tree value cell: coin-only,
        /// currency-only, and mixed all render via
        /// LayoutValueSegmentsRightAligned unchanged from (or, for
        /// currency/mixed, newly matching) the coin invariant; a value with
        /// neither a coin price nor a currency cost renders a plain dash
        /// instead of a blank cell or an invented "0" (KNOWN-ISSUES #16b).
        /// </summary>
        private static void RenderValueCellRightAligned(
            Panel parent, long copper, IReadOnlyList<CurrencyAmountViewModel> currencyAmounts,
            int rightEdgeX, int y, BitmapFont font, float alphaScale = 1f)
        {
            bool hasCoin = copper > 0;
            bool hasCurrency = currencyAmounts != null && currencyAmounts.Count > 0;

            if (!hasCoin && !hasCurrency)
            {
                Color dashColor = alphaScale < 1f ? UnpricedDashColor * alphaScale : UnpricedDashColor;
                CreateRightAlignedLabel(parent, UnpricedDashText, font, dashColor, rightEdgeX, y);
                return;
            }

            LayoutValueSegmentsRightAligned(parent, copper, currencyAmounts, rightEdgeX, y, font, alphaScale);
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
