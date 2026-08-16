using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services.Diagnostics;

namespace GW2CraftingHelper.Services
{
    public class CraftingPlanPipeline
    {
        private readonly RecipeService _recipeService;
        private readonly TradingPostService _tradingPostService;
        private readonly PlanSolver _solver;
        private readonly ItemMetadataService _itemMetadataService;
        private readonly VendorOfferStore _vendorOfferStore;
        private readonly InventoryReducer _reducer;
        private readonly IAccountRecipeClient _accountRecipeClient;
        private readonly CurrencyMetadataService _currencyMetadataService;
        private readonly IReadOnlyDictionary<int, AcquisitionHint> _acquisitionHints;

        // W3B: rich per-generation logging sink. Optional constructor
        // injection (defaults to the app-wide ModuleLog.Shared singleton -
        // see Module.cs's construction site, which never passes this) so
        // tests can inject an isolated `new ModuleLog()` instance for
        // deterministic, non-shared assertions instead of touching Shared -
        // see ModuleLog's own class doc comment on why Shared is unsuitable
        // for exact-count/content test assertions.
        private readonly ModuleLog _moduleLog;

        // W3B review-fix: shared literal for both GenerateStructuredAsync's
        // single-item Step 1 and GenerateStructuredMultiAsync's Step 1 -
        // used both as the tree-building PlanPhaseEvent's Detail (surfaced
        // live in CraftingPlanView.FormatPhaseText) and inside the
        // existing PlanStatus wording, so the two channels never drift out
        // of sync with each other.
        private const string FirstRunTreeHint = "may take several seconds on first run";

        public CraftingPlanPipeline(
            RecipeService recipeService,
            TradingPostService tradingPostService,
            PlanSolver solver,
            ItemMetadataService itemMetadataService,
            VendorOfferStore vendorOfferStore = null,
            InventoryReducer reducer = null,
            IAccountRecipeClient accountRecipeClient = null,
            CurrencyMetadataService currencyMetadataService = null,
            IReadOnlyDictionary<int, AcquisitionHint> acquisitionHints = null,
            ModuleLog moduleLog = null)
        {
            _recipeService = recipeService;
            _tradingPostService = tradingPostService;
            _solver = solver;
            _itemMetadataService = itemMetadataService;
            _vendorOfferStore = vendorOfferStore;
            _reducer = reducer;
            _accountRecipeClient = accountRecipeClient;
            _currencyMetadataService = currencyMetadataService;
            _acquisitionHints = acquisitionHints;
            _moduleLog = moduleLog ?? ModuleLog.Shared;
        }

        public async Task<CraftingPlanResult> GenerateStructuredAsync(
            int targetItemId, int quantity, AccountSnapshot snapshot,
            CancellationToken ct, IProgress<PlanStatus> progress = null,
            string activeCharacterName = null,
            // M33 spec item 8: default to gw2efficiency's own "buy price"
            // (buy orders) basis rather than instant-buy - see
            // Views/CraftingPlanView.cs's matching field default.
            PriceBasis priceBasis = PriceBasis.BuyOrder,
            CurrencyValuation currencyValuation = null,
            OwnMaterialsMode ownMaterialsMode = OwnMaterialsMode.Free,
            // M37 (KNOWN-ISSUES #24, gw2e parity): see ModuleSettings.
            // GetHomesteadEfficiencyTiers/PlanSolveContext.HomesteadTiers.
            HomesteadEfficiencyTiers homesteadTiers = null,
            // W3B: live coarse-phase events for CraftingPlanView's status
            // strip - see PlanPhaseEvent's own doc comment. Optional/
            // default null so every existing caller (Module.cs, every
            // pipeline test) is unaffected.
            IProgress<PlanPhaseEvent> phaseProgress = null,
            // W3C review-fix (mustFix): the cosmetic per-character
            // discipline list, threaded as ITS OWN argument rather than
            // derived solely from `snapshot`. Module.cs's useOwn:false
            // branch intentionally passes snapshot: null to disable
            // reduction/force-buy/owned-currency, but that must NOT also
            // blank the Required Disciplines tiebreak - see
            // AccountSnapshot.CharacterDisciplines' doc comment. Default
            // null preserves every existing caller's behavior unchanged
            // (falls back to snapshot?.CharacterDisciplines below).
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines = null)
        {
            var valuation = currencyValuation ?? CurrencyValuation.None;
            var tiers = homesteadTiers ?? HomesteadEfficiencyTiers.Default;
            var sw = new Stopwatch();
            var timingLog = new List<string>();
            var phaseTracker = new PhaseTracker(phaseProgress, _moduleLog);

            // Step 1: Build recipe tree
            // W3B review-fix: the "(may take several seconds on first run)"
            // hint now also rides the phase event's Detail (see
            // PlanPhaseEvent.Detail and CraftingPlanView.FormatPhaseText),
            // so it still reaches the live status strip now that the view
            // passes progress: null below - see FirstRunTreeHint's own
            // doc comment.
            phaseTracker.Start(PlanPhase.BuildingTree, "Building recipe tree", null, FirstRunTreeHint);
            progress?.Report(new PlanStatus
            {
                Message = $"Building recipe tree ({FirstRunTreeHint})..."
            });
            // W3B review-fix: these two RecipeService diagnostics (this one
            // and the stale-seed warning below) exist to explain a slow
            // first run and an out-of-date recipe seed - genuinely useful,
            // not routine per-step noise, and CraftingPlanView now passes
            // progress: null (the coarse phase events above replace
            // PlanStatus's frequent per-step text for the live strip). Also
            // writing them straight to ModuleLog guarantees they are never
            // silently lost regardless of whether any IProgress<PlanStatus>
            // consumer is attached - RecipeService's own statusReported/
            // staleReported flags already bound this to at most one Info
            // line each per generation, so this cannot spam the log.
            _recipeService.OnStatusUpdate = msg =>
            {
                progress?.Report(new PlanStatus { Message = msg });
                _moduleLog.Write(ModuleLogLevel.Info, "plan", msg);
            };
            sw.Restart();
            RecipeNode tree;
            try
            {
                tree = await _recipeService.BuildTreeAsync(targetItemId, quantity, ct);
            }
            finally
            {
                _recipeService.OnStatusUpdate = null;
            }
            sw.Stop();
            timingLog.Add($"Build recipe tree: {sw.ElapsedMilliseconds}ms");

            // M37 (KNOWN-ISSUES #26): pure correctness fix, always applied
            // (no settings toggle) - a no-op whenever the tree has no
            // achievement-bit ingredients at all (every existing seed row).
            // Runs BEFORE inventory reduction (Step 6) and the force-buy
            // pre-pass's own zero-owned-baseline solve below - see
            // AchievementBitDedupPrePass's own doc comment for why.
            AchievementBitDedupPrePass.Apply(tree);

            // Step 2: Collect all item IDs from the tree for price lookup
            progress?.Report(new PlanStatus { Message = "Collecting item IDs..." });
            sw.Restart();
            var allItemIds = new HashSet<int>();
            CollectItemIds(tree, allItemIds);
            sw.Stop();
            timingLog.Add($"Collect item IDs: {sw.ElapsedMilliseconds}ms ({allItemIds.Count} items)");

            // Step 3: Fetch TP prices
            phaseTracker.Start(PlanPhase.FetchingPrices, "Fetching prices", allItemIds.Count);
            progress?.Report(new PlanStatus
            {
                Message = $"Fetching prices ({allItemIds.Count} items)...",
                Total = allItemIds.Count
            });
            sw.Restart();
            var prices = await _tradingPostService.GetPricesAsync(allItemIds, ct);
            sw.Stop();
            timingLog.Add($"Fetch TP prices: {sw.ElapsedMilliseconds}ms ({allItemIds.Count} items)");

            // Step 4: Query vendor offers, then price any vendor-only cost items
            var vendorContext = await FetchPricedVendorContextAsync(
                allItemIds, prices, progress, sw, timingLog, ct);
            var vendorOffers = vendorContext.VendorOffers;
            prices = vendorContext.Prices;

            // M34-B2a #3: gw2e's "Value Own Materials" force-buy pre-pass -
            // only runs when the setting is Valued AND a snapshot actually
            // drives reduction (see OwnedMaterialsForceBuyPrePass's and
            // ModuleSettings.ValueOwnMaterials's doc comments for why this
            // is deliberately narrower than gw2e's own unconditional
            // `if (valueOwnItems)` gate).
            bool useForceBuyPrePass = ownMaterialsMode == OwnMaterialsMode.Valued &&
                snapshot != null && _reducer != null;

            if (useForceBuyPrePass)
            {
                // Pre-assign stable NodeIds to the UNREDUCED tree BEFORE
                // Step 6 clones/prunes it below - see RecipeNodeIds' doc
                // comment: InventoryReducer.CloneNode preserves whatever
                // NodeId a node already has, so these ids survive onto the
                // corresponding surviving nodes of the reduced tree Step 7
                // solves, letting the pre-pass below (computed against a
                // genuine zero-owned baseline - this same, still-unreduced
                // `tree`) key its forceBuyOnlyNodeIds set against exactly
                // the ids that real solve will use.
                RecipeNodeIds.Assign(tree);
            }

            // Step 5.5/5.6 (M34-B2a #3 / VOM design Candidate A - review-fix:
            // merged into one `if` block, was two adjacent identical
            // `if (useForceBuyPrePass)` blocks - see GenerateStructuredMultiAsync's
            // matching block for why keeping the two edit sites in lockstep
            // matters here). Both computed against `tree` - the ORIGINAL,
            // UNREDUCED tree (InventoryReducer.Reduce below only ever
            // mutates its CLONE, so `tree` still holds the full
            // pre-ownership demand here) - matching gw2e's own
            // zero-owned-baseline mechanics exactly (Section 2.2 of the R2
            // report): otherwise, evaluating this rule on the ALREADY-
            // reduced tree would make it a near no-op in precisely the
            // scenario it exists for, since owning a pile of components
            // already makes their post-reduction craft cost look cheap
            // regardless of what a FRESH purchase would cost. Moved ahead
            // of Step 6 (VOM design, Candidate A) so its output can feed
            // the zero-owned decision pass below, which Step 6's Reduce
            // call now needs as its guide.
            //
            // Step 5.6's throwaway Solve() runs on the SAME zero-owned/
            // unreduced `tree`, this time WITH forceBuyOnlyNodeIds applied,
            // so its Decisions dictionary reflects the exact Craft/Buy/
            // vendor/recipe-option choice a zero-owned baseline would make.
            // InventoryReducer.Reduce below uses this as a guide: only the
            // option this decision actually chose gets to consume owned
            // stock, so owned stock can never flip a decision toward a
            // chain that was worse at market prices - it can only make the
            // zero-owned winner an even stronger winner. Null guide
            // (useForceBuyPrePass false, e.g. Free mode or no snapshot)
            // leaves InventoryReducer's legacy primary-option heuristic
            // fully in charge, unchanged.
            ISet<int> forceBuyOnlyNodeIds = null;
            IReadOnlyDictionary<int, SolverDecision> zeroOwnedDecisions = null;
            if (useForceBuyPrePass)
            {
                forceBuyOnlyNodeIds = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                    _solver, tree, prices, vendorOffers, priceBasis, valuation);

                var zeroOwnedSolve = _solver.Solve(
                    tree, prices, vendorOffers, priceBasis,
                    overrides: null, currencyValuation: valuation,
                    forceBuyOnlyNodeIds: forceBuyOnlyNodeIds,
                    homesteadTiers: tiers);
                zeroOwnedDecisions = zeroOwnedSolve.Decisions;
            }

