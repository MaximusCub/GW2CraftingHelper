using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Recipes;

namespace GW2CraftingHelper.RecipeSeeder
{
    internal class ProfileItem
    {
        public string Name { get; set; }
        public int ItemId { get; set; }
    }

    internal class Program
    {
        private static int Main(string[] args)
        {
            return MainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task<int> MainAsync(string[] args)
        {
            int profile = -1;
            string outputDir = null;
            bool force = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--profile":
                        if (i + 1 < args.Length)
                        {
                            profile = int.Parse(args[++i], CultureInfo.InvariantCulture);
                        }
                        break;
                    case "--output-dir":
                        if (i + 1 < args.Length)
                        {
                            outputDir = args[++i];
                        }
                        break;
                    case "--force":
                        force = true;
                        break;
                }
            }

            if (profile < 0)
            {
                Console.Error.WriteLine(
                    "Usage: GW2CraftingHelper.RecipeSeeder " +
                    "--profile <n> [--output-dir <path>] [--force]");
                return 1;
            }

            var items = GetProfileItems(profile);
            if (items == null || items.Count == 0)
            {
                Console.Error.WriteLine($"Unknown profile: {profile}");
                return 1;
            }

            // Default output to repo ref/ directory
            if (string.IsNullOrEmpty(outputDir))
            {
                outputDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "ref");
            }

            // Check for existing files
            string searchPath = Path.Combine(outputDir, "recipe_search_seed.json");
            string recipesPath = Path.Combine(outputDir, "recipes_seed.json");
            string manifestPath = Path.Combine(outputDir, "recipe_seed_manifest.json");

            if (!force && (File.Exists(searchPath) || File.Exists(recipesPath)))
            {
                Console.Error.WriteLine(
                    "Seed files already exist. Use --force to overwrite.");
                return 1;
            }

            Console.WriteLine($"Profile {profile} | Output: {outputDir}");
            Console.WriteLine($"Items: {items.Count}");
            Console.WriteLine();

            using (var httpClient = new HttpClient())
            {
                // Build recipe API pipeline
                var rawApi = new Gw2RecipeApiClient(httpClient);
                var mfSource = new FileMysticForgeRecipeSource();
                var recipeApi = RecipeClientFactory.Create(rawApi, mfSource);

                // Use InMemoryRecipeCacheStore to collect all discovered data
                var cacheStore = new InMemoryRecipeCacheStore();
                var service = new RecipeService(recipeApi, cacheStore: cacheStore);

                // Build tree for each profile item
                foreach (var item in items)
                {
                    Console.Write($"  {item.Name} ({item.ItemId})...");
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    await service.BuildTreeAsync(item.ItemId, 1, CancellationToken.None);
                    sw.Stop();
                    Console.WriteLine($" {sw.ElapsedMilliseconds}ms");
                }

                Console.WriteLine();

                // Fetch GW2 build ID
                int gw2BuildId = 0;
                try
                {
                    gw2BuildId = await FetchBuildIdAsync(httpClient);
                    Console.WriteLine($"GW2 Build ID: {gw2BuildId}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: Could not fetch build ID: {ex.Message}");
                }

                // Extract collected data
                var allSearches = cacheStore.GetAllSearches();
                var allRecipes = cacheStore.GetAllRecipes();

                Console.WriteLine(
                    $"Collected: {allSearches.Count} search entries, " +
                    $"{allRecipes.Count} recipes");

                // Write seed files
                Directory.CreateDirectory(outputDir);

                string searchJson = RecipeCacheSerializer.SerializeSearches(allSearches);
                File.WriteAllText(searchPath, searchJson, Encoding.UTF8);

                string recipeJson = RecipeCacheSerializer.SerializeRecipes(allRecipes);
                File.WriteAllText(recipesPath, recipeJson, Encoding.UTF8);

                var manifest = new RecipeSeedManifest
                {
                    SeedVersion = 1,
                    Gw2BuildId = gw2BuildId,
                    CreatedUtc = DateTime.UtcNow.ToString("o")
                };
                string manifestJson = RecipeCacheSerializer.SerializeManifest(manifest);
                File.WriteAllText(manifestPath, manifestJson, Encoding.UTF8);

                Console.WriteLine();
                Console.WriteLine($"Written: {searchPath}");
                Console.WriteLine($"Written: {recipesPath}");
                Console.WriteLine($"Written: {manifestPath}");
            }

            return 0;
        }

        private static List<ProfileItem> GetProfileItems(int profile)
        {
            switch (profile)
            {
                case 1:
                    return new List<ProfileItem>
                    {
                        new ProfileItem { Name = "Gift of Fortune", ItemId = 19626 },
                        new ProfileItem { Name = "Zojja's Claymore", ItemId = 46762 }
                    };
                default:
                    return null;
            }
        }

        private static async Task<int> FetchBuildIdAsync(HttpClient httpClient)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var response = await httpClient.GetAsync(
                    "https://api.guildwars2.com/v2/build", cts.Token);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(json))
                {
                    return doc.RootElement.GetProperty("id").GetInt32();
                }
            }
        }
    }
}
