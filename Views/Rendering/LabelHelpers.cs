using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace GW2CraftingHelper.Views.Rendering
{
    // M38 WP-21 (Tier-1 static renderer extraction, m38-a1-architecture.md
    // S3b-T1): moved verbatim out of CraftingPlanView's "11. Generic
    // control/format helpers" region (KNOWN-ISSUES #23 / DO-NOT-TOUCH #6 -
    // CreateRowDivider's divider math and its M36b 1px scissor clearance
    // constants move byte-identical, unchanged from below) - private
    // static -> internal static, no logic changes. Callers in
    // CraftingPlanView now qualify as LabelHelpers.CreateRowDivider /
    // LabelHelpers.CreateRightAlignedLabel / LabelHelpers.CreateSmallTag /
    // LabelHelpers.EllipsizeToWidth. This class takes no dependency back on
    // CraftingPlanView (CreateSmallTag's pill colors are resolved by the
    // caller and passed in) - review fix (WP-21 findings pass): the initial
    // move had CreateSmallTag call CraftingPlanView.GetPillColors directly,
    // which was a reverse Rendering -> CraftingPlanView edge; removed so
    // this namespace stays a true leaf.
    internal static class LabelHelpers
    {
        // Only consumer is CreateRowDivider below - moved alongside it from
        // CraftingPlanView's shared "General" constants (it was declared
        // next to SectionDividerColor there, which stays put and is used
        // elsewhere in that file).
        private static readonly Color RowDividerColor = new Color(100, 100, 100);

        /// <summary>
        /// 2px divider at the bottom edge of a row panel - the shared "list
        /// row" chrome used by every table-style section except the tree
        /// (which uses indent guidelines instead, per gw2e's own convention).
        /// M36: was 1px, bottom-anchored via rowHeight - 1. Blish applies
        /// its UI-scale (e.g. the "Normal" GW2 UI size's 0.897) as a real
        /// GPU scale matrix, not an integer-pixel-snapped one, so a 1px-tall
        /// quad rasterizes to 0.897 physical pixels - guaranteed physical
        /// coverage is floor(0.897) = 0, i.e. it can disappear entirely
        /// depending on scroll-offset sub-pixel alignment (KNOWN-ISSUES
        /// #23). At 2px, floor(2 * 0.897) = floor(1.794) = 1 guarantees at
        /// least one covered physical scanline for the divider's OWN
        /// quad-vs-scissor math analyzed in isolation.
        ///
        /// M36b (KNOWN-ISSUES #23 follow-up): that isolated argument is
        /// necessary but not sufficient. rowPanel is itself a Container, and
        /// every Container.Paint() performs a SECOND, independent
        /// floor/ceil round trip - it unscales the physical scissor it was
        /// just given back to logical space (ScaleBy(1/UIScaleMultiplier))
        /// before re-intersecting and re-scaling it for its own children
        /// (Container.cs:377-381, Control.cs:1176-1177 in the decompiled
        /// Blish HUD binary). That round trip can shrink the clip rectangle
        /// propagated to the divider by exactly 1 logical pixel, but
        /// provably only at the row's BOTTOM edge (the reconstructed START
        /// never exceeds the true start - floor(floor(Y*s)/s) &lt;= Y for any
        /// positive scale s). Whether that 1px shrink actually deletes the
        /// divider depends on rowHeight: simulation across every rowHeight
        /// in this file and all four GW2 UI Size scale factors (0.81 /
        /// 0.897 / 1.0 / 1.103) shows 44px rows (CraftStepRowHeight,
        /// RecipeRowHeightWithSublabel) and 32px rows (DisciplineRowHeight)
        /// vanish completely (0 physical scanlines) at ~10.2% of scroll
        /// phases at the default scale; 36px rows (UsedMaterialRowHeight,
        /// ShoppingRowHeight, RecipeRowHeightNoSublabel) are immune at every
        /// tested scale.
        ///
        /// Fix: bottomClearance - an extra logical pixel of gap between the
        /// divider and rowHeight, i.e. Location.Y = rowHeight - 2 -
        /// bottomClearance. This moves the divider's own interval entirely
        /// inside the worst-case-shrunk clip window, which simulation
        /// confirms is immune (0/5000 vanishes) for every (rowHeight, scale)
        /// pair tested - proven, not just observed clean at one scale.
        /// Callers pass 1 for the vulnerable 44px/32px row types above and 0
        /// for the immune 36px row types (CreateUsedMaterialRow,
        /// CreateShoppingRow, CreateRecipeRow's no-sublabel branch) - those
        /// three were tuned in M36 to a flush icon(0..34) + divider(34..36)
        /// fit with zero slack, and giving them clearance they don't need
        /// would reintroduce the icon/divider overlap M36 fixed.
        /// </summary>
        internal static Panel CreateRowDivider(Panel rowPanel, int panelWidth, int rowHeight, int bottomClearance)
        {
            return new Panel()
            {
                Size = new Point(panelWidth, 2),
                Location = new Point(0, rowHeight - 2 - bottomClearance),
                BackgroundColor = RowDividerColor,
                Parent = rowPanel
            };
        }

        internal static Label CreateRightAlignedLabel(
            Panel parent, string text, BitmapFont font, Color color, int rightEdgeX, int y)
        {
            int width = (int)System.Math.Ceiling(font.MeasureString(text ?? "").Width);
            return new Label()
            {
                Text = text ?? "",
                Font = font,
                TextColor = color,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(rightEdgeX - width, y),
                Parent = parent
            };
        }

        /// <summary>
        /// Small grey informational tag - used for the shopping list's
        /// source tag and anywhere else a short non-interactive label needs
        /// pill chrome. border/fill are supplied by the caller (typically
        /// the tree's Locked pill styling, via CraftingPlanView.GetPillColors)
        /// so this helper never has to call back into the view it was
        /// extracted from.
        /// </summary>
        internal static Panel CreateSmallTag(Panel parent, string text, int x, int y, Color border, Color fill)
        {
            var font = GameService.Content.DefaultFont12;
            int textWidth = (int)System.Math.Ceiling(font.MeasureString(text).Width);
            int width = textWidth + 12;

            var outer = new Panel()
            {
                Size = new Point(width, 18),
                Location = new Point(x, y),
                BackgroundColor = border,
                Parent = parent
            };
            var inner = new Panel()
            {
                Size = new Point(width - 2, 16),
                Location = new Point(1, 1),
                BackgroundColor = fill,
                Parent = outer
            };
            new Label()
            {
                Text = text,
                Font = font,
                // White, not border: the fill exposes the border hue behind
                // the label, so border-colored text has zero contrast
                // against its own backdrop - same fix as RenderDecisionPills
                // (M30 #11); KNOWN-ISSUES #15 is this same bug on this tag.
                TextColor = Color.White,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point((width - 2 - textWidth) / 2, 1),
                Parent = inner
            };

            return outer;
        }

        /// <summary>
        /// Truncates text to fit maxWidth, appending "..." when it doesn't
        /// fit whole. Binary-searches the longest prefix (rather than
        /// trimming one character at a time) since MeasureString is not
        /// free and item names can run long.
        /// </summary>
        internal static string EllipsizeToWidth(BitmapFont font, string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            if (maxWidth <= 0) return "";

            int fullWidth = (int)System.Math.Ceiling(font.MeasureString(text).Width);
            if (fullWidth <= maxWidth) return text;

            const string ellipsis = "...";
            int ellipsisWidth = (int)System.Math.Ceiling(font.MeasureString(ellipsis).Width);
            if (ellipsisWidth >= maxWidth)
            {
                // Degenerate (extremely narrow column): still show the
                // ellipsis rather than nothing, so the row reads as
                // "truncated" instead of "blank/broken".
                return ellipsis;
            }

            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                int width = (int)System.Math.Ceiling(font.MeasureString(text.Substring(0, mid)).Width) + ellipsisWidth;
                if (width <= maxWidth) lo = mid; else hi = mid - 1;
            }
            return lo <= 0 ? ellipsis : text.Substring(0, lo) + ellipsis;
        }
    }
}
