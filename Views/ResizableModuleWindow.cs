using System;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views
{
    public class ResizableModuleWindow : StandardWindow
    {
        private static readonly Point MinSize = new Point(450, 400);

        public ResizableModuleWindow(
            AsyncTexture2D background,
            Rectangle windowRegion,
            Rectangle contentRegion)
            : base(background, windowRegion, contentRegion)
        {
            CanResize = true;
            SavesSize = true;
        }

        protected override Point HandleWindowResize(Point newSize)
        {
            return new Point(
                Math.Max(newSize.X, MinSize.X),
                Math.Max(newSize.Y, MinSize.Y));
        }
    }
}
