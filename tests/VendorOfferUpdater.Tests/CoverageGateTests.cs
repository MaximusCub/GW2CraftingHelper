using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using Xunit;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// Rows a scrape never fetched are invisible to the merge step's data-loss
    /// guard, which only protects rows already in the baseline. This gate is
    /// what compares the dataset about to be written against the one on disk
    /// and refuses the write when the run lost coverage.
    /// </summary>
    public class CoverageGateTests
    {
        private static VendorOffer Offer(int itemId, string merchant)
        {
            var offer = new VendorOffer
            {
                OutputItemId = itemId,
                OutputCount = 1,
                MerchantName = merchant,
                Locations = null,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = 1, Count = 100 },
                },
            };

            offer.OfferId = VendorOfferHasher.ComputeOfferId(
                offer.OutputItemId,
                offer.OutputCount,
                offer.CostLines,
                offer.MerchantName,
                offer.Locations,
                offer.DailyCap,
                offer.WeeklyCap,
                offer.HomesteadTier,
                offer.SeasonalCap);

            return offer;
        }

        private static List<VendorOffer> Dataset(int merchants, int offersEach)
        {
            var offers = new List<VendorOffer>();
            for (int m = 0; m < merchants; m++)
            {
                for (int i = 0; i < offersEach; i++)
                {
                    offers.Add(Offer((m * 100) + i, "Merchant " + m));
                }
            }

            return offers;
        }

        private static List<UnresolvedSection> OneUnresolved()
        {
            return new List<UnresolvedSection>
            {
                new UnresolvedSection
                {
                    Kind = "partition",
                    Label = "As",
                    Prefix = "As",
                    Condition = "[[Sells item::+]][[Has vendor::~As*]]",
                    ErrorCode = "maxlag",
                    Reason = "Waiting for 10.64.16.79: 6.9 seconds lagged.",
                    Attempts = 5,
                },
            };
        }

        [Fact]
        public void UnchangedDataset_Passes()
        {
            var previous = Dataset(20, 5);
            var next = Dataset(20, 5);

            var report = CoverageGate.Evaluate(
                previous, next, Array.Empty<UnresolvedSection>(),
                CoverageGate.DefaultMaxDropFraction, false);

            Assert.False(report.Blocked);
            Assert.Empty(report.Reasons);
            Assert.Equal(100, report.NewOfferCount);
            Assert.Equal(20, report.NewMerchantCount);
        }

        [Fact]
        public void SmallCorrection_Passes()
        {
            var previous = Dataset(20, 5);
            var next = Dataset(20, 5);
            next.RemoveAt(0);

            var report = CoverageGate.Evaluate(
                previous, next, Array.Empty<UnresolvedSection>(),
                CoverageGate.DefaultMaxDropFraction, false);

            Assert.False(report.Blocked);
        }

        [Fact]
        public void LargeOfferDrop_BlocksTheWrite()
        {
            var previous = Dataset(20, 5);
            var next = Dataset(20, 5).Take(80).ToList();

            var report = CoverageGate.Evaluate(
                previous, next, Array.Empty<UnresolvedSection>(),
                CoverageGate.DefaultMaxDropFraction, false);

            Assert.True(report.Blocked);
            Assert.Contains(report.Reasons, r => r.StartsWith("offers fell", StringComparison.Ordinal));
        }

        [Fact]
        public void LostMerchant_IsNamedInTheReport()
        {
            var previous = Dataset(20, 5);
            var next = previous.Where(o => o.MerchantName != "Merchant 7").ToList();

            var report = CoverageGate.Evaluate(
                previous, next, Array.Empty<UnresolvedSection>(),
                CoverageGate.DefaultMaxDropFraction, false);

            Assert.True(report.Blocked);
            Assert.Contains("Merchant 7", report.MerchantsLost);
            Assert.Contains(
                report.Reasons,
                r => r.StartsWith("distinct merchants fell", StringComparison.Ordinal));
            Assert.Contains("Merchant 7", CoverageGate.Format(report), StringComparison.Ordinal);
        }

        [Fact]
        public void UnresolvedSection_BlocksEvenAnIdenticalDataset()
        {
            var previous = Dataset(20, 5);
            var next = Dataset(20, 5);

            var report = CoverageGate.Evaluate(
                previous, next, OneUnresolved(), CoverageGate.DefaultMaxDropFraction, false);

            Assert.True(report.Blocked);
            Assert.Equal(1, report.UnresolvedCount);
            Assert.Contains(report.Reasons, r => r.Contains("unresolved", StringComparison.Ordinal));
            Assert.Contains("BLOCKED", CoverageGate.Format(report), StringComparison.Ordinal);
        }

        [Fact]
        public void Override_LetsTheWriteThroughAndStillReportsWhy()
        {
            var previous = Dataset(20, 5);
            var next = Dataset(20, 5).Take(40).ToList();

            var report = CoverageGate.Evaluate(
                previous, next, OneUnresolved(), CoverageGate.DefaultMaxDropFraction, true);

            Assert.False(report.Blocked);
            Assert.True(report.Overridden);
            // The unresolved section, the lost offers and the lost merchants.
            Assert.Equal(3, report.Reasons.Count);
            Assert.Contains("OVERRIDDEN", CoverageGate.Format(report), StringComparison.Ordinal);
        }

        [Fact]
        public void FirstRunWithNoPreviousDataset_Passes()
        {
            var report = CoverageGate.Evaluate(
                null, Dataset(20, 5), Array.Empty<UnresolvedSection>(),
                CoverageGate.DefaultMaxDropFraction, false);

            Assert.False(report.Blocked);
            Assert.Equal(0, report.OldOfferCount);
        }

        [Fact]
        public void GrowingDataset_Passes()
        {
            var previous = Dataset(20, 5);
            var next = Dataset(25, 5);

            var report = CoverageGate.Evaluate(
                previous, next, Array.Empty<UnresolvedSection>(),
                CoverageGate.DefaultMaxDropFraction, false);

            Assert.False(report.Blocked);
            Assert.Empty(report.MerchantsLost);
        }

        // -- The sidecar --------------------------------------------
        [Fact]
        public async Task Sidecar_NamesTheSectionsAndTheirQueries()
        {
            string dir = Path.Combine(Path.GetTempPath(), "vou-coverage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string datasetPath = Path.Combine(dir, "vendor_offers.json");

                var written = await UnresolvedSectionFile.SaveAsync(datasetPath, OneUnresolved());

                Assert.Equal(Path.Combine(dir, "vendor_offers_unresolved.json"), written);

                using var doc = JsonDocument.Parse(File.ReadAllText(written!));
                var section = doc.RootElement.GetProperty("sections")[0];
                Assert.Equal("partition", section.GetProperty("kind").GetString());
                Assert.Equal(
                    "[[Sells item::+]][[Has vendor::~As*]]",
                    section.GetProperty("condition").GetString());
                Assert.Equal(5, section.GetProperty("attempts").GetInt32());
                Assert.False(string.IsNullOrEmpty(
                    doc.RootElement.GetProperty("generatedAt").GetString()));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public async Task Sidecar_IsRemovedByACleanRun()
        {
            string dir = Path.Combine(Path.GetTempPath(), "vou-coverage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string datasetPath = Path.Combine(dir, "vendor_offers.json");
                await UnresolvedSectionFile.SaveAsync(datasetPath, OneUnresolved());

                var written = await UnresolvedSectionFile.SaveAsync(
                    datasetPath, Array.Empty<UnresolvedSection>());

                Assert.Null(written);
                Assert.False(File.Exists(Path.Combine(dir, "vendor_offers_unresolved.json")));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
