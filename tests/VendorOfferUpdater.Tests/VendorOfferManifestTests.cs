using System.IO;
using System.Text.Json;
using VendorOfferUpdater;
using VendorOfferUpdater.Models;
using Xunit;
using static VendorOfferUpdater.Tests.Helpers.RepoFileLocator;

namespace VendorOfferUpdater.Tests
{
    /// <summary>
    /// ref/vendor_offers_manifest.json is the provenance record that lets
    /// ref/vendor_offers.json stay byte-stable across a no-op refresh. These
    /// tests pin the shipped manifest against the writer that produces it, so
    /// the checked-in file cannot drift from what the next run would emit.
    /// </summary>
    public class VendorOfferManifestTests
    {
        private static string LoadShippedManifestJson()
        {
            string path = FindRepoFile(Path.Combine("ref", "vendor_offers_manifest.json"));
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate ref/vendor_offers_manifest.json by walking up from the test assembly's directory.");
            return File.ReadAllText(path);
        }

        [Fact]
        public void ShippedManifest_RoundTripsThroughTheProductionWriterByteForByte()
        {
            string shipped = LoadShippedManifestJson();

            var manifest = JsonSerializer.Deserialize<VendorOfferManifest>(
                shipped,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(manifest);

            Assert.Equal(shipped, Program.SerializeManifest(manifest!));
        }

        /// <summary>
        /// The count in the manifest is the only cheap answer to "did that
        /// refresh drop half the dataset on the floor" without parsing 14.8MB.
        /// It is worth nothing if it is allowed to go stale.
        /// </summary>
        [Fact]
        public void ShippedManifest_OfferCountMatchesTheShippedDataset()
        {
            var manifest = JsonSerializer.Deserialize<VendorOfferManifest>(
                LoadShippedManifestJson(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(manifest);

            string datasetPath = FindRepoFile(Path.Combine("ref", "vendor_offers.json"));
            Assert.False(string.IsNullOrEmpty(datasetPath));

            var dataset = JsonSerializer.Deserialize<VendorOfferDataset>(
                File.ReadAllText(datasetPath),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
            Assert.NotNull(dataset);

            Assert.Equal(dataset!.Offers.Count, manifest!.OfferCount);
            Assert.Equal(dataset.SchemaVersion, manifest.SchemaVersion);
            Assert.Equal(dataset.Source, manifest.Source);
        }

        /// <summary>
        /// System.Text.Json's WriteIndented emits Environment.NewLine, so
        /// without the normalization in SerializeManifest the same content
        /// would be CRLF on a Windows refresh and LF on a Linux one - a
        /// spurious diff in the file whose job is to prove there was none.
        /// </summary>
        [Fact]
        public void SerializeManifest_UsesLfRegardlessOfHostPlatform()
        {
            string json = Program.SerializeManifest(new VendorOfferManifest
            {
                Source = "gw2wiki-smw",
                OfferCount = 3,
                GeneratedAt = "2026-01-01T00:00:00.0000000Z"
            });

            Assert.DoesNotContain("\r", json);
            Assert.EndsWith("}\n", json);
        }

        [Theory]
        [InlineData("ref/vendor_offers.json", "ref/vendor_offers_manifest.json")]
        [InlineData("vendor_offers.json", "vendor_offers_manifest.json")]
        public void ManifestPathFor_PutsTheManifestBesideItsDataset(string dataset, string expected)
        {
            string actual = Program.ManifestPathFor(dataset.Replace('/', Path.DirectorySeparatorChar));

            Assert.Equal(expected.Replace('/', Path.DirectorySeparatorChar), actual);
        }
    }
}
