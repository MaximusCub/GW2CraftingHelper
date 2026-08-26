using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services.Recipes
{
    internal class CompositeRecipeCacheStore : IRecipeCacheStore
    {
        private readonly SeededRecipeCacheStore _seed;
        private readonly OverlayRecipeCacheStore _overlay;
        private readonly RecipeCacheStats _stats = new RecipeCacheStats();

        public RecipeCacheStats Stats => _stats;

        public bool SeedIsStale => _seed.SeedIsStale;

        public int? SeedBuildId => _seed.SeedBuildId;

        public int? CurrentBuildId => _seed.CurrentBuildId;

        public int NegativesVerifiedBuildId => _overlay.NegativesVerifiedBuildId;

        public int VerifiedKnownRecipeCount => _overlay.VerifiedKnownRecipeCount;

        public bool NegativesVerifiedAtCurrentBuild
        {
            get
            {
                int? current = _seed.CurrentBuildId;
                return current.HasValue
                    && _overlay.NegativesVerifiedBuildId == current.Value;
            }
        }

        public CompositeRecipeCacheStore(
            SeededRecipeCacheStore seed,
            OverlayRecipeCacheStore overlay)
        {
            _seed = seed;
            _overlay = overlay;
        }

        /// <summary>
        /// Records that the corpus was checked against the live recipe id
        /// list (RecipeCorpusVerifier); flushed into the overlay manifest.
        /// </summary>
        public void SetCorpusVerified(int buildId, int knownRecipeCount)
        {
            _overlay.SetCorpusVerified(buildId, knownRecipeCount);
        }

        public IReadOnlyList<int> TryGetSearch(int outputItemId)
        {
            // Overlay first - it has newer content discovered after seed.
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

            // The authoritative negative, derived rather than stored: the
            // corpus (seed + forge + overlay) holds every recipe the live
            // id list does, so "no known recipe outputs this item" IS the
            // answer - the search endpoint would add nothing but its 15
            // known false negatives. The RecipeCount guard matters:
            // Module.cs's seed-load catch can leave an empty seed, and an
            // empty corpus must not answer "no recipe" for everything.
            if (_seed.RecipeCount > 0)
            {
                _stats.IncrementSearchHit();
                return Array.Empty<int>();
            }

            _stats.IncrementSearchMiss();
            return null;
        }

        public RawRecipe TryGetRecipe(int recipeId)
        {
            // Overlay first - it has newer content discovered after seed.
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
