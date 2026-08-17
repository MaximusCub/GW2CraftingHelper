using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VendorOfferUpdater.Tests.Helpers
{
    /// <summary>
    /// Loads the cross-project golden-vector fixture at
    /// tests/shared/vendor_offer_hasher_vectors.json. See GW2CraftingHelper.Tests/Helpers/VendorOfferHasherVectorFixture.cs
    /// (this project's counterpart) for why the fixture lives outside both
    /// test projects rather than under ref/ or either project's own
    /// Helpers/.
    /// </summary>
    public static class VendorOfferHasherVectorFixture
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public static IReadOnlyList<VendorOfferHasherVector> Load()
        {
            string path = RepoFileLocator.FindRepoFile(
                Path.Combine("tests", "shared", "vendor_offer_hasher_vectors.json"));
            if (string.IsNullOrEmpty(path))
            {
                throw new FileNotFoundException(
                    "Could not locate tests/shared/vendor_offer_hasher_vectors.json " +
                    "by walking up from the test assembly's directory.");
            }

            using (var stream = File.OpenRead(path))
            {
                var doc = JsonSerializer.Deserialize<VectorFixtureDocument>(stream, Options);
                return (IReadOnlyList<VendorOfferHasherVector>)doc?.Vectors
                    ?? new List<VendorOfferHasherVector>();
            }
        }

        private class VectorFixtureDocument
        {
            public int SchemaVersion { get; set; }
            public List<string> SourceHashers { get; set; }
            public List<VendorOfferHasherVector> Vectors { get; set; }
        }
    }

    /// <summary>
    /// One golden-vector row: fixed ComputeOfferId inputs plus the exact
    /// hex digest they must produce. See tests/shared/vendor_offer_hasher_vectors.json.
    /// </summary>
    public class VendorOfferHasherVector
    {
        public string Name { get; set; }
        public int OutputItemId { get; set; }
        public int OutputCount { get; set; }
        public List<CostLineVector> CostLines { get; set; }
        public string MerchantName { get; set; }
        public List<string> Locations { get; set; }
        public int? DailyCap { get; set; }
        public int? WeeklyCap { get; set; }
        public int? HomesteadTier { get; set; }
        public int? SeasonalCap { get; set; }
        public string ExpectedOfferId { get; set; }

        public override string ToString()
        {
            // Gives xUnit's Theory data display a readable row name instead
            // of the default ToString() dump.
            return Name;
        }
    }

    /// <summary>
    /// Project-neutral mirror of VendorOfferUpdater.Models.CostLine, so the
    /// fixture loader has no dependency on either production model type -
    /// each test project maps CostLineVector rows onto its own CostLine
    /// before calling its own VendorOfferHasher.ComputeOfferId.
    /// </summary>
    public class CostLineVector
    {
        public string Type { get; set; }
        public int Id { get; set; }
        public int Count { get; set; }
    }
}
