using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TaimisToolbench.Services.Recipes;
using Xunit;

namespace TaimisToolbench.Tests.Helpers
{
    /// <summary>
    /// Checks a shipped seed corpus against the manifest its own seeder
    /// wrote beside it.
    /// <para>
    /// The tests that guard ref/*.json used to pin exact row counts as
    /// literals - Assert.Equal(14966, recipes.Count) and four more like it -
    /// with the churn history kept as changelog comments above them. That
    /// form trips on any change but cannot distinguish "the game shipped
    /// four new recipes" from "the seeder dropped 200 rows and gained 204",
    /// and the only remedy it offers a contributor is to edit the expected
    /// number until the run is green.
    /// </para>
    /// <para>
    /// Against the manifest, the count and the digest can only move
    /// together, and only a seeder run can move them: a hand-edited seed
    /// fails on the digest, a legitimately reseeded corpus passes because
    /// the same run rewrote both, and the diff a reviewer reads is the small
    /// machine-generated manifest rather than a number inside a test.
    /// </para>
    /// </summary>
    internal static class ShippedSeedManifest
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Asserts that a seed file written by tools/TaimisToolbench.RecipeSeeder
        /// still has the row count and bytes its manifest records, and that
        /// the caller loaded exactly that many rows out of it.
        /// </summary>
        public static void AssertRecipeSeedMatches(string fileName, int loadedRowCount)
        {
            string manifestPath = Locate(Path.Combine("ref", "recipe_seed_manifest.json"));
            RecipeSeedManifest manifest;
            using (var stream = File.OpenRead(manifestPath))
            {
                manifest = JsonSerializer.Deserialize<RecipeSeedManifest>(
                    ReadAllBytesSkippingBom(stream), Options);
            }

            Assert.NotNull(manifest.Files);
            SeedFileIntegrity record = manifest.Files.SingleOrDefault(
                f => string.Equals(f.Name, fileName, StringComparison.Ordinal));
            Assert.True(
                record != null,
                "ref/recipe_seed_manifest.json has no integrity record for " + fileName
                    + ". Re-run tools/TaimisToolbench.RecipeSeeder, which writes one "
                    + "per seed file it produces.");

            AssertFileMatches(fileName, record.RowCount, record.Sha256, loadedRowCount);
        }

        /// <summary>
        /// Same check for ref/vendor_offers.json, whose manifest is written
        /// by tools/VendorOfferUpdater and carries one file rather than a
        /// list.
        /// </summary>
        public static void AssertVendorOffersMatch(int loadedRowCount)
        {
            string manifestPath = Locate(Path.Combine("ref", "vendor_offers_manifest.json"));
            VendorManifestShape manifest;
            using (var stream = File.OpenRead(manifestPath))
            {
                manifest = JsonSerializer.Deserialize<VendorManifestShape>(
                    ReadAllBytesSkippingBom(stream), Options);
            }

            Assert.False(
                string.IsNullOrEmpty(manifest.Sha256),
                "ref/vendor_offers_manifest.json carries no sha256. Re-run "
                    + "tools/VendorOfferUpdater, which writes one beside the offer count.");

            AssertFileMatches(
                "vendor_offers.json", manifest.OfferCount, manifest.Sha256, loadedRowCount);
        }

        private static void AssertFileMatches(
            string fileName, int manifestRowCount, string manifestSha, int loadedRowCount)
        {
            string path = Locate(Path.Combine("ref", fileName));

            Assert.True(
                manifestSha == RecipeCacheSerializer.HashFile(path),
                "ref/" + fileName + " does not match the digest its manifest records.\n"
                    + "  manifest: " + manifestSha + "\n"
                    + "  on disk:  " + RecipeCacheSerializer.HashFile(path) + "\n"
                    + "A seed corpus is machine-generated: re-run its seeder rather than "
                    + "editing either the file or this expectation.");

            Assert.Equal(manifestRowCount, loadedRowCount);
        }

        private static string Locate(string relativePath)
        {
            string path = RepoFileLocator.FindRepoFile(relativePath);
            Assert.False(
                string.IsNullOrEmpty(path),
                "Could not locate " + relativePath.Replace('\\', '/')
                    + " by walking up from the test assembly's directory.");
            return path;
        }

        // The recipe manifest is written with a UTF-8 BOM; System.Text.Json
        // rejects one at the head of a document.
        private static ReadOnlySpan<byte> ReadAllBytesSkippingBom(Stream stream)
        {
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                byte[] bytes = buffer.ToArray();
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    return new ReadOnlySpan<byte>(bytes, 3, bytes.Length - 3);
                }

                return bytes;
            }
        }

        private class VendorManifestShape
        {
            public int OfferCount { get; set; }

            public string Sha256 { get; set; }
        }
    }
}
