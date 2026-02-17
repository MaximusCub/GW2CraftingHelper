using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Tests.Helpers
{
    public class InMemoryRecipeApiClient : IRecipeApiClient
    {
        private readonly Dictionary<int, List<int>> _searchResults = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, RawRecipe> _recipes = new Dictionary<int, RawRecipe>();

        private int _currentConcurrency;
        private int _maxObservedConcurrency;

        public int MaxObservedConcurrency => _maxObservedConcurrency;
        public int LatencyMs { get; set; }

        public void AddSearchResult(int itemId, params int[] recipeIds)
        {
            _searchResults[itemId] = new List<int>(recipeIds);
        }

        public void AddRecipe(RawRecipe recipe)
        {
            _recipes[recipe.Id] = recipe;
        }

        public async Task<IReadOnlyList<int>> SearchByOutputAsync(int itemId, CancellationToken ct)
        {
            int concurrent = Interlocked.Increment(ref _currentConcurrency);
            try
            {
                UpdateMaxConcurrency(concurrent);

                if (LatencyMs > 0)
                {
                    await Task.Delay(LatencyMs, ct);
                }

                if (_searchResults.TryGetValue(itemId, out var ids))
                {
                    return ids;
                }

                return Array.Empty<int>();
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
