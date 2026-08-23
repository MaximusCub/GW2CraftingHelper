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

            // Resets _isShowing - and runs the caller's cancel callback -
            // whenever the window's own Visible=false transition completes,
            // not just when the Confirm/Cancel StandardButton handlers below
            // run. WindowBase2's built-in title-bar X button and Escape key
            // both call Hide() directly (CanClose/CanCloseWithEscape default
            // true, never overridden here), bypassing those handlers
            // entirely - without this, dismissing the dialog that way would
            // leave _isShowing stuck true and every later Show() call, from
            // every caller of this shared instance, would silently no-op for
            // the rest of the session. Same subscription, same reason, as
            // ApiAccessDialog's.
            _window.Hidden += OnWindowHidden;
        }

        // confirmText is required so every caller states its own verb
        // ("Regenerate", "Delete") - a default here would hand an
        // unrelated caller the wrong label on a destructive confirm.
        // Returns false when another caller's dialog is already on screen,
        // so a caller that arms state for the dialog's lifetime (MainView
        // disables its Snapshot buttons) knows not to arm it.
        public bool Show(string message, Action onConfirm, Action onCancel, string confirmText)
        {
            if (_isShowing) return false;
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
            confirmBtn.Click += (_, __) => Dismiss(confirmed: true);

            var cancelBtn = new StandardButton()
            {
                Text = "Cancel",
                Size = new Point(cancelW, 25),
                Location = new Point(btnX + btnW + btnGap, btnY),
                Parent = _window
            };
            cancelBtn.Click += (_, __) => Dismiss(confirmed: false);

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
            return true;
        }

        /// <summary>
        /// Programmatic close: drops the pending callbacks without running
        /// either of them. Clearing _isShowing first is what stops
        /// OnWindowHidden below from reading this as a user cancel.
        /// </summary>
        public void Hide()
        {
            _isShowing = false;
            _onConfirm = null;
            _onCancel = null;
            _window.Hide();
        }

        public void Dispose()
        {
            // Unsubscribe BEFORE hiding: teardown must not fire a caller's
            // cancel callback into controls the module is already disposing.
            _window.Hidden -= OnWindowHidden;
            _window.Moved -= OnWindowMoved;
            _window.Hide();
            _window.Dispose();
        }

        /// <summary>
        /// The single exit path for both buttons and for the window's own
        /// X/Escape dismissal. Callbacks are read into locals and cleared
        /// before either runs, so a callback that reopens the dialog gets a
        /// clean slate and cannot see the previous request's handlers.
        /// </summary>
        private void Dismiss(bool confirmed)
        {
            if (!_isShowing)
            {
                return;
            }

            _isShowing = false;
            var onConfirm = _onConfirm;
            var onCancel = _onCancel;
            _onConfirm = null;
            _onCancel = null;

            _window.Hide();

            if (confirmed)
            {
                onConfirm?.Invoke();
            }
            else
            {
                onCancel?.Invoke();
            }
        }

        // Fires on every Visible=false transition, whichever path caused it.
        // The button handlers and Hide() clear _isShowing before hiding, so
        // for those this is a no-op and only the title-bar X and Escape key
        // reach the Dismiss below - which is what makes them behave like
        // Cancel instead of stranding both _isShowing and whatever state the
        // caller armed for the dialog's lifetime.
        private void OnWindowHidden(object sender, EventArgs e)
        {
            Dismiss(confirmed: false);
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