            // Step 6: Inventory reduction
            phaseTracker.Start(PlanPhase.SolvingDecisions, "Solving decisions", null);
            progress?.Report(new PlanStatus { Message = "Reducing inventory..." });
            sw.Restart();
            RecipeNode treeUsedForSolve = tree;
            List<UsedMaterial> usedMaterials = null;
            Dictionary<RecipeNode, int> ownedQuantityUsedByNode = null;
            // VOM finding #1 fix: captured here (rather than scoped inside
            // the `if` below) so it can also feed PlanSolveContext.
            // AccountIndex further down - see that field's own doc comment.
            AccountItemIndex accountIndex = null;

            if (snapshot != null && _reducer != null)
            {
                accountIndex = new AccountItemIndex(snapshot.Items);
                var reduced = _reducer.Reduce(tree, accountIndex, activeCharacterName, zeroOwnedDecisions);
                treeUsedForSolve = reduced.ReducedTree;
                usedMaterials = reduced.UsedMaterials;
                ownedQuantityUsedByNode = reduced.OwnedQuantityUsedByNode;
            }
            sw.Stop();
            timingLog.Add($"Inventory reduction: {sw.ElapsedMilliseconds}ms");

            // Step 7: Solve. assignNodeIds:false only when the pre-pass
            // above pre-assigned ids to `tree` (and therefore, via cloning,
            // to treeUsedForSolve's surviving nodes) - reusing those ids
            // here instead of renumbering from scratch is what lets
            // forceBuyOnlyNodeIds' keys actually match (see RecipeNodeIds).
            progress?.Report(new PlanStatus { Message = "Solving crafting plan..." });
            sw.Restart();
            var solveResult = _solver.Solve(
                treeUsedForSolve, prices, vendorOffers, priceBasis,
                overrides: null, currencyValuation: valuation,
                forceBuyOnlyNodeIds: forceBuyOnlyNodeIds,
                assignNodeIds: !useForceBuyPrePass,
                homesteadTiers: tiers);
            var plan = solveResult.Plan;
            sw.Stop();
            timingLog.Add($"Solve: {sw.ElapsedMilliseconds}ms");

            // Step 7b (M34-B2a #1): convert the per-node owned-usage side
            // channel (keyed by node object reference at reduction time,
            // when NodeId did not exist yet) into a NodeId-keyed lookup now
            // that Solve() above has assigned this tree's real, stable
            // NodeIds to these same node objects.
            IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId =
                BuildOwnedQuantityUsedByNodeId(ownedQuantityUsedByNode);

            // Step 8: Fetch item metadata for all step items + target + used materials + tree items
            // Fetch metadata for EVERY tree item (not just chosen-path ones):
            // local override re-solves can surface any node's item in steps,
            // and the cached SolveContext metadata must cover them all.
            var metadataIds = new HashSet<int>(allItemIds);
            metadataIds.UnionWith(plan.Steps.Select(s => s.ItemId));
            metadataIds.Add(targetItemId);
            if (usedMaterials != null)
            {
                foreach (var um in usedMaterials)
                {
                    metadataIds.Add(um.ItemId);
                }
            }
            // W4B: a vendor cost-component ITEM leaf (e.g. Globs of
            // Ectoplasm) is never a real tree ingredient - only a
            // VendorOffer.CostLines entry - so allItemIds above never
            // collects it. Add every such id here, before the single bulk
            // metadata fetch below, so CraftingTreeBuilder can resolve a
            // real name/icon for it instead of falling back to "Unknown
            // Item" (see AddVendorItemComponentIds).
            AddVendorItemComponentIds(solveResult.Decisions, metadataIds);
            // W4B review-fix (Must Fix): also widen for every OTHER offer
            // (not just the baseline winning one) reachable by a later
            // manual override - see AddAllVendorOfferItemComponentIds' own
            // doc comment for why ResolveWithOverrides needs this covered
            // up front (it never re-fetches metadata).
            AddAllVendorOfferItemComponentIds(vendorOffers, metadataIds);
            phaseTracker.Start(PlanPhase.FetchingItemDetails, "Fetching item details", metadataIds.Count);
            progress?.Report(new PlanStatus
            {
                Message = $"Fetching item details ({metadataIds.Count} items)...",
                Total = metadataIds.Count
            });
            sw.Restart();

            // Kick off the decorative currency-metadata fetch now, in
            // parallel with item metadata, rather than sequentially after
            // it - the service has its own internal timeout (see
            // CurrencyMetadataService), so a hung /v2/currencies can no
            // longer add to the plan-generation critical path. Observed
            // independently of the await below so a fault is never left
            // unobserved if item metadata throws first.
            var currencyTask = _currencyMetadataService?.GetAllAsync(ct);
            ObserveFault(currencyTask);

            var metadata = await _itemMetadataService.GetMetadataAsync(metadataIds, ct);
            sw.Stop();
            timingLog.Add($"Fetch item metadata: {sw.ElapsedMilliseconds}ms ({metadataIds.Count} items)");

            // Step 9: Await the currency name/icon metadata fetch started
            // above - see AwaitCurrencyMetadataOrNullAsync's own doc comment.
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata =
                await AwaitCurrencyMetadataOrNullAsync(currencyTask, progress, sw, timingLog, ct);

            // Step 10: Fetch learned recipe IDs (if permission available) -
            // see FetchLearnedRecipeIdsAsync's own doc comment.
            ISet<int> learnedRecipeIds =
                await FetchLearnedRecipeIdsAsync(progress, sw, timingLog, ct);

            // Step 11: Build structured result
            phaseTracker.Start(PlanPhase.BuildingDisplay, "Building display", null);
            progress?.Report(new PlanStatus { Message = "Building final result..." });
            sw.Restart();
            var resultBuilder = new PlanResultBuilder();
            // W3C: per-character discipline data, cosmetic only (see
            // AccountSnapshot.CharacterDisciplines' doc comment) - a
            // straight passthrough of the snapshot, never fed back into any
            // decision/total EXCEPT the Build() tiebreak below (see
            // PlanResultBuilder.Build's characterDisciplines doc comment -
            // it can only relabel which equally-good discipline is
            // reported, never change a decision or a total).
            // W3C review-fix (mustFix): prefer the explicit
            // characterDisciplines argument over snapshot?.CharacterDisciplines
            // so Build()'s tiebreak sees the SAME list whether or not
            // `snapshot` itself was nulled out to disable reduction (see
            // this method's characterDisciplines parameter doc comment).
            // Falls back to snapshot?.CharacterDisciplines when the caller
            // did not supply the argument, preserving every pre-existing
            // caller's behavior.
            var effectiveCharacterDisciplines = characterDisciplines ?? snapshot?.CharacterDisciplines;
            var result = resultBuilder.Build(
                plan, treeUsedForSolve, metadata, usedMaterials, learnedRecipeIds, effectiveCharacterDisciplines);
            result.CurrencyMetadata = currencyMetadata;
            result.AcquisitionHints = _acquisitionHints;
            result.CharacterDisciplines = effectiveCharacterDisciplines;

            // M34-B2a #4: owned-currency annotation, cosmetic only (see
            // AccountCurrencyIndex's doc comment) - built from the plan's
            // final currency totals and the wallet snapshot, never fed back
            // into any decision/total above.
            // W4B review-fix (Must Fix): also pass vendorOffers - see
            // BuildOwnedCurrencyAmounts' own doc comment for why.
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts =
                BuildOwnedCurrencyAmounts(snapshot, plan.CurrencyCosts, vendorOffers);
            result.OwnedCurrencyAmounts = ownedCurrencyAmounts;

            // W4B: owned-item annotation for vendor cost-component ITEM
            // leaves, cosmetic only - see
            // BuildOwnedVendorItemComponentAmounts' own doc comment.
            // W4B review-fix (Must Fix): also pass vendorOffers - see that
            // method's own doc comment for why.
            IReadOnlyDictionary<int, int> ownedVendorItemAmounts =
                BuildOwnedVendorItemComponentAmounts(snapshot, solveResult.Decisions, vendorOffers);

