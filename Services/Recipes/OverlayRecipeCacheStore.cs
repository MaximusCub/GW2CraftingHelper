using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GW2CraftingHelper.Services.Recipes
{
    public class OverlayRecipeCacheStore : IRecipeCacheStore
    {
        private readonly string _cacheDir;
        private readonly string _searchPath;
        private readonly string _recipesPath;
        private readonly string _manifestPath;

        private Dictionary<int, IReadOnlyList<int>> _searches =
            new Dictionary<int, IReadOnlyList<int>>();

        private Dictionary<int, RawRecipe> _recipes =
            new Dictionary<int, RawRecipe>();

        private readonly RecipeCacheStats _stats = new RecipeCacheStats();
        private readonly object _gate = new object();
        private bool _dirty;
        private DateTime _lastFlushUtc = DateTime.MinValue;
        private int? _storedBuildId;

        private static readonly TimeSpan FlushDebounce = TimeSpan.FromSeconds(2);

        public RecipeCacheStats Stats => _stats;

        public OverlayRecipeCacheStore(string dataDir)
        {
            _cacheDir = Path.Combine(dataDir, "recipe_cache");
            _searchPath = Path.Combine(_cacheDir, "search_overlay.json");
            _recipesPath = Path.Combine(_cacheDir, "recipes_overlay.json");
            _manifestPath = Path.Combine(_cacheDir, "overlay_manifest.json");
        }

        public void Load(int? currentGw2BuildId)
        {
            lock (_gate)
            {
                if (!Directory.Exists(_cacheDir))
                {
                    _searches = new Dictionary<int, IReadOnlyList<int>>();
                    _recipes = new Dictionary<int, RawRecipe>();
                    _storedBuildId = null;
                    return;
                }

                // Read manifest to check build ID
                _storedBuildId = null;
                if (File.Exists(_manifestPath))
                {
                    try
                    {
                        using (var fs = File.OpenRead(_manifestPath))
                        {
                            var manifest = RecipeCacheSerializer
                                .LoadManifest<RecipeOverlayManifest>(fs);
                            _storedBuildId = manifest.Gw2BuildId;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"Failed to load overlay manifest: {ex.Message}");
                    }
                }

                // If build ID known and mismatches, invalidate
                if (currentGw2BuildId.HasValue
                    && _storedBuildId.HasValue
                    && currentGw2BuildId.Value != _storedBuildId.Value)
                {
                    Debug.WriteLine(
                        $"Recipe overlay build mismatch " +
                        $"(stored={_storedBuildId}, current={currentGw2BuildId}). " +
                        $"Clearing overlay.");
                    DeleteOverlayFiles();
                    _searches = new Dictionary<int, IReadOnlyList<int>>();
                    _recipes = new Dictionary<int, RawRecipe>();
                    return;
                }

                // Load search overlay
                if (File.Exists(_searchPath))
                {
                    try
                    {
                        using (var fs = File.OpenRead(_searchPath))
                        {
                            _searches = RecipeCacheSerializer.LoadSearchSeed(fs);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"Failed to load search overlay: {ex.Message}");
                        _searches = new Dictionary<int, IReadOnlyList<int>>();
                    }
                }
                else
                {
                    _searches = new Dictionary<int, IReadOnlyList<int>>();
                }

                // Load recipe overlay
                if (File.Exists(_recipesPath))
                {
                    try
                    {
                        using (var fs = File.OpenRead(_recipesPath))
                        {
                            _recipes = RecipeCacheSerializer.LoadRecipeSeed(fs);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"Failed to load recipe overlay: {ex.Message}");
                        _recipes = new Dictionary<int, RawRecipe>();
                    }
                }
                else
                {
                    _recipes = new Dictionary<int, RawRecipe>();
                }
            }
        }

        public void InvalidateIfStale(int currentGw2BuildId)
        {
            lock (_gate)
            {
                if (_storedBuildId.HasValue
                    && _storedBuildId.Value != currentGw2BuildId)
                {
                    Debug.WriteLine(
                        $"Recipe overlay stale " +
                        $"(stored={_storedBuildId}, current={currentGw2BuildId}). " +
                        $"Clearing.");
                    DeleteOverlayFiles();
                    _searches = new Dictionary<int, IReadOnlyList<int>>();
                    _recipes = new Dictionary<int, RawRecipe>();
                    _storedBuildId = null;
                    _dirty = false;
                }
            }
        }

        public IReadOnlyList<int> TryGetSearch(int outputItemId)
        {
            lock (_gate)
            {
                if (_searches.TryGetValue(outputItemId, out var result))
                {
                    _stats.IncrementSearchHit();
                    return result;
                }
                _stats.IncrementSearchMiss();
                return null;
            }
        }

        public RawRecipe TryGetRecipe(int recipeId)
        {
            lock (_gate)
            {
                if (_recipes.TryGetValue(recipeId, out var result))
                {
                    _stats.IncrementRecipeHit();
                    return result;
                }
                _stats.IncrementRecipeMiss();
                return null;
            }
        }

        public void PutSearch(int outputItemId, IReadOnlyList<int> recipeIds)
        {
            lock (_gate)
            {
                _searches[outputItemId] = recipeIds;
                _dirty = true;
            }
        }

        public void PutRecipe(int recipeId, RawRecipe recipe)
        {
            lock (_gate)
            {
                _recipes[recipeId] = recipe;
                _dirty = true;
            }
        }

        public void Flush(bool force = false)
        {
            lock (_gate)
            {
                if (!_dirty)
                {
                    return;
                }

                if (!force)
                {
                    var now = DateTime.UtcNow;
                    if (now - _lastFlushUtc < FlushDebounce)
                    {
                        return;
                    }
                }

                PersistLocked();
                _dirty = false;
                _lastFlushUtc = DateTime.UtcNow;
            }
        }

        private void PersistLocked()
        {
            try
            {
                Directory.CreateDirectory(_cacheDir);

                // Write searches
                string searchJson = RecipeCacheSerializer.SerializeSearches(_searches);
                AtomicWrite(_searchPath, searchJson);

                // Write recipes
                string recipeJson = RecipeCacheSerializer.SerializeRecipes(_recipes);
                AtomicWrite(_recipesPath, recipeJson);

                // Write manifest
                var manifest = new RecipeOverlayManifest
                {
                    Gw2BuildId = _storedBuildId ?? 0,
                    UpdatedUtc = DateTime.UtcNow.ToString("o")
                };
                string manifestJson = RecipeCacheSerializer.SerializeManifest(manifest);
                AtomicWrite(_manifestPath, manifestJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to persist recipe overlay: {ex.Message}");
            }
        }

        private static void AtomicWrite(string path, string content)
        {
            string tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, content, Encoding.UTF8);

            if (File.Exists(path))
            {
                File.Replace(tmpPath, path, null);
            }
            else
            {
                File.Move(tmpPath, path);
            }
        }

        private void DeleteOverlayFiles()
        {
            try
            {
                if (File.Exists(_searchPath))
                {
                    File.Delete(_searchPath);
                }
                if (File.Exists(_recipesPath))
                {
                    File.Delete(_recipesPath);
                }
                if (File.Exists(_manifestPath))
                {
                    File.Delete(_manifestPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Failed to delete overlay files: {ex.Message}");
            }
        }
    }
}
