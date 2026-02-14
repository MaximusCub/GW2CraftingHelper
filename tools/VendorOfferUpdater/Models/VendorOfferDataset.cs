using System.Collections.Generic;

namespace VendorOfferUpdater.Models
{
    public class VendorOfferDataset
    {
        public int SchemaVersion { get; set; } = 1;
        public string GeneratedAt { get; set; }
        public string Source { get; set; }
        public List<VendorOffer> Offers { get; set; } = new List<VendorOffer>();
    }
}