            // Build crafting tree
            var treeBuilder = new CraftingTreeBuilder();
            result.CraftingTree = treeBuilder.BuildTree(
                treeUsedForSolve, solveResult.Decisions, metadata, _acquisitionHints,
                ownedQuantityUsedByNodeId, ignoredItemIds: null, currencyMetadata: currencyMetadata,
                ownedCurrencyAmounts: ownedCurrencyAmounts, ownedVendorItemAmounts: ownedVendorItemAmounts);

            SellSideEconomics.ApplySellSideEconomics(
                result, treeUsedForSolve, solveResult, prices,
                targetItemId, quantity, priceBasis, usedMaterials, ownMaterialsMode);

            // Capture inputs so the UI can re-solve locally with per-node
            // overrides (no network round-trips).
            result.SolveContext = new PlanSolveContext
            {
                TargetItemId = targetItemId,
                Quantity = quantity,
                Tree = treeUsedForSolve,
                Prices = prices,
                VendorOffers = vendorOffers,
                Metadata = metadata,
                LearnedRecipeIds = learnedRecipeIds,
                UsedMaterials = usedMaterials,
                PriceBasis = priceBasis,
                CurrencyValuation = valuation,
                OwnMaterialsMode = ownMaterialsMode,
                CurrencyMetadata = currencyMetadata,
                AcquisitionHints = _acquisitionHints,
                OwnedQuantityUsedByNodeId = ownedQuantityUsedByNodeId,
                OwnedCurrencyAmounts = ownedCurrencyAmounts,
                OwnedVendorItemAmounts = ownedVendorItemAmounts,
                ForceBuyOnlyNodeIds = forceBuyOnlyNodeIds,
                HomesteadTiers = tiers,
                CharacterDisciplines = result.CharacterDisciplines,
                // VOM finding #1 fix: only populated when the force-buy
                // pre-pass ran (useForceBuyPrePass implies snapshot/reducer
                // non-null, so accountIndex is guaranteed set here too) -
                // see PlanSolveContext.UnreducedTree's own doc comment.
                UnreducedTree = useForceBuyPrePass ? tree : null,
                AccountItems = useForceBuyPrePass ? snapshot.Items : null,
                ActiveCharacterName = useForceBuyPrePass ? activeCharacterName : null
            };
            sw.Stop();
            timingLog.Add($"Build result: {sw.ElapsedMilliseconds}ms");

            // Prepend timing log to debug entries from PlanResultBuilder -
            // see FinishTimingLog's own doc comment.
            FinishTimingLog(result, timingLog);
            phaseTracker.Finish();

            return result;
        }

