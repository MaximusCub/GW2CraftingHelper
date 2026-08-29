using System.Collections.Generic;
using System.Linq;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using VendorOfferUpdater.Tests.Helpers;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// Absolute-value net for VendorOfferHasher: the only test that pins what
    /// the digests actually ARE, rather than how they relate.
    /// VendorOfferHasherTests.cs (same directory) covers relative behavior
    /// (equal/not-equal across varied inputs), which a self-consistent mistake
    /// such as renaming a segment or reordering the composition passes
    /// unchanged while silently rekeying all 59,414 rows of
    /// ref/vendor_offers.json. This file asserts against the fixed hex digests
    /// in tests/shared/vendor_offer_hasher_vectors.json, so such a change has
    /// to be made deliberately, by regenerating the fixture.
    /// <para>
    /// The "all_optional_fields_distinct_field_order_guard" row exists for one
    /// specific footgun: reordering or mis-slotting the trailing dailyCap/
    /// weeklyCap/homesteadTier/seasonalCap parameters, which every other row's
    /// mostly-null optionals would not catch. Why the fixture lives in
    /// tests/shared/, outside either project's Helpers/:
    /// docs/ARCHITECTURE.md section T.7.
    /// </para>
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
        // exact-count seed-pin convention, KNOWN-ISSUES #13):
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
