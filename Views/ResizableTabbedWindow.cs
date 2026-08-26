using System;
using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// TabbedWindow2 subclass that enforces a minimum window size,
    /// matching the behavior of ResizableModuleWindow for StandardWindow.
    /// Also clamps at construction and on every layout pass, so neither the
    /// texture-derived constructed size nor a size persisted by an earlier
    /// session can open the window below the minimum.
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
            ClampToMinimum();
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
