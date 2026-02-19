using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Recipes;

namespace GW2CraftingHelper.RecipeSeeder
{
    internal class Program
    {
        private const string BaseUrl = "https://api.guildwars2.com/v2";
        private const int BatchSize = 200;
        private const int MaxConcurrency = 4;

        private static int Main(string[] args)
        {
            return MainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task<int> MainAsync(string[] args)
        {
            string outputDir = null;
            bool force = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
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
            string itemNamePath = Path.Combine(outputDir, "item_name_seed.json");

            if (!force && (File.Exists(searchPath) || File.Exists(recipesPath)))
            {
                Console.Error.WriteLine(
                    "Seed files already exist. Use --force to overwrite.");
                return 1;
            }

            Console.WriteLine($"Output: {outputDir}");
            Console.WriteLine();

            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromMinutes(5);
                var totalSw = Stopwatch.StartNew();

                // Step 1: Fetch GW2 build ID
                int gw2BuildId = 0;
                try
                {
                    gw2BuildId = await FetchBuildIdAsync(httpClient);
                    Console.WriteLine($"GW2 Build ID: {gw2BuildId}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"Warning: Could not fetch build ID: {ex.Message}");
                }

                // Step 2: Fetch all recipe IDs from /v2/recipes
                Console.Write("Fetching recipe ID list...");
                var sw = Stopwatch.StartNew();
                var allRecipeIds = await FetchAllRecipeIdsAsync(httpClient);
                sw.Stop();
                Console.WriteLine(
                    $" {allRecipeIds.Count} recipes ({sw.ElapsedMilliseconds}ms)");

                // Step 3: Batch-fetch all recipe details
                Console.Write(
                    $"Fetching recipe details ({allRecipeIds.Count} recipes " +
                    $"in batches of {BatchSize}, concurrency {MaxConcurrency})...");
                sw.Restart();
                var allRecipes = await FetchAllRecipesAsync(
                    httpClient, allRecipeIds);
                sw.Stop();
                Console.WriteLine(
                    $" {allRecipes.Count} fetched ({sw.ElapsedMilliseconds}ms)");

                // Step 4: Build search index (outputItemId → recipeIds)
                var searchIndex = new Dictionary<int, List<int>>();
                foreach (var recipe in allRecipes.Values)
                {
                    if (!searchIndex.TryGetValue(
                        recipe.OutputItemId, out var list))
                    {
                        list = new List<int>();
                        searchIndex[recipe.OutputItemId] = list;
                    }
                    list.Add(recipe.Id);
                }

                // Step 5: Load and merge mystic forge recipes
                int mfCount = 0;
                try
                {
                    var mfSource = new FileMysticForgeRecipeSource();
                    using (var mfStream = mfSource.Open())
                    {
                        MergeMysticForgeRecipes(
                            mfStream, allRecipes, searchIndex, out mfCount);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"Warning: Could not load mystic forge recipes: {ex.Message}");
                }

                // Step 5b: Add negative search entries for leaf items
                // (ingredients that aren't the output of any recipe)
                var allIngredientIds = new HashSet<int>();
                foreach (var recipe in allRecipes.Values)
                {
                    foreach (var ing in recipe.Ingredients)
                    {
                        if (ing.Type == "Item")
                        {
                            allIngredientIds.Add(ing.Id);
                        }
                    }
                }

                int negativeCount = 0;
                foreach (var ingId in allIngredientIds)
                {
                    if (!searchIndex.ContainsKey(ingId))
                    {
                        searchIndex[ingId] = new List<int>();
                        negativeCount++;
                    }
                }

                // Sort search index entries for deterministic output
                foreach (var list in searchIndex.Values)
                {
                    list.Sort();
                }

                totalSw.Stop();
                Console.WriteLine();
                Console.WriteLine(
                    $"Total: {allRecipes.Count} recipes, " +
                    $"{searchIndex.Count} search entries " +
                    $"({mfCount} mystic forge, " +
                    $"{negativeCount} negative/leaf entries) " +
                    $"in {totalSw.ElapsedMilliseconds}ms");

                // Step 6: Convert and write seed files
                Directory.CreateDirectory(outputDir);

                var searches = searchIndex.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (IReadOnlyList<int>)kvp.Value.AsReadOnly());

                string searchJson = RecipeCacheSerializer.SerializeSearches(searches);
                File.WriteAllText(searchPath, searchJson, Encoding.UTF8);

                string recipeJson = RecipeCacheSerializer.SerializeRecipes(allRecipes);
                File.WriteAllText(recipesPath, recipeJson, Encoding.UTF8);

                var manifest = new RecipeSeedManifest
                {
                    SeedVersion = 1,
                    Gw2BuildId = gw2BuildId,
                    CreatedUtc = DateTime.UtcNow.ToString("o")
                };
                string manifestJson =
                    RecipeCacheSerializer.SerializeManifest(manifest);
                File.WriteAllText(manifestPath, manifestJson, Encoding.UTF8);

                // Step 7: Generate item name seed for search provider
                var craftableItemIds = searchIndex
                    .Where(kvp => kvp.Value.Count > 0)
                    .Select(kvp => kvp.Key)
                    .ToList();
                craftableItemIds.Sort();

                Console.Write(
                    $"Fetching item names ({craftableItemIds.Count} craftable items " +
                    $"in batches of {BatchSize})...");
                sw.Restart();
                var itemNames = await FetchItemNamesAsync(
                    httpClient, craftableItemIds);
                sw.Stop();
                Console.WriteLine(
                    $" {itemNames.Count} fetched ({sw.ElapsedMilliseconds}ms)");

                var itemNameJson = JsonSerializer.Serialize(
                    itemNames.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(e => new { id = e.Id, name = e.Name, icon = e.Icon }),
                    new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(itemNamePath, itemNameJson, Encoding.UTF8);

                Console.WriteLine();
                Console.WriteLine($"Written: {searchPath}");
                Console.WriteLine($"Written: {recipesPath}");
                Console.WriteLine($"Written: {manifestPath}");
                Console.WriteLine($"Written: {itemNamePath} ({itemNames.Count} items)");
            }

            return 0;
        }

        private static async Task<List<int>> FetchAllRecipeIdsAsync(
            HttpClient httpClient)
        {
            string json = await httpClient.GetStringAsync($"{BaseUrl}/recipes");
            return JsonSerializer.Deserialize<List<int>>(json);
        }

        private static async Task<Dictionary<int, RawRecipe>> FetchAllRecipesAsync(
            HttpClient httpClient, List<int> recipeIds)
        {
            var result = new Dictionary<int, RawRecipe>();
            var batches = new List<List<int>>();

            for (int i = 0; i < recipeIds.Count; i += BatchSize)
            {
                int count = Math.Min(BatchSize, recipeIds.Count - i);
                batches.Add(recipeIds.GetRange(i, count));
            }

            int completed = 0;
            int total = batches.Count;
            var gate = new object();

            using (var semaphore = new SemaphoreSlim(MaxConcurrency))
            {
                var tasks = batches.Select(async batch =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var recipes = await FetchRecipeBatchAsync(
                            httpClient, batch);

                        lock (gate)
                        {
                            foreach (var recipe in recipes)
                            {
                                result[recipe.Id] = recipe;
                            }

                            completed++;
                            if (completed % 10 == 0 || completed == total)
                            {
                                Console.Write(
                                    $"\r  Batches: {completed}/{total}   ");
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToList();

                await Task.WhenAll(tasks);
            }

            Console.WriteLine();
            return result;
        }

        private static async Task<List<RawRecipe>> FetchRecipeBatchAsync(
            HttpClient httpClient, List<int> ids)
        {
            string idsParam = string.Join(",",
                ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            string url = $"{BaseUrl}/recipes?ids={idsParam}";

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    string json = await httpClient.GetStringAsync(url);
                    return ParseRecipeBatch(json);
                }
                catch (HttpRequestException) when (attempt < 2)
                {
                    await Task.Delay(1000 * (attempt + 1));
                }
            }

            return new List<RawRecipe>();
        }

        private static List<RawRecipe> ParseRecipeBatch(string json)
        {
            var recipes = new List<RawRecipe>();
            using (var doc = JsonDocument.Parse(json))
            {
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    var recipe = new RawRecipe
                    {
                        Id = elem.GetProperty("id").GetInt32(),
                        OutputItemId = elem.GetProperty("output_item_id").GetInt32(),
                        OutputItemCount = elem.GetProperty("output_item_count").GetInt32(),
                        MinRating = elem.TryGetProperty("min_rating", out var mr)
                            ? mr.GetInt32() : 0
                    };

                    if (elem.TryGetProperty("disciplines", out var disc))
                    {
                        foreach (var d in disc.EnumerateArray())
                        {
                            recipe.Disciplines.Add(d.GetString());
                        }
                    }

                    if (elem.TryGetProperty("flags", out var flags))
                    {
                        foreach (var f in flags.EnumerateArray())
                        {
                            recipe.Flags.Add(f.GetString());
                        }
                    }

                    if (elem.TryGetProperty("ingredients", out var ings))
                    {
                        foreach (var ing in ings.EnumerateArray())
                        {
                            recipe.Ingredients.Add(new RawIngredient
                            {
                                Type = ing.TryGetProperty("type", out var t)
                                    ? t.GetString() ?? "Item" : "Item",
                                Id = ing.GetProperty("item_id").GetInt32(),
                                Count = ing.GetProperty("count").GetInt32()
                            });
                        }
                    }

                    recipes.Add(recipe);
                }
            }

            return recipes;
        }

        private static void MergeMysticForgeRecipes(
            Stream mfStream,
            Dictionary<int, RawRecipe> allRecipes,
            Dictionary<int, List<int>> searchIndex,
            out int count)
        {
            count = 0;

            using (var reader = new StreamReader(mfStream))
            {
                string json = reader.ReadToEnd();
                using (var doc = JsonDocument.Parse(json))
                {
                    if (!doc.RootElement.TryGetProperty("recipes", out var arr))
                    {
                        return;
                    }

                    foreach (var entry in arr.EnumerateArray())
                    {
                        if (!entry.TryGetProperty("id", out var idProp))
                        {
                            continue;
                        }

                        int id = idProp.GetInt32();
                        if (id >= 0)
                        {
                            continue;
                        }

                        if (!entry.TryGetProperty("outputItemId", out var outId) ||
                            !entry.TryGetProperty("outputItemCount", out var outCount))
                        {
                            continue;
                        }

                        if (!entry.TryGetProperty("ingredients", out var ingsArr))
                        {
                            continue;
                        }

                        var recipe = new RawRecipe
                        {
                            Id = id,
                            OutputItemId = outId.GetInt32(),
                            OutputItemCount = outCount.GetInt32(),
                            Disciplines = new List<string> { "MysticForge" },
                            MinRating = 0,
                            Flags = new List<string>()
                        };

                        foreach (var ing in ingsArr.EnumerateArray())
                        {
                            if (!ing.TryGetProperty("type", out var ingType) ||
                                !ing.TryGetProperty("id", out var ingId) ||
                                !ing.TryGetProperty("count", out var ingCount))
                            {
                                continue;
                            }

                            recipe.Ingredients.Add(new RawIngredient
                            {
                                Type = ingType.GetString() ?? "Item",
                                Id = ingId.GetInt32(),
                                Count = ingCount.GetInt32()
                            });
                        }

                        if (recipe.Ingredients.Count == 0)
                        {
                            continue;
                        }

                        allRecipes[recipe.Id] = recipe;

                        if (!searchIndex.TryGetValue(
                            recipe.OutputItemId, out var list))
                        {
                            list = new List<int>();
                            searchIndex[recipe.OutputItemId] = list;
                        }

                        if (!list.Contains(recipe.Id))
                        {
                            list.Add(recipe.Id);
                        }

                        count++;
                    }
                }
            }
        }

        private static async Task<List<ItemNameInfo>> FetchItemNamesAsync(
            HttpClient httpClient, List<int> itemIds)
        {
            var result = new List<ItemNameInfo>();
            var batches = new List<List<int>>();

            for (int i = 0; i < itemIds.Count; i += BatchSize)
            {
                int count = Math.Min(BatchSize, itemIds.Count - i);
                batches.Add(itemIds.GetRange(i, count));
            }

            int completed = 0;
            int total = batches.Count;
            var gate = new object();

            using (var semaphore = new SemaphoreSlim(MaxConcurrency))
            {
                var tasks = batches.Select(async batch =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var items = await FetchItemBatchAsync(httpClient, batch);

                        lock (gate)
                        {
                            result.AddRange(items);
                            completed++;
                            if (completed % 10 == 0 || completed == total)
                            {
                                Console.Write(
                                    $"\r  Batches: {completed}/{total}   ");
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToList();

                await Task.WhenAll(tasks);
            }

            Console.WriteLine();
            return result;
        }

        private static async Task<List<ItemNameInfo>> FetchItemBatchAsync(
            HttpClient httpClient, List<int> ids)
        {
            string idsParam = string.Join(",",
                ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            string url = $"{BaseUrl}/items?ids={idsParam}";

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    string json = await httpClient.GetStringAsync(url);
                    return ParseItemBatch(json);
                }
                catch (HttpRequestException) when (attempt < 2)
                {
                    await Task.Delay(1000 * (attempt + 1));
                }
            }

            return new List<ItemNameInfo>();
        }

        private static List<ItemNameInfo> ParseItemBatch(string json)
        {
            var items = new List<ItemNameInfo>();
            using (var doc = JsonDocument.Parse(json))
            {
                foreach (var elem in doc.RootElement.EnumerateArray())
                {
                    var item = new ItemNameInfo
                    {
                        Id = elem.GetProperty("id").GetInt32(),
                        Name = elem.TryGetProperty("name", out var n)
                            ? n.GetString() ?? "" : "",
                        Icon = elem.TryGetProperty("icon", out var ic)
                            ? ic.GetString() : null
                    };
                    if (!string.IsNullOrEmpty(item.Name))
                    {
                        items.Add(item);
                    }
                }
            }
            return items;
        }

        private class ItemNameInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Icon { get; set; }
        }

        private static async Task<int> FetchBuildIdAsync(HttpClient httpClient)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var response = await httpClient.GetAsync(
                    $"{BaseUrl}/build", cts.Token);
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
