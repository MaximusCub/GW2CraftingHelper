using System.Collections.Generic;

namespace VendorOfferUpdater.Models
{
    public class VendorOfferDataset
    {
        public int SchemaVersion { get; set; } = 1;
        public string GeneratedAt { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public List<VendorOffer> Offers { get; set; } = new List<VendorOffer>();
    }
}
