using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Views.Rendering
{
    // Moved verbatim out of CraftingPlanView.CreateCTableHeaderRow -
    // private static -> internal static, no logic changes. Each c-table
    // section renderer (Disciplines/Recipes) owns its own header call
    // directly inside Render(), mirroring how ShoppingListSectionRenderer
    // owns CreateShoppingListHeaderRow; CraftingPlanView itself no longer
    // references the c-table header at all.
    //
    // Optional middleLabel/middleX so Required Disciplines
    // can honestly label its per-character availability text with a
    // "Characters" header once that text lines up on a fixed column (see
    // DisciplinesSectionRenderer's own comment on Render() for why the
    // column has to be fixed, not per-row). Defaulted to null/0 so the
    // Required Recipes call site (still a plain left/right header) needs no
    // change. Like leftLabel, middleLabel sits at a fixed X computed once
    // by the caller before this row is built.
    //
    // middleXForWidth exists for the ONE caller whose middle column is not
    // fixed: the Recipe Tree's "Source" header sits over the decision-pill
    // column, whose x is derived from the panel width
    // (PlanRelayoutMath.ComputeTreeColumnEdges), so a build-time constant
    // would strand it the moment the window is dragged. Supplying it opts
    // that label into the AddRelayout closure below; omitting it keeps the
    // previous fixed-x behaviour for every other caller. Still
    // position-only, per the interface's "position/width-only" contract -
    // see ISectionRelayoutSink.AddRelayout's doc comment.
    //
    // rightXForWidth is the same escape hatch for the right label: the
    // Recipe Tree's "Cost" header sits over a column whose x is derived
    // through PlanRelayoutMath.ComputeTreeColumnEdges rather than straight
    // off the panel edge, so it tracks the same arithmetic its rows do.
    // Omitting it anchors the label at PlanRelayoutMath.PinnedRightEdge,
    // which is what every flat table wants.
    //
    // Chrome (band color, font, label color, height, label y) comes from
    // the shared TableHeaderStyle - see that class for the L3 inventory and
    // the reason the band, rather than the Shopping List's lighter
    // treatment, is the one every plan table now uses.
    //
    // It also sizes the header BAND, which ends one TableRightMargin past
    // the right column - the full panel width for every caller whose
    // columns are pinned, i.e. all of them, and clamped to the panel for a
    // caller whose derived edge ever landed past it.
    // onLeftClick/onRightClick turn those two labels into sort controls
    // for the one caller that has a sortable table (Used Materials).
    // Omitted everywhere else, which leaves the label inert exactly as
    // before. The label text a sortable caller passes already carries its
    // sort indicator (SortableHeaderLabel.Decorate), so the right label's
    // x-tracking below - which right-aligns off the control's own Width -
    // accounts for the indicator without knowing about it.
    internal static class CTableHeaderRenderer
    {
        internal static void CreateCTableHeaderRow(
            FlowPanel parent, int panelWidth, string leftLabel, int leftX, string rightLabel, ISectionRelayoutSink sink,
            string middleLabel = null, int middleX = 0, Func<int, int> middleXForWidth = null,
            Func<int, int> rightXForWidth = null, Action onLeftClick = null, Action onRightClick = null)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(BandWidth(rightXForWidth, panelWidth), TableHeaderStyle.RowHeight),
                BackgroundColor = TableHeaderStyle.BandColor,
                Parent = parent
            };
            var font = TableHeaderStyle.Font;
            var leftLabelControl = LabelHelpers.WithDescenderClearance(new Label()
            {
                Text = leftLabel, Font = font, TextColor = TableHeaderStyle.LabelColor,
                AutoSizeWidth = true, AutoSizeHeight = true,
                Location = new Point(leftX, TableHeaderStyle.LabelY), Parent = rowPanel
            });
            Label middleLabelControl = null;
            if (!string.IsNullOrEmpty(middleLabel))
            {
                middleLabelControl = LabelHelpers.WithDescenderClearance(new Label()
                {
                    Text = middleLabel, Font = font, TextColor = TableHeaderStyle.LabelColor,
                    AutoSizeWidth = true, AutoSizeHeight = true,
                    Location = new Point(
                        middleXForWidth != null ? middleXForWidth(panelWidth) : middleX, TableHeaderStyle.LabelY),
                    Parent = rowPanel
                });
            }
            var rightLabelControl = LabelHelpers.CreateRightAlignedLabel(
                rowPanel, rightLabel, font, TableHeaderStyle.LabelColor,
                rightXForWidth != null
                    ? rightXForWidth(panelWidth)
                    : panelWidth - PlanRelayoutMath.TableRightMargin,
                TableHeaderStyle.LabelY);

            // The hit area is the whole cell, not the text - see
            // SortableHeaderCells. The labels only carry the note, because
            // a label swallows the hover of whatever is under it.
            if (onLeftClick != null) SortableHeaderLabel.MarkSortable(leftLabelControl);
            if (onRightClick != null) SortableHeaderLabel.MarkSortable(rightLabelControl);

            // Everything the cell split needs that does NOT change with the
            // panel width, resolved once: the labels, their measured widths
            // and their actions. Only the x's move on a resize, so the
            // closure below neither measures a string nor allocates.
            var plan = new HeaderCellPlan(
                middleLabelControl == null ? 2 : 3, new SortableHeaderCells(rowPanel));
            plan.Set(0, leftLabelControl, Measure(font, leftLabel), onLeftClick);
            if (middleLabelControl != null)
            {
                plan.Set(1, middleLabelControl, Measure(font, middleLabel), null);
            }
            plan.Set(plan.Count - 1, rightLabelControl, Measure(font, rightLabel), onRightClick);
            plan.Sync(rowPanel.Width);

            sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(BandWidth(rightXForWidth, w), TableHeaderStyle.RowHeight);
                int rightEdge = rightXForWidth != null ? rightXForWidth(w) : w - PlanRelayoutMath.TableRightMargin;
                rightLabelControl.Location = new Point(
                    PlanRelayoutMath.RightAlignedX(rightEdge, rightLabelControl.Width), TableHeaderStyle.LabelY);
                if (middleLabelControl != null && middleXForWidth != null)
                {
                    middleLabelControl.Location = new Point(middleXForWidth(w), TableHeaderStyle.LabelY);
                }

                // A right-pinned column's x is a function of the panel
                // width, so its cell has to follow it rather than stay
                // where the build-time width put it.
                plan.Sync(rowPanel.Width);
            });
        }

        /// <summary>
        /// Measured from the string rather than read off the control: a
        /// Blish Label's own Width is not settled until its next layout
        /// pass, and these cells are described in the same breath as the
        /// label is created (the same reason CreateRightAlignedLabel
        /// measures rather than reading Width).
        /// </summary>
        private static int Measure(BitmapFont font, string text)
        {
            return (int)Math.Ceiling(font.MeasureString(text ?? "").Width);
        }

        /// <summary>
        /// Width of the header's dark band: up to the right column plus the
        /// margin every plan table keeps past its block, never wider than the
        /// panel itself. Full width when the caller's right column is still
        /// pinned to the panel edge.
        /// </summary>
        private static int BandWidth(Func<int, int> rightXForWidth, int panelWidth)
        {
            if (rightXForWidth == null) return panelWidth;

            int width = rightXForWidth(panelWidth) + PlanRelayoutMath.TableRightMargin;
            if (width > panelWidth) width = panelWidth;
            return width > 0 ? width : 0;
        }
    }
}
