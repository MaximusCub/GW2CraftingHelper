using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
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
using GW2CraftingHelper.Views.Rendering;
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
        private ApiAccessDialog _apiAccessDialog;
        private MainView _snapshotContent;
        private CraftingPlanView _craftingContent;
        private LogTabContent _logContent;
        private Tab _logTab;
        private Tab _settingsTab;

        // The Log tab's "Clear View" floor lives on Module, not
        // LogTabContent: Blish reconstructs LogTabContent on every tab
        // selection, so a field there cannot survive a tab switch (a
        // cleared view resurrected on reopen). A plain long is enough -
        // every read/write of this field is main-thread-only; the one
        // ThreadPool touch (the view-factory closure) only copies
        // delegates, never dereferences the field.
        private long _logViewClearedBeforeVersion;
        private SettingsTabContent _settingsContent;
        private AboutTabContent _aboutContent;

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

        // A generated plan loaded from disk at LoadAsync time, applied to
        // _craftingContent from Update() - same dirty-flag drain shape as
        // _pendingSnapshot/_snapshotDirty. Written once in LoadAsync and
        // drained (never re-armed) the first time Update() sees the flag.
        private PersistedPlan _pendingPlanRestore;
        private bool _planRestoreDirty;

        // The original request/timestamp behind the most recently
        // persisted plan - the last successful Generate, or the restored
        // plan. A ResolveWithOverrides persist reuses this as-is: a local
        // re-solve must not advance GeneratedAt/request just because a
        // pill was clicked. One immutable object published through a
        // single volatile field: a reader always sees all four values
        // from the same publish, never a torn mix - four separate fields
        // written from a ThreadPool continuation raced main-thread reads.
        private volatile PersistedPlanMetadata _lastPersistedPlanMetadata;

        // Guards the compound "check _generateCompletedThisSession, then
        // publish restore metadata" sequence in Update()'s drain against
        // PersistAfterGenerateAsync's "set completed, then publish
        // generate metadata" sequence - a bare volatile bool leaves a
        // TOCTOU window where the restore's stale metadata could pair
        // with a just-generated Result, or clobber the live view. Held
        // only across the cheap field read/write pair, never disk I/O or
        // rendering.
        private readonly object _generateCompletionLock = new object();

        // True once a real Generate has completed this session - distinct
        // from _lastPersistedPlanMetadata being non-null, which a
        // restored plan also sets. Guards Update()'s restore drain: a
        // Generate can complete while LoadAsync is still awaiting its
        // refresh, and without this guard the drain would overwrite the
        // just-generated plan with the stale on-disk one. Every access
        // goes through _generateCompletionLock.
        private bool _generateCompletedThisSession;

        // Mirrors CraftingPlanView's ++_generateSequence "only the newest
        // generation may act" convention, scoped to Module's own
        // disk-write decision. A second Generate CAN start while an
        // earlier one's persist is pending (the modal-confirm path fires
        // one gated only on _currentPlan != null). Incremented
        // synchronously on the main thread in lockstep with the view's
        // own myGen bump; volatile because the post-await continuation
        // compares it from a ThreadPool thread.
        private volatile int _persistGenerateSequence;

        private HttpClient _httpClient;
        private CraftingPlanPipeline _craftingPipeline;
        private PlanStore _planStore;

        // Lives here rather than on CraftingPlanView so it survives a
        // view build cycle (see PlanStripStatusBoard). Thread-safe and
        // constructor-injected once - CraftingPlanView is a singleton
        // Module constructs exactly once.
        private readonly PlanStripStatusBoard _planStripStatusBoard = new PlanStripStatusBoard();
        private VendorOfferStore _vendorOfferStore;
        private IItemSearchProvider _itemSearchProvider;
        private Texture2D _moduleIconTexture;
        private Texture2D _cornerIconTexture;
        private Texture2D _emblemTexture;

        private CancellationTokenSource _refreshCts;

        // Written in the finally of the refresh methods, which may resume
        // on a ThreadPool continuation, and read from Update() on the
        // main thread as a mutual-exclusion gate - volatile for
        // cross-thread visibility.
        private volatile bool _refreshInProgress;

        // Bumped only by ClearCache; a
        // fetch that captured an older epoch before starting must discard
        // its result rather than commit over a cleared cache. A bare
        // volatile counter (the original fix) left the check and the
        // commit as separate unsynchronized steps, so the gate now owns
        // both the epoch and the lock that makes ClearCache's bump/clear
        // and FetchAndSaveSnapshotAsync's check/commit mutually exclusive
        // - see SnapshotCommitGate's own doc comment for the full race.
        private readonly SnapshotCommitGate _snapshotCommitGate = new SnapshotCommitGate();

        // UTC ticks of the most
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

        // Minimum wait after a failed background
        // refresh before RefreshSnapshotInBackgroundAsync is allowed to
        // auto-retrigger again. Deliberately does NOT gate UserRefreshAsync
        // (the explicit "Refresh Now" button) - a user-initiated retry
        // should never be throttled by an earlier automatic failure.
        private static readonly TimeSpan RefreshFailureBackoff = TimeSpan.FromSeconds(60);

        // Whether the timer-driven auto-refresh is running right now.
        // Written from RefreshSnapshotInBackgroundAsync, which starts on the
        // main thread but whose finally may resume on a ThreadPool
        // continuation (Blish's XNA host installs no SynchronizationContext)
        // - hence volatile, and hence applied to the view from Update()
        // rather than written to a control here, the same shape
        // SaveStatusThreadSafe already uses for status text.
        //
        // NOT the same flag as _refreshInProgress: that one gates whether a
        // refresh may START (and covers the clicked path too), while this
        // one is only ever about the SPINNER for the automatic path. Keeping
        // them apart is what lets the clicked path own its own spinner
        // without either path switching the other's off.
        private volatile bool _backgroundRefreshInFlight;

        // Last value actually pushed to the view, so the drain below pushes
        // on CHANGE rather than every frame. Only advanced when a view
        // existed to receive the push: the very first refresh runs at module
        // load, and marking it applied against a null view would lose the
        // state instead of retrying on the next tick.
        private bool _backgroundRefreshSpinnerApplied;

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        protected override void DefineSettings(SettingCollection settings)
        {
            _settings = new ModuleSettings(settings);
        }

        protected override void Initialize()
        {
            string dataDir = DirectoriesManager.GetFullDirectoryPath("data");

            // Configured before any other store so their
            // onError callbacks (below) can always reach ModuleLog.Shared
            // regardless of construction order - Write() is always safe to
            // call even before Configure() attaches the file store (writes
            // just stay ring-only until then). The log store's OWN IO
            // failure is wired straight to Blish's Logger, never back into
            // ModuleLog - see ModuleLogStore's doc comment on why
            // (unbounded recursion into the sink whose own write just
            // failed).
            var logStore = new ModuleLogStore(dataDir, (message, ex) => Logger.Warn(ex, message));
            ModuleLog.Shared.Configure(logStore, _settings.GetClampedLogMaxSizeBytes(), (message, ex) => Logger.Warn(ex, message));
            // Blish renders each SettingEntry a second time in Manage
            // Modules, and those controls only write the setting - these
            // subscriptions carry a change live no matter which UI made it.
            _settings.LogDiagnosticsEnabled.SettingChanged += OnLogDiagnosticsEnabledChanged;
            ModuleLog.Shared.DiagnosticsEnabled = _settings.LogDiagnosticsEnabled.Value;
            _settings.LogMaxSizeBytes.SettingChanged += OnLogMaxSizeBytesChanged;
            _settings.ClickSoundVolumePercent.SettingChanged += OnClickSoundVolumeChanged;
            Views.Rendering.ClickSound.VolumePercent = _settings.GetClampedClickSoundVolumePercent();

            // Once-per-session age-based retention enforcement, BEFORE the
            // ring is seeded from the file below - the ring then mirrors
            // exactly what survived retention, rather than briefly showing
            // an entry this same call is about to prune from disk. Both run
            // here (Initialize, not LoadAsync) and before any other store
            // is even constructed, so the seeded pre-session history always
            // sorts before this session's own first log line - see
            // ModuleLog.SeedFromStore's own doc comment.
            ModuleLog.Shared.PruneOlderThan(_settings.GetClampedLogRetentionDays());
            ModuleLog.Shared.SeedFromStore();

            // Every store's IO-failure callback routes to ModuleLog so a
            // store failure is visible in the Log tab, not just in an
            // attached debugger.
            Action<string, Exception> onStoreError = (message, ex) =>
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "store", $"{message}: {ex.GetType().Name} - {ex.Message}");

            _snapshotStore = new SnapshotStore(dataDir, onStoreError);
            _statusStore = new StatusStore(dataDir, onStoreError);
            _planStore = new PlanStore(dataDir, onStoreError);
            _snapshotService = new Gw2AccountSnapshotService(Gw2ApiManager);
            _lastStatus = _statusStore.Load();

            _httpClient = new HttpClient();
            var rawRecipeApi = new Gw2RecipeApiClient(_httpClient);
            var mfSource = new ContentsManagerRecipeSource(ContentsManager);
            var recipeApi = RecipeClientFactory.Create(rawRecipeApi, mfSource);
            var priceApi = new Gw2PriceApiClient(_httpClient);
            var itemApi = new Gw2ItemApiClient(_httpClient);

            var vendorLoader = new VendorOfferLoader();
            _vendorOfferStore = new VendorOfferStore(dataDir, vendorLoader, onStoreError);
            try
            {
                using (var baselineStream = ContentsManager.GetFileStream("vendor_offers.json"))
                {
                    _vendorOfferStore.LoadBaseline(baselineStream);
                }
            }
            catch (Exception ex)
            {
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"Vendor baseline load failed, starting with an empty baseline: {ex.GetType().Name} - {ex.Message}");
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
            catch (Exception ex)
            {
                // No seed files yet - graceful degradation. Previously a
                // fully silent bare catch (d2-log-system.md Section 8: "a
                // real gap the migration closes, not just a routing
                // change") - now visible in the Log tab at Warn.
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"Recipe seed load failed, starting with an empty seed cache: {ex.GetType().Name} - {ex.Message}");
            }

            try
            {
                using (var manifestStream = ContentsManager.GetFileStream("recipe_seed_manifest.json"))
                {
                    recipeSeed.LoadManifest(manifestStream);
                }
            }
            catch (Exception ex)
            {
                // No manifest - staleness detection disabled. Previously a
                // fully silent bare catch - see the recipe-seed catch above.
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"Recipe seed manifest load failed, staleness detection disabled: {ex.GetType().Name} - {ex.Message}");
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
                        ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"Item search fallback to static provider: {fallbackReason}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info("Item search fallback to static provider: [{0}] {1}", ex.GetType().Name, ex.Message);
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"Item search fallback to static provider: [{ex.GetType().Name}] {ex.Message}");
                _itemSearchProvider = new StaticItemSearchProvider();
            }

            // Static-local-file seeds, no async fetch needed - loaded once
            // here and passed straight to the pipeline (simpler than
            // CurrencyMetadataService, which hits a live API).
            // Acquisition hints: wiki-derived guidance for items with no
            // priceable source (docs/KNOWN-ISSUES.md item 8).
            IReadOnlyDictionary<int, AcquisitionHint> acquisitionHints = LoadSeedOrNull(
                "acquisition_hints_seed.json", "Acquisition hints unavailable",
                AcquisitionHintService.Load);

            // Daily craft-cooldown seed: wiki-verified recipes whose
            // crafting action itself is server-capped.
            IReadOnlyDictionary<int, DailyCooldownItem> dailyCooldownItems = LoadSeedOrNull(
                "daily_cooldown_items.json", "Daily cooldown items unavailable",
                DailyCooldownItemService.Load);

            // Recipe-sheet seed: recipe id -> unlocking recipe-sheet item
            // id for RecipeSheetSavingsCalculator.
            IReadOnlyDictionary<int, int> recipeSheetItemIdByRecipeId = LoadSeedOrNull(
                "recipe_sheet_items.json", "Recipe sheet items unavailable",
                RecipeSheetItemSeedService.Load);

            var recipeOverlay = new OverlayRecipeCacheStore(dataDir, onStoreError);
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
                    ModuleLog.Shared.Write(ModuleLogLevel.Debug, "startup", $"Could not fetch GW2 build ID for cache validation: {ex.Message}");
                }
            });

            var recipeCacheStore = new CompositeRecipeCacheStore(recipeSeed, recipeOverlay);

            // Hoisted out of the pipeline's argument list so the plan view
            // can read its session item-stat cache for tooltips - the same
            // instance, so the stats the plan already fetched are the ones
            // a hover reads. Never a fetch (GetCachedStatBlock).
            var itemMetadataService = new ItemMetadataService(itemApi, itemNameSeed);

            _craftingPipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi, cacheStore: recipeCacheStore),
                new TradingPostService(priceApi),
                new PlanSolver(),
                itemMetadataService,
                _vendorOfferStore,
                reducer: new InventoryReducer(),
                accountRecipeClient: new Gw2AccountRecipeClient(Gw2ApiManager),
                currencyMetadataService: new CurrencyMetadataService(_httpClient),
                acquisitionHints: acquisitionHints,
                dailyCooldownItems: dailyCooldownItems,
                recipeSheetItemIdByRecipeId: recipeSheetItemIdByRecipeId,
                activeFestivalNames: ReadActiveFestivalNames);

            try
            {
                _moduleIconTexture = ContentsManager.GetTexture("icon.png");
            }
            catch (Exception ex)
            {
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"Module icon texture load failed, using the fallback error texture: {ex.GetType().Name} - {ex.Message}");
                _moduleIconTexture = ContentService.Textures.Error;
            }

            try
            {
                // The strip variant: same hammer, re-padded so the glyph
                // fills ~72% of the canvas like the game's own top-row
                // icons (icon.png fills 84% - correct for the module list
                // and emblem, visibly oversized between GW2's menu icons;
                // measured against a live top-row capture 2026-08-23).
                _cornerIconTexture = ContentsManager.GetTexture("corner-icon.png");
            }
            catch (Exception ex)
            {
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"Corner icon texture load failed, using the module icon: {ex.GetType().Name} - {ex.Message}");
                _cornerIconTexture = _moduleIconTexture;
            }

            try
            {
                _emblemTexture = ContentsManager.GetTexture("emblem.png");
            }
            catch (Exception ex)
            {
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"Emblem texture load failed, reusing the module icon: {ex.GetType().Name} - {ex.Message}");
                _emblemTexture = _moduleIconTexture;
            }

            // The module window is built further down this method, so the
            // blocked surface is handed over as a lambda rather than a
            // reference - see ModalBackdrop for what it does with it.
            _modalDialog = new ModalDialog(_settings, () => _mainWindow);
            _apiAccessDialog = new ApiAccessDialog();

            _snapshotContent = new MainView(
                _currentSnapshot,
                _lastStatus,
                UserRefreshAsync,
                _apiAccessDialog,
                _modalDialog,
                _settings,
                ClearCache,
                SaveStatus,
                SaveStatusThreadSafe,
                itemMetadataService.GetCachedStatBlock
            );

            _craftingContent = new CraftingPlanView(
                // Always routed through the list overload - a
                // single-entry list short-circuits to the single-item
                // method inside the pipeline, so this lambda needs no
                // single-vs-multi branch of its own.
                (items, useOwn, valueOwnMaterials, priceBasis, ct, progress, phaseProgress, requestLabel) =>
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
                    catch (Exception ex)
                    {
                        // Gw2Mumble unavailable - graceful fallback. Debug,
                        // not Warn: this runs once per Generate click (a
                        // human-paced action, not a hot loop), but a user
                        // running Blish without Mumble wired up would hit
                        // it every single click - Debug (ring-always,
                        // file-only-when-diagnostics-on) keeps that from
                        // becoming routine file noise for a purely
                        // cosmetic fallback (active-character is only used
                        // for account-bound recipe checks).
                        ModuleLog.Shared.Write(ModuleLogLevel.Debug, "plan", $"Gw2Mumble unavailable, active character unknown: {ex.GetType().Name} - {ex.Message}");
                    }

                    // The EFFECTIVE
                    // valuation (user overrides + CurrencyDecisionDefaults'
                    // curated defaults, minus anything explicitly cleared -
                    // see ModuleSettings.GetEffectiveCurrencyValuation's own
                    // doc comment) - not the raw GetCurrencyValuation the
                    // Settings tab itself reads, which must stay default-
                    // free so it can tell a real user override apart from
                    // an applied default.
                    var currencyValuation = _settings.GetEffectiveCurrencyValuation();
                    // The per-plan valueOwnMaterials parameter drives this
                    // directly, matching how priceBasis/useOwn are also
                    // per-plan rather than read from ModuleSettings.
                    var ownMaterialsMode = valueOwnMaterials
                        ? OwnMaterialsMode.Valued
                        : OwnMaterialsMode.Free;
                    var homesteadTiers = _settings.GetHomesteadEfficiencyTiers();

                    // characterDisciplines is passed explicitly so the
                    // useOwn:false branch (snapshot: null, disabling
                    // reduction) still feeds the discipline tiebreak the
                    // same list useOwn:true does - the reported discipline
                    // must not change with the toggle.
                    // PersistAfterGenerateAsync awaits the pipeline call
                    // and saves on success only; a cancelled/failed
                    // generation propagates unchanged. myPersistGen is
                    // stamped here, synchronously, before generateTask is
                    // created - in lockstep with the view's myGen bump.
                    int myPersistGen = ++_persistGenerateSequence;

                    Task<CraftingPlanResult> generateTask = useOwn
                        ? _craftingPipeline.GenerateStructuredAsync(
                            items, _currentSnapshot, ct, progress,
                            activeChar, priceBasis, currencyValuation, ownMaterialsMode,
                            homesteadTiers, phaseProgress, requestLabel,
                            characterDisciplines: _currentSnapshot?.CharacterDisciplines)
                        : _craftingPipeline.GenerateStructuredAsync(
                            items, null, ct, progress,
                            null, priceBasis, currencyValuation, ownMaterialsMode,
                            homesteadTiers, phaseProgress, requestLabel,
                            characterDisciplines: _currentSnapshot?.CharacterDisciplines);

                    return PersistAfterGenerateAsync(generateTask, items, useOwn, priceBasis, valueOwnMaterials, myPersistGen);
                },
                _modalDialog,
                _itemSearchProvider,
                _settings,
                _planStripStatusBoard,
                (ctx, overrides, ignoredItemIds) =>
                {
                    var result = _craftingPipeline.ResolveWithOverrides(ctx, overrides, ignoredItemIds);
                    // Persist overrides/ignoredItemIds alongside the
                    // result so a restored session's decision pills start
                    // from the same baseline, not empty (see
                    // PersistResolvedPlanInBackground).
                    PersistResolvedPlanInBackground(result, overrides, ignoredItemIds);
                    return result;
                },
                itemMetadataService.GetCachedStatBlock,
                // Q13: a restored plan fetches its items' stat blocks in
                // the background so its rows can show item tooltips at
                // all. Fills only the session stat side table - see
                // ItemMetadataService.WarmStatBlocksAsync for why it is
                // not GetMetadataAsync.
                ids => itemMetadataService.WarmStatBlocksAsync(ids, CancellationToken.None)
            );

            _settingsContent = new SettingsTabContent(_settings);

            // DataDir and
            // _moduleIconTexture are both already in scope at this point in
            // Initialize() (dataDir computed at the top of this method,
            // _moduleIconTexture loaded a few lines above) - trivial
            // plumbing, no new fields needed on Module itself beyond the
            // view instance.
            _aboutContent = new AboutTabContent(this.ModuleParameters, dataDir, _moduleIconTexture);

            // SpriteScreen is the GW2 CLIENT area, not the monitor, so a
            // windowed player can legitimately be narrower than the minimum.
            // Enforcing the full minimum there would put the window's right
            // edge - cost column, Generate button, and the bottom-right
            // resize grip - off-screen with no way to drag it back, so on
            // such a client the enforced minimum falls back to the client's
            // own width and deep rows ellipsize as they used to.
            int minWindowWidth = WindowSizing.EffectiveMinWindowWidth(
                GameService.Graphics.SpriteScreen.Width);

            // The window/content regions below stay at the 930x710 pair the
            // 1024x1024 background texture (502049) was authored against -
            // they are texture-space regions, and Blish grows the content
            // region by the same delta it grows the window by, so the 46px
            // horizontal chrome they encode holds at every size. Only the
            // minimum (WindowSizing) moved; the window opens at it because
            // ResizableTabbedWindow clamps the constructed size up, on the
            // same paths that clamp a drag and a size persisted by an
            // earlier session.
            // Validated in-game to align with Event Table / Blish HUD's own
            // TabbedWindow dimensions.
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
                new Point(WindowSizing.MinWindowWidth, WindowSizing.MinWindowHeight))
            {
                Parent = GameService.Graphics.SpriteScreen,
                Title = "GW2 Crafting Helper",
                Emblem = new AsyncTexture2D(_emblemTexture),
                Id = $"{nameof(Module)}_MainWindow",

                // Clamped at 0: on a client narrower than even the 930
                // fallback a negative centered x would put the title bar
                // (and its close button) off the left edge with no way to
                // drag it back.
                Location = new Point(
                    Math.Max(0, (GameService.Graphics.SpriteScreen.Width - minWindowWidth) / 2),
                    Math.Max(0, (GameService.Graphics.SpriteScreen.Height - WindowSizing.MinWindowHeight) / 2)),
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
                    _logContent = new LogTabContent(
                        ModuleLog.Shared,
                        _modalDialog,
                        () => _logViewClearedBeforeVersion,
                        v => _logViewClearedBeforeVersion = v);
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

            _settingsTab = new Tab(
                AsyncTexture2D.FromAssetId(156736),
                () =>
                {
                    // Main thread, and the last step before Blish queues the
                    // off-thread Build - which is the whole point of doing it
                    // here rather than in Build (see BeginRebuild).
                    _settingsContent.BeginRebuild();
                    return new ViewAdapter("Settings", c => _settingsContent.Build(c));
                },
                "Settings");
            _mainWindow.Tabs.Add(_settingsTab);

            _mainWindow.Tabs.Add(new Tab(
                AsyncTexture2D.FromAssetId(157097),
                () => new ViewAdapter("About", c => _aboutContent.Build(c)),
                "About"));

            // Refresh log content when switching to the Log tab
            _mainWindow.TabChanged += (s, e) =>
            {
                // AFTER the switch, not before it: TabbedWindow2 exposes no
                // cancellable pre-change hook - see PromptForUnsavedSettings.
                if (e.PreviousValue == _settingsTab)
                {
                    PromptForUnsavedSettings();
                }

                if (_mainWindow.SelectedTab == _logTab && _logContent != null)
                {
                    _logContent.Refresh();
                }
            };

            _cornerIcon = new CornerIcon()
            {
                IconName = "GW2 Crafting Helper",
                Icon = new AsyncTexture2D(_cornerIconTexture),
                Priority = 1245846523,
                Parent = GameService.Graphics.SpriteScreen
            };

            _cornerIcon.Click += (s, e) =>
            {
                _mainWindow.ToggleWindow();
            };
        }

        /// <summary>
        /// Asks whether to keep or drop unsaved Settings edits, once the
        /// user has already left the tab.
        ///
        /// <para>
        /// The prompt is after the fact because Blish 1.3.0 has nowhere to
        /// put it earlier. Measured from the vendored binary:
        /// TabbedWindow2.SelectedTab's setter assigns the backing field via
        /// SetProperty and only then calls OnTabChanged, which itself calls
        /// ShowView (tearing down the old view) BEFORE raising the public
        /// TabChanged event. There is no pre-change event, nothing the
        /// handler can set to veto, and the one virtual member in the chain
        /// already runs after the assignment - so by the time any module
        /// code is reached, the tab has changed and cannot be changed back
        /// without triggering a second switch. See KNOWN-ISSUES "Settings
        /// dirty prompt" for the alternatives that were measured and
        /// rejected.
        /// </para>
        ///
        /// <para>
        /// Blish only unparents the outgoing view's controls
        /// (Container.ClearChildren sets Parent = null; it does not
        /// dispose), so the Settings TextBoxes still hold the user's typed
        /// text when this runs and Save persists exactly what was on
        /// screen.
        /// </para>
        ///
        /// <para>
        /// Only the tab path is hooked. The window's own Hidden event is
        /// NOT: measured in the vendored 1.3.0 binary, every WindowBase2
        /// subscribes to Gw2Mumble.PlayerCharacter.IsInCombatChanged and
        /// Gw2Instance.IsInGameChanged, both of which call Hide() when the
        /// user has Blish's "hide windows in combat" / "hide during
        /// loading" overlay options on - so entering combat with an edited
        /// field would pop a modal over gameplay. Closing the window
        /// leaves the edits in the live TextBoxes exactly as it always
        /// has: nothing tears the view down, so reopening the window shows
        /// the typed text again.
        /// </para>
        /// </summary>
        private void PromptForUnsavedSettings()
        {
            if (_settingsContent == null || _modalDialog == null) return;

            int unsaved = _settingsContent.UnsavedChangeCount();
            if (unsaved <= 0) return;

            string changeWord = unsaved == 1 ? "change" : "changes";
            _modalDialog.Show(
                $"You have {unsaved} unsaved {changeWord} on the Settings tab. Save now, or discard and keep the last saved values?",
                () => ReportSaveOutcome(_settingsContent.SaveAll()),
                () => _settingsContent.DiscardChanges(),
                "Save",
                "Discard");
        }

        // The tab's own save bar reports the outcome of a Save, but saving
        // from the prompt above happens after the view was torn down - its
        // status label is unparented by then and renders nowhere, so a
        // rejected entry would vanish with no explanation at all. Raising a
        // second dialog from the first one's callback is supported:
        // ModalDialog.Dismiss clears its pending state before running the
        // callback and skips its own Hide when the callback re-armed it, so
        // this message lands in the window that is already on screen.
        private void ReportSaveOutcome(SettingsTabContent.SaveOutcome outcome)
        {
            if (outcome.AllSaved) return;

            string message;
            if (outcome.WriteFailed)
            {
                message = "Your Settings changes could not be saved - the module log has the details. Open the Settings tab to try again?";
            }
            else
            {
                string entryWord = outcome.InvalidCount == 1 ? "entry" : "entries";
                string subject = outcome.InvalidCount == 1 ? "it" : "them";
                message = $"{outcome.InvalidCount} Settings {entryWord} could not be saved - the value was not a valid number. Everything else was saved. Open the Settings tab to re-enter {subject}?";
            }

            _modalDialog.Show(
                message,
                () => _mainWindow.SelectedTab = _settingsTab,
                null,
                "Open Settings",
                "Dismiss");
        }

        // Reads Blish's FestivalContext and projects it to plain strings,
        // invoked lazily at plan-generation time - an eager
        // Initialize()-time read could observe NotReady and silently
        // disable the feature for the session. Non-Available outcomes log
        // Info so "disabled by <availability>" is distinguishable from
        // "no festival active" (which logs nothing). This method is the
        // one place in the pipeline's call chain that touches
        // GameService.Contexts - everything below it stays Blish-free.
        private IReadOnlyList<string> ReadActiveFestivalNames()
        {
            var activeFestivalNames = new List<string>();
            try
            {
                var festivalContext = GameService.Contexts.GetContext<Blish_HUD.Contexts.FestivalContext>();
                if (festivalContext == null)
                {
                    ModuleLog.Shared.Write(ModuleLogLevel.Info, "plan", "Festival context not registered - seasonal vendor tips disabled for this plan.");
                    return activeFestivalNames;
                }

                var availability = festivalContext.TryGetActiveFestivals(out var festivalResult);
                if (availability != Blish_HUD.Contexts.ContextAvailability.Available)
                {
                    ModuleLog.Shared.Write(ModuleLogLevel.Info, "plan", $"Festival context not available ({availability}) - seasonal vendor tips disabled for this plan.");
                    return activeFestivalNames;
                }

                if (festivalResult.Value != null)
                {
                    foreach (var festival in festivalResult.Value)
                    {
                        if (!string.IsNullOrEmpty(festival.Name))
                        {
                            activeFestivalNames.Add(festival.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "plan", $"Festival context unavailable, seasonal vendor tips disabled: {ex.GetType().Name} - {ex.Message}");
            }

            return activeFestivalNames;
        }

        /// <summary>
        /// Shared shape for the static seed loads in Initialize(): open the
        /// packaged file, hand the stream to the loader, and on ANY failure
        /// log at Warn and return null so the module starts without that
        /// seed. The broad catch is deliberate - a bad or missing seed file
        /// must never block module load.
        /// </summary>
        private T LoadSeedOrNull<T>(string fileName, string unavailableLabel, Func<System.IO.Stream, T> load)
            where T : class
        {
            try
            {
                using (var stream = ContentsManager.GetFileStream(fileName))
                {
                    return load(stream);
                }
            }
            catch (Exception ex)
            {
                Logger.Info("{0}: [{1}] {2}", unavailableLabel, ex.GetType().Name, ex.Message);
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"{unavailableLabel}: [{ex.GetType().Name}] {ex.Message}");
                return null;
            }
        }

        protected override async Task LoadAsync()
        {
            // A disk-restored snapshot routes through the same drain and
            // commit gate as a network refresh, so a Clear Cache racing
            // this load composes exactly like it does against a fetch.
            int loadEpoch = _snapshotCommitGate.Epoch;
            var loadedSnapshot = _snapshotStore.LoadLatest();

            if (loadedSnapshot != null)
            {
                _snapshotCommitGate.TryCommit(loadEpoch, () =>
                {
                    _currentSnapshot = loadedSnapshot;
                    _pendingSnapshot = loadedSnapshot;
                    _snapshotDirty = true;
                });
            }

            // Same dirty-flag drain shape as the snapshot restore above -
            // _craftingContent (built in Initialize() with no plan at all)
            // is only ever pushed to from Update(), never touched directly
            // here (see Update()'s own "Applying restored plan to view"
            // block). A missing file is silent (LoadLatest returns null,
            // nothing to restore); a corrupt/old-schema file already
            // logged its own Warn inside PlanStore.LoadLatest - either way
            // this is a no-op fresh start, never a crash.
            var loadedPlan = _planStore.LoadLatest();
            if (loadedPlan != null)
            {
                _pendingPlanRestore = loadedPlan;
                _planRestoreDirty = true;
            }

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

            // The Log tab's own poll, run
            // only while it is the selected tab - a cheap Version compare
            // when nothing changed, not a full rebuild every frame. This is
            // the "PLUS a poll" half of the refresh design; TabChanged
            // above already covers "just switched to this tab".
            if (_logContent != null && _mainWindow?.SelectedTab == _logTab)
            {
                _logContent.PollForUpdates();
            }

            // "Applying restored plan to view" - mirrors the
            // _snapshotDirty block above. Runs at most once per session
            // and must stay ahead of the early returns below (a fresh
            // account with no snapshot must still restore its plan).
            // Guarded by !_generateCompletedThisSession under
            // _generateCompletionLock - see those fields' comments; only
            // the cheap check/write is inside the lock, the rendering
            // work runs outside it.
            if (_planRestoreDirty)
            {
                _planRestoreDirty = false;
                if (_pendingPlanRestore != null)
                {
                    bool shouldApplyRestore;
                    lock (_generateCompletionLock)
                    {
                        shouldApplyRestore = !_generateCompletedThisSession;
                        if (shouldApplyRestore)
                        {
                            _lastPersistedPlanMetadata = new PersistedPlanMetadata(
                                _pendingPlanRestore.GeneratedAt,
                                _pendingPlanRestore.RequestItems,
                                _pendingPlanRestore.UseOwnMaterials,
                                _pendingPlanRestore.PriceBasis,
                                _pendingPlanRestore.ValueOwnMaterials);
                        }
                    }

                    if (shouldApplyRestore)
                    {
                        _craftingContent?.ApplyRestoredPlan(
                            _pendingPlanRestore.Result,
                            _pendingPlanRestore.GeneratedAt,
                            _pendingPlanRestore.NodeOverrides,
                            _pendingPlanRestore.IgnoredItemIds,
                            _pendingPlanRestore.ValueOwnMaterials);
                    }
                }
            }

            // The auto-refresh spinner, drained on change. Above the
            // early return below on purpose: _refreshInProgress is true for
            // the whole of the refresh whose spinner this is, so a drain
            // underneath it would only ever get to switch the spinner ON
            // once the refresh had already finished.
            bool backgroundRefreshing = _backgroundRefreshInFlight;
            if (backgroundRefreshing != _backgroundRefreshSpinnerApplied && _snapshotContent != null)
            {
                _backgroundRefreshSpinnerApplied = backgroundRefreshing;
                _snapshotContent.SetBackgroundRefreshInFlight(backgroundRefreshing);
            }

            if (_refreshInProgress) return;
            if (_currentSnapshot == null) return;

            // Reads the
            // clamped setting fresh on every tick (cheap - a single
            // SettingEntry read plus two int comparisons, no I/O) rather
            // than caching it, so a Settings tab save takes effect on the
            // very next Update() without any separate live-push plumbing.
            var staleThreshold = TimeSpan.FromMinutes(_settings.GetClampedSnapshotRefreshIntervalMinutes());
            if (!StatusText.IsStale(DateTime.UtcNow - _currentSnapshot.CapturedAt, staleThreshold)) return;
            if (!_snapshotService.HasRequiredPermissions()) return;

            _ = RefreshSnapshotInBackgroundAsync();
        }

        protected override void Unload()
        {
            Gw2ApiManager.SubtokenUpdated -= OnSubtokenUpdated;

            // The SettingEntry objects outlive this module instance
            // (DefineSetting returns the existing entry on re-enable), so a
            // leftover handler would root each dead Module in turn.
            _settings.LogDiagnosticsEnabled.SettingChanged -= OnLogDiagnosticsEnabledChanged;
            _settings.LogMaxSizeBytes.SettingChanged -= OnLogMaxSizeBytesChanged;
            _settings.ClickSoundVolumePercent.SettingChanged -= OnClickSoundVolumeChanged;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();

            // The scroll-verify/resize-debounce/wheel-wrap-verify tickers
            // are parented to the SpriteScreen, not this view's control
            // tree, so nothing else tears them down on unload - this must
            // be called explicitly before disposing the host window.
            _craftingContent?.StopLiveTickers();

            // Same reasoning, same ownership: one screen-parented popup per
            // item row, each holding a global mouse subscription.
            _craftingContent?.DisposeSuggestionPanels();

            _httpClient?.Dispose();
            _modalDialog?.Dispose();
            _apiAccessDialog?.Dispose();
            _cornerIcon?.Dispose();
            _mainWindow?.Dispose();

            // The module's ONE rich tooltip surface. Like the tickers
            // above it is parented to the SpriteScreen (only while
            // visible), never to a view's control tree, so disposing the
            // window does not reach it - see Views/Rendering/
            // TooltipFacility for why there is one instance rather than
            // one per tooltip'd control.
            Views.Rendering.TooltipFacility.Shutdown();

            Views.Rendering.ClickSound.Unload();
            _settingsContent?.Teardown();

            // Module-level log system (d2-log-system.md Section 7): the
            // file-sink append/trim now happens on a background flush
            // queue, never on the calling thread (see ModuleLog's own class
            // doc comment) - give any writes already queued (e.g. from a
            // scrolldiag burst moments before unload) a brief, bounded
            // chance to land on disk before the ring is cleared. Best
            // effort only: Unload must never hang on a stuck flush (a
            // locked/very slow disk), so this is capped short rather than
            // waited on indefinitely.
            ModuleLog.Shared.WaitForPendingFileWrites(ModuleLog.FlushDrainBudget);

            // The in-memory ring is cleared only here (process exit / module
            // disable) - never by any in-tab user action. The on-disk file
            // is untouched (survives across sessions by design).
            ModuleLog.Shared.Clear();
        }

        private void OnClickSoundVolumeChanged(object sender, ValueChangedEventArgs<int> e)
        {
            Views.Rendering.ClickSound.VolumePercent = e.NewValue;
        }

        private void OnLogDiagnosticsEnabledChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            ModuleLog.Shared.DiagnosticsEnabled = e.NewValue;
        }

        // Reads the clamped accessor, not e.NewValue: Blish's Manage Modules
        // bar spans 0 to the persisted value, so it can deliver a byte count
        // below the 1 MB floor the tab's own parser enforces.
        private void OnLogMaxSizeBytesChanged(object sender, ValueChangedEventArgs<int> e)
        {
            ModuleLog.Shared.MaxFileSizeBytes = _settings.GetClampedLogMaxSizeBytes();
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
            ModuleLog.Shared.Write(ModuleLogLevel.Info, "snapshot", "Refreshing account snapshot...");

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
                ModuleLog.Shared.Write(ModuleLogLevel.Info, "snapshot", $"Discarding snapshot fetch superseded by Clear Cache (epoch {myEpoch})");
                return null;
            }

            // Null CharacterDisciplines ("never captured") must stay
            // distinguishable in the log from an empty list ("captured,
            // nobody has any"), so the null case gets its own wording.
            string disciplinesLogText = snapshot.CharacterDisciplines != null
                ? $"{snapshot.CharacterDisciplines.Count} character disciplines"
                : "disciplines not captured";

            Logger.Info("Fetched snapshot CapturedAt={0:o} items={1} wallet={2} coin={3}, {4}",
                snapshot.CapturedAt, snapshot.Items.Count, snapshot.Wallet.Count, snapshot.CoinCopper, disciplinesLogText);
            ModuleLog.Shared.Write(ModuleLogLevel.Info, "snapshot",
                $"Fetched snapshot CapturedAt={snapshot.CapturedAt:o} items={snapshot.Items.Count} wallet={snapshot.Wallet.Count} coin={snapshot.CoinCopper}, {disciplinesLogText}");

            return snapshot;
        }

        private async Task RefreshSnapshotInBackgroundAsync()
        {
            if (_refreshInProgress) return;

            // Refuse to auto-retrigger again so
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

            // Set only past both early returns above, so a tick that
            // declines to refresh (already running, or inside the failure
            // backoff) never spins a spinner over nothing.
            _backgroundRefreshInFlight = true;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();

            try
            {
                var snapshot = await FetchAndSaveSnapshotAsync(_refreshCts.Token);
                Interlocked.Exchange(ref _lastFailedRefreshAttemptTicks, 0);
                if (snapshot != null)
                {
                    var status = $"Updated \u2014 {snapshot.CapturedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)}";
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
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "snapshot", $"Failed to refresh account snapshot: {ex.GetType().Name} - {ex.Message}");
                Interlocked.Exchange(ref _lastFailedRefreshAttemptTicks, DateTime.UtcNow.Ticks);

                // KNOWN-ISSUES #37 follow-up: status-text parity with
                // MainView.RefreshNowAsync's own catch block - this
                // background/auto-refresh path (module load, every stale-
                // snapshot Update() tick, OnSubtokenUpdated) can hit the
                // exact same InvalidAccessTokenException root cause as the
                // manual Refresh Now button, so it must not fall back to
                // the old bare, uninformative "Refresh failed" label.
                // Deliberately does NOT pop ApiAccessDialog here - see
                // KNOWN-ISSUES #37 follow-up for why unprompted background
                // popups are a separate, deferred UX call.
                var classification = SnapshotFailureClassifier.Classify(ex);
                string cause = StatusText.ForRefreshFailure(classification.Kind, classification.FailedSourceCount, classification.TotalSourceCount);
                var status = $"{cause} \u2014 {DateTime.Now.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture)}";
                SaveStatusThreadSafe(status);
            }
            finally
            {
                _backgroundRefreshInFlight = false;
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

        /// <summary>
        /// The four values the persist paths and Update()'s restore drain
        /// must agree on atomically (see _lastPersistedPlanMetadata). A
        /// plain immutable data holder, private to Module.
        /// </summary>
        private sealed class PersistedPlanMetadata
        {
            public DateTime GeneratedAt { get; }
            public IReadOnlyList<PlanRequestItem> RequestItems { get; }
            public bool UseOwnMaterials { get; }
            public PriceBasis PriceBasis { get; }
            public bool ValueOwnMaterials { get; }

            public PersistedPlanMetadata(
                DateTime generatedAt,
                IReadOnlyList<PlanRequestItem> requestItems,
                bool useOwnMaterials,
                PriceBasis priceBasis,
                bool valueOwnMaterials)
            {
                GeneratedAt = generatedAt;
                RequestItems = requestItems;
                UseOwnMaterials = useOwnMaterials;
                PriceBasis = priceBasis;
                ValueOwnMaterials = valueOwnMaterials;
            }
        }

        /// <summary>
        /// Awaits a Generate call and, only on success, persists the full
        /// result alongside the original request and a fresh timestamp.
        /// No Task.Run needed: the post-await continuation already
        /// resumes on a ThreadPool thread (Blish's XNA host installs no
        /// SynchronizationContext). A cancelled/failed generateTask
        /// propagates unchanged and persistence never runs.
        /// <para>
        /// <paramref name="myPersistGen"/> is this call's stamp from
        /// _persistGenerateSequence - a second Generate CAN start while
        /// this await is pending, so the disk write is skipped when a
        /// newer call has since started.
        /// </para>
        /// </summary>
        private async Task<CraftingPlanResult> PersistAfterGenerateAsync(
            Task<CraftingPlanResult> generateTask,
            IReadOnlyList<PlanRequestItem> requestItems,
            bool useOwnMaterials,
            PriceBasis priceBasis,
            bool valueOwnMaterials,
            int myPersistGen)
        {
            var result = await generateTask;

            // See _generateCompletedThisSession/_generateCompletionLock's
            // own doc comments for why this is set unconditionally, under
            // the lock, before the stale-generation check below.
            lock (_generateCompletionLock)
            {
                _generateCompletedThisSession = true;
            }

            if (myPersistGen != _persistGenerateSequence) return result;

            var generatedAt = DateTime.Now;
            var metadata = new PersistedPlanMetadata(
                generatedAt, requestItems, useOwnMaterials, priceBasis, valueOwnMaterials);
            lock (_generateCompletionLock)
            {
                _lastPersistedPlanMetadata = metadata;
            }

            // A fresh Generate always starts the override loop clean (see
            // TreeSectionController.ResetForNewPlan) - the persisted
            // overrides/ignored-item-ids for a plan that was JUST generated
            // are therefore always empty, never whatever an earlier
            // (now-superseded) plan's own overrides happened to be.
            _planStore.Save(new PersistedPlan
            {
                // Set explicitly, never via a property initializer - see
                // PersistedPlan.SchemaVersion.
                SchemaVersion = PersistedPlan.CurrentSchemaVersion,
                GeneratedAt = generatedAt,
                RequestItems = requestItems,
                UseOwnMaterials = useOwnMaterials,
                PriceBasis = priceBasis,
                ValueOwnMaterials = valueOwnMaterials,
                Result = result,
                NodeOverrides = new Dictionary<int, AcquisitionSource>(),
                IgnoredItemIds = new List<int>()
            });

            return result;
        }

        // Latest-write-wins coalescing for
        // PersistResolvedPlanInBackground's disk writes: a full persist
        // is multi-hundred-KB, and rapid pill clicking must not queue one
        // write per click. Only the newest pending write is kept; a
        // superseded one is dropped before it reaches PlanStore.Save.
        private readonly object _pendingPlanSaveLock = new object();
        private PersistedPlan _pendingPlanSave;
        private bool _planSaveWorkerRunning;

        /// <summary>
        /// Persists an override-updated result "in place" - same
        /// GeneratedAt/original request as the last full Generate (or the
        /// restored plan), only Result/NodeOverrides/IgnoredItemIds
        /// updated. The caller runs on the MAIN thread (a pill Click
        /// chain), so the file write is dispatched to a background worker
        /// - no file I/O on the UI thread. Fire-and-forget: a slow or
        /// failing write never delays the click's own re-solve/render.
        /// Can race PersistAfterGenerateAsync's write; PlanStore.Save's
        /// internal lock prevents corruption and whichever write lands
        /// last wins. No-ops if nothing was ever persisted this session
        /// (a defensive guard, unreachable in practice).
        /// <para>
        /// <paramref name="overrides"/>/<paramref name="ignoredItemIds"/>
        /// are the same mutable collections TreeSectionController holds -
        /// copied here, synchronously, before this method returns, so the
        /// background write never holds a live reference a later pill
        /// click could still be mutating.
        /// </para>
        /// </summary>
        private void PersistResolvedPlanInBackground(
            CraftingPlanResult result,
            IReadOnlyDictionary<int, AcquisitionSource> overrides,
            ISet<int> ignoredItemIds)
        {
            var metadata = _lastPersistedPlanMetadata;
            if (metadata == null) return;

            // Copied via an explicit loop, not the Dictionary(IDictionary<>)
            // constructor - overrides is IReadOnlyDictionary<>, which does
            // not implement IDictionary<> (a separate interface), and this
            // project's target framework has no Dictionary constructor
            // overload accepting a bare IEnumerable<KeyValuePair<>> either.
            var overridesSnapshot = new Dictionary<int, AcquisitionSource>();
            if (overrides != null)
            {
                foreach (var kvp in overrides)
                {
                    overridesSnapshot[kvp.Key] = kvp.Value;
                }
            }

            var ignoredSnapshot = new List<int>();
            if (ignoredItemIds != null)
            {
                foreach (int itemId in ignoredItemIds)
                {
                    ignoredSnapshot.Add(itemId);
                }
            }

            var persisted = new PersistedPlan
            {
                // Set explicitly, never via a property initializer - see
                // PersistedPlan.SchemaVersion.
                SchemaVersion = PersistedPlan.CurrentSchemaVersion,
                GeneratedAt = metadata.GeneratedAt,
                RequestItems = metadata.RequestItems,
                UseOwnMaterials = metadata.UseOwnMaterials,
                PriceBasis = metadata.PriceBasis,
                ValueOwnMaterials = metadata.ValueOwnMaterials,
                Result = result,
                NodeOverrides = overridesSnapshot,
                IgnoredItemIds = ignoredSnapshot
            };

            lock (_pendingPlanSaveLock)
            {
                _pendingPlanSave = persisted;
                if (_planSaveWorkerRunning) return;
                _planSaveWorkerRunning = true;
            }

            Task.Run(() => DrainPendingPlanSaves());
        }

        /// <summary>
        /// Background worker loop for <see cref="PersistResolvedPlanInBackground"/>'s
        /// coalescing - see <see cref="_pendingPlanSaveLock"/>'s own doc
        /// comment. Runs until no further write is pending, saving only the
        /// LATEST one queued at each pass; a write that lands while a
        /// previous one is already mid-Save is picked up by the next loop
        /// iteration instead of spawning a second concurrent worker.
        /// </summary>
        private void DrainPendingPlanSaves()
        {
            while (true)
            {
                PersistedPlan next;
                lock (_pendingPlanSaveLock)
                {
                    next = _pendingPlanSave;
                    _pendingPlanSave = null;
                    if (next == null)
                    {
                        _planSaveWorkerRunning = false;
                        return;
                    }
                }

                _planStore.Save(next);
            }
        }

        private static void BuildPlaceholder(Container container)
        {
            new Label()
            {
                Text = "Coming Soon",
                Font = UiFonts.Body,
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
