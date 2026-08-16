namespace GW2CraftingHelper.Models
{
    public class ItemMetadata
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }

        // GW2 API rarity string (e.g. "Fine", "Exotic"); null/empty = unknown.
        public string Rarity { get; set; }

        // design-plan-notes.md (Notes section, excess/reclaim account-bound
        // exclusion): true when the GW2 API's /v2/items "flags" array for
        // this item contains "AccountBound" (see RawItem.Flags /
        // ItemMetadataService.FetchBatchIntoCacheAsync). False (never null)
        // when flags data is unavailable (bundled seed fallback entries -
        // see ItemMetadataService.GetMetadataAsync's seed branch, which
        // never sets this field) - an unknown item is never claimed
        // account-bound.
        public bool IsAccountBound { get; set; }
    }
}
