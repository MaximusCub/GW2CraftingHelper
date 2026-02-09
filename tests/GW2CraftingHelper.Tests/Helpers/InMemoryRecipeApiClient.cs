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

        public void AddSearchResult(int itemId, params int[] recipeIds)
        {
            _searchResults[itemId] = new List<int>(recipeIds);
        }

        public void AddRecipe(RawRecipe recipe)
        {
            _recipes[recipe.Id] = recipe;
        }

        public Task<IReadOnlyList<int>> SearchByOutputAsync(int itemId, CancellationToken ct)
        {
            if (_searchResults.TryGetValue(itemId, out var ids))
            {
                return Task.FromResult<IReadOnlyList<int>>(ids);
            }

            return Task.FromResult<IReadOnlyList<int>>(new List<int>());
        }

        public Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
        {
            return Task.FromResult(_recipes[recipeId]);
        }
    }
}
