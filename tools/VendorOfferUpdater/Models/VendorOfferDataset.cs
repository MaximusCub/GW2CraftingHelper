using System.Collections.Generic;

namespace VendorOfferUpdater.Models
{
    // No GeneratedAt member on purpose. An embedded regeneration timestamp
    // made every run emit a fresh 14.8MB blob even when the scrape returned
    // byte-identical data, so "the bytes did not change" could never be used
    // as evidence that a refresh was a no-op. The timestamp now lives in the
    // sibling ref/vendor_offers_manifest.json (VendorOfferManifest), matching
    // the pattern ref/recipe_seed_manifest.json already establishes. Reading a
    // pre-existing baseline that still carries the key is unaffected:
    // System.Text.Json ignores unknown members by default.
    public class VendorOfferDataset
    {
        public int SchemaVersion { get; set; } = 1;
        public string Source { get; set; } = string.Empty;
        public List<VendorOffer> Offers { get; set; } = new List<VendorOffer>();
    }
}
