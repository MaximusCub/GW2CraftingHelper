using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GW2CraftingHelper.Services.Recipes
{
    internal class RecipeSearchSeedData
    {
        public int SchemaVersion { get; set; }

        public Dictionary<string, List<int>> Searches { get; set; }
    }

    internal class RecipeSeedData
    {
        public int SchemaVersion { get; set; }

        public List<RawRecipe> Recipes { get; set; }
    }

    /// <summary>
    /// One seed file's integrity record: how many rows the seeder wrote and
    /// the SHA-256 of the bytes it wrote them as.
    /// <para>
    /// This exists so the tests that guard the shipped corpora can pin a
    /// number the SEEDER produced rather than a literal a contributor typed.
    /// The old form (Assert.Equal(14966, recipes.Count)) tripped on any
    /// change but could not tell "the game shipped four new recipes" from
    /// "the seeder dropped 200 rows and gained 204", and it taught every
    /// contributor - human or agent - that the way to green a failing seed
    /// test is to edit the expected number. Against a manifest, a
    /// hand-edited seed fails and a legitimately reseeded one passes,
    /// because the count and the digest can only move together.
    /// </para>
    /// </summary>
    internal class SeedFileIntegrity
    {
        /// <summary>File name, relative to the seed directory.</summary>
        public string Name { get; set; }

        /// <summary>Rows the seeder wrote into it.</summary>
        public int RowCount { get; set; }

        /// <summary>Lowercase hex SHA-256 of the file's bytes.</summary>
        public string Sha256 { get; set; }
    }

    internal class RecipeSeedManifest
    {
        public int SeedVersion { get; set; }

        public int Gw2BuildId { get; set; }

        public string CreatedUtc { get; set; }

        /// <summary>
        /// Integrity records for the seed files this manifest was written
        /// beside. Null on manifests written before this field existed -
        /// staleness detection never reads it, so an old manifest still
        /// loads.
        /// </summary>
        public List<SeedFileIntegrity> Files { get; set; }
    }

    internal class RecipeOverlayManifest
    {
        /// <summary>
        /// 2 since the staleness policy: learned negatives are no longer
        /// stored and the manifest carries the verification stamp below.
        /// Deserializes as 0 from a v1 manifest, which never had the field.
        /// </summary>
        public int SchemaVersion { get; set; }

        public int Gw2BuildId { get; set; }

        /// <summary>
        /// The game build the corpus was last verified against via the
        /// /v2/recipes id-list probe; 0 = never verified.
        /// </summary>
        public int NegativesVerifiedBuildId { get; set; }

        /// <summary>
        /// Corpus size at that verification, so a module update swapping
        /// the shipped seed (or a user deleting recipe_cache/) re-arms the
        /// probe even when the game build has not moved.
        /// </summary>
        public int VerifiedKnownRecipeCount { get; set; }

        public string UpdatedUtc { get; set; }
    }

    internal static class RecipeCacheSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        public static Dictionary<int, IReadOnlyList<int>> LoadSearchSeed(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // See VendorOfferLoader.Load for why this reads the UTF-8 bytes
            // directly (via DeserializeAsync, blocked synchronously) instead
            // of StreamReader.ReadToEnd() + Deserialize<string>: it avoids
            // ReadToEnd's full UTF-16 string materialization (and the
            // internal UTF-8 re-encoding System.Text.Json performs to parse
            // a string) on this seed file.
            var data = JsonSerializer.DeserializeAsync<RecipeSearchSeedData>(stream, Options)
                .GetAwaiter().GetResult();
            if (data?.Searches == null)
            {
                return new Dictionary<int, IReadOnlyList<int>>();
            }

            var result = new Dictionary<int, IReadOnlyList<int>>();
            foreach (var kvp in data.Searches)
            {
                int key = int.Parse(kvp.Key, CultureInfo.InvariantCulture);
                result[key] = kvp.Value?.AsReadOnly()
                              ?? (IReadOnlyList<int>)Array.Empty<int>();
            }

            return result;
        }

        public static Dictionary<int, RawRecipe> LoadRecipeSeed(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // See VendorOfferLoader.Load for why this reads the UTF-8 bytes
            // directly (via DeserializeAsync, blocked synchronously) instead
            // of StreamReader.ReadToEnd() + Deserialize<string>: it avoids
            // ReadToEnd's full UTF-16 string materialization (and the
            // internal UTF-8 re-encoding System.Text.Json performs to parse
            // a string) on this seed file.
            var data = JsonSerializer.DeserializeAsync<RecipeSeedData>(stream, Options)
                .GetAwaiter().GetResult();
            if (data?.Recipes == null)
            {
                return new Dictionary<int, RawRecipe>();
            }

            var result = new Dictionary<int, RawRecipe>();
            foreach (var recipe in data.Recipes)
            {
                result[recipe.Id] = recipe;
            }

            return result;
        }

        public static T LoadManifest<T>(Stream stream) where T : class, new()
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
            }
        }

        public static string SerializeSearches(
            IReadOnlyDictionary<int, IReadOnlyList<int>> searches)
        {
            var data = new RecipeSearchSeedData
            {
                SchemaVersion = 1,
                Searches = new Dictionary<string, List<int>>(),
            };

            foreach (var kvp in searches.OrderBy(k => k.Key))
            {
                string key = kvp.Key.ToString(CultureInfo.InvariantCulture);
                data.Searches[key] = new List<int>(kvp.Value);
            }

            return JsonSerializer.Serialize(data, Options);
        }

        public static string SerializeRecipes(IReadOnlyDictionary<int, RawRecipe> recipes)
        {
            var data = new RecipeSeedData
            {
                SchemaVersion = 1,
                Recipes = recipes.Values.OrderBy(r => r.Id).ToList(),
            };

            return JsonSerializer.Serialize(data, Options);
        }

        public static string SerializeManifest<T>(T manifest)
        {
            return JsonSerializer.Serialize(manifest, Options);
        }

        /// <summary>
        /// Lowercase hex SHA-256 of a file's bytes - the one definition the
        /// seeders write and the seed tests check against, so neither can
        /// drift into its own idea of the digest.
        /// </summary>
        public static string HashFile(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] digest = sha.ComputeHash(stream);
                var text = new System.Text.StringBuilder(digest.Length * 2);
                for (int i = 0; i < digest.Length; i++)
                {
                    text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return text.ToString();
            }
        }
    }
}
