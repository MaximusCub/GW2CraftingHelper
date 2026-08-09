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

        // Wave-3 quick win #4 (2026-08-06 field testing): the Log tab's
        // "Clear view" floor (the ring version below which entries are
        // hidden from the CURRENT view only - see LogTabContent's own
        // _getClearedBeforeVersion/_setClearedBeforeVersion doc comments)
        // lives HERE, on Module, rather than on LogTabContent itself,
        // because Blish reconstructs a brand new LogTabContent every time
        // the Log tab is selected (the tab's own view-factory below calls
        // "new LogTabContent(...)" on every build, per
        // docs/ARCHITECTURE.md Section 1's "Build() itself also runs off
        // the main thread") - a field on that short-lived instance cannot
        // survive a tab switch away and back, which is exactly the bug a
        // user hit in the field: a cleared view "resurrected" every time
        // they reopened the Log tab. This field persists for the whole
        // module session instead, exactly like _logContent/_logTab
        // themselves.
        // <para>
        // Threading (PR #101 rules): a plain long is enough - no
        // volatile/Interlocked needed - because every access to this field
        // is main-thread-only. It is WRITTEN only from the Clear-view
        // button's Click handler (a genuine Blish input event, always
        // dispatched on the main thread, same as every other .Click
        // handler in this codebase used without marshaling). It is READ
        // only from LogTabContent.GetFilteredEntries (RebuildRows' own
        // helper) and AppendNewRows, both of which LogTabContent's own
        // _buildComplete gate already restricts to running main-thread-only
        // (see that field's doc comment in LogTabContent.cs). The one place this
        // field IS touched from a ThreadPool thread - the tab's
        // view-factory closure just below, which runs off the main thread -
        // only ever PASSES two delegates that close over this field into
        // LogTabContent's constructor; it never reads or writes the field's
        // value itself, so that ThreadPool-thread touch is a plain
        // reference/delegate copy (always atomic), not a field
        // dereference.
        // </para>
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

        // W3D (plan persistence across module restarts): a generated plan
        // loaded from disk at LoadAsync time, applied to _craftingContent
        // from Update() - same dirty-flag drain shape as
        // _pendingSnapshot/_snapshotDirty above (see that pair's own
        // comments and Update()'s "Applying snapshot to view" block).
        // _pendingPlanRestore is only ever non-null together with
        // _planRestoreDirty == true; both are written once, in LoadAsync,
        // and drained (never re-armed) the first time Update() sees the
        // flag set.
        private PersistedPlan _pendingPlanRestore;
        private bool _planRestoreDirty;

        // W3D: the original request/timestamp behind the MOST RECENTLY
        // persisted plan - the last successful Generate this session, or
        // (if none has run yet) the restored plan loaded from disk. A
        // later ResolveWithOverrides persist (see the resolveOverridesSync
        // wiring below) reuses these as-is: a local override re-solve does
        // not change what was requested or re-fetch prices, so the
        // persisted GeneratedAt/request must not silently advance just
        // because the user clicked a decision pill - see
        // PersistAfterGenerateAsync's own doc comment. Written only from
        // Update() (the restore drain, main thread) and
        // PersistAfterGenerateAsync's post-await continuation (a
        // ThreadPool thread with no SynchronizationContext) - both of
        // which are sequenced strictly before any point a pill click could
        // read them (Update()'s drain runs before the tab can even be
        // built; PersistAfterGenerateAsync's write happens-before
        // TriggerGenerate's own MainThreadMarshal.Run callback, which is
        // what first renders the clickable pills a user could react to -
        // see PersistAfterGenerateAsync's own comment), so no lock is
        // needed for this cross-thread handoff, the same reasoning
        // _lastDebugLog and other CraftingPlanView state already relies on.
        private DateTime _lastPersistedPlanGeneratedAt;
        private IReadOnlyList<PlanRequestItem> _lastPersistedPlanRequestItems;
        private bool _lastPersistedPlanUseOwnMaterials;
        private PriceBasis _lastPersistedPlanPriceBasis;

        private HttpClient _httpClient;
        private CraftingPlanPipeline _craftingPipeline;
        private PlanStore _planStore;

        // W3B gate round 1 fix (tab-switch strip freeze/lost completion
        // status - see docs/KNOWN-ISSUES.md's W3B section and
        // Services/PlanStripStatusBoard.cs's own doc comment for the full
        // rationale). Lives here rather than as a CraftingPlanView field so
        // it survives independently of any single view build cycle, same
        // module-level-state-outlives-a-view-rebuild precedent as
        // _logViewClearedBeforeVersion above - unlike that field, this is
        // a genuinely thread-safe object (constructor-injected once into
        // CraftingPlanView below, not re-injected per Build() the way
        // LogTabContent's getter/setter delegates are, since
        // CraftingPlanView itself is a singleton Module constructs exactly
        // once - see the field's own doc comment for why a single
        // reference is enough here).
        private readonly PlanStripStatusBoard _planStripStatusBoard = new PlanStripStatusBoard();
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

            // M39 (log system): configured before any other store so their
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
            ModuleLog.Shared.DiagnosticsEnabled = _settings.LogDiagnosticsEnabled.Value;

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

            // WP-16 shape (d2-log-system.md Section 4.2/11): every other
            // store's IO-failure callback routes to ModuleLog (in addition
            // to whatever it already does) rather than the previous silent
            // Debug.WriteLine, so a store failure is now visible in-module
            // via the Log tab, not just in an attached debugger.
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
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"Acquisition hints unavailable: [{ex.GetType().Name}] {ex.Message}");
                acquisitionHints = null;
            }

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

            _craftingPipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi, cacheStore: recipeCacheStore),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi, itemNameSeed),
                _vendorOfferStore,
                reducer: new InventoryReducer(),
                accountRecipeClient: new Gw2AccountRecipeClient(Gw2ApiManager),
                currencyMetadataService: new CurrencyMetadataService(_httpClient),
                acquisitionHints: acquisitionHints);

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
                _emblemTexture = ContentsManager.GetTexture("emblem.png");
            }
            catch (Exception ex)
            {
                ModuleLog.Shared.Write(ModuleLogLevel.Warn, "startup", $"Emblem texture load failed, reusing the module icon: {ex.GetType().Name} - {ex.Message}");
                _emblemTexture = _moduleIconTexture;
            }

            _modalDialog = new ModalDialog(_settings);
            _apiAccessDialog = new ApiAccessDialog();

            _snapshotContent = new MainView(
                _currentSnapshot,
                _lastStatus,
                UserRefreshAsync,
                _apiAccessDialog,
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
                // W3B: gained phaseProgress (live coarse-phase events) and
                // requestLabel (best-effort item-name label) as two new
                // trailing lambda parameters, both forwarded straight
                // through to the pipeline - see
                // CraftingPlanPipeline.GenerateStructuredAsync's matching
                // parameters.
                (items, useOwn, priceBasis, ct, progress, phaseProgress, requestLabel) =>
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

                    var currencyValuation = _settings.GetCurrencyValuation();
                    var ownMaterialsMode = _settings.GetOwnMaterialsMode();
                    var homesteadTiers = _settings.GetHomesteadEfficiencyTiers();

                    // W3C review-fix (mustFix): per-character discipline
                    // data is cosmetic account info (see AccountSnapshot.
                    // CharacterDisciplines' own doc comment), not part of
                    // owned-materials reduction - it must not disappear, and
                    // the discipline the plan REPORTS must not change,
                    // depending on whether the user has "Use Own Materials"
                    // on for this one generation. Passing characterDisciplines
                    // explicitly (rather than relying on the pipeline's
                    // snapshot?.CharacterDisciplines fallback) means the
                    // useOwn:false branch below - which still correctly
                    // passes snapshot: null to disable reduction/the
                    // force-buy pre-pass/owned-currency annotation, all
                    // gated on snapshot != null inside the pipeline (see
                    // CraftingPlanPipeline.GenerateStructuredAsync's own
                    // snapshot != null checks) - feeds Build()'s
                    // discipline tiebreak the SAME list useOwn:true does.
                    // That keeps the reported discipline identical between
                    // the two modes and stable across a later
                    // ResolveWithOverrides re-solve (PlanSolveContext.
                    // CharacterDisciplines is populated from this same
                    // value at generation time - see CraftingPlanPipeline's
                    // own SolveContext construction).
                    // W3D (plan persistence across module restarts):
                    // PersistAfterGenerateAsync awaits the pipeline call
                    // and, on success only, saves the full result (plus
                    // this request's items/useOwn/priceBasis and a fresh
                    // timestamp) to disk - see that method's own doc
                    // comment for why this needs no extra Task.Run
                    // dispatch. A cancelled/failed generation propagates
                    // its exception through unchanged (persistence never
                    // runs) - see PersistAfterGenerateAsync's own doc
                    // comment.
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

                    return PersistAfterGenerateAsync(generateTask, items, useOwn, priceBasis);
                },
                _modalDialog,
                _itemSearchProvider,
                _settings,
                _planStripStatusBoard,
                (ctx, overrides, ignoredItemIds) =>
                {
                    var result = _craftingPipeline.ResolveWithOverrides(ctx, overrides, ignoredItemIds);
                    PersistResolvedPlanInBackground(result);
                    return result;
                }
            );

            _settingsContent = new SettingsTabContent(_settings);

            // M39 (d1-snapshot-about-settings.md Feature 2): dataDir and
            // _moduleIconTexture are both already in scope at this point in
            // Initialize() (dataDir computed at the top of this method,
            // _moduleIconTexture loaded a few lines above) - trivial
            // plumbing, no new fields needed on Module itself beyond the
            // view instance.
            _aboutContent = new AboutTabContent(this.ModuleParameters, dataDir, _moduleIconTexture);

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
                    _logContent = new LogTabContent(
                        ModuleLog.Shared,
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

            _mainWindow.Tabs.Add(new Tab(
                AsyncTexture2D.FromAssetId(156736),
                () => new ViewAdapter("Settings", c => _settingsContent.Build(c)),
                "Settings"));

            _mainWindow.Tabs.Add(new Tab(
                AsyncTexture2D.FromAssetId(157097),
                () => new ViewAdapter("About", c => _aboutContent.Build(c)),
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
            // KNOWN-ISSUES "M37 desktop-wave observations" note (a): a
            // snapshot restored from disk here previously never reached the
            // Snapshot tab, because _snapshotContent (built in Initialize()
            // with the then-null _currentSnapshot) is only ever pushed to
            // via the _pendingSnapshot/_snapshotDirty drain in Update() -
            // and that drain used to fire only after a successful network
            // refresh committed through _snapshotCommitGate. Routed through
            // the same drain and the same gate here, so a Clear Cache
            // racing this disk load composes exactly like it already does
            // against a network fetch (KNOWN-ISSUES 31a-F1) - see
            // SnapshotCommitGate's own doc comment.
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

            // W3D (plan persistence across module restarts): same
            // dirty-flag drain shape as the snapshot restore just above -
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

            // M39 (log system, d2 Section 4.3): the Log tab's own poll, run
            // only while it is the selected tab - a cheap Version compare
            // when nothing changed, not a full rebuild every frame. This is
            // the "PLUS a poll" half of the refresh design; TabChanged
            // above already covers "just switched to this tab".
            if (_logContent != null && _mainWindow?.SelectedTab == _logTab)
            {
                _logContent.PollForUpdates();
            }

            // W3D (plan persistence across module restarts): "Applying
            // restored plan to view" - mirrors the _snapshotDirty block
            // above exactly (see LoadAsync's matching comment). Runs at
            // most once per module session, and must stay ahead of the
            // _refreshInProgress/_currentSnapshot early returns below - a
            // fresh account with no snapshot yet must still restore its
            // persisted plan.
            if (_planRestoreDirty)
            {
                _planRestoreDirty = false;
                if (_pendingPlanRestore != null)
                {
                    _lastPersistedPlanGeneratedAt = _pendingPlanRestore.GeneratedAt;
                    _lastPersistedPlanRequestItems = _pendingPlanRestore.RequestItems;
                    _lastPersistedPlanUseOwnMaterials = _pendingPlanRestore.UseOwnMaterials;
                    _lastPersistedPlanPriceBasis = _pendingPlanRestore.PriceBasis;
                    _craftingContent?.ApplyRestoredPlan(_pendingPlanRestore.Result, _pendingPlanRestore.GeneratedAt);
                }
            }

            if (_refreshInProgress) return;
            if (_currentSnapshot == null) return;

            // M39 (d1-snapshot-about-settings.md Feature 3): reads the
            // clamped setting fresh on every tick (cheap - a single
            // SettingEntry read plus two int comparisons, no I/O) rather
            // than caching it, so a Settings tab save takes effect on the
            // very next Update() without any separate live-push plumbing.
            var staleThreshold = TimeSpan.FromMinutes(_settings.GetClampedSnapshotRefreshIntervalMinutes());
            if (DateTime.UtcNow - _currentSnapshot.CapturedAt < staleThreshold) return;
            if (!_snapshotService.HasRequiredPermissions()) return;

            _ = RefreshSnapshotInBackgroundAsync();
        }

        protected override void Unload()
        {
            Gw2ApiManager.SubtokenUpdated -= OnSubtokenUpdated;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();

            // WP-17 (FrameTicker teardown-on-Unload): the scroll-verify/
            // resize-debounce/wheel-wrap-verify tickers are parented to the
            // SpriteScreen, not this view's own control tree (see their own
            // field comments in CraftingPlanView), so nothing else tears
            // them down when the module unloads while a tab holding this
            // view is open and a ticker is mid-flight - this must be called
            // explicitly, before disposing the window that hosts the view.
            _craftingContent?.StopLiveTickers();

            _httpClient?.Dispose();
            _modalDialog?.Dispose();
            _apiAccessDialog?.Dispose();
            _cornerIcon?.Dispose();
            _mainWindow?.Dispose();

            // Module-level log system (d2-log-system.md Section 7): the
            // file-sink append/trim now happens on a background flush
            // queue, never on the calling thread (see ModuleLog's own class
            // doc comment) - give any writes already queued (e.g. from a
            // scrolldiag burst moments before unload) a brief, bounded
            // chance to land on disk before the ring is cleared. Best
            // effort only: Unload must never hang on a stuck flush (a
            // locked/very slow disk), so this is capped short rather than
            // waited on indefinitely.
            ModuleLog.Shared.WaitForPendingFileWrites(TimeSpan.FromMilliseconds(250));

            // The in-memory ring is cleared only here (process exit / module
            // disable) - never by any in-tab user action. The on-disk file
            // is untouched (survives across sessions by design).
            ModuleLog.Shared.Clear();
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

            // W3C polish (review nice-to-have): CharacterDisciplines is null
            // only when this snapshot never captured per-character
            // discipline data at all (a pre-W3C snapshot.json, or a
            // degraded fetch - see the field's own doc comment on
            // AccountSnapshot); that must stay distinguishable in the log
            // from "captured, and it happens to be an empty list" (e.g. a
            // zero-character account), so the null case gets its own
            // wording rather than folding into a count of 0.
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
                var status = $"{cause} \u2014 {DateTime.Now:t}";
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

        /// <summary>
        /// W3D (plan persistence across module restarts): awaits a Generate
        /// call and, only on success, persists the full result alongside
        /// the original request and a fresh timestamp - see PlanStore.cs's
        /// own doc comment. Awaited here rather than wrapped in Task.Run:
        /// once <paramref name="generateTask"/> completes, this method's
        /// own continuation resumes on a ThreadPool thread (Blish HUD's
        /// XNA host installs no SynchronizationContext -
        /// docs/ARCHITECTURE.md section 1), the exact same reasoning
        /// FetchAndSaveSnapshotAsync's own post-await _snapshotStore.Save
        /// call already relies on - so the write below is already off the
        /// UI thread with no extra dispatch needed. A cancelled/failed
        /// generateTask propagates its exception out of the `await`
        /// unchanged (persistence never runs, and _lastPersistedPlan*
        /// below is left at whatever it already held) - this method adds
        /// no new error handling, matching CraftingPlanView.TriggerGenerate's
        /// own catch block, which is unaffected by this wrapper.
        /// <para>
        /// No generation-sequence guard is needed here (unlike
        /// CraftingPlanView's own myGen checks): this whole method is now
        /// part of the single Task CraftingPlanView.TriggerGenerate awaits
        /// as `_generateAsync(...)`, which it only ever calls with
        /// `_generateButton.Enabled = false` and does not re-enable until
        /// its own `finally` runs AFTER that await completes - so a second
        /// Generate (button click, or the "Use Own Materials" toggle's
        /// modal-confirm path, which itself only re-fires TriggerGenerate
        /// once a PRIOR generation already finished and set _currentPlan)
        /// cannot start while this method is still running for an earlier
        /// one. If that invariant ever changes (e.g. a future
        /// cancel-and-regenerate feature), this method would need the same
        /// stale-write guard _generateSequence gives every other
        /// generation-completion path.
        /// </para>
        /// </summary>
        private async Task<CraftingPlanResult> PersistAfterGenerateAsync(
            Task<CraftingPlanResult> generateTask,
            IReadOnlyList<PlanRequestItem> requestItems,
            bool useOwnMaterials,
            PriceBasis priceBasis)
        {
            var result = await generateTask;

            var generatedAt = DateTime.Now;
            _lastPersistedPlanGeneratedAt = generatedAt;
            _lastPersistedPlanRequestItems = requestItems;
            _lastPersistedPlanUseOwnMaterials = useOwnMaterials;
            _lastPersistedPlanPriceBasis = priceBasis;

            _planStore.Save(new PersistedPlan
            {
                GeneratedAt = generatedAt,
                RequestItems = requestItems,
                UseOwnMaterials = useOwnMaterials,
                PriceBasis = priceBasis,
                Result = result
            });

            return result;
        }

        /// <summary>
        /// W3D: persists an override-updated result "in place" - same
        /// GeneratedAt/original request as the plan's last full Generate
        /// (or, if none has run yet this session, the restored plan
        /// LoadAsync applied - see _lastPersistedPlan* fields' own doc
        /// comment), only Result itself updated. Unlike
        /// PersistAfterGenerateAsync above, the caller here
        /// (ResolveWithOverrides' wiring lambda) runs synchronously on the
        /// MAIN thread - a pill Click handler chain, see
        /// TreeSectionController.ApplyOverridesAndResolve - so the actual
        /// file write is dispatched to a background thread via Task.Run
        /// rather than running inline (docs/ARCHITECTURE.md section 1, "no
        /// file I/O on the UI thread"). Fire-and-forget: never awaited, so
        /// a slow or failing write can never delay the click's own
        /// synchronous re-solve/render; PlanStore.Save's own internal
        /// try/catch still logs a Warn on failure exactly like every other
        /// store. This can race PersistAfterGenerateAsync's own write (an
        /// override pill on an OLD plan stays clickable while a NEW
        /// Generate is in flight) - PlanStore.Save's own internal lock
        /// (see that class's doc comment) keeps two such overlapping
        /// writers from ever corrupting the same .tmp path; whichever
        /// write lands last on disk simply wins, same as any other
        /// last-write-wins file, and self-heals on the next successful
        /// persist either way. No-ops if nothing has ever been persisted
        /// this session
        /// (_lastPersistedPlanRequestItems still null) - unreachable in
        /// practice (ResolveWithOverrides is only reachable once a plan,
        /// generated or restored, already exists to click overrides on -
        /// see TreeSectionController.ApplyOverridesAndResolve's own
        /// _lastResult?.SolveContext == null bail), kept as a defensive
        /// guard rather than an assumed invariant.
        /// </summary>
        private void PersistResolvedPlanInBackground(CraftingPlanResult result)
        {
            if (_lastPersistedPlanRequestItems == null) return;

            var persisted = new PersistedPlan
            {
                GeneratedAt = _lastPersistedPlanGeneratedAt,
                RequestItems = _lastPersistedPlanRequestItems,
                UseOwnMaterials = _lastPersistedPlanUseOwnMaterials,
                PriceBasis = _lastPersistedPlanPriceBasis,
                Result = result
            };
            Task.Run(() => _planStore.Save(persisted));
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
