using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TaimisToolbench.Services.Recipes
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
        private int _negativesVerifiedBuildId;
        private int _verifiedKnownRecipeCount;
        private int _corpusRefreshBuildId;
        private int _corpusRefreshCursorId;
        private bool _corpusRefreshComplete;
        private int _droppedLearnedNegatives;

        // The overlay manifest's schema. 1 stored learned negatives (empty
        // search rows); 2 stores positives only and carries the
        // corpus-verification stamp.
        private const int SchemaVersion = 2;

        // See StatusStore's
        // matching field comment.
        private readonly Action<string, Exception> _onError;

        private static readonly TimeSpan FlushDebounce = TimeSpan.FromSeconds(2);

        public RecipeCacheStats Stats => _stats;

        /// <summary>
        /// The game build the corpus was last verified against, off the
        /// manifest; 0 = never. See RecipeOverlayManifest.
        /// </summary>
        public int NegativesVerifiedBuildId
        {
            get
            {
                lock (_gate)
                {
                    return _negativesVerifiedBuildId;
                }
            }
        }

        public int VerifiedKnownRecipeCount
        {
            get
            {
                lock (_gate)
                {
                    return _verifiedKnownRecipeCount;
                }
            }
        }

        /// <summary>
        /// The build the content sweep (RecipeCorpusRefresher) last made
        /// progress against, off the manifest; 0 = never run.
        /// </summary>
        public int CorpusRefreshBuildId
        {
            get
            {
                lock (_gate)
                {
                    return _corpusRefreshBuildId;
                }
            }
        }

        /// <summary>
        /// The sweep's resume point: every held positive recipe id at or
        /// below this was refetched at <see cref="CorpusRefreshBuildId"/>.
        /// </summary>
        public int CorpusRefreshCursorId
        {
            get
            {
                lock (_gate)
                {
                    return _corpusRefreshCursorId;
                }
            }
        }

        /// <summary>
        /// True once the sweep finished at <see cref="CorpusRefreshBuildId"/>.
        /// </summary>
        public bool CorpusRefreshComplete
        {
            get
            {
                lock (_gate)
                {
                    return _corpusRefreshComplete;
                }
            }
        }

        /// <summary>
        /// How many v1 learned-negative (empty) rows the last
        /// <see cref="Load"/> dropped - exposed so Module.cs can log the
        /// one-time migration once at Info.
        /// </summary>
        public int DroppedLearnedNegatives
        {
            get
            {
                lock (_gate)
                {
                    return _droppedLearnedNegatives;
                }
            }
        }

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
                _negativesVerifiedBuildId = 0;
                _verifiedKnownRecipeCount = 0;
                _corpusRefreshBuildId = 0;
                _corpusRefreshCursorId = 0;
                _corpusRefreshComplete = false;
                _droppedLearnedNegatives = 0;

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
                            _negativesVerifiedBuildId = manifest.NegativesVerifiedBuildId;
                            _verifiedKnownRecipeCount = manifest.VerifiedKnownRecipeCount;
                            _corpusRefreshBuildId = manifest.CorpusRefreshBuildId;
                            _corpusRefreshCursorId = manifest.CorpusRefreshCursorId;
                            _corpusRefreshComplete = manifest.CorpusRefreshComplete;
                        }
                    }
                    catch (Exception ex)
                    {
                        _onError?.Invoke("Failed to load overlay manifest", ex);
                    }
                }

                LoadOverlayFilesLocked();
                FinalizeOverlayLocked();
            }
        }

        // SeededRecipeCacheStore.FinalizeIndex's pass over the overlay's own
        // contents, doubling as the v1 migration: learned positive rows and
        // recipes carry over whatever build stamped them, v1 learned-negative
        // (empty) rows are dropped unconditionally - the one-time cleanup
        // that removes any already-poisoned negative from disk - and any
        // change is marked dirty so the next flush rewrites the file, at
        // which point PersistLocked stamps the manifest at schema 2.
        private void FinalizeOverlayLocked()
        {
            bool changed = false;
            foreach (var recipe in _recipes.Values)
            {
                changed |= SeededRecipeCacheStore.AddRecipeIdToRow(
                    _searches, recipe.OutputItemId, recipe.Id);
            }

            var emptyRows = new List<int>();
            foreach (var entry in _searches)
            {
                if (entry.Value.Count == 0)
                {
                    emptyRows.Add(entry.Key);
                }
            }

            foreach (int key in emptyRows)
            {
                _searches.Remove(key);
            }

            _droppedLearnedNegatives = emptyRows.Count;
            if (changed || emptyRows.Count > 0)
            {
                _searchesDirty = true;
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

        /// <summary>
        /// Records a successful corpus verification: the manifest written
        /// by the next flush says derived negatives are exact at this build
        /// for this corpus size. A no-op when both values already match, so
        /// a relaunch inside the same patch stays write-free.
        /// </summary>
        public void SetCorpusVerified(int buildId, int knownRecipeCount)
        {
            lock (_gate)
            {
                if (_negativesVerifiedBuildId == buildId
                    && _verifiedKnownRecipeCount == knownRecipeCount)
                {
                    return;
                }

                _negativesVerifiedBuildId = buildId;
                _verifiedKnownRecipeCount = knownRecipeCount;
                _stampDirty = true;
            }
        }

        /// <summary>
        /// Records how far the content sweep has walked the held corpus at
        /// this build. Written after every batch so an unload, a network
        /// drop or a game exit costs at most the batch in flight; a
        /// buildId that differs from the stored one resets the cursor,
        /// which is what makes a new game build restart the sweep.
        /// </summary>
        public void SetCorpusRefreshProgress(int buildId, int cursorRecipeId, bool complete)
        {
            lock (_gate)
            {
                if (_corpusRefreshBuildId == buildId
                    && _corpusRefreshCursorId == cursorRecipeId
                    && _corpusRefreshComplete == complete)
                {
                    return;
                }

                _corpusRefreshBuildId = buildId;
                _corpusRefreshCursorId = cursorRecipeId;
                _corpusRefreshComplete = complete;
                _stampDirty = true;
            }
        }

        /// <summary>
        /// A snapshot of the recipe ids the overlay holds, for the corpus
        /// diff; copied under the lock because the overlay mutates.
        /// </summary>
        public List<int> GetRecipeIds()
        {
            lock (_gate)
            {
                return new List<int>(_recipes.Keys);
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
                // An empty row is never stored: "no recipe" is derived from
                // the corpus at lookup time (CompositeRecipeCacheStore), so
                // the only thing an empty row can do is shadow data.
                if (recipeIds == null || recipeIds.Count == 0)
                {
                    return;
                }

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

        /// <summary>
        /// The manual route out of a bad overlay, now that build changes
        /// never wipe one (Module.ClearCache): deletes the three files and
        /// resets the in-memory overlay to empty. The shipped seed is
        /// untouched, and the zeroed verification stamp re-arms the corpus
        /// probe.
        /// </summary>
        public void Clear()
        {
            lock (_gate)
            {
                DeleteOverlayFiles();
                _searches = new Dictionary<int, IReadOnlyList<int>>();
                _recipes = new Dictionary<int, RawRecipe>();
                _storedBuildId = null;
                _negativesVerifiedBuildId = 0;
                _verifiedKnownRecipeCount = 0;
                _corpusRefreshBuildId = 0;
                _corpusRefreshCursorId = 0;
                _corpusRefreshComplete = false;
                _droppedLearnedNegatives = 0;
                ClearDirtyLocked();
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
                    SchemaVersion = SchemaVersion,
                    Gw2BuildId = _storedBuildId ?? 0,
                    NegativesVerifiedBuildId = _negativesVerifiedBuildId,
                    VerifiedKnownRecipeCount = _verifiedKnownRecipeCount,
                    CorpusRefreshBuildId = _corpusRefreshBuildId,
                    CorpusRefreshCursorId = _corpusRefreshCursorId,
                    CorpusRefreshComplete = _corpusRefreshComplete,
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
