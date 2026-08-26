using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    internal interface IAccountRecipeClient
    {
        Task<ISet<int>> GetLearnedRecipeIdsAsync(CancellationToken ct);

        bool HasRequiredPermission();
    }
}
