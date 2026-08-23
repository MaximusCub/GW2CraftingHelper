using System;
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
    public sealed class ResizableTabbedWindow : TabbedWindow2
    {
        private readonly Point _minSize;

        public ResizableTabbedWindow(
            AsyncTexture2D background,
            Rectangle windowRegion,
            Rectangle contentRegion,
            Point minSize)
            : base(background, windowRegion, contentRegion)
        {
            _minSize = minSize;
            CanResize = true;
            SavesSize = true;

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
            return new Point(
                Math.Max(newSize.X, _minSize.X),
                Math.Max(newSize.Y, _minSize.Y));
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

        private void ClampToMinimum()
        {
            if (this.Width >= _minSize.X && this.Height >= _minSize.Y)
            {
                return;
            }

            this.Size = new Point(
                Math.Max(this.Width, _minSize.X),
                Math.Max(this.Height, _minSize.Y));
        }
    }
}
