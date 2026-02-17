using System.Collections.Generic;

namespace GW2CraftingHelper.Services.Recipes
{
    public class InMemoryRecipeCacheStore : IRecipeCacheStore
    {
        private readonly Dictionary<int, IReadOnlyList<int>> _searches =
            new Dictionary<int, IReadOnlyList<int>>();

        private readonly Dictionary<int, RawRecipe> _recipes =
            new Dictionary<int, RawRecipe>();

        private readonly RecipeCacheStats _stats = new RecipeCacheStats();

        public RecipeCacheStats Stats => _stats;

        public IReadOnlyList<int> TryGetSearch(int outputItemId)
        {
            if (_searches.TryGetValue(outputItemId, out var result))
            {
                _stats.SearchHits++;
                return result;
            }
            _stats.SearchMisses++;
            return null;
        }

        public RawRecipe TryGetRecipe(int recipeId)
        {
            if (_recipes.TryGetValue(recipeId, out var result))
            {
                _stats.RecipeHits++;
                return result;
            }
            _stats.RecipeMisses++;
            return null;
        }

        public void PutSearch(int outputItemId, IReadOnlyList<int> recipeIds)
        {
            _searches[outputItemId] = recipeIds;
        }

        public void PutRecipe(int recipeId, RawRecipe recipe)
        {
            _recipes[recipeId] = recipe;
        }

        public void Flush()
        {
            // No persistence — nothing to do.
        }

        public IReadOnlyDictionary<int, IReadOnlyList<int>> GetAllSearches()
        {
            return _searches;
        }

        public IReadOnlyDictionary<int, RawRecipe> GetAllRecipes()
        {
            return _recipes;
        }
    }
}
