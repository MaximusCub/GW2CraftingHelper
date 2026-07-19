namespace GW2CraftingHelper.Models
{
    public class ItemMetadata
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string IconUrl { get; set; }

        // GW2 API rarity string (e.g. "Fine", "Exotic"); null/empty = unknown.
        public string Rarity { get; set; }
    }
}
