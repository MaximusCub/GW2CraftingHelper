using System;
using System.Linq;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Views.Rendering;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    public class ModalDialog : IDisposable
    {
        private const string WindowId = "GW2CraftingHelper_ModalDialog_c4f19a";

        // 400x150 before, with the message in an unwrapped 380px-wide
        // centered Label: a sentence wider than the label (Clear Cache's is
        // ~640px at DefaultFont14) was centered on the label's midpoint and
        // clipped at BOTH ends by the label's own scissor, so the dialog
        // showed the middle of the sentence and nothing else. The width also
        // squeezed WindowBase2's left title-bar texture into ~200px, which
        // rasterized as coloured streaks behind the title.
        //
        // 560 is ApiAccessDialog's width, whose title bar renders clean and
        // whose wrapped body is the shape copied below - the two dialogs are
        // now the same size for the same reasons. Blish draws the title
        // itself at a fixed 80px indent in DefaultFont32 with no alignment
        // control (see ApiAccessDialog's own measurement note), so window
        // width is the only lever either dialog has over the title bar.
        private const int WindowWidth = 560;
        private const int WindowHeight = 170;
        private const int ContentX = 10;
        private const int ContentY = 35;
        private const int ContentWidth = WindowWidth - (2 * ContentX);
        private const int ContentHeight = WindowHeight - ContentY - 10;
        private const int MessageTopMargin = 6;
        private const int MessageToButtonGap = 16;
        private const int ButtonHeight = 25;
        private const int ButtonBottomMargin = 10;

        // The button line is FIXED, not measured against the message, and
        // the message is capped to the lines that fit above it instead.
        // The window cannot grow to fit a longer sentence: WindowBase2
        // derives ContentRegion from the region passed to its protected
        // ConstructWindow, and Container.ContentRegion has no public
        // setter, so a Height written from here would leave the content
        // region where it was. Pushing the buttons down instead (what the
        // previous Math.Max did) walks them out of that region - at five
        // wrapped lines they land almost entirely outside it and stop
        // taking clicks, leaving the title-bar X as the only exit. A capped
        // message keeps Confirm/Cancel reachable for any input; the full
        // text stays available on the label's tooltip.
        private const int ButtonY = ContentHeight - ButtonHeight - ButtonBottomMargin;
        private const int MessageAreaHeight = ButtonY - MessageToButtonGap - MessageTopMargin;

        private readonly StandardWindow _window;
        private readonly ModuleSettings _settings;

        // The surface a confirm has to freeze while it is up, resolved
        // lazily: Module builds this dialog before it builds the module
        // window. Null (no backdrop) is a supported shape - the dialog
        // still works, it just is not modal, which is what every caller
        // got before this existed.
        private readonly Func<Control> _blockedSurface;

        // Built on the FIRST Show(), not in the constructor, and that is
        // load-bearing - see ModalBackdrop's z-order note: it has to be a
        // later SpriteScreen child than the window it blocks.
        private ModalBackdrop _backdrop;

        private bool _isShowing;
        private bool _suppressMoved;
        private Action _onConfirm;
        private Action _onCancel;

        public ModalDialog(ModuleSettings settings, Func<Control> blockedSurface = null)
        {
            _settings = settings;
            _blockedSurface = blockedSurface;

            // Use a 1x1 pixel texture to avoid overflow from large asset textures.
            // StandardWindow chrome (title bar, borders, close button) uses its own
            // built-in textures and does not depend on the background parameter.
            _window = new StandardWindow(
                new AsyncTexture2D(ContentService.Textures.Pixel),
                new Rectangle(0, 0, WindowWidth, WindowHeight),
                new Rectangle(ContentX, ContentY, ContentWidth, ContentHeight))
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
        // cancelText is optional and defaults to the plain "Cancel" every
        // existing caller wants: for those, the second button really does
        // abandon the operation. It exists for the callers whose second
        // button is a CHOICE rather than an escape - the Settings tab's
        // unsaved-changes prompt cannot put the user back where they were
        // (see KNOWN-ISSUES "Settings dirty prompt"), so a button labelled
        // "Cancel" there would promise something it does not do.
        // Returns false when another caller's dialog is already on screen,
        // so a caller that arms state for the dialog's lifetime (MainView
        // disables its Snapshot buttons) knows not to arm it.
        public bool Show(string message, Action onConfirm, Action onCancel, string confirmText, string cancelText = "Cancel")
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

            // Pre-wrapped, not left to the Label control's WrapText
            // property, for the reason ApiAccessDialog documents: that
            // property pins its wrap width at the control's first internal
            // layout pass, which runs before a Width assigned later in the
            // same object initializer takes effect. TextWrapMath rather
            // than DrawUtil.WrapText because only the former caps the line
            // count, which is what keeps the buttons in the content region
            // (see ButtonY) - the same greedy wrap plus ellipsized tail the
            // notes section already renders with.
            var font = GameService.Content.DefaultFont14;
            var measure = LabelHelpers.MeasureWith(font);
            int lineHeight = font.LineHeight > 0 ? font.LineHeight : 1;
            var wrapped = TextWrapMath.Wrap(
                message ?? "",
                ContentWidth,
                ContentWidth,
                measure,
                MessageAreaHeight / lineHeight);

            // Auto-size BOTH axes and parent last - ApiAccessDialog's
            // proven AddWrappedLine shape. A fixed Width with
            // AutoSizeHeight takes Blish's stale-layout-pass measure and
            // clipped the second wrapped line mid-glyph (gate capture
            // gA6w). The block centers by its measured width instead.
            var messageLabel = new Label()
            {
                Text = string.Join("\n", wrapped.Lines),
                Font = font,
                AutoSizeWidth = true,
                AutoSizeHeight = true
            };
            messageLabel.Location = new Point(
                System.Math.Max(0, (ContentWidth - messageLabel.Width) / 2),
                MessageTopMargin);
            messageLabel.Parent = _window;

            // Only when text was actually dropped - a tooltip repeating the
            // visible sentence is noise.
            TooltipFacility.ApplyPlain(messageLabel, wrapped.Truncated ? message : null);

            // Buttons: centered horizontally, on the fixed bottom line so
            // every caller's dialog puts them in the same place.
            int btnW = 100;
            // 70 is the width every caller had before cancelText existed
            // and is the floor, so "Cancel" is pixel-identical to what it
            // was; a longer label grows the button instead of being
            // clipped by StandardButton's own scissor.
            string cancelLabel = string.IsNullOrEmpty(cancelText) ? "Cancel" : cancelText;
            int cancelW = System.Math.Max(70, measure(cancelLabel) + 24);
            int btnGap = 16;
            int totalBtnW = btnW + btnGap + cancelW;
            int btnX = (ContentWidth - totalBtnW) / 2;
            int btnY = ButtonY;

            var confirmBtn = new StandardButton()
            {
                Text = confirmText,
                Size = new Point(btnW, ButtonHeight),
                Location = new Point(btnX, btnY),
                Parent = _window
            };
            confirmBtn.Click += (_, __) => Dismiss(confirmed: true);

            var cancelBtn = new StandardButton()
            {
                Text = cancelLabel,
                Size = new Point(cancelW, ButtonHeight),
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

            ShowBackdrop();
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
            _backdrop?.Hide();
            _window.Hide();
        }

        /// <summary>
        /// Raises the input-eating layer beneath the dialog. Deferred to
        /// the first Show() because the surface it blocks does not exist
        /// when this dialog is constructed, AND because it must be a later
        /// child of SpriteScreen than that surface to win the sibling-index
        /// tiebreak (see ModalBackdrop).
        /// </summary>
        private void ShowBackdrop()
        {
            if (_blockedSurface == null) return;

            if (_backdrop == null)
            {
                _backdrop = new ModalBackdrop(_window, _blockedSurface);
            }

            // Bounds and z-order before Visible: a frame that shows the
            // backdrop at a stale rect is a frame where the click it exists
            // to eat gets through.
            _backdrop.Sync();
            _backdrop.Show();
        }

        public void Dispose()
        {
            // Unsubscribe BEFORE hiding: teardown must not fire a caller's
            // cancel callback into controls the module is already disposing.
            _window.Hidden -= OnWindowHidden;
            _window.Moved -= OnWindowMoved;
            _backdrop?.Dispose();
            _backdrop = null;
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

            // Dropped before the window, and before either callback runs -
            // a confirm callback that opens another dialog re-raises it,
            // and one that touches the module window must not be doing so
            // through a live input blocker.
            _backdrop?.Hide();
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
