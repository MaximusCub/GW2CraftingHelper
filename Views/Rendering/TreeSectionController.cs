using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-25 (m38-a1-architecture.md S3b-T2, hardest of the Wave G
    // extractions): moved verbatim out of CraftingPlanView's "8. Tree
    // rendering (state)"/"8. Tree rendering (continued)"/"9. Decision
    // pills" regions - the Recipe Tree section renderer AND the interactive
    // override loop it drives (Best Path/Craft All/Buy All presets, the
    // per-node craft/tp/vendor decision pills, and the Ignore pill), plus
    // every field that loop owns: TreeNodeState, _treeNodeStates,
    // _treeRoots/_treeFlow (the current render pass's tree bookkeeping),
    // _nodeOverrides/_ignoredItemIds/_nodeExpansion (session-persistent
    // decision/ignore/expansion state - M34-B2b, M21), and _lastResult (the
    // solve context the override loop re-resolves against).
    //
    // Unlike the six section renderers WP-23/WP-23b/WP-23c/WP-23d/WP-24
    // extracted before it, this component owns a slice of application
    // state, not just presentation - the field group above survives across
    // every local re-solve (a pill click never rebuilds it) and is reset
    // only once per genuinely new Generate. It also cannot reach several
    // things it still needs purely through ISectionRelayoutSink, because
    // those things are NOT relayout registrations: PreserveScrollAcross
    // (DO-NOT-TOUCH #3 - scroll preserve/restore/verify machinery, stays on
    // CraftingPlanView per the WP-26 cut-scope decision - see
    // docs/KNOWN-ISSUES.md), SetStatus, the top-level RenderPlan rebuild
    // entry point, GetCurrentPanelWidth, the view's own _currentPlan/
    // _lastDebugLog fields, and CreateSectionHeader (shared chrome every
    // section uses, including the plain PlanSectionType sections this
    // package never touches). Each is threaded in as a plain constructor
    // delegate - the same shape CraftingPlanView's own constructor already
    // takes generateAsync/resolveOverridesSync in - rather than handing
    // this class a reference to the view itself, which would reopen the
    // whole private surface this extraction is meant to shrink.
    // CreateSectionHeader's return type (SectionHeaderHandle) is a private
    // nested class of CraftingPlanView, so the delegate unpacks it into a
    // plain ValueTuple at the one call site inside CraftingPlanView's
    // constructor instead of widening that type's own accessibility.
    //
    // The only other non-move edits, beyond the established this-> _sink
    // substitution every extracted renderer makes: (1) the DEBUG
    // must-register assert inside CreateTreeSection used to read
    // _relayoutActions.Count directly (a private CraftingPlanView field);
    // it now reads the new ISectionRelayoutSink.RelayoutCount member added
    // by this package specifically for that (see the interface's own doc
    // comment - every other extracted renderer's equivalent assert stays
    // in CraftingPlanView.CreateCollapsibleSection, which still has direct
    // field access, so this is the first caller that needed it exposed
    // through the seam). (2) RenderTreeNode's cost-cell currency
    // resolution used to read the CraftingPlanView field _currentPlan
    // directly (_currentPlan?.CurrencyMetadata); it now calls the injected
    // getCurrentPlan() delegate - same value, since _currentPlan is always
    // already set to the very view model this render pass is building
    // before RenderPlan (and therefore CreateTreeSection) ever runs.
    // (3) CraftingPlanView.RenderPlan's top-of-method reset
    // (_treeNodeStates.Clear(); _treeRoots = null; _treeFlow = null;) and
    // TriggerGenerate's fresh-generation reset (_nodeOverrides.Clear();
    // _ignoredItemIds.Clear(); _nodeExpansion.Clear(); _lastResult =
    // result;) both moved onto this class as ResetTreeRenderState()/
    // ResetForNewPlan(result) - the two reset shapes are semantically
    // different (per-render-pass vs. per-generation - see each method's
    // own doc comment) so they stay two methods, not one.
    //
    // See docs/ARCHITECTURE.md section 5 (M38 WP-27) for the state-ownership
    // rationale and the WP-26 scroll/resize/wheel controller cut decision.
    internal sealed class TreeSectionController
    {
        private readonly ISectionRelayoutSink _sink;
        private readonly Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, ISet<int>, CraftingPlanResult> _resolveOverridesSync;
        private readonly PlanViewModelBuilder _vmBuilder;
        private readonly Action<Action> _preserveScrollAcross;
        private readonly Action<string> _setStatus;
        private readonly Action<PlanViewModel> _renderPlan;
        private readonly Func<int> _getCurrentPanelWidth;
        private readonly Func<PlanViewModel> _getCurrentPlan;
        private readonly Action<PlanViewModel> _setCurrentPlan;
        private readonly Action<IReadOnlyList<string>> _setLastDebugLog;
        private readonly Func<string, PlanSectionType, int, bool, Func<bool>, (Panel HeaderPanel, Label ArrowLabel, FlowPanel ContentFlow)> _createSectionHeader;

        private static readonly Logger Logger = Logger.GetLogger<TreeSectionController>();

        internal TreeSectionController(
            ISectionRelayoutSink sink,
            Func<PlanSolveContext, IReadOnlyDictionary<int, AcquisitionSource>, ISet<int>, CraftingPlanResult> resolveOverridesSync,
            PlanViewModelBuilder vmBuilder,
            Action<Action> preserveScrollAcross,
            Action<string> setStatus,
            Action<PlanViewModel> renderPlan,
            Func<int> getCurrentPanelWidth,
            Func<PlanViewModel> getCurrentPlan,
            Action<PlanViewModel> setCurrentPlan,
            Action<IReadOnlyList<string>> setLastDebugLog,
            Func<string, PlanSectionType, int, bool, Func<bool>, (Panel HeaderPanel, Label ArrowLabel, FlowPanel ContentFlow)> createSectionHeader)
        {
            // resolveOverridesSync is deliberately NOT null-guarded - the
            // sole production call site (CraftingPlanView's own
            // constructor) accepts it as an optional parameter defaulting
            // to null, and every reader below already treats a null value
            // as "override re-solve unavailable" (ApplyOverridesAndResolve
            // bails out; RenderDecisionPills renders its pills
            // non-interactive) rather than a construction-time error.
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _resolveOverridesSync = resolveOverridesSync;
            _vmBuilder = vmBuilder ?? throw new ArgumentNullException(nameof(vmBuilder));
            _preserveScrollAcross = preserveScrollAcross ?? throw new ArgumentNullException(nameof(preserveScrollAcross));
            _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
            _renderPlan = renderPlan ?? throw new ArgumentNullException(nameof(renderPlan));
            _getCurrentPanelWidth = getCurrentPanelWidth ?? throw new ArgumentNullException(nameof(getCurrentPanelWidth));
            _getCurrentPlan = getCurrentPlan ?? throw new ArgumentNullException(nameof(getCurrentPlan));
            _setCurrentPlan = setCurrentPlan ?? throw new ArgumentNullException(nameof(setCurrentPlan));
            _setLastDebugLog = setLastDebugLog ?? throw new ArgumentNullException(nameof(setLastDebugLog));
            _createSectionHeader = createSectionHeader ?? throw new ArgumentNullException(nameof(createSectionHeader));
        }

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

        // Solve context the override loop re-resolves against - the result
        // of the last full Generate or the last local override re-solve,
        // whichever happened most recently.
        private CraftingPlanResult _lastResult;

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
        /// M38 WP-25: per-render-pass reset, called from
        /// CraftingPlanView.RenderPlan before it disposes/rebuilds the
        /// content panel's children - moved verbatim from RenderPlan's own
        /// top-of-method _treeNodeStates.Clear()/_treeRoots = null/
        /// _treeFlow = null (a plan without a tree section must not retain
        /// disposed controls from the previous render). Deliberately
        /// separate from ResetForNewPlan below: this runs on EVERY
        /// RenderPlan call (a fresh Generate AND an override-resolve
        /// re-render both go through RenderPlan), while ResetForNewPlan
        /// only runs once per genuinely new Generate.
        /// </summary>
        internal void ResetTreeRenderState()
        {
            _treeNodeStates.Clear();
            _treeRoots = null;
            _treeFlow = null;
        }

        /// <summary>
        /// M38 WP-25: fresh-generation reset, called from
        /// CraftingPlanView.TriggerGenerate right before its own RenderPlan
        /// call - moved verbatim from TriggerGenerate's own
        /// _nodeOverrides.Clear()/_ignoredItemIds.Clear()/
        /// _nodeExpansion.Clear()/_lastResult = result. A brand new
        /// Generate discards every prior override/ignore/expansion
        /// decision and adopts the new solve result as the override loop's
        /// baseline.
        /// </summary>
        internal void ResetForNewPlan(CraftingPlanResult result)
        {
            _nodeOverrides.Clear();
            _ignoredItemIds.Clear();
            _nodeExpansion.Clear();
            _lastResult = result;
        }

        /// <summary>
        /// Review-fix (W3D adversarial review, critical): re-seeds the
        /// override loop's decision/ignore state from a persisted plan,
        /// called from CraftingPlanView.ApplyRestoredPlan immediately after
        /// ResetForNewPlan(result) (which the restore path also calls, to
        /// adopt the restored result as _lastResult exactly like a fresh
        /// Generate does - only the Clear()s above need undoing here).
        /// Without this, a restored session's <see cref="_nodeOverrides"/>/
        /// <see cref="_ignoredItemIds"/> would start empty even though the
        /// restored <paramref name="result"/> already reflects the user's
        /// prior overrides (it is the OUTPUT of applying them) - the very
        /// next pill click would then re-solve with only that ONE new
        /// override applied, silently discarding every override the user
        /// set before restarting (W3D spec item 3's correctness bar).
        /// <paramref name="nodeOverrides"/>/<paramref name="ignoredItemIds"/>
        /// are copied, not aliased - this instance owns its two collections
        /// for their entire lifetime (every other mutator below assumes
        /// that), and PersistedPlan's own copies must stay independent so a
        /// later pill click's Dictionary/HashSet mutation here can never
        /// reach back into the object PlanStoreHelpers just deserialized.
        /// </summary>
        internal void RestoreOverrides(
            IReadOnlyDictionary<int, AcquisitionSource> nodeOverrides,
            IReadOnlyList<int> ignoredItemIds)
        {
            if (nodeOverrides != null)
            {
                foreach (var kvp in nodeOverrides)
                {
                    _nodeOverrides[kvp.Key] = kvp.Value;
                }
            }

            if (ignoredItemIds != null)
            {
                foreach (int itemId in ignoredItemIds)
                {
                    _ignoredItemIds.Add(itemId);
                }
            }
        }

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
        // Moved verbatim from CraftingPlanView.CreateTreeSection. Edits:
        // CreateSectionHeader(...) -> the injected createSectionHeader
        // delegate (unpacked into local headerPanel/treeFlow, ArrowLabel
        // unused here exactly as it was unused in the original body) -
        // the suppressToggle argument is passed positionally rather than
        // named (`suppressToggle: () => ...`) since a plain Func<...>
        // delegate invocation has no parameter names of its own to match
        // against, same value either way; _relayoutActions.Add(...) ->
        // _sink.AddRelayout(...); the DEBUG must-register assert reads
        // _sink.RelayoutCount instead of _relayoutActions.Count;
        // PreserveScrollAcross(...) -> _preserveScrollAcross(...);
        // GetCurrentPanelWidth() -> _getCurrentPanelWidth().
        internal void CreateTreeSection(IReadOnlyList<CraftingTreeNode> treeRoots, int panelWidth)
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

            var header = _createSectionHeader(
                "Recipe Tree", PlanSectionType.RecipeTree, panelWidth, true,
                () => pressStartedOnButton);
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
            StandardButton PlaceButtonRight(string text, int width, string tooltipText)
            {
                cursorX -= width;
                var button = new StandardButton()
                {
                    Text = text,
                    Size = new Point(width, 24),
                    Location = new Point(cursorX, 3),
                    BasicTooltipText = tooltipText,
                    Parent = headerPanel
                };
                headerButtons.Add((button, width));
                cursorX -= 4;
                return button;
            }

            collapseAllButton = PlaceButtonRight("Collapse All", 96,
                "Collapses every branch of the Recipe Tree back down to the top level.");
            expandAllButton = PlaceButtonRight("Expand All", 92,
                "Expands every branch of the Recipe Tree, including nested children, so the full tree is visible.");
            buyAllButton = PlaceButtonRight("Buy All", 70,
                "Forces every ingredient with a Trading Post price to Buy from TP, throughout the whole tree " +
                "including nodes hidden under bought items - replacing any manual choices already made. " +
                "Ingredients with no Trading Post price fall back to the solver's normal choice.");
            craftAllButton = PlaceButtonRight("Craft All", 76,
                "Forces every ingredient with a known recipe to Craft, throughout the whole tree including " +
                "nodes hidden under bought items - replacing any manual choices already made. Ingredients " +
                "with no recipe fall back to the solver's normal choice.");
            bestPathButton = PlaceButtonRight("Best Path", 80,
                "Clears every manual override, including Craft All/Buy All, and re-solves for the solver's " +
                "cheapest plan. Ignore selections are left unchanged.");

            // M33 C2b: right-to-left button placement is font-only (fixed
            // widths) - pure reposition on every drag tick, same order as
            // PlaceButtonRight built them so the right-to-left offsets
            // reproduce identically.
            _sink.AddRelayout(w =>
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
            int relayoutCountBeforeTree = _sink.RelayoutCount;
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
                    _sink.AddRelayout(w => rootDivider.Size = new Point(w, PlanContentHeightMath.MultiRootDividerHeight));
                }
                RenderTreeNode(_treeRoots[i], treeFlow, panelWidth, 0, dimmed: false);
            }
#if DEBUG
            // M33 C2b (m2 risk 3): every RenderTreeNode call registers its
            // own relayout closure (see the field comment on
            // _relayoutActions) - a single root node still yields at least
            // one. Zero growth here would mean that mechanism itself
            // silently broke.
            if (_sink.RelayoutCount == relayoutCountBeforeTree)
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

            expandAllButton.Click += (_, __) => _preserveScrollAcross(() =>
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
                            RenderTreeNode(child, s.ChildContainer, _getCurrentPanelWidth(), s.Depth + 1, s.ChildDimmed);
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

            collapseAllButton.Click += (_, __) => _preserveScrollAcross(() =>
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

        // Moved verbatim from CraftingPlanView.ApplyPreset. No edits - both
        // fields/methods it touches (_lastResult, _nodeOverrides,
        // ApplyOverridesAndResolve) are this class's own.
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
        // Moved verbatim from CraftingPlanView.ApplyOverridesAndResolve.
        // Edits: _lastDebugLog = ... -> _setLastDebugLog(...); _currentPlan
        // = vm -> _setCurrentPlan(vm); PreserveScrollAcross(() =>
        // RenderPlan(vm)) -> _preserveScrollAcross(() => _renderPlan(vm));
        // SetStatus(...) -> _setStatus(...).
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
                _setLastDebugLog(result.DebugLog);
                var vm = _vmBuilder.Build(result);
                _setCurrentPlan(vm);
                _preserveScrollAcross(() => _renderPlan(vm));
                _setStatus(StatusText.ForOverrideResolve(isBestPathPreset, _nodeOverrides.Count));
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Override re-solve failed");
                _setStatus($"Error: {ex.Message}");
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
        // Moved verbatim from CraftingPlanView.RefreshTreeContainerHeights.
        // Only edit: GetCurrentPanelWidth() -> _getCurrentPanelWidth().
        private void RefreshTreeContainerHeights()
        {
            int panelWidth = _getCurrentPanelWidth();
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

        // Moved verbatim from CraftingPlanView.RenderTreeNode. Edits:
        // _relayoutActions.Add(...) -> _sink.AddRelayout(...);
        // _reellipsisActions.Add(...) -> _sink.AddReellipsis(...);
        // _currentPlan?.CurrencyMetadata -> _getCurrentPlan()?.
        // CurrencyMetadata; PreserveScrollAcross(...) ->
        // _preserveScrollAcross(...); GetCurrentPanelWidth() ->
        // _getCurrentPanelWidth().
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
            if (node.Quantity > 1 &&
                (node.Decision == CraftingDecision.BuyFromTp ||
                 node.Decision == CraftingDecision.BuyFromVendor))
            {
                // Field-test finding B: a pure-currency vendor offer
                // (spirit shards, karma, ...) has UnitCost == 0 (not null -
                // see CraftingTreeBuilder.BuildNode), which used to render a
                // misleading "0g 0s 0c" instead of the real per-unit
                // currency cost; a mixed coin+currency offer still shows
                // both lines below. The coin line is suppressed only when
                // it is genuinely zero AND a currency cost exists to show
                // instead of it.
                bool hasCurrencyCosts = node.VendorCurrencyCosts != null && node.VendorCurrencyCosts.Count > 0;
                if (node.UnitCost.HasValue && !(node.UnitCost.Value == 0 && hasCurrencyCosts))
                {
                    extraTooltipLines.Add("Unit price: " + CoinCurrencyRenderer.FormatCoinText(node.UnitCost.Value));
                }
                if (node.Decision == CraftingDecision.BuyFromVendor && hasCurrencyCosts)
                {
                    var unitCurrencyAmounts = CurrencyDisplayResolver.ResolveTreeNodeUnitAmounts(
                        node.VendorCurrencyCosts, node.Quantity, _getCurrentPlan()?.CurrencyMetadata);
                    if (unitCurrencyAmounts != null)
                    {
                        foreach (var amount in unitCurrencyAmounts)
                        {
                            string amountText = amount.BundleLabel ?? amount.Amount.ToString();
                            extraTooltipLines.Add($"Unit price: {amountText} {amount.Name}");
                        }
                    }
                }
            }
            // AUDIT ROW 20/38 (gw2e price-side fallback parity, DISPLAY
            // CAVEAT): this node's TP unit price came from the item's
            // NON-preferred side because the preferred side had no
            // listings (CraftingTreeBuilder.BuildNode/
            // SolverDecision.PriceSideFellBack) - flag it so the number
            // shown doesn't read as an ordinary preferred-side price.
            // Deliberately outside the node.Quantity > 1 gate above: this
            // caveat is about WHICH TP side priced the node, not about a
            // qty=1 row already showing its own total as the unit price.
            if (node.Decision == CraftingDecision.BuyFromTp && node.PriceSideFellBack)
            {
                extraTooltipLines.Add(_getCurrentPlan()?.PriceBasis == PriceBasis.BuyOrder
                    ? "Buy-order price unavailable - instant-buy price shown"
                    : "Instant-buy price unavailable - buy-order price shown");
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
            //
            // W4B: a node whose children are the new synthesized cost-
            // component leaves (see CraftingTreeBuilder.
            // BuildVendorCostComponentLeaves - every child of such a node is
            // a component leaf, never mixed with a reference branch or a
            // real craft child) shows ONLY the compact gold total here - no
            // currency segments - since the breakdown those segments used
            // to cram into this one row now lives one expand-click away as
            // real child rows. This is the fix for the exact collision the
            // W4B field case hit (a mixed coin/currency/item vendor cost
            // rendering as one very long segmented row).
            bool hasCostComponentChildren = node.Children.Count > 0 && node.Children[0].IsCostComponent;
            CoinCurrencyRenderer.ValueCellHandle costCell = null;
            if (node.SubtreeCost.HasValue)
            {
                var costFont = GameService.Content.DefaultFont14;
                var currencyAmounts = hasCostComponentChildren
                    ? null
                    : CurrencyDisplayResolver.ResolveAmounts(
                        node.VendorCurrencyCosts, _getCurrentPlan()?.CurrencyMetadata);
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
                    _preserveScrollAcross(() =>
                    {
                        if (!state.ChildrenBuilt)
                        {
                            // M33 C2b: read the LIVE width rather than the
                            // (possibly long-stale, since resize no longer
                            // triggers a rebuild) width this node itself was
                            // built at - see GetCurrentPanelWidth.
                            int currentWidth = _getCurrentPanelWidth();
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
            _sink.AddRelayout(w =>
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
            _sink.AddReellipsis(w =>
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
        // Moved verbatim from CraftingPlanView.UpdateTreeRowTooltip. No
        // edits - references no view state, only its own parameters.
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

        // --- Decision pills ---
        //
        // PillKind/PillSpec/BuildPillSpecs (the decision -> pill mapping,
        // gw2e's multi-pill model, KNOWN-ISSUES #18) live in
        // Services/DecisionPillPlanner.cs - Blish-free and directly unit
        // tested (DecisionPillPlannerTests) - so only the actual
        // Panel/Label rendering below stays here.

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
        // Moved verbatim from CraftingPlanView.RenderDecisionPills. Only
        // edit: the interactive/ignoreInteractive click handlers write
        // _nodeOverrides/_ignoredItemIds and call ApplyOverridesAndResolve
        // - both now this class's own field/method, so the bodies are
        // unchanged text.
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

                PillColors.GetPillColors(spec.Kind, node.IsIgnored, out Color borderColor, out Color fillColor);
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
                var label = new Label()
                {
                    Text = spec.Text,
                    Font = font,
                    TextColor = textColor,
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point((pillWidth - 2 - textWidth) / 2, 2),
                    Parent = inner
                };

                // Field-test finding D: tooltipText is resolved once below,
                // then stamped onto outer/inner/label together right before
                // the loop moves on - the inner fill panel and its label
                // cover almost the entire pill (outer is only a 1px border
                // ring once inset by inner's Location), so a tooltip set on
                // outer alone is swallowed by whichever of inner/label is
                // actually under the cursor (labels capture mouse - the
                // same lesson M32 already established for hover/click
                // targets elsewhere in this file) and the user never sees
                // it hovering the pill body. Click/MouseEntered/MouseLeft
                // stay on outer only - unlike tooltip lookup, those already
                // work correctly today.
                string tooltipText = null;

                bool interactive = !dimmed && spec.Source.HasValue && _resolveOverridesSync != null;
                bool ignoreInteractive = !dimmed && spec.Kind == PillKind.Ignore && _resolveOverridesSync != null;
                if (interactive)
                {
                    tooltipText = $"Switch to {spec.Text}";
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
                    tooltipText = node.IsIgnored
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
                    // W4B (2026-08-15): a cost-component leaf's "CURRENCY"
                    // badge (BuildPillSpecs' IsCostComponent short-circuit) -
                    // its cost cell is deliberately blank because the
                    // quantity itself IS the cost, in a non-coin currency
                    // (see CraftingTreeBuilder.
                    // BuildVendorCostComponentLeaves' currency-line branch) -
                    // never a "no source"/"no choice" situation like every
                    // other Locked pill below, so it gets its own tooltip
                    // before either of those checks run.
                    if (node.IsCostComponent)
                    {
                        tooltipText = "Paid in a non-coin currency - no gold value to show here";
                    }
                    // The UNKNOWN pill (node.Decision == Unknown - no
                    // feasible source at all) is a different situation from
                    // every other locked pill (exactly one feasible source,
                    // just not a choice): "Only available source" is
                    // misleading there since there IS no available source.
                    // Prefer the seeded wiki hint when one exists.
                    else if (node.Decision == CraftingDecision.Unknown)
                    {
                        tooltipText = !string.IsNullOrEmpty(node.AcquisitionHint)
                            ? node.AcquisitionHint
                            : "No known acquisition source";
                    }
                    else
                    {
                        tooltipText = "Only available source";
                    }
                }
                else if (spec.Kind == PillKind.Selected)
                {
                    // Field-test finding D: the currently-committed source
                    // pill (non-interactive - clicking it would be a no-op
                    // re-solve, see BuildPillSpecs) previously had no
                    // tooltip at all.
                    tooltipText = $"Current source: {spec.Text}";
                }
                else if (spec.Kind == PillKind.Have)
                {
                    // W4B (2026-08-15): a cost-component leaf can no longer
                    // reach this branch - BuildPillSpecs' IsCostComponent
                    // short-circuit now emits only the "OWN n"/"CURRENCY"
                    // badges (never PillKind.Have) for a component leaf, so
                    // this tooltip only ever needs the ordinary-node wording
                    // below.
                    //
                    // Maintainer's final wording pass (2026-08-06): matches
                    // the OwnedInfo pill's "Needs N - ..." vocabulary below
                    // instead of the old bare "Fully covered by your
                    // materials". For a genuinely-owned Have node, Quantity
                    // is 0 (the node's whole demand was already subtracted
                    // during reduction), so OwnedQuantityUsed alone already
                    // is the original total demand.
                    tooltipText = $"Needs {node.OwnedQuantityUsed} - all covered by your materials";
                }
                else if (spec.Kind == PillKind.OwnedInfo)
                {
                    if (node.IsCostComponent)
                    {
                        // W4B (2026-08-15): the "OWN n" badge's own tooltip -
                        // unlike the ordinary OwnedInfo case below, owning
                        // some of a cost component never reduces what still
                        // has to be handed over as part of this purchase, or
                        // this line's cost (see CraftingTreeNode.
                        // ComponentOwnedQuantity's own doc comment) - purely
                        // informational, stated explicitly so it is never
                        // mistaken for the ordinary "reduced the plan"
                        // OwnedInfo/HAVE vocabulary used everywhere else in
                        // the tree.
                        tooltipText =
                            $"You own {node.ComponentOwnedQuantity} - informational only, " +
                            "does not change the plan cost";
                    }
                    else
                    {
                        // Field-test finding A's tooltip spelled out what the
                        // pill text means in full sentences, alongside the tree
                        // row's own remaining-need "Nx" prefix (node.Quantity);
                        // the maintainer's final wording pass (2026-08-06, see
                        // DecisionPillPlanner.AppendOwnershipPills) reworded the
                        // pill itself to "HAVE {used}/{total} NEEDED" and this
                        // tooltip to match, without changing what either number
                        // means - remaining (node.Quantity) is still total minus
                        // used.
                        int totalDemand = node.OwnedQuantityUsed + node.Quantity;
                        tooltipText =
                            $"Needs {totalDemand} total - {node.OwnedQuantityUsed} covered by your materials, " +
                            $"{node.Quantity} left to acquire";
                    }
                }
                else if (spec.Kind == PillKind.AchievementBitDeduped)
                {
                    // M37, KNOWN-ISSUES #26: explains the "COUNTED
                    // ELSEWHERE" semantics - nothing here is actually
                    // owned, this exact occurrence is just already required
                    // elsewhere in the tree.
                    tooltipText = "Already counted elsewhere in the tree - this item is obtained once, not needed again here";
                }

                if (tooltipText != null)
                {
                    outer.BasicTooltipText = tooltipText;
                    inner.BasicTooltipText = tooltipText;
                    label.BasicTooltipText = tooltipText;
                }

                pillPanels.Add(outer);
                x += pillWidth + 6;
            }

            return pillPanels;
        }
    }
}
