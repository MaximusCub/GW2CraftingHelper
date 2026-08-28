using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Services;

namespace TaimisToolbench.Tests.Helpers
{
    internal class InMemoryAccountRecipeClient : IAccountRecipeClient
    {
        private readonly HashSet<int> _learnedRecipes = new HashSet<int>();
        private bool _hasPermission = true;

        /// <summary>
        /// When true, GetLearnedRecipeIdsAsync throws instead of returning
        /// - simulating a transient /v2/account/recipes failure (KNOWN-
        /// ISSUES api-degradation F4).
        /// </summary>
        public bool ThrowOnGet { get; set; }

        /// <summary>
        /// How many times GetLearnedRecipeIdsAsync has been entered,
        /// counting failed attempts - lets a test observe whether a caller
        /// re-queried the account endpoint or served a cache.
        /// </summary>
        public int GetCallCount { get; private set; }

        /// <summary>
        /// When set, every call awaits this task before returning - lets a
        /// test hold a fetch in flight while it does something to the caller,
        /// then release it. GetCallCount is bumped before the gate is
        /// awaited, so an in-flight call is already observable. Mirrors
        /// InMemoryPriceApiClient.Gate.
        /// </summary>
        public Task Gate { get; set; }

        public void AddLearnedRecipe(int recipeId)
        {
            _learnedRecipes.Add(recipeId);
        }

        /// <summary>
        /// Replaces the learned set outright - a second account's ids, or a
        /// recipe list that changed between calls.
        /// </summary>
        public void SetLearnedRecipes(params int[] recipeIds)
        {
            _learnedRecipes.Clear();
            foreach (var recipeId in recipeIds)
            {
                _learnedRecipes.Add(recipeId);
            }
        }

        public void SetHasPermission(bool hasPermission)
        {
            _hasPermission = hasPermission;
        }

        public async Task<ISet<int>> GetLearnedRecipeIdsAsync(CancellationToken ct)
        {
            GetCallCount++;

            if (Gate != null)
            {
                await Gate;
            }

            if (ThrowOnGet)
            {
                throw new InvalidOperationException("Simulated transient /v2/account/recipes failure.");
            }

            return new HashSet<int>(_learnedRecipes);
        }

        public bool HasRequiredPermission()
        {
            return _hasPermission;
        }
    }
}