        /// <summary>
        /// M35-B1 (gw2efficiency parity - multi-item plans): generates a
        /// combined plan for N requested items in one calculation. A
        /// single-entry list delegates STRAIGHT to the untouched single-
        /// item overload above - byte-identical output, no wrapper built at
        /// all - echoing gw2e's own `if (r.length === 1) return r[0]`
        /// short-circuit (docs/gw2e-parity-spec.md, the M34 r1 multi-item
        /// research report). For 2+ items, builds the synthetic wrapper
        /// tree (see RecipeService.BuildMultiItemTreeAsync) and feeds it
        /// through the SAME reduction/force-buy-pre-pass/solve/vendor-
        /// batch-finalization pipeline a single item uses - merged
        /// shopping-list/steps/currency totals across shared materials fall
        /// out of the existing per-item-id aggregation for free (see
        /// PlanSolver.Collect's AggregateStep), with zero multi-item-
        /// specific solver code.
        /// </summary>
        public async Task<CraftingPlanResult> GenerateStructuredAsync(
            IReadOnlyList<PlanRequestItem> items,
            AccountSnapshot snapshot,
            CancellationToken ct,
            IProgress<PlanStatus> progress = null,
            string activeCharacterName = null,
            PriceBasis priceBasis = PriceBasis.BuyOrder,
            CurrencyValuation currencyValuation = null,
            OwnMaterialsMode ownMaterialsMode = OwnMaterialsMode.Free,
            HomesteadEfficiencyTiers homesteadTiers = null,
            // W3B: live coarse-phase events for CraftingPlanView's status
            // strip - see the single-item overload's matching parameter
            // (PlanPhaseEvent's own doc comment). Optional/default null so
            // every existing caller (Module.cs, every pipeline test) is
            // unaffected.
            IProgress<PlanPhaseEvent> phaseProgress = null,
            // W3B: best-effort "name x quantity[, name x quantity...]"
            // label for the Info start/finish log lines below (e.g. "Orrax
            // Manifested x1") - supplied by CraftingPlanView from its own
            // already-resolved item-row selection, so no extra network
            // round trip is needed to know item names here. Null/empty
            // falls back to the pre-W3B "(N items)" wording, e.g. for a
            // caller that bypasses the view (every pipeline test, a future
            // non-UI caller).
            string requestLabel = null,
            // W3C review-fix (mustFix): see the single-item overload's
            // matching parameter doc comment - threaded through to whichever
            // branch below actually runs (single-item short-circuit or the
            // genuine multi-item path).
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines = null)
        {
            // Marked async (rather than returning the branch Tasks directly)
            // so this validation throws INSIDE the returned Task, exactly
            // like every other failure mode of this method - a caller that
            // awaits (rather than merely calls) this method sees consistent
            // exception delivery regardless of which branch below is taken.
            if (items == null || items.Count == 0)
            {
                throw new ArgumentException("At least one plan request item is required.", nameof(items));
            }

            // M39 (log system, d2-log-system.md Section 8's last row): NEW
            // plan-lifecycle events, not a migration of an existing call -
            // this is the one entry point Module.cs's own generateAsync
            // lambda actually calls (a single-entry list short-circuits to
            // the untouched single-item overload below, exactly as before -
            // see that overload's own doc comment), so wrapping ONLY this
            // thin dispatcher covers every real call site without touching
            // either branch's internals - deliberately scoped this way so
            // it does not collide with WP-13's planned extraction of shared
            // helpers across the Generate*Async overloads (tab-roadmap-
            // proposal.md Section 2.3's sequencing note).
            //
            // W3B: `label` upgrades the wording from "(N items)" to real
            // item names whenever the caller supplied requestLabel - see
            // that parameter's own doc comment for the fallback.
            var sw = Stopwatch.StartNew();
            string itemWord = items.Count == 1 ? "item" : "items";
            string label = string.IsNullOrEmpty(requestLabel) ? $"{items.Count} {itemWord}" : requestLabel;
            _moduleLog.Write(ModuleLogLevel.Info, "plan", $"Generating plan for {label}");

            try
            {
                CraftingPlanResult result;
                if (items.Count == 1)
                {
                    result = await GenerateStructuredAsync(
                        items[0].ItemId, items[0].Quantity, snapshot, ct, progress,
                        activeCharacterName, priceBasis, currencyValuation, ownMaterialsMode,
                        homesteadTiers, phaseProgress, characterDisciplines: characterDisciplines);
                }
                else
                {
                    result = await GenerateStructuredMultiAsync(
                        items, snapshot, ct, progress, activeCharacterName,
                        priceBasis, currencyValuation, ownMaterialsMode, homesteadTiers,
                        phaseProgress, characterDisciplines: characterDisciplines);
                }

                // W3B: compact per-phase summary line, derived from the raw
                // timing lines FinishTimingLog already prepended to
                // result.DebugLog inside the single/multi method just
                // called - see PlanPhaseTimingSummary's own doc comment for
                // why no separate timing plumbing is needed between here
                // and there. W3B review-fix: sw (this wrapper's own
                // Stopwatch, running since before the single/multi call)
                // is now passed through as wallClockMs - the phase-sum-only
                // "total" this used to log excludes every un-instrumented
                // gap between steps, so it silently under-reported the
                // real duration a field tester actually experiences; see
                // FormatCompactSummary's own doc comment.
                string phaseSummary = PlanPhaseTimingSummary.FormatCompactSummary(result?.DebugLog, sw.ElapsedMilliseconds);
                _moduleLog.Write(ModuleLogLevel.Info, "plan",
                    string.IsNullOrEmpty(phaseSummary)
                        ? $"Generation finished in {sw.ElapsedMilliseconds}ms"
                        : $"Plan for {label}: {phaseSummary}");
                return result;
            }
            catch (OperationCanceledException)
            {
                _moduleLog.Write(ModuleLogLevel.Info, "plan", $"Generation cancelled after {sw.ElapsedMilliseconds}ms ({label})");
                throw;
            }
            catch (Exception ex)
            {
                _moduleLog.Write(ModuleLogLevel.Warn, "plan", $"Generation failed after {sw.ElapsedMilliseconds}ms ({label}): {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// The genuine (2+ item) multi-item path behind the list overload
        /// of GenerateStructuredAsync above. Mirrors the single-item
        /// overload's own pipeline step-for-step (reduction, M34-B2a #3's
        /// force-buy pre-pass, solve, vendor-batch finalization, metadata
        /// fetch, structured result build) with the wrapper tree standing
        /// in for a single item's tree throughout - PlanSolver,
        /// InventoryReducer, and OwnedMaterialsForceBuyPrePass are all
        /// oblivious to the wrapper's presence (see their own doc comments)
        /// so none of that logic needed to change.
        ///
        /// M37 (gw2efficiency parity - multi-item sell-side economics,
        /// closes KNOWN-ISSUES #25): calls
        /// SellSideEconomics.ApplyBatchSellSideEconomics
        /// (Services/SellSideEconomics.cs) to populate
        /// SellableQuantity/NetSaleValue/CraftingProfit/
        /// MaterialOpportunityCost as a sum across every requested root
        /// that has a live TP sell price - see that method's own doc
        /// comment for the exact aggregation and its deliberate
        /// divergences from gw2e's own multi-item rollup.
        /// </summary>
        private async Task<CraftingPlanResult> GenerateStructuredMultiAsync(
            IReadOnlyList<PlanRequestItem> items,
            AccountSnapshot snapshot,
            CancellationToken ct,
            IProgress<PlanStatus> progress,
            string activeCharacterName,
            PriceBasis priceBasis,
            CurrencyValuation currencyValuation,
            OwnMaterialsMode ownMaterialsMode,
            HomesteadEfficiencyTiers homesteadTiers,
            // W3B: see the single-item overload's matching parameter.
            IProgress<PlanPhaseEvent> phaseProgress,
            // W3C review-fix (mustFix): see the single-item overload's
            // matching parameter doc comment.
            IReadOnlyList<SnapshotCharacterDiscipline> characterDisciplines = null)
        {
            var valuation = currencyValuation ?? CurrencyValuation.None;
            var tiers = homesteadTiers ?? HomesteadEfficiencyTiers.Default;
            var sw = new Stopwatch();
            var timingLog = new List<string>();
            var phaseTracker = new PhaseTracker(phaseProgress, _moduleLog);

            // Step 1: Build each item's own tree, then wrap them under the
            // synthetic multi-item root (RecipeService.BuildMultiItemTreeAsync).
            // W3B review-fix: see the single-item overload's matching call
            // site for why FirstRunTreeHint/OnStatusUpdate's ModuleLog
            // write were added.
            phaseTracker.Start(PlanPhase.BuildingTree, "Building recipe tree", null, FirstRunTreeHint);
            progress?.Report(new PlanStatus
            {
                Message = $"Building recipe trees ({FirstRunTreeHint})..."
            });
            _recipeService.OnStatusUpdate = msg =>
            {
                progress?.Report(new PlanStatus { Message = msg });
                _moduleLog.Write(ModuleLogLevel.Info, "plan", msg);
            };
            sw.Restart();
            RecipeNode tree;
            try
            {
                tree = await _recipeService.BuildMultiItemTreeAsync(items, ct);
            }
            finally
            {
                _recipeService.OnStatusUpdate = null;
            }
            sw.Stop();
            timingLog.Add($"Build recipe trees: {sw.ElapsedMilliseconds}ms ({items.Count} items)");

            // M37 (KNOWN-ISSUES #26): same unconditional pre-pass as the
            // single-item path, applied to the whole wrapper tree at once -
            // an achievement-bit ingredient nested under one requested item
            // can coexist with a plain occurrence of the same id under a
            // DIFFERENT requested item, which only the merged wrapper tree
            // can see (see the class's own doc comment and
            // MultiItemPlanTests' dedicated coverage of exactly this case).
            AchievementBitDedupPrePass.Apply(tree);

            // Step 2: Collect all item IDs from the tree for price lookup
            progress?.Report(new PlanStatus { Message = "Collecting item IDs..." });
            sw.Restart();
            var allItemIds = new HashSet<int>();
            CollectItemIds(tree, allItemIds);
            sw.Stop();
            timingLog.Add($"Collect item IDs: {sw.ElapsedMilliseconds}ms ({allItemIds.Count} items)");

            // Step 3: Fetch TP prices
            phaseTracker.Start(PlanPhase.FetchingPrices, "Fetching prices", allItemIds.Count);
            progress?.Report(new PlanStatus
            {
                Message = $"Fetching prices ({allItemIds.Count} items)...",
                Total = allItemIds.Count
            });
            sw.Restart();
            var prices = await _tradingPostService.GetPricesAsync(allItemIds, ct);
            sw.Stop();
            timingLog.Add($"Fetch TP prices: {sw.ElapsedMilliseconds}ms ({allItemIds.Count} items)");

            // Step 4: Query vendor offers, then price any vendor-only cost items
            var vendorContext = await FetchPricedVendorContextAsync(
                allItemIds, prices, progress, sw, timingLog, ct);
            var vendorOffers = vendorContext.VendorOffers;
            prices = vendorContext.Prices;

            // M34-B2a #3: same force-buy pre-pass as the single-item path,
            // applied to the WHOLE wrapper batch at once.
            bool useForceBuyPrePass = ownMaterialsMode == OwnMaterialsMode.Valued &&
                snapshot != null && _reducer != null;

            if (useForceBuyPrePass)
            {
                RecipeNodeIds.Assign(tree);
            }

            // Step 5.5/5.6 (M34-B2a #3 / VOM design Candidate A - review-fix:
            // merged into one `if` block, was two adjacent identical
            // `if (useForceBuyPrePass)` blocks): see the single-item
            // overload's matching block for the full rationale - same
            // force-buy pre-pass, same zero-owned decision pass, same WHOLE
            // wrapper batch at once, moved ahead of Step 6 so its output can
            // guide InventoryReducer.Reduce below.
            ISet<int> forceBuyOnlyNodeIds = null;
            IReadOnlyDictionary<int, SolverDecision> zeroOwnedDecisions = null;
            if (useForceBuyPrePass)
            {
                forceBuyOnlyNodeIds = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                    _solver, tree, prices, vendorOffers, priceBasis, valuation);

                var zeroOwnedSolve = _solver.Solve(
                    tree, prices, vendorOffers, priceBasis,
                    overrides: null, currencyValuation: valuation,
                    forceBuyOnlyNodeIds: forceBuyOnlyNodeIds,
                    homesteadTiers: tiers);
                zeroOwnedDecisions = zeroOwnedSolve.Decisions;
            }

            // Step 6: Inventory reduction
            phaseTracker.Start(PlanPhase.SolvingDecisions, "Solving decisions", null);
            progress?.Report(new PlanStatus { Message = "Reducing inventory..." });
            sw.Restart();
            RecipeNode treeUsedForSolve = tree;
            List<UsedMaterial> usedMaterials = null;
            Dictionary<RecipeNode, int> ownedQuantityUsedByNode = null;
            // VOM finding #1 fix: see the single-item overload's matching
            // declaration for why this is hoisted out of the `if` below.
            AccountItemIndex accountIndex = null;

            if (snapshot != null && _reducer != null)
            {
                accountIndex = new AccountItemIndex(snapshot.Items);
                var reduced = _reducer.Reduce(tree, accountIndex, activeCharacterName, zeroOwnedDecisions);
                treeUsedForSolve = reduced.ReducedTree;
                usedMaterials = reduced.UsedMaterials;
                ownedQuantityUsedByNode = reduced.OwnedQuantityUsedByNode;
            }
            sw.Stop();
            timingLog.Add($"Inventory reduction: {sw.ElapsedMilliseconds}ms");

            // Step 7: Solve. The wrapper tree is fed through exactly like a
            // single item's tree - see PlanSolver.Collect's own doc comment
            // for how the wrapper's own throwaway "craft" is hidden from
            // the resulting plan/steps.
            progress?.Report(new PlanStatus { Message = "Solving crafting plan..." });
            sw.Restart();
            var solveResult = _solver.Solve(
                treeUsedForSolve, prices, vendorOffers, priceBasis,
                overrides: null, currencyValuation: valuation,
                forceBuyOnlyNodeIds: forceBuyOnlyNodeIds,
                assignNodeIds: !useForceBuyPrePass,
                homesteadTiers: tiers);
            var plan = solveResult.Plan;
            sw.Stop();
            timingLog.Add($"Solve: {sw.ElapsedMilliseconds}ms");

            IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId =
                BuildOwnedQuantityUsedByNodeId(ownedQuantityUsedByNode);

            // Step 8: Fetch item metadata for every tree item + every
            // requested item + used materials.
            var metadataIds = new HashSet<int>(allItemIds);
            metadataIds.UnionWith(plan.Steps.Select(s => s.ItemId));
            foreach (var item in items)
            {
                metadataIds.Add(item.ItemId);
            }
            if (usedMaterials != null)
            {
                foreach (var um in usedMaterials)
                {
                    metadataIds.Add(um.ItemId);
                }
            }
            // W4B: see the single-item overload's matching call for why.
            AddVendorItemComponentIds(solveResult.Decisions, metadataIds);
            // W4B review-fix (Must Fix): see the single-item overload's
            // matching call site (AddAllVendorOfferItemComponentIds' own
            // doc comment).
            AddAllVendorOfferItemComponentIds(vendorOffers, metadataIds);
            phaseTracker.Start(PlanPhase.FetchingItemDetails, "Fetching item details", metadataIds.Count);
            progress?.Report(new PlanStatus
            {
                Message = $"Fetching item details ({metadataIds.Count} items)...",
                Total = metadataIds.Count
            });
            sw.Restart();

            // Kick off the decorative currency-metadata fetch now, in
            // parallel with item metadata, rather than sequentially after
            // it - the service has its own internal timeout (see
            // CurrencyMetadataService), so a hung /v2/currencies can no
            // longer add to the plan-generation critical path. Observed
            // independently of the await below so a fault is never left
            // unobserved if item metadata throws first.
            var currencyTask = _currencyMetadataService?.GetAllAsync(ct);
            ObserveFault(currencyTask);

            var metadata = await _itemMetadataService.GetMetadataAsync(metadataIds, ct);
            sw.Stop();
            timingLog.Add($"Fetch item metadata: {sw.ElapsedMilliseconds}ms ({metadataIds.Count} items)");

            // Await the currency name/icon metadata fetch started above -
            // see AwaitCurrencyMetadataOrNullAsync's own doc comment.
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata =
                await AwaitCurrencyMetadataOrNullAsync(currencyTask, progress, sw, timingLog, ct);

            // Step 10: Fetch learned recipe IDs (if permission available) -
            // see FetchLearnedRecipeIdsAsync's own doc comment.
            ISet<int> learnedRecipeIds =
                await FetchLearnedRecipeIdsAsync(progress, sw, timingLog, ct);

            // Step 11: Build structured result
            phaseTracker.Start(PlanPhase.BuildingDisplay, "Building display", null);
            progress?.Report(new PlanStatus { Message = "Building final result..." });
            sw.Restart();
            var resultBuilder = new PlanResultBuilder();
            // W3C: per-character discipline data, cosmetic only (see
            // AccountSnapshot.CharacterDisciplines' doc comment) - see the
            // single-item GenerateStructuredAsync's matching assignment
            // above for the full rationale, including the Build()
            // tiebreak-only use.
            // W3C review-fix (mustFix): see the single-item overload's
            // matching effectiveCharacterDisciplines computation.
            var effectiveCharacterDisciplines = characterDisciplines ?? snapshot?.CharacterDisciplines;
            var result = resultBuilder.Build(
                plan, treeUsedForSolve, metadata, usedMaterials, learnedRecipeIds, effectiveCharacterDisciplines);
            result.CurrencyMetadata = currencyMetadata;
            result.AcquisitionHints = _acquisitionHints;
            result.RequestedItems = items;
            result.CharacterDisciplines = effectiveCharacterDisciplines;

            // W4B review-fix (Must Fix): also pass vendorOffers - see
            // BuildOwnedCurrencyAmounts' own doc comment for why.
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts =
                BuildOwnedCurrencyAmounts(snapshot, plan.CurrencyCosts, vendorOffers);
            result.OwnedCurrencyAmounts = ownedCurrencyAmounts;

            // W4B: see the single-item overload's matching computation.
            // W4B review-fix (Must Fix): also pass vendorOffers - see that
            // method's own doc comment for why.
            IReadOnlyDictionary<int, int> ownedVendorItemAmounts =
                BuildOwnedVendorItemComponentAmounts(snapshot, solveResult.Decisions, vendorOffers);

            BuildCraftingTreeResult(
                result, treeUsedForSolve, solveResult.Decisions, metadata,
                _acquisitionHints, ownedQuantityUsedByNodeId, ignoredItemIds: null,
                currencyMetadata: currencyMetadata, ownedCurrencyAmounts: ownedCurrencyAmounts,
                ownedVendorItemAmounts: ownedVendorItemAmounts);

            SellSideEconomics.ApplyBatchSellSideEconomics(
                result, treeUsedForSolve, solveResult, prices, items,
                priceBasis, usedMaterials, ownMaterialsMode);

            result.SolveContext = new PlanSolveContext
            {
                TargetItemId = Gw2Constants.MultiItemWrapperItemId,
                Quantity = 1,
                Tree = treeUsedForSolve,
                Prices = prices,
                VendorOffers = vendorOffers,
                Metadata = metadata,
                LearnedRecipeIds = learnedRecipeIds,
                UsedMaterials = usedMaterials,
                PriceBasis = priceBasis,
                CurrencyValuation = valuation,
                OwnMaterialsMode = ownMaterialsMode,
                CurrencyMetadata = currencyMetadata,
                AcquisitionHints = _acquisitionHints,
                OwnedQuantityUsedByNodeId = ownedQuantityUsedByNodeId,
                OwnedCurrencyAmounts = ownedCurrencyAmounts,
                OwnedVendorItemAmounts = ownedVendorItemAmounts,
                ForceBuyOnlyNodeIds = forceBuyOnlyNodeIds,
                RequestedItems = items,
                HomesteadTiers = tiers,
                CharacterDisciplines = result.CharacterDisciplines,
                // VOM finding #1 fix: see the single-item overload's
                // matching assignment (PlanSolveContext.UnreducedTree's own
                // doc comment).
                UnreducedTree = useForceBuyPrePass ? tree : null,
                AccountItems = useForceBuyPrePass ? snapshot.Items : null,
                ActiveCharacterName = useForceBuyPrePass ? activeCharacterName : null
            };
            sw.Stop();
            timingLog.Add($"Build result: {sw.ElapsedMilliseconds}ms");

            // See FinishTimingLog's own doc comment.
            FinishTimingLog(result, timingLog);
            phaseTracker.Finish();

            return result;
        }

        /// <summary>
        /// Re-solves a previously generated plan with per-node decision
        /// overrides. Purely local: reuses the context's tree, prices,
        /// offers, and metadata; no network calls.
        ///
        /// W4B review-fix (Must Fix): because this never re-fetches
        /// metadata, context.Metadata must already cover every id a
        /// possible override could surface - including a vendor cost-
        /// component ITEM leaf on an offer that was NOT the baseline
        /// winner (e.g. a node whose original decision was Craft, manually
        /// overridden here to BuyFromVendor). The generation-time callers
        /// (GenerateStructuredAsync/GenerateStructuredMultiAsync) widen
        /// their metadata fetch for exactly this via
        /// AddAllVendorOfferItemComponentIds - see that method's own doc
        /// comment - rather than this method fetching anything itself. The
        /// same is true of context.OwnedCurrencyAmounts/
        /// OwnedVendorItemAmounts below (line 875/887 reuse them verbatim,
        /// never recomputed here): the generation-time callers already
        /// widen BOTH via BuildOwnedCurrencyAmounts(..., vendorOffers) and
        /// BuildOwnedVendorItemComponentAmounts(..., vendorOffers) so a
        /// component leaf surfaced only by an override still gets a correct
        /// HAVE pill - see each method's own doc comment.
        /// </summary>
        public CraftingPlanResult ResolveWithOverrides(
            PlanSolveContext context,
            IReadOnlyDictionary<int, AcquisitionSource> overrides,
            // M34-B2b (gw2e "Ignore" pill): item ids the user has manually
            // marked "fully in-hand" for this session, re-applied on every
            // local re-solve the same way `overrides` is - see
            // PlanSolver.Solve's ignoredItemIds parameter. Not part of
            // PlanSolveContext: unlike ForceBuyOnlyNodeIds (computed once at
            // GENERATION time), this is live session state supplied fresh by
            // the caller on every re-solve, exactly like `overrides` itself.
            ISet<int> ignoredItemIds = null)
        {
            // VOM finding #1 fix: context.Tree/UsedMaterials/
            // OwnedQuantityUsedByNodeId were reduced at GENERATION time
            // using a guide keyed to the ZERO-OWNED decision at each node
            // (see InventoryReducer.ReduceNode's doc comment) - a node the
            // force-buy pre-pass flagged Buy therefore never discounted its
            // own ingredient subtree. Replaying `overrides` against that
            // frozen tree is correct for every node whose decision did NOT
            // change, but silently wrong the moment an override flips a
            // force-buy-flagged node to Craft: its ingredients are still
            // priced at the full, un-owned cost even though real owned
            // stock exists. When context.UnreducedTree is set (the pre-pass
            // ran at generation time - see that field's own doc comment),
            // re-run the SAME zero-owned-decision-pass-then-reduce dance
            // Step 5.6/6 used at generation, but this time with `overrides`
            // (and `ignoredItemIds`) applied to the decision pass, so the
            // guide - and therefore which branch may discount - stays in
            // sync with whatever the user actually picked. Falls back to
            // the frozen context.Tree/UsedMaterials verbatim (today's exact
            // behavior) whenever no pre-pass ran, since there is then
            // nothing to re-guide (context.Tree is already the tree the
            // legacy heuristic reduced, and it is already correct).
            RecipeNode solveTree = context.Tree;
            List<UsedMaterial> usedMaterials = context.UsedMaterials;
            IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId = context.OwnedQuantityUsedByNodeId;

            // Defensive: UnreducedTree is only ever set alongside a reducer
            // at generation time (see PlanSolveContext.UnreducedTree's doc
            // comment), but guard against a mismatched pipeline instance
            // (context generated by one CraftingPlanPipeline, resolved
            // against another with no _reducer wired up) rather than NRE.
            if (context.UnreducedTree != null && _reducer != null)
            {
                var guideSolve = _solver.Solve(
                    context.UnreducedTree, context.Prices, context.VendorOffers,
                    context.PriceBasis, overrides, context.CurrencyValuation,
                    forceBuyOnlyNodeIds: context.ForceBuyOnlyNodeIds,
                    assignNodeIds: false,
                    ignoredItemIds: ignoredItemIds,
                    homesteadTiers: context.HomesteadTiers);

                var accountIndex = new AccountItemIndex(context.AccountItems);
                var reduced = _reducer.Reduce(
                    context.UnreducedTree, accountIndex, context.ActiveCharacterName,
                    guideSolve.Decisions);

                solveTree = reduced.ReducedTree;
                usedMaterials = reduced.UsedMaterials;
                ownedQuantityUsedByNodeId = BuildOwnedQuantityUsedByNodeId(reduced.OwnedQuantityUsedByNode);
            }

            // M34-B2a #3: reapply the SAME force-buy pre-pass result the
            // original generation computed, so a local per-node override
            // re-solve doesn't silently forget it for every other node - a
            // manual override in `overrides` still always wins (see
            // PlanSolver.Evaluate). assignNodeIds:false: solveTree's nodes
            // already carry stable ids from the original generation's own
            // Solve() call (whether freshly assigned there, or pre-assigned/
            // preserved for the force-buy pre-pass - see RecipeNodeIds), and
            // (when re-reduced above) InventoryReducer.CloneNode preserves
            // those same ids onto the fresh clone - reassigning again here
            // would either be a harmless no-op (the common case) or, when
            // the pre-pass ran, would renumber the tree's already-pruned/
            // non-contiguous ids from scratch and desync them from
            // forceBuyOnlyNodeIds' keys.
            var solveResult = _solver.Solve(
                solveTree, context.Prices, context.VendorOffers,
                context.PriceBasis, overrides, context.CurrencyValuation,
                forceBuyOnlyNodeIds: context.ForceBuyOnlyNodeIds,
                assignNodeIds: false,
                ignoredItemIds: ignoredItemIds,
                homesteadTiers: context.HomesteadTiers);

            var resultBuilder = new PlanResultBuilder();
            var result = resultBuilder.Build(
                solveResult.Plan, solveTree, context.Metadata,
                usedMaterials, context.LearnedRecipeIds,
                context.CharacterDisciplines);
            result.CurrencyMetadata = context.CurrencyMetadata;
            result.AcquisitionHints = context.AcquisitionHints;
            result.OwnedCurrencyAmounts = context.OwnedCurrencyAmounts;
            result.RequestedItems = context.RequestedItems;
            // W3C: per-character discipline data, cosmetic only - carried
            // forward from the generation-time context so a local override
            // re-solve keeps showing it (see PlanSolveContext.
            // CharacterDisciplines' doc comment).
            result.CharacterDisciplines = context.CharacterDisciplines;

            BuildCraftingTreeResult(
                result, solveTree, solveResult.Decisions, context.Metadata,
                context.AcquisitionHints, ownedQuantityUsedByNodeId, ignoredItemIds,
                currencyMetadata: context.CurrencyMetadata, ownedCurrencyAmounts: context.OwnedCurrencyAmounts,
                ownedVendorItemAmounts: context.OwnedVendorItemAmounts);

            // M37 (closes KNOWN-ISSUES #25): a local override/Ignore
            // re-solve must recompute whichever sell-side economics the
            // original generation used - single-item ApplySellSideEconomics
            // for a single-item context, or the M37 batch equivalent for a
            // multi-item context - so the Total Cost section's sell/profit
            // rows stay live across re-solves exactly like every other part
            // of the plan already does.
            if (context.Tree.Id != Gw2Constants.MultiItemWrapperItemId)
            {
                SellSideEconomics.ApplySellSideEconomics(
                    result, solveTree, solveResult, context.Prices,
                    context.TargetItemId, context.Quantity, context.PriceBasis,
                    usedMaterials, context.OwnMaterialsMode);
            }
            else
            {
                SellSideEconomics.ApplyBatchSellSideEconomics(
                    result, solveTree, solveResult, context.Prices,
                    context.RequestedItems, context.PriceBasis,
                    usedMaterials, context.OwnMaterialsMode);
            }
            result.SolveContext = context;

            if (result.DebugLog == null)
            {
                result.DebugLog = new List<string>();
            }
            result.DebugLog.Insert(0,
                $"Local re-solve with {overrides?.Count ?? 0} override(s), {ignoredItemIds?.Count ?? 0} ignored item(s)");

            return result;
        }

        /// <summary>Vendor offers for a request, paired with vendor-augmented prices.</summary>
        private readonly struct PricedVendorContext
        {
            public PricedVendorContext(
                IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
                IReadOnlyDictionary<int, ItemPrice> prices)
            {
                VendorOffers = vendorOffers;
                Prices = prices;
            }

            public IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> VendorOffers { get; }

            public IReadOnlyDictionary<int, ItemPrice> Prices { get; }
        }

        /// <summary>
        /// Queries vendor offers for the given item ids, then augments prices for
        /// vendor-only cost items not covered by the recipe-tree price fetch (see
        /// AugmentWithVendorCostPricesAsync).
        /// </summary>
        private async Task<PricedVendorContext> FetchPricedVendorContextAsync(
            HashSet<int> allItemIds,
            IReadOnlyDictionary<int, ItemPrice> prices,
            IProgress<PlanStatus> progress,
            Stopwatch sw,
            List<string> timingLog,
            CancellationToken ct)
        {
            progress?.Report(new PlanStatus { Message = "Looking up vendor offers..." });
            sw.Restart();
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers = null;
            if (_vendorOfferStore != null)
            {
                vendorOffers = _vendorOfferStore.GetOffersForItems(allItemIds);
            }
            sw.Stop();
            timingLog.Add($"Query vendor offers: {sw.ElapsedMilliseconds}ms");

            var mergedPrices = await AugmentWithVendorCostPricesAsync(prices, vendorOffers, ct);
            return new PricedVendorContext(vendorOffers, mergedPrices);
        }

        /// <summary>
        /// Awaits the currency-metadata fetch started earlier. Null task or any
        /// non-cancellation failure yields null (currency rows fall back to
        /// text-only formatting via PlanViewModelBuilder's Gw2Constants fallback).
        /// </summary>
        private static async Task<IReadOnlyDictionary<int, CurrencyMetadata>> AwaitCurrencyMetadataOrNullAsync(
            Task<IReadOnlyDictionary<int, CurrencyMetadata>> currencyTask,
            IProgress<PlanStatus> progress,
            Stopwatch sw,
            List<string> timingLog,
            CancellationToken ct)
        {
            progress?.Report(new PlanStatus { Message = "Fetching currency details..." });
            sw.Restart();
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata = null;
            if (currencyTask != null)
            {
                try
                {
                    currencyMetadata = await currencyTask;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    currencyMetadata = null;
                }
            }
            sw.Stop();
            timingLog.Add($"Fetch currency metadata: {sw.ElapsedMilliseconds}ms");
            return currencyMetadata;
        }

        /// <summary>
        /// Fetches learned recipe ids if the account client is wired up and
        /// permitted. KNOWN-ISSUES api-degradation F4: any non-cancellation
        /// failure degrades to null, a state PlanResultBuilder already treats
        /// as supported rather than discarding an otherwise-priced plan.
        /// </summary>
        private async Task<ISet<int>> FetchLearnedRecipeIdsAsync(
            IProgress<PlanStatus> progress,
            Stopwatch sw,
            List<string> timingLog,
            CancellationToken ct)
        {
            progress?.Report(new PlanStatus { Message = "Checking learned recipes..." });
            sw.Restart();
            ISet<int> learnedRecipeIds = null;
            if (_accountRecipeClient != null && _accountRecipeClient.HasRequiredPermission())
            {
                try
                {
                    learnedRecipeIds = await _accountRecipeClient.GetLearnedRecipeIdsAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    learnedRecipeIds = null;
                }
            }
            sw.Stop();
            timingLog.Add($"Fetch learned recipes: {sw.ElapsedMilliseconds}ms");
            return learnedRecipeIds;
        }

        /// <summary>
        /// Prepends the timing log and its PlanTimingAnalyzer summary to
        /// <paramref name="result"/>.DebugLog, initializing the list if needed.
        /// </summary>
        private static void FinishTimingLog(CraftingPlanResult result, List<string> timingLog)
        {
            if (result.DebugLog == null)
            {
                result.DebugLog = new List<string>();
            }
            result.DebugLog.InsertRange(0, timingLog);
            var summary = PlanTimingAnalyzer.Summarize(timingLog);
            result.DebugLog.InsertRange(timingLog.Count, summary);
        }

        /// <summary>
        /// Fetches TP prices for vendor-offer Item cost lines that are not
        /// already priced (they are not recipe-tree items, so the main price
        /// fetch never sees them) and returns a merged price dictionary.
        /// </summary>
        private async Task<IReadOnlyDictionary<int, ItemPrice>> AugmentWithVendorCostPricesAsync(
            IReadOnlyDictionary<int, ItemPrice> prices,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers,
            CancellationToken ct)
        {
            if (vendorOffers == null)
            {
                return prices;
            }

            var costItemIds = new HashSet<int>();
            foreach (var offerList in vendorOffers.Values)
            {
                foreach (var offer in offerList)
                {
                    if (offer.CostLines == null) continue;
                    foreach (var cost in offer.CostLines)
                    {
                        if (string.Equals(cost.Type, "Item", StringComparison.Ordinal) &&
                            !prices.ContainsKey(cost.Id))
                        {
                            costItemIds.Add(cost.Id);
                        }
                    }
                }
            }

            if (costItemIds.Count == 0)
            {
                return prices;
            }

            var costPrices = await _tradingPostService.GetPricesAsync(costItemIds, ct);
            var merged = new Dictionary<int, ItemPrice>(prices.Count + costPrices.Count);
            foreach (var kvp in prices) merged[kvp.Key] = kvp.Value;
            foreach (var kvp in costPrices) merged[kvp.Key] = kvp.Value;
            return merged;
        }

        /// <summary>
        /// Builds an override map forcing <paramref name="source"/> on every
        /// node of the context's solver tree where it is feasible: nodes
        /// with recipes for Craft, nodes priced under the context's basis
        /// for BuyFromTp. Walks the full tree so nodes hidden beneath
        /// bought intermediates are covered in a single pass.
        /// </summary>
        public static Dictionary<int, AcquisitionSource> BuildPresetOverrides(
            PlanSolveContext context, AcquisitionSource source)
        {
            var overrides = new Dictionary<int, AcquisitionSource>();
            CollectPresetOverrides(context.Tree, context, source, overrides);
            return overrides;
        }

        private static void CollectPresetOverrides(
            RecipeNode node,
            PlanSolveContext context,
            AcquisitionSource source,
            Dictionary<int, AcquisitionSource> overrides)
        {
            if (node.IngredientType == "Item")
            {
                bool feasible = false;
                if (source == AcquisitionSource.Craft)
                {
                    // Permissive: the solver ignores forced crafts whose cost
                    // is not fully priceable, so stray entries are harmless.
                    feasible = node.Recipes.Count > 0;
                }
                else if (source == AcquisitionSource.BuyFromTp)
                {
                    feasible = context.Prices != null &&
                               context.Prices.TryGetValue(node.Id, out var price) &&
                               PlanSolver.GetUnitPrice(price, context.PriceBasis) > 0;
                }
                if (feasible)
                {
                    overrides[node.NodeId] = source;
                }
            }

            foreach (var recipe in node.Recipes)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    CollectPresetOverrides(ingredient, context, source, overrides);
                }
            }
        }

