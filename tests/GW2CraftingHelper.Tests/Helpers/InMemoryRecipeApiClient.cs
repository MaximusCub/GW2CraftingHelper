using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Tests.Helpers
{
    internal class InMemoryRecipeApiClient : IRecipeApiClient
    {
        private readonly Dictionary<int, List<int>> _searchResults = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, RawRecipe> _recipes = new Dictionary<int, RawRecipe>();

        private int _currentConcurrency;
        private int _maxObservedConcurrency;
        private int _searchCallCount;

        public int MaxObservedConcurrency => _maxObservedConcurrency;
        public int SearchCallCount => _searchCallCount;
        public int LatencyMs { get; set; }

        /// <summary>
        /// Recipe ids GetRecipeAsync should return null for, simulating a
        /// 404 (KNOWN-ISSUES #31/api-degradation F5's new null-on-404 contract)
        /// instead of the indexer's default throw-on-missing-key behavior,
        /// which every other existing test relies on to catch a genuinely
        /// mismatched search/recipe setup.
        /// </summary>
        public HashSet<int> Return404For { get; } = new HashSet<int>();

        /// <summary>
        /// Output item ids SearchByOutputAsync should answer for as a 404
        /// does - empty, with absence unproven - instead of the ordinary
        /// "no rows registered, so this item has no recipe" empty.
        /// </summary>
        public HashSet<int> Return404ForSearch { get; } = new HashSet<int>();

        public void AddSearchResult(int itemId, params int[] recipeIds)
        {
            _searchResults[itemId] = new List<int>(recipeIds);
        }

        public void AddRecipe(RawRecipe recipe)
        {
            _recipes[recipe.Id] = recipe;
        }

        public async Task<RecipeSearchResult> SearchByOutputAsync(int itemId, CancellationToken ct)
        {
            int concurrent = Interlocked.Increment(ref _currentConcurrency);
            try
            {
                UpdateMaxConcurrency(concurrent);
                Interlocked.Increment(ref _searchCallCount);

                if (LatencyMs > 0)
                {
                    await Task.Delay(LatencyMs, ct);
                }

                if (Return404ForSearch.Contains(itemId))
                {
                    return new RecipeSearchResult(Array.Empty<int>(), absenceProven: false);
                }

                if (_searchResults.TryGetValue(itemId, out var ids))
                {
                    return new RecipeSearchResult(ids, absenceProven: true);
                }

                return new RecipeSearchResult(Array.Empty<int>(), absenceProven: true);
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }

        public async Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
        {
            int concurrent = Interlocked.Increment(ref _currentConcurrency);
            try
            {
                UpdateMaxConcurrency(concurrent);

                if (LatencyMs > 0)
                {
                    await Task.Delay(LatencyMs, ct);
                }

                if (Return404For.Contains(recipeId))
                {
                    return null;
                }

                return _recipes[recipeId];
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }

        private void UpdateMaxConcurrency(int concurrent)
        {
            int max;
            do
            {
                max = _maxObservedConcurrency;
                if (concurrent <= max)
                {
                    break;
                }
            }
            while (Interlocked.CompareExchange(
                ref _maxObservedConcurrency, concurrent, max) != max);
        }
    }
}
