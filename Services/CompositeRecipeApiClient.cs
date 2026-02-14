using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    public class CompositeRecipeApiClient : IRecipeApiClient
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

        public async Task<RawRecipe> GetRecipeAsync(int recipeId, CancellationToken ct)
        {
            if (recipeId < 0)
            {
                var mfRecipe = _mfData.GetRecipe(recipeId);
                if (mfRecipe != null)
                {
                    return mfRecipe;
                }
            }

            return await _primary.GetRecipeAsync(recipeId, ct);
        }
    }
}
