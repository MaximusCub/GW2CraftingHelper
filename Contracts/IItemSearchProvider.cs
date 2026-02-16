using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Contracts
{
    public class ItemSearchResult
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }
    }

    public interface IItemSearchProvider
    {
        Task<IReadOnlyList<ItemSearchResult>> SearchAsync(
            string query, int maxResults, CancellationToken ct);
    }
}
