using System;
using System.Linq;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    public class ModalDialog : IDisposable
    {
        private const string WindowId = "GW2CraftingHelper_ModalDialog_c4f19a";

        private readonly StandardWindow _window;
        private readonly ModuleSettings _settings;
        private bool _isShowing;
        private Action _onConfirm;
        private Action _onCancel;

        public ModalDialog(ModuleSettings settings)
        {
            _settings = settings;

            _window = new StandardWindow(
                AsyncTexture2D.FromAssetId(155997),
                new Rectangle(0, 0, 370, 120),
                new Rectangle(30, 30, 310, 60))
            {
                Parent = GameService.Graphics.SpriteScreen,
                Title = "Confirm",
                Id = WindowId,
                TopMost = true,
                SavesPosition = true
            };

            // Apply saved position from settings as fallback
            if (_settings.ModalDialogX.Value >= 0 && _settings.ModalDialogY.Value >= 0)
            {
                _window.Location = new Point(
                    _settings.ModalDialogX.Value,
                    _settings.ModalDialogY.Value);
            }

            _window.Moved += OnWindowMoved;
        }

        public void Show(string message, Action onConfirm, Action onCancel)
        {
            if (_isShowing) return;
            _isShowing = true;
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            // Clear old children
            foreach (var child in _window.Children.ToArray())
            {
                child.Dispose();
            }

            // Message label
            new Label()
            {
                Text = message,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, 4),
                Parent = _window
            };

            // Confirm button
            var confirmBtn = new StandardButton()
            {
                Text = "Regenerate",
                Size = new Point(100, 25),
                Location = new Point(80, 30),
                Parent = _window
            };
            confirmBtn.Click += (_, __) =>
            {
                _isShowing = false;
                _window.Hide();
                _onConfirm?.Invoke();
            };

            // Cancel button
            var cancelBtn = new StandardButton()
            {
                Text = "Cancel",
                Size = new Point(70, 25),
                Location = new Point(190, 30),
                Parent = _window
            };
            cancelBtn.Click += (_, __) =>
            {
                _isShowing = false;
                _window.Hide();
                _onCancel?.Invoke();
            };

            _window.Show();
        }

        public void Hide()
        {
            _isShowing = false;
            _window.Hide();
        }

        public void Dispose()
        {
            _window.Moved -= OnWindowMoved;
            _window.Hide();
            _window.Dispose();
        }

        private void OnWindowMoved(object sender, MovedEventArgs e)
        {
            _settings.ModalDialogX.Value = _window.Location.X;
            _settings.ModalDialogY.Value = _window.Location.Y;
        }
    }
}
