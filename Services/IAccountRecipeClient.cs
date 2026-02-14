using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    public interface IAccountRecipeClient
    {
        Task<ISet<int>> GetLearnedRecipeIdsAsync(CancellationToken ct);
        bool HasRequiredPermission();
    }
}