        /// <summary>
        /// M35-B1 (gw2e parity, multi-item plans): builds
        /// CraftingPlanResult.CraftingTree (single-item) or MultiItemRoots
        /// (multi-item) from <paramref name="tree"/> - the synthetic
        /// wrapper root (see Gw2Constants.MultiItemWrapperItemId) never
        /// surfaces in either field, echoing gw2efficiency's own
        /// componentTree.html hiding its equivalent fake node
        /// (docs/gw2e-parity-spec.md, the M34 r1 multi-item research
        /// report). Shared by GenerateStructuredMultiAsync and
        /// ResolveWithOverrides so a local override/Ignore re-solve of a
        /// multi-item batch keeps exposing the same N roots on every
        /// re-solve, not just the first generation.
        /// </summary>
        private static void BuildCraftingTreeResult(
            CraftingPlanResult result,
            RecipeNode tree,
            IReadOnlyDictionary<int, SolverDecision> decisions,
            IReadOnlyDictionary<int, ItemMetadata> metadata,
            IReadOnlyDictionary<int, AcquisitionHint> hints,
            IReadOnlyDictionary<int, int> ownedQuantityUsedByNodeId,
            ISet<int> ignoredItemIds,
            // W4B: optional/null-tolerant, threaded straight through to
            // CraftingTreeBuilder.BuildTree - see that method's own doc
            // comment.
            IReadOnlyDictionary<int, CurrencyMetadata> currencyMetadata = null,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts = null,
            IReadOnlyDictionary<int, int> ownedVendorItemAmounts = null)
        {
            var treeBuilder = new CraftingTreeBuilder();

            if (tree.Id == Gw2Constants.MultiItemWrapperItemId)
            {
                var wrapperRecipe = tree.Recipes.FirstOrDefault(
                    r => r.RecipeId == Gw2Constants.MultiItemWrapperRecipeId);
                var roots = new List<CraftingTreeNode>(wrapperRecipe?.Ingredients.Count ?? 0);
                if (wrapperRecipe != null)
                {
                    foreach (var itemRoot in wrapperRecipe.Ingredients)
                    {
                        roots.Add(treeBuilder.BuildTree(
                            itemRoot, decisions, metadata, hints,
                            ownedQuantityUsedByNodeId, ignoredItemIds,
                            currencyMetadata, ownedCurrencyAmounts, ownedVendorItemAmounts));
                    }
                }
                result.CraftingTree = null;
                result.MultiItemRoots = roots;
            }
            else
            {
                result.CraftingTree = treeBuilder.BuildTree(
                    tree, decisions, metadata, hints,
                    ownedQuantityUsedByNodeId, ignoredItemIds,
                    currencyMetadata, ownedCurrencyAmounts, ownedVendorItemAmounts);
                result.MultiItemRoots = null;
            }
        }

