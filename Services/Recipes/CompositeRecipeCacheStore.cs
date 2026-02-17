using System.Collections.Generic;

namespace GW2CraftingHelper.Services.Recipes
{
    public class CompositeRecipeCacheStore : IRecipeCacheStore
    {
        private readonly SeededRecipeCacheStore _seed;
        private readonly OverlayRecipeCacheStore _overlay;
        private readonly RecipeCacheStats _stats = new RecipeCacheStats();

        public RecipeCacheStats Stats => _stats;

        public CompositeRecipeCacheStore(
            SeededRecipeCacheStore seed,
            OverlayRecipeCacheStore overlay)
        {
            _seed = seed;
            _overlay = overlay;
        }

        public IReadOnlyList<int> TryGetSearch(int outputItemId)
        {
            // Overlay first — it has newer content discovered after seed.
            var result = _overlay.TryGetSearch(outputItemId);
            if (result != null)
            {
                _stats.SearchHits++;
                return result;
            }

            result = _seed.TryGetSearch(outputItemId);
            if (result != null)
            {
                _stats.SearchHits++;
                return result;
            }

            _stats.SearchMisses++;
            return null;
        }

        public RawRecipe TryGetRecipe(int recipeId)
        {
            // Overlay first — it has newer content discovered after seed.
            var result = _overlay.TryGetRecipe(recipeId);
            if (result != null)
            {
                _stats.RecipeHits++;
                return result;
            }

            result = _seed.TryGetRecipe(recipeId);
            if (result != null)
            {
                _stats.RecipeHits++;
                return result;
            }

            _stats.RecipeMisses++;
            return null;
        }

        public void PutSearch(int outputItemId, IReadOnlyList<int> recipeIds)
        {
            _overlay.PutSearch(outputItemId, recipeIds);
        }

        public void PutRecipe(int recipeId, RawRecipe recipe)
        {
            _overlay.PutRecipe(recipeId, recipe);
        }

        public void Flush()
        {
            _overlay.Flush();
        }
    }
}
