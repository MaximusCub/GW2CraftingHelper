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
            IProgress<PlanStatus> progress = null)
        {
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

            // Step 6: Solve
            progress?.Report(new PlanStatus { Message = "Solving crafting plan..." });
            sw.Restart();
            var solveResult = _solver.Solve(tree, prices, vendorOffers);
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
            CancellationToken ct, IProgress<PlanStatus> progress = null)
        {
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

            // Step 6: Inventory reduction
            progress?.Report(new PlanStatus { Message = "Reducing inventory..." });
            sw.Restart();
            RecipeNode treeUsedForSolve = tree;
            List<UsedMaterial> usedMaterials = null;

            if (snapshot != null && _reducer != null)
            {
                var pool = SnapshotHelpers.AggregateItems(snapshot.Items)
                    .ToDictionary(e => e.ItemId, e => e.Count);
                var reduced = _reducer.Reduce(tree, pool);
                treeUsedForSolve = reduced.ReducedTree;
                usedMaterials = reduced.UsedMaterials;
            }
            sw.Stop();
            timingLog.Add($"Inventory reduction: {sw.ElapsedMilliseconds}ms");

            // Step 7: Solve
            progress?.Report(new PlanStatus { Message = "Solving crafting plan..." });
            sw.Restart();
            var solveResult = _solver.Solve(treeUsedForSolve, prices, vendorOffers);
            var plan = solveResult.Plan;
            sw.Stop();
            timingLog.Add($"Solve: {sw.ElapsedMilliseconds}ms");

            // Step 8: Fetch item metadata for all step items + target + used materials + tree items
            var metadataIds = new HashSet<int>(plan.Steps.Select(s => s.ItemId));
            metadataIds.Add(targetItemId);
            if (usedMaterials != null)
            {
                foreach (var um in usedMaterials)
                {
                    metadataIds.Add(um.ItemId);
                }
            }
            CraftingTreeBuilder.CollectTreeItemIds(treeUsedForSolve, solveResult.Decisions, metadataIds);
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
