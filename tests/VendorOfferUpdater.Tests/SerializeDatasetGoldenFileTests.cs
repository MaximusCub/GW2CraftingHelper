using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using Xunit;
using static VendorOfferUpdater.Tests.Helpers.RepoFileLocator;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// Characterization tests for the writer that produces
    /// ref/vendor_offers.json. The shipped file IS the golden vector: a
    /// full read/write round-trip of it must reproduce it byte for byte,
    /// which pins the whole serialization contract at once - camelCase
    /// naming, no indentation, null members omitted, relaxed JSON escaping
    /// plus the non-ASCII re-escape, and the member order the file's
    /// leading bytes encode.
    /// <para>
    /// This is the only thing standing between a future serializer change
    /// and a 14.8MB single-line diff that no reviewer can read. Running the
    /// real scrape to find out costs ~15 minutes of live wiki traffic.
    /// </para>
    /// </summary>
    public class SerializeDatasetGoldenFileTests
    {
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private static string LoadShippedJson()
        {
            string path = FindRepoFile(Path.Combine("ref", "vendor_offers.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/vendor_offers.json by walking up from the test assembly's directory.");
            return File.ReadAllText(path);
        }

        private static VendorOfferDataset ReadShippedDataset(string json)
        {
            var dataset = JsonSerializer.Deserialize<VendorOfferDataset>(json, ReadOptions);
            Assert.NotNull(dataset);

            // Same fixup --merge-into applies before re-serializing a
            // baseline: an offer whose "locations" key was OMITTED
            // deserializes to an empty list, not null, and
            // DefaultIgnoreCondition.WhenWritingNull only omits null. Without
            // this, every location-less offer would round-trip as
            // "locations":[] and the comparison below would fail for a
            // reason that has nothing to do with the serializer.
            foreach (var offer in dataset!.Offers)
            {
                if (offer.Locations != null && offer.Locations.Count == 0)
                {
                    offer.Locations = null;
                }
            }

            return dataset;
        }

        [Fact]
        public void SerializeDataset_RoundTripOfShippedFile_ReproducesItByteForByte()
        {
            string shipped = LoadShippedJson();
            var dataset = ReadShippedDataset(shipped);

            string rewritten = Program.SerializeDataset(dataset);

            Assert.Equal(shipped.Length, rewritten.Length);
            Assert.Equal(shipped, rewritten);
        }

        /// <summary>
        /// The whole point of moving generatedAt into the sibling manifest: a
        /// refresh that scrapes identical data must produce identical bytes, so
        /// an unchanged `git status` is proof the run was a no-op. Asserting on
        /// the shipped file rather than a fixture means a future reintroduction
        /// of any run-scoped field into the payload fails here.
        /// </summary>
        [Fact]
        public void ShippedFile_CarriesNoRunScopedTimestamp()
        {
            Assert.DoesNotContain("\"generatedAt\"", LoadShippedJson());
        }

        [Fact]
        public void SerializeDataset_IsPureFunctionOfItsInput()
        {
            var dataset = new VendorOfferDataset
            {
                SchemaVersion = 1,
                Source = "gw2wiki-smw",
                Offers = new List<VendorOffer>
                {
                    new VendorOffer
                    {
                        OfferId = "abc",
                        OutputItemId = 1,
                        OutputCount = 1,
                        MerchantName = "Hearth's Glow & Co",
                        Locations = null,
                        CostLines = new List<CostLine>()
                    }
                }
            };

            Assert.Equal(
                Program.SerializeDataset(dataset),
                Program.SerializeDataset(dataset));
        }
    }
}
