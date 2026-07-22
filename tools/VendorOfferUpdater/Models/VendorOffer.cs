using System.Collections.Generic;

namespace VendorOfferUpdater.Models
{
    public class VendorOffer
    {
        // Nullable rather than defaulted to "": always populated by the
        // one production construction site (ConvertToOffer), but a
        // deserialized --merge-into baseline entry from a malformed/legacy
        // file could theoretically omit either key, and MergeIntoBaseline's
        // own "?? string.Empty" coalescing below already treats
        // MerchantName as possibly null - a non-null default here would
        // make such a row round-trip differently (an empty "" written out
        // instead of the key being omitted, since output serialization
        // uses DefaultIgnoreCondition.WhenWritingNull).
        public string? OfferId { get; set; }
        public int OutputItemId { get; set; }
        public int OutputCount { get; set; }
        public List<CostLine> CostLines { get; set; } = new List<CostLine>();
        public string? MerchantName { get; set; }

        // Nullable, not just an empty list: --merge-into deliberately
        // restores this to null (rather than []) for a baseline offer with
        // no location data, so an untouched offer round-trips byte-for-byte
        // against a baseline that never had the "locations" key at all -
        // see the --merge-into round-trip fix in Program.cs.
        public List<string>? Locations { get; set; } = new List<string>();
        public int? DailyCap { get; set; }
        public int? WeeklyCap { get; set; }

        // M37 (KNOWN-ISSUES #24): Homestead Refinement efficiency tier
        // (0/1/2) this offer row corresponds to, or null for a non-
        // Homestead-Refinement offer. See ConvertToOffer/HomesteadTierResolver.
        public int? HomesteadTier { get; set; }
    }
}
