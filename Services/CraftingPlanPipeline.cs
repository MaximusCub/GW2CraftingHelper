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
        private readonly VendorOfferResolver _resolver;
        private readonly InventoryReducer _reducer;
        private readonly IAccountRecipeClient _accountRecipeClient;

        public CraftingPlanPipeline(
            RecipeService recipeService,
            TradingPostService tradingPostService,
            PlanSolver solver,
            ItemMetadataService itemMetadataService,
            VendorOfferStore vendorOfferStore = null,
            VendorOfferResolver resolver = null,
            InventoryReducer reducer = null,
            IAccountRecipeClient accountRecipeClient = null)
        {
            _recipeService = recipeService;
            _tradingPostService = tradingPostService;
            _solver = solver;
            _itemMetadataService = itemMetadataService;
            _vendorOfferStore = vendorOfferStore;
            _resolver = resolver;
            _reducer = reducer;
            _accountRecipeClient = accountRecipeClient;
        }

        public async Task<CraftingPlanResult> GenerateAsync(
            int targetItemId, int quantity, CancellationToken ct,
            IProgress<PlanStatus> progress = null,
            CurrencyValuation currencyValuation = null)
        {
            var valuation = currencyValuation ?? CurrencyValuation.None;
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

            // Step 4: Resolve missing vendor offers (if resolver available)
            progress?.Report(new PlanStatus { Message = "Resolving vendor offers..." });
            sw.Restart();
            if (_resolver != null && _vendorOfferStore != null)
            {
                await _resolver.EnsureVendorOffersAsync(allItemIds, progress, ct);
            }
            sw.Stop();
            timingLog.Add($"Resolve vendor offers: {sw.ElapsedMilliseconds}ms");

            // Step 5: Query vendor offers
            progress?.Report(new PlanStatus { Message = "Looking up vendor offers..." });
            sw.Restart();
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers = null;
            if (_vendorOfferStore != null)
            {
                vendorOffers = _vendorOfferStore.GetOffersForItems(allItemIds);
            }
            sw.Stop();
            timingLog.Add($"Query vendor offers: {sw.ElapsedMilliseconds}ms");

            // Vendor offers can charge ITEMS that appear nowhere in the
            // recipe tree (e.g. Gift of Glory costs 250x Shard of Glory).
            // Without their prices the solver silently skips the offer as
            // unpriceable, so fetch the missing cost-item prices and merge.
            prices = await AugmentWithVendorCostPricesAsync(prices, vendorOffers, ct);

            // Step 6: Solve
            progress?.Report(new PlanStatus { Message = "Solving crafting plan..." });
            sw.Restart();
            var solveResult = _solver.Solve(
                tree, prices, vendorOffers, currencyValuation: valuation);
            sw.Stop();
            timingLog.Add($"Solve: {sw.ElapsedMilliseconds}ms");

            // Step 7: Fetch item metadata for all step items + target + tree items
            var metadataIds = new HashSet<int>(solveResult.Plan.Steps.Select(s => s.ItemId));
            metadataIds.Add(targetItemId);
            CraftingTreeBuilder.CollectTreeItemIds(tree, solveResult.Decisions, metadataIds);
            progress?.Report(new PlanStatus
            {
                Message = $"Fetching item details ({metadataIds.Count} items)...",
                Total = metadataIds.Count
            });
            sw.Restart();
            var metadata = await _itemMetadataService.GetMetadataAsync(metadataIds, ct);
            sw.Stop();
            timingLog.Add($"Fetch item metadata: {sw.ElapsedMilliseconds}ms ({metadataIds.Count} items)");

            // Build crafting tree
            var treeBuilder = new CraftingTreeBuilder();
            var craftingTree = treeBuilder.BuildTree(tree, solveResult.Decisions, metadata);

            var debugLog = new List<string>(timingLog);
            debugLog.AddRange(PlanTimingAnalyzer.Summarize(timingLog));

            return new CraftingPlanResult
            {
                Plan = solveResult.Plan,
                ItemMetadata = metadata,
                CraftingTree = craftingTree,
                DebugLog = debugLog
            };
        }

        public async Task<CraftingPlanResult> GenerateStructuredAsync(
            int targetItemId, int quantity, AccountSnapshot snapshot,
            CancellationToken ct, IProgress<PlanStatus> progress = null,
            string activeCharacterName = null,
            PriceBasis priceBasis = PriceBasis.InstantBuy,
            CurrencyValuation currencyValuation = null,
            OwnMaterialsMode ownMaterialsMode = OwnMaterialsMode.Free)
        {
            var valuation = currencyValuation ?? CurrencyValuation.None;
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

            // Step 4: Resolve missing vendor offers (if resolver available)
            progress?.Report(new PlanStatus { Message = "Resolving vendor offers..." });
            sw.Restart();
            if (_resolver != null && _vendorOfferStore != null)
            {
                await _resolver.EnsureVendorOffersAsync(allItemIds, progress, ct);
            }
            sw.Stop();
            timingLog.Add($"Resolve vendor offers: {sw.ElapsedMilliseconds}ms");

            // Step 5: Query vendor offers
            progress?.Report(new PlanStatus { Message = "Looking up vendor offers..." });
            sw.Restart();
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers = null;
            if (_vendorOfferStore != null)
            {
                vendorOffers = _vendorOfferStore.GetOffersForItems(allItemIds);
            }
            sw.Stop();
            timingLog.Add($"Query vendor offers: {sw.ElapsedMilliseconds}ms");

            // Vendor offers can charge ITEMS that appear nowhere in the
            // recipe tree (e.g. Gift of Glory costs 250x Shard of Glory).
            // Without their prices the solver silently skips the offer as
            // unpriceable, so fetch the missing cost-item prices and merge.
            prices = await AugmentWithVendorCostPricesAsync(prices, vendorOffers, ct);

            // Step 6: Inventory reduction
            progress?.Report(new PlanStatus { Message = "Reducing inventory..." });
            sw.Restart();
            RecipeNode treeUsedForSolve = tree;
            List<UsedMaterial> usedMaterials = null;

            if (snapshot != null && _reducer != null)
            {
                var index = new AccountItemIndex(snapshot.Items);
                var reduced = _reducer.Reduce(tree, index, activeCharacterName);
                treeUsedForSolve = reduced.ReducedTree;
                usedMaterials = reduced.UsedMaterials;
            }
            sw.Stop();
            timingLog.Add($"Inventory reduction: {sw.ElapsedMilliseconds}ms");

            // Step 7: Solve
            progress?.Report(new PlanStatus { Message = "Solving crafting plan..." });
            sw.Restart();
            var solveResult = _solver.Solve(
                treeUsedForSolve, prices, vendorOffers, priceBasis,
                currencyValuation: valuation);
            var plan = solveResult.Plan;
            sw.Stop();
            timingLog.Add($"Solve: {sw.ElapsedMilliseconds}ms");

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
            var metadata = await _itemMetadataService.GetMetadataAsync(metadataIds, ct);
            sw.Stop();
            timingLog.Add($"Fetch item metadata: {sw.ElapsedMilliseconds}ms ({metadataIds.Count} items)");

            // Step 9: Fetch learned recipe IDs (if permission available)
            progress?.Report(new PlanStatus { Message = "Checking learned recipes..." });
            sw.Restart();
            ISet<int> learnedRecipeIds = null;
            if (_accountRecipeClient != null && _accountRecipeClient.HasRequiredPermission())
            {
                learnedRecipeIds = await _accountRecipeClient.GetLearnedRecipeIdsAsync(ct);
            }
            sw.Stop();
            timingLog.Add($"Fetch learned recipes: {sw.ElapsedMilliseconds}ms");

            // Step 10: Build structured result
            progress?.Report(new PlanStatus { Message = "Building final result..." });
            sw.Restart();
            var resultBuilder = new PlanResultBuilder();
            var result = resultBuilder.Build(plan, treeUsedForSolve, metadata, usedMaterials, learnedRecipeIds);

            // Build crafting tree
            var treeBuilder = new CraftingTreeBuilder();
            result.CraftingTree = treeBuilder.BuildTree(treeUsedForSolve, solveResult.Decisions, metadata);

            ApplySellSideEconomics(
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
                OwnMaterialsMode = ownMaterialsMode
            };
            sw.Stop();
            timingLog.Add($"Build result: {sw.ElapsedMilliseconds}ms");

            // Prepend timing log to debug entries from PlanResultBuilder
            if (result.DebugLog == null)
            {
                result.DebugLog = new List<string>();
            }
            result.DebugLog.InsertRange(0, timingLog);
            var summary = PlanTimingAnalyzer.Summarize(timingLog);
            result.DebugLog.InsertRange(timingLog.Count, summary);

            return result;
        }

        /// <summary>
        /// Re-solves a previously generated plan with per-node decision
        /// overrides. Purely local: reuses the context's tree, prices,
        /// offers, and metadata; no network calls.
        /// </summary>
        public CraftingPlanResult ResolveWithOverrides(
            PlanSolveContext context,
            IReadOnlyDictionary<int, AcquisitionSource> overrides)
        {
            var solveResult = _solver.Solve(
                context.Tree, context.Prices, context.VendorOffers,
                context.PriceBasis, overrides, context.CurrencyValuation);

            var resultBuilder = new PlanResultBuilder();
            var result = resultBuilder.Build(
                solveResult.Plan, context.Tree, context.Metadata,
                context.UsedMaterials, context.LearnedRecipeIds);

            var treeBuilder = new CraftingTreeBuilder();
            result.CraftingTree = treeBuilder.BuildTree(
                context.Tree, solveResult.Decisions, context.Metadata);

            ApplySellSideEconomics(
                result, context.Tree, solveResult, context.Prices,
                context.TargetItemId, context.Quantity, context.PriceBasis,
                context.UsedMaterials, context.OwnMaterialsMode);
            result.SolveContext = context;

            if (result.DebugLog == null)
            {
                result.DebugLog = new List<string>();
            }
            result.DebugLog.Insert(0,
                $"Local re-solve with {overrides?.Count ?? 0} override(s)");

            return result;
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

        private static void ApplySellSideEconomics(
            CraftingPlanResult result,
            RecipeNode treeUsedForSolve,
            SolveResult solveResult,
            IReadOnlyDictionary<int, ItemPrice> prices,
            int targetItemId,
            int quantity,
            PriceBasis priceBasis,
            List<UsedMaterial> usedMaterials,
            OwnMaterialsMode ownMaterialsMode)
        {
            // Sell-side economics: what the crafted quantity nets after TP
            // fees, and profit versus the plan's coin cost. Coin-only by
            // design - non-coin currency costs have no coin value here.
            // Revenue must cover what the batch actually PRODUCES: when the
            // chosen root recipe over-produces (OutputCount does not divide
            // the requested quantity), the plan's cost pays for the whole
            // batch, so the extra units are sellable too.
            result.PriceBasis = priceBasis;
            int sellableQuantity = quantity;
            if (solveResult.Decisions.TryGetValue(treeUsedForSolve.NodeId, out var rootDecision) &&
                rootDecision.Source == AcquisitionSource.Craft)
            {
                var chosenRecipe = treeUsedForSolve.Recipes
                    .FirstOrDefault(r => r.RecipeId == rootDecision.RecipeId);
                if (chosenRecipe != null && chosenRecipe.OutputCount > 0)
                {
                    int produced = chosenRecipe.CraftsNeeded * chosenRecipe.OutputCount;
                    if (produced > sellableQuantity)
                    {
                        sellableQuantity = produced;
                    }
                }
            }
            result.SellableQuantity = sellableQuantity;

            // Own-materials opportunity cost (gw2efficiency-style "value own
            // materials"): what selling the owned materials that inventory
            // reduction consumed would have netted after TP fees. Reduction
            // itself never changes - owned mats are still consumed first at
            // zero acquisition cost in both modes; this only affects the
            // profit figure below. A material with no instant-sell price
            // (SellInstant 0/absent) contributes 0, not an exclusion.
            long? materialOpportunityCost = null;
            if (ownMaterialsMode == OwnMaterialsMode.Valued &&
                usedMaterials != null && usedMaterials.Count > 0)
            {
                long sum = 0;
                foreach (var used in usedMaterials)
                {
                    if (prices.TryGetValue(used.ItemId, out var matPrice) &&
                        matPrice.SellInstant > 0)
                    {
                        sum += TradingPostMath.NetSaleRevenue(matPrice.SellInstant, used.QuantityUsed);
                    }
                }
                materialOpportunityCost = sum;
            }
            result.MaterialOpportunityCost = materialOpportunityCost;

            if (prices.TryGetValue(targetItemId, out var targetPrice) &&
                targetPrice.SellInstant > 0)
            {
                result.TargetUnitSellPrice = targetPrice.SellInstant;
                result.NetSaleValue = TradingPostMath.NetSaleRevenue(
                    targetPrice.SellInstant, sellableQuantity);
                long profit = result.NetSaleValue.Value - solveResult.Plan.TotalCoinCost;
                if (materialOpportunityCost.HasValue)
                {
                    profit -= materialOpportunityCost.Value;
                }
                result.CraftingProfit = profit;
            }
        }

        private static void CollectItemIds(RecipeNode node, HashSet<int> ids)
        {
            if (node.IngredientType == "Item")
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
