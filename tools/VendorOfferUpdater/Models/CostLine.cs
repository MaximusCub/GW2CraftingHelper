namespace VendorOfferUpdater.Models
{
    public class CostLine
    {
        // Nullable rather than defaulted to "": always populated by the
        // production construction sites (ConvertToOffer), but this type is
        // also deserialized as part of a --merge-into baseline's
        // VendorOffer.CostLines, where a malformed/legacy entry could
        // theoretically omit the key - see VendorOffer.OfferId's doc
        // comment for why a non-null default would risk a round-trip
        // difference in that case.
        public string? Type { get; set; }
        public int Id { get; set; }
        public int Count { get; set; }
    }
}
