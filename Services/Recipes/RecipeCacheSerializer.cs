using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace GW2CraftingHelper.Services.Recipes
{
    public class RecipeSearchSeedData
    {
        public int SchemaVersion { get; set; }
        public Dictionary<string, List<int>> Searches { get; set; }
    }

    public class RecipeSeedData
    {
        public int SchemaVersion { get; set; }
        public List<RawRecipe> Recipes { get; set; }
    }

    public class RecipeSeedManifest
    {
        public int SeedVersion { get; set; }
        public int Gw2BuildId { get; set; }
        public string CreatedUtc { get; set; }
    }

    public class RecipeOverlayManifest
    {
        public int Gw2BuildId { get; set; }
        public string UpdatedUtc { get; set; }
    }

    public static class RecipeCacheSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
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
                Searches = new Dictionary<string, List<int>>()
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
                Recipes = recipes.Values.OrderBy(r => r.Id).ToList()
            };

            return JsonSerializer.Serialize(data, Options);
        }

        public static string SerializeManifest<T>(T manifest)
        {
            return JsonSerializer.Serialize(manifest, Options);
        }
    }
}
