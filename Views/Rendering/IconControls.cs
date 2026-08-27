using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// THE item-icon component: every item, currency and search-result icon
    /// is built here, so all get one rarity frame (neutral at unknown
    /// rarity, never guessed), one empty-slot placeholder, and one hover
    /// wiring stamped on every control in the icon's tree - Blish resolves
    /// a tooltip on the deepest control and never bubbles.
    /// <see cref="CreateUnframedIcon"/> and <see cref="CreateAssetIcon"/>
    /// are the only unframed paths, and say why themselves.
    /// </summary>
    internal static class IconControls
    {
        // --- Icon helper ---

        /// <summary>
        /// Item icon inside a rarity-colored frame. Defaults to the legacy
        /// small size (32px icon, 1px border = 34px overall), still used by
        /// the compact sites outside the two-tier ruling (wallet rows,
        /// search suggestions, Plan History); the Crafting Plan tab's rows
        /// pass ItemIconTiers.BagSidebarIconSize and the plan header /
        /// Snapshot grid / Ranker rows pass ItemIconTiers.BagSlotIconSize.
        /// <paramref name="tooltipText"/> is for an icon whose subject is
        /// not already spelled out beside it; callers wanting the full item
        /// hover stamp it after, via <see cref="ApplyRichToIconTree"/>.
        /// </summary>
        internal static Panel CreateItemIcon(
            Panel parent, string iconUrl, string rarity, int x, int y,
            int iconSize = 32, int borderThickness = 1, string tooltipText = null)
        {
            return CreateItemIcon(
                parent, iconUrl, RarityColors.GetRarityBorderColor(rarity), x, y,
                iconSize, borderThickness, tooltipText);
        }

        /// <summary>
        /// Same as above with an explicit frame color, for dimmed
        /// not-crafted subtree rows (neutral grey frame instead of rarity).
        /// Returns the outer frame Panel so a caller whose icon position
        /// depends on panelWidth (currently only the plan header's centered
        /// title) can reposition it on relayout without recreating it.
        /// </summary>
        internal static Panel CreateItemIcon(
            Panel parent, string iconUrl, Color frameColor, int x, int y,
            int iconSize = 32, int borderThickness = 1, string tooltipText = null)
        {
            int frameSize = iconSize + borderThickness * 2;
            var frame = new Panel()
            {
                Size = new Point(frameSize, frameSize),
                Location = new Point(x, y),
                BackgroundColor = frameColor,
                Parent = parent,
            };
            CreateUnframedIcon(frame, iconUrl, borderThickness, borderThickness, iconSize, tooltipText);

            // Thin, but hoverable: an unstamped frame is a hole in the
            // icon's hover. From the SAME resolution the square gets.
            TooltipFacility.ApplyPlain(frame, ResolveTooltip(iconUrl, tooltipText));
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

        /// <summary>The frame's interior, and the unframed icon for
        /// CoinCurrencyRenderer's inline runs, where a frame would add 2px
        /// to every segment's advance - a term in the minimum-window-width
        /// derivation - around something with no rarity.</summary>
        internal static Panel CreateUnframedIcon(
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
                    Parent = parent,
                }
                : new Panel()
                {
                    Size = new Point(size, size),
                    Location = new Point(x, y),
                    BackgroundTexture = GameService.Content.GetRenderServiceTexture(iconUrl),
                    Parent = parent,
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
                    Parent = icon,
                };
            }

            // An icon's tooltip is almost always an item name, which is
            // unbounded - through the facility, not assigned raw. Stamped
            // on the placeholder mark as well as the square: Blish resolves
            // a tooltip on the deepest control under the cursor and never
            // bubbles to the parent, so the mark would otherwise swallow
            // the hover in the exact middle of the square.
            string resolvedTooltip = ResolveTooltip(iconUrl, tooltipText);
            TooltipFacility.ApplyPlain(icon, resolvedTooltip);
            if (placeholderMark != null)
            {
                TooltipFacility.ApplyPlain(placeholderMark, resolvedTooltip);
            }

            return icon;
        }

        /// <summary>The unframed path for art that ships with the game: a
        /// coin denomination, by asset id. No missing-art branch - an asset
        /// id is a constant, so there is no data gap to degrade.</summary>
        internal static Panel CreateAssetIcon(
            Panel parent, int assetId, int x, int y, int size, string tooltipText)
        {
            var icon = new Panel()
            {
                Size = new Point(size, size),
                Location = new Point(x, y),
                BackgroundTexture = AsyncTexture2D.FromAssetId(assetId),
                Parent = parent,
            };

            TooltipFacility.ApplyPlain(icon, tooltipText);
            return icon;
        }

        /// <summary>What an icon says on hover: the caller's text, or the
        /// missing-icon note when there is neither art nor text. One rule,
        /// so frame and square cannot disagree.</summary>
        private static string ResolveTooltip(string iconUrl, string tooltipText)
        {
            return string.IsNullOrEmpty(iconUrl) && string.IsNullOrEmpty(tooltipText)
                ? NoIconTooltip
                : tooltipText;
        }

        /// <summary>
        /// The plain-text twin of <see cref="ApplyRichToIconTree"/>, for a
        /// row whose tooltip is composed prose. A null text CLEARS, unlike
        /// the rich version's empty no-op: that is how a row that no longer
        /// truncates retracts its note.
        /// </summary>
        internal static void ApplyPlainToIconTree(Control control, string text)
        {
            if (control == null)
            {
                return;
            }

            TooltipFacility.ApplyPlain(control, text);

            if (control is Container container)
            {
                foreach (var child in container.Children)
                {
                    ApplyPlainToIconTree(child, text);
                }
            }
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

        /// <summary>
        /// The deferred twin of <see cref="ApplyRichToIconTree"/>, for a
        /// row whose content is composed at hover time (see
        /// <c>TooltipFacility.ApplyRichDeferred</c>). Unlike the eager
        /// version this cannot skip an empty payload, because nothing is
        /// composed yet - a row having a real item id does NOT make its
        /// builder non-empty, since a plan restored from disk has no stat
        /// blocks until the background top-up lands and a row whose name is
        /// short enough not to ellipsize composes nothing at all until
        /// then. What keeps the icon's own note ("no icon available for
        /// this entry", a currency name) from being replaced with silence
        /// is <c>TooltipFacility</c>, which captures each control's plain
        /// text as the builder's fallback.
        /// </summary>
        internal static void ApplyRichDeferredToIconTree(Control control, System.Func<TooltipContent> build)
        {
            if (control == null || build == null)
            {
                return;
            }

            TooltipFacility.ApplyRichDeferred(control, build);

            if (control is Container container)
            {
                foreach (var child in container.Children)
                {
                    ApplyRichDeferredToIconTree(child, build);
                }
            }
        }
    }
}
