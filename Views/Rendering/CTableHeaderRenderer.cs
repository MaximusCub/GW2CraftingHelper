using Blish_HUD;
using Blish_HUD.Controls;
using GW2CraftingHelper.Services;
using Microsoft.Xna.Framework;
using System;

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
    // Recipe Tree's "Cost" header sits over a cost column that is no longer
    // pinned to the panel edge (audit batch H pulls the whole pill+cost
    // block in beside the names), so it has to track the same
    // PlanRelayoutMath.ComputeTreeColumnEdges arithmetic its rows do.
    // Omitting it keeps the previous panelWidth-8 anchor for every other
    // caller.
    //
    // Chrome (band color, font, label color, height, label y) comes from
    // the shared TableHeaderStyle - see that class for the L3 inventory and
    // the reason the band, rather than the Shopping List's lighter
    // treatment, is the one every plan table now uses.
    //
    // It also bounds the header BAND: this row's whole point is the dark
    // background behind the column names, and a band spanning the
    // full panel width no longer bounds the columns it belongs to once those
    // columns have been pulled in (audit batch H fix round). The band ends
    // one TableRightMargin past the right column, which is exactly the panel
    // width for a caller whose columns are still pinned.
    internal static class CTableHeaderRenderer
    {
        internal static void CreateCTableHeaderRow(
            FlowPanel parent, int panelWidth, string leftLabel, int leftX, string rightLabel, ISectionRelayoutSink sink,
            string middleLabel = null, int middleX = 0, Func<int, int> middleXForWidth = null,
            Func<int, int> rightXForWidth = null)
        {
            var rowPanel = new Panel()
            {
                Size = new Point(BandWidth(rightXForWidth, panelWidth), TableHeaderStyle.RowHeight),
                BackgroundColor = TableHeaderStyle.BandColor,
                Parent = parent
            };
            var font = TableHeaderStyle.Font;
            LabelHelpers.WithDescenderClearance(new Label()
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
            });
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
