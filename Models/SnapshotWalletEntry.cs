namespace GW2CraftingHelper.Models
{
    public class SnapshotWalletEntry
    {
        public int    CurrencyId   { get; set; }
        public string CurrencyName { get; set; } = "";
        public string IconUrl      { get; set; } = "";
        public int    Value        { get; set; }
    }
}
