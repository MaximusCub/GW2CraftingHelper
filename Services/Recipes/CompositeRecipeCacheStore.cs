using System.Collections.Generic;

namespace GW2CraftingHelper.Services.Recipes
{
    public class CompositeRecipeCacheStore : IRecipeCacheStore
    {
        private readonly SeededRecipeCacheStore _seed;
        private readonly OverlayRecipeCacheStore _overlay;
        private readonly RecipeCacheStats _stats = new RecipeCacheStats();

        public RecipeCacheStats Stats => _stats;
        public bool SeedIsStale => _seed.SeedIsStale;
        public int? SeedBuildId => _seed.SeedBuildId;
        public int? CurrentBuildId => _seed.CurrentBuildId;

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
                _stats.IncrementSearchHit();
                return result;
            }

            result = _seed.TryGetSearch(outputItemId);
            if (result != null)
            {
                _stats.IncrementSearchHit();
                return result;
            }

            _stats.IncrementSearchMiss();
            return null;
        }

        public RawRecipe TryGetRecipe(int recipeId)
        {
            // Overlay first — it has newer content discovered after seed.
            var result = _overlay.TryGetRecipe(recipeId);
            if (result != null)
            {
                _stats.IncrementRecipeHit();
                return result;
            }

            result = _seed.TryGetRecipe(recipeId);
            if (result != null)
            {
                _stats.IncrementRecipeHit();
                return result;
            }

            _stats.IncrementRecipeMiss();
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

        public void Flush(bool force = false)
        {
            _overlay.Flush(force);
        }
    }
}