        /// <summary>
        /// M34-B2a #1: converts the reference-keyed per-node owned-usage
        /// side channel (see ReducedTreeResult.OwnedQuantityUsedByNode) into
        /// a NodeId-keyed lookup, once the tree's real NodeIds have been
        /// assigned by the Solve() call that just ran on these same node
        /// objects. Null input (no reduction happened) yields an empty,
        /// non-null dictionary so callers never need a null check.
        /// </summary>
        private static IReadOnlyDictionary<int, int> BuildOwnedQuantityUsedByNodeId(
            Dictionary<RecipeNode, int> ownedQuantityUsedByNode)
        {
            var result = new Dictionary<int, int>(ownedQuantityUsedByNode?.Count ?? 0);
            if (ownedQuantityUsedByNode == null)
            {
                return result;
            }
            foreach (var kvp in ownedQuantityUsedByNode)
            {
                result[kvp.Key.NodeId] = kvp.Value;
            }
            return result;
        }

        /// <summary>
        /// M34-B2a #4: owned-currency annotation for the plan's final
        /// currency totals (see AccountCurrencyIndex's doc comment) -
        /// cosmetic only, computed strictly AFTER the plan/solve already
        /// exist, never fed back into them. Null when there is no wallet
        /// snapshot and the plan needs no currency at all, so callers can
        /// treat null as "no data" distinctly from "0 owned".
        ///
        /// W4B review-fix (Must Fix): widened the SAME way
        /// BuildOwnedVendorItemComponentAmounts widens its item id set (see
        /// that method's own doc comment for the full rationale) -
        /// <paramref name="vendorOffers"/> is scanned for every non-coin
        /// Currency cost line on ANY vendor offer for ANY item in the tree,
        /// not just the currency ids that made it into the baseline plan's
        /// aggregated <paramref name="currencyCosts"/>. Without this, a
        /// currency cost-component LEAF surfaced only by a manual override
        /// (a node whose baseline decision was Craft, so its vendor offer's
        /// currency cost lines were never folded into plan.CurrencyCosts)
        /// would show correct name/icon/quantity but no HAVE pill,
        /// permanently, even with a full wallet - the exact sibling of the
        /// item-side gap AddAllVendorOfferItemComponentIds already closes.
        /// Harmless for the pre-existing currency SUMMARY rows
        /// (PlanViewModelBuilder), which only ever look up the ids they
        /// themselves iterate from plan.CurrencyCosts - extra keys in the
        /// returned map are simply never read by that caller.
        /// </summary>
        private static IReadOnlyDictionary<int, int> BuildOwnedCurrencyAmounts(
            AccountSnapshot snapshot, List<CurrencyCost> currencyCosts,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers = null)
        {
            if (snapshot == null)
            {
                return null;
            }

            var currencyIds = new HashSet<int>();
            if (currencyCosts != null)
            {
                foreach (var cc in currencyCosts)
                {
                    currencyIds.Add(cc.CurrencyId);
                }
            }
            AddAllVendorOfferCurrencyComponentIds(vendorOffers, currencyIds);
            if (currencyIds.Count == 0)
            {
                return null;
            }

            var currencyIndex = new AccountCurrencyIndex(snapshot.Wallet);
            var result = new Dictionary<int, int>(currencyIds.Count);
            foreach (var currencyId in currencyIds)
            {
                result[currencyId] = currencyIndex.GetQuantity(currencyId);
            }
            return result;
        }

