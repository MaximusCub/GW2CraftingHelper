using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    internal class CompositeRecipeApiClient : IRecipeApiClient
    {
        private readonly IRecipeApiClient _primary;
        private readonly MysticForgeRecipeData _mfData;

        public CompositeRecipeApiClient(IRecipeApiClient primary, MysticForgeRecipeData mfData)
        {
            _primary = primary;
            _mfData = mfData;
        }

        public async Task<IReadOnlyList<int>> SearchByOutputAsync(int itemId, CancellationToken ct)
        {
            var apiResults = await _primary.SearchByOutputAsync(itemId, ct);
            var mfResults = _mfData.SearchByOutput(itemId);

            if (mfResults.Count == 0)
            {
                return apiResults;
            }

            if (apiResults.Count == 0)
            {
                return mfResults;
            }

            // Merge: API first, then MF, deduplicated
            var seen = new HashSet<int>();
            var merged = new List<int>();

            foreach (var id in apiResults)
            {
                if (seen.Add(id))
                {
                    merged.Add(id);
                }
            }

            foreach (var id in mfResults)
            {
                if (seen.Add(id))
                {
                    merged.Add(id);
                }
            }

            return merged;
        }

        public Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
        {
            // Membership check, not a bare sign check: a negative recipeId
            // is Mystic Forge ONLY if MysticForgeRecipeData actually
            // recognizes it. Other negative-id synthetic recipes (e.g. the
            // achievement/merchant seed recipes, ref/recipes_seed.json
            // ids -1592..-1595, adjacent to but not part of the Mystic
            // Forge id range) are NOT Mystic Forge recipes and must fall
            // through to primary instead of being silently swallowed as a
            // false "not found" from mfData alone.
            if (recipeId < 0)
            {
                var mfRecipe = _mfData.GetRecipe(recipeId);
                if (mfRecipe != null)
                {
                    return Task.FromResult(mfRecipe);
                }
            }

            return _primary.GetRecipeAsync(recipeId, ct);
        }
    }
}
