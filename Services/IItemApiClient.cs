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
        public string Rarity { get; set; }

        // design-plan-notes.md (Notes section, excess/reclaim account-bound
        // exclusion): raw /v2/items "flags" strings (e.g. "AccountBound",
        // "SoulBindOnAcquire", "NoSell") - see Gw2ItemApiClient.GetItemsAsync.
        // Never null from that production parser (empty list when the API
        // response has no flags array); ItemMetadataService.
        // FetchBatchIntoCacheAsync reads this to set ItemMetadata.
        // IsAccountBound.
        public List<string> Flags { get; set; }
    }

    public interface IItemApiClient
    {
        Task<IReadOnlyList<RawItem>> GetItemsAsync(IReadOnlyList<int> itemIds, CancellationToken ct);
    }
}
