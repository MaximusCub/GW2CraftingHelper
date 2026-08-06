using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-21 (Tier-1 static renderer extraction, m38-a1-architecture.md
    // S3b-T1): moved verbatim out of CraftingPlanView's "11. Generic
    // control/format helpers" region - private static -> internal static,
    // no logic changes. Callers in CraftingPlanView now qualify as
    // IconControls.CreateRarityFramedIcon / IconControls.CreateItemIcon.
    internal static class IconControls
    {
        // --- Icon helper ---

        /// <summary>
        /// Item icon inside a rarity-colored frame. Defaults to the tree/row
        /// size (32px icon, 1px border = 34px overall); the plan header uses
        /// a larger 40px/2px variant (44px overall, gw2e's .tooltip-item).
        /// </summary>
        internal static Panel CreateRarityFramedIcon(
            Panel parent, string iconUrl, string rarity, int x, int y,
            int iconSize = 32, int borderThickness = 1)
        {
            return CreateRarityFramedIcon(
                parent, iconUrl, RarityColors.GetRarityBorderColor(rarity), x, y, iconSize, borderThickness);
        }

        /// <summary>
        /// Same as above with an explicit frame color, for dimmed
        /// not-crafted subtree rows (neutral grey frame instead of rarity).
        /// Returns the outer frame Panel so a caller whose icon position
        /// depends on panelWidth (currently only the plan header's centered
        /// title) can reposition it on relayout without recreating it.
        /// </summary>
        internal static Panel CreateRarityFramedIcon(
            Panel parent, string iconUrl, Color frameColor, int x, int y,
            int iconSize = 32, int borderThickness = 1)
        {
            int frameSize = iconSize + borderThickness * 2;
            var frame = new Panel()
            {
                Size = new Point(frameSize, frameSize),
                Location = new Point(x, y),
                BackgroundColor = frameColor,
                Parent = parent
            };
            CreateItemIcon(frame, iconUrl, borderThickness, borderThickness, iconSize);
            return frame;
        }

        // tooltipText (field-test finding B's name-tooltip sweep principle:
        // anywhere a currency icon shows, its name must be available)
        // defaults to null - every pre-existing caller (item icons, whose
        // name already renders as adjacent text) is unaffected; only a new
        // caller that opts in by passing it gets a hover tooltip.
        internal static Panel CreateItemIcon(
            Panel parent, string iconUrl, int x, int y, int size = 32, string tooltipText = null)
        {
            // Missing icon: render a neutral empty-slot square, not the
            // alarming red error texture - a data gap is not a failure.
            if (string.IsNullOrEmpty(iconUrl))
            {
                return new Panel()
                {
                    Size = new Point(size, size),
                    Location = new Point(x, y),
                    BackgroundColor = new Color(45, 45, 45),
                    BasicTooltipText = tooltipText,
                    Parent = parent
                };
            }

            return new Panel()
            {
                Size = new Point(size, size),
                Location = new Point(x, y),
                BackgroundTexture = GameService.Content.GetRenderServiceTexture(iconUrl),
                BasicTooltipText = tooltipText,
                Parent = parent
            };
        }
    }
}
