using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GW2CraftingHelper.Tests.Helpers
{
    /// <summary>
    /// Loads the cross-project golden-vector fixture at
    /// tests/shared/vendor_offer_hasher_vectors.json (M38 WP-19, tests
    /// T13). The fixture lives outside both test projects' own directories
    /// - not under either project's Helpers/, and deliberately not under
    /// ref/ (ref/ is shipped module seed data loaded at runtime; this file
    /// is test-only infrastructure with no runtime consumer) - so both
    /// GW2CraftingHelper.Tests and VendorOfferUpdater.Tests can load the
    /// exact same rows via their own RepoFileLocator-style walk-up. A
    /// divergence between Services/VendorOfferHasher.cs and
    /// tools/VendorOfferUpdater/VendorOfferHasher.cs now fails both suites
    /// against the same fixed expected hashes, instead of each suite only
    /// ever proving its own copy self-consistent.
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
                // System.Text.Json 5.0.0 (net461, this project's TFM) has no
                // synchronous Deserialize(Stream) overload, only
                // DeserializeAsync(Stream) - mirrors the same blocking-is-
                // safe rationale documented on Services/VendorOfferLoader.cs
                // (no SynchronizationContext to deadlock against here
                // either; this is a test helper, not the UI thread).
                var doc = JsonSerializer.DeserializeAsync<VectorFixtureDocument>(stream, Options)
                    .GetAwaiter().GetResult();
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
    /// Project-neutral mirror of GW2CraftingHelper.Models.CostLine, so the
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
