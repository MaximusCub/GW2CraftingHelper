using System;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;

namespace TaimisToolbench.Views
{
    /// <summary>
    /// TabbedWindow2 subclass that enforces a minimum window size,
    /// matching the behavior of ResizableModuleWindow for StandardWindow.
    /// Also clamps at construction and on every layout pass, so neither the
    /// texture-derived constructed size nor a size persisted by an earlier
    /// session can open the window below the minimum.
    /// <para>
    /// A size too LARGE for the client, and the window's POSITION, are
    /// corrected too, but on a different schedule and for a different
    /// reason - see <see cref="FitSizeToScreen"/>,
    /// <see cref="ClampToScreen"/> and <see cref="Services.WindowPlacement"/>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Sealed: the constructor clamps, which writes <c>Size</c> and so runs
    /// the virtual OnResized/RecalculateLayout chain. Sealing keeps that off
    /// any subclass override, which would otherwise run against a
    /// half-constructed instance.
    /// </remarks>
    internal sealed class ResizableTabbedWindow : TabbedWindow2
    {
        private readonly Point _designMinSize;

        // The screen-fitted floor is re-derived on EVERY clamp rather than
        // captured at construction: at module-build time the sprite screen
        // has not necessarily settled to the real client size (measured
        // live 2026-08-23 - a one-shot capture froze the floor near the
        // unsettled backbuffer width and a persisted 990px window was
        // never clamped up to 1436), so each clamp asks
        // WindowSizing.EffectiveMinWindowWidth for the floor the CURRENT
        // screen supports and the first layout pass after the screen
        // settles grows the window the rest of the way.
        private Point EffectiveMinSize()
        {
            int screenWidth = Blish_HUD.GameService.Graphics.SpriteScreen?.Width ?? 0;
            return new Point(
                Services.WindowSizing.EffectiveMinWindowWidth(screenWidth),
                _designMinSize.Y);
        }

        public ResizableTabbedWindow(
            AsyncTexture2D background,
            Rectangle windowRegion,
            Rectangle contentRegion,
            Point minSize)
            : base(background, windowRegion, contentRegion)
        {
            _designMinSize = minSize;
            CanResize = true;
            SavesSize = true;

            // Blish adopts the game client's size AFTER modules load (the
            // sprite screen resizes once the overlay attaches), and no
            // window layout pass runs on its own after that - measured
            // live 2026-08-23: without this, a launch on a wide client
            // kept the smaller floor computed against the unsettled
            // screen. Re-clamp whenever the screen itself changes size;
            // unhooked in DisposeControl.
            Blish_HUD.GameService.Graphics.SpriteScreen.Resized += OnScreenResized;

            // Belt to Hide()'s braces: Hide() is the intent, Hidden is the
            // fact, and a direct Visible = false only raises the latter.
            // Both unhooked in DisposeControl.
            this.Hidden += OnWindowHidden;

            // The base constructor sizes the window from windowRegion, a
            // region of the background texture, which is narrower than the
            // minimum. Clamping here means the window is never below the
            // floor at any observable point - including its first draw -
            // rather than depending on an invalidation ordering this repo
            // has not measured. (Tabs are registered as lazy factories, so
            // no hosted view exists in the gap either way.)
            ClampToMinimum();
        }

        protected override Point HandleWindowResize(Point newSize)
        {
            var min = EffectiveMinSize();
            return new Point(
                Math.Max(newSize.X, min.X),
                Math.Max(newSize.Y, min.Y));
        }

        public override void RecalculateLayout()
        {
            base.RecalculateLayout();

            // Persisted sizes from earlier sessions may be below the current
            // minimum - a saved 930px window has to come back at the raised
            // one. Blish restores the size after construction, so this pass
            // is what catches it; the clamp only ever grows a window, so a
            // saved size above the minimum is left exactly as it was.
            ClampToMinimum();
        }

        private void OnScreenResized(object sender, ResizedEventArgs e)
        {
            // Size first: the position clamp is taken against the size the
            // window ends up at, and both the floor and the ceiling this
            // screen supports may have just moved.
            FitSizeToScreen();
            ClampToScreen();
        }

