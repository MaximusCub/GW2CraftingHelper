using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// THE item-icon component: every item, currency and search-result icon
    /// is built here, so all get one rarity frame (neutral at unknown
    /// rarity, never guessed), one empty-slot placeholder, and one hover
    /// wiring stamped on every control in the icon's tree - Blish resolves
    /// a tooltip on the deepest control and never bubbles.
    /// <see cref="CreateUnframedIcon"/> and <see cref="CreateAssetIcon"/>
    /// are the only unframed paths, and say why themselves.
    /// <para>
    /// THE RULE, stated once: every framed item icon goes through
    /// <see cref="CreateItemIcon"/> at a named <see cref="ItemIconTier"/>,
    /// with an <see cref="ItemIconFrame"/> that says why it is the colour it
    /// is and an <see cref="ItemIconTooltip"/> that says what it shows on
    /// hover. No pixel size, no rarity string and no hover reaches this file
    /// from a call site that did not name one.
    /// </para>
    /// </summary>
    internal static class IconControls
    {
        // --- Icon helper ---

        /// <summary>
        /// THE item icon. Every framed icon in the module is built here, at a
        /// NAMED <see cref="ItemIconTier"/>, with an explicit
        /// <see cref="ItemIconFrame"/> and an explicit
        /// <see cref="ItemIconTooltip"/> - no defaults of any kind.
        /// <para>
        /// The frame's thickness comes from the tier
        /// (<c>ItemIconTiers.BorderThickness</c>), never from the caller, so two
        /// icons at the same tier cannot differ. Reserve room with
        /// <c>ItemIconTiers.FrameSize(tier)</c> - art plus both borders.
        /// </para>
        /// <para>
        /// Returns the outer frame Panel so a caller whose icon position depends
        /// on panelWidth (currently only the plan header's centered title) can
        /// reposition it without recreating it. The hover is stamped on the
        /// WHOLE tree here, so a caller cannot build an icon and forget it - it
        /// can only decide, out loud, that the icon stays silent.
        /// </para>
        /// docs/ARCHITECTURE.md, "Views: relocated design narrative".
        /// </summary>
        internal static Panel CreateItemIcon(
            Panel parent, string iconUrl, ItemIconFrame frame, int x, int y,
            ItemIconTier tier, ItemIconTooltip tooltip)
        {
            return CreateFramedIcon(
                parent, iconUrl, frame.Color, x, y,
                ItemIconTiers.ArtSize(tier), ItemIconTiers.BorderThickness(tier), tooltip);
        }

        /// <summary>
        /// The pre-tier signature, kept ONLY so the one call site still owned
        /// by an in-flight branch keeps compiling until it migrates; the
        /// defaults are gone so nothing new can drift into it by accident,
        /// and the tests workflow's "Every item icon renders at a named
        /// tier" step allow-lists exactly that file and fails on any other
        /// caller.
        ///
        /// <para>
        /// The only remaining caller is Views/RankerTabContent.cs, whose
        /// shortfall cells become ItemIconTier.CurrencyBarRun. That one is
        /// NOT pixel-neutral: it passes the wallet-bar measurement as ART
        /// and frames outside it, so its box is 18 where the measured window
        /// is 16, and the tier insets as the other two currency sites
        /// already do. Whoever owns that file makes the call.
        /// </para>
        /// </summary>
        internal static Panel CreateItemIcon(
            Panel parent, string iconUrl, string rarity, int x, int y,
            int iconSize, int borderThickness, ItemIconTooltip tooltip)
        {
            return CreateFramedIcon(
                parent, iconUrl, RarityColors.GetRarityBorderColor(rarity), x, y,
                iconSize, borderThickness, tooltip);
        }

        /// <summary>
        /// The explicit-colour twin of the pre-tier signature above, kept on
        /// the same terms and for the same allow-listed callers.
        /// </summary>
        internal static Panel CreateItemIcon(
            Panel parent, string iconUrl, Color frameColor, int x, int y,
            int iconSize, int borderThickness, ItemIconTooltip tooltip)
        {
            return CreateFramedIcon(
                parent, iconUrl, frameColor, x, y, iconSize, borderThickness, tooltip);
        }

        private static Panel CreateFramedIcon(
            Panel parent, string iconUrl, Color frameColor, int x, int y,
            int iconSize, int borderThickness, ItemIconTooltip tooltip)
        {
            int frameSize = iconSize + borderThickness * 2;
            var frame = new Panel()
            {
                Size = new Point(frameSize, frameSize),
                Location = new Point(x, y),
                BackgroundColor = frameColor,
                Parent = parent,
            };
            CreateUnframedIcon(frame, iconUrl, borderThickness, borderThickness, iconSize, tooltip.PlainText);

            // Thin, but hoverable: an unstamped frame is a hole in the
            // icon's hover. From the SAME resolution the square gets.
            TooltipFacility.ApplyPlain(frame, ResolveTooltip(iconUrl, tooltip.PlainText));

            // The rich half goes on last and on the whole tree, over the
            // plain notes just written: a builder that composes nothing
            // keeps them as its fallback (TooltipFacility.Register).
            tooltip.StampOnIconTree(frame);
            return frame;
        }

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
        /// Stamps a deferred rich builder on a framed icon AND everything nested
        /// inside it. Blish resolves a tooltip on the deepest control under the
        /// cursor and never bubbles to the parent, so stamping the frame alone
        /// leaves the hover swallowed by the icon square that covers all but its
        /// border - and the square swallowed in turn by its missing-icon
        /// placeholder mark.
        /// <para>
        /// It cannot skip an empty payload, because nothing is composed yet - a
        /// row having a real item id does NOT make its builder non-empty. What
        /// keeps the icon's own note from being replaced with silence is
        /// <c>TooltipFacility</c>, which captures each control's plain text as
        /// the builder's fallback.
        /// </para>
        /// Reached through <see cref="ItemIconTooltip.StampOnIconTree"/>; there
        /// is deliberately no eager or plain-text twin. Why:
        /// docs/ARCHITECTURE.md, "Views: relocated design narrative".
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
