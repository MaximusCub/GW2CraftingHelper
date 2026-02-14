using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;

namespace GW2CraftingHelper.Tests.Helpers
{
    public class InMemoryAccountRecipeClient : IAccountRecipeClient
    {
        private readonly HashSet<int> _learnedRecipes = new HashSet<int>();
        private bool _hasPermission = true;

        public void AddLearnedRecipe(int recipeId)
        {
            _learnedRecipes.Add(recipeId);
        }

        public void SetHasPermission(bool hasPermission)
        {
            _hasPermission = hasPermission;
        }

        public Task<ISet<int>> GetLearnedRecipeIdsAsync(CancellationToken ct)
        {
            return Task.FromResult<ISet<int>>(_learnedRecipes);
        }

        public bool HasRequiredPermission()
        {
            return _hasPermission;
        }
    }
}
