using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GW2CraftingHelper.Services.Recipes
{
    public class SeededRecipeCacheStore : IRecipeCacheStore
    {
        private Dictionary<int, IReadOnlyList<int>> _searches =
            new Dictionary<int, IReadOnlyList<int>>();

        private Dictionary<int, RawRecipe> _recipes =
            new Dictionary<int, RawRecipe>();

        private readonly RecipeCacheStats _stats = new RecipeCacheStats();
        private int? _seedBuildId;
        private int _currentBuildId;
        private int _hasCurrent;

        public RecipeCacheStats Stats => _stats;
        public int? SeedBuildId => _seedBuildId;

        public int? CurrentBuildId
        {
            get { return Volatile.Read(ref _hasCurrent) == 1 ? (int?)Volatile.Read(ref _currentBuildId) : null; }
        }

        public bool SeedIsStale
        {
            get
            {
                if (!_seedBuildId.HasValue || Volatile.Read(ref _hasCurrent) == 0)
                {
                    return false;
                }
                return _seedBuildId.Value != Volatile.Read(ref _currentBuildId);
            }
        }

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

        public void LoadManifest(Stream manifestStream)
        {
            if (manifestStream == null)
            {
                return;
            }

            var manifest = RecipeCacheSerializer.LoadManifest<RecipeSeedManifest>(manifestStream);
            if (manifest.Gw2BuildId > 0)
            {
                _seedBuildId = manifest.Gw2BuildId;
            }
        }

        public void SetCurrentBuildId(int buildId)
        {
            Volatile.Write(ref _currentBuildId, buildId);
            Volatile.Write(ref _hasCurrent, 1);
        }

        public IReadOnlyList<int> TryGetSearch(int outputItemId)
        {
            if (_searches.TryGetValue(outputItemId, out var result))
            {
                if (result.Count == 0 && SeedIsStale)
                {
                    _stats.IncrementSearchMiss();
                    return null;
                }
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
            // Read-only store - no-op.
        }

        public void PutRecipe(int recipeId, RawRecipe recipe)
        {
            // Read-only store - no-op.
        }

        public void Flush(bool force = false)
        {
            // Read-only store - nothing to persist.
        }
    }
}
