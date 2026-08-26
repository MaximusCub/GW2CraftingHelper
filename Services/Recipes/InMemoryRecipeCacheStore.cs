using System.Collections.Generic;

namespace GW2CraftingHelper.Services.Recipes
{
    internal class InMemoryRecipeCacheStore : IRecipeCacheStore
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
                _stats.IncrementSearchHit();
                return result;
            }

            _stats.IncrementSearchMiss();
            return null;
        }

        public RawRecipe TryGetRecipe(int recipeId)
        {
            if (_recipes.TryGetValue(recipeId, out var result))
            {
                _stats.IncrementRecipeHit();
                return result;
            }

            _stats.IncrementRecipeMiss();
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

        public void Flush(bool force = false)
        {
            // No persistence - nothing to do.
        }
    }
}
