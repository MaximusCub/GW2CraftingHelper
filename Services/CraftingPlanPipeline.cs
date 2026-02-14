using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class CraftingPlanPipeline
    {
        private readonly RecipeService _recipeService;
        private readonly TradingPostService _tradingPostService;
        private readonly PlanSolver _solver;
        private readonly ItemMetadataService _itemMetadataService;
        private readonly VendorOfferStore _vendorOfferStore;

        public CraftingPlanPipeline(
            RecipeService recipeService,
            TradingPostService tradingPostService,
            PlanSolver solver,
            ItemMetadataService itemMetadataService,
            VendorOfferStore vendorOfferStore = null)
        {
            _recipeService = recipeService;
            _tradingPostService = tradingPostService;
            _solver = solver;
            _itemMetadataService = itemMetadataService;
            _vendorOfferStore = vendorOfferStore;
        }

        public async Task<CraftingPlanResult> GenerateAsync(
            int targetItemId, int quantity, CancellationToken ct)
        {
            // Step 1: Build recipe tree
            var tree = await _recipeService.BuildTreeAsync(targetItemId, quantity, ct);

            // Step 2: Collect all item IDs from the tree for price lookup
            var allItemIds = new HashSet<int>();
            CollectItemIds(tree, allItemIds);

            // Step 3: Fetch TP prices
            var prices = await _tradingPostService.GetPricesAsync(allItemIds, ct);

            // Step 4: Query vendor offers
            IReadOnlyDictionary<int, IReadOnlyList<VendorOffer>> vendorOffers = null;
            if (_vendorOfferStore != null)
            {
                vendorOffers = _vendorOfferStore.GetOffersForItems(allItemIds);
            }

            // Step 5: Solve
            var plan = _solver.Solve(tree, prices, vendorOffers);

            // Step 6: Fetch item metadata for all step items + target
            var metadataIds = new HashSet<int>(plan.Steps.Select(s => s.ItemId));
            metadataIds.Add(targetItemId);
            var metadata = await _itemMetadataService.GetMetadataAsync(metadataIds, ct);

            return new CraftingPlanResult
            {
                Plan = plan,
                ItemMetadata = metadata
            };
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
