using System;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// TabbedWindow2 subclass that enforces a minimum window size,
    /// matching the behavior of ResizableModuleWindow for StandardWindow.
    /// Also clamps persisted sizes on layout so the window never opens
    /// smaller than the minimum.
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

            // Enforce minimum size on initial layout. Persisted sizes
            // from earlier sessions may be below the current minimum.
            if (this.Width < _minSize.X || this.Height < _minSize.Y)
            {
                this.Size = new Point(
                    Math.Max(this.Width, _minSize.X),
                    Math.Max(this.Height, _minSize.Y));
            }
        }
    }
}
