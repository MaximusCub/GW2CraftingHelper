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
    /// merges in an unrelated fresh merchant's offer, and asserts every
    /// known tagged offer still carries seasonalFestival afterward.
    ///
    /// Festival-vendor auto-tagging follow-up (2026-08-16): the shipped
    /// baseline now carries seasonalFestival on 57 offers across all six
    /// known festivals, not just the original three hand-tagged Candy Corn
    /// Vendor (Weekly) ecto offers - Dragon Bash Merchant (Weekly),
    /// Wintersday Trader (Weekly), Festival Rewards Vendor (Weekly),
    /// Gauntlet Ticket Vendor, New Year Vendor, and Super Adventure Box
    /// Weekly Trader were live-tagged via a scoped
    /// --tag-seasonal-festivals --merge-into run targeting exactly those
    /// six merchants (Candy Corn Vendor (Weekly) deliberately excluded
    /// from that scoped query, so its three original offer IDs - and every
    /// other one of its nine offers - stay byte-for-byte identical to what
    /// was hand-tagged before; a fresh scrape of ANY merchant recomputes
    /// new OfferIds for that merchant, per VendorOfferHasher's own doc
    /// comment on the M37/Astral Acclaim hash-format migration, so
    /// touching Candy Corn Vendor (Weekly) in that pass would have broken
    /// the "3 known offer IDs survive identically" requirement). See
    /// docs/KNOWN-ISSUES.md for the full partial-coverage note (thousands
    /// of non-festival vendor pages remain untagged - this pass only
    /// covered the known festival vendor list, not a full re-scrape).
    /// </summary>
    public class SeasonalFestivalRoundTripTests
    {
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // The three ORIGINAL hand-tagged Candy Corn Vendor (Weekly) ecto
        // offer IDs - deliberately excluded from the festival-vendor
        // auto-tagging live run (see class doc comment) so these three
        // exact hashes must never change.
        private static readonly string[] OriginalCandyCornOfferIds =
        {
            "accd0339ca102a6c8250d42a629c486fef2f0717b89c4e0a597918ba518c6c9a",
            "cd7b951101a369470d65dadc145da9e1fb5b94485a17c2846367b8cf9c901b62",
            "db02003a8801af4952b6dfdcb89cc1965db1369dfd13d8bad2fc84eedc0223c1"
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

            // Pin the exact known count (a drop here means the
            // DESERIALIZE side already lost tags, before MergeIntoBaseline
            // is even involved) and the exact set of festival keys now
            // seeded - see class doc comment for the six-festival live run
            // that produced this count.
            Assert.Equal(57, seasonalBefore.Count);
            Assert.All(
                seasonalBefore,
                o => Assert.Contains(
                    o.SeasonalFestival,
                    new[]
                    {
                        "halloween", "dragonbash", "wintersday",
                        "festivalofthefourwinds", "lunarnewyear", "superadventurefestival"
                    }));
            Assert.Equal(3, seasonalBefore.Count(o => o.MerchantName == "Candy Corn Vendor (Weekly)"));
            Assert.All(
                seasonalBefore.Where(o => o.MerchantName == "Candy Corn Vendor (Weekly)"),
                o => Assert.Equal("halloween", o.SeasonalFestival));

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

            Assert.Equal(57, seasonalAfter.Count);
            foreach (var offerId in OriginalCandyCornOfferIds)
            {
                var offer = result.Merged.SingleOrDefault(o => o.OfferId == offerId);
                Assert.True(offer != null, $"Offer {offerId} should survive an unrelated --merge-into run untouched.");
                Assert.Equal("halloween", offer!.SeasonalFestival);
            }
        }
    }
}
