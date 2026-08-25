using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView's "8. Tree
    // rendering (state)"/"8. Tree rendering (continued)"/"9. Decision
    // pills" regions - the Recipe Tree section renderer AND the interactive
    // override loop it drives (Best Path/Craft All/Buy All presets, the
    // per-node craft/tp/vendor decision pills, and the Ignore pill), plus
    // every field that loop owns: TreeNodeState, _treeNodeStates,
    // _treeRoots/_treeFlow (the current render pass's tree bookkeeping),
    // _nodeOverrides/_ignoredItemIds/_nodeExpansion (session-persistent
    // decision/ignore/expansion state), and _lastResult (the
    // solve context the override loop re-resolves against).
    //
    // Unlike the six section renderers
    // extracted before it, this component owns a slice of application
    // state, not just presentation - the field group above survives across
    // every local re-solve (a pill click never rebuilds it) and is reset
    // only once per genuinely new Generate. It also cannot reach several
    // things it still needs purely through ISectionRelayoutSink, because
    // those things are NOT relayout registrations: PreserveScrollAcross
    // (scroll preserve/restore/verify machinery, stays on
    // CraftingPlanView - see
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
    // it now reads ISectionRelayoutSink.RelayoutCount, added
    // specifically for that (see the interface's own doc
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
    // See docs/ARCHITECTURE.md section 5 for the state-ownership
    // rationale and the scroll/resize/wheel controller cut decision.
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

        // Publishes (or, with null, withdraws) the five tree actions to
        // whatever surface hosts their buttons - see TreeToolbarCommands.
        private readonly Action<TreeToolbarCommands> _setTreeToolbar;

        // This session's item stat block for an item id, or null. Null is
        // routine, not exceptional: a synthesized cost-component leaf is
        // not a real item at all, and a plan restored from disk has no
        // stats until something re-fetches (docs/KNOWN-ISSUES.md, "Item
        // stat tooltips"). Either way the row falls back to the tooltip it
        // had before this feature existed.
        private readonly Func<int, ItemStatBlock> _getItemStatBlock;

        // Registers one row control under a stable scroll-anchor key, so a
        // re-solve can put the row the user was looking at back under
        // their cursor instead of merely restoring the scroll offset (see
        // Services/ScrollAnchorMath). Optional - a null one simply leaves
        // the view anchoring at section granularity.
        private readonly Action<int, Control> _registerRowScrollAnchor;

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
            Func<string, PlanSectionType, int, bool, Func<bool>, (Panel HeaderPanel, Label ArrowLabel, FlowPanel ContentFlow)> createSectionHeader,
            Action<TreeToolbarCommands> setTreeToolbar,
            Func<int, ItemStatBlock> getItemStatBlock = null,
            Action<int, Control> registerRowScrollAnchor = null)
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
            _setTreeToolbar = setTreeToolbar ?? throw new ArgumentNullException(nameof(setTreeToolbar));

            // Optional and NOT null-guarded, matching resolveOverridesSync
            // above: null simply means "no stat tooltips this session",
            // which every reader below already treats as the fallback.
            _getItemStatBlock = getItemStatBlock;
            _registerRowScrollAnchor = registerRowScrollAnchor;
        }

        // Per-node user decision overrides (keyed by solver NodeId) and
        // explicit tree expansion state; both survive local re-solves and
        // reset on a fresh Generate.
        private readonly Dictionary<int, AcquisitionSource> _nodeOverrides =
            new Dictionary<int, AcquisitionSource>();

        // Item ids manually marked "Ignore" this session (gw2e
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

            // PanelWidth removed - a captured build-time width
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

        /// <summary>
        /// Everything an in-place refresh of one already-built tree row
        /// needs to reach - see <see cref="TryRefreshInPlace"/>. Held per
        /// BUILT row, in build order, which is exactly the pre-order the
        /// refresh walk re-derives.
        /// <para>
        /// The row's own relayout and re-ellipsis closures read their
        /// mutable state (the pill list, the cost cell, the qty width)
        /// through this handle rather than capturing it, so a refresh that
        /// replaces a row's pills does not leave a closure repositioning
        /// controls that no longer exist.
        /// </para>
        /// </summary>
        private sealed class TreeRowHandle
        {
            internal CraftingTreeNode Node;
            internal int Depth;
            internal bool Dimmed;
            internal string CaptionText;
            internal string FullName;

            internal Panel RowPanel;
            internal Label QtyLabel;
            internal Label NameLabel;
            internal Panel IconFrame;
            internal Panel IconScrim;

            /// <summary>
            /// The SAME instance for the row's whole life. The row's click
            /// guard closes over it to ask whether a pill is under the
            /// cursor, so a refresh must refill it, never replace it.
            /// </summary>
            internal readonly List<Panel> Pills = new List<Panel>();

            internal CoinCurrencyRenderer.ValueCellHandle CostCell;
            internal bool RowDrawsCurrency;

            internal int NameX;
            internal int QtyWidth;
            internal int CostColumnWidth;
            internal TreeCostColumnMath.CostColumnWidths ColumnWidths;

            /// <summary>Null for a row with no children.</summary>
            internal TreeNodeState State;
        }

        // Every built row of the current render pass, keyed by the solver
        // NodeId its row draws. A MAP rather than the build-order list it
        // started as: rows are appended in pre-order by the initial build,
        // but a later expand APPENDS its children at the end, so build
        // order stops being tree order the first time anyone expands
        // anything - and a refresh that walked the list positionally would
        // have quietly stopped matching from then on.
        private readonly Dictionary<int, TreeRowHandle> _treeRowsByNodeId =
            new Dictionary<int, TreeRowHandle>();

        // Set when two rows of one render claim the same NodeId, which
        // would make the map above ambiguous. Never observed - the solver
        // numbers its nodes uniquely - so this is a guard that declines to
        // refresh rather than a case with a handling strategy.
        private bool _treeRowIdsAmbiguous;

        // Node count of the pre-scan that titled this render's section
        // header ("Recipe Tree (N)"). A refresh that would change it has to
        // decline: the header is preserved across an in-place refresh, and
        // a title that no longer counts the tree under it is worse than a
        // rebuild.
        private int _scannedNodeCount;

        // Root nodes + top-level content FlowPanel for the current render's
        // Recipe Tree section (null when the plan has no tree). Held so
        // RefreshTreeContainerHeights - called from the tree row toggle
        // handler deep inside RenderTreeNode's recursion, as well as from
        // CreateTreeSection itself - can recompute treeFlow's own explicit
        // Height without threading both through every recursive call.
        // A single-item plan still populates this with exactly one root,
        // so every consumer
        // below is unchanged in that case (see MultiRootTreeFlowHeight's
        // own doc comment for the "N==1 is byte-identical" guarantee).
        private List<CraftingTreeNode> _treeRoots;
        private FlowPanel _treeFlow;

        // Per-render-pass widest value per coin denomination (plus the
        // widest whole currency run) across the ENTIRE tree, so every
        // row's cost cell lands in the same sub-columns and the coin icons
        // form straight vertical rules - see Services/TreeCostColumnMath,
        // including why this covers every node rather than only the rows
        // currently expanded. Scanned once in CreateTreeSection and read
        // by every RenderTreeNode call of that pass, including the ones a
        // later expand click builds lazily.
        private TreeCostColumnMath.CostColumnWidths _costColumnWidths =
            TreeCostColumnMath.CostColumnWidths.Empty;

        /// <summary>
        /// Per-render-pass reset, called from
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
            _treeRowsByNodeId.Clear();
            _treeRowIdsAmbiguous = false;
            _scannedNodeCount = 0;
            _treeRoots = null;
            _treeFlow = null;
            _costColumnWidths = TreeCostColumnMath.CostColumnWidths.Empty;

            // Withdrawn with the render pass that published them: the tree
            // actions operate on the controls this reset is about to
            // discard, and the next plan may have no tree at all.
            _setTreeToolbar(null);
        }

        /// <summary>
        /// Fresh-generation reset, called from
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
        /// Re-seeds the
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
        /// set before restarting.
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
            _treeRowsByNodeId.Clear();
            _treeRowIdsAmbiguous = false;

            // The five action buttons this header used to carry now live in
            // CraftingPlanView's non-scrolling top strip - see
            // TreeToolbarCommands. Nothing interactive is left in the header,
            // so the suppressToggle guard (and the press-time hover flag it
            // read) went with them.
            _treeRoots = treeRoots as List<CraftingTreeNode> ?? new List<CraftingTreeNode>(treeRoots);

            // Column pre-scan: ONE walk of the whole tree per render
            // pass, before any row is built, so every row (including the
            // ones an expand click builds later) anchors to the same
            // sub-columns. Never re-run per row draw or per resize tick -
            // the result is data-derived, not panelWidth-derived.
            //
            // Hoisted above the header because the header's title now
            // carries the tree's node count, which comes out of this same
            // walk (audit batch J, L2). It reads nothing the header
            // produces, so the move is ordering only.
            var scan = ScanTreeColumns(_treeRoots);
            _costColumnWidths = scan.CostWidths;
            _scannedNodeCount = scan.NodeCount;

            // Parenthesised count, like every other countable section
            // ("Used Materials (12)", "Shopping List (7)"). The number is
            // every node at every depth - the rows Expand All reveals -
            // not the currently visible ones, which would change under the
            // reader on every caret click. A tree with no roots renders no
            // section body at all, so it keeps the bare title rather than
            // advertising "(0)".
            string title = scan.NodeCount > 0
                ? $"Recipe Tree ({scan.NodeCount})"
                : "Recipe Tree";
            var header = _createSectionHeader(
                title, PlanSectionType.RecipeTree, panelWidth, true, null);
            var treeFlow = header.ContentFlow;
            _treeFlow = treeFlow;

            // Column headers over the two columns a tree row's right-hand
            // side actually has. Both track the panel width now (the
            // pill+cost block's x is width-derived), hence
            // middleXForWidth/rightXForWidth rather than build-time x's.
            // Counted by
            // PlanContentHeightMath.MultiRootTreeFlowHeight, which every
            // treeFlow height assignment goes through.
            // Guarded on the same "is there a tree at all" condition
            // MultiRootTreeFlowHeight counts the header under: a header
            // drawn over zero roots would be a row the section's own
            // height math reserves nothing for.
            if (_treeRoots.Count > 0)
            {
                int headerCostColumnWidth = EffectiveCostColumnWidth();
                CTableHeaderRenderer.CreateCTableHeaderRow(
                    treeFlow, panelWidth, "Item", TreeCaretColWidth + TreeIconFrameSize + TreeNameGap, "Cost", _sink,
                    middleLabel: "Source",
                    middleXForWidth: w => PlanRelayoutMath.ComputeTreeColumnEdges(
                        w, 0, 0, TreePillColumnWidth, headerCostColumnWidth, TreeRightMargin).PillColX,
                    rightXForWidth: w => PlanRelayoutMath.ComputeTreeColumnEdges(
                        w, 0, 0, TreePillColumnWidth, headerCostColumnWidth, TreeRightMargin).CostRightEdge);
            }

