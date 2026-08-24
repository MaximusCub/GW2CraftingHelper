using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView's "11. Generic
    // control/format helpers" region (KNOWN-ISSUES #23 -
    // CreateRowDivider's divider math and its 1px scissor clearance
    // constants move byte-identical, unchanged from below) - private
    // static -> internal static, no logic changes. Callers in
    // CraftingPlanView now qualify as LabelHelpers.CreateRowDivider /
    // LabelHelpers.CreateRightAlignedLabel / LabelHelpers.CreateSmallTag /
    // LabelHelpers.EllipsizeToWidth. This class takes no dependency back on
    // CraftingPlanView (CreateSmallTag's pill colors are resolved by the
    // caller and passed in), so
    // this namespace stays a true leaf with no reverse edge back into
    // CraftingPlanView.
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
        /// Was 1px, bottom-anchored via rowHeight - 1. Blish applies
        /// its UI-scale (e.g. the "Normal" GW2 UI size's 0.897) as a real
        /// GPU scale matrix, not an integer-pixel-snapped one, so a 1px-tall
        /// quad rasterizes to 0.897 physical pixels - guaranteed physical
        /// coverage is floor(0.897) = 0, i.e. it can disappear entirely
        /// depending on scroll-offset sub-pixel alignment (KNOWN-ISSUES
        /// #23). At 2px, floor(2 * 0.897) = floor(1.794) = 1 guarantees at
        /// least one covered physical scanline for the divider's OWN
        /// quad-vs-scissor math analyzed in isolation.
        ///
        /// M36b: that isolated argument is
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
        /// three were tuned to a flush icon(0..34) + divider(34..36)
        /// fit with zero slack, and giving them clearance they don't need
        /// would reintroduce the icon/divider overlap that fix removed.
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

        /// <summary>
        /// Extra height a text label needs on top of its font's measured
        /// text height so a descender ('y', 'g', 'p') survives to the
        /// screen. AutoSizeHeight sizes a Label to exactly that measured
        /// height, and Blish then clips the label's own paint to its own
        /// bounds - so the descender lands in the last row or two of the
        /// clip window, where the floor/ceil scissor round trip documented
        /// on <see cref="CreateRowDivider"/> can shave it off - so whether a
        /// given descender survives depends on the scroll phase and the GW2
        /// UI scale, which is why it reads as intermittent. Field test, bug
        /// 5: character names in Required Disciplines lost the tail of
        /// their 'y'. Two pixels, matching the Log tab's row metrics, which
        /// have measured their own rows as Measure(font, "Ag").Height + 2
        /// since they were written.
        /// </summary>
        internal const int DescenderClearance = 2;

        /// <summary>
        /// Pins a label to its measured text height plus
        /// <see cref="DescenderClearance"/> in place of AutoSizeHeight, and
        /// returns it so it can wrap an object initializer in place.
        /// Measured with the label's own font, so a call site cannot pass
        /// one font and render in another. Width is untouched
        /// (AutoSizeWidth still governs it), so nothing that measures or
        /// right-aligns against a label's width moves. Single-line labels
        /// only - a later Text assignment of the same one-line shape keeps
        /// this height, which is the point.
        ///
        /// VerticalAlignment is pinned to Top, and that is what makes the
        /// extra height safe to apply to some labels in a row and not
        /// others. Blish_HUD.Controls.Label.VerticalAlignment is a public
        /// settable property whose default this module does not control; if
        /// it were Middle, growing a box by 2 would push its glyphs down by
        /// 1 while an unswept sibling on the same row stayed put, and a
        /// ragged baseline inside one sentence ("Craft 12x " + item name)
        /// is worse than the clip this fixes. Top makes the two pixels land
        /// entirely BELOW the glyphs, so a swept label renders at exactly
        /// the y it rendered at before - the change is additive clearance,
        /// never motion.
        /// </summary>
        internal static Label WithDescenderClearance(Label label)
        {
            var font = label?.Font;
            if (font == null) return label;

            label.VerticalAlignment = VerticalAlignment.Top;
            label.AutoSizeHeight = false;
            label.Height =
                (int)System.Math.Ceiling(font.MeasureString(label.Text ?? "").Height) + DescenderClearance;
            return label;
        }

        internal static Label CreateRightAlignedLabel(
            Panel parent, string text, BitmapFont font, Color color, int rightEdgeX, int y)
        {
            int width = (int)System.Math.Ceiling(font.MeasureString(text ?? "").Width);
            return WithDescenderClearance(new Label()
            {
                Text = text ?? "",
                Font = font,
                TextColor = color,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(rightEdgeX - width, y),
                Parent = parent
            });
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
            var font = UiFonts.Caption;
            int textWidth = (int)System.Math.Ceiling(font.MeasureString(text).Width);
            int width = MeasureSmallTagWidth(text);

            var outer = new Panel()
            {
                Size = new Point(width, SmallTagHeight),
                Location = new Point(x, y),
                BackgroundColor = border,
                Parent = parent
            };
            var inner = new Panel()
            {
                Size = new Point(width - 2, SmallTagHeight - 2),
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
                //; KNOWN-ISSUES #15 is this same bug on this tag.
                TextColor = Color.White,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point((width - 2 - textWidth) / 2, 1),
                Parent = inner
            };

            return outer;
        }

        /// <summary>
        /// Outer height of a small tag. 22, not the 18 it was at Font12: the
        /// label inside sits at y=1 in an inset panel of SmallTagHeight - 2
        /// and its lowest Font14 ink is y=20. Callers that centre a tag in a
        /// row read this rather than repeating the literal.
        /// </summary>
        internal const int SmallTagHeight = 22;

        /// <summary>
        /// Width CreateSmallTag will give a tag of this text, without
        /// building it - a caller that has to reserve room for the tag
        /// before laying out what sits left of it (the shopping list's name
        /// column) must not re-derive the +12 padding itself.
        /// </summary>
        internal static int MeasureSmallTagWidth(string text)
        {
            var font = UiFonts.Caption;
            return (int)System.Math.Ceiling(font.MeasureString(text ?? "").Width) + 12;
        }

        /// <summary>
        /// Truncates text to fit maxWidth, appending "..." when it doesn't
        /// fit whole. The arithmetic itself lives in the Blish-free
        /// TextWrapMath.Ellipsize (moved there so the Notes wrapper can
        /// reach the same truncation without a font); this is the font
        /// adapter every existing call site keeps calling.
        /// </summary>
        internal static string EllipsizeToWidth(BitmapFont font, string text, int maxWidth)
        {
            return TextWrapMath.Ellipsize(text, maxWidth, MeasureWith(font));
        }

        /// <summary>
        /// The measurement seam TextWrapMath takes in place of a font -
        /// same Ceiling(MeasureString(...).Width) every label-width
        /// calculation in this namespace already uses.
        /// </summary>
        internal static System.Func<string, int> MeasureWith(BitmapFont font)
        {
            return s => (int)System.Math.Ceiling(font.MeasureString(s ?? "").Width);
        }
    }
}
