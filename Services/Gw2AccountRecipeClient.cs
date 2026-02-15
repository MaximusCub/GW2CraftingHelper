using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD.Modules.Managers;
using Gw2Sharp.WebApi.V2.Models;

namespace GW2CraftingHelper.Services
{
    public class Gw2AccountRecipeClient : IAccountRecipeClient
    {
        private readonly Gw2ApiManager _apiManager;

        public Gw2AccountRecipeClient(Gw2ApiManager apiManager)
        {
            _apiManager = apiManager;
        }

        public bool HasRequiredPermission()
        {
            return _apiManager.HasPermissions(new[] { TokenPermission.Unlocks });
        }

        public async Task<ISet<int>> GetLearnedRecipeIdsAsync(CancellationToken ct)
        {
            var recipes = await _apiManager.Gw2ApiClient.V2.Account.Recipes.GetAsync(ct);
            return new HashSet<int>(recipes.Select(r => (int)r));
        }
    }
}
