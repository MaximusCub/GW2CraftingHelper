using System.Collections.Generic;
using System.Linq;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using VendorOfferUpdater.Tests.Helpers;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// Cross-project parity net for VendorOfferHasher.
    /// tools/VendorOfferUpdater/VendorOfferHasher.cs (this project,
    /// net8.0) and Services/VendorOfferHasher.cs (net48) are two
    /// independently-maintained copies with byte-for-byte identical
    /// string-composition logic; only the digest-to-hex step differs
    /// (SHA256.HashData+Convert.ToHexString here, SHA256.Create()+
    /// byte-to-hex loop there), which is why they must still agree on
    /// every input. VendorOfferHasherTests.cs (same directory) already
    /// pins this copy's relative behavior (equal/not-equal on varied
    /// inputs); this file instead asserts against the fixed hex digests in
    /// tests/shared/vendor_offer_hasher_vectors.json, the exact same rows
    /// GW2CraftingHelper.Tests/Services/VendorOfferHasherGoldenVectorTests.cs
    /// asserts against. A future edit that changes either copy's
    /// composition (cost-line ordering, null handling, or - the specific
    /// footgun the "all_optional_fields_distinct_field_order_guard" row
    /// exists for - reordering/mis-slotting the dailyCap/weeklyCap/
    /// homesteadTier/seasonalCap trailing parameters) fails both suites
    /// against the same golden hashes, instead of only self-consistency
    /// checks that a synchronized mistake in both files would still pass.
    /// </summary>
    public class VendorOfferHasherGoldenVectorTests
    {
        public static IEnumerable<object[]> GoldenVectors()
        {
            foreach (var vector in VendorOfferHasherVectorFixture.Load())
            {
                yield return new object[] { vector };
            }
        }

        [Theory]
        [MemberData(nameof(GoldenVectors))]
        public void ComputeOfferId_MatchesGoldenVector(VendorOfferHasherVector vector)
        {
            var costLines = vector.CostLines
                .Select(c => new CostLine { Type = c.Type, Id = c.Id, Count = c.Count })
                .ToList();

            string actual = VendorOfferHasher.ComputeOfferId(
                vector.OutputItemId,
                vector.OutputCount,
                costLines,
                vector.MerchantName,
                vector.Locations,
                vector.DailyCap,
                vector.WeeklyCap,
                vector.HomesteadTier,
                vector.SeasonalCap);

            Assert.Equal(vector.ExpectedOfferId, actual);
        }

        // Trip-wire for the fixture itself (mirrors the repo's established
        // exact-count seed-pin convention, KNOWN-ISSUES DO-NOT-TOUCH #13):
        // if this drops, a row went missing from tests/shared/
        // vendor_offer_hasher_vectors.json without anyone noticing the
        // Theory silently ran fewer cases.
        [Fact]
        public void GoldenVectors_FixtureHasExpectedRowCount()
        {
            Assert.Equal(19, VendorOfferHasherVectorFixture.Load().Count);
        }
    }
}
