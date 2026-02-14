using System.Collections.Generic;

namespace GW2CraftingHelper.Models
{
    public class VendorOfferDataset
    {
        public int SchemaVersion { get; set; }
        public string GeneratedAt { get; set; }
        public string Source { get; set; }
        public List<VendorOffer> Offers { get; set; } = new List<VendorOffer>();
    }
}
