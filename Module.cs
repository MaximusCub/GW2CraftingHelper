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

        private ModuleSettings _settings;
        private SnapshotStore _snapshotStore;
        private StatusStore _statusStore;
        private Gw2AccountSnapshotService _snapshotService;
        private AccountSnapshot _currentSnapshot;
        private AccountSnapshot _pendingSnapshot;
        private bool _snapshotDirty;
        private string _lastStatus;

        private HttpClient _httpClient;
        private CraftingPlanPipeline _craftingPipeline;
        private VendorOfferStore _vendorOfferStore;
        private IItemSearchProvider _itemSearchProvider;
        private Texture2D _moduleIconTexture;
        private Texture2D _emblemTexture;

        private CancellationTokenSource _refreshCts;
        private bool _refreshInProgress;

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
                accountRecipeClient: new Gw2AccountRecipeClient(Gw2ApiManager));

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
                SaveStatus
            );

            _craftingContent = new CraftingPlanView(
                (itemId, qty, useOwn, priceBasis, ct, progress) =>
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

                    if (useOwn)
                    {
                        return _craftingPipeline.GenerateStructuredAsync(
                            itemId, qty, _currentSnapshot, ct, progress,
                            activeChar, priceBasis);
                    }
                    return _craftingPipeline.GenerateStructuredAsync(
                        itemId, qty, null, ct, progress,
                        null, priceBasis);
                },
                _modalDialog,
                _itemSearchProvider,
                (ctx, overrides) => _craftingPipeline.ResolveWithOverrides(ctx, overrides)
            );

            // Minimum size (930x710) matches the window region intentionally.
            // Validated in-game to align with Event Table / Blish HUD's own
            // TabbedWindow dimensions and the 1024x1024 background texture (502049).
            _mainWindow = new ResizableTabbedWindow(
                AsyncTexture2D.FromAssetId(502049),
                new Rectangle(35, 26, 930, 710),
                new Rectangle(81, 11, 884, 710),
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
                () => new ViewAdapter("Settings", BuildPlaceholder),
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
            if (_snapshotDirty)
            {
                Logger.Info("Applying snapshot to view CapturedAt={0:o}", _pendingSnapshot?.CapturedAt);
                _snapshotDirty = false;
                _snapshotContent?.SetSnapshot(_pendingSnapshot);
                _snapshotContent?.SetStatus(_lastStatus);
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

            var snapshot = await _snapshotService.FetchSnapshotAsync(ct);

            _currentSnapshot = snapshot;
            _snapshotStore.Save(snapshot);

            _pendingSnapshot = snapshot;
            _snapshotDirty = true;

            Logger.Info("Fetched snapshot CapturedAt={0:o} items={1} wallet={2} coin={3}",
                snapshot.CapturedAt, snapshot.Items.Count, snapshot.Wallet.Count, snapshot.CoinCopper);

            return snapshot;
        }

        private async Task RefreshSnapshotInBackgroundAsync()
        {
            if (_refreshInProgress) return;
            _refreshInProgress = true;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();

            try
            {
                var snapshot = await FetchAndSaveSnapshotAsync(_refreshCts.Token);
                var status = $"Updated \u2014 {snapshot.CapturedAt.ToLocalTime():t}";
                SaveStatus(status);
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("Snapshot refresh cancelled");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to refresh account snapshot");
                var status = $"Refresh failed \u2014 {DateTime.Now:t}";
                SaveStatus(status);
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
            _snapshotStore.Delete();
            _currentSnapshot = null;
            _pendingSnapshot = null;
            _snapshotDirty = false;
        }

        private void SaveStatus(string status)
        {
            _lastStatus = status ?? "";
            _statusStore.Save(_lastStatus);
            _snapshotContent?.SetStatus(_lastStatus);
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
