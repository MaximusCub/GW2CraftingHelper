using System;
using System.Linq;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using GW2CraftingHelper.Views.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// The "GW2 API access not ready" walkthrough dialog for the
    /// ApiAccessNotReady snapshot-refresh failure kind (at CHARACTER
    /// SELECT, Blish has not yet resolved the
    /// game's Mumble identity, so every account data source call fails
    /// with an invalid/missing API key, and the Snapshot tab's Refresh Now
    /// used to show only the unhelpful "Refresh Failed - {time}"). Lists
    /// the three things a user needs to check, then offers Retry/Close.
    /// <para>
    /// Follows the same StandardWindow construction technique as the
    /// existing ModalDialog (a 1x1 pixel background stretched to the
    /// window's own size, TopMost, a stable Id, Show()/Hide() semantics) -
    /// but is a SEPARATE class rather than a generalization of ModalDialog
    /// itself: ModalDialog's shape (one short sentence, fixed "Confirm"
    /// title, a caller-named confirm button beside a fixed Cancel) does
    /// not fit a multi-line
    /// numbered checklist with a different title and a Retry/Close pair,
    /// and its message Label is not wrapped at all - fine for its own
    /// short sentence, but this dialog's checklist items are full
    /// sentences that need to wrap. Text is pre-wrapped with Blish HUD's
    /// own DrawUtil.WrapText (the AboutTabContent.AddInfoLine pattern)
    /// rather than the Label control's own WrapText property,
    /// whose wrap width is pinned at the control's first internal layout
    /// pass - a pass that fires before a later Width assignment in the
    /// same object initializer would ever take effect (confirmed by
    /// decompiling the shipped Blish HUD assembly).
    /// </para>
    /// <para>
    /// Deliberately skips ModalDialog's settings-backed drag position
    /// persistence: this is a rare error-path dialog, not a workflow the
    /// user repeatedly opens and repositions, so it simply centers on
    /// every Show() call - no new ModuleSettings entries needed for it.
    /// </para>
    /// </summary>
    public class ApiAccessDialog : IDisposable
    {
        private const string WindowId = "GW2CraftingHelper_ApiAccessDialog_7d2c31";

        // 480 before: at that width the title ran into the title bar's
        // close X and was clipped mid-word ("GW2 API access is not read|y|X").
        // Measured against BlishHUD 1.3.0's WindowBase2: the title is drawn
        // in DefaultFont32 - the largest font in the toolkit, and NOT the
        // font a title this long was sized against - at a fixed 80px offset
        // into the left title-bar texture, clipped to that texture's own
        // bounds, which end 2px short of the right title-bar section; the
        // exit button then sits 32px plus its own width inside that section's
        // right edge. So the title's budget is (window width - 80 - the
        // right section's reserved run), and widening the window buys title
        // room 1:1. This carries roughly 80px more than the clip needed, on
        // top of the three characters the title itself dropped, because
        // Font32's per-character cost is ~15px and neither figure is
        // available to a unit test.
        private const int WindowWidth = 560;
        private const int WindowHeight = 300;
        private const int ContentX = 10;
        private const int ContentY = 35;
        private const int ContentWidth = WindowWidth - (2 * ContentX);
        private const int LineSpacing = 8;
        private const int ButtonTopMargin = 20;

        private static readonly string[] Checks =
        {
            "1. You are logged into a character in the game world (not the character-select screen) - Blish only learns which account is active once you are in-world.",
            "2. A Guild Wars 2 API key is added in Blish HUD settings.",
            "3. This module has permission to use the API key (Blish settings > Manage Modules > GW2 Crafting Helper)."
        };

        private readonly StandardWindow _window;
        private bool _isShowing;
        private bool _disposed;
        private Action _onRetry;

        public ApiAccessDialog()
        {
            _window = new StandardWindow(
                new AsyncTexture2D(ContentService.Textures.Pixel),
                new Rectangle(0, 0, WindowWidth, WindowHeight),
                new Rectangle(ContentX, ContentY, ContentWidth, WindowHeight - ContentY - 10))
            {
                BackgroundColor = new Color(30, 30, 30),
                Parent = GameService.Graphics.SpriteScreen,
                Title = "GW2 API access not ready",
                Id = WindowId,
                TopMost = true
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
            if (_disposed) return;
            if (_isShowing) return;
            _isShowing = true;
            _onRetry = onRetry;

            foreach (var child in _window.Children.ToArray())
            {
                child.Dispose();
            }

            var font = UiFonts.Body;
            int y = 4;

            foreach (var check in Checks)
            {
                y = AddWrappedLine(font, check, y);
            }

            int btnW = 100;
            int closeW = 70;
            int btnGap = 16;
            int totalBtnW = btnW + btnGap + closeW;
            int btnX = (ContentWidth - totalBtnW) / 2;
            int btnY = y + ButtonTopMargin;

            var retryBtn = new FeedbackButton()
            {
                Text = "Retry",
                Size = new Point(btnW, 25),
                Location = new Point(btnX, btnY),
                Parent = _window
            };
            retryBtn.Click += (_, __) =>
            {
                _isShowing = false;
                _window.Hide();
                _onRetry?.Invoke();
            };

            var closeBtn = new FeedbackButton()
            {
                Text = "Close",
                Size = new Point(closeW, 25),
                Location = new Point(btnX + btnW + btnGap, btnY),
                Parent = _window
            };
            closeBtn.Click += (_, __) =>
            {
                _isShowing = false;
                _window.Hide();
            };

            var screen = GameService.Graphics.SpriteScreen;
            _window.Location = new Point(
                (screen.Width - _window.Width) / 2,
                (screen.Height - _window.Height) / 2);

            _window.Show();
        }

        public void Hide()
        {
            if (_disposed) return;
            _isShowing = false;
            _window.Hide();
        }

        public void Dispose()
        {
            if (_disposed) return;
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
        /// Adds one pre-wrapped, left-aligned Label at the given Y and
        /// returns the Y for the next line - see the class doc comment for
        /// why DrawUtil.WrapText is used instead of the Label control's own
        /// WrapText property. Constructed without a Parent so Height
        /// reflects only the wrapped text itself, then parented once the
        /// next line's Y is already computed - mirrors
        /// AboutTabContent.AddInfoLine's own ordering.
        /// </summary>
        private int AddWrappedLine(BitmapFont font, string text, int y)
        {
            string wrapped = DrawUtil.WrapText(font, text, ContentWidth);

            var label = new Label()
            {
                Text = wrapped,
                Font = font,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(0, y)
            };

            int nextY = y + label.Height + LineSpacing;
            label.Parent = _window;
            return nextY;
        }
    }
}