        /// <summary>
        /// Runs AFTER the base implementation, which is where a persisted
        /// position is restored and where Blish applies its own top-left-
        /// corner-only clamp - see <see cref="Services.WindowPlacement"/> for
        /// why that one is not enough on a client narrower than the one the
        /// position was saved against.
        /// <para>
        /// The persisted SIZE is restored on the same path, one statement
        /// earlier, and Control.Size reaches RecalculateLayout synchronously
        /// (dev/records/firstpaint-truncation.md) - so the size floor has
        /// already been applied by the time <see cref="FitSizeToScreen"/>
        /// takes the ceiling and the position is clamped against the result.
        /// </para>
        /// </summary>
        public override void Show()
        {
            base.Show();
            FitSizeToScreen();
            ClampToScreen();
        }

        /// <summary>
        /// A focused text box whose window goes away without a click keeps
        /// Blish's keyboard focus, and with it the text-input listener that
        /// swallows every keystroke bound for the game - see
        /// <see cref="FocusRelease"/>. Released on the intent (here), not on
        /// the fade that follows it, because the box eats keys for the whole
        /// of that fade.
        /// </summary>
        public override void Hide()
        {
            FocusRelease.ReleaseWithin(this);
            base.Hide();
        }

        private void OnWindowHidden(object sender, EventArgs e)
        {
            FocusRelease.ReleaseWithin(this);
        }

        /// <summary>
        /// Runs BEFORE the base implementation, which swaps the hosted view
        /// and disposes the outgoing tab's controls with it.
        /// </summary>
        protected override void OnTabChanged(ValueChangedEventArgs<Tab> e)
        {
            FocusRelease.ReleaseWithin(this);
            base.OnTabChanged(e);
        }

        protected override void DisposeControl()
        {
            Blish_HUD.GameService.Graphics.SpriteScreen.Resized -= OnScreenResized;
            this.Hidden -= OnWindowHidden;
            FocusRelease.ReleaseWithin(this);
            base.DisposeControl();
        }

        /// <summary>
        /// Moves the window back inside the current sprite screen, on the
        /// rule <see cref="Services.WindowPlacement"/> states.
        /// <para>
        /// Called from <see cref="Show"/> and from the screen's own resize,
        /// and deliberately from nowhere else - not from the layout path
        /// <see cref="ClampToMinimum"/> uses. WindowBase2 writes Location on
        /// every frame of a drag, so a clamp anywhere on that path would
        /// fight the user for a window they had deliberately parked half
        /// off-screen; both callers here are discrete events at which the
        /// position's frame of reference has just changed.
        /// </para>
        /// </summary>
        private void ClampToScreen()
        {
            var screen = Blish_HUD.GameService.Graphics.SpriteScreen;
            if (screen == null)
            {
                return;
            }

            var clamped = new Point(
                Services.WindowPlacement.ClampAxis(this.Location.X, this.Width, screen.Width),
                Services.WindowPlacement.ClampAxis(this.Location.Y, this.Height, screen.Height));

            if (clamped != this.Location)
            {
                this.Location = clamped;
            }
        }

        /// <summary>
        /// Shrinks the window onto the current sprite screen, on the rule
        /// <see cref="Services.WindowPlacement.ClampExtent"/> states, and
        /// applies the floor in the same pass.
        /// <para>
        /// Called from the same two events as <see cref="ClampToScreen"/>
        /// and for the same reason, and like it NOT from the layout path:
        /// WindowBase2 writes Size on every frame of a resize drag, and a
        /// ceiling there would cap a user dragging the grip on a window
        /// whose left edge is off-screen.
        /// </para>
        /// <para>
        /// The shrink is not persisted as the user's preference: Blish
        /// writes the size setting only from OnGlobalMouseRelease, and only
        /// while Resizing - a flag set by pressing the grip (BlishHUD 1.3.0,
        /// decompiled). A programmatic write is invisible to it, so the size
        /// chosen on a wide client is restored in full the next time the
        /// window is opened on one.
        /// </para>
        /// </summary>
        private void FitSizeToScreen()
        {
            var screen = Blish_HUD.GameService.Graphics.SpriteScreen;
            var min = EffectiveMinSize();

            var fitted = new Point(
                Services.WindowPlacement.ClampExtent(this.Width, min.X, screen?.Width ?? 0),
                Services.WindowPlacement.ClampExtent(this.Height, min.Y, screen?.Height ?? 0));

            if (fitted != this.Size)
            {
                this.Size = fitted;
            }
        }

        private void ClampToMinimum()
        {
            var min = EffectiveMinSize();
            if (this.Width >= min.X && this.Height >= min.Y)
            {
                return;
            }

            this.Size = new Point(
                Math.Max(this.Width, min.X),
                Math.Max(this.Height, min.Y));
        }
    }
}
