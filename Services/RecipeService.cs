using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    public class RecipeService
    {
        private readonly IRecipeApiClient _api;
        private readonly Dictionary<int, IReadOnlyList<int>> _searchCache = new Dictionary<int, IReadOnlyList<int>>();
        private readonly Dictionary<int, RawRecipe> _recipeCache = new Dictionary<int, RawRecipe>();

        public RecipeService(IRecipeApiClient api)
        {
            _api = api;
        }

        public async Task<RecipeNode> BuildTreeAsync(int itemId, int quantity, CancellationToken ct)
        {
            var visiting = new HashSet<int>();
            return await BuildNodeAsync(itemId, "Item", quantity, visiting, ct);
        }

        private async Task<RecipeNode> BuildNodeAsync(
            int id, string ingredientType, int quantity,
            HashSet<int> visiting, CancellationToken ct)
        {
            var node = new RecipeNode
            {
                Id = id,
                IngredientType = ingredientType,
                Quantity = quantity
            };

            if (ingredientType != "Item")
            {
                return node;
            }

            if (!visiting.Add(id))
            {
                return node;
            }

            try
            {
                var recipeIds = await SearchByOutputCachedAsync(id, ct);

                foreach (var recipeId in recipeIds)
                {
                    var raw = await GetRecipeCachedAsync(recipeId, ct);
                    int craftsNeeded = (int)Math.Ceiling((double)quantity / raw.OutputItemCount);

                    var option = new RecipeOption
                    {
                        RecipeId = raw.Id,
                        OutputCount = raw.OutputItemCount,
                        CraftsNeeded = craftsNeeded
                    };

                    foreach (var ingredient in raw.Ingredients)
                    {
                        int ingredientQuantity = craftsNeeded * ingredient.Count;
                        var childNode = await BuildNodeAsync(
                            ingredient.Id, ingredient.Type, ingredientQuantity,
                            visiting, ct);
                        option.Ingredients.Add(childNode);
                    }

                    node.Recipes.Add(option);
                }
            }
            finally
            {
                visiting.Remove(id);
            }

            return node;
        }

        private async Task<IReadOnlyList<int>> SearchByOutputCachedAsync(int itemId, CancellationToken ct)
        {
            if (_searchCache.TryGetValue(itemId, out var cached))
            {
                return cached;
            }

            var result = await _api.SearchByOutputAsync(itemId, ct);
            _searchCache[itemId] = result;
            return result;
        }

        private async Task<RawRecipe> GetRecipeCachedAsync(int recipeId, CancellationToken ct)
        {
            if (_recipeCache.TryGetValue(recipeId, out var cached))
            {
                return cached;
            }

            var result = await _api.GetRecipeAsync(recipeId, ct);
            _recipeCache[recipeId] = result;
            return result;
        }
    }
}
