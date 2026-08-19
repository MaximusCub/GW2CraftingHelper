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
        private bool _suppressMoved;
        private Action _onConfirm;
        private Action _onCancel;

        public ModalDialog(ModuleSettings settings)
        {
            _settings = settings;

            // Use a 1x1 pixel texture to avoid overflow from large asset textures.
            // StandardWindow chrome (title bar, borders, close button) uses its own
            // built-in textures and does not depend on the background parameter.
            _window = new StandardWindow(
                new AsyncTexture2D(ContentService.Textures.Pixel),
                new Rectangle(0, 0, 400, 150),
                new Rectangle(10, 35, 380, 105))
            {
                BackgroundColor = new Color(30, 30, 30),
                Parent = GameService.Graphics.SpriteScreen,
                Title = "Confirm",
                Id = WindowId,
                TopMost = true
            };

            _window.Moved += OnWindowMoved;
        }

        // confirmText defaults to the pre-existing hardcoded label so the
        // regenerate-confirm call sites stay unchanged; destructive
        // callers (the Log tab's Delete Log File) pass their own verb.
        public void Show(string message, Action onConfirm, Action onCancel, string confirmText = "Regenerate")
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

            // Message label (centered horizontally)
            new Label()
            {
                Text = message,
                AutoSizeWidth = false,
                AutoSizeHeight = true,
                Width = 380,
                HorizontalAlignment = HorizontalAlignment.Center,
                Location = new Point(0, 4),
                Parent = _window
            };

            // Buttons: centered horizontally, placed in lower half of content
            int btnW = 100;
            int cancelW = 70;
            int btnGap = 16;
            int totalBtnW = btnW + btnGap + cancelW;
            int btnX = (380 - totalBtnW) / 2;
            int btnY = 55;

            var confirmBtn = new StandardButton()
            {
                Text = confirmText,
                Size = new Point(btnW, 25),
                Location = new Point(btnX, btnY),
                Parent = _window
            };
            confirmBtn.Click += (_, __) =>
            {
                _isShowing = false;
                _window.Hide();
                _onConfirm?.Invoke();
            };

            var cancelBtn = new StandardButton()
            {
                Text = "Cancel",
                Size = new Point(cancelW, 25),
                Location = new Point(btnX + btnW + btnGap, btnY),
                Parent = _window
            };
            cancelBtn.Click += (_, __) =>
            {
                _isShowing = false;
                _window.Hide();
                _onCancel?.Invoke();
            };

            // Position: restore saved location, or center on first show
            var screen = GameService.Graphics.SpriteScreen;
            int sx = _settings.ModalDialogX.Value;
            int sy = _settings.ModalDialogY.Value;

            if (sx < 0 || sy < 0
                || sx + _window.Width > screen.Width
                || sy + _window.Height > screen.Height)
            {
                sx = (screen.Width - _window.Width) / 2;
                sy = (screen.Height - _window.Height) / 2;
                _settings.ModalDialogX.Value = sx;
                _settings.ModalDialogY.Value = sy;
            }

            _window.Location = new Point(sx, sy);

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
            if (_suppressMoved) return;

            var screen = GameService.Graphics.SpriteScreen;
            int maxX = Math.Max(0, screen.Width - _window.Width);
            int maxY = Math.Max(0, screen.Height - _window.Height);

            int clampedX = Math.Min(Math.Max(0, e.CurrentLocation.X), maxX);
            int clampedY = Math.Min(Math.Max(0, e.CurrentLocation.Y), maxY);

            if (clampedX != e.CurrentLocation.X || clampedY != e.CurrentLocation.Y)
            {
                _suppressMoved = true;
                _window.Location = new Point(clampedX, clampedY);
                _suppressMoved = false;
            }

            _settings.ModalDialogX.Value = _window.Location.X;
            _settings.ModalDialogY.Value = _window.Location.Y;
        }
    }
}
