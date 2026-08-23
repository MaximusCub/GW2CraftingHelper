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
    public class ResizableTabbedWindow : TabbedWindow2
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

            // The base constructor sizes the window from windowRegion, which
            // is a region of the background texture and is narrower than the
            // minimum. Clamping here rather than waiting for the first
            // layout pass means the hosted views are never built against a
            // content region they will immediately be resized out of.
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