        /// <summary>
        /// W4B review-fix (Must Fix): currency-side twin of
        /// AddAllVendorOfferItemComponentIds (see that method's own doc
        /// comment for the full "why a decisions-only scan is not enough"
        /// rationale - identical reasoning applies here). Adds every
        /// currency id that appears as a non-coin Currency cost line on any
        /// vendor offer for any item in the tree into
        /// <paramref name="currencyIds"/>, mirroring exactly the
        /// Type=="Currency" / Id != Gw2Constants.CoinCurrencyId / Count > 0
        /// filter VendorBatchSolver.EvaluateVendorOffers itself uses to
        /// decide what counts as a non-coin currency cost line (see that
        /// method's own comments) - so this widened set can only ever
        /// contain ids a real leaf could actually surface. A no-op when no
        /// vendor offer in the tree has any non-coin Currency cost line at
        /// all (the common case).
        /// </summary>
        private static void AddAllVendorOfferCurrencyComponentIds(
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers, HashSet<int> currencyIds)
        {
            if (vendorOffers == null)
            {
                return;
            }
            foreach (var offers in vendorOffers.Values)
            {
                if (offers == null)
                {
                    continue;
                }
                foreach (var offer in offers)
                {
                    if (offer?.CostLines == null)
                    {
                        continue;
                    }
                    foreach (var cost in offer.CostLines)
                    {
                        if (string.Equals(cost.Type, "Currency", StringComparison.Ordinal)
                            && cost.Id != Gw2Constants.CoinCurrencyId
                            && cost.Count > 0)
                        {
                            currencyIds.Add(cost.Id);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// W4B (vendor cost-component leaves): adds every item id that
        /// appears as a TP-valued Item cost line on any winning
        /// BuyFromVendor decision (SolverDecision.VendorItemCosts) into
        /// <paramref name="metadataIds"/> - called before the single bulk
        /// item-metadata fetch each generation path already makes, so
        /// CraftingTreeBuilder's synthesized item-component leaves get a
        /// real name/icon instead of the "Unknown Item"/null fallback (see
        /// CraftingTreeBuilder.ResolveName/ResolveIcon). A no-op when no
        /// decision has any VendorItemCosts at all (the common case).
        /// </summary>
        private static void AddVendorItemComponentIds(
            IReadOnlyDictionary<int, SolverDecision> decisions, HashSet<int> metadataIds)
        {
            if (decisions == null)
            {
                return;
            }
            foreach (var decision in decisions.Values)
            {
                if (decision.VendorItemCosts == null)
                {
                    continue;
                }
                foreach (var line in decision.VendorItemCosts)
                {
                    metadataIds.Add(line.ItemId);
                }
            }
        }

        /// <summary>
        /// W4B review-fix (Must Fix): widens <paramref name="metadataIds"/>
        /// to cover every item id that appears as a TP-valued Item cost
        /// line on ANY vendor offer for ANY item in the tree - not just the
        /// ones on the BASELINE winning decisions AddVendorItemComponentIds
        /// (decisions overload, above) already covers. `ResolveWithOverrides`
        /// is a purely local, no-network re-solve that reuses
        /// PlanSolveContext.Metadata verbatim (see its own doc comment) -
        /// it never re-fetches metadata. Without this, forcing a node to
        /// BuyFromVendor via a manual override at generation time can win a
        /// DIFFERENT offer than the one Evaluate originally picked (e.g. a
        /// node whose baseline decision was Craft, so its vendor offer's
        /// item cost component was never scanned by the decisions-only
        /// overload above) - that offer's item component would render as
        /// "Unknown Item" with no icon, forever, until the plan is fully
        /// regenerated. Scanning every offer for every item that has ANY
        /// vendor offer (using vendorOffers, already fetched for this
        /// generation - no extra network round trip) guarantees every
        /// offer reachable by ANY override, comparable or fallback, already
        /// has its item components' metadata in hand before
        /// ResolveWithOverrides ever runs. A no-op when no vendor offer in
        /// the tree has any Item cost line at all (the common case).
        /// </summary>
        private static void AddAllVendorOfferItemComponentIds(
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers, HashSet<int> metadataIds)
        {
            if (vendorOffers == null)
            {
                return;
            }
            foreach (var offers in vendorOffers.Values)
            {
                if (offers == null)
                {
                    continue;
                }
                foreach (var offer in offers)
                {
                    if (offer?.CostLines == null)
                    {
                        continue;
                    }
                    foreach (var cost in offer.CostLines)
                    {
                        if (string.Equals(cost.Type, "Item", StringComparison.Ordinal))
                        {
                            metadataIds.Add(cost.Id);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// W4B: owned-item annotation for vendor cost-component ITEM leaves
        /// (CraftingTreeNode.ComponentOwnedQuantity), computed strictly
        /// AFTER solving from the account inventory snapshot - the exact
        /// same "cosmetic reconciliation, never fed back into any decision
        /// or total" contract BuildOwnedCurrencyAmounts already has for
        /// currencies (see AccountCurrencyIndex/AccountItemIndex's own doc
        /// comments), just for item components instead of wallet
        /// currencies. Scoped to only the item ids that actually appear as
        /// a vendor Item cost component anywhere in this solve (not every
        /// owned item in the account) - null when there is no snapshot or
        /// no such component anywhere, so callers can treat null as "no
        /// data" distinctly from "0 owned", same as
        /// BuildOwnedCurrencyAmounts.
        ///
        /// W4B review-fix (Must Fix): widened the same way
        /// AddAllVendorOfferItemComponentIds widened the metadata scan
        /// (same commit's own doc comment) - <paramref name="vendorOffers"/>
        /// is scanned for every Item cost line on ANY offer, not just the
        /// BASELINE winning decisions AddVendorItemComponentIds alone
        /// covers. PlanSolveContext.OwnedVendorItemAmounts is, like
        /// Metadata, captured once at generation time and reused verbatim
        /// by ResolveWithOverrides (see its own doc comment) - it is never
        /// recomputed. Without this, a node whose baseline decision was
        /// Craft (so its vendor offer's item cost component was never
        /// scanned by the decisions-only overload), manually overridden to
        /// BuyFromVendor via ResolveWithOverrides, would show its item
        /// component leaf with correct name/icon (metadata already
        /// widened) but NO have pill - permanently - even with the item
        /// sitting in the account, until the whole plan is regenerated.
        /// </summary>
        private static IReadOnlyDictionary<int, int> BuildOwnedVendorItemComponentAmounts(
            AccountSnapshot snapshot, IReadOnlyDictionary<int, SolverDecision> decisions,
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers)
        {
            if (snapshot == null)
            {
                return null;
            }

            var itemIds = new HashSet<int>();
            AddVendorItemComponentIds(decisions, itemIds);
            AddAllVendorOfferItemComponentIds(vendorOffers, itemIds);
            if (itemIds.Count == 0)
            {
                return null;
            }

            var itemIndex = new AccountItemIndex(snapshot.Items);
            var result = new Dictionary<int, int>(itemIds.Count);
            foreach (var itemId in itemIds)
            {
                int total = 0;
                foreach (var source in itemIndex.GetSources(itemId))
                {
                    total += itemIndex.GetQuantity(itemId, source);
                }
                result[itemId] = total;
            }
            return result;
        }

        /// <summary>
        /// Attaches a fire-and-forget continuation that touches Exception
        /// on fault, so a task's failure is always observed even if the
        /// caller's own await of it is skipped (e.g. an earlier awaited
        /// step throws first) - prevents an unobserved task exception at
        /// GC time. Does not change the task's outcome for anyone who does
        /// await it.
        /// </summary>
        private static void ObserveFault(Task task)
        {
            task?.ContinueWith(
                t => { var _ = t.Exception; },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        private static void CollectItemIds(RecipeNode node, HashSet<int> ids)
        {
            // M35: never collect the synthetic multi-item wrapper's own
            // sentinel id (see Gw2Constants.MultiItemWrapperItemId) - it is
            // not a real GW2 item and must never trigger a TP price fetch.
            // The recursion below still walks past it into its recipe's
            // Ingredients (the N real item roots) unaffected.
            if (node.IngredientType == "Item" && node.Id != Gw2Constants.MultiItemWrapperItemId)
            {
                ids.Add(node.Id);
            }

            foreach (var recipe in node.Recipes)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    CollectItemIds(ingredient, ids);
                }
            }
        }

        /// <summary>
        /// W3B (generation progress + rich logging): tracks the 5 coarse,
        /// user-facing phases of one GenerateStructuredAsync/
        /// GenerateStructuredMultiAsync run - fires a live PlanPhaseEvent
        /// when each phase STARTS (for CraftingPlanView's status strip, via
        /// <see cref="Start"/>) and writes one bounded Debug ModuleLog
        /// entry (timing + optional count) when each phase COMPLETES,
        /// detected as either the next phase starting or <see cref="Finish"/>
        /// being called for the last one. Deliberately separate from
        /// timingLog (the existing, much finer-grained ~10-step breakdown
        /// that ends up in CraftingPlanResult.DebugLog via
        /// FinishTimingLog) - that channel is unchanged; this one exists
        /// purely to drive a stable, coarse live indicator without a UI
        /// needing to parse PlanStatus.Message text, and to make the Log
        /// tab show forward progress DURING a long-running generation
        /// rather than only a burst of entries once it is already done.
        /// <para>
        /// Single-threaded, synchronous use only: constructed fresh per
        /// GenerateStructuredAsync call (never shared across concurrent
        /// generations) and driven entirely on whatever thread is running
        /// that call's own async state machine at each await resumption -
        /// never accessed concurrently by two threads at once, matching how
        /// the existing local `timingLog`/`sw` variables in the very same
        /// methods are already used with no locking.
        /// </para>
        /// <para>
        /// If the generation throws/is cancelled mid-phase, the
        /// currently-open phase never gets a completion Debug entry (Finish
        /// is only reached on the success path) - accepted for v1: the
        /// wrapper's own "Generation cancelled/failed" Info/Warn line
        /// already reports elapsed time for that case, and an incomplete
        /// phase leaves no resource to leak (no IDisposable, no external
        /// handle).
        /// </para>
        /// </summary>
        private sealed class PhaseTracker
        {
            private readonly IProgress<PlanPhaseEvent> _phaseProgress;
            private readonly ModuleLog _moduleLog;
            private readonly Stopwatch _sw = new Stopwatch();
            private PlanPhase? _currentPhase;
            private string _currentDisplayName;
            private int? _currentTotal;

            public PhaseTracker(IProgress<PlanPhaseEvent> phaseProgress, ModuleLog moduleLog)
            {
                _phaseProgress = phaseProgress;
                _moduleLog = moduleLog;
            }

            /// <summary>
            /// Completes whatever phase was previously running (if any -
            /// writing its Debug entry), then starts and reports the new
            /// one. <paramref name="total"/> is an item/step count known up
            /// front (e.g. items to price), or null when not applicable -
            /// see PlanPhaseEvent.Total's own doc comment.
            /// <paramref name="detail"/> is an optional short additional
            /// detail (W3B review-fix: currently only the tree-building
            /// phase's first-run hint - see PlanPhaseEvent.Detail's own doc
            /// comment); null for every other call site.
            /// </summary>
            public void Start(PlanPhase phase, string displayName, int? total, string detail = null)
            {
                CompleteCurrent();
                _currentPhase = phase;
                _currentDisplayName = displayName;
                _currentTotal = total;
                _sw.Restart();
                _phaseProgress?.Report(new PlanPhaseEvent
                {
                    Phase = phase,
                    DisplayName = displayName,
                    Total = total,
                    Detail = detail
                });
            }

            /// <summary>
            /// Completes the final phase (writing its Debug entry). Safe to
            /// call even if <see cref="Start"/> was never called (no-op).
            /// </summary>
            public void Finish()
            {
                CompleteCurrent();
            }

            // W3B review-fix (doc-only): this Debug line's ms figure is
            // _sw's elapsed time since THIS phase's own Start() call, up to
            // whichever comes first of the next phase's Start() or Finish()
            // - i.e. WALL time between two consecutive Start() calls,
            // including any un-instrumented gap between the previous
            // phase's actual work ending and this phase's own work
            // beginning. That is a DIFFERENT measurement from the Info
            // "finish" summary line (see PlanPhaseTimingSummary.
            // FormatCompactSummary), which buckets and sums the finer-
            // grained raw timingLog entries recorded around each step's own
            // narrower stopwatched work only, excluding those same gaps -
            // so the same phase can legitimately show two different
            // millisecond figures across the Debug and Info log lines; this
            // Debug figure is the wall-clock one, the Info bucket is the
            // instrumented-work-only one.
            private void CompleteCurrent()
            {
                if (_currentPhase == null)
                {
                    return;
                }

                _sw.Stop();
                long ms = _sw.ElapsedMilliseconds;
                string countSuffix = _currentTotal.HasValue
                    ? $" ({_currentTotal.Value} items)"
                    : string.Empty;
                _moduleLog.Write(ModuleLogLevel.Debug, "plan", $"{_currentDisplayName}: {ms}ms{countSuffix}");
                _currentPhase = null;
            }
        }
    }
}
