using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Views;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GW2CraftingHelper
{
    [Export(typeof(Blish_HUD.Modules.Module))]
    public class Module : Blish_HUD.Modules.Module
    {
        private static readonly Logger Logger = Logger.GetLogger<Module>();
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(10);

        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;

        private CornerIcon _cornerIcon;
        private StandardWindow _mainWindow;
        private MainView _mainView;

        private SnapshotStore _snapshotStore;
        private StatusStore _statusStore;
        private Gw2AccountSnapshotService _snapshotService;
        private AccountSnapshot _currentSnapshot;
        private AccountSnapshot _pendingSnapshot;
        private bool _snapshotDirty;
        private string _lastStatus;

        private CancellationTokenSource _refreshCts;
        private bool _refreshInProgress;

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        protected override void DefineSettings(SettingCollection settings) { }

        protected override void Initialize()
        {
            string dataDir = DirectoriesManager.GetFullDirectoryPath("data");
            _snapshotStore = new SnapshotStore(dataDir);
            _statusStore = new StatusStore(dataDir);
            _snapshotService = new Gw2AccountSnapshotService(Gw2ApiManager);
            _lastStatus = _statusStore.Load();

            Texture2D iconTexture;
            try
            {
                iconTexture = ContentsManager.GetTexture("icon.png");
            }
            catch
            {
                iconTexture = ContentService.Textures.Error;
            }

            _mainWindow = new StandardWindow(
                AsyncTexture2D.FromAssetId(155997),
                new Rectangle(25, 26, 560, 640),
                new Rectangle(40, 50, 540, 590)
            )
            {
                Parent = GameService.Graphics.SpriteScreen,
                Title = "GW2 Crafting Helper",
                Id = $"{nameof(Module)}_MainWindow_38d37290",
                SavesPosition = true
            };

            _cornerIcon = new CornerIcon()
            {
                IconName = "GW2 Crafting Helper",
                Icon = new AsyncTexture2D(iconTexture),
                Priority = 1245846523,
                Parent = GameService.Graphics.SpriteScreen
            };

            _cornerIcon.Click += (s, e) =>
            {
                if (_mainView == null)
                {
                    _mainView = new MainView(
                        _currentSnapshot,
                        _lastStatus,
                        UserRefreshAsync,
                        ClearCache,
                        SaveStatus
                    );
                }
                else
                {
                    _mainView.SetSnapshot(_currentSnapshot);
                    _mainView.SetStatus(_lastStatus);
                }

                _mainWindow.ToggleWindow(_mainView);
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
                _mainView?.SetSnapshot(_pendingSnapshot);
                _mainView?.SetStatus(_lastStatus);
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
            _mainView?.SetStatus(_lastStatus);
        }
    }
}
