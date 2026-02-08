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

        #region Service Managers
        internal ContentsManager ContentsManager => this.ModuleParameters.ContentsManager;
        internal DirectoriesManager DirectoriesManager => this.ModuleParameters.DirectoriesManager;
        internal Gw2ApiManager Gw2ApiManager => this.ModuleParameters.Gw2ApiManager;
        #endregion

        private CornerIcon _cornerIcon;
        private StandardWindow _mainWindow;
        private MainView _mainView;

        private SnapshotStore _snapshotStore;
        private Gw2AccountSnapshotService _snapshotService;
        private AccountSnapshot _currentSnapshot;
        private AccountSnapshot _pendingSnapshot;
        private bool _snapshotDirty;

        private CancellationTokenSource _refreshCts;
        private bool _refreshInProgress;

        [ImportingConstructor]
        public Module([Import("ModuleParameters")] ModuleParameters moduleParameters) : base(moduleParameters) { }

        protected override void DefineSettings(SettingCollection settings) { }

        protected override void Initialize()
        {
            _snapshotStore = new SnapshotStore(DirectoriesManager.GetFullDirectoryPath("data"));
            _snapshotService = new Gw2AccountSnapshotService(Gw2ApiManager);

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

            _cornerIcon.Click += (s, e) => {
                if (_mainView == null)
                {
                    _mainView = new MainView(_currentSnapshot, RefreshSnapshotAsync);
                }
                else
                {
                    _mainView.SetSnapshot(_currentSnapshot);
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
                await RefreshSnapshotAsync();
            }
        }

        protected override void Update(GameTime gameTime)
        {
            if (_snapshotDirty)
            {
                Logger.Info("Applying snapshot to view CapturedAt={0:o}", _pendingSnapshot?.CapturedAt);
                _snapshotDirty = false;
                _mainView?.SetSnapshot(_pendingSnapshot);
            }

            if (_refreshInProgress) return;
            if (_currentSnapshot == null) return;
            if (DateTime.UtcNow - _currentSnapshot.CapturedAt < StaleThreshold) return;
            if (!_snapshotService.HasRequiredPermissions()) return;

            // Fire-and-forget refresh; UI will update if window is open.
            _ = RefreshSnapshotAsync();
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
                _ = RefreshSnapshotAsync();
            }
        }

        private async Task RefreshSnapshotAsync()
        {
            if (_refreshInProgress) return;
            _refreshInProgress = true;

            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();

            try
            {
                Logger.Info("Refreshing account snapshot...");

                var snapshot = await _snapshotService.FetchSnapshotAsync(_refreshCts.Token);

                _currentSnapshot = snapshot;
                _snapshotStore.Save(snapshot);

                _pendingSnapshot = _currentSnapshot;
                _snapshotDirty = true;

                Logger.Info("Fetched snapshot CapturedAt={0:o} items={1} wallet={2} coin={3}",
                    snapshot.CapturedAt, snapshot.Items.Count, snapshot.Wallet.Count, snapshot.CoinCopper);


            }
            catch (OperationCanceledException)
            {
                Logger.Debug("Snapshot refresh cancelled");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to refresh account snapshot");
            }
            finally
            {
                _refreshInProgress = false;
            }
        }
    }
}
