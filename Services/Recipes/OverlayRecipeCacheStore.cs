using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GW2CraftingHelper.Services.Recipes
{
    internal class OverlayRecipeCacheStore : IRecipeCacheStore
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

        // Tracked per file, not as one flag: the two caches fill at very
        // different rates (a session that learns a search learns no new
        // recipe at all when the shipped seed already has them), and each
        // rewrite is a whole-file write whether one entry changed or none.
        private bool _searchesDirty;
        private bool _recipesDirty;
        private bool _stampDirty;

        private DateTime _lastFlushUtc = DateTime.MinValue;
        private int? _storedBuildId;

        // See StatusStore's
        // matching field comment.
        private readonly Action<string, Exception> _onError;

        private static readonly TimeSpan FlushDebounce = TimeSpan.FromSeconds(2);

        public RecipeCacheStats Stats => _stats;

        private bool IsDirty => _searchesDirty || _recipesDirty || _stampDirty;

        public OverlayRecipeCacheStore(string dataDir, Action<string, Exception> onError = null)
        {
            _cacheDir = Path.Combine(dataDir, "recipe_cache");
            _searchPath = Path.Combine(_cacheDir, "search_overlay.json");
            _recipesPath = Path.Combine(_cacheDir, "recipes_overlay.json");
            _manifestPath = Path.Combine(_cacheDir, "overlay_manifest.json");
            _onError = onError;
        }

        // POLICY (recipe cache staleness policy): a build-id mismatch never
        // invalidates anything. Learned positives are served whatever build
        // they were cached from - measured basis: 13,371/13,371 seed recipes
        // byte-identical across a 275-build gap - and stored negatives no
        // longer exist to go stale. The build id in the manifest is
        // provenance and a verification cheap-out, not a wipe trigger; the
        // wipe-on-mismatch this store used to do destroyed the overlay at
        // exactly the moment it became useful (a new build is what makes the
        // shipped seed stale).
        public void Load()
        {
            lock (_gate)
            {
                // Load replaces the maps with what disk holds, so anything
                // put into them before this call is gone and must not be
                // flushed back out.
                ClearDirtyLocked();

                _searches = new Dictionary<int, IReadOnlyList<int>>();
                _recipes = new Dictionary<int, RawRecipe>();
                _storedBuildId = null;

                if (!Directory.Exists(_cacheDir))
                {
                    return;
                }

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
                        _onError?.Invoke("Failed to load overlay manifest", ex);
                    }
                }

                LoadOverlayFilesLocked();
            }
        }

        private void LoadOverlayFilesLocked()
        {
            if (File.Exists(_searchPath))
            {
                try
                {
                    using (var fs = File.OpenRead(_searchPath))
                    {
                        MergeUnder(RecipeCacheSerializer.LoadSearchSeed(fs), _searches);
                    }
                }
                catch (Exception ex)
                {
                    _onError?.Invoke("Failed to load search overlay", ex);
                }
            }

            if (File.Exists(_recipesPath))
            {
                try
                {
                    using (var fs = File.OpenRead(_recipesPath))
                    {
                        MergeUnder(RecipeCacheSerializer.LoadRecipeSeed(fs), _recipes);
                    }
                }
                catch (Exception ex)
                {
                    _onError?.Invoke("Failed to load recipe overlay", ex);
                }
            }
        }

        private static void MergeUnder<T>(
            IDictionary<int, T> loaded, IDictionary<int, T> target)
        {
            foreach (var entry in loaded)
            {
                if (!target.ContainsKey(entry.Key))
                {
                    target[entry.Key] = entry.Value;
                }
            }
        }

        /// <summary>
        /// Stamps the live game build id onto the overlay, so the manifest
        /// written by the next flush records the build the cached recipes
        /// came from. Provenance only - a differing stored id restamps, it
        /// never clears anything.
        /// </summary>
        public void SetCurrentBuildId(int buildId)
        {
            lock (_gate)
            {
                if (_storedBuildId.HasValue && _storedBuildId.Value == buildId)
                {
                    return;
                }

                _storedBuildId = buildId;
                _stampDirty = true;
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
                _searchesDirty = true;
            }
        }

        public void PutRecipe(int recipeId, RawRecipe recipe)
        {
            lock (_gate)
            {
                _recipes[recipeId] = recipe;
                _recipesDirty = true;
            }
        }

        public void Flush(bool force = false)
        {
            lock (_gate)
            {
                if (!IsDirty)
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
                ClearDirtyLocked();
                _lastFlushUtc = DateTime.UtcNow;
            }
        }

        private void PersistLocked()
        {
            try
            {
                Directory.CreateDirectory(_cacheDir);

                if (_searchesDirty)
                {
                    string searchJson = RecipeCacheSerializer.SerializeSearches(_searches);
                    AtomicWrite(_searchPath, searchJson);
                }

                if (_recipesDirty)
                {
                    string recipeJson = RecipeCacheSerializer.SerializeRecipes(_recipes);
                    AtomicWrite(_recipesPath, recipeJson);
                }

                // The manifest goes out on every persist, not just a stamp
                // change: it dates the two files above and records their
                // provenance. 0 means "written before the live build id was
                // known" - the entries are still served; only the vintage
                // line in the Log tab is poorer for it.
                var manifest = new RecipeOverlayManifest
                {
                    Gw2BuildId = _storedBuildId ?? 0,
                    UpdatedUtc = DateTime.UtcNow.ToString("o"),
                };
                string manifestJson = RecipeCacheSerializer.SerializeManifest(manifest);
                AtomicWrite(_manifestPath, manifestJson);
            }
            catch (Exception ex)
            {
                _onError?.Invoke("Failed to persist recipe overlay", ex);
            }
        }

        private void ClearDirtyLocked()
        {
            _searchesDirty = false;
            _recipesDirty = false;
            _stampDirty = false;
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
                _onError?.Invoke("Failed to delete overlay files", ex);
            }
        }
    }
}
