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

        public CraftingPlanPipeline(
            RecipeService recipeService,
            TradingPostService tradingPostService,
            PlanSolver solver,
            ItemMetadataService itemMetadataService,
            VendorOfferStore vendorOfferStore = null,
            InventoryReducer reducer = null,
            IAccountRecipeClient accountRecipeClient = null,
            CurrencyMetadataService currencyMetadataService = null,
            IReadOnlyDictionary<int, AcquisitionHint> acquisitionHints = null)
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
            HomesteadEfficiencyTiers homesteadTiers = null)
        {
            var valuation = currencyValuation ?? CurrencyValuation.None;
            var tiers = homesteadTiers ?? HomesteadEfficiencyTiers.Default;
            var sw = new Stopwatch();
            var timingLog = new List<string>();

            // Step 1: Build recipe tree
            progress?.Report(new PlanStatus
            {
                Message = "Building recipe tree (may take several seconds on first run)..."
            });
            _recipeService.OnStatusUpdate = msg =>
                progress?.Report(new PlanStatus { Message = msg });
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

            // Step 6: Inventory reduction
            progress?.Report(new PlanStatus { Message = "Reducing inventory..." });
            sw.Restart();
            RecipeNode treeUsedForSolve = tree;
            List<UsedMaterial> usedMaterials = null;
            Dictionary<RecipeNode, int> ownedQuantityUsedByNode = null;

            if (snapshot != null && _reducer != null)
            {
                var index = new AccountItemIndex(snapshot.Items);
                var reduced = _reducer.Reduce(tree, index, activeCharacterName);
                treeUsedForSolve = reduced.ReducedTree;
                usedMaterials = reduced.UsedMaterials;
                ownedQuantityUsedByNode = reduced.OwnedQuantityUsedByNode;
            }
            sw.Stop();
            timingLog.Add($"Inventory reduction: {sw.ElapsedMilliseconds}ms");

            // Step 6.5 (M34-B2a #3): computed against `tree` - the ORIGINAL,
            // UNREDUCED tree (Reduce above only ever mutates its CLONE, so
            // `tree` still holds the full pre-ownership demand here) -
            // matching gw2e's own zero-owned-baseline mechanics exactly
            // (Section 2.2 of the R2 report): otherwise, evaluating this
            // rule on the ALREADY-reduced tree would make it a near no-op
            // in precisely the scenario it exists for, since owning a pile
            // of components already makes their post-reduction craft cost
            // look cheap regardless of what a FRESH purchase would cost.
            ISet<int> forceBuyOnlyNodeIds = null;
            if (useForceBuyPrePass)
            {
                forceBuyOnlyNodeIds = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                    _solver, tree, prices, vendorOffers, priceBasis, valuation);
            }

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
            progress?.Report(new PlanStatus { Message = "Building final result..." });
            sw.Restart();
            var resultBuilder = new PlanResultBuilder();
            var result = resultBuilder.Build(plan, treeUsedForSolve, metadata, usedMaterials, learnedRecipeIds);
            result.CurrencyMetadata = currencyMetadata;
            result.AcquisitionHints = _acquisitionHints;

            // M34-B2a #4: owned-currency annotation, cosmetic only (see
            // AccountCurrencyIndex's doc comment) - built from the plan's
            // final currency totals and the wallet snapshot, never fed back
            // into any decision/total above.
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts =
                BuildOwnedCurrencyAmounts(snapshot, plan.CurrencyCosts);
            result.OwnedCurrencyAmounts = ownedCurrencyAmounts;

            // Build crafting tree
            var treeBuilder = new CraftingTreeBuilder();
            result.CraftingTree = treeBuilder.BuildTree(
                treeUsedForSolve, solveResult.Decisions, metadata, _acquisitionHints,
                ownedQuantityUsedByNodeId);

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
                ForceBuyOnlyNodeIds = forceBuyOnlyNodeIds,
                HomesteadTiers = tiers
            };
            sw.Stop();
            timingLog.Add($"Build result: {sw.ElapsedMilliseconds}ms");

            // Prepend timing log to debug entries from PlanResultBuilder -
            // see FinishTimingLog's own doc comment.
            FinishTimingLog(result, timingLog);

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
            HomesteadEfficiencyTiers homesteadTiers = null)
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

            if (items.Count == 1)
            {
                return await GenerateStructuredAsync(
                    items[0].ItemId, items[0].Quantity, snapshot, ct, progress,
                    activeCharacterName, priceBasis, currencyValuation, ownMaterialsMode,
                    homesteadTiers);
            }

            return await GenerateStructuredMultiAsync(
                items, snapshot, ct, progress, activeCharacterName,
                priceBasis, currencyValuation, ownMaterialsMode, homesteadTiers);
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
            HomesteadEfficiencyTiers homesteadTiers)
        {
            var valuation = currencyValuation ?? CurrencyValuation.None;
            var tiers = homesteadTiers ?? HomesteadEfficiencyTiers.Default;
            var sw = new Stopwatch();
            var timingLog = new List<string>();

            // Step 1: Build each item's own tree, then wrap them under the
            // synthetic multi-item root (RecipeService.BuildMultiItemTreeAsync).
            progress?.Report(new PlanStatus
            {
                Message = "Building recipe trees (may take several seconds on first run)..."
            });
            _recipeService.OnStatusUpdate = msg =>
                progress?.Report(new PlanStatus { Message = msg });
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

            // Step 6: Inventory reduction
            progress?.Report(new PlanStatus { Message = "Reducing inventory..." });
            sw.Restart();
            RecipeNode treeUsedForSolve = tree;
            List<UsedMaterial> usedMaterials = null;
            Dictionary<RecipeNode, int> ownedQuantityUsedByNode = null;

            if (snapshot != null && _reducer != null)
            {
                var index = new AccountItemIndex(snapshot.Items);
                var reduced = _reducer.Reduce(tree, index, activeCharacterName);
                treeUsedForSolve = reduced.ReducedTree;
                usedMaterials = reduced.UsedMaterials;
                ownedQuantityUsedByNode = reduced.OwnedQuantityUsedByNode;
            }
            sw.Stop();
            timingLog.Add($"Inventory reduction: {sw.ElapsedMilliseconds}ms");

            ISet<int> forceBuyOnlyNodeIds = null;
            if (useForceBuyPrePass)
            {
                forceBuyOnlyNodeIds = OwnedMaterialsForceBuyPrePass.ComputeForceBuyOnlyNodeIds(
                    _solver, tree, prices, vendorOffers, priceBasis, valuation);
            }

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
            progress?.Report(new PlanStatus { Message = "Building final result..." });
            sw.Restart();
            var resultBuilder = new PlanResultBuilder();
            var result = resultBuilder.Build(plan, treeUsedForSolve, metadata, usedMaterials, learnedRecipeIds);
            result.CurrencyMetadata = currencyMetadata;
            result.AcquisitionHints = _acquisitionHints;
            result.RequestedItems = items;

            IReadOnlyDictionary<int, int> ownedCurrencyAmounts =
                BuildOwnedCurrencyAmounts(snapshot, plan.CurrencyCosts);
            result.OwnedCurrencyAmounts = ownedCurrencyAmounts;

            BuildCraftingTreeResult(
                result, treeUsedForSolve, solveResult.Decisions, metadata,
                _acquisitionHints, ownedQuantityUsedByNodeId, ignoredItemIds: null);

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
                ForceBuyOnlyNodeIds = forceBuyOnlyNodeIds,
                RequestedItems = items,
                HomesteadTiers = tiers
            };
            sw.Stop();
            timingLog.Add($"Build result: {sw.ElapsedMilliseconds}ms");

            // See FinishTimingLog's own doc comment.
            FinishTimingLog(result, timingLog);

            return result;
        }

        /// <summary>
        /// Re-solves a previously generated plan with per-node decision
        /// overrides. Purely local: reuses the context's tree, prices,
        /// offers, and metadata; no network calls.
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
            // M34-B2a #3: reapply the SAME force-buy pre-pass result the
            // original generation computed, so a local per-node override
            // re-solve doesn't silently forget it for every other node - a
            // manual override in `overrides` still always wins (see
            // PlanSolver.Evaluate). assignNodeIds:false: context.Tree's
            // nodes already carry stable ids from the original generation's
            // own Solve() call (whether freshly assigned there, or
            // pre-assigned/preserved for the force-buy pre-pass - see
            // RecipeNodeIds) - reassigning again here would either be a
            // harmless no-op (the common case) or, when the pre-pass ran,
            // would renumber the tree's already-pruned/non-contiguous ids
            // from scratch and desync them from forceBuyOnlyNodeIds' keys.
            var solveResult = _solver.Solve(
                context.Tree, context.Prices, context.VendorOffers,
                context.PriceBasis, overrides, context.CurrencyValuation,
                forceBuyOnlyNodeIds: context.ForceBuyOnlyNodeIds,
                assignNodeIds: false,
                ignoredItemIds: ignoredItemIds,
                homesteadTiers: context.HomesteadTiers);

            var resultBuilder = new PlanResultBuilder();
            var result = resultBuilder.Build(
                solveResult.Plan, context.Tree, context.Metadata,
                context.UsedMaterials, context.LearnedRecipeIds);
            result.CurrencyMetadata = context.CurrencyMetadata;
            result.AcquisitionHints = context.AcquisitionHints;
            result.OwnedCurrencyAmounts = context.OwnedCurrencyAmounts;
            result.RequestedItems = context.RequestedItems;

            BuildCraftingTreeResult(
                result, context.Tree, solveResult.Decisions, context.Metadata,
                context.AcquisitionHints, context.OwnedQuantityUsedByNodeId, ignoredItemIds);

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
                    result, context.Tree, solveResult, context.Prices,
                    context.TargetItemId, context.Quantity, context.PriceBasis,
                    context.UsedMaterials, context.OwnMaterialsMode);
            }
            else
            {
                SellSideEconomics.ApplyBatchSellSideEconomics(
                    result, context.Tree, solveResult, context.Prices,
                    context.RequestedItems, context.PriceBasis,
                    context.UsedMaterials, context.OwnMaterialsMode);
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
            ISet<int> ignoredItemIds)
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
                            ownedQuantityUsedByNodeId, ignoredItemIds));
                    }
                }
                result.CraftingTree = null;
                result.MultiItemRoots = roots;
            }
            else
            {
                result.CraftingTree = treeBuilder.BuildTree(
                    tree, decisions, metadata, hints,
                    ownedQuantityUsedByNodeId, ignoredItemIds);
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
        /// snapshot or the plan needs no currency at all, so callers can
        /// treat null as "no data" distinctly from "0 owned".
        /// </summary>
        private static IReadOnlyDictionary<int, int> BuildOwnedCurrencyAmounts(
            AccountSnapshot snapshot, List<CurrencyCost> currencyCosts)
        {
            if (snapshot == null || currencyCosts == null || currencyCosts.Count == 0)
            {
                return null;
            }

            var currencyIndex = new AccountCurrencyIndex(snapshot.Wallet);
            var result = new Dictionary<int, int>(currencyCosts.Count);
            foreach (var cc in currencyCosts)
            {
                result[cc.CurrencyId] = currencyIndex.GetQuantity(cc.CurrencyId);
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
    }
}
