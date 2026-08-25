using System.Collections.Generic;
using System.Linq;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using VendorOfferUpdater.Tests.Helpers;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// Absolute-value net for VendorOfferHasher: the only test that pins
    /// what the digests actually ARE, rather than how they relate.
    /// VendorOfferHasherTests.cs (same directory) covers relative behavior
    /// (equal/not-equal across varied inputs), which a self-consistent
    /// mistake - renaming a segment, reordering the composition - passes
    /// unchanged while silently rekeying all 59,414 rows of
    /// ref/vendor_offers.json. This file asserts against the fixed hex
    /// digests in tests/shared/vendor_offer_hasher_vectors.json, so such a
    /// change has to be made deliberately, by regenerating the fixture.
    /// <para>
    /// The fixture was originally a CROSS-PROJECT net: the module carried
    /// its own copy of the hasher in Services/, and both suites replayed
    /// these same rows so the two copies could not drift. That copy had no
    /// callers anywhere in the module and has been deleted, leaving one
    /// implementation - so the fixture's job is now regression pinning over
    /// time rather than agreement between two files. It stays where it is
    /// (tests/shared/, outside either project's Helpers/) because it is
    /// still the right home for a hash contract that keys shipped data, and
    /// because a second consumer may return.
    /// </para>
    /// <para>
    /// The specific footgun the
    /// "all_optional_fields_distinct_field_order_guard" row exists for is
    /// reordering or mis-slotting the trailing dailyCap/weeklyCap/
    /// homesteadTier/seasonalCap parameters, which every other row's
    /// mostly-null optionals would not catch.
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
