using System;
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

        /// <summary>
        /// When true, GetLearnedRecipeIdsAsync throws instead of returning
        /// - simulating a transient /v2/account/recipes failure (KNOWN-
        /// ISSUES api-degradation F4).
        /// </summary>
        public bool ThrowOnGet { get; set; }

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
            if (ThrowOnGet)
            {
                throw new InvalidOperationException("Simulated transient /v2/account/recipes failure.");
            }

            return Task.FromResult<ISet<int>>(_learnedRecipes);
        }

        public bool HasRequiredPermission()
        {
            return _hasPermission;
        }
    }
}
