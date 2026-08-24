using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView's "11. Generic
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

        // tooltipText (anywhere a currency icon shows, its name must be
        // available on hover)
        // defaults to null - every pre-existing caller (item icons, whose
        // name already renders as adjacent text) is unaffected; only a new
        // caller that opts in by passing it gets a hover tooltip.
        // What a missing icon says instead of an item name. Assigned only
        // when the caller supplied no tooltip of its own, so a currency
        // icon still names its currency.
        private const string NoIconTooltip = "No icon available for this entry.";

        // The placeholder's mark. ASCII, per this repo's standing finding
        // that the Blish font does not reliably render the glyphs an
        // "empty slot" would otherwise want (see CraftingPlanView's
        // caret comment).
        private const string NoIconGlyph = "-";

        internal static Panel CreateItemIcon(
            Panel parent, string iconUrl, int x, int y, int size = 32, string tooltipText = null)
        {
            // Missing icon: render a neutral empty-slot square, not the
            // alarming red error texture - a data gap is not a failure.
            bool missing = string.IsNullOrEmpty(iconUrl);
            Panel icon = missing
                ? new Panel()
                {
                    Size = new Point(size, size),
                    Location = new Point(x, y),
                    BackgroundColor = new Color(45, 45, 45),
                    Parent = parent
                }
                : new Panel()
                {
                    Size = new Point(size, size),
                    Location = new Point(x, y),
                    BackgroundTexture = GameService.Content.GetRenderServiceTexture(iconUrl),
                    Parent = parent
                };

            // The bare square reads as a HOLE in the icon column rather
            // than as an entry without an icon - the reported Snapshot
            // "Spirit Shards" row. A dim centered mark plus a tooltip says
            // which it is. Deliberately marks the square rather than
            // collapsing the column for that row: an un-iconed row whose
            // text starts 32px left of every other row's is a worse
            // artifact than a quiet placeholder, and the plan's tables
            // derive their name column from a fixed x that a per-row
            // collapse would break.
            //
            // Built only on the missing path, so the common case allocates
            // nothing extra.
            Label placeholderMark = null;
            if (missing && size > 0)
            {
                var font = UiFonts.Body;
                var glyphSize = font.MeasureString(NoIconGlyph);
                placeholderMark = new Label()
                {
                    Text = NoIconGlyph,
                    Font = font,
                    TextColor = new Color(110, 110, 110),
                    AutoSizeWidth = true,
                    AutoSizeHeight = true,
                    Location = new Point(
                        (size - (int)System.Math.Ceiling(glyphSize.Width)) / 2,
                        (size - (int)System.Math.Ceiling(glyphSize.Height)) / 2),
                    Parent = icon
                };
            }

            // An icon's tooltip is almost always an item name, which is
            // unbounded - through the facility, not assigned raw. Stamped
            // on the placeholder mark as well as the square: Blish resolves
            // a tooltip on the deepest control under the cursor and never
            // bubbles to the parent, so the mark would otherwise swallow
            // the hover in the exact middle of the square.
            string resolvedTooltip =
                missing && string.IsNullOrEmpty(tooltipText) ? NoIconTooltip : tooltipText;
            TooltipFacility.ApplyPlain(icon, resolvedTooltip);
            if (placeholderMark != null)
            {
                TooltipFacility.ApplyPlain(placeholderMark, resolvedTooltip);
            }
            return icon;
        }

        /// <summary>
        /// Stamps rich content on a framed icon AND everything nested
        /// inside it. Blish resolves a tooltip on the deepest control under
        /// the cursor and never bubbles to the parent, so stamping the
        /// frame alone leaves the hover swallowed by the icon square that
        /// covers all but its border - and the square swallowed in turn by
        /// its missing-icon placeholder mark. Same swallowed-hover class
        /// <see cref="CreateItemIcon"/> already handles for its own plain
        /// tooltip and TreeSectionController.UpdateTreeRowTooltip for a
        /// row's Labels.
        /// <para>
        /// Empty content is a no-op rather than a clear. The icon may
        /// already carry a plain tooltip this method did not set - the
        /// missing-icon note, or a currency name - and clearing would
        /// destroy information instead of replacing it. Real content still
        /// overwrites: an item's own stat block says strictly more than
        /// either.
        /// </para>
        /// </summary>
        internal static void ApplyRichToIconTree(Control control, TooltipContent content)
        {
            if (control == null || content == null || content.IsEmpty)
            {
                return;
            }

            TooltipFacility.ApplyRich(control, content);

            if (control is Container container)
            {
                foreach (var child in container.Children)
                {
                    ApplyRichToIconTree(child, content);
                }
            }
        }
    }
}