#if DEBUG
            int relayoutCountBeforeTree = _sink.RelayoutCount;
#endif
            // A thin gap
            // between consecutive roots so N stacked full item trees read
            // as N distinct blocks (PlanContentHeightMath.
            // MultiRootDividerHeight) - never inserted for a single root,
            // which keeps that case's rendered rows byte-identical to the
            // single-item render.
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
            // Every RenderTreeNode call registers its
            // own relayout closure (see the field comment on
            // _relayoutActions) - a single root node still yields at least
            // one. Zero growth here would mean that mechanism itself
            // silently broke.
            if (_sink.RelayoutCount == relayoutCountBeforeTree)
            {
                Logger.Warn("M33 C2b: Recipe Tree root rendered but registered no relayout closures - it will not track live window resize.");
            }
#endif

            // Every container this initial build
            // populated (treeFlow plus every childFlow created for a
            // default-expanded node) still reads its construction-time
            // Size.Y of 0 at this point - one synchronous pass now finalizes
            // every one of them from the same PlanContentHeightMath
            // arithmetic the rows above were just laid out with, before
            // this method returns to RenderPlan/PreserveScrollAcross.
            RefreshTreeContainerHeights();

            // Published last: every action below reads state this method
            // just finished building.
            _setTreeToolbar(new TreeToolbarCommands
            {
                BestPath = ApplyBestPathPreset,
                CraftAll = () => ApplyPreset(AcquisitionSource.Craft),
                BuyAll = () => ApplyPreset(AcquisitionSource.BuyFromTp),
                ExpandAll = ExpandAll,
                CollapseAll = CollapseAll,
                ClearOverrides = ClearOverrides,
                ClearIgnored = ClearIgnored,
                GetOverrideCount = () => _nodeOverrides.Count,
                GetIgnoredCount = () => _ignoredItemIds.Count,
                CanReSolve = () => _lastResult?.SolveContext != null,
                CraftAllWouldChange = () => PresetWouldChange(AcquisitionSource.Craft),
                BuyAllWouldChange = () => PresetWouldChange(AcquisitionSource.BuyFromTp)
            });
        }

        // Decision preset: clear every manual override and re-solve for the
        // solver's own cheapest plan. The Count == 0 early return is the
        // belt to the view's braces - the view gates this behind its own
        // would-change predicate now, but the command is still directly
        // invokable with no dialog wiring at all.
        private void ApplyBestPathPreset()
        {
            if (_nodeOverrides.Count == 0) return;
            _nodeOverrides.Clear();
            ApplyOverridesAndResolve(isBestPathPreset: true);
        }

        /// <summary>
        /// The Overrides chip's clear action: back to the solver's own
        /// choices. MEASURED: identical work to
        /// <see cref="ApplyBestPathPreset"/> - clear the same dictionary,
        /// re-solve - and it differs only in writing the ordinary
        /// "Plan updated" event rather than claiming "Best path restored",
        /// which is a preset's label and not a description of clearing.
        /// See docs/KNOWN-ISSUES.md: the two buttons being one action is a
        /// finding for the maintainer, not something this seam invents a
        /// difference to hide.
        /// </summary>
        private void ClearOverrides()
        {
            if (_nodeOverrides.Count == 0) return;
            _nodeOverrides.Clear();
            ApplyOverridesAndResolve();
        }

        /// <summary>
        /// The Ignored chip's clear action. Runs the SAME re-solve path any
        /// ignore-pill click runs - no bespoke status string, because
        /// nothing bespoke happened.
        /// </summary>
        private void ClearIgnored()
        {
            if (_ignoredItemIds.Count == 0) return;
            _ignoredItemIds.Clear();
            ApplyOverridesAndResolve();
        }

        /// <summary>
        /// Whether applying a preset would actually change the override
        /// map - the same key set with the same values re-solves to the
        /// identical plan (ignore marks are untouched by a preset), so the
        /// click is a no-op the view reports instead of performing.
        /// Answered at click time: it walks the solver tree to build the
        /// preset, which is bounded but not free.
        /// <para>
        /// NULL, not false, with no solve context. A persisted plan whose
        /// Result deserialises without one restores a renderable tree
        /// (PlanStructuralValidator accepts a null SolveContext) and a
        /// visible toolbar, and every re-solve on it is unavailable rather
        /// than unnecessary. See <see cref="TreeToolbarCommands"/>.
        /// </para>
        /// </summary>
        private bool? PresetWouldChange(AcquisitionSource source)
        {
            if (_lastResult?.SolveContext == null) return null;

            var preset = CraftingPlanPipeline.BuildPresetOverrides(_lastResult.SolveContext, source);
            if (preset.Count != _nodeOverrides.Count) return true;

            foreach (var kvp in preset)
            {
                if (!_nodeOverrides.TryGetValue(kvp.Key, out var current) || current != kvp.Value)
                {
                    return true;
                }
            }
            return false;
        }

        private void ExpandAll()
        {
            _preserveScrollAcross(() =>
            {
                // Building children appends to _treeNodeStates; index loop
                // deliberately walks the growing list.
                for (int i = 0; i < _treeNodeStates.Count; i++)
                {
                    var s = _treeNodeStates[i];
                    if (!s.ChildrenBuilt)
                    {
                        int captionSplitIndex = ReceiptCaptionHelper.ComputeCaptionSplitIndex(s.Node);
                        for (int childIndex = 0; childIndex < s.Node.Children.Count; childIndex++)
                        {
                            string childCaption = ReceiptCaptionHelper.CaptionForChildIndex(captionSplitIndex, childIndex);
                            RenderTreeNode(
                                s.Node.Children[childIndex], s.ChildContainer, _getCurrentPanelWidth(),
                                s.Depth + 1, s.ChildDimmed, childCaption);
                        }
                        s.ChildrenBuilt = true;
                    }
                    s.IsExpanded = true;
                    _nodeExpansion[s.Node.NodeId] = true;
                    s.ChildContainer.Visible = true;
                    s.ArrowLabel.Text = "v";
                }
                RefreshTreeContainerHeights();
            });
            HoverChainResync.AfterRebuild();
        }

        private void CollapseAll()
        {
            _preserveScrollAcross(() =>
            {
                foreach (var s in _treeNodeStates)
                {
                    s.IsExpanded = false;
                    _nodeExpansion[s.Node.NodeId] = false;
                    s.ChildContainer.Visible = false;
                    s.ArrowLabel.Text = ">";
                }
                RefreshTreeContainerHeights();
            });
            HoverChainResync.AfterRebuild();
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

        // IsBestPathPreset must come from which
        // control fired this call, not be inferred from the resulting
        // _nodeOverrides count - see StatusText.ForOverrideResolve for why.
        // Moved verbatim from CraftingPlanView.ApplyOverridesAndResolve.
        // Edits: _lastDebugLog = ... -> _setLastDebugLog(...); _currentPlan
        // = vm -> _setCurrentPlan(vm); PreserveScrollAcross(() =>
        // RenderPlan(vm)) -> _preserveScrollAcross(() => _renderPlan(vm));
        // SetStatus(...) -> _setStatus(...).
        private void ApplyOverridesAndResolve(bool isBestPathPreset = false)
        {
            // Edit since the move: this used to return silently on a
            // missing solve context, which made EVERY local change - a pill
            // click, a preset, either chip's clear - a dead click on a plan
            // restored without one. The click is still refused; it now says
            // so. Unwired _resolveOverridesSync stays silent: that is a
            // build-time wiring fault, not a state the user is in.
            if (_lastResult?.SolveContext == null)
            {
                _setStatus(StatusText.ReSolveUnavailable);
                return;
            }

            if (_resolveOverridesSync == null)
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
                // The click that got us here came from a cursor that has
                // not moved, and the render just replaced controls under
                // it - see HoverChainResync.
                HoverChainResync.AfterRebuild();
                _setStatus(StatusText.ForOverrideResolve(isBestPathPreset));
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Override re-solve failed");
                _setStatus(StatusText.ForUpdateFailure(ex.Message));
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

        // Defined in PlanRelayoutMath, which owns the column arithmetic and
        // the measurement behind the width.
        private const int TreePillColumnWidth = PlanRelayoutMath.TreePillColumnWidth;
        private const int TreeCostColumnWidth = 150;
        private const int TreeRightMargin = 8;

        // Left-indent rule down a dimmed subtree: 2px wide (1px is not
        // guaranteed a physical scanline under Blish's non-integer UI
        // scale - see LabelHelpers.CreateRowDivider), drawn at every dimmed
        // row's own indent channel and spanning the full row height, so
        // consecutive rows at the same depth join into one continuous line
        // and the branch reads as a single inactive block instead of a
        // stack of independently-styled rows. Sits inside the existing
        // TreeRowHeight, so no height math changes.
        // Decision-pill chrome. TightPillPadding is the first thing tried
        // when a row's pills do not fit: 3px of side padding instead of 6
        // still reads as a pill, and squeezing beats hiding a real option
        // (PlanRelayoutMath.ComputePillFit).
        // 24, not 20. A pill's label sits at y=2 inside an inset fill panel
        // of PillHeight - 2, and the Font14 label's lowest ink is y=21; the
        // old 18px interior clipped it. 24 gives the same 1px of interior
        // slack the Font12 label had.
        private const int PillHeight = 24;
        private const int PillGap = 6;
        private const int PillPadding = 12;
        private const int TightPillPadding = 6;

        private const int TreeDimmedRuleWidth = 2;
        private const int TreeDimmedRuleOffset = 8;
        private static readonly Color TreeDimmedRuleColor = Color.White * 0.18f;

        // What a dead click on a dimmed pill means, and the one action
        // that makes it live again. Every dimmed row is somewhere under a
        // node the solver decided to buy, so switching that node to CRAFT
        // is always the answer, however deep the row sits.
        private const string DimmedPillTooltip =
            "Under a bought item - switch the parent to CRAFT to change this";

        /// <summary>
        /// Width the cost column actually needs this render: its fixed
        /// floor, or the pre-scanned sub-columns' real total when a tree
        /// full of multi-gold (or currency-priced) values needs more. The
        /// column's RIGHT edge never moves, so widening it only pushes the
        /// decision pills and the name budget left - which is the point:
        /// before, a wide cost run silently overprinted the pills.
        /// </summary>
        private int EffectiveCostColumnWidth()
        {
            int scanned = TreeCostColumnMath.TotalWidth(_costColumnWidths);
            return scanned > TreeCostColumnWidth ? scanned : TreeCostColumnWidth;
        }

        /// <summary>
        /// Blish-bound half of the column pre-scan: the pure walk lives in
        /// TreeCostColumnMath.ScanColumns, this supplies the measurements
        /// it cannot make itself. Strings are memoised because a tree
        /// repeats them heavily (number strings like "00"/"42", and one
        /// item name recurs across the tree), so MeasureString runs once
        /// per DISTINCT string rather than once per node; the currency
        /// callback only fires for the handful of vendor nodes that draw a
        /// currency run at all.
        /// </summary>
        private TreeCostColumnMath.TreeColumnScan ScanTreeColumns(IReadOnlyList<CraftingTreeNode> roots)
        {
            var font = UiFonts.Body;
            var measured = new Dictionary<string, int>();
            var metadata = _getCurrentPlan()?.CurrencyMetadata;

            Func<string, int> measure = text =>
            {
                if (!measured.TryGetValue(text, out int width))
                {
                    width = (int)Math.Ceiling(font.MeasureString(text).Width);
                    measured[text] = width;
                }
                return width;
            };

            return TreeCostColumnMath.ScanColumns(
                roots,
                measure,
                node => CoinCurrencyRenderer.TotalCurrencySegmentsWidth(
                    CoinCurrencyRenderer.BuildCurrencySegments(
                        CurrencyDisplayResolver.ResolveAmounts(node.VendorCurrencyCosts, metadata), font)));
        }

        /// <summary>
        /// Recomputes and re-assigns the explicit
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
        // UI-bundle milestone: captionText is the sanctioned tooltip
        // fallback for Feature C (receipt/what-if captions) - see
        // ReceiptCaptionHelper's own doc comment for why a real extra ROW
        // is not used (frozen PlanContentHeightMath tree-height math counts
        // exactly node.Children.Count rows per level). null for every node
        // except the first child of each group under a node whose Children
        // stack cost-component leaves + a reference branch - see the three
        // call sites that compute it via ReceiptCaptionHelper.
        private void RenderTreeNode(
            CraftingTreeNode node, FlowPanel parent, int panelWidth, int depth, bool dimmed, string captionText = null)
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

            // Left-indent rule (see TreeDimmedRuleColor). Drawn before
            // every other child so nothing else in the row paints under it,
            // and never on a live row.
            if (dimmed)
            {
                int ruleX = indent - TreeDimmedRuleOffset;
                if (ruleX < 0) ruleX = 0;
                new Panel()
                {
                    Size = new Point(TreeDimmedRuleWidth, TreeRowHeight),
                    Location = new Point(ruleX, 0),
                    BackgroundColor = TreeDimmedRuleColor,
                    Parent = rowPanel
                };
            }

            Label arrowLabel = null;
            if (hasChildren)
            {
                Color arrowColor = dimmed ? Color.White * 0.35f : Color.White;
                arrowLabel = new Label()
                {
                    Font = UiFonts.Body,
                    // ASCII, matching the section headers - the U+25BC/U+25B6
                    // triangles do not render in Blish's font.
                    Text = isExpanded ? "v" : ">",
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
            var iconFrame = IconControls.CreateItemIcon(
                rowPanel, node.IconUrl, frameColor, iconX, 3, TreeIconSize, TreeIconBorder);
            Panel iconScrim = null;
            if (dimmed)
            {
                iconScrim = new Panel()
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
            // PillColX/costRightEdge/nameMaxWidth now come from
            // PlanRelayoutMath.ComputeTreeColumnEdges - the SAME pure
            // function the relayout/re-ellipsis closures below call, so the
            // build and every later resize tick can never disagree about
            // these columns.
            int nameX = indent + TreeCaretColWidth + TreeIconFrameSize + TreeNameGap;

            var nameFont = UiFonts.Body;
            string qtyPrefix = node.Quantity > 0 ? $"{node.Quantity}x " : "";
            int qtyWidth = qtyPrefix.Length > 0
                ? (int)System.Math.Ceiling(nameFont.MeasureString(qtyPrefix).Width)
                : 0;

            // Snapshot, not a live field read: every closure below outlives
            // this call, and the next render pass resets _costColumnWidths
            // before rebuilding. Capturing the value keeps a row's build-time
            // columns and its own relayout arithmetic identical by
            // construction.
            var columnWidths = _costColumnWidths;
            int costColumnWidth = EffectiveCostColumnWidth();
            var edges = PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, nameX, qtyWidth, TreePillColumnWidth, costColumnWidth, TreeRightMargin);
            int pillColX = edges.PillColX;

            string fullName = node.Name ?? "";

            // Registered before the row's controls exist so every closure
            // below reads its mutable state (pills, cost cell, qty width)
            // from ONE place an in-place refresh can rewrite.
            var handle = new TreeRowHandle
            {
                Node = node,
                Depth = depth,
                Dimmed = dimmed,
                CaptionText = captionText,
                FullName = fullName,
                RowPanel = rowPanel,
                IconFrame = iconFrame,
                IconScrim = iconScrim,
                NameX = nameX,
                QtyWidth = qtyWidth,
                CostColumnWidth = costColumnWidth,
                ColumnWidths = columnWidths
            };
            if (_treeRowsByNodeId.ContainsKey(node.NodeId))
            {
                _treeRowIdsAmbiguous = true;
            }
            else
            {
                _treeRowsByNodeId[node.NodeId] = handle;
                // Same NodeId identity, same ambiguity guard: a duplicated
                // id cannot say which row the user was on either.
                _registerRowScrollAnchor?.Invoke(node.NodeId, rowPanel);
            }
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

            Label qtyLabel = null;
            if (qtyPrefix.Length > 0)
            {
                // Same baseline as the name label below it - both boxes get
                // the descender clearance so the two halves of "12x <name>"
                // can never sit on different lines.
                qtyLabel = LabelHelpers.WithDescenderClearance(
                    new Label()
                    {
                        Text = qtyPrefix,
                        Font = nameFont,
                        TextColor = qtyColor,
                        AutoSizeWidth = true,
                        AutoSizeHeight = true,
                        Location = new Point(nameX, 12),
                        Parent = rowPanel
                    });
            }
            var nameLabel = LabelHelpers.WithDescenderClearance(
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
                });
            handle.QtyLabel = qtyLabel;
            handle.NameLabel = nameLabel;

            // ExtraTooltipLines never depends on panelWidth (unit
            // price / acquisition hint text is fixed), so it is computed
            // once and reused verbatim by the settle re-ellipsis pass -
            // only the "is the name actually truncated" line needs to be
            // reconsidered when nameMaxWidth changes.
            //
            // tree-tooltip-composer milestone: the actual line-building
            // logic (unit price, AUDIT ROW 20/38 price-side-fallback
            // caveat, acquisition hint, caption, wiki-link line) moved
            // verbatim to the pure, unit-tested
            // Services/TreeRowTooltipComposer.cs - see that class's own doc
            // comment and docs/ARCHITECTURE.md section 5's STANDING RULE.
            // Only the Blish-bound right-click event wiring below stays
            // here.
            var currentPlan = _getCurrentPlan();
            var extraTooltipContent = TreeRowTooltipComposer.BuildExtraTooltipContent(node, captionText, currentPlan);

            // Composed at HOVER time, not here: a plan restored from disk
            // fills its stat cache in the background (Q13), so a snapshot
            // taken at render time could never show what lands after it.
            // The lookup itself is a session cache read - see
            // ItemMetadataService.GetCachedStatBlock, which never fetches.
            Func<TooltipContent> getStatContent =
                () => TreeRowTooltipComposer.BuildStatTooltipContent(node, _getItemStatBlock);

            // This module's only external-URL launch - a context action
            // (right-click), not a visible icon. Every tree
            // row gets this, item leaf or internal node alike - a wiki page
            // that does not exist for an internal-only concept (e.g. a
            // synthesized cost-component "currency" name) just 404s rather
            // than crashing anything; WikiLinkBuilder.HasWikiPage/
            // BuildItemPageUrl additionally suppress the affordance
            // entirely for the known placeholder names (see
            // WikiLinkBuilder's SentinelNames), which never resolve to a
            // real page at all.
            //
            // Fix-pass (render-path allocation): HasWikiPage is a cheap
            // non-whitespace + not-a-placeholder-name check - the actual
            // URL (Trim + Replace + Uri.EscapeDataString, a closure, and a
            // delegate) is built lazily inside the press/release handlers
            // below instead of eagerly for every tree row on every build
            // and every lazy expand, since most rows are never
            // right-clicked at all.
            //
            // Fix-pass (right-click-as-camera-drag): GW2's own right-drag
            // is the camera-rotate gesture, and firing on button-DOWN alone
            // (the previous behavior) meant a drag begun over this row -
            // input Blish otherwise swallows here today - opened the
            // browser and yanked focus out of a fullscreen game the
            // instant the button went down, with no way to abort. Firing
            // on RightMouseButtonReleased alone is NOT a fix: Blish routes
            // the release event to whichever row is under the cursor at
            // release time, so a drag that started on a DIFFERENT row
            // would open THIS row's page instead. Pairing press+release on
            // this SAME rowPanel closes that: press arms a per-row flag,
            // and only this row's own Released handler (which only fires
            // when the release also lands on this row) can consume it.
            // MouseLeft additionally disarms the flag the moment the
            // cursor leaves this row after a press, so a drag that starts
            // here, wanders off, and is released back over this row later
            // (from an unrelated gesture) cannot replay a stale arm.
            //
            // Unlike toggleHandler below, this handler does NOT exclude
            // clicks landing on a pill (pillPanels is not yet in scope
            // here). Intentional and harmless: decision pills carry no
            // right-click meaning, so a right-click that lands on one
            // still falls through to this row's wiki-link handler rather
            // than doing nothing.
            if (WikiLinkBuilder.HasWikiPage(node.Name))
            {
                string nodeName = node.Name;
                bool wikiLinkArmed = false;
                rowPanel.RightMouseButtonPressed += (_, __) => wikiLinkArmed = true;
                rowPanel.MouseLeft += (_, __) => wikiLinkArmed = false;
                rowPanel.RightMouseButtonReleased += (_, __) =>
                {
                    if (wikiLinkArmed)
                    {
                        wikiLinkArmed = false;
                        WikiLinkLauncher.Open(WikiLinkBuilder.BuildItemPageUrl(nodeName));
                    }
                };
            }

            UpdateTreeRowTooltip(
                rowPanel, nameLabel, qtyLabel, iconFrame, iconScrim,
                fullName, getStatContent, extraTooltipContent);

            // Decision pill column: one pill per feasible source (direct
            // selection - click sets the override and re-solves), or a
            // single locked/HAVE/CURRENCY pill when there is no choice.
            var pillPanels = handle.Pills;
            RenderDecisionPills(rowPanel, node, pillColX, 10, dimmed, pillPanels);

            // Cost column: four right-aligned sub-columns (gold, silver,
            // copper, then any non-coin currency), each sized by this
            // render's pre-scan of the whole tree, so the coin ICONS land
            // on the same x on every row drawing the same bands - see
            // Services/TreeCostColumnMath, whose ComputeRowEdges also
            // covers why a row with no currency of its own ends on the
            // column's right edge rather than short of it.
            // Only rendered
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
            // A node whose children are the new synthesized cost-
            // component leaves (see CraftingTreeBuilder.
            // BuildVendorCostComponentLeaves - every child of such a node is
            // a component leaf, never mixed with a reference branch or a
            // real craft child) shows ONLY the compact gold total here -
            // the breakdown lives one expand-click away as real child
            // rows, instead of one very long segmented row colliding with
            // the layout.
            RenderCostCell(handle, node, edges.CostRightEdge, dimmed);

            // Child container. Children of a non-Craft decision are this
            // module's own informational reference branch (audit row 56
            // PART B #3: corrected provenance - gw2e has no equivalent
            // ".not-crafted" concept; this dimmed "what it would cost to
            // craft instead" branch is a module original) - dimmed, and the
            // flag does not stack on already-dimmed branches.
            FlowPanel childFlow = null;
            if (hasChildren)
            {
                bool childDimmed = dimmed || node.Decision != CraftingDecision.Craft;

                // Standard (explicit) height, same
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
                handle.State = state;
                if (isExpanded)
                {
                    // UI-bundle milestone, Feature C: caption split computed
                    // once per node, reused for every child index - see
                    // ReceiptCaptionHelper's own doc comment.
                    int captionSplitIndex = ReceiptCaptionHelper.ComputeCaptionSplitIndex(node);
                    for (int childIndex = 0; childIndex < node.Children.Count; childIndex++)
                    {
                        string childCaption = ReceiptCaptionHelper.CaptionForChildIndex(captionSplitIndex, childIndex);
                        RenderTreeNode(node.Children[childIndex], childFlow, panelWidth, depth + 1, childDimmed, childCaption);
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
                    if (AnyPillHovered(pillPanels)) return;

                    _preserveScrollAcross(() =>
                    {
                        if (!state.ChildrenBuilt)
                        {
                            // Read the LIVE width rather than the
                            // (possibly long-stale, since resize no longer
                            // triggers a rebuild) width this node itself was
                            // built at - see GetCurrentPanelWidth.
                            int currentWidth = _getCurrentPanelWidth();
                            int captionSplitIndex = ReceiptCaptionHelper.ComputeCaptionSplitIndex(state.Node);
                            for (int childIndex = 0; childIndex < state.Node.Children.Count; childIndex++)
                            {
                                string childCaption = ReceiptCaptionHelper.CaptionForChildIndex(captionSplitIndex, childIndex);
                                RenderTreeNode(
                                    state.Node.Children[childIndex], state.ChildContainer, currentWidth,
                                    state.Depth + 1, state.ChildDimmed, childCaption);
                            }
                            state.ChildrenBuilt = true;
                        }
                        state.IsExpanded = !state.IsExpanded;
                        _nodeExpansion[state.Node.NodeId] = state.IsExpanded;
                        state.ChildContainer.Visible = state.IsExpanded;
                        state.ArrowLabel.Text = state.IsExpanded ? "v" : ">";
                        RefreshTreeContainerHeights();
                    });
                    // A caret click builds or hides the rows directly
                    // under the cursor - see HoverChainResync.
                    HoverChainResync.AfterRebuild();
                };
                // Same pill guard as toggleHandler, for the same reason: a
                // press on a pill reaches this row panel too, and the row
                // must not answer a click it is about to ignore.
                PressFeedback.Wire(rowPanel, () => AnyPillHovered(pillPanels));
                rowPanel.Click += toggleHandler;
            }

            // Pills/cost cell reposition every drag tick (no
            // MeasureString - pill widths are already-known control Width,
            // CoinCurrencyRenderer.RepositionValueCellRightAligned uses only cached segment text
            // widths); childFlow's width tracks panelWidth with its Height
            // preserved exactly (never perturbs scroll - every
            // row/container height is explicit). The name label is
            // untouched here; it only re-ellipsizes at settle below.
            _sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, TreeRowHeight);
                var e = RowEdges(handle, w);

                if (handle.Pills.Count > 0)
                {
                    int x = e.PillColX;
                    foreach (var pill in handle.Pills)
                    {
                        pill.Location = new Point(x, 10);
                        x += pill.Width + PillGap;
                    }
                }
                if (handle.CostCell != null)
                {
                    CoinCurrencyRenderer.RepositionValueCellInSubColumns(
                        handle.CostCell,
                        TreeCostColumnMath.ComputeRowEdges(
                            e.CostRightEdge, handle.ColumnWidths, handle.RowDrawsCurrency),
                        12);
                }
                if (childFlow != null)
                {
                    childFlow.Size = new Point(w, childFlow.Height);
                }
            });
            _sink.AddReellipsis(w =>
            {
                string newDisplayName = LabelHelpers.EllipsizeToWidth(
                    nameFont, handle.FullName, RowEdges(handle, w).NameMaxWidth);
                // No tooltip re-stamp: the deferred builder reads the
                // label's CURRENT text when the box is drawn.
                if (handle.NameLabel.Text != newDisplayName)
                {
                    handle.NameLabel.Text = newDisplayName;
                }
            });
        }

        /// <summary>
        /// One row's column grid at a given panel width, read entirely off
        /// its handle - the single place build, relayout, re-ellipsis and
        /// refresh all derive it, so a refresh that changes a row's qty
        /// prefix moves every one of them together.
        /// </summary>
        private static PlanRelayoutMath.TreeColumnEdges RowEdges(TreeRowHandle handle, int panelWidth)
        {
            return PlanRelayoutMath.ComputeTreeColumnEdges(
                panelWidth, handle.NameX, handle.QtyWidth,
                TreePillColumnWidth, handle.CostColumnWidth, TreeRightMargin);
        }

        /// <summary>
        /// Builds (or rebuilds) the row's cost cell into its handle. Only
        /// a node with a real committed decision AND a cost figure gets one
        /// - HAVE/CURRENCY/UNKNOWN nodes carry no SubtreeCost and keep the
        /// column blank, their own pill already saying "no price".
        /// </summary>
        private void RenderCostCell(
            TreeRowHandle handle, CraftingTreeNode node, int costRightEdge, bool dimmed)
        {
            handle.CostCell = null;
            handle.RowDrawsCurrency = false;
            if (!node.SubtreeCost.HasValue) return;

            // TreeCostColumnMath.ShowsCurrencySegments, not a
            // hand-repeated cost-component check: the pre-scan reserves
            // the currency sub-column from that same predicate, so a
            // second copy here could reserve for rows that never draw
            // and vice versa.
            var currencyAmounts = TreeCostColumnMath.ShowsCurrencySegments(node)
                ? CurrencyDisplayResolver.ResolveAmounts(
                    node.VendorCurrencyCosts, _getCurrentPlan()?.CurrencyMetadata)
                : null;
            handle.RowDrawsCurrency = currencyAmounts != null && currencyAmounts.Count > 0;
            handle.CostCell = CoinCurrencyRenderer.RenderValueCellInSubColumns(
                handle.RowPanel, node.SubtreeCost.Value, currencyAmounts,
                TreeCostColumnMath.ComputeRowEdges(
                    costRightEdge, handle.ColumnWidths, handle.RowDrawsCurrency),
                12, UiFonts.Body, dimmed ? 0.35f : 1f);
        }

        /// <summary>
        /// Updates the already-built tree to a fresh solve WITHOUT
        /// disposing a single row, returning false when it cannot - in
        /// which case the caller renders the plan from scratch as before.
        ///
        /// <para>
        /// WHY (measured, decompiled Blish HUD 1.3.0). A decision pill's
        /// click re-solves and, until now, rebuilt every control in the
        /// plan. Two facts turn that into the reported "rapid IGNORE
        /// toggling drops clicks":
        /// <c>MouseHandler</c> holds exactly ONE pending mouse event
        /// (<c>_mouseEvent</c>, written by the hook thread, consumed once
        /// per <c>Update</c>), and <c>Control.OnLeftMouseButtonReleased</c>
        /// raises Click only when that same control INSTANCE was primed by
        /// its own press. A frame long enough to contain both halves of the
        /// next click therefore loses the press, and the release lands on a
        /// control that was never primed. Shortening the frame is the fix -
        /// and the whole of it: <see cref="RepaintRow"/> still rebuilds a
        /// matched row's pills, so no pill INSTANCE survives a re-solve and
        /// nothing here removes the priming hazard outright. See
        /// <see cref="HoverChainResync"/>, which states the same mechanism.
        /// </para>
        ///
        /// <para>
        /// The gate is deliberately strict, and every rejection is a
        /// correct full rebuild rather than a wrong cheap one: the new
        /// tree must present the SAME built rows, in the same order, at the
        /// same depth and dim state, each still passing
        /// <see cref="TreeRowIdentity"/> against the node its row was built
        /// from; the cost sub-column widths and the header's node count
        /// must be unchanged (both are chrome this refresh preserves rather
        /// than redraws). Ignoring a LEAF material - the common case, and the
        /// one the field report is about - satisfies all of that. Ignoring
        /// a node with children does not, because an ignored node is built
        /// as a leaf, and that click still pays for a full rebuild.
        /// </para>
        /// </summary>
        internal bool TryRefreshInPlace(IReadOnlyList<CraftingTreeNode> newRoots)
        {
            if (_treeRoots == null || _treeFlow == null || newRoots == null) return false;
            if (_treeRowIdsAmbiguous || _treeRowsByNodeId.Count == 0) return false;
            if (newRoots.Count != _treeRoots.Count) return false;

            var scan = ScanTreeColumns(newRoots);
            if (scan.NodeCount != _scannedNodeCount) return false;
            if (!CostWidthsEqual(scan.CostWidths, _costColumnWidths)) return false;

            var plan = new List<KeyValuePair<TreeRowHandle, CraftingTreeNode>>(_treeRowsByNodeId.Count);
            if (!MatchRows(newRoots, 0, false, plan)) return false;

            // Every built row has to be accounted for. A shorter plan means
            // the new tree reaches fewer rows than are on screen, which is
            // a structural change the walk cannot see from the top.
            if (plan.Count != _treeRowsByNodeId.Count) return false;

            int panelWidth = _getCurrentPanelWidth();
            foreach (var pair in plan)
            {
                RepaintRow(pair.Key, pair.Value, panelWidth);
            }

            _treeRoots = newRoots as List<CraftingTreeNode> ?? new List<CraftingTreeNode>(newRoots);
            RefreshTreeContainerHeights();
            return true;
        }

        /// <summary>
        /// Walks the new tree, pairing each node that HAS a row with that
        /// row and refusing the moment the two disagree about identity or
        /// structure. Only a node whose children were actually built
        /// descends - a collapsed subtree has no rows, so its shape is free
        /// to differ and is simply adopted along with the node.
        /// <para>
        /// A matching NodeId is where the pairing STARTS, not where it is
        /// settled: a synthetic cost-component id names a position in a
        /// vendor offer rather than an item, so identity is asked of
        /// TreeRowIdentity, which owns the argument.
        /// </para>
        /// </summary>
        private bool MatchRows(
            IReadOnlyList<CraftingTreeNode> newSiblings, int depth, bool dimmed,
            List<KeyValuePair<TreeRowHandle, CraftingTreeNode>> plan)
        {
            for (int i = 0; i < newSiblings.Count; i++)
            {
                var newNode = newSiblings[i];
                if (!_treeRowsByNodeId.TryGetValue(newNode.NodeId, out var handle)) return false;
                if (handle.Depth != depth || handle.Dimmed != dimmed) return false;
                if (!TreeRowIdentity.SameRow(handle.Node, newNode)) return false;

                plan.Add(new KeyValuePair<TreeRowHandle, CraftingTreeNode>(handle, newNode));

                if (handle.State == null || !handle.State.ChildrenBuilt) continue;

                bool childDimmed = dimmed || newNode.Decision != CraftingDecision.Craft;
                if (!MatchRows(newNode.Children, depth + 1, childDimmed, plan)) return false;
            }
            return true;
        }

        private static bool CostWidthsEqual(
            TreeCostColumnMath.CostColumnWidths a, TreeCostColumnMath.CostColumnWidths b)
        {
            return a.GoldTextWidth == b.GoldTextWidth
                && a.SilverTextWidth == b.SilverTextWidth
                && a.CopperTextWidth == b.CopperTextWidth
                && a.CurrencyRunWidth == b.CurrencyRunWidth;
        }

        /// <summary>
        /// Re-renders the parts of one row a re-solve can change - the qty
        /// prefix, the pill column, the cost cell and the tooltip - into
        /// the controls the row already has. Everything
        /// <see cref="TreeRowIdentity"/> has just proved unchanged (icon,
        /// name text, rarity colour, caret, dim chrome) is left alone,
        /// which is most of the row and all of its texture work.
        /// <para>
        /// Unconditional rather than gated on a per-row "did anything
        /// change" test: a pill's own text, colour, tooltip and click
        /// wiring all derive from the node AND from plan-scope facts
        /// (currency totals, owned amounts, subduing results), so a
        /// cheaper test would have to re-derive nearly all of it to be
        /// correct - and a wrong skip leaves a stale, still-clickable pill.
        /// </para>
        /// </summary>
        private void RepaintRow(TreeRowHandle handle, CraftingTreeNode newNode, int panelWidth)
        {
            var nameFont = UiFonts.Body;

            if (handle.QtyLabel != null)
            {
                string newQtyPrefix = $"{newNode.Quantity}x ";
                if (handle.QtyLabel.Text != newQtyPrefix)
                {
                    handle.QtyLabel.Text = newQtyPrefix;
                    handle.QtyWidth = (int)Math.Ceiling(nameFont.MeasureString(newQtyPrefix).Width);
                    handle.NameLabel.Location = new Point(handle.NameX + handle.QtyWidth, 12);
                }
            }

            var edges = RowEdges(handle, panelWidth);

            // Only when the budget actually moved: the name TEXT is one of
            // the facts TreeRowIdentity proved unchanged, so an unchanged
            // qty prefix leaves an unchanged ellipsis, and this is the
            // row's only MeasureString loop.
            string displayName = LabelHelpers.EllipsizeToWidth(nameFont, handle.FullName, edges.NameMaxWidth);
            if (handle.NameLabel.Text != displayName)
            {
                handle.NameLabel.Text = displayName;
            }

            DisposePills(handle.Pills);
            RenderDecisionPills(handle.RowPanel, newNode, edges.PillColX, 10, handle.Dimmed, handle.Pills);

            DisposeValueCell(handle.CostCell);
            RenderCostCell(handle, newNode, edges.CostRightEdge, handle.Dimmed);

            var extraTooltipContent = TreeRowTooltipComposer.BuildExtraTooltipContent(
                newNode, handle.CaptionText, _getCurrentPlan());
            UpdateTreeRowTooltip(
                handle.RowPanel, handle.NameLabel, handle.QtyLabel, handle.IconFrame, handle.IconScrim,
                handle.FullName,
                () => TreeRowTooltipComposer.BuildStatTooltipContent(newNode, _getItemStatBlock),
                extraTooltipContent);

            handle.Node = newNode;
            if (handle.State != null)
            {
                handle.State.Node = newNode;
                handle.State.ChildDimmed = handle.Dimmed || newNode.Decision != CraftingDecision.Craft;
            }
        }

        private static void DisposePills(List<Panel> pills)
        {
            foreach (var pill in pills)
            {
                pill.Dispose();
            }
            pills.Clear();
        }

        /// <summary>
        /// Disposes every control a value cell put on its row - the dash,
        /// or both halves of the coin/currency segment run. A cell is
        /// parented straight to the row panel rather than to a wrapper of
        /// its own, so there is no single control to drop.
        /// </summary>
        private static void DisposeValueCell(CoinCurrencyRenderer.ValueCellHandle cell)
        {
            if (cell == null) return;

            cell.DashLabel?.Dispose();
            DisposeSegments(cell.CoinSegments);
            DisposeSegments(cell.CurrencySegments);
        }

        private static void DisposeSegments(CoinCurrencyRenderer.SegmentLayoutHandle segments)
        {
            if (segments.Controls == null) return;

            for (int i = 0; i < segments.Controls.Length; i++)
            {
                segments.Controls[i].Item1?.Dispose();
                segments.Controls[i].Item2?.Dispose();
            }
        }

        /// <summary>
        /// Rebuilds a tree row's tooltip from its (possibly re-ellipsized)
        /// display name plus its width-invariant extra lines - shared by
        /// RenderTreeNode's initial build and its settle re-ellipsis
        /// closure so the two can never disagree about tooltip content.
        /// <para>
        /// Rich, not <c>BasicTooltipText</c>: a row's unit-price line
        /// carries a gold figure, which only the rich surface can draw with
        /// coin icons - see <see cref="TooltipFacility"/>.
        /// </para>
        /// </summary>
        private static void UpdateTreeRowTooltip(
            Panel rowPanel, Label nameLabel, Label qtyLabel,
            Panel iconFrame, Panel iconScrim,
            string fullName,
            Func<TooltipContent> getStatContent, TooltipContent extraContent)
        {
            // The whole tooltip is composed when the box is about to be
            // drawn - see TooltipFacility.ApplyRichDeferred. Which means
            // the row's re-ellipsis no longer has to re-stamp anything,
            // and a stat block fetched after this render still shows.
            Func<TooltipContent> build = () =>
            {
                // The stat block already OPENS with the full item name, in
                // its rarity colour, so the truncation line would be a
                // duplicate; the name line is the no-stats fallback only.
                // The blank between it and the plan lines is its own
                // block rhythm.
                // nameLabel.Text, not the display name captured at build
                // time: the settle re-ellipsis rewrites the label in place
                // and no longer re-stamps anything, so the truncation
                // state has to be read live.
                return ItemRowTooltipComposer.BuildRowContent(
                    getStatContent(), fullName, nameLabel.Text != fullName, extraContent);
            };

            // The name and quantity Labels get it too, not just the row
            // Panel. Tooltip lookup reads ONE control -
            // Tooltip.HandleMouseMoved uses Control.ActiveControl, which is
            // the deepest capturing control under the cursor - so a Label
            // lying over the row swallows the row's hover. Same
            // swallowed-hover class already fixed in
            // ShoppingListSectionRenderer, in LogTabContent's rows, and in
            // this file's own pill outer/inner/label stamping. On the tree
            // it bit the worst spot: the item NAME, which is exactly what a
            // reader points at to find out what the row is, and exactly
            // what the tooltip's full-name line exists to expand.
            //
            // Tooltips ONLY. The row's click, right-click and hover-wash
            // handlers stay on rowPanel alone and must not be copied onto
            // these Labels: mouse EVENTS do reach the parent, because
            // Container.TriggerMouseInput fires the container's own handlers
            // (base.TriggerMouseInput) before it recurses into children - the
            // deepest child only wins the RETURN value (ActiveControl) and
            // suppresses its siblings. Duplicating toggleHandler onto a Label
            // would toggle the row twice per click.
            TooltipFacility.ApplyRichDeferred(rowPanel, build);
            TooltipFacility.ApplyRichDeferred(nameLabel, build);
            TooltipFacility.ApplyRichDeferred(qtyLabel, build);

            // The icon column is the same swallowed-hover case, one level
            // deeper: the framed icon is a Panel inside a Panel, and a
            // dimmed row lays a scrim Panel over the top of both. Left
            // unstamped, the largest and most obvious target on the row -
            // the item picture - was the one spot that showed nothing.
            IconControls.ApplyRichDeferredToIconTree(iconFrame, build);
            IconControls.ApplyRichDeferredToIconTree(iconScrim, build);
        }

        // --- Decision pills ---
        //
        // PillKind/PillSpec/BuildPillSpecs (the decision -> pill mapping,
        // gw2e's multi-pill model, KNOWN-ISSUES #18) live in
        // Services/DecisionPillPlanner.cs - Blish-free and directly unit
        // tested (DecisionPillPlannerTests) - so only the actual
        // Panel/Label rendering below stays here.

        /// <summary>
        /// Whether the cursor is over one of this row's decision pills. The
        /// row panel receives every mouse event its pills do (measured -
        /// Container.TriggerMouseInput raises the container's own events
        /// before walking its children), so both the row's click handler and
        /// its press feedback have to defer to the pill under the cursor.
        /// </summary>
        private static bool AnyPillHovered(List<Panel> pillPanels)
        {
            foreach (var pill in pillPanels)
            {
                if (pill.MouseOver)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Renders the pill column into the caller's own list, which the
        /// row's expand/collapse click handler closes over to exclude pills
        /// from its hit-test (a pill click is a decision, not a toggle).
        /// The list is REFILLED rather than replaced so an in-place refresh
        /// can rebuild a row's pills without invalidating that closure.
        ///
        /// TreePillColumnWidth (256px) is
        /// a fixed budget, but DecisionPillPlanner.AppendOwnershipPills now
        /// unconditionally adds an "IGNORE" pill (plus "USING N OWNED" when
        /// applicable) to every ordinary node, on top of its 1-3 source
        /// pills - realistic combinations still exceed it (a measured
        /// "HAVE n/m NEEDED" annotation run reaches 436px). Rather
        /// than let trailing pills render on top of the right-aligned cost
        /// column (this row has no wrap/second-line support - TreeRowHeight
        /// is a fixed per-row height shared by every layout/scroll-height
        /// calculation in this file), PlanRelayoutMath.ComputePillFit
        /// decides the column: all pills at normal padding, else all pills
        /// at tightened padding, else as many tightened pills as fit
        /// alongside a trailing "+N" pill naming what was left out.
        /// Trailing pills used to be dropped with nothing on the row to say
        /// they existed at all.
        /// <para>
        /// The budget is width-INVARIANT: maxRightEdge - pillColX is always
        /// TreePillColumnWidth - 4, whatever the panel width, because both
        /// endpoints move together. That is why the fit is resolved once at
        /// build time and the resize closure only repositions - there is no
        /// window width at which a hidden pill would have fit.
        /// </para>
        /// </summary>
        // Moved verbatim from CraftingPlanView.RenderDecisionPills. Only
        // edit: the interactive/ignoreInteractive click handlers write
        // _nodeOverrides/_ignoredItemIds and call ApplyOverridesAndResolve
        // - both now this class's own field/method, so the bodies are
        // unchanged text.
        private void RenderDecisionPills(
            Panel rowPanel, CraftingTreeNode node, int pillColX, int pillY, bool dimmed,
            List<Panel> pillPanels)
        {
            // Plan-scope currency facts
            // for the new HAVE/TOTAL pill - see PlanViewModel.
            // CurrencyPlanTotals/OwnedCurrencyAmounts' own doc comments.
            var plan = _getCurrentPlan();
            var specs = DecisionPillPlanner.BuildPillSpecs(node, plan?.CurrencyPlanTotals, plan?.OwnedCurrencyAmounts);
            var font = UiFonts.Caption;
            pillPanels.Clear();
            int x = pillColX;

            var pillWidths = new List<int>(specs.Count);
            foreach (var spec in specs)
            {
                pillWidths.Add((int)System.Math.Ceiling(font.MeasureString(spec.Text).Width) + PillPadding);
            }
            int maxRightEdge = pillColX + TreePillColumnWidth - 4;
            var fit = PlanRelayoutMath.ComputePillFit(
                pillWidths, PillPadding - TightPillPadding, PillGap, pillColX, maxRightEdge,
                MeasureOverflowPillWidth);

            int chosenPadding = PillPadding - fit.WidthReduction;

            for (int specIndex = 0; specIndex < fit.VisibleCount; specIndex++)
            {
                var spec = specs[specIndex];
                int pillWidth = PlanRelayoutMath.ReducedWidth(pillWidths[specIndex], fit.WidthReduction);
                int textWidth = pillWidth - chosenPadding;

                PillColors.GetPillColors(spec.Kind, node.IsIgnored, out Color borderColor, out Color fillColor);
                // White, not borderColor: Selected/Available fills expose the
                // border hue behind the label, so border-colored text has zero
                // contrast against its own backdrop.
                Color textColor = Color.White;
                // Chrome (UNKNOWN/UNRECOGNIZED/CURRENCY/GUILD UPGRADE/the
                // sole-source badge) reads one tier below a pill you can
                // act on, matching the recessed ring PillColors gives it.
                if (PillColors.IsNonInteractiveChrome(spec.Kind))
                {
                    textColor *= PillColors.NonInteractiveTextAlpha;
                }
                if (dimmed)
                {
                    // PillColors.DimmedPillFactor, not the 0.35 this row's
                    // name/quantity/cost use - see that constant's own doc
                    // comment for why a pill needs a higher floor than the
                    // text around it.
                    borderColor *= PillColors.DimmedPillFactor;
                    fillColor *= PillColors.DimmedPillFactor;
                    textColor *= PillColors.DimmedPillFactor;
                }

                var outer = CreatePillPanel(rowPanel, spec.Text, font, pillWidth, textWidth, x, pillY,
                    borderColor, fillColor, textColor, out Panel inner, out Label label);

                // The pill's head prose. Resolved by the branches below,
                // then composed with the subduing/value-detail/dead-click
                // blocks and stamped onto outer/inner/label at the bottom
                // of this loop - the inner fill panel and its label cover
                // almost the entire pill, so a tooltip on outer alone is
                // swallowed by whichever child is under the cursor (labels
                // capture mouse). Click/MouseEntered/MouseLeft stay on
                // outer only - unlike tooltip lookup, those already work
                // correctly today.
                string tooltipText = null;

                // The dimmed-only difference between this and the two flags
                // below is exactly what the dead-click tooltip at the
                // bottom of this loop has to explain.
                bool clickableWhenActive = DecisionPillPlanner.IsInteractive(spec);
                bool interactive = !dimmed && spec.Source.HasValue && _resolveOverridesSync != null;
                bool ignoreInteractive = !dimmed && spec.Kind == PillKind.Ignore && _resolveOverridesSync != null;

                // Built outside the interactive arm below: a decisively-
                // losing pill owes the reader its "why it loses" text
                // whether or not this row's clicks are wired, and a dimmed
                // row's pills are exactly the ones that are not. Pure text
                // derived from the spec, so it costs nothing to resolve
                // here and null for every other kind.
                TooltipContent subduingContent = spec.Kind == PillKind.Subdued
                    ? PillSubduingTooltipBuilder.BuildContent(
                        spec.SubduingResult, plan?.ItemMetadata, plan?.CurrencyMetadata)
                    : null;
                bool appendSubduing = false;

                if (interactive)
                {
                    tooltipText = $"Switch to {spec.Text}";
                    // A decisively-losing
                    // pill (Kind == Subdued) stays clickable - only its
                    // tooltip gains the "why" explanation, appended after
                    // the ordinary "Switch to X" line rather than replacing
                    // it, since clicking still does exactly that.
                    appendSubduing = subduingContent != null;
                    var source = spec.Source.Value;
                    outer.Click += (_, __) =>
                    {
                        _nodeOverrides[node.NodeId] = source;
                        ApplyOverridesAndResolve();
                    };
                    Color restingBorder = borderColor;
                    outer.MouseEntered += (_, __) => outer.BackgroundColor = Color.White;
                    outer.MouseLeft += (_, __) => outer.BackgroundColor = restingBorder;
                    PressFeedback.Wire(outer);
                }
                else if (ignoreInteractive)
                {
                    // Toggles this ITEM id (not just this node) in
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
                    PressFeedback.Wire(outer);
                }
                else if (spec.Kind == PillKind.Subdued)
                {
                    // Reached only when the click is NOT wired - a dimmed
                    // row, or no re-solve callback at all. The pill still
                    // shows why this option loses; the dead-click line
                    // below is appended after it, never over it.
                    appendSubduing = subduingContent != null;
                }
                else if (spec.Kind == PillKind.Locked)
                {
                    // A cost-component leaf's "CURRENCY" badge - its cost
                    // cell is deliberately blank because the quantity
                    // itself IS the cost. Never a "no source" situation
                    // like the other Locked pills, so it gets its own
                    // tooltip first.
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
                    // guildupgrade-ingredients fix: the GUILD UPGRADE pill
                    // is the same "no available source" situation as
                    // UNKNOWN above (not "exactly one feasible source" -
                    // "Only available source" would be equally misleading
                    // here), just with its own always-populated
                    // AcquisitionHint (see CraftingTreeBuilder's
                    // "GuildUpgrade" branch) instead of a seeded wiki hint.
                    else if (node.Decision == CraftingDecision.GuildUpgrade)
                    {
                        tooltipText = !string.IsNullOrEmpty(node.AcquisitionHint)
                            ? node.AcquisitionHint
                            : "Requires a claimed Guild Hall upgrade";
                    }
                    // The UNRECOGNIZED pill is the same "no available
                    // source" situation as UNKNOWN/GUILD UPGRADE, not
                    // "exactly one feasible source" - without this branch
                    // it falls into the misleading "Only available source"
                    // default. node.AcquisitionHint is always null here
                    // (the builder returns before ApplyAcquisitionHint).
                    else if (node.Decision == CraftingDecision.UnrecognizedIngredient)
                    {
                        tooltipText = "Unrecognized ingredient type - no known acquisition source";
                    }
                    // The plain CURRENCY pill must not fall into the "Only
                    // available source" default - a currency ingredient is
                    // paid from the wallet, so no "source" wording applies.
                    else if (node.Decision == CraftingDecision.Currency)
                    {
                        tooltipText = "Paid from your wallet as a game currency - no purchase or crafting source applies";
                    }
                    else
                    {
                        tooltipText = "Only available source";
                    }
                }
                else if (spec.Kind == PillKind.Selected)
                {
                    // The currently-committed source pill is
                    // non-interactive (clicking it would be a no-op
                    // re-solve), but still gets a tooltip.
                    tooltipText = $"Current source: {spec.Text}";
                }
                else if ((spec.Kind == PillKind.Have || spec.Kind == PillKind.OwnedInfo) &&
                    (node.Decision == CraftingDecision.Currency ||
                     (node.IsCostComponent && !node.SubtreeCost.HasValue)))
                {
                    // The plan-scope HAVE/TOTAL pill reuses the same
                    // PillKind.Have/OwnedInfo the item-ownership pills
                    // use, so it must be intercepted BEFORE the ordinary
                    // branches below, whose item-ownership wording means
                    // nothing for a currency leaf. The pill text is
                    // plan-scope only; the tooltip adds what the pill text
                    // cannot: this row's own need (node.Quantity).
                    int have = 0;
                    plan?.OwnedCurrencyAmounts?.TryGetValue(node.ItemId, out have);
                    long planTotal = 0;
                    plan?.CurrencyPlanTotals?.TryGetValue(node.ItemId, out planTotal);
                    long shortfall = planTotal > have ? planTotal - have : 0;
                    tooltipText = shortfall > 0
                        ? $"Plan needs {planTotal} total, you have {have} - short {shortfall}. This row needs {node.Quantity}."
                        : $"Plan needs {planTotal} total, you have {have} - fully covered. This row needs {node.Quantity}.";
                }
                else if (spec.Kind == PillKind.Have)
                {
                    // An ITEM cost-component leaf can never reach this
                    // branch (it gets only badges, never PillKind.Have);
                    // a currency leaf CAN, but is always intercepted by
                    // the currency-specific branch above - so this
                    // tooltip only needs the ordinary-item wording. For a
                    // genuinely-owned Have node, Quantity is 0, so
                    // OwnedQuantityUsed alone is the original total demand.
                    tooltipText = $"Needs {node.OwnedQuantityUsed} - all covered by your materials";
                }
                else if (spec.Kind == PillKind.OwnedInfo)
                {
                    if (node.IsCostComponent)
                    {
                        // The "OWN n" badge's tooltip - owning some of a
                        // cost component never reduces what must be
                        // handed over or this line's cost; stated
                        // explicitly so it is never mistaken for the
                        // "reduced the plan" vocabulary used elsewhere.
                        tooltipText =
                            $"You own {node.ComponentOwnedQuantity} - informational only, " +
                            "does not change the plan cost";
                    }
                    else
                    {
                        // Matches the "HAVE {used}/{total} NEEDED" pill
                        // wording; remaining (node.Quantity) is total
                        // minus used.
                        int totalDemand = node.OwnedQuantityUsed + node.Quantity;
                        tooltipText =
                            $"Needs {totalDemand} total - {node.OwnedQuantityUsed} covered by your materials, " +
                            $"{node.Quantity} left to acquire";
                    }
                }
                else if (spec.Kind == PillKind.AchievementBitDeduped)
                {
                    // KNOWN-ISSUES #26: explains the "COUNTED
                    // ELSEWHERE" semantics - nothing here is actually
                    // owned, this exact occurrence is just already required
                    // elsewhere in the tree.
                    tooltipText = "Already counted elsewhere in the tree - this item is obtained once, not needed again here";
                }

                // Appends the value-detail
                // hover (real gold vs. decision-only optimization price,
                // plus a vendor cap line when applicable) onto the
                // committed CRAFT/VENDOR pill's existing tooltip, only when
                // ValueDetailTooltipBuilder finds a real divergence -
                // Selected (multi-option winner) and Locked (sole option)
                // are the only two kinds a committed CRAFT/VENDOR pill can
                // ever have (see BuildPillSpecs' own "the selected pill
                // always matches node.Decision" guarantee), so gating on
                // node.Decision here (rather than re-checking spec.Text)
                // cannot accidentally attach this to an unrelated pill -
                // every other Kind's node.Decision is never Craft/
                // BuyFromVendor (Currency/GuildUpgrade/Have/etc. all use
                // their own distinct CraftingDecision values).
                TooltipContent valueDetailContent = null;
                if ((spec.Kind == PillKind.Selected || spec.Kind == PillKind.Locked) &&
                    (node.Decision == CraftingDecision.Craft || node.Decision == CraftingDecision.BuyFromVendor))
                {
                    ValueDetailTooltipBuilder.TryBuildContent(
                        node, plan?.VendorCapsByItemId, out valueDetailContent);
                }

                // A dimmed row's would-be-clickable pills are inert: the
                // reference branch under a bought item is a "what it would
                // cost to craft instead" preview, not a live decision, so
                // the click handlers above are never wired. They still draw
                // a full pill set, so the only honest thing left is to say
                // why the click did nothing and what to change to make it
                // work. Appended, never assigned over: a dimmed Subdued
                // pill carries its "why it loses" text (resolved in its own
                // arm above, which exists precisely because the interactive
                // arm never runs on a dimmed row), and a dimmed committed
                // pill can carry the value-detail hover - neither may be
                // clobbered.
                //
                // Composed as CONTENT, not by string concatenation: the
                // value-detail block and a Weighted pill's margin both
                // carry gold figures the rich surface draws with coin
                // icons, and a "\n\n" join would have flattened them back
                // into text. Separator() is the blank line that join used
                // to produce, and is a no-op when nothing precedes it -
                // which is what the old "tooltipText == null ? x : y"
                // ternaries were doing by hand.
                var pillTooltip = new TooltipContentBuilder();
                pillTooltip.Text(tooltipText);
                if (appendSubduing)
                {
                    pillTooltip.Separator().Append(subduingContent);
                }
                if (valueDetailContent != null)
                {
                    pillTooltip.Separator().Append(valueDetailContent);
                }
                if (dimmed && clickableWhenActive)
                {
                    pillTooltip.Separator().Text(DimmedPillTooltip);
                }

                // All three controls point at the ONE shared rich surface
                // - see TooltipFacility for why there is one instance for
                // the whole module rather than one per tooltip'd control.
                var pillContent = pillTooltip.Build();
                if (!pillContent.IsEmpty)
                {
                    TooltipFacility.ApplyRich(outer, pillContent);
                    TooltipFacility.ApplyRich(inner, pillContent);
                    TooltipFacility.ApplyRich(label, pillContent);
                }

                pillPanels.Add(outer);
                x += pillWidth + PillGap;
            }

            if (fit.HiddenCount > 0)
            {
                RenderOverflowPill(rowPanel, specs, fit, font, x, pillY, dimmed, pillPanels);
            }
        }

        /// <summary>
        /// The trailing "+N" pill: the row admitting that N of its pills
        /// did not fit, instead of the column simply ending early. Styled
        /// as non-interactive chrome, because it is - clicking it does
        /// nothing, and its tooltip names what is missing.
        /// <para>
        /// Deliberately NOT wired to a popup offering the hidden options.
        /// The hidden pills are almost always the trailing annotation and
        /// the IGNORE toggle, and a real affordance means a new
        /// popup/menu surface (and its own dismiss, focus and scroll
        /// behaviour) hanging off a case that tightened padding already
        /// resolves most of the time. The tooltip states the fact; the
        /// desktop gate decides whether the fact needs an affordance.
        /// </para>
        /// <para>
        /// The tooltip does not suggest widening the window: the pill
        /// column's budget is fixed at TreePillColumnWidth regardless of
        /// panel width (see RenderDecisionPills), so that advice would be
        /// false.
        /// </para>
        /// </summary>
        private static void RenderOverflowPill(
            Panel rowPanel, IReadOnlyList<PillSpec> specs, PlanRelayoutMath.PillFitPlan fit,
            BitmapFont font, int x, int pillY, bool dimmed, List<Panel> pillPanels)
        {
            string text = OverflowPillText(fit.HiddenCount);
            int textWidth = (int)System.Math.Ceiling(font.MeasureString(text).Width);

            PillColors.GetPillColors(PillKind.Locked, false, out Color borderColor, out Color fillColor);
            Color textColor = Color.White * PillColors.NonInteractiveTextAlpha;
            if (dimmed)
            {
                borderColor *= PillColors.DimmedPillFactor;
                fillColor *= PillColors.DimmedPillFactor;
                textColor *= PillColors.DimmedPillFactor;
            }

            var hiddenTexts = new List<string>(fit.HiddenCount);
            for (int i = fit.VisibleCount; i < specs.Count; i++)
            {
                hiddenTexts.Add(specs[i].Text);
            }
            string tooltipText = $"No room to show: {string.Join(", ", hiddenTexts)}";

            var outer = CreatePillPanel(
                rowPanel, text, font, fit.OverflowPillWidth, textWidth, x, pillY,
                borderColor, fillColor, textColor, out Panel inner, out Label label);

            TooltipFacility.ApplyPlain(outer, tooltipText);
            TooltipFacility.ApplyPlain(inner, tooltipText);
            TooltipFacility.ApplyPlain(label, tooltipText);

            pillPanels.Add(outer);
        }

        private static string OverflowPillText(int hiddenCount)
        {
            return "+" + hiddenCount;
        }

        /// <summary>
        /// Width of the "+N" pill. A method group, not a lambda over the
        /// row's font local: RenderDecisionPills runs once per tree row, so
        /// a capturing closure would be one allocation per row on the
        /// render path for a callback most rows never invoke.
        /// </summary>
        private static int MeasureOverflowPillWidth(int hiddenCount)
        {
            var font = UiFonts.Caption;
            return (int)System.Math.Ceiling(
                font.MeasureString(OverflowPillText(hiddenCount)).Width) + TightPillPadding;
        }

        /// <summary>
        /// One pill's three nested controls (border panel, inset fill
        /// panel, centered label) - shared by the decision pills and the
        /// trailing "+N" pill so the two can never disagree about pill
        /// chrome. Border simulated as an outer colored panel with a
        /// 1px-inset fill panel, the same nesting technique
        /// IconControls.CreateItemIcon uses.
        /// </summary>
        private static Panel CreatePillPanel(
            Panel rowPanel, string text, BitmapFont font, int pillWidth, int textWidth,
            int x, int pillY, Color borderColor, Color fillColor, Color textColor,
            out Panel inner, out Label label)
        {
            var outer = new Panel()
            {
                Size = new Point(pillWidth, PillHeight),
                Location = new Point(x, pillY),
                BackgroundColor = borderColor,
                Parent = rowPanel
            };
            inner = new Panel()
            {
                Size = new Point(pillWidth - 2, PillHeight - 2),
                Location = new Point(1, 1),
                BackgroundColor = fillColor,
                Parent = outer
            };
            // Clamped: a decision pill's width is its text plus padding, so
            // the offset is always positive there, but the "+N" pill's
            // width was reserved before its final N was known - a
            // digit-count change would otherwise start its label left of
            // its own pill.
            int labelX = (pillWidth - 2 - textWidth) / 2;
            if (labelX < 0) labelX = 0;
            label = new Label()
            {
                Text = text,
                Font = font,
                TextColor = textColor,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(labelX, 2),
                Parent = inner
            };
            return outer;
        }
    }
}
