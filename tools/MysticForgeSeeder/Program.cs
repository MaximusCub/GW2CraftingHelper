using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MysticForgeSeeder
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                return await RunAsync(args, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Cancelled.");
                return 130;
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
            catch (InvalidOperationException ex)
                when (ex.Message.Contains("request limit"))
            {
                Console.Error.WriteLine($"LIMIT: {ex.Message}");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"ERROR: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }

        private static async Task<int> RunAsync(
            string[] args, CancellationToken ct)
        {
            bool dryRun = false;
            bool forceResolve = false;
            int delayMs = 250;
            int maxRequests = 200;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--dry-run":
                        dryRun = true;
                        break;
                    case "--force-resolve":
                        forceResolve = true;
                        break;
                    case "--delay" when i + 1 < args.Length:
                        delayMs = int.Parse(args[++i]);
                        break;
                    case "--max-requests" when i + 1 < args.Length:
                        maxRequests = int.Parse(args[++i]);
                        break;
                }
            }

            string repoRoot = FindRepoRoot();
            string outputPath = Path.Combine(
                repoRoot, "ref", "mystic_forge_recipes.json");
            string cachePath = Path.Combine(
                repoRoot, "ref", "mf_item_id_cache.json");

            Console.WriteLine($"Output:        {outputPath}");
            Console.WriteLine($"Cache:         {cachePath}");
            Console.WriteLine($"Dry run:       {dryRun}");
            Console.WriteLine($"Force resolve: {forceResolve}");
            Console.WriteLine($"Delay:         {delayMs}ms");
            Console.WriteLine($"Max requests:  {maxRequests}");
            Console.WriteLine();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "GW2CraftingHelper-MysticForgeSeeder/1.0");

            var client = new WikiRecipeClient(
                httpClient, delayMs, maxRequests);

            // ================================================================
            // Step 1: Query wiki for all MF recipes
            // ================================================================
            Console.WriteLine(
                "=== Step 1: Query wiki for Mystic Forge recipes ===");
            var recipes = await client.QueryMysticForgeRecipesAsync(ct);
            Console.WriteLine($"  Recipes: {recipes.Count}");
            Console.WriteLine();

            // ================================================================
            // Step 2: Collect unique item names
            // ================================================================
            Console.WriteLine("=== Step 2: Collect unique item names ===");

            // Build canonical name set (case-insensitive dedup, log collisions)
            var canonicalNames = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var recipe in recipes)
            {
                TrackCanonicalName(canonicalNames, recipe.OutputName);
                foreach (var ing in recipe.Ingredients)
                {
                    TrackCanonicalName(canonicalNames, ing.Name);
                }
            }

            Console.WriteLine(
                $"  Total unique names: {canonicalNames.Count}");

            var cache = LoadCache(cachePath);
            Console.WriteLine($"  Cache entries: {cache.Count}");

            List<string> namesToResolve;
            if (forceResolve)
            {
                namesToResolve = canonicalNames.Values.ToList();
            }
            else
            {
                namesToResolve = canonicalNames.Values
                    .Where(n => !cache.ContainsKey(n))
                    .ToList();
            }

            Console.WriteLine(
                $"  Names to resolve: {namesToResolve.Count}");
            Console.WriteLine();

            // ================================================================
            // Step 3: Resolve names to GW2 item IDs
            // ================================================================
            Console.WriteLine(
                "=== Step 3: Resolve item IDs via wiki ===");

            if (namesToResolve.Count > 0)
            {
                var resolved = await client.ResolveItemIdsAsync(
                    namesToResolve, ct);

                int resolvedCount = 0;
                int unresolvedCount = 0;
                var unresolvedNames = new List<string>();

                foreach (var name in namesToResolve)
                {
                    if (resolved.TryGetValue(name, out int id))
                    {
                        // Store under canonical wiki fulltext (trimmed)
                        // Log if cache already had a different ID
                        if (cache.TryGetValue(name, out int oldId) &&
                            oldId != id && oldId > 0)
                        {
                            Console.WriteLine(
                                $"  CACHE UPDATE: '{name}' " +
                                $"{oldId} -> {id}");
                        }
                        cache[name] = id;
                        resolvedCount++;
                    }
                    else if (!cache.ContainsKey(name))
                    {
                        cache[name] = -1; // miss sentinel
                        unresolvedCount++;
                        unresolvedNames.Add(name);
                    }
                }

                Console.WriteLine($"  Resolved: {resolvedCount}");
                Console.WriteLine($"  Unresolved: {unresolvedCount}");

                if (unresolvedNames.Count > 0)
                {
                    int showCount = Math.Min(unresolvedNames.Count, 50);
                    foreach (var name in unresolvedNames
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .Take(showCount))
                    {
                        Console.WriteLine($"    - {name}");
                    }
                    if (unresolvedNames.Count > 50)
                    {
                        Console.WriteLine(
                            $"    ... and {unresolvedNames.Count - 50} more");
                    }
                }

                SaveCacheAtomic(cachePath, cache);
            }
            else
            {
                Console.WriteLine("  All names already cached.");
            }
            Console.WriteLine();

            // ================================================================
            // Step 4: Build recipe objects
            // ================================================================
            Console.WriteLine("=== Step 4: Build recipe objects ===");

            var validRecipes = new List<ValidRecipe>();
            int skipped = 0;

            foreach (var recipe in recipes)
            {
                if (!TryGetValidId(cache, recipe.OutputName, out int outputId))
                {
                    Console.WriteLine(
                        $"  SKIP: {recipe.OutputName}" +
                        " - output ID unresolved");
                    skipped++;
                    continue;
                }

                bool valid = true;
                var ingredients = new List<RecipeIngredient>();

                foreach (var ing in recipe.Ingredients)
                {
                    if (ing.Quantity <= 0)
                    {
                        Console.WriteLine(
                            $"  SKIP: {recipe.OutputName}" +
                            $" - ingredient '{ing.Name}' count <= 0");
                        valid = false;
                        break;
                    }

                    if (!TryGetValidId(cache, ing.Name, out int ingId))
                    {
                        Console.WriteLine(
                            $"  SKIP: {recipe.OutputName}" +
                            $" - ingredient '{ing.Name}' ID unresolved");
                        valid = false;
                        break;
                    }

                    ingredients.Add(new RecipeIngredient
                    {
                        Id = ingId,
                        Count = ing.Quantity
                    });
                }

                if (!valid)
                {
                    skipped++;
                    continue;
                }

                validRecipes.Add(new ValidRecipe
                {
                    OutputItemId = outputId,
                    OutputItemCount = recipe.OutputQuantity,
                    OutputName = recipe.OutputName,
                    Ingredients = ingredients
                });
            }

            // Deterministic sort: by outputItemId, then by OutputName for ties
            validRecipes = validRecipes
                .OrderBy(r => r.OutputItemId)
                .ThenBy(r => r.OutputName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Console.WriteLine($"  Valid recipes: {validRecipes.Count}");
            Console.WriteLine($"  Skipped: {skipped}");
            Console.WriteLine();

            // ================================================================
            // Step 5: Write output
            // ================================================================
            Console.WriteLine("=== Step 5: Write output ===");

            if (dryRun)
            {
                Console.WriteLine(
                    "  DRY RUN - skipping recipe file write.");
            }
            else
            {
                WriteRecipeFileAtomic(outputPath, validRecipes);
                var fileInfo = new FileInfo(outputPath);
                Console.WriteLine($"  Written to: {outputPath}");
                Console.WriteLine(
                    $"  File size: {fileInfo.Length:N0} bytes");
            }
            Console.WriteLine();

            // ================================================================
            // Summary
            // ================================================================
            Console.WriteLine("=== Mystic Forge Seeder Complete ===");
            Console.WriteLine($"Recipes written: {validRecipes.Count}");
            Console.WriteLine(
                $"Recipes skipped: {skipped} (see warnings above)");
            int totalUnresolved = cache.Values.Count(v => v == -1);
            Console.WriteLine($"Unresolved names: {totalUnresolved}");
            Console.WriteLine("Next steps:");
            Console.WriteLine(
                "  dotnet run --project " +
                "tools/GW2CraftingHelper.RecipeSeeder/" +
                "GW2CraftingHelper.RecipeSeeder.csproj");
            Console.WriteLine(
                "  dotnet build GW2CraftingHelper.csproj -p:Platform=x64");
            Console.WriteLine(
                "  dotnet test tests/GW2CraftingHelper.Tests/" +
                "GW2CraftingHelper.Tests.csproj");

            return 0;
        }

        /// <summary>
        /// Tracks canonical name for case-insensitive dedup.
        /// Logs collisions where two different-case variants exist.
        /// Keeps the first-seen variant as canonical.
        /// </summary>
        private static void TrackCanonicalName(
            Dictionary<string, string> canonicalNames, string name)
        {
            string? trimmed = name?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            if (canonicalNames.TryGetValue(trimmed, out string? existing))
            {
                // Case-insensitive match exists; log if cases differ
                if (!string.Equals(existing, trimmed, StringComparison.Ordinal))
                {
                    Console.WriteLine(
                        $"  CASE COLLISION: '{trimmed}' vs '{existing}'" +
                        " - keeping first seen");
                }
            }
            else
            {
                canonicalNames[trimmed] = trimmed;
            }
        }

        private static bool TryGetValidId(
            Dictionary<string, int> cache, string name, out int id)
        {
            if (cache.TryGetValue(name, out id) && id > 0)
            {
                return true;
            }
            id = 0;
            return false;
        }

        /// <summary>
        /// Writes the recipe file using Utf8JsonWriter for deterministic
        /// property order. Atomic: writes to .tmp then moves.
        ///
        /// Property order (invariant):
        ///   root: schemaVersion, recipes
        ///   recipe: id, outputItemId, outputItemCount, ingredients, comment
        ///   ingredient: type, id, count
        /// </summary>
        private static void WriteRecipeFileAtomic(
            string outputPath, List<ValidRecipe> recipes)
        {
            string tmpPath = outputPath + ".tmp";

            using (var stream = new FileStream(
                tmpPath, FileMode.Create, FileAccess.Write))
            {
                using var writer = new Utf8JsonWriter(stream,
                    new JsonWriterOptions { Indented = true });

                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", 1);
                writer.WriteStartArray("recipes");

                int recipeId = -1;
                foreach (var recipe in recipes)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("id", recipeId);
                    writer.WriteNumber("outputItemId", recipe.OutputItemId);
                    writer.WriteNumber(
                        "outputItemCount", recipe.OutputItemCount);

                    writer.WriteStartArray("ingredients");
                    foreach (var ing in recipe.Ingredients)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("type", "Item");
                        writer.WriteNumber("id", ing.Id);
                        writer.WriteNumber("count", ing.Count);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    writer.WriteString("comment",
                        $"{recipe.OutputName} (from wiki)");
                    writer.WriteEndObject();

                    recipeId--;
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            File.Move(tmpPath, outputPath, overwrite: true);
        }

        private static Dictionary<string, int> LoadCache(string path)
        {
            var cache = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(path))
            {
                return cache;
            }

            try
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    cache[prop.Name] = prop.Value.GetInt32();
                }
                Console.WriteLine(
                    $"  Loaded cache ({cache.Count} entries) from {path}");
            }
            catch
            {
                Console.WriteLine(
                    "  WARNING: Cache file corrupt, starting fresh.");
            }

            return cache;
        }

        private static void SaveCacheAtomic(
            string path, Dictionary<string, int> cache)
        {
            string tmpPath = path + ".tmp";

            var sorted = cache
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(sorted, options);

            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
            Console.WriteLine(
                $"  Saved cache ({cache.Count} entries) to {path}");
        }

        private static string FindRepoRoot()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir, ".git")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return Directory.GetCurrentDirectory();
        }
    }

    internal class ValidRecipe
    {
        public int OutputItemId { get; set; }
        public int OutputItemCount { get; set; }

        // Always set at construction (Step 4's one object initializer).
        public string OutputName { get; set; } = string.Empty;
        public List<RecipeIngredient> Ingredients { get; set; } = new();
    }

    internal class RecipeIngredient
    {
        public int Id { get; set; }
        public int Count { get; set; }
    }
}
