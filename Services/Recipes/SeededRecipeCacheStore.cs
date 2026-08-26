using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace GW2CraftingHelper.Services.Recipes
{
    internal class SeededRecipeCacheStore : IRecipeCacheStore
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

        /// <summary>
        /// Folds the wiki-sourced Mystic Forge recipes into the seed, so
        /// they are served from cache like any other seeded recipe.
        /// <para>
        /// Load-time only (same contract as <see cref="Load"/>: called once
        /// at startup, before any lookup), and additive - an existing
        /// search entry keeps its API-sourced ids and gains the MF ones
        /// after them, matching CompositeRecipeApiClient's own merge order.
        /// </para>
        /// <para>
        /// Without this, an item whose seed entry is an EMPTY list - the
        /// seeder's "the API knows no recipe for this" negative cache row -
        /// is served as a cache HIT, so nothing ever asks the Mystic Forge
        /// data whether it has one, and the item renders UNKNOWN. That made
        /// MF coverage depend on the seeder having run since the wiki data
        /// was last edited, and (via the stale-seed fallback, which turns
        /// empty entries into API calls the composite client rescues) on
        /// the live game build id - neither of which has anything to do
        /// with wiki-sourced data.
        /// </para>
        /// </summary>
        public void MergeMysticForgeRecipes(MysticForgeRecipeData mysticForge)
        {
            if (mysticForge == null)
            {
                return;
            }

            foreach (var recipe in mysticForge.AllRecipes)
            {
                _recipes[recipe.Id] = recipe;

                if (!_searches.TryGetValue(recipe.OutputItemId, out var existing))
                {
                    _searches[recipe.OutputItemId] = new List<int> { recipe.Id };
                    continue;
                }

                if (existing.Contains(recipe.Id))
                {
                    continue;
                }

                var merged = new List<int>(existing.Count + 1);
                merged.AddRange(existing);
                merged.Add(recipe.Id);
                _searches[recipe.OutputItemId] = merged;
            }
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
