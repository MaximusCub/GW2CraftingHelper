using System;
using System.Linq;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using TaimisToolbench.Services;
using TaimisToolbench.Views.Rendering;

namespace TaimisToolbench.Views
{
    /// <summary>
    /// The "GW2 API access not ready" walkthrough for the ApiAccessNotReady
    /// snapshot-refresh failure kind: at character select Blish has not yet
    /// resolved the game's Mumble identity, so every account data source
    /// call fails with an invalid/missing API key. Lists the three things to
    /// check, then offers Retry/Close.
    /// <para>
    /// Text is pre-wrapped by Services/DialogLayoutMath rather than by the
    /// Label control's WrapText property, whose wrap width is pinned at the
    /// control's first internal layout pass - a pass that fires before a
    /// later Width assignment in the same object initializer takes effect
    /// (confirmed by decompiling the shipped assembly). The same service
    /// sizes the window around the three checks below.
    /// </para>
    /// Centers on every Show(); it persists no drag position, so it needs no
    /// ModuleSettings entries. Why this is a separate class rather than a
    /// generalized ModalDialog: docs/ARCHITECTURE.md, "Views: relocated
    /// design narrative".
    /// </summary>
    internal class ApiAccessDialog : IDisposable
    {
        private const string WindowId = "TaimisToolbench_ApiAccessDialog_7d2c31";

        private const string RetryText = "Retry";

        private const string CloseText = "Close";

        private static readonly string[] Checks =
        {
            "1. You are logged into a character in the game world (not the character-select screen) - Blish only learns which account is active once you are in-world.",
            "2. A Guild Wars 2 API key is added in Blish HUD settings.",
            "3. This module has permission to use the API key (Blish settings > Manage Modules > Taimi's Toolbench).",
        };

        // Width, height and every inner offset: Services/DialogLayoutMath.
        // This dialog is why its title floor exists - at a 480px window the
        // title ran into the title bar's close X and was clipped mid-word
        // ("GW2 API access is not read|y|X"), and the fix was a wider
        // window, because Blish draws the title at a fixed indent in
        // DefaultFont32 with no alignment control.
        private readonly DialogWindow _window;
        private bool _isShowing;
        private bool _disposed;
        private Action _onRetry;

        public ApiAccessDialog()
        {
            _window = new DialogWindow(
                new AsyncTexture2D(ContentService.Textures.Pixel),
                DialogLayoutMath.MinContentWidth,
                DialogLayoutMath.MinContentHeight(0))
            {
                BackgroundColor = new Color(30, 30, 30),
                Parent = GameService.Graphics.SpriteScreen,
                Title = "GW2 API access not ready",
                Id = WindowId,
                TopMost = true,
            };

            // Resets _isShowing whenever the window's own Visible=false
            // transition completes - not just when our own Retry/Close
            // StandardButton handlers below run. WindowBase2's built-in
            // title-bar X button and Escape key both call Hide() directly
            // (CanClose/CanCloseWithEscape default true, never overridden
            // here), bypassing those handlers entirely - without this,
            // dismissing the dialog that way would leave _isShowing stuck
            // true and every later Show() call would silently no-op for
            // the rest of the session.
            _window.Hidden += OnWindowHidden;
        }

        public void Show(Action onRetry)
        {
            // Self-defending: a caller's guard over an unrelated object's
            // field (e.g. MainView's _headerPanel liveness check) is not a
            // substitute for this dialog checking its own disposal state -
            // see the class doc comment.
            if (_disposed)
            {
                return;
            }

            if (_isShowing)
            {
                return;
            }

            _isShowing = true;
            _onRetry = onRetry;

            foreach (var child in _window.Children.ToArray())
            {
                child.Dispose();
            }

            var font = UiFonts.Body;
            var measure = LabelHelpers.MeasureWith(font);
            var buttonMeasure = LabelHelpers.MeasureWith(UiFonts.Caption);
            int lineHeight = font.LineHeight > 0 ? font.LineHeight : 1;

            var screen = GameService.Graphics.SpriteScreen;
            var layout = DialogLayoutMath.Measure(
                Checks,
                measure,
                lineHeight,
                LabelHelpers.MeasureWith(UiFonts.Display)(_window.Title),
                buttonMeasure(RetryText),
                buttonMeasure(CloseText),
                DialogLayoutMath.MaxContentWidth(screen.Width, DialogWindow.ChromeWidth),
                DialogLayoutMath.MaxContentHeight(screen.Height, DialogWindow.ChromeHeight, lineHeight));

            // Before the children: they are placed against the region this
            // call establishes.
            _window.Resize(layout.ContentWidth, layout.ContentHeight);

            for (int i = 0; i < layout.Blocks.Count; i++)
            {
                AddWrappedLine(font, layout.Blocks[i], Checks[i]);
            }

            var retryBtn = new FeedbackButton()
            {
                Text = RetryText,
                Size = new Point(layout.ConfirmWidth, DialogLayoutMath.ButtonHeight),
                Location = new Point(layout.ConfirmX, layout.ButtonY),
                Parent = _window,
            };
            retryBtn.Click += (_, __) =>
            {
                _isShowing = false;
                _window.Hide();
                _onRetry?.Invoke();
            };

            var closeBtn = new FeedbackButton()
            {
                Text = CloseText,
                Size = new Point(layout.CancelWidth, DialogLayoutMath.ButtonHeight),
                Location = new Point(layout.CancelX, layout.ButtonY),
                Parent = _window,
            };
            closeBtn.Click += (_, __) =>
            {
                _isShowing = false;
                _window.Hide();
            };

            _window.Location = new Point(
                (screen.Width - _window.Width) / 2,
                (screen.Height - _window.Height) / 2);

            _window.Show();
        }

        public void Hide()
        {
            if (_disposed)
            {
                return;
            }

            _isShowing = false;
            _window.Hide();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _window.Hidden -= OnWindowHidden;
            _window.Hide();
            _window.Dispose();
        }

        /// <summary>
        /// Fires whenever the window's own Visible=false transition
        /// completes, regardless of which path triggered it - our own
        /// Retry/Close StandardButton handlers (which already set
        /// _isShowing = false synchronously, so this is a harmless
        /// no-op re-assignment for those), or WindowBase2's built-in
        /// title-bar X button/Escape key, which call Hide() directly and
        /// never touch _isShowing otherwise - see the constructor's own
        /// comment on why this subscription exists.
        /// </summary>
        private void OnWindowHidden(object sender, EventArgs e)
        {
            _isShowing = false;
        }

        /// <summary>
        /// Adds one pre-wrapped, left-aligned Label at the Y the layout put
        /// it. Constructed without a Parent so Height reflects only the
        /// wrapped text itself, then parented - mirrors
        /// AboutTabContent.AddInfoLine's own ordering.
        /// </summary>
        private void AddWrappedLine(BitmapFont font, DialogLayoutMath.MessageBlock block, string fullText)
        {
            var label = new Label()
            {
                Text = string.Join("\n", block.Lines),
                Font = font,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, block.Y),
            };

            label.Parent = _window;

            // Only a screen too short for all three checks drops text, and
            // only then is the tooltip anything but a repeat of the line.
            TooltipFacility.ApplyPlain(label, block.Truncated ? fullText : null);
        }
    }
}
