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
        private readonly int _maxConcurrency;
        private readonly Dictionary<int, IReadOnlyList<int>> _searchCache = new Dictionary<int, IReadOnlyList<int>>();
        private readonly Dictionary<int, RawRecipe> _recipeCache = new Dictionary<int, RawRecipe>();
        private readonly object _cacheGate = new object();

        private const int DefaultMaxConcurrency = 4;

        public RecipeService(IRecipeApiClient api, int maxConcurrency = DefaultMaxConcurrency)
        {
            _api = api;
            _maxConcurrency = maxConcurrency;
        }

        public async Task<RecipeNode> BuildTreeAsync(int itemId, int quantity, CancellationToken ct)
        {
            await PreWarmCacheAsync(itemId, ct);

            var visiting = new HashSet<int>();
            return await BuildNodeAsync(itemId, "Item", quantity, visiting, ct);
        }

        private async Task PreWarmCacheAsync(int itemId, CancellationToken ct)
        {
            var visited = new HashSet<int>();
            var frontier = new HashSet<int> { itemId };

            while (frontier.Count > 0)
            {
                // Sub-phase A: Search all frontier items concurrently
                await BoundedConcurrency.ForEachAsync(
                    frontier,
                    _maxConcurrency,
                    id => SearchByOutputCachedAsync(id, ct),
                    ct);

                // Collect recipe IDs not yet cached
                var recipeIds = new HashSet<int>();
                foreach (var fid in frontier)
                {
                    IReadOnlyList<int> rids;
                    lock (_cacheGate)
                    {
                        _searchCache.TryGetValue(fid, out rids);
                    }

                    if (rids == null)
                    {
                        continue;
                    }

                    foreach (var rid in rids)
                    {
                        bool cached;
                        lock (_cacheGate)
                        {
                            cached = _recipeCache.ContainsKey(rid);
                        }

                        if (!cached)
                        {
                            recipeIds.Add(rid);
                        }
                    }
                }

                // Sub-phase B: Fetch all recipe details concurrently
                await BoundedConcurrency.ForEachAsync(
                    recipeIds,
                    _maxConcurrency,
                    rid => GetRecipeCachedAsync(rid, ct),
                    ct);

                // Build next frontier from ingredient item IDs
                visited.UnionWith(frontier);
                var nextFrontier = new HashSet<int>();

                foreach (var fid in frontier)
                {
                    IReadOnlyList<int> rids;
                    lock (_cacheGate)
                    {
                        _searchCache.TryGetValue(fid, out rids);
                    }

                    if (rids == null)
                    {
                        continue;
                    }

                    foreach (var rid in rids)
                    {
                        RawRecipe recipe;
                        lock (_cacheGate)
                        {
                            _recipeCache.TryGetValue(rid, out recipe);
                        }

                        if (recipe == null)
                        {
                            continue;
                        }

                        foreach (var ingredient in recipe.Ingredients)
                        {
                            if (ingredient.Type == "Item" && !visited.Contains(ingredient.Id))
                            {
                                nextFrontier.Add(ingredient.Id);
                            }
                        }
                    }
                }

                frontier = nextFrontier;
            }
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

            if (!string.IsNullOrEmpty(ingredientType) && ingredientType != "Item")
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
                        CraftsNeeded = craftsNeeded,
                        Disciplines = new List<string>(raw.Disciplines),
                        MinRating = raw.MinRating,
                        Flags = new List<string>(raw.Flags)
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
            lock (_cacheGate)
            {
                if (_searchCache.TryGetValue(itemId, out var cached))
                {
                    return cached;
                }
            }

            var result = await _api.SearchByOutputAsync(itemId, ct);

            lock (_cacheGate)
            {
                if (!_searchCache.ContainsKey(itemId))
                {
                    _searchCache[itemId] = result;
                }
            }

            return result;
        }

        private async Task<RawRecipe> GetRecipeCachedAsync(int recipeId, CancellationToken ct)
        {
            lock (_cacheGate)
            {
                if (_recipeCache.TryGetValue(recipeId, out var cached))
                {
                    return cached;
                }
            }

            var result = await _api.GetRecipeAsync(recipeId, ct);

            lock (_cacheGate)
            {
                if (!_recipeCache.ContainsKey(recipeId))
                {
                    _recipeCache[recipeId] = result;
                }
            }

            return result;
        }
    }
}
