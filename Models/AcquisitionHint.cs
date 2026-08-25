namespace GW2CraftingHelper.Models
{
    /// <summary>
    /// Wiki-derived acquisition guidance for an item with no priceable
    /// source (no TP price, no vendor offer, no craftable recipe). Hint and
    /// Badge are the only fields ever rendered to the user (tooltip text
    /// and a short pill/tag label, respectively); SourceUrl and
    /// LastVerified are provenance for maintainers only.
    /// </summary>
    public class AcquisitionHint
    {
        public int ItemId { get; set; }

        public string Hint { get; set; }

        // Short pill/tag label (e.g. "SALVAGE", "EXPLORE") shown in place of
        // the generic "UNKNOWN" badge when a hint gives a specific enough
        // acquisition category. Null/empty when the seed entry has none -
        // callers fall back to "UNKNOWN".
        public string Badge { get; set; }

        public string SourceUrl { get; set; }

        public string LastVerified { get; set; }
    }
}
