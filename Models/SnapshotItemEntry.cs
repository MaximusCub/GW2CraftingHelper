namespace GW2CraftingHelper.Models
{
    public class SnapshotItemEntry
    {
        public int    ItemId  { get; set; }
        public string Name    { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public int    Count   { get; set; }
        public string Source  { get; set; } = "";
    }
}
