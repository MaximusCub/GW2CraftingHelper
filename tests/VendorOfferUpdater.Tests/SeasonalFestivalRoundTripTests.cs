using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using Xunit;
// FindRepoFile comes from Helpers/RepoFileLocator.cs.
using static VendorOfferUpdater.Tests.Helpers.RepoFileLocator;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// opportunity-notes (SEASONAL VENDOR TIP, review-fix): guards the
    /// exact regression the review found - VendorOfferUpdater.Models.
    /// VendorOffer used to have no SeasonalFestival property, so a
    /// --merge-into run (Program.MergeIntoBaseline, reached via
    /// Program.cs's deserialize-the-whole-baseline-through-this-model then
    /// re-serialize flow) would silently drop the tag from every offer it
    /// re-serializes, even one scraping an unrelated merchant - with no
    /// OfferId change to make the loss noticeable (SeasonalFestival is
    /// deliberately not hashed by VendorOfferHasher). Loads the REAL
    /// shipped ref/vendor_offers.json (not a fixture) through the tool's
    /// own model, exactly like Program.cs's --merge-into baseline read,
    /// merges in an unrelated fresh merchant's offer, and asserts the
    /// three known Candy Corn Vendor (Weekly) ecto offers still carry
    /// seasonalFestival afterward.
    /// </summary>
    public class SeasonalFestivalRoundTripTests
    {
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        [Fact]
        public void ShippedBaseline_SeasonalFestivalTag_SurvivesUnrelatedMergeIntoRun()
        {
            string path = FindRepoFile(Path.Combine("ref", "vendor_offers.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/vendor_offers.json by walking up from the test assembly's directory.");

            string baselineJson = File.ReadAllText(path);
            var baseline = JsonSerializer.Deserialize<VendorOfferDataset>(baselineJson, ReadOptions);
            Assert.NotNull(baseline);

            var seasonalBefore = baseline!.Offers
                .Where(o => !string.IsNullOrEmpty(o.SeasonalFestival))
                .ToList();

            // Pin the exact three known rows (repo invariant precedent:
            // this module seeds exactly these three Candy Corn Vendor
            // (Weekly) ecto offers, see docs/KNOWN-ISSUES.md) - a count
            // drop here means the DESERIALIZE side already lost the tag,
            // before MergeIntoBaseline is even involved.
            Assert.Equal(3, seasonalBefore.Count);
            Assert.All(seasonalBefore, o => Assert.Equal("Candy Corn Vendor (Weekly)", o.MerchantName));
            Assert.All(seasonalBefore, o => Assert.Equal("halloween", o.SeasonalFestival));

            // Simulate a --merge-into run that re-scrapes a completely
            // unrelated merchant - the exact scenario the finding
            // describes ("even one scraping an unrelated merchant").
            var fresh = new List<VendorOffer>
            {
                new VendorOffer
                {
                    OfferId = "fresh-unrelated-merchant-offer",
                    OutputItemId = 12345,
                    OutputCount = 1,
                    CostLines = new List<CostLine>(),
                    MerchantName = "Some Unrelated Merchant",
                    Locations = new List<string>()
                }
            };

            var result = Program.MergeIntoBaseline(baseline.Offers, fresh);

            var seasonalAfter = result.Merged
                .Where(o => !string.IsNullOrEmpty(o.SeasonalFestival))
                .ToList();

            Assert.Equal(3, seasonalAfter.Count);
            foreach (var offerId in new[]
            {
                "accd0339ca102a6c8250d42a629c486fef2f0717b89c4e0a597918ba518c6c9a",
                "cd7b951101a369470d65dadc145da9e1fb5b94485a17c2846367b8cf9c901b62",
                "db02003a8801af4952b6dfdcb89cc1965db1369dfd13d8bad2fc84eedc0223c1"
            })
            {
                var offer = result.Merged.SingleOrDefault(o => o.OfferId == offerId);
                Assert.True(offer != null, $"Offer {offerId} should survive an unrelated --merge-into run untouched.");
                Assert.Equal("halloween", offer!.SeasonalFestival);
            }
        }
    }
}
