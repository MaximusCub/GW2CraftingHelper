using System;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    /// <summary>
    /// TabbedWindow2 subclass that enforces a minimum window size,
    /// matching the behavior of ResizableModuleWindow for StandardWindow.
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
    }
}
