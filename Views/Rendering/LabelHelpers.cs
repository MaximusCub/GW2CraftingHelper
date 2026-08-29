using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using TaimisToolbench.Services;

namespace TaimisToolbench.Views.Rendering
{
    // A leaf: nothing here refers back to CraftingPlanView (CreateSmallTag's
    // pill colors are resolved by the caller and passed in).
    internal static class LabelHelpers
    {
        // Only consumer is CreateRowDivider below. Distinct from
        // CraftingPlanView's SectionDividerColor, which divides sections.
        private static readonly Color RowDividerColor = new Color(100, 100, 100);

        /// <summary>
        /// 2px divider at the bottom edge of a row panel - the shared "list row"
        /// chrome used by every table-style section except the tree (which uses
        /// indent guidelines instead, per gw2e's own convention).
        /// <para>
        /// Both constants are derived, not chosen. 2px, not 1px: Blish applies
        /// its UI scale (the "Normal" GW2 UI size is 0.897) as a real GPU scale
        /// matrix, so a 1px quad rasterizes to floor(0.897) = 0 guaranteed
        /// physical pixels and can vanish (KNOWN-ISSUES #23). bottomClearance
        /// adds a logical pixel - Location.Y = rowHeight - 2 - bottomClearance -
        /// putting the divider inside the worst-case shrunk clip window.
        /// </para>
        /// <para>
        /// Which heights need clearance is not a judgement call:
        /// RowDividerScissorSimulationTests sweeps every shipped (rowHeight,
        /// clearance) pair at all four GW2 UI scales and fails on any vanish.
        /// Icon-led rows pass PlanContentHeightMath.IconRowDividerClearance (1).
        /// </para>
        /// docs/ARCHITECTURE.md, "Views: relocated design narrative".
        /// </summary>
        internal static Panel CreateRowDivider(Panel rowPanel, int panelWidth, int rowHeight, int bottomClearance)
        {
            return new Panel()
            {
                Size = new Point(panelWidth, 2),
                Location = new Point(0, rowHeight - 2 - bottomClearance),
                BackgroundColor = RowDividerColor,
                Parent = rowPanel,
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
        /// <para>
        /// VerticalAlignment is pinned to Top, and that is what makes the extra
        /// height safe to apply to some labels in a row and not others: the two
        /// pixels land entirely BELOW the glyphs, so a swept label renders at
        /// exactly the y it rendered at before - additive clearance, never
        /// motion.
        /// </para>
        /// Why Top and not Blish's default: docs/ARCHITECTURE.md, "Views:
        /// relocated design narrative".
        /// </summary>
        internal static Label WithDescenderClearance(Label label)
        {
            var font = label?.Font;
            if (font == null)
            {
                return label;
            }

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
                Parent = parent,
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
                Parent = parent,
            };
            var inner = new Panel()
            {
                Size = new Point(width - 2, SmallTagHeight - 2),
                Location = new Point(1, 1),
                BackgroundColor = fill,
                Parent = outer,
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
                Parent = inner,
            };

            return outer;
        }

        /// <summary>
        /// One tag's hover, stamped on the outer panel, its inset fill
        /// panel and the label inside that. Blish resolves a tooltip on the
        /// deepest capturing control under the cursor and never bubbles,
        /// and <see cref="CreateSmallTag"/>'s two inner controls cover
        /// almost the whole tag - a stamp on the returned outer panel alone
        /// would fire on a 1px border and nowhere else. Null clears all
        /// three (see TooltipFacility.ApplyPlain).
        /// </summary>
        internal static void ApplyTagTooltip(Control control, string text)
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
                    ApplyTagTooltip(child, text);
                }
            }
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
