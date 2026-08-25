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

        // Recipes persisted by an earlier session are only servable once the
        // live game build is known to match the build they were cached from.
        // Module.cs learns that build from an async /v2/build call that lands
        // seconds after Load, so Load leaves the overlay files on disk UNREAD
        // and sets this instead; ResolveDeferredLocked below then either
        // reads them in (builds match) or deletes them (they do not). Until
        // that happens the maps hold only what THIS session fetched, so a
        // plan generated in the meantime - or in a whole session whose build
        // check failed - can never be built from another build's recipes.
        private bool _deferredDiskLoad;

        // See StatusStore's
        // matching field comment.
        private readonly Action<string, Exception> _onError;

        private static readonly TimeSpan FlushDebounce = TimeSpan.FromSeconds(2);

        public RecipeCacheStats Stats => _stats;

        public OverlayRecipeCacheStore(string dataDir, Action<string, Exception> onError = null)
        {
            _cacheDir = Path.Combine(dataDir, "recipe_cache");
            _searchPath = Path.Combine(_cacheDir, "search_overlay.json");
            _recipesPath = Path.Combine(_cacheDir, "recipes_overlay.json");
            _manifestPath = Path.Combine(_cacheDir, "overlay_manifest.json");
            _onError = onError;
        }

        public void Load(int? currentGw2BuildId)
        {
            lock (_gate)
            {
                _deferredDiskLoad = false;

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
                        _onError?.Invoke("Failed to load overlay manifest", ex);
                    }
                }

                _searches = new Dictionary<int, IReadOnlyList<int>>();
                _recipes = new Dictionary<int, RawRecipe>();

                if (!currentGw2BuildId.HasValue)
                {
                    // Vintage unproven - see _deferredDiskLoad. A manifest
                    // alone is enough to defer: its build id is the one
                    // PersistLocked would otherwise stamp onto entries this
                    // session fetched under a build nobody has checked.
                    _deferredDiskLoad = _storedBuildId.HasValue
                        || File.Exists(_searchPath)
                        || File.Exists(_recipesPath);
                    return;
                }

                if (_storedBuildId.HasValue
                    && currentGw2BuildId.Value != _storedBuildId.Value)
                {
                    Debug.WriteLine(
                        $"Recipe overlay build mismatch " +
                        $"(stored={_storedBuildId}, current={currentGw2BuildId}). " +
                        $"Clearing overlay.");
                    DeleteOverlayFiles();
                    _storedBuildId = null;
                    return;
                }

                LoadOverlayFilesLocked();
            }
        }

        public void InvalidateIfStale(int currentGw2BuildId)
        {
            lock (_gate)
            {
                ResolveDeferredLocked(currentGw2BuildId);

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

        // Settles a deferred load now that the live build is known: reads the
        // persisted overlay in if it was cached from this same build,
        // discards it if it was not. Whatever this session has already
        // fetched is kept either way - those entries are current-build by
        // construction, and win over a same-key entry off disk.
        private void ResolveDeferredLocked(int currentGw2BuildId)
        {
            if (!_deferredDiskLoad)
            {
                return;
            }

            _deferredDiskLoad = false;

            if (_storedBuildId.HasValue && _storedBuildId.Value == currentGw2BuildId)
            {
                LoadOverlayFilesLocked();
                return;
            }

            Debug.WriteLine(
                $"Recipe overlay stale " +
                $"(stored={_storedBuildId}, current={currentGw2BuildId}). " +
                $"Discarding unread overlay.");
            DeleteOverlayFiles();
            _storedBuildId = null;
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
        /// came from.
        /// <para>
        /// Must be called AFTER <see cref="InvalidateIfStale"/>, which clears
        /// the stored build when it wipes a stale overlay - an earlier stamp
        /// would be discarded, the manifest would record 0, and the next
        /// launch would treat the overlay as stale and delete it again.
        /// </para>
        /// </summary>
        public void SetCurrentBuildId(int buildId)
        {
            lock (_gate)
            {
                ResolveDeferredLocked(buildId);

                if (_storedBuildId.HasValue && _storedBuildId.Value == buildId)
                {
                    return;
                }

                _storedBuildId = buildId;
                _dirty = true;
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

                // Nothing to write while the live build is still unknown:
                // this session's entries have no build id to be honestly
                // stamped with, and persisting them would overwrite an
                // overlay that has not even been read yet (_deferredDiskLoad).
                // _dirty stays set, so a later resolve still flushes.
                if (_deferredDiskLoad)
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
                // 0 means "written before the live build id was known"; the
                // next Load treats it as a mismatch and discards the overlay
                // once, rather than serving recipes of unknown vintage.
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
                _onError?.Invoke("Failed to persist recipe overlay", ex);
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
                _onError?.Invoke("Failed to delete overlay files", ex);
            }
        }
    }
}
