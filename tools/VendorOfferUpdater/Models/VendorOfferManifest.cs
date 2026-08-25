namespace VendorOfferUpdater.Models
{
    /// <summary>
    /// Provenance record for ref/vendor_offers.json, written alongside it as
    /// ref/vendor_offers_manifest.json. Everything about a run that is true of
    /// the run rather than of the data lives here, so the data file itself is
    /// byte-stable whenever the data is: a refresh that changes nothing leaves
    /// the 14.8MB blob untouched and moves only this file. Mirrors the shape
    /// ref/recipe_seed_manifest.json already uses for the recipe seeds.
    /// </summary>
    public class VendorOfferManifest
    {
        public int ManifestVersion { get; set; } = 1;
        public int SchemaVersion { get; set; } = 1;
        public string Source { get; set; } = string.Empty;
        public int OfferCount { get; set; }
        public string GeneratedAt { get; set; } = string.Empty;
    }
}
