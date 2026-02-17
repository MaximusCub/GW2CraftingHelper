using System;
using System.Collections.Generic;
using System.IO;

namespace GW2CraftingHelper.Services.Recipes
{
    public class SeededRecipeCacheStore : IRecipeCacheStore
    {
        private Dictionary<int, IReadOnlyList<int>> _searches =
            new Dictionary<int, IReadOnlyList<int>>();

        private Dictionary<int, RawRecipe> _recipes =
            new Dictionary<int, RawRecipe>();

        private readonly RecipeCacheStats _stats = new RecipeCacheStats();

        public RecipeCacheStats Stats => _stats;

        public void Load(Stream searchStream, Stream recipesStream)
        {
            if (searchStream == null)
            {
                throw new ArgumentNullException(nameof(searchStream));
            }
            if (recipesStream == null)
            {
                throw new ArgumentNullException(nameof(recipesStream));
            }

            _searches = RecipeCacheSerializer.LoadSearchSeed(searchStream);
            _recipes = RecipeCacheSerializer.LoadRecipeSeed(recipesStream);
        }

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
            // Read-only store — no-op.
        }

        public void PutRecipe(int recipeId, RawRecipe recipe)
        {
            // Read-only store — no-op.
        }

        public void Flush()
        {
            // Read-only store — nothing to persist.
        }
    }
}
