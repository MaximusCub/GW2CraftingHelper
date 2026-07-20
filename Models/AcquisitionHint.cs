namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Wiki-derived acquisition guidance for an item with no priceable
    /// source (no TP price, no vendor offer, no craftable recipe). Hint is
    /// the only field ever rendered to the user (tooltip text); SourceUrl
    /// and LastVerified are provenance for maintainers only.
    /// </summary>
    public class AcquisitionHint
    {
        public int ItemId { get; set; }
        public string Hint { get; set; }
        public string SourceUrl { get; set; }
        public string LastVerified { get; set; }
    }
}
