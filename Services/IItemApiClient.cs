using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GW2CraftingHelper.Services
{
    public class RawItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
    }

    public interface IItemApiClient
    {
        Task<IReadOnlyList<RawItem>> GetItemsAsync(IReadOnlyList<int> itemIds, CancellationToken ct);
    }
}
