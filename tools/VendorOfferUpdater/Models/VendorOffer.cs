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
    }
}
