using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Services.Recipes;
using GW2CraftingHelper.Views;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GW2CraftingHelper
{
    internal class ContentsManagerRecipeSource : IMysticForgeRecipeSource
    {
        private readonly ContentsManager _contents;

        public ContentsManagerRecipeSource(ContentsManager contents)
        {
            _contents = contents;
        }

        public System.IO.Stream Open()
        {
            return _contents.GetFileStream("mystic_forge_recipes.json");
        }
    }

    [Export(typeof(Blish_HUD.Modules.Module))]
    public class Module : Blish_HUD.Modules.Module
    {
        private static readonly Logger Logger = Logger.GetLogger<Module>();
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(10);

        // Bounds the whole multi-step account-snapshot fetch (wallet, bank,
        // shared inventory, materials, one call per character) so a full
        // network outage fails fast instead of stacking several ~100s HTTP
        // timeouts sequentially (KNOWN-ISSUES 31b/api-degradation F6) -
        // mirrors CurrencyMetadataService's own internal-timeout pattern,
        // just with a larger budget since this fetch does far more work on
        // a genuine success than a single /v2/currencies call.
        private static readonly TimeSpan SnapshotFetchTimeout = TimeSpan.FromSeconds(60);

        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        private CornerIcon _cornerIcon;
        private ResizableTabbedWindow _mainWindow;
        private ModalDialog _modalDialog;
        private MainView _snapshotContent;
        private CraftingPlanView _craftingContent;
        private LogTabContent _logContent;
        private Tab _logTab;
        private SettingsTabContent _settingsContent;

        private ModuleSettings _settings;
        private SnapshotStore _snapshotStore;
        private StatusStore _statusStore;
        private Gw2AccountSnapshotService _snapshotService;
        private AccountSnapshot _currentSnapshot;
        private AccountSnapshot _pendingSnapshot;
        private bool _snapshotDirty;
        private string _lastStatus;
        // Drained in Update() using the same dirty-flag polling pattern as
        // _snapshotDirty above, rather than MainThreadMarshal, so a status
        // saved from a ThreadPool continuation reaches the main thread the
        // same way an already-established mechanism does - one polling
        // path instead of two competing ways to get back to the UI thread.
        private bool _statusDirty;

        private HttpClient _httpClient;
        private CraftingPlanPipeline _craftingPipeline;
        private VendorOfferStore _vendorOfferStore;
        private IItemSearchProvider _itemSearchProvider;
        private Texture2D _moduleIconTexture;
        private Texture2D _emblemTexture;

        private CancellationTokenSource _refreshCts;

        // KNOWN-ISSUES 31a-F3 (nice-to-have): written in the finally of
        // RefreshSnapshotInBackgroundAsync/UserRefreshAsync, which may
        // resume on a ThreadPool continuation (Blish's XNA host installs no
        // SynchronizationContext), and read from Update() on the main
        // thread as a mutual-exclusion gate - a genuine cross-thread field,
        // so it needs the visibility guarantee volatile provides even
        // though no torn read is possible for a bool.
        private volatile bool _refreshInProgress;

        // KNOWN-ISSUES 31a-F1 (audit-of-fix): bumped only by ClearCache; a
        // fetch that captured an older epoch before starting must discard
        // its result rather than commit over a cleared cache. A bare
        // volatile counter (the original fix) left the check and the
        // commit as separate unsynchronized steps, so the gate now owns
        // both the epoch and the lock that makes ClearCache's bump/clear
        // and FetchAndSaveSnapshotAsync's check/commit mutually exclusive
        // - see SnapshotCommitGate's own doc comment for the full race.
        private readonly SnapshotCommitGate _snapshotCommitGate = new SnapshotCommitGate();

        // KNOWN-ISSUES 31c-audit (api-F1 follow-up): UTC ticks of the most
        // recent FAILED background refresh, or 0 if none. api-F1 made a
        // failed FetchSnapshotAsync throw instead of stamping
        // _currentSnapshot.CapturedAt with a fresh timestamp - correct
        // (no more silent data corruption), but that stamping used to be
        // the only thing making Update()'s staleness gate wait before
        // retrying. Without this, a persistent failure would re-arm the
        // gate on every single Update() tick instead of waiting out
        // RefreshFailureBackoff - see RefreshSnapshotInBackgroundAsync.
        // long, not DateTime, because C# disallows `volatile` on 64-bit
        // primitives; Interlocked.Read/Exchange give the same cross-thread
        // visibility guarantee _refreshInProgress gets from volatile
        // (_snapshotCommitGate below gets it from its own internal lock
        // instead), without needing a lock of its own here.
        private long _lastFailedRefreshAttemptTicks;

        // KNOWN-ISSUES 31c-audit: minimum wait after a failed background
        // refresh before RefreshSnapshotInBackgroundAsync is allowed to
        // auto-retrigger again. Deliberately does NOT gate UserRefreshAsync
        // (the explicit "Refresh Now" button) - a user-initiated retry
        // should never be throttled by an earlier automatic failure.
        private static readonly TimeSpan RefreshFailureBackoff = TimeSpan.FromSeconds(60);

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        protected override void DefineSettings(SettingCollection settings)
        {
            _settings = new ModuleSettings(settings);
        }

        protected override void Initialize()
        {
            string dataDir = DirectoriesManager.GetFullDirectoryPath("data");
            _snapshotStore = new SnapshotStore(dataDir);
            _statusStore = new StatusStore(dataDir);
            _snapshotService = new Gw2AccountSnapshotService(Gw2ApiManager);
            _lastStatus = _statusStore.Load();

            _httpClient = new HttpClient();
            var rawRecipeApi = new Gw2RecipeApiClient(_httpClient);
            var mfSource = new ContentsManagerRecipeSource(ContentsManager);
            var recipeApi = RecipeClientFactory.Create(rawRecipeApi, mfSource);
            var priceApi = new Gw2PriceApiClient(_httpClient);
            var itemApi = new Gw2ItemApiClient(_httpClient);

            var vendorLoader = new VendorOfferLoader();
            _vendorOfferStore = new VendorOfferStore(dataDir, vendorLoader);
            try
            {
                using (var baselineStream = ContentsManager.GetFileStream("vendor_offers.json"))
                {
                    _vendorOfferStore.LoadBaseline(baselineStream);
                }
            }
            catch
            {
                _vendorOfferStore.LoadBaseline(null);
            }
            _vendorOfferStore.LoadOverlay();

            // Recipe cache: seed + overlay
            var recipeSeed = new SeededRecipeCacheStore();
            try
            {
                using (var searchStream = ContentsManager.GetFileStream("recipe_search_seed.json"))
                using (var recipesStream = ContentsManager.GetFileStream("recipes_seed.json"))
                {
                    recipeSeed.Load(searchStream, recipesStream);
                }
            }
            catch
            {
                // No seed files yet - graceful degradation
            }

            try
            {
                using (var manifestStream = ContentsManager.GetFileStream("recipe_seed_manifest.json"))
                {
                    recipeSeed.LoadManifest(manifestStream);
                }
            }
            catch
            {
                // No manifest - staleness detection disabled
            }

            // Item name seed for search provider; the parsed seed is also
            // reused as the metadata fallback for ids the live API drops.
            ItemNameSeedData itemNameSeed = null;
            try
            {
                using (var nameStream = ContentsManager.GetFileStream("item_name_seed.json"))
                {
                    _itemSearchProvider = ItemSearchProviderFactory.Create(
                        nameStream, out string fallbackReason, out itemNameSeed);
                    if (fallbackReason != null)
                    {
                        Logger.Info("Item search fallback to static provider: {0}", fallbackReason);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info("Item search fallback to static provider: [{0}] {1}", ex.GetType().Name, ex.Message);
                _itemSearchProvider = new StaticItemSearchProvider();
            }

            // Acquisition hints seed: wiki-derived guidance for items with
            // no priceable source (docs/KNOWN-ISSUES.md item 8). Static
            // local file, no async fetch needed - loaded once here and
            // passed straight to the pipeline (simpler than
            // CurrencyMetadataService, which hits a live API).
            IReadOnlyDictionary<int, AcquisitionHint> acquisitionHints = null;
            try
            {
                using (var hintsStream = ContentsManager.GetFileStream("acquisition_hints_seed.json"))
                {
                    acquisitionHints = AcquisitionHintService.Load(hintsStream);
                }
            }
            catch (Exception ex)
            {
                Logger.Info("Acquisition hints unavailable: [{0}] {1}", ex.GetType().Name, ex.Message);
                acquisitionHints = null;
            }

            var recipeOverlay = new OverlayRecipeCacheStore(dataDir);
            recipeOverlay.Load(currentGw2BuildId: null);

            // Async build ID fetch for overlay invalidation + seed staleness
            Task.Run(async () =>
            {
                try
                {
                    int buildId = await FetchGw2BuildIdAsync();
                    recipeOverlay.InvalidateIfStale(buildId);
                    recipeSeed.SetCurrentBuildId(buildId);
                }
                catch (Exception ex)
                {
                    Logger.Debug("Could not fetch GW2 build ID for cache validation: {0}", ex.Message);
                }
            });

            var recipeCacheStore = new CompositeRecipeCacheStore(recipeSeed, recipeOverlay);

            _craftingPipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi, cacheStore: recipeCacheStore),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi, itemNameSeed),
                _vendorOfferStore,
                resolver: null,
                reducer: new InventoryReducer(),
                accountRecipeClient: new Gw2AccountRecipeClient(Gw2ApiManager),
                currencyMetadataService: new CurrencyMetadataService(_httpClient),
                acquisitionHints: acquisitionHints);

            try
            {
                _moduleIconTexture = ContentsManager.GetTexture("icon.png");
            }
            catch
            {
                _moduleIconTexture = ContentService.Textures.Error;
            }

            try
            {
                _emblemTexture = ContentsManager.GetTexture("emblem.png");
            }
            catch
            {
                _emblemTexture = _moduleIconTexture;
            }

            _modalDialog = new ModalDialog(_settings);

            _snapshotContent = new MainView(
                _currentSnapshot,
                _lastStatus,
                UserRefreshAsync,
                ClearCache,
                SaveStatus,
                SaveStatusThreadSafe
            );

            _craftingContent = new CraftingPlanView(
                // M35 (gw2efficiency parity - multi-item plans): always
                // routed through the list overload - a single-entry list
                // short-circuits straight to the untouched single-item
                // method inside the pipeline itself (byte-identical
                // output, no wrapper built at all - see
                // CraftingPlanPipeline.GenerateStructuredAsync's own doc
                // comment), so this lambda no longer needs its own
                // single-vs-multi branch.
                (items, useOwn, priceBasis, ct, progress) =>
                {
                    string activeChar = null;
                    try
                    {
                        var mumble = GameService.Gw2Mumble;
                        if (mumble != null &&
                            mumble.PlayerCharacter != null &&
                            !string.IsNullOrEmpty(mumble.PlayerCharacter.Name))
                        {
                            activeChar = mumble.PlayerCharacter.Name;
                        }
                    }
                    catch
                    {
                        // Gw2Mumble unavailable - graceful fallback
                    }

                    var currencyValuation = _settings.GetCurrencyValuation();
                    var ownMaterialsMode = _settings.GetOwnMaterialsMode();

                    if (useOwn)
                    {
                        return _craftingPipeline.GenerateStructuredAsync(
                            items, _currentSnapshot, ct, progress,
                            activeChar, priceBasis, currencyValuation, ownMaterialsMode);
                    }
                    return _craftingPipeline.GenerateStructuredAsync(
                        items, null, ct, progress,
                        null, priceBasis, currencyValuation, ownMaterialsMode);
                },
                _modalDialog,
                _itemSearchProvider,
                _settings,
                (ctx, overrides, ignoredItemIds) => _craftingPipeline.ResolveWithOverrides(ctx, overrides, ignoredItemIds)
            );

            _settingsContent = new SettingsTabContent(_settings);

            // Minimum size (930x710) matches the window region intentionally.
            // Validated in-game to align with Event Table / Blish HUD's own
            // TabbedWindow dimensions and the 1024x1024 background texture (502049).
            // contentRegion must end above the window bottom: flush would be
            // contentRegion.Y + contentRegion.Height == windowRegion.Height
            // (11 + 699 == 710), but texture 502049 also fades to transparent
            // over roughly its last 15 rows (verified via screenshot: content
            // at the flush edge shows windows behind bleeding through), so an
            // extra 15px margin keeps every row on opaque backdrop.
            _mainWindow = new ResizableTabbedWindow(
                AsyncTexture2D.FromAssetId(502049),
                new Rectangle(35, 26, 930, 710),
                new Rectangle(81, 11, 884, 684),
                new Point(930, 710))
            {
                Parent = GameService.Graphics.SpriteScreen,
                Title = "GW2 Crafting Helper",
                Emblem = new AsyncTexture2D(_emblemTexture),
                Id = $"{nameof(Module)}_MainWindow",
                Location = new Point(
                    (GameService.Graphics.SpriteScreen.Width - 930) / 2,
                    (GameService.Graphics.SpriteScreen.Height - 710) / 2),
                SavesPosition = true
            };

            _mainWindow.Tabs.Add(new Tab(
                AsyncTexture2D.FromAssetId(156699),
                () => new ViewAdapter("Snapshot", c => _snapshotContent.Build(c)),
                "Snapshot"));

            _mainWindow.Tabs.Add(new Tab(
                AsyncTexture2D.FromAssetId(156711),
                () => new ViewAdapter("Crafting Plan", c => _craftingContent.Build(c)),
                "Crafting Plan"));

            _logTab = new Tab(
                AsyncTexture2D.FromAssetId(156701),
                () => new ViewAdapter("Log", c =>
                {
                    _logContent = new LogTabContent(() => _craftingContent.LastDebugLog);
                    _logContent.Build(c);
                }),
                "Log");
            _mainWindow.Tabs.Add(_logTab);

            _mainWindow.Tabs.Add(new Tab(
                AsyncTexture2D.FromAssetId(156691),
                () => new ViewAdapter("Plan History", BuildPlaceholder),
                "Plan History"));

            _mainWindow.Tabs.Add(new Tab(
                AsyncTexture2D.FromAssetId(156686),
                () => new ViewAdapter("Crafting Ranker", BuildPlaceholder),
                "Crafting Ranker"));

            _mainWindow.Tabs.Add(new Tab(
                AsyncTexture2D.FromAssetId(156736),
                () => new ViewAdapter("Settings", c => _settingsContent.Build(c)),
                "Settings"));

            _mainWindow.Tabs.Add(new Tab(
                AsyncTexture2D.FromAssetId(157097),
                () => new ViewAdapter("About", BuildPlaceholder),
                "About"));

            // Refresh log content when switching to the Log tab
            _mainWindow.TabChanged += (s, e) =>
            {
                if (_mainWindow.SelectedTab == _logTab && _logContent != null)
                {
                    _logContent.Refresh();
                }
            };

            _cornerIcon = new CornerIcon()
            {
                IconName = "GW2 Crafting Helper",
                Icon = new AsyncTexture2D(_moduleIconTexture),
                Priority = 1245846523,
                Parent = GameService.Graphics.SpriteScreen
            };

            _cornerIcon.Click += (s, e) =>
            {
                _mainWindow.ToggleWindow();
            };
        }

        protected override async Task LoadAsync()
        {
            _currentSnapshot = _snapshotStore.LoadLatest();

            Gw2ApiManager.SubtokenUpdated += OnSubtokenUpdated;

            if (_snapshotService.HasRequiredPermissions())
            {
                await RefreshSnapshotInBackgroundAsync();
            }
        }

        protected override void Update(GameTime gameTime)
        {
            bool statusApplied = false;

            if (_snapshotDirty)
            {
                Logger.Info("Applying snapshot to view CapturedAt={0:o}", _pendingSnapshot?.CapturedAt);
                _snapshotDirty = false;
                _snapshotContent?.SetSnapshot(_pendingSnapshot);
                _snapshotContent?.SetStatus(_lastStatus);
                statusApplied = true;
            }

            // Status updates saved from a ThreadPool continuation (Blish's
            // XNA host has no SynchronizationContext) land here instead of
            // touching the view directly - see SaveStatusThreadSafe. Skipped
            // when the snapshot branch above already applied _lastStatus
            // this tick (both flags can be set together, e.g. a background
            // refresh updates both), so SetStatus runs at most once per tick.
            if (_statusDirty)
            {
                _statusDirty = false;
                if (!statusApplied)
                {
                    _snapshotContent?.SetStatus(_lastStatus);
                }
            }

            if (_refreshInProgress) return;
            if (_currentSnapshot == null) return;
            if (DateTime.UtcNow - _currentSnapshot.CapturedAt < StaleThreshold) return;
            if (!_snapshotService.HasRequiredPermissions()) return;

            _ = RefreshSnapshotInBackgroundAsync();
        }

        protected override void Unload()
        {
            Gw2ApiManager.SubtokenUpdated -= OnSubtokenUpdated;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();

            _httpClient?.Dispose();
            _modalDialog?.Dispose();
            _cornerIcon?.Dispose();
            _mainWindow?.Dispose();
        }

        private void OnSubtokenUpdated(object sender, ValueEventArgs<IEnumerable<Gw2Sharp.WebApi.V2.Models.TokenPermission>> e)
        {
            if (_snapshotService.HasRequiredPermissions())
            {
                _ = RefreshSnapshotInBackgroundAsync();
            }
        }

        private async Task<AccountSnapshot> FetchAndSaveSnapshotAsync(CancellationToken ct)
        {
            Logger.Info("Refreshing account snapshot...");

            // Captured before the fetch starts (main thread - see the
            // field's own comment) so the post-await commit below can
            // detect a Clear Cache that ran while this fetch was still in
            // flight (KNOWN-ISSUES 31a-F1).
            int myEpoch = _snapshotCommitGate.Epoch;

            AccountSnapshot snapshot;
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeoutCts.CancelAfter(SnapshotFetchTimeout);
                try
                {
                    snapshot = await _snapshotService.FetchSnapshotAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // The internal timeout fired, not the caller's own
                    // token - a genuine fetch failure (KNOWN-ISSUES
                    // api-degradation F6), not a cancellation. Re-thrown as
                    // a plain Exception so callers' "cancelled" catch
                    // (which must stay silent) does not swallow it.
                    throw new TimeoutException(
                        $"Account snapshot fetch exceeded {SnapshotFetchTimeout.TotalSeconds:0}s.");
                }
            }

            // Re-check and commit run inside SnapshotCommitGate's lock -
            // the same lock ClearCache's own bump+clear runs under below -
            // so the two can never interleave (KNOWN-ISSUES 31a-F1
            // audit-of-fix; see SnapshotCommitGate's doc comment).
            bool committed = _snapshotCommitGate.TryCommit(myEpoch, () =>
            {
                _currentSnapshot = snapshot;
                _snapshotStore.Save(snapshot);

                _pendingSnapshot = snapshot;
                _snapshotDirty = true;
            });

            if (!committed)
            {
                // Clear Cache ran (fully, atomically) either before or
                // during this check; committing now would resurrect data
                // the user explicitly cleared (KNOWN-ISSUES 31a-F1). Drop
                // the result - _currentSnapshot, _pendingSnapshot,
                // _snapshotDirty, and the on-disk file are all left
                // untouched by this call.
                Logger.Info("Discarding snapshot fetch superseded by Clear Cache (epoch {0})", myEpoch);
                return null;
            }

            Logger.Info("Fetched snapshot CapturedAt={0:o} items={1} wallet={2} coin={3}",
                snapshot.CapturedAt, snapshot.Items.Count, snapshot.Wallet.Count, snapshot.CoinCopper);

            return snapshot;
        }

        private async Task RefreshSnapshotInBackgroundAsync()
        {
            if (_refreshInProgress) return;

            // KNOWN-ISSUES 31c-audit: refuse to auto-retrigger again so
            // soon after a failed attempt - see _lastFailedRefreshAttemptTicks'
            // own doc comment. Both callers of this method (Update()'s
            // staleness tick and OnSubtokenUpdated) can otherwise re-fire
            // far faster than any real transient failure needs to be
            // retried at.
            var lastFailedTicks = Interlocked.Read(ref _lastFailedRefreshAttemptTicks);
            if (lastFailedTicks != 0 &&
                DateTime.UtcNow - new DateTime(lastFailedTicks, DateTimeKind.Utc) < RefreshFailureBackoff)
            {
                Logger.Debug("Skipping snapshot refresh retry - within backoff window after a prior failure");
                return;
            }

            _refreshInProgress = true;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();

            try
            {
                var snapshot = await FetchAndSaveSnapshotAsync(_refreshCts.Token);
                Interlocked.Exchange(ref _lastFailedRefreshAttemptTicks, 0);
                if (snapshot != null)
                {
                    var status = $"Updated \u2014 {snapshot.CapturedAt.ToLocalTime():t}";
                    SaveStatusThreadSafe(status);
                }
                // else: superseded by Clear Cache while this fetch was in
                // flight (KNOWN-ISSUES 31a-F1, see SnapshotEpochGuard) -
                // Clear Cache already wrote its own status; nothing further
                // to report here.
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("Snapshot refresh cancelled");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to refresh account snapshot");
                Interlocked.Exchange(ref _lastFailedRefreshAttemptTicks, DateTime.UtcNow.Ticks);
                var status = $"Refresh failed \u2014 {DateTime.Now:t}";
                SaveStatusThreadSafe(status);
            }
            finally
            {
                _refreshInProgress = false;
            }
        }

        private async Task<AccountSnapshot> UserRefreshAsync()
        {
            if (_refreshInProgress) return null;
            _refreshInProgress = true;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();

            try
            {
                return await FetchAndSaveSnapshotAsync(_refreshCts.Token);
            }
            finally
            {
                _refreshInProgress = false;
            }
        }

        private void ClearCache()
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = null;

            // Epoch bump + on-disk delete + field resets all run inside
            // SnapshotCommitGate's lock so a snapshot fetch already in
            // flight (which captured an epoch before this call ran) either
            // commits fully before this runs, or has its post-fetch commit
            // check fail atomically against this bump - no interleaving,
            // no torn field state (KNOWN-ISSUES 31a-F1 audit-of-fix; see
            // SnapshotCommitGate's own doc comment).
            _snapshotCommitGate.Clear(() =>
            {
                _snapshotStore.Delete();
                _currentSnapshot = null;
                _pendingSnapshot = null;
                _snapshotDirty = false;
            });
        }

        private void PersistStatus(string status)
        {
            _lastStatus = status ?? "";
            _statusStore.Save(_lastStatus);
        }

        // Called directly from a context already known to be on the main
        // thread: MainView's Clear Cache click handler (synchronous, no
        // await). MainView's async Refresh Now handler persists via
        // SaveStatusThreadSafe instead, because its continuation may resume
        // on a ThreadPool thread and the _snapshotContent.SetStatus call
        // below is a control mutation - not safe to run off the UI thread.
        private void SaveStatus(string status)
        {
            PersistStatus(status);
            _snapshotContent?.SetStatus(_lastStatus);
        }

        // Thread-safe variant for callers that may run on a ThreadPool
        // continuation (Blish HUD's XNA host installs no
        // SynchronizationContext, so await continuations do not resume on
        // the main thread) - used by the background auto-refresh path below
        // and wired into MainView as its async Refresh Now handler's
        // persistence callback. Persists the status immediately - file I/O
        // is safe off the UI thread - but defers the control mutation to
        // Update() via the same dirty-flag polling pattern already used for
        // snapshots, rather than touching _snapshotContent here.
        private void SaveStatusThreadSafe(string status)
        {
            PersistStatus(status);
            _statusDirty = true;
        }

        private static void BuildPlaceholder(Container container)
        {
            new Label()
            {
                Text = "Coming Soon",
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(20, 20),
                TextColor = new Color(150, 150, 150),
                Parent = container
            };
        }

        private async Task<int> FetchGw2BuildIdAsync()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                var response = await _httpClient.GetAsync(
                    "https://api.guildwars2.com/v2/build", cts.Token);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                using (var doc = System.Text.Json.JsonDocument.Parse(json))
                {
                    return doc.RootElement.GetProperty("id").GetInt32();
                }
            }
        }
    }
}
