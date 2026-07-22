using System.Collections.Generic;

namespace VendorOfferUpdater.Models
{
    public class VendorOffer
    {
        public string OfferId { get; set; }
        public int OutputItemId { get; set; }
        public int OutputCount { get; set; }
        public List<CostLine> CostLines { get; set; } = new List<CostLine>();
        public string MerchantName { get; set; }
        public List<string> Locations { get; set; } = new List<string>();
        public int? DailyCap { get; set; }
        public int? WeeklyCap { get; set; }

        // M37 (KNOWN-ISSUES #24): Homestead Refinement efficiency tier
        // (0/1/2) this offer row corresponds to, or null for a non-
        // Homestead-Refinement offer. See ConvertToOffer/HomesteadTierResolver.
        public int? HomesteadTier { get; set; }
    }
}
